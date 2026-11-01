using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Vcc.Vida.DiagSwdl.Documents
{
    public class CgmDecodeResult : IDisposable
    {
        public Bitmap Image { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<string, RectangleF> NormalizedHotspots { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public CgmDecodeResult()
        {
            NormalizedHotspots = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (Image != null)
            {
                Image.Dispose();
                Image = null;
            }
        }
    }

    public static class CgmDecoder
    {
        public static CgmDecodeResult Decode(byte[] data)
        {
            var res = new CgmDecodeResult();
            if (data == null || data.Length < 16)
            {
                res.ErrorMessage = "Empty or insufficient data";
                return res;
            }

            try
            {
                int pos = 0;
                int imgW = 0, imgH = 0;
                int vdcMinX = 0, vdcMinY = 0, vdcMaxX = 0, vdcMaxY = 0;
                byte[] rawG4 = null;

                string currentAps = null;
                string currentApsName = null;

                while (pos < data.Length - 1)
                {
                    ushort h = (ushort)((data[pos] << 8) | data[pos + 1]);
                    int ec = (h >> 12) & 0x0F;
                    int ei = (h >> 5) & 0x7F;
                    int len = h & 0x1F;
                    pos += 2;

                    MemoryStream payloadMs = new MemoryStream();

                    if (len == 31)
                    {
                        // Parameter continuation chunking
                        while (pos < data.Length - 1)
                        {
                            ushort w = (ushort)((data[pos] << 8) | data[pos + 1]);
                            pos += 2;
                            bool isContinuation = (w & 0x8000) != 0;
                            int chunkLen = w & 0x7FFF;

                            if (pos + chunkLen <= data.Length)
                            {
                                payloadMs.Write(data, pos, chunkLen);
                                pos += chunkLen;
                                if ((chunkLen % 2) == 1) pos++; // Word alignment padding
                            }
                            else break;

                            if (!isContinuation) break;
                        }
                    }
                    else
                    {
                        if (pos + len <= data.Length)
                        {
                            payloadMs.Write(data, pos, len);
                            pos += len;
                            if ((len % 2) == 1) pos++;
                        }
                    }

                    byte[] payload = payloadMs.ToArray();

                    // VDC Extent (Class 2, ei=6)
                    if (ec == 2 && ei == 6 && payload.Length >= 8)
                    {
                        vdcMinX = (short)((payload[0] << 8) | payload[1]);
                        vdcMinY = (short)((payload[2] << 8) | payload[3]);
                        vdcMaxX = (short)((payload[4] << 8) | payload[5]);
                        vdcMaxY = (short)((payload[6] << 8) | payload[7]);
                    }
                    // Begin Tile Array (Class 0, ei=19) - pixel dimensions at offsets 12 and 14
                    else if (ec == 0 && ei == 19 && payload.Length >= 16)
                    {
                        imgW = (payload[12] << 8) | payload[13];
                        imgH = (payload[14] << 8) | payload[15];
                    }
                    // Tile Array (Class 4, ei=29) - CCITT Group 4 bitonal raster tile
                    else if (ec == 4 && ei == 29 && payload.Length > 7)
                    {
                        int rawOffset = 7;
                        int rawLen = payload.Length - rawOffset;
                        rawG4 = new byte[rawLen];
                        Array.Copy(payload, rawOffset, rawG4, 0, rawLen);
                    }
                    // BEGIN APS (Class 0, ei=21)
                    else if (ec == 0 && ei == 21 && payload.Length > 0)
                    {
                        int sLen = payload[0];
                        if (sLen > 0 && 1 + sLen <= payload.Length)
                        {
                            currentAps = System.Text.Encoding.ASCII.GetString(payload, 1, sLen);
                        }
                        currentApsName = null;
                    }
                    // APPLICATION DATA (Class 9, ei=1)
                    else if (ec == 9 && ei == 1 && currentAps != null && payload.Length > 0)
                    {
                        int tLen = payload[0];
                        if (tLen > 0 && 1 + tLen <= payload.Length)
                        {
                            string tag = System.Text.Encoding.ASCII.GetString(payload, 1, tLen);
                            if (tag == "name" && payload.Length >= 12)
                            {
                                int nLen = payload[10];
                                if (11 + nLen <= payload.Length)
                                {
                                    currentApsName = System.Text.Encoding.ASCII.GetString(payload, 11, nLen).Trim();
                                }
                            }
                            else if (tag == "region" && payload.Length >= 26)
                            {
                                int off = 18;
                                short x1 = (short)((payload[off] << 8) | payload[off + 1]);
                                short y1 = (short)((payload[off + 2] << 8) | payload[off + 3]);
                                short x2 = (short)((payload[off + 4] << 8) | payload[off + 5]);
                                short y2 = (short)((payload[off + 6] << 8) | payload[off + 7]);

                                string key = currentApsName ?? currentAps;
                                if (key.StartsWith("POSID_", StringComparison.OrdinalIgnoreCase))
                                {
                                    key = key.Substring(6);
                                }
                                key = key.Trim();

                                float vdcW = (vdcMaxX - vdcMinX) > 0 ? (vdcMaxX - vdcMinX) : 1000f;
                                float vdcH = (vdcMaxY - vdcMinY) > 0 ? (vdcMaxY - vdcMinY) : 1000f;

                                float normX = (Math.Min(x1, x2) - vdcMinX) / vdcW;
                                float normY = (vdcMaxY - Math.Max(y1, y2)) / vdcH;
                                float normWidth = Math.Abs(x2 - x1) / vdcW;
                                float normHeight = Math.Abs(y2 - y1) / vdcH;

                                res.NormalizedHotspots[key] = new RectangleF(normX, normY, Math.Max(0.015f, normWidth), Math.Max(0.015f, normHeight));
                            }
                        }
                    }
                    // END APS (Class 0, ei=22)
                    else if (ec == 0 && ei == 22)
                    {
                        currentAps = null;
                        currentApsName = null;
                    }
                }

                if (rawG4 == null || rawG4.Length == 0)
                {
                    res.ErrorMessage = "No CCITT raster tile found in CGM";
                    return res;
                }

                if (imgW <= 0 || imgH <= 0)
                {
                    if (vdcMaxX > vdcMinX && vdcMaxY > vdcMinY)
                    {
                        imgW = (vdcMaxX - vdcMinX) * 3;
                        imgH = (vdcMaxY - vdcMinY) * 3;
                    }
                    else
                    {
                        imgW = 2479;
                        imgH = 3508;
                    }
                }

                res.Width = imgW;
                res.Height = imgH;

                // Wrap raw CCITT Group 4 into in-memory TIFF container
                using (MemoryStream ms = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write((byte)'I'); bw.Write((byte)'I');
                        bw.Write((short)42);
                        bw.Write((int)8); // offset to IFD

                        short numEntries = 8;
                        bw.Write(numEntries);

                        WriteTag(bw, 256, 4, 1, imgW); // ImageWidth (LONG)
                        WriteTag(bw, 257, 4, 1, imgH); // ImageLength (LONG)
                        WriteTag(bw, 258, 3, 1, 1);    // BitsPerSample (SHORT)
                        WriteTag(bw, 259, 3, 1, 4);    // Compression = 4 (CCITT T.6)
                        WriteTag(bw, 262, 3, 1, 0);    // Photometric = 0 (WhiteIsZero)

                        int dataOffset = 8 + 2 + numEntries * 12 + 4;
                        WriteTag(bw, 273, 4, 1, dataOffset); // StripOffsets (LONG)
                        WriteTag(bw, 278, 4, 1, imgH);       // RowsPerStrip (LONG)
                        WriteTag(bw, 279, 4, 1, rawG4.Length); // StripByteCounts (LONG)

                        bw.Write((int)0); // next IFD offset
                        bw.Write(rawG4);
                    }

                    byte[] tiffBytes = ms.ToArray();
                    using (MemoryStream loadMs = new MemoryStream(tiffBytes))
                    {
                        using (Image loaded = Image.FromStream(loadMs))
                        {
                            res.Image = new Bitmap(loaded);
                            res.Success = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res.ErrorMessage = ex.Message;
                res.Success = false;
            }

            return res;
        }

        private static void WriteTag(BinaryWriter bw, ushort tag, ushort type, uint count, int val)
        {
            bw.Write(tag);
            bw.Write(type);
            bw.Write(count);
            bw.Write(val);
        }
    }
}
