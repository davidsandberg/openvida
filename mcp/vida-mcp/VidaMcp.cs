using System;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Web.Script.Serialization;

class VidaMcpServer
{
    private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private const string ConnStr = "Server=127.0.0.1,1433;User Id=ASUser;Password=GunnarS3g3;Database=master;TrustServerCertificate=True;";
    private const string EpcConnStr = "Server=127.0.0.1,1433;User Id=ASUser;Password=GunnarS3g3;Database=EPC;TrustServerCertificate=True;";
    private const string PipeName = "openvida_rpc";
    private static string _lastIpcError = "";

    static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);

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
                { "serverInfo", new Dictionary<string, object> { { "name", "vida-mcp" }, { "version", "2.0.0" } } }
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
            { "description", "Checks Volvo DiCE adapter connection, real-time vehicle battery voltage (mV and V), ignition key position, and profile via OpenVIDA." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        // 2. scan_network
        list.Add(new Dictionary<string, object>
        {
            { "name", "scan_network" },
            { "description", "Scans high-speed (HS-CAN) and low-speed (LS-CAN) buses for all responding Electronic Control Units (ECM, TCM, BCM, CEM, DIM, CCM, REM, etc.) via OpenVIDA." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        // 3. read_active_dtcs
        list.Add(new Dictionary<string, object>
        {
            { "name", "read_active_dtcs" },
            { "description", "Reads currently active Diagnostic Trouble Codes (DTCs), fault states (Permanent/Intermittent), and ECU sources directly from the vehicle via OpenVIDA." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        // 4. read_parameters
        list.Add(new Dictionary<string, object>
        {
            { "name", "read_parameters" },
            { "description", "Reads real-time ECU live parameters (Engine RPM, Coolant Temp, Mass Air Flow, Battery Voltage, Damper positions, etc.) via OpenVIDA." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "ecu", new Dictionary<string, object> { { "type", "string" }, { "description", "Target ECU abbreviation (default: 'ECM', or 'CCM', 'BCM', 'CEM')." } } }
                        }
                    }
                }
            }
        });

        // 5. lookup_dtc
        list.Add(new Dictionary<string, object>
        {
            { "name", "lookup_dtc" },
            { "description", "Searches the Volvo VIDA factory knowledge base for Diagnostic Trouble Codes (DTCs), returning ECU name, fault status, conditions, and descriptions (works offline)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "code", new Dictionary<string, object> { { "type", "string" }, { "description", "DTC code to lookup (e.g. 'ECM-2000', 'ABS-0077', 'CCM-0070', 'CEM-1A53', or keywords like 'battery', 'alternator')." } } },
                            { "language", new Dictionary<string, object> { { "type", "string" }, { "description", "Language code (default: 'en-US', or 'sv-SE')." } } }
                        }
                    },
                    { "required", new string[] { "code" } }
                }
            }
        });

        // 6. search_repair_manual
        list.Add(new Dictionary<string, object>
        {
            { "name", "search_repair_manual" },
            { "description", "Searches the official Volvo workshop service and repair manuals for procedures, diagnostics, component replacement, and overhaul instructions (works offline)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "Search keywords (e.g. 'alternator replacing', 'timing belt', 'throttle adaptation', 'battery replacement')." } } },
                            { "limit", new Dictionary<string, object> { { "type", "integer" }, { "description", "Maximum number of document summaries to return (default 5)." } } }
                        }
                    },
                    { "required", new string[] { "query" } }
                }
            }
        });

        // 7. get_repair_document
        list.Add(new Dictionary<string, object>
        {
            { "name", "get_repair_document" },
            { "description", "Retrieves the full text, step-by-step repair instructions, specifications, tightening torques, and warnings of a specific Volvo workshop document by ID (works offline)." },
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

        // 8. get_wiring_schematic
        list.Add(new Dictionary<string, object>
        {
            { "name", "get_wiring_schematic" },
            { "description", "Searches official Volvo Electronic Wiring Diagrams (EWD) by component name or code (e.g. '1/1' Battery, '4/56' CEM, 'alternator', 'throttle module', 'starter relay') returning both electrical schematic and physical car placement page numbers (works offline)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "component", new Dictionary<string, object> { { "type", "string" }, { "description", "Component code or keyword (e.g. '1/1', '10/1', 'alternator', 'glow plug', 'throttle', 'wiper relay')." } } },
                            { "publication_id", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional EWD publication ID (default '39180202' for 2011 V70/XC70)." } } }
                        }
                    },
                    { "required", new string[] { "component" } }
                }
            }
        });

        // 9. search_parts_catalog
        list.Add(new Dictionary<string, object>
        {
            { "name", "search_parts_catalog" },
            { "description", "Searches official Volvo Electronic Parts Catalog (EPC) by part number or component keyword, returning factory part numbers, descriptions, unit types, and assembly groups (works offline)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "Part number or description keyword (e.g. '30761308', 'spark plug', 'brake disc', 'alternator', 'oil filter')." } } },
                            { "limit", new Dictionary<string, object> { { "type", "integer" }, { "description", "Maximum results to return (default 10)." } } }
                        }
                    },
                    { "required", new string[] { "query" } }
                }
            }
        });

        // 10. decode_vin
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

        // 11. list_ecus
        list.Add(new Dictionary<string, object>
        {
            { "name", "list_ecus" },
            { "description", "Lists standard Volvo Electronic Control Units (ECUs) with their abbreviations (ECM, TCM, BCM, CEM, DIM, etc.) and system descriptions." },
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
            case "scan_network":
                return ToolScanNetwork();
            case "read_active_dtcs":
                return ToolReadActiveDtcs();
            case "read_parameters":
                string pEcu = args.ContainsKey("ecu") ? args["ecu"].ToString() : "ECM";
                return ToolReadParameters(pEcu);
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
            case "get_wiring_schematic":
                string comp = args.ContainsKey("component") ? args["component"].ToString() : "";
                string pub = args.ContainsKey("publication_id") ? args["publication_id"].ToString() : "39180202";
                return ToolGetWiringSchematic(comp, pub);
            case "search_parts_catalog":
                string partQ = args.ContainsKey("query") ? args["query"].ToString() : "";
                int partLimit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : 10;
                return ToolSearchPartsCatalog(partQ, partLimit);
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

    // -------------------------------------------------------------
    // IPC HELPER
    // -------------------------------------------------------------
    private static bool TrySendIpcRequest(string action, Dictionary<string, object> parameters, out Dictionary<string, object> response, int timeoutMs = 2000)
    {
        response = null;
        try
        {
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                pipe.Connect(timeoutMs);
                var writer = new StreamWriter(pipe, new UTF8Encoding(false));
                var reader = new StreamReader(pipe, Encoding.UTF8);

                var req = parameters != null ? new Dictionary<string, object>(parameters) : new Dictionary<string, object>();
                req["action"] = action;
                string jsonReq = _json.Serialize(req);
                writer.WriteLine(jsonReq);
                writer.Flush();

                string line = reader.ReadLine();
                if (!string.IsNullOrEmpty(line))
                {
                    response = _json.Deserialize<Dictionary<string, object>>(line);
                    return response != null;
                }
            }
        }
        catch (Exception ex)
        {
            _lastIpcError = ex.ToString();
            return false;
        }
        return false;
    }

    private static string GetOpenVidaNotRunningPrompt(string requestedOperation)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚠️ **OpenVIDA (VidaLauncher) is not currently running.**");
        sb.AppendLine();
        sb.AppendLine("To protect hardware stability and avoid J2534 device lock contention (`ERR_DEVICE_IN_USE`), all live car communication is managed exclusively through OpenVIDA.");
        sb.AppendLine();
        sb.AppendLine("👉 **To " + requestedOperation + ":**");
        sb.AppendLine("1. Start **VidaLauncher** (`VidaLauncher.exe`).");
        sb.AppendLine("2. Connect your DiCE hardware via USB and OBD-II port (Ignition in Position II).");
        sb.AppendLine("3. Re-run this tool.");
        sb.AppendLine();
        sb.AppendLine("*(Note: Workshop manuals, DTC lookups, parts catalog, wiring schematics, and VIN decoding continue to work completely offline without running VidaLauncher).*");
        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 1: dice_status
    // -------------------------------------------------------------
    private static string ToolDiceStatus()
    {
        Dictionary<string, object> resp;
        if (!TrySendIpcRequest("dice_status", null, out resp, 2000))
        {
            return GetOpenVidaNotRunningPrompt("check live vehicle voltage and DiCE status");
        }

        var sb = new StringBuilder();
        sb.AppendLine("### 🔌 Volvo DiCE & Vehicle Telemetry Status");
        sb.AppendLine();

        bool diceConn = resp.ContainsKey("dice_connected") && Convert.ToBoolean(resp["dice_connected"]);
        bool mock = resp.ContainsKey("mock_simulation") && Convert.ToBoolean(resp["mock_simulation"]);
        double batteryV = resp.ContainsKey("battery_v") ? Convert.ToDouble(resp["battery_v"]) : 0.0;
        string keyPos = resp.ContainsKey("key_position") ? resp["key_position"].ToString() : "Unknown";
        string connState = resp.ContainsKey("connection_state") ? resp["connection_state"].ToString() : "Unknown";
        string profile = resp.ContainsKey("profile_id") ? resp["profile_id"].ToString() : "";
        string hwOwner = resp.ContainsKey("hardware_owner") ? resp["hardware_owner"].ToString() : "OpenVIDA";

        sb.AppendLine(string.Format("- **DiCE Interface:** {0}", diceConn ? "🟢 Connected & Active" : (mock ? "🧪 Mock Simulation Mode" : "⚪ Disconnected / Not Detected")));
        sb.AppendLine(string.Format("- **Battery Voltage:** {0:F1} V ({1:F0} mV)", batteryV, batteryV * 1000));
        sb.AppendLine(string.Format("- **Ignition Key Position:** {0}", keyPos));
        sb.AppendLine(string.Format("- **Hardware Session:** {0}", hwOwner));
        if (!string.IsNullOrEmpty(profile)) sb.AppendLine(string.Format("- **Vehicle Profile:** `{0}`", profile));

        sb.AppendLine();
        if (batteryV > 0 && batteryV < 11.8)
        {
            sb.AppendLine("⚠️ **Warning:** Battery voltage is low (< 11.8 V). Connect a battery maintainer before programming or extensive diagnostics.");
        }
        else if (batteryV >= 13.5)
        {
            sb.AppendLine("⚡ **Charging Active:** Engine running or external battery charger connected (>= 13.5 V).");
        }
        else if (batteryV >= 12.0)
        {
            sb.AppendLine("✅ **Voltage Healthy:** Battery level is normal for diagnostic scanning.");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 2: scan_network
    // -------------------------------------------------------------
    private static string ToolScanNetwork()
    {
        Dictionary<string, object> resp;
        if (!TrySendIpcRequest("scan_network", null, out resp, 2500))
        {
            return GetOpenVidaNotRunningPrompt("scan the vehicle CAN network");
        }

        var sb = new StringBuilder();
        sb.AppendLine("### 🛰️ Volvo Controller Area Network (CAN) Status");
        sb.AppendLine();

        if (resp.ContainsKey("ecus") && resp["ecus"] is System.Collections.IEnumerable)
        {
            var ecus = (System.Collections.IEnumerable)resp["ecus"];
            sb.AppendLine("| ECU | System Description | Network Bus | Status |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");
            int count = 0;
            foreach (object ecuObj in ecus)
            {
                var dict = ecuObj as Dictionary<string, object>;
                if (dict == null) continue;
                count++;
                string name = dict.ContainsKey("ecu") ? dict["ecu"].ToString() : "";
                string bus = dict.ContainsKey("bus") ? dict["bus"].ToString() : "CAN";
                string status = dict.ContainsKey("status") ? dict["status"].ToString() : "Responding";
                string desc = GetEcuDescription(name);
                sb.AppendLine(string.Format("| **{0}** | {1} | {2} | {3} |", name, desc, bus, status == "Responding" ? "🟢 Responding" : "⚪ " + status));
            }
            sb.AppendLine();
            sb.AppendLine(string.Format("Total responsive modules: **{0}**", count));
        }
        else
        {
            sb.AppendLine("No modules responded during network query.");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 3: read_active_dtcs
    // -------------------------------------------------------------
    private static string ToolReadActiveDtcs()
    {
        Dictionary<string, object> resp;
        if (!TrySendIpcRequest("get_active_dtcs", null, out resp, 2500))
        {
            return GetOpenVidaNotRunningPrompt("read active DTCs from the car");
        }

        var sb = new StringBuilder();
        sb.AppendLine("### ⚠️ Active Diagnostic Trouble Codes (DTCs)");
        sb.AppendLine();

        if (resp.ContainsKey("dtcs") && resp["dtcs"] is System.Collections.IEnumerable)
        {
            var dtcs = (System.Collections.IEnumerable)resp["dtcs"];
            var list = new List<Dictionary<string, object>>();
            foreach (object dtcObj in dtcs)
            {
                var dict = dtcObj as Dictionary<string, object>;
                if (dict != null) list.Add(dict);
            }

            if (list.Count == 0)
            {
                sb.AppendLine("✅ **No active trouble codes stored in vehicle modules.** All systems normal.");
            }
            else
            {
                sb.AppendLine("| DTC Code | ECU | Status | Description |");
                sb.AppendLine("| :--- | :--- | :--- | :--- |");
                foreach (var dict in list)
                {
                    string code = dict.ContainsKey("code") ? dict["code"].ToString() : "";
                    string ecu = dict.ContainsKey("ecu") ? dict["ecu"].ToString() : "";
                    string status = dict.ContainsKey("status") ? dict["status"].ToString() : "Intermittent";
                    string desc = dict.ContainsKey("description") ? dict["description"].ToString() : "";
                    string icon = status.ToLowerInvariant().Contains("perm") ? "🔴 Permanent" : "🟡 Intermittent";
                    sb.AppendLine(string.Format("| **{0}** | {1} | {2} | {3} |", code, ecu, icon, desc));
                }
                sb.AppendLine();
                sb.AppendLine(string.Format("Found **{0}** active fault codes.", list.Count));
                sb.AppendLine("💡 *Tip: Call `lookup_dtc` with any code to see detailed fault conditions, test procedures, and wiring pins.*");
            }
        }
        else
        {
            sb.AppendLine("No DTC data returned from vehicle session.");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 4: read_parameters
    // -------------------------------------------------------------
    private static string ToolReadParameters(string ecu)
    {
        var req = new Dictionary<string, object> { { "ecu", ecu } };
        Dictionary<string, object> resp;
        if (!TrySendIpcRequest("read_parameters", req, out resp, 2500))
        {
            return GetOpenVidaNotRunningPrompt("read live ECU parameters");
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format("### 📊 Live Diagnostic Parameters — Module: {0}", ecu.ToUpperInvariant()));
        sb.AppendLine();

        if (resp.ContainsKey("parameters") && resp["parameters"] is Dictionary<string, object>)
        {
            var pars = (Dictionary<string, object>)resp["parameters"];
            sb.AppendLine("| Parameter | Value |");
            sb.AppendLine("| :--- | :--- |");
            foreach (var kvp in pars)
            {
                sb.AppendLine(string.Format("| {0} | **{1}** |", kvp.Key, kvp.Value));
            }
        }
        else
        {
            sb.AppendLine("No live parameters available for this module.");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 5: lookup_dtc (Offline SQL)
    // -------------------------------------------------------------
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
                sb.AppendLine("### 🔍 Volvo DTC Knowledge Base: '" + code + "'");
                sb.AppendLine();
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    string ieId = reader.GetString(0);
                    string text = reader.GetString(1);
                    sb.AppendLine(string.Format("{0}. [`{1}`] **{2}**", count, ieId, text));
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
                        sb.AppendLine();
                        sb.AppendLine("#### 🛠️ Associated Fault Symptoms & Root Causes");
                        headerPrinted = true;
                    }
                    sb.AppendLine(string.Format("- **Symptom #{0}:** {1}", reader.GetInt32(0), reader.GetString(1)));
                }
            }
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 6: search_repair_manual (Offline SQL)
    // -------------------------------------------------------------
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
                sb.AppendLine("### 📖 Volvo Service & Repair Manual Search: '" + query + "'");
                sb.AppendLine();
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    int id = reader.GetInt32(0);
                    string title = reader.GetString(1);
                    sb.AppendLine(string.Format("{0}. **[Doc ID: {1}]** {2}", count, id, title.Trim()));
                }
                if (count == 0)
                {
                    sb.AppendLine("No repair manual documents found matching query: " + query);
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("💡 *Call `get_repair_document` with `document_id` to inspect full instructions, torque specs, and diagrams.*");
                }
            }
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 7: get_repair_document (Offline SQL)
    // -------------------------------------------------------------
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

                byte[] compressedBytes = (byte[])reader[1];
                string xmlContent = "";
                try
                {
                    using (var msIn = new MemoryStream(compressedBytes))
                    using (var zip = new ZipArchive(msIn, ZipArchiveMode.Read))
                    {
                        var entry = zip.Entries.Count > 0 ? zip.Entries[0] : null;
                        if (entry != null)
                        {
                            using (var entryStream = entry.Open())
                            using (var sr = new StreamReader(entryStream, Encoding.UTF8))
                            {
                                xmlContent = sr.ReadToEnd();
                            }
                        }
                    }
                }
                catch
                {
                    try
                    {
                        using (var msIn = new MemoryStream(compressedBytes))
                        using (var gz = new GZipStream(msIn, CompressionMode.Decompress))
                        using (var sr = new StreamReader(gz, Encoding.UTF8))
                        {
                            xmlContent = sr.ReadToEnd();
                        }
                    }
                    catch (Exception exDecomp)
                    {
                        return "Error decompressing document XML: " + exDecomp.Message;
                    }
                }

                return FormatDocumentMarkdown(docId, title, xmlContent);
            }
        }
    }

    private static string FormatDocumentMarkdown(int docId, string title, string xml)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("## 🛠️ Volvo Workshop Procedure [ID: {0}]: {1}", docId, title));
        sb.AppendLine();

        string text = Regex.Replace(xml, @"<note[^>]*>(.*?)</note>", "\n> [!NOTE]\n> $1\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<warning[^>]*>(.*?)</warning>", "\n> [!WARNING]\n> $1\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<step[^>]*>(.*?)</step>", "\n- $1", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<p[^>]*>(.*?)</p>", "\n$1\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<title[^>]*>(.*?)</title>", "\n### $1\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Web.HttpUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        sb.AppendLine(text.Trim());
        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 8: get_wiring_schematic (Offline TSV / EWD Index)
    // -------------------------------------------------------------
    private static string ToolGetWiringSchematic(string component, string publicationId)
    {
        if (string.IsNullOrEmpty(publicationId)) publicationId = "39180202";
        string rootDir = AppDomain.CurrentDomain.BaseDirectory;
        string ewdPath = Path.GetFullPath(Path.Combine(rootDir, @"..\..\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\wiring_diagrams\US_Eng\" + publicationId + @"\images\" + publicationId + "_nav.tsv"));

        if (!File.Exists(ewdPath))
        {
            ewdPath = Path.GetFullPath(Path.Combine(rootDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\wiring_diagrams\US_Eng\" + publicationId + @"\images\" + publicationId + "_nav.tsv"));
        }

        if (!File.Exists(ewdPath))
        {
            return "Wiring diagram index for publication " + publicationId + " was not found at: " + ewdPath;
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format("### ⚡ Volvo Wiring Diagrams (EWD Pub {0}): '{1}'", publicationId, component));
        sb.AppendLine();
        sb.AppendLine("| Component Code | Description | ⚡ Schematic Page | 📍 Car Placement Page |");
        sb.AppendLine("| :--- | :--- | :--- | :--- |");

        string compLower = (component ?? "").ToLowerInvariant().Trim();
        string[] lines = File.ReadAllLines(ewdPath, Encoding.UTF8);
        int matchCount = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("Code\t", StringComparison.OrdinalIgnoreCase)) continue;
            string[] parts = line.Split('\t');
            if (parts.Length < 4) continue;

            string code = parts[0].Trim();
            string desc = parts[1].Trim();
            string schemPage = parts[2].Trim();
            string placePage = parts[3].Trim();

            if (code.ToLowerInvariant().Contains(compLower) || desc.ToLowerInvariant().Contains(compLower))
            {
                matchCount++;
                string schemStr = schemPage != "0" ? "**Page " + schemPage + "**" : "-";
                string placeStr = placePage != "0" ? "**Page " + placePage + "**" : "-";
                sb.AppendLine(string.Format("| `{0}` | {1} | {2} | {3} |", code, desc, schemStr, placeStr));
            }
        }

        if (matchCount == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No electrical components matched '" + component + "'.");
            sb.AppendLine("Common codes: `1/1` Battery, `10/1` Alternator, `2/14` Glow Plug Relay, `4/56` Central Electronic Module (CEM), `6/38` Throttle Module.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine(string.Format("Found **{0}** matching components. In OpenVIDA Wiring Diagrams tab, clicking either page immediately displays the interactive vector diagram.", matchCount));
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 9: search_parts_catalog (Offline SQL EPC)
    // -------------------------------------------------------------
    private static string ToolSearchPartsCatalog(string query, int limit)
    {
        if (limit <= 0) limit = 10;
        var sb = new StringBuilder();

        using (var conn = new SqlConnection(EpcConnStr))
        {
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TOP (@limit) p.ItemNumber, l.Description, p.UnitType, " +
                                  "(SELECT TOP 1 sec_lex.Description FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id LEFT JOIN Lexicon sec_lex ON sec.DescriptionId = sec_lex.DescriptionId AND sec_lex.fkLanguage = 15 WHERE c.fkPartItem = p.Id) AS SectionName, " +
                                  "(SELECT TOP 1 SUBSTRING(sec.VCCSectionId, 8, 2) FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id WHERE c.fkPartItem = p.Id) AS FunctionGroup " +
                                  "FROM PartItems p " +
                                  "JOIN Lexicon l ON p.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                  "WHERE p.ItemNumber LIKE @q + '%' OR l.Description LIKE '%' + @q + '%' " +
                                  "ORDER BY p.ItemNumber";
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@q", (query ?? "").Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    sb.AppendLine("### 📦 Volvo Electronic Parts Catalog (EPC): '" + query + "'");
                    sb.AppendLine();
                    sb.AppendLine("| Part Number | Description | Unit | Assembly Section | Group |");
                    sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

                    int count = 0;
                    while (reader.Read())
                    {
                        count++;
                        string partNo = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                        string desc = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                        string unit = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();
                        string secName = reader.IsDBNull(3) ? "-" : reader.GetString(3).Trim();
                        string fg = reader.IsDBNull(4) ? "-" : reader.GetString(4).Trim();
                        sb.AppendLine(string.Format("| **`{0}`** | {1} | {2} | {3} | Gr. {4} |", partNo, desc, unit, secName, fg));
                    }

                    if (count == 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("No Volvo parts found matching query: " + query);
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.AppendLine(string.Format("Found **{0}** matching parts in official factory EPC database.", count));
                    }
                }
            }
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 10: decode_vin
    // -------------------------------------------------------------
    private static string ToolDecodeVin(string vin)
    {
        if (string.IsNullOrEmpty(vin) || vin.Length != 17)
        {
            return "Invalid VIN. A standard Volvo VIN must be exactly 17 characters.";
        }

        vin = vin.ToUpperInvariant();
        var sb = new StringBuilder();
        sb.AppendLine("### 🚗 Volvo VIN Decode: `" + vin + "`");
        sb.AppendLine();

        string wmi = vin.Substring(0, 3);
        string country = (wmi == "YV1") ? "Sweden (Gothenburg)" : (wmi == "YV4") ? "Sweden (Torslanda)" : (wmi == "YB1") ? "Belgium (Ghent)" : "Volvo Manufacturer";
        sb.AppendLine("- **Manufacturer & Origin:** " + country + " (`" + wmi + "`)");

        char modelCode = vin[3];
        string model = "Volvo " + modelCode;
        if (modelCode == 'B') model = "XC90";
        else if (modelCode == 'S') model = "XC70 (2008+) / V70";
        else if (modelCode == 'A') model = "S80";
        else if (modelCode == 'F') model = "S60 / V60";
        else if (modelCode == 'M') model = "C30 / S40 / V50 / C70";
        sb.AppendLine("- **Vehicle Model Series:** " + model);

        char yearChar = vin[9];
        int year = 0;
        if (yearChar >= '1' && yearChar <= '9') year = 2000 + (yearChar - '0');
        else if (yearChar == 'A') year = 2010;
        else if (yearChar == 'B') year = 2011;
        else if (yearChar == 'C') year = 2012;
        else if (yearChar == 'D') year = 2013;
        else if (yearChar == 'E') year = 2014;
        else if (yearChar == 'F') year = 2015;
        sb.AppendLine("- **Model Year:** " + (year > 0 ? year.ToString() : yearChar.ToString()));

        char plantChar = vin[10];
        string plant = (plantChar == '1') ? "Torslanda, Sweden" : (plantChar == '2') ? "Ghent, Belgium" : (plantChar == 'J') ? "Uddevalla, Sweden" : "Factory Code " + plantChar;
        sb.AppendLine("- **Assembly Plant:** " + plant);

        string serial = vin.Substring(11, 6);
        sb.AppendLine("- **Production Serial Number:** " + serial);

        return sb.ToString();
    }

    // -------------------------------------------------------------
    // TOOL 11: list_ecus
    // -------------------------------------------------------------
    private static string ToolListEcus(string query)
    {
        var ecus = new Dictionary<string, string>
        {
            { "ECM", "Engine Control Module (Bosch / Denso)" },
            { "TCM", "Transmission Control Module (Aisin TF-80SC / Powershift)" },
            { "BCM", "Brake Control Module (ABS / Stability Control / DSTC)" },
            { "CEM", "Central Electronic Module (Main Gateway & Power Distribution)" },
            { "DIM", "Driver Information Module (Instrument Cluster)" },
            { "CCM", "Climate Control Module (HVAC / Air Distribution / Damper Steppers)" },
            { "REM", "Rear Electronic Module (Tail lights, tailgate, fuel pump relay)" },
            { "DDM", "Driver Door Module (Window lift, mirror, central locking)" },
            { "PDM", "Passenger Door Module" },
            { "PSM", "Power Seat Module (Memory seat motors and position hall sensors)" },
            { "UEM", "Upper Electronic Module (Auto-dim rearview mirror, interior alarm)" },
            { "SAS", "Steering Angle Sensor" },
            { "SWM", "Steering Wheel Module (Stalk controls, cruise, wiper controls)" },
            { "DEM", "Differential Electronic Module (Haldex AWD Active Coupling)" },
            { "PBM", "Parking Brake Module (Electronic Parking Brake servo calipers)" },
            { "AUM", "Audio Module / Infotainment Head Unit" },
            { "IAM", "Integrated Audio Module (High Performance / Premium Sound)" },
            { "ICM", "Infotainment Control Module (Display & center stack keyboard)" },
            { "PAM", "Parking Assistance Module (Ultrasonic sonar sensors)" }
        };

        var sb = new StringBuilder();
        sb.AppendLine("### 🎛️ Volvo Electronic Control Units (ECUs)");
        sb.AppendLine();
        sb.AppendLine("| ECU | System Function |");
        sb.AppendLine("| :--- | :--- |");

        string q = (query ?? "").ToLowerInvariant();
        int count = 0;
        foreach (var kvp in ecus)
        {
            if (string.IsNullOrEmpty(q) || kvp.Key.ToLowerInvariant().Contains(q) || kvp.Value.ToLowerInvariant().Contains(q))
            {
                count++;
                sb.AppendLine(string.Format("| **{0}** | {1} |", kvp.Key, kvp.Value));
            }
        }

        if (count == 0)
        {
            sb.AppendLine("No ECUs matched query: " + query);
        }

        return sb.ToString();
    }

    private static string GetEcuDescription(string ecu)
    {
        switch (ecu.ToUpperInvariant())
        {
            case "ECM": return "Engine Control Module";
            case "TCM": return "Transmission Control Module";
            case "BCM": return "Brake Control Module";
            case "CEM": return "Central Electronic Module";
            case "DIM": return "Driver Information Module";
            case "CCM": return "Climate Control Module";
            case "REM": return "Rear Electronic Module";
            case "DDM": return "Driver Door Module";
            case "PDM": return "Passenger Door Module";
            case "PSM": return "Power Seat Module";
            case "UEM": return "Upper Electronic Module";
            case "SAS": return "Steering Angle Sensor";
            case "SWM": return "Steering Wheel Module";
            case "DEM": return "Differential Electronic Module (AWD)";
            case "PBM": return "Parking Brake Module";
            default: return "Control Module";
        }
    }

    // -------------------------------------------------------------
    // JSON-RPC PROTOCOL HELPERS
    // -------------------------------------------------------------
    private static void SendResult(object id, object result)
    {
        var resp = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" },
            { "id", id },
            { "result", result }
        };
        Console.WriteLine(_json.Serialize(resp));
    }

    private static void SendError(object id, int code, string message)
    {
        var err = new Dictionary<string, object>
        {
            { "code", code },
            { "message", message }
        };
        var resp = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" },
            { "id", id },
            { "error", err }
        };
        Console.WriteLine(_json.Serialize(resp));
    }
}
