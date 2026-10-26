using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;

class VolvoMcpServer
{
    private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private const string ConnStr = "Server=127.0.0.1,1433;User Id=ASUser;Password=GunnarS3g3;Database=master;TrustServerCertificate=True;";

    // P/Invoke for J2534 DiCE PassThru API
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("TSDiCE32.dll", EntryPoint = "PassThruOpen")]
    private static extern int PassThruOpen(IntPtr pName, out uint pDeviceId);

    [DllImport("TSDiCE32.dll", EntryPoint = "PassThruClose")]
    private static extern int PassThruClose(uint deviceId);

    [DllImport("TSDiCE32.dll", EntryPoint = "PassThruReadVersion")]
    private static extern int PassThruReadVersion(uint deviceId, StringBuilder firmwareVer, StringBuilder dllVer, StringBuilder apiVer);

    [DllImport("TSDiCE32.dll", EntryPoint = "PassThruIoctl")]
    private static extern int PassThruIoctl(uint handle, uint ioctlId, IntPtr pInput, IntPtr pOutput);

    private const uint READ_VBATT = 0x08;

    static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);

        SetDllDirectory(@"C:\Program Files (x86)\DiCE\Tools");

        string line;
        while ((line = Console.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var req = _json.Deserialize<Dictionary<string, object>>(line);
                if (req == null) continue;

                ProcessRequest(req);
            }
            catch (Exception ex)
            {
                SendError(null, -32603, "Internal error: " + ex.Message);
            }
        }
    }

    private static void ProcessRequest(Dictionary<string, object> req)
    {
        object id = req.ContainsKey("id") ? req["id"] : null;
        string method = req.ContainsKey("method") ? req["method"] as string : "";

        if (method == "initialize")
        {
            var result = new Dictionary<string, object>
            {
                { "protocolVersion", "2024-11-05" },
                { "capabilities", new Dictionary<string, object> { { "tools", new Dictionary<string, object>() } } },
                { "serverInfo", new Dictionary<string, object> { { "name", "volvo-dice-mcp" }, { "version", "1.0.0" } } }
            };
            SendResult(id, result);
        }
        else if (method == "notifications/initialized")
        {
        }
        else if (method == "tools/list")
        {
            var tools = GetToolsDefinition();
            var result = new Dictionary<string, object> { { "tools", tools } };
            SendResult(id, result);
        }
        else if (method == "tools/call")
        {
            var paramsObj = req.ContainsKey("params") ? req["params"] as Dictionary<string, object> : null;
            string toolName = paramsObj != null && paramsObj.ContainsKey("name") ? paramsObj["name"] as string : "";
            var arguments = paramsObj != null && paramsObj.ContainsKey("arguments") ? paramsObj["arguments"] as Dictionary<string, object> : new Dictionary<string, object>();

            try
            {
                string textResult = ExecuteTool(toolName, arguments ?? new Dictionary<string, object>());
                var content = new List<object>
                {
                    new Dictionary<string, object> { { "type", "text" }, { "text", textResult } }
                };
                var result = new Dictionary<string, object> { { "content", content } };
                SendResult(id, result);
            }
            catch (Exception ex)
            {
                var content = new List<object>
                {
                    new Dictionary<string, object> { { "type", "text" }, { "text", "Error executing " + toolName + ": " + ex.Message } }
                };
                var result = new Dictionary<string, object> { { "content", content }, { "isError", true } };
                SendResult(id, result);
            }
        }
        else
        {
            if (id != null)
            {
                SendError(id, -32601, "Method not found: " + method);
            }
        }
    }

    private static List<object> GetToolsDefinition()
    {
        var list = new List<object>();

        // 1. dice_status
        list.Add(new Dictionary<string, object>
        {
            { "name", "dice_status" },
            { "description", "Checks physical Volvo DiCE adapter connection, firmware version, driver status, and reads real-time vehicle battery voltage (mV and V) via J2534 PassThru." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        // 2. lookup_dtc
        list.Add(new Dictionary<string, object>
        {
            { "name", "lookup_dtc" },
            { "description", "Searches the Volvo VIDA factory knowledge base for Diagnostic Trouble Codes (DTCs), returning ECU name, fault status (Permanent/Intermittent), voltage/signal condition, and description." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "code", new Dictionary<string, object> { { "type", "string" }, { "description", "DTC code to lookup (e.g. 'ECM-2000', 'ABS-0077', 'PSM-001E', 'CEM-1A53', or keywords like 'battery', 'alternator')." } } },
                            { "language", new Dictionary<string, object> { { "type", "string" }, { "description", "Language code (default: 'en-US', or 'sv-SE')." } } }
                        }
                    },
                    { "required", new string[] { "code" } }
                }
            }
        });

        // 3. search_repair_manual
        list.Add(new Dictionary<string, object>
        {
            { "name", "search_repair_manual" },
            { "description", "Searches the official Volvo workshop service and repair manual (servicerep_en-US) for procedures, diagnostics, component replacement, and wiring guides." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "Search keywords (e.g. 'alternator replacing', 'generator', 'battery charging', 'charge regulator')." } } },
                            { "limit", new Dictionary<string, object> { { "type", "integer" }, { "description", "Maximum number of document summaries to return (default 5)." } } }
                        }
                    },
                    { "required", new string[] { "query" } }
                }
            }
        });

        // 4. get_repair_document
        list.Add(new Dictionary<string, object>
        {
            { "name", "get_repair_document" },
            { "description", "Retrieves the full text, step-by-step repair instructions, specifications, and warnings of a specific Volvo workshop document by ID." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "document_id", new Dictionary<string, object> { { "type", "integer" }, { "description", "The document ID returned by search_repair_manual." } } }
                        }
                    },
                    { "required", new string[] { "document_id" } }
                }
            }
        });

        // 5. decode_vin
        list.Add(new Dictionary<string, object>
        {
            { "name", "decode_vin" },
            { "description", "Decodes a Volvo 17-character Vehicle Identification Number (VIN) to extract vehicle model, model year, engine type, transmission, and body style." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "vin", new Dictionary<string, object> { { "type", "string" }, { "description", "17-character Volvo VIN (e.g. 'YV1...')." } } }
                        }
                    },
                    { "required", new string[] { "vin" } }
                }
            }
        });

        // 6. list_ecus
        list.Add(new Dictionary<string, object>
        {
            { "name", "list_ecus" },
            { "description", "Lists Volvo Electronic Control Units (ECUs) with their standard abbreviations (ECM, TCM, BCM, CEM, DIM, etc.) and system descriptions." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional search filter for ECU abbreviation or name (e.g. 'ECM', 'engine', 'brake')." } } }
                        }
                    }
                }
            }
        });

        return list;
    }

    private static string ExecuteTool(string name, Dictionary<string, object> args)
    {
        switch (name)
        {
            case "dice_status":
                return ToolDiceStatus();
            case "lookup_dtc":
                string code = args.ContainsKey("code") ? args["code"].ToString() : "";
                string lang = args.ContainsKey("language") ? args["language"].ToString() : "en-US";
                return ToolLookupDtc(code, lang);
            case "search_repair_manual":
                string query = args.ContainsKey("query") ? args["query"].ToString() : "";
                int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : 5;
                return ToolSearchRepairManual(query, limit);
            case "get_repair_document":
                int docId = args.ContainsKey("document_id") ? Convert.ToInt32(args["document_id"]) : 0;
                return ToolGetRepairDocument(docId);
            case "decode_vin":
                string vin = args.ContainsKey("vin") ? args["vin"].ToString() : "";
                return ToolDecodeVin(vin);
            case "list_ecus":
                string ecuQuery = args.ContainsKey("query") ? args["query"].ToString() : "";
                return ToolListEcus(ecuQuery);
            default:
                throw new Exception("Unknown tool: " + name);
        }
    }

    private static string ToolDiceStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Volvo DiCE Hardware Status ===");

        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\PassThruSupport.04.04\SETEK - DiCE"))
            {
                if (key != null)
                {
                    sb.AppendLine("Registry Driver: " + key.GetValue("Name"));
                    sb.AppendLine("DLL Path: " + key.GetValue("FunctionLibrary"));
                    sb.AppendLine("Firmware Version Config: " + key.GetValue("FirmwareVersion"));
                }
            }
        }
        catch {}

        var procs = Process.GetProcessesByName("VidaLauncher");
        if (procs.Length > 0)
        {
            sb.AppendLine("Active Application: VidaLauncher is currently running (PID " + procs[0].Id + ")");
            sb.AppendLine("Note: DiCE device is currently held open by VidaLauncher GUI.");
            sb.AppendLine("DiCE Status: ONLINE (Connected & In Use by GUI)");
            return sb.ToString();
        }

        uint devId = 0;
        int status = -1;
        try
        {
            status = PassThruOpen(IntPtr.Zero, out devId);
        }
        catch (Exception ex)
        {
            sb.AppendLine("PassThruOpen Exception: " + ex.Message);
            return sb.ToString();
        }

        if (status == 0)
        {
            sb.AppendLine("Device Status: CONNECTED (Device ID: " + devId + ")");
            try
            {
                var fw = new StringBuilder(100);
                var dll = new StringBuilder(100);
                var api = new StringBuilder(100);
                if (PassThruReadVersion(devId, fw, dll, api) == 0)
                {
                    sb.AppendLine("Firmware Version: " + fw.ToString());
                    sb.AppendLine("DLL Version: " + dll.ToString());
                    sb.AppendLine("API Version: " + api.ToString());
                }

                IntPtr pOut = Marshal.AllocHGlobal(4);
                try
                {
                    int ioRes = PassThruIoctl(devId, READ_VBATT, IntPtr.Zero, pOut);
                    if (ioRes == 0)
                    {
                        uint vBattMv = (uint)Marshal.ReadInt32(pOut);
                        double vBatt = vBattMv / 1000.0;
                        sb.AppendLine(string.Format("Live Vehicle/Input Voltage: {0:F2} V ({1} mV)", vBatt, vBattMv));
                        if (vBatt < 11.8)
                        {
                            sb.AppendLine("Warning: Battery/Supply voltage is LOW (< 11.8V). Connect a battery charger before programming ECUs.");
                        }
                        else if (vBatt >= 13.5)
                        {
                            sb.AppendLine("State: Charging detected (Voltage >= 13.5V). Alternator / Charger active.");
                        }
                    }
                    else
                    {
                        sb.AppendLine("Voltage Readout: Ioctl returned code " + ioRes);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pOut);
                }
            }
            finally
            {
                PassThruClose(devId);
            }
        }
        else if (status == 8)
        {
            sb.AppendLine("Device Status: ONLINE (Device in use by another application)");
        }
        else
        {
            sb.AppendLine("Device Status: NOT DETECTED (PassThruOpen error code: " + status + ")");
            sb.AppendLine("Ensure the DiCE is connected via USB and powered with 12V (either via car OBD-II or 12V DC power adapter).");
        }

        return sb.ToString();
    }

    private static string ToolLookupDtc(string code, string language)
    {
        var sb = new StringBuilder();
        int langId = (language != null && language.StartsWith("sv", StringComparison.OrdinalIgnoreCase)) ? 1 : 15;

        using (var conn = new SqlConnection(ConnStr))
        {
            conn.Open();

            var cmd = conn.CreateCommand();
            string[] words = code.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var sql = new StringBuilder("SELECT TOP 20 iet.fkIE, iet.DisplayText FROM DiagSwdlRepository.dbo.IETitle iet WHERE iet.fkLanguage = @lang");
            cmd.Parameters.AddWithValue("@lang", langId);

            for (int i = 0; i < words.Length; i++)
            {
                sql.Append(" AND iet.DisplayText LIKE @w" + i);
                cmd.Parameters.AddWithValue("@w" + i, "%" + words[i] + "%");
            }
            sql.Append(" ORDER BY iet.DisplayText");
            cmd.CommandText = sql.ToString();

            using (var reader = cmd.ExecuteReader())
            {
                sb.AppendLine("=== Volvo Diagnostic Trouble Code (DTC) Search: '" + code + "' ===");
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    string ieId = reader.GetString(0);
                    string text = reader.GetString(1);
                    sb.AppendLine(string.Format("{0}. [{1}] {2}", count, ieId, text));
                }
                if (count == 0)
                {
                    sb.AppendLine("No exact DTC titles found matching '" + code + "'.");
                }
            }

            var cmd2 = conn.CreateCommand();
            int carComLang = (langId == 1) ? 16 : 19;
            var sql2 = new StringBuilder("SELECT TOP 10 s.id, td.data FROM CarCom.dbo.T152_Symptom s JOIN CarCom.dbo.T191_TextData td ON s.fkT190_Text = td.fkT190_Text WHERE td.fkT193_Language = @lang");
            cmd2.Parameters.AddWithValue("@lang", carComLang);
            for (int i = 0; i < words.Length; i++)
            {
                sql2.Append(" AND td.data LIKE @cw" + i);
                cmd2.Parameters.AddWithValue("@cw" + i, "%" + words[i] + "%");
            }
            cmd2.CommandText = sql2.ToString();

            using (var reader = cmd2.ExecuteReader())
            {
                bool headerPrinted = false;
                while (reader.Read())
                {
                    if (!headerPrinted)
                    {
                        sb.AppendLine("\n--- Associated CarCom Symptoms / System Fault Paths ---");
                        headerPrinted = true;
                    }
                    sb.AppendLine("  * Symptom #" + reader.GetInt32(0) + ": " + reader.GetString(1));
                }
            }
        }

        return sb.ToString();
    }

    private static string ToolSearchRepairManual(string query, int limit)
    {
        var sb = new StringBuilder();
        if (limit <= 0) limit = 5;

        using (var conn = new SqlConnection(ConnStr))
        {
            conn.Open();
            var cmd = conn.CreateCommand();

            string[] words = (query ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sql = new StringBuilder("SELECT TOP (@limit) d.id, d.title, d.path FROM [servicerep_en-US].dbo.Document d WHERE 1=1");
            cmd.Parameters.AddWithValue("@limit", limit);

            for (int i = 0; i < words.Length; i++)
            {
                sql.Append(" AND d.title LIKE @w" + i);
                cmd.Parameters.AddWithValue("@w" + i, "%" + words[i] + "%");
            }
            sql.Append(" ORDER BY d.title");
            cmd.CommandText = sql.ToString();

            using (var reader = cmd.ExecuteReader())
            {
                sb.AppendLine("=== Volvo Workshop Service & Repair Manual Search: '" + query + "' ===");
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    int id = reader.GetInt32(0);
                    string title = reader.GetString(1);
                    string path = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    sb.AppendLine(string.Format("{0}. [Doc ID: {1}] {2}", count, id, title.Trim()));
                }
                if (count == 0)
                {
                    sb.AppendLine("No repair manual documents found matching query: " + query);
                }
                else
                {
                    sb.AppendLine("\nTip: Call tool 'get_repair_document' with document_id to view full instructions.");
                }
            }
        }

        return sb.ToString();
    }

    private static string ToolGetRepairDocument(int docId)
    {
        using (var conn = new SqlConnection(ConnStr))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT title, XmlContent FROM [servicerep_en-US].dbo.Document WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", docId);

            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return "Document ID " + docId + " not found.";
                }

                string title = reader.GetString(0);
                if (reader.IsDBNull(1))
                {
                    return "Document [ID: " + docId + "] '" + title + "' has no embedded XML content.";
                }

                byte[] bytes = (byte[])reader.GetValue(1);
                string textContent = ExtractTextFromZip(bytes);

                var sb = new StringBuilder();
                sb.AppendLine("=== Volvo Workshop Document: " + title.Trim() + " [ID: " + docId + "] ===");
                sb.AppendLine();
                sb.AppendLine(textContent);
                return sb.ToString();
            }
        }
    }

    private static string ExtractTextFromZip(byte[] zipBytes)
    {
        try
        {
            using (var ms = new MemoryStream(zipBytes))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string rawXml = reader.ReadToEnd();
                            string cleaned = CleanXmlToText(rawXml);
                            return cleaned;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return "Error extracting document content: " + ex.Message;
        }
        return "No XML document found in archive.";
    }

    private static string CleanXmlToText(string xml)
    {
        xml = Regex.Replace(xml, @"<(p|li|step|title|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"<[^>]+>", " ");
        xml = xml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&apos;", "'");
        xml = Regex.Replace(xml, @"[ \t]+", " ");
        xml = Regex.Replace(xml, @"\n{3,}", "\n\n");
        return xml.Trim();
    }

    private static string ToolDecodeVin(string vin)
    {
        vin = (vin ?? "").Trim().ToUpper();
        if (vin.Length != 17)
        {
            return "Invalid VIN length: " + vin.Length + " characters. Standard Volvo VIN must be 17 characters.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== Volvo VIN Decoding for: " + vin + " ===");
        sb.AppendLine("WMI (Manufacturer): " + vin.Substring(0, 3) + " (Volvo Cars)");

        string modelCode = vin.Substring(3, 2);
        string engineCode = vin.Substring(5, 2);
        string bodyCode = vin.Substring(7, 1);
        string yearCode = vin.Substring(9, 1);
        string factoryCode = vin.Substring(10, 1);
        string chassisNo = vin.Substring(11, 6);

        sb.AppendLine("Model Code: " + modelCode);
        sb.AppendLine("Engine Code: " + engineCode);
        sb.AppendLine("Body / Safety: " + bodyCode);
        sb.AppendLine("Model Year Code: " + yearCode);
        sb.AppendLine("Assembly Plant: " + factoryCode);
        sb.AppendLine("Chassis Serial: " + chassisNo);

        using (var conn = new SqlConnection(ConnStr))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT TOP 5 vm.Description AS ModelName, my.Description AS ModelYear
FROM BaseData.dbo.VINDecodeModel vdm
JOIN BaseData.dbo.VehicleModel vm ON vdm.fkVehicleModel = vm.ID
JOIN BaseData.dbo.ModelYear my ON vdm.fkModelYear = my.ID
WHERE vdm.VinCompare = @modelCode
";
            cmd.Parameters.AddWithValue("@modelCode", modelCode);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    sb.AppendLine("Resolved Vehicle Model: " + reader.GetString(0));
                }
            }
        }

        return sb.ToString();
    }

    private static string ToolListEcus(string filter)
    {
        var sb = new StringBuilder();
        using (var conn = new SqlConnection(ConnStr))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT TOP 30 e.id, e.acronym, td.data AS Description
FROM CarCom.dbo.T101_Ecu e
LEFT JOIN CarCom.dbo.T191_TextData td ON e.fkT190_Text = td.fkT190_Text AND td.fkT193_Language = 19
WHERE (@filter = '' OR e.acronym LIKE @pattern OR td.data LIKE @pattern)
ORDER BY e.acronym
";
            cmd.Parameters.AddWithValue("@filter", filter ?? "");
            cmd.Parameters.AddWithValue("@pattern", "%" + (filter ?? "") + "%");

            using (var reader = cmd.ExecuteReader())
            {
                sb.AppendLine("=== Volvo Electronic Control Units (ECUs) ===");
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    string acronym = reader.GetString(1);
                    string desc = reader.IsDBNull(2) ? "Electronic Control Module" : reader.GetString(2);
                    sb.AppendLine(string.Format("  * {0}: {1}", acronym.PadRight(8), desc));
                }
                if (count == 0)
                {
                    sb.AppendLine("No ECUs found matching filter: " + filter);
                }
            }
        }

        return sb.ToString();
    }

    private static void SendResult(object id, object result)
    {
        var resp = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" },
            { "id", id },
            { "result", result }
        };
        string outStr = _json.Serialize(resp);
        Console.WriteLine(outStr);
    }

    private static void SendError(object id, int code, string message)
    {
        var error = new Dictionary<string, object>
        {
            { "code", code },
            { "message", message }
        };
        var resp = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" },
            { "id", id },
            { "error", error }
        };
        string outStr = _json.Serialize(resp);
        Console.WriteLine(outStr);
    }
}
