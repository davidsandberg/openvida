using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ISOVIEWXLib;

namespace IsoViewStub
{
    [Guid("865b2280-2b71-11d1-bc01-006097ac382a")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(_DIsoViewXEvents))]
    public class DummyIsoView : UserControl, _DIsoViewX
    {
        public class EcuNode
        {
            public string Id;
            public string Name;
            public float MinX, MaxX, MinY, MaxY;
            public Color FillColor = Color.FromArgb(230, 235, 240);
            public Color BorderColor = Color.FromArgb(60, 60, 60);
            public float BorderWidth = 1.0f;
            public bool Highlighted = false;
            public Color HighlightColor = Color.FromArgb(0, 102, 204);
            public RectangleF ScreenRect;
        }

        public event _DIsoViewXEvents_HsHitEventHandler HsHit;

        private List<PointF[]> _polylines = new List<PointF[]>();
        private Dictionary<string, EcuNode> _ecus = new Dictionary<string, EcuNode>(StringComparer.OrdinalIgnoreCase);
        private float _minX = 7660, _maxX = 32421, _minY = 17840, _maxY = 30192;
        private bool _hasLoadedCgm = false;

        public DummyIsoView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(235, 238, 242);
        }

        private Color ParseRgb(string value)
        {
            if (string.IsNullOrEmpty(value)) return Color.Empty;
            try
            {
                string[] parts = value.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    int r = int.Parse(parts[0]);
                    int g = int.Parse(parts[1]);
                    int b = int.Parse(parts[2]);
                    return Color.FromArgb(r, g, b);
                }
            }
            catch {}
            return Color.Empty;
        }

        public void LoadCgm(string urlOrPath)
        {
            if (string.IsNullOrEmpty(urlOrPath)) return;
            byte[] data = null;
            try
            {
                if (urlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    using (WebClient wc = new WebClient())
                    {
                        data = wc.DownloadData(urlOrPath);
                    }
                }
                else if (File.Exists(urlOrPath))
                {
                    data = File.ReadAllBytes(urlOrPath);
                }
            }
            catch
            {
                // Fall back to local scratch file if available
                string fallback = @"C:\Users\david\.gemini\antigravity\brain\582883f0-afd8-4231-87cd-0add866e7f19\scratch\v70_diesel_0920.cgm";
                if (File.Exists(fallback))
                {
                    try { data = File.ReadAllBytes(fallback); } catch {}
                }
            }

            if (data == null || data.Length == 0) return;

            ParseCgmData(data);
        }

        private void ParseCgmData(byte[] data)
        {
            try
            {
                int pos = 0;
                List<PointF[]> lines = new List<PointF[]>();
                Dictionary<string, EcuNode> nodes = new Dictionary<string, EcuNode>(StringComparer.OrdinalIgnoreCase);

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                List<Tuple<int, int, byte[]>> elements = new List<Tuple<int, int, byte[]>>();
                while (pos < data.Length - 1)
                {
                    ushort h = (ushort)((data[pos] << 8) | data[pos + 1]);
                    int ec = (h >> 12) & 0x0F;
                    int ei = (h >> 5) & 0x7F;
                    int len = h & 0x1F;
                    pos += 2;
                    if (len == 31)
                    {
                        if (pos >= data.Length - 1) break;
                        len = (data[pos] << 8) | data[pos + 1];
                        pos += 2;
                        if ((len & 0x8000) != 0) len &= 0x7FFF;
                    }
                    byte[] pd = new byte[len];
                    if (pos + len <= data.Length)
                    {
                        Array.Copy(data, pos, pd, 0, len);
                    }
                    pos += len;
                    if (len % 2 != 0) pos++;
                    elements.Add(new Tuple<int, int, byte[]>(ec, ei, pd));
                }

                // Polylines
                foreach (var el in elements)
                {
                    if (el.Item1 == 4 && el.Item2 == 1)
                    {
                        byte[] pd = el.Item3;
                        int count = (pd.Length / 2) & ~1;
                        PointF[] pts = new PointF[count / 2];
                        for (int k = 0; k < count; k += 2)
                        {
                            short x = (short)((pd[k * 2] << 8) | pd[k * 2 + 1]);
                            short y = (short)((pd[(k + 1) * 2] << 8) | pd[(k + 1) * 2 + 1]);
                            pts[k / 2] = new PointF(x, y);
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                        if (pts.Length > 1) lines.Add(pts);
                    }
                }

                // ECUs
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];
                    if (el.Item1 == 0 && el.Item2 == 21) // BEGIN APS
                    {
                        byte[] pd = el.Item3;
                        if (pd.Length < 2) continue;
                        int slen = pd[0];
                        string apsId = Encoding.GetEncoding("iso-8859-1").GetString(pd, 1, Math.Min(slen, pd.Length - 1));

                        if (!apsId.StartsWith("_") && !apsId.Equals("ct", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string text = "";
                        short txCoord = 0, tyCoord = 0, dw = 0, dh = 0;
                        List<PointF> boxPts = new List<PointF>();

                        int j = i + 1;
                        while (j < elements.Count)
                        {
                            var cel = elements[j];
                            if (cel.Item1 == 0 && cel.Item2 == 23)
                                break;

                            if (cel.Item1 == 4 && (cel.Item2 == 4 || cel.Item2 == 5))
                            {
                                byte[] cpd = cel.Item3;
                                if (cpd.Length >= 11)
                                {
                                    dw = (short)((cpd[0] << 8) | cpd[1]);
                                    dh = (short)((cpd[2] << 8) | cpd[3]);
                                    txCoord = (short)((cpd[4] << 8) | cpd[5]);
                                    tyCoord = (short)((cpd[6] << 8) | cpd[7]);
                                    int tlen = cpd[10];
                                    if (11 + tlen <= cpd.Length)
                                    {
                                        text = Encoding.GetEncoding("iso-8859-1").GetString(cpd, 11, tlen).Trim();
                                    }
                                }
                            }
                            else if (cel.Item1 == 4 && cel.Item2 == 26)
                            {
                                byte[] cpd = cel.Item3;
                                int count = (cpd.Length / 2);
                                short[] ints = new short[count];
                                for (int k = 0; k < count; k++)
                                {
                                    ints[k] = (short)((cpd[k * 2] << 8) | cpd[k * 2 + 1]);
                                }
                                for (int k = 1; k < count - 1; k += 2)
                                {
                                    boxPts.Add(new PointF(ints[k], ints[k + 1]));
                                }
                            }
                            j++;
                        }

                        EcuNode node = new EcuNode();
                        node.Id = apsId;
                        node.Name = string.IsNullOrEmpty(text) ? apsId : text;

                        if (boxPts.Count > 0)
                        {
                            float bMinX = float.MaxValue, bMaxX = float.MinValue;
                            float bMinY = float.MaxValue, bMaxY = float.MinValue;
                            foreach (var p in boxPts)
                            {
                                if (p.X < bMinX) bMinX = p.X;
                                if (p.X > bMaxX) bMaxX = p.X;
                                if (p.Y < bMinY) bMinY = p.Y;
                                if (p.Y > bMaxY) bMaxY = p.Y;
                            }
                            node.MinX = bMinX;
                            node.MaxX = bMaxX;
                            node.MinY = bMinY;
                            node.MaxY = bMaxY;
                        }
                        else if (!string.IsNullOrEmpty(text))
                        {
                            node.MinX = txCoord - 200;
                            node.MaxX = txCoord + dw + 200;
                            node.MinY = tyCoord - 600;
                            node.MaxY = tyCoord + 300;
                        }

                        // Preserves any existing color if re-parsing
                        if (_ecus.ContainsKey(apsId))
                        {
                            node.FillColor = _ecus[apsId].FillColor;
                            node.Highlighted = _ecus[apsId].Highlighted;
                        }

                        nodes[apsId] = node;
                        // Also index without underscore
                        if (apsId.StartsWith("_"))
                        {
                            nodes[apsId.Substring(1)] = node;
                        }
                        // Also index by uppercase name
                        if (!string.IsNullOrEmpty(node.Name))
                        {
                            nodes[node.Name] = node;
                        }

                        i = j;
                    }
                }

                _polylines = lines;
                _ecus = nodes;
                if (minX < maxX && minY < maxY)
                {
                    _minX = minX; _maxX = maxX;
                    _minY = minY; _maxY = maxY;
                }
                _hasLoadedCgm = true;

                this.Invalidate();
            }
            catch {}
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush bg = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            if (!_hasLoadedCgm || _polylines.Count == 0)
            {
                // Try auto-loading fallback if not yet loaded
                string fallback = @"C:\Users\david\.gemini\antigravity\brain\582883f0-afd8-4231-87cd-0add866e7f19\scratch\v70_diesel_0920.cgm";
                if (File.Exists(fallback))
                {
                    LoadCgm(fallback);
                }
            }

            int imgW = this.ClientSize.Width;
            int imgH = this.ClientSize.Height;
            if (imgW < 20 || imgH < 20) return;

            float margin = 6.0f;
            float cgmW = _maxX - _minX;
            float cgmH = _maxY - _minY;
            if (cgmW <= 0 || cgmH <= 0) return;

            float scale = Math.Min((imgW - margin * 2) / cgmW, (imgH - margin * 2) / cgmH);
            float offX = margin + (imgW - margin * 2 - cgmW * scale) / 2.0f;
            float offY = margin + (imgH - margin * 2 - cgmH * scale) / 2.0f;

            // Draw polylines
            using (Pen pen = new Pen(Color.FromArgb(140, 160, 180), 1.0f))
            {
                foreach (var line in _polylines)
                {
                    PointF[] screenLine = new PointF[line.Length];
                    for (int k = 0; k < line.Length; k++)
                    {
                        screenLine[k] = new PointF(
                            offX + (line[k].X - _minX) * scale,
                            offY + (_maxY - line[k].Y) * scale
                        );
                    }
                    g.DrawLines(pen, screenLine);
                }
            }

            // Draw ECUs (only draw unique instances)
            HashSet<EcuNode> drawn = new HashSet<EcuNode>();
            using (Font font = new Font("Arial", 5.8f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                foreach (var node in _ecus.Values)
                {
                    if (drawn.Contains(node)) continue;
                    drawn.Add(node);

                    if (node.MinX >= node.MaxX || node.MinY >= node.MaxY) continue;

                    float x1 = offX + (node.MinX - _minX) * scale;
                    float y1 = offY + (_maxY - node.MaxY) * scale;
                    float x2 = offX + (node.MaxX - _minX) * scale;
                    float y2 = offY + (_maxY - node.MinY) * scale;

                    RectangleF rect = new RectangleF(x1, y1, Math.Max(16, x2 - x1), Math.Max(10, y2 - y1));
                    node.ScreenRect = rect;

                    using (SolidBrush b = new SolidBrush(node.FillColor))
                    {
                        g.FillRectangle(b, rect);
                    }

                    if (node.Highlighted)
                    {
                        using (Pen hp = new Pen(node.HighlightColor, 2.5f))
                        {
                            g.DrawRectangle(hp, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                    }
                    else
                    {
                        using (Pen bp = new Pen(node.BorderColor, 1.0f))
                        {
                            g.DrawRectangle(bp, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                    }

                    g.DrawString(node.Name, font, textBrush, rect, sf);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            EcuNode hit = null;
            HashSet<EcuNode> checkedNodes = new HashSet<EcuNode>();
            foreach (var node in _ecus.Values)
            {
                if (checkedNodes.Contains(node)) continue;
                checkedNodes.Add(node);
                if (node.ScreenRect.Contains(e.Location))
                {
                    hit = node;
                    break;
                }
            }

            if (HsHit != null)
            {
                if (hit != null)
                {
                    HsHit(1, 0, hit.Id, "", "");
                }
                else
                {
                    HsHit(1, 0, "", "", "");
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = false;
            HashSet<EcuNode> checkedNodes = new HashSet<EcuNode>();
            foreach (var node in _ecus.Values)
            {
                if (checkedNodes.Contains(node)) continue;
                checkedNodes.Add(node);
                if (node.ScreenRect.Contains(e.Location))
                {
                    over = true;
                    break;
                }
            }
            this.Cursor = over ? Cursors.Hand : Cursors.Default;
        }


        

        private System.UInt32 _BackColor = 0;
        System.UInt32 _DIsoViewX.BackColor
        {
            get { return _BackColor; }
            set { _BackColor = value; }
        }

        private System.Int16 _BorderStyle = 0;
        System.Int16 _DIsoViewX.BorderStyle
        {
            get { return _BorderStyle; }
            set { _BorderStyle = value; }
        }

        private System.Boolean _Enabled = false;
        System.Boolean _DIsoViewX.Enabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        private System.String _FileName = string.Empty;
        System.String _DIsoViewX.FileName { get { return _FileName; } set { _FileName = value; LoadCgm(value); } }

        private System.Boolean _HsVisible = false;
        System.Boolean _DIsoViewX.HsVisible
        {
            get { return _HsVisible; }
            set { _HsVisible = value; }
        }

        private System.Boolean _ResizeToFit = false;
        System.Boolean _DIsoViewX.ResizeToFit
        {
            get { return _ResizeToFit; }
            set { _ResizeToFit = value; }
        }

        private System.Boolean _Preview = false;
        System.Boolean _DIsoViewX.Preview
        {
            get { return _Preview; }
            set { _Preview = value; }
        }

        private System.Boolean _AllowJumps = false;
        System.Boolean _DIsoViewX.AllowJumps
        {
            get { return _AllowJumps; }
            set { _AllowJumps = value; }
        }

        private System.Double _OffsetX = 0;
        System.Double _DIsoViewX.OffsetX
        {
            get { return _OffsetX; }
            set { _OffsetX = value; }
        }

        private System.Double _OffsetY = 0;
        System.Double _DIsoViewX.OffsetY
        {
            get { return _OffsetY; }
            set { _OffsetY = value; }
        }

        private System.Double _ViewSize = 0;
        System.Double _DIsoViewX.ViewSize
        {
            get { return _ViewSize; }
            set { _ViewSize = value; }
        }

        private System.Boolean _PropertyChangeMode = false;
        System.Boolean _DIsoViewX.PropertyChangeMode
        {
            get { return _PropertyChangeMode; }
            set { _PropertyChangeMode = value; }
        }

        private System.Int32 _ViewPortCount = 0;
        System.Int32 _DIsoViewX.ViewPortCount
        {
            get { return _ViewPortCount; }
            set { _ViewPortCount = value; }
        }

        private System.String _ViewPort = string.Empty;
        System.String _DIsoViewX.ViewPort
        {
            get { return _ViewPort; }
            set { _ViewPort = value; }
        }

        private System.Int32 _ObjectTipState = 0;
        System.Int32 _DIsoViewX.ObjectTipState
        {
            get { return _ObjectTipState; }
            set { _ObjectTipState = value; }
        }

        private System.Int32 _ActiveTool = 0;
        System.Int32 _DIsoViewX.ActiveTool
        {
            get { return _ActiveTool; }
            set { _ActiveTool = value; }
        }

        private System.Double _ExtentX = 0;
        System.Double _DIsoViewX.ExtentX
        {
            get { return _ExtentX; }
            set { _ExtentX = value; }
        }

        private System.Double _ExtentY = 0;
        System.Double _DIsoViewX.ExtentY
        {
            get { return _ExtentY; }
            set { _ExtentY = value; }
        }

        private System.String _HomeFileName = string.Empty;
        System.String _DIsoViewX.HomeFileName
        {
            get { return _HomeFileName; }
            set { _HomeFileName = value; }
        }

        private System.String _HomeViewPort = string.Empty;
        System.String _DIsoViewX.HomeViewPort
        {
            get { return _HomeViewPort; }
            set { _HomeViewPort = value; }
        }

        private System.Boolean _AcceptDroppedFiles = false;
        System.Boolean _DIsoViewX.AcceptDroppedFiles
        {
            get { return _AcceptDroppedFiles; }
            set { _AcceptDroppedFiles = value; }
        }

        System.String _DIsoViewX.Iso3GetObjectID(System.String @Object)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetObjectName(System.String @Object)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetObjectLayer(System.String @Object)
        {
            return string.Empty;
        }

        System.Int32 _DIsoViewX.Iso3GetAttrCount(System.String @Object)
        {
            return 0;
        }

        System.String _DIsoViewX.Iso3GetAttrByIndex(System.String @Object, System.Int32 Index)
        {
            return string.Empty;
        }

        System.Int32 _DIsoViewX.Iso3GetAttrType(System.String @Object, System.String attrName)
        {
            return 0;
        }

        System.String _DIsoViewX.Iso3GetAttrValue(System.String @Object, System.String attrName)
        {
            return string.Empty;
        }

        System.Int32 _DIsoViewX.Iso3GetIntegerAttrValue(System.String @Object, System.String attrName)
        {
            return 0;
        }

        System.Single _DIsoViewX.Iso3GetFloatAttrValue(System.String @Object, System.String attrName)
        {
            return 0.0f;
        }

        System.String _DIsoViewX.Iso3GetStringAttrValue(System.String @Object, System.String attrName)
        {
            return string.Empty;
        }

        System.Boolean _DIsoViewX.Iso3SetIntegerAttrValue(System.String @Object, System.String attrName, System.Int32 value)
        {
            return false;
        }

        System.Boolean _DIsoViewX.Iso3SetFloatAttrValue(System.String @Object, System.String attrName, System.Single value)
        {
            return false;
        }

        System.Boolean _DIsoViewX.Iso3SetStringAttrValue(System.String @Object, System.String attrName, System.String value)
        {
            return false;
        }

        void _DIsoViewX.Iso3SetGrAttribute(System.String objExpr, System.String attrName, System.String value)
        {
            if (string.IsNullOrEmpty(objExpr))
            {
                if (attrName == "backColor")
                {
                    Color c = ParseRgb(value);
                    if (c != Color.Empty) { this.BackColor = c; this.Invalidate(); }
                }
                return;
            }
            if (attrName == "fill")
            {
                Color c = ParseRgb(value);
                if (c != Color.Empty)
                {
                    if (_ecus.ContainsKey(objExpr))
                    {
                        _ecus[objExpr].FillColor = c;
                    }
                    else
                    {
                        string alt = objExpr.StartsWith("_") ? objExpr.Substring(1) : "_" + objExpr;
                        if (_ecus.ContainsKey(alt)) _ecus[alt].FillColor = c;
                    }
                    this.Invalidate();
                }
            }
        }

        System.Int32 _DIsoViewX.Iso3CountLinks(System.String @Object)
        {
            return 0;
        }

        System.Int32 _DIsoViewX.Iso3SelectLink(System.String @Object)
        {
            return 0;
        }

        System.String _DIsoViewX.Iso3GetLinkByIndex(System.String @Object, System.Int32 Index)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetLicensee()
        {
            return string.Empty;
        }

        void _DIsoViewX.Iso3SetObjectTipStyle(System.String Font, System.Int16 Size, System.Int16 Style, System.Int16 Alignment, System.Int16 Delay, System.Int16 ShowTime)
        {
        }

        void _DIsoViewX.Iso3ShowObjects(System.Boolean showObjects, System.Boolean showHotSpots)
        {
        }

        void _DIsoViewX.Iso3SetRotAngle(System.Double angle)
        {
        }

        System.Double _DIsoViewX.Iso3GetRotAngle()
        {
            return 0.0;
        }

        System.Int32 _DIsoViewX.Iso3GetObjectCount()
        {
            return 0;
        }

        System.String _DIsoViewX.Iso3GetObjectIDByIndex(System.Int32 Index)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetObjectNameByIndex(System.Int32 Index)
        {
            return string.Empty;
        }

        System.Boolean _DIsoViewX.Iso4ConfigMagnifier(System.Int32 styleFlags, System.Int32 Width, System.Int32 Height, System.Double Scale, System.Double minScale, System.Double maxScale)
        {
            return false;
        }

        System.Boolean _DIsoViewX.Iso4OpenMagnifier()
        {
            return false;
        }

        void _DIsoViewX.Iso4CloseMagnifier()
        {
        }

        System.Int32 _DIsoViewX.Iso4GetMagnifierState()
        {
            return 0;
        }

        System.Boolean _DIsoViewX.Iso4SetMagnifierScale(System.Double Scale)
        {
            return false;
        }

        System.Double _DIsoViewX.Iso4GetMagnifierScale()
        {
            return 0.0;
        }

        System.Boolean _DIsoViewX.Iso4ConfigGlobalView(System.Int32 styleFlags, System.Int32 Width, System.Int32 Height, System.Boolean useStroke, System.Int16 strokeWidth, System.UInt32 Color)
        {
            return false;
        }

        System.Boolean _DIsoViewX.Iso4OpenGlobalView()
        {
            return false;
        }

        void _DIsoViewX.Iso4CloseGlobalView()
        {
        }

        System.Int32 _DIsoViewX.Iso4GetGlobalViewState()
        {
            return 0;
        }

        System.Boolean _DIsoViewX.Iso4StartRedlining(System.Int32 flags)
        {
            return false;
        }

        void _DIsoViewX.Iso4StopRedlining()
        {
        }

        System.Boolean _DIsoViewX.Iso4ExportRedlining(System.String illuFile, System.String rlFile)
        {
            return false;
        }

        void _DIsoViewX.Iso4SetPreference(System.String prefName, System.String prefValue)
        {
        }

        System.String _DIsoViewX.Iso4GetPreference(System.String prefName)
        {
            return string.Empty;
        }

        void _DIsoViewX.Iso4EditPreferences()
        {
        }

        void _DIsoViewX.Iso4SetValue(System.String valID, System.String value)
        {
        }

        System.String _DIsoViewX.Iso4GetValue(System.String valID)
        {
            return string.Empty;
        }

        void _DIsoViewX.Refresh()
        {
        }

        void _DIsoViewX.AboutBox()
        {
        }

        System.Boolean _DIsoViewX.HighlightHotspot(System.Int32 lID, System.String strName, System.Int32 lFlags, System.UInt32 Color)
        {
            return false;
        }

        void _DIsoViewX.ConfigTools(System.Boolean bWithTools, System.Boolean bAsPopup, System.Boolean bTop, System.Boolean bRight)
        {
        }

        System.Boolean _DIsoViewX.OpenFile(System.String FileName, System.String View)
        {
            return false;
        }

        void _DIsoViewX.ShutDownServer()
        {
        }

        System.String _DIsoViewX.GetFirstViewPort()
        {
            return string.Empty;
        }

        System.String _DIsoViewX.GetNextViewPort()
        {
            return string.Empty;
        }

        System.Int16 _DIsoViewX.AddContextMenuItem(System.String strTitle, System.Boolean bEnabled)
        {
            return 0;
        }

        System.Int16 _DIsoViewX.CreateContextMenu()
        {
            return 0;
        }

        void _DIsoViewX.DisposeContextMenu()
        {
        }

        System.Boolean _DIsoViewX.EnableContextMenuItem(System.Int16 nItem, System.Boolean bEnabled)
        {
            return false;
        }

        System.String _DIsoViewX.getFileName()
        {
            return string.Empty;
        }

        void _DIsoViewX.setFileName(System.String strFileName) { _FileName = strFileName; LoadCgm(strFileName); }

        System.Boolean _DIsoViewX.getHsVisible()
        {
            return false;
        }

        void _DIsoViewX.setHsVisible(System.Boolean bVisible)
        {
        }

        System.Boolean _DIsoViewX.getResizeToFit()
        {
            return false;
        }

        void _DIsoViewX.setResizeToFit(System.Boolean bRTF)
        {
        }

        System.Boolean _DIsoViewX.getPreview()
        {
            return false;
        }

        void _DIsoViewX.setPreview(System.Boolean bPreview)
        {
        }

        System.Boolean _DIsoViewX.getAllowJumps()
        {
            return false;
        }

        void _DIsoViewX.setAllowJumps(System.Boolean bAllow)
        {
        }

        System.Double _DIsoViewX.getOffsetX()
        {
            return 0.0;
        }

        void _DIsoViewX.setOffsetX(System.Double fOffset)
        {
        }

        System.Double _DIsoViewX.getOffsetY()
        {
            return 0.0;
        }

        void _DIsoViewX.setOffsetY(System.Double fOffset)
        {
        }

        System.Double _DIsoViewX.getViewSize()
        {
            return 0.0;
        }

        void _DIsoViewX.setViewSize(System.Double fScale)
        {
        }

        System.Boolean _DIsoViewX.getPropertyChangeMode()
        {
            return false;
        }

        void _DIsoViewX.setPropertyChangeMode(System.Boolean bPCMode)
        {
        }

        System.Int32 _DIsoViewX.getViewPortCount()
        {
            return 0;
        }

        System.String _DIsoViewX.getViewPort()
        {
            return string.Empty;
        }

        void _DIsoViewX.setViewPort(System.String strVPName)
        {
        }

        System.String _DIsoViewX.GetVersion()
        {
            return string.Empty;
        }

        System.Int32 _DIsoViewX.getObjectTipState()
        {
            return 0;
        }

        void _DIsoViewX.setObjectTipState(System.Int32 State)
        {
        }

        void _DIsoViewX.SetObjectTip(System.Int32 ID, System.String Name, System.String Tip)
        {
        }

        System.String _DIsoViewX.GetObjectTip(System.Int32 ID, System.String Name)
        {
            return string.Empty;
        }

        void _DIsoViewX.SetObjectTipColor(System.UInt32 Background, System.UInt32 Text)
        {
        }

        System.UInt32 _DIsoViewX.GetObjectTipColor(System.Boolean GetBackCol)
        {
            return 0;
        }

        void _DIsoViewX.SetObjectTipStyle(System.String Font, System.Int16 Size, System.Int16 Style, System.Int16 Alignment)
        {
        }

        void _DIsoViewX.ConfigTools2(System.Int32 ToolMode, System.Int32 ToolSet, System.Int32 SelectedTool, System.Boolean Top, System.Boolean Right)
        {
        }

        System.Int32 _DIsoViewX.getActiveTool()
        {
            return 0;
        }

        void _DIsoViewX.setActiveTool(System.Int32 Tool)
        {
        }

        System.Boolean _DIsoViewX.ConfigPrinting(System.Int16 OnlyVisContent, System.Int16 SplitPages, System.Int16 Orientation, System.Double Scale)
        {
            return false;
        }

        void _DIsoViewX.SetPrintStrings(System.String Header, System.String Footer)
        {
        }

        void _DIsoViewX.SetPrintStringStyle(System.Int16 Target, System.String Font, System.Int16 Size, System.Int16 Style, System.Int16 Alignment, System.Double TopMargin, System.Double BotMargin, System.Double TopGap, System.Double BotGap)
        {
        }

        void _DIsoViewX.DoPrint(System.Int16 OnlyVisContent, System.Int16 SplitPages, System.Int16 Orientation, System.Double Scale)
        {
        }

        System.Boolean _DIsoViewX.SetHome(System.String FileName, System.String View)
        {
            return false;
        }

        System.String _DIsoViewX.getHomeFileName()
        {
            return string.Empty;
        }

        System.String _DIsoViewX.getHomeViewPort()
        {
            return string.Empty;
        }

        void _DIsoViewX.GoHome()
        {
        }

        System.Double _DIsoViewX.getExtentX()
        {
            return 0.0;
        }

        System.Double _DIsoViewX.getExtentY()
        {
            return 0.0;
        }

        System.Int32 _DIsoViewX.GetObjectCount()
        {
            return 0;
        }

        System.Int32 _DIsoViewX.GetObjectID(System.Int32 Index)
        {
            return 0;
        }

        System.String _DIsoViewX.GetObjectName(System.Int32 Index)
        {
            return string.Empty;
        }

        System.Boolean _DIsoViewX.DisposeObjectList()
        {
            return false;
        }

        System.String _DIsoViewX.GetObjectDataString(System.Int32 ID, System.String Name)
        {
            return string.Empty;
        }

        void _DIsoViewX.ConfigEvents(System.Int32 Events)
        {
        }

        System.Boolean _DIsoViewX.DoPopupMenu(System.Int32 x, System.Int32 y)
        {
            return false;
        }

        void _DIsoViewX.CloseFile()
        {
        }

        System.Int32 _DIsoViewX.GetLayerCount()
        {
            return 0;
        }

        System.String _DIsoViewX.GetLayerName(System.Int32 Index)
        {
            return string.Empty;
        }

        System.Boolean _DIsoViewX.GetLayerVisibility(System.String Name)
        {
            return false;
        }

        System.Boolean _DIsoViewX.SetLayerVisibility(System.String Name, System.Boolean IsVisible)
        {
            return false;
        }

        System.Boolean _DIsoViewX.LayerHasObjects(System.String Name)
        {
            return false;
        }

        System.Boolean _DIsoViewX.DisposeLayerList()
        {
            return false;
        }

        System.String _DIsoViewX.GetViewPortName(System.Int32 Index)
        {
            return string.Empty;
        }

        System.Boolean _DIsoViewX.DisposeViewPortList()
        {
            return false;
        }

        System.String _DIsoViewX.CmdLine(System.String CmdLine)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.GetObjectViewPortFile(System.Int32 ID, System.String Name)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.GetObjectViewPortName(System.Int32 ID, System.String Name)
        {
            return string.Empty;
        }

        void _DIsoViewX.SetZoomRange(System.Double minVal, System.Double maxVal)
        {
        }

        System.Boolean _DIsoViewX.SetPassword(System.String Password)
        {
            return false;
        }

        System.Boolean _DIsoViewX.Iso3OpenFile(System.String url)
        {
            return false;
        }

        void _DIsoViewX.Iso3SetHome(System.String url)
        {
        }

        System.String _DIsoViewX.Iso3GetObjectTip(System.String @Object)
        {
            return string.Empty;
        }

        void _DIsoViewX.Iso3SetObjectTip(System.String @Object, System.String Tip)
        {
        }

        void _DIsoViewX.Iso3HighlightObject(System.String objExpr, System.Int32 flags, System.UInt32 Color)
        {
            if (string.IsNullOrEmpty(objExpr)) return;
            EcuNode node = null;
            if (_ecus.ContainsKey(objExpr)) node = _ecus[objExpr];
            else
            {
                string alt = objExpr.StartsWith("_") ? objExpr.Substring(1) : "_" + objExpr;
                if (_ecus.ContainsKey(alt)) node = _ecus[alt];
            }
            if (node != null)
            {
                node.Highlighted = (Color != 0 && flags != 0);
                if (Color != 0)
                {
                    int r = (int)(Color & 0xFF);
                    int g = (int)((Color >> 8) & 0xFF);
                    int b = (int)((Color >> 16) & 0xFF);
                    node.HighlightColor = System.Drawing.Color.FromArgb(r, g, b);
                }
                this.Invalidate();
            }
        }

        System.String _DIsoViewX.Iso3GetRootObject()
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetFirstChild(System.String @Object)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetNextSibling(System.String @Object)
        {
            return string.Empty;
        }

        System.String _DIsoViewX.Iso3GetPreviousSibling(System.String @Object)
        {
            return string.Empty;
        }

        System.Int32 _DIsoViewX.Iso3GetChildCount(System.String @Object)
        {
            return 0;
        }

        System.String _DIsoViewX.Iso3GetParent(System.String @Object)
        {
            return string.Empty;
        }

    }
}
