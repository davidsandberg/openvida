using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace Vcc.Vida.DiagSwdl.UI.Diagnostics
{
    public class ReadoutExportResult
    {
        public bool Success { get; set; }
        public string JsonFilePath { get; set; }
        public string XmlFilePath { get; set; }
        public string Vin { get; set; }
        public int DtcCount { get; set; }
        public int EcuCount { get; set; }
        public string Message { get; set; }
    }

    public static class ReadoutExporter
    {
        private const string ConnectionString = @"Server=localhost\SQLEXPRESS;Database=DiagSwdlSession;Integrated Security=True;";

        private static string CleanString(object obj)
        {
            if (obj == null || obj == DBNull.Value) return "";
            string s = obj.ToString();
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == 0x9 || c == 0xA || c == 0xD || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        public static string GetReadoutsDirectory()
        {
            try
            {
                string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string baseDir = !string.IsNullOrEmpty(asmPath) ? Path.GetDirectoryName(asmPath) : AppDomain.CurrentDomain.BaseDirectory;

                // 1. If running from repository (e.g. c:\Users\david\git\vida\...), find repo root
                DirectoryInfo dir = new DirectoryInfo(baseDir);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "src")) && Directory.Exists(Path.Combine(dir.FullName, "assets")))
                    {
                        string repoReadouts = Path.Combine(dir.FullName, "readouts");
                        if (!Directory.Exists(repoReadouts)) Directory.CreateDirectory(repoReadouts);
                        return repoReadouts;
                    }
                    dir = dir.Parent;
                }

                // 2. In production installed mode under C:\OpenVIDA
                if (baseDir.IndexOf(@"\OpenVIDA", StringComparison.OrdinalIgnoreCase) >= 0 || Directory.Exists(@"C:\OpenVIDA\Diagnostics"))
                {
                    string prodDir = @"C:\OpenVIDA\readouts";
                    if (!Directory.Exists(prodDir)) Directory.CreateDirectory(prodDir);
                    return prodDir;
                }

                // 3. Fallback: readouts directory next to executable
                string candidate = Path.Combine(baseDir, "readouts");
                if (!Directory.Exists(candidate)) Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch
            {
                string fallback = Path.Combine(Path.GetTempPath(), "OpenVIDA_Readouts");
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        public static void OpenReadoutsFolder()
        {
            try
            {
                string dir = GetReadoutsDirectory();

                // If folder has no readouts yet, export the current baseline readout automatically
                string[] existingFiles = Directory.GetFiles(dir, "readout_*.json");
                if (existingFiles == null || existingFiles.Length == 0)
                {
                    ExportLatestReadout();
                }

                Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ReadoutExporter] Failed to open readouts folder: " + ex.Message);
            }
        }

        public static ReadoutExportResult ExportLatestReadout(string specificVin = null)
        {
            var result = new ReadoutExportResult();
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // 1. Identify WorkshopSession
                    int sessionId = 0;
                    string vin = specificVin;
                    DateTime sessionStarted = DateTime.Now;

                    using (var cmd = conn.CreateCommand())
                    {
                        if (string.IsNullOrEmpty(specificVin))
                        {
                            cmd.CommandText = "SELECT TOP 1 Id, Started, VehicleIdentifier FROM WorkshopSession ORDER BY Id DESC";
                        }
                        else
                        {
                            cmd.CommandText = "SELECT TOP 1 Id, Started, VehicleIdentifier FROM WorkshopSession WHERE VehicleIdentifier = @vin ORDER BY Id DESC";
                            cmd.Parameters.AddWithValue("@vin", specificVin);
                        }

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                sessionId = Convert.ToInt32(r["Id"]);
                                if (r["Started"] != DBNull.Value) sessionStarted = Convert.ToDateTime(r["Started"]);
                                if (r["VehicleIdentifier"] != DBNull.Value) vin = CleanString(r["VehicleIdentifier"]);
                            }
                        }
                    }

                    if (sessionId == 0)
                    {
                        result.Success = false;
                        result.Message = "No diagnostic workshop session found in database.";
                        return result;
                    }

                    result.Vin = vin;

                    // 2. Identify latest DtcReadout (preferring one that contains DTCs)
                    int readoutId = 0;
                    DateTime readoutTime = sessionStarted;
                    long globalTime = 0;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP 1 Id, Timestamp, GlobalTime FROM DtcReadout 
                            WHERE fkWorkshopSessionId = @sessionId AND Id IN (SELECT DISTINCT fkDtcReadoutId FROM Dtc)
                            ORDER BY Id DESC";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                readoutId = Convert.ToInt32(r["Id"]);
                                if (r["Timestamp"] != DBNull.Value) readoutTime = Convert.ToDateTime(r["Timestamp"]);
                                if (r["GlobalTime"] != DBNull.Value) globalTime = Convert.ToInt64(r["GlobalTime"]);
                            }
                        }
                    }

                    // Fallback to any DtcReadout for this session if none had DTCs
                    if (readoutId == 0)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 1 Id, Timestamp, GlobalTime FROM DtcReadout WHERE fkWorkshopSessionId = @sessionId ORDER BY Id DESC";
                            cmd.Parameters.AddWithValue("@sessionId", sessionId);
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    readoutId = Convert.ToInt32(r["Id"]);
                                    if (r["Timestamp"] != DBNull.Value) readoutTime = Convert.ToDateTime(r["Timestamp"]);
                                    if (r["GlobalTime"] != DBNull.Value) globalTime = Convert.ToInt64(r["GlobalTime"]);
                                }
                            }
                        }
                    }

                    // 3. Load VehConfig
                    string vehType = "135";
                    string chassis = "";
                    string factoryCode = "21";
                    string structureWeek = "";
                    string fyon = "";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 * FROM VehConfig WHERE fkworkshopSessionId = @sessionId ORDER BY Id DESC";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                if (r["VehType"] != DBNull.Value) vehType = CleanString(r["VehType"]);
                                if (r["Chassis"] != DBNull.Value) chassis = CleanString(r["Chassis"]);
                                if (r["FactoryCode"] != DBNull.Value) factoryCode = CleanString(r["FactoryCode"]);
                                if (r["StructureWeek"] != DBNull.Value) structureWeek = CleanString(r["StructureWeek"]);
                                if (r["Fyon"] != DBNull.Value) fyon = CleanString(r["Fyon"]);
                            }
                        }
                    }

                    // 4. Load EcuInfo
                    var ecus = new List<Dictionary<string, object>>();
                    var ecuIdToName = new Dictionary<int, string>();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Id, EcuType, EcuAddress, EcuIdentifier, PartNo, DiagNo, SerialNo, EcuStatus FROM EcuInfo WHERE fkWorkshopSessionId = @sessionId ORDER BY EcuAddress";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                int eId = Convert.ToInt32(r["Id"]);
                                string ident = CleanString(r["EcuIdentifier"]);
                                string type = CleanString(r["EcuType"]);
                                string name = !string.IsNullOrEmpty(ident) ? ident : type;
                                ecuIdToName[eId] = name;

                                var ecuDict = new Dictionary<string, object>();
                                ecuDict["id"] = eId;
                                ecuDict["identifier"] = name;
                                ecuDict["address"] = r["EcuAddress"] != DBNull.Value ? Convert.ToInt32(r["EcuAddress"]) : 0;
                                ecuDict["partNo"] = CleanString(r["PartNo"]);
                                ecuDict["diagNo"] = CleanString(r["DiagNo"]);
                                ecuDict["serialNo"] = CleanString(r["SerialNo"]);
                                int status = r["EcuStatus"] != DBNull.Value ? Convert.ToInt32(r["EcuStatus"]) : 0;
                                ecuDict["status"] = status == 0 ? "Responding" : "NotResponding";
                                ecuDict["software"] = new List<Dictionary<string, string>>();

                                ecus.Add(ecuDict);
                            }
                        }
                    }

                    result.EcuCount = ecus.Count;

                    // Load EcuSoftware
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT s.fkEcuInfoId, s.softwareName, s.softwarePartNo 
                            FROM EcuSoftware s 
                            JOIN EcuInfo e ON s.fkEcuInfoId = e.Id 
                            WHERE e.fkWorkshopSessionId = @sessionId";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                int fkEcuId = Convert.ToInt32(r["fkEcuInfoId"]);
                                string swName = CleanString(r["softwareName"]);
                                string swPart = CleanString(r["softwarePartNo"]);

                                foreach (var ecu in ecus)
                                {
                                    if (Convert.ToInt32(ecu["id"]) == fkEcuId)
                                    {
                                        var swList = (List<Dictionary<string, string>>)ecu["software"];
                                        var swItem = new Dictionary<string, string>();
                                        if (!string.IsNullOrEmpty(swName)) swItem["name"] = swName;
                                        swItem["partNo"] = swPart;
                                        swList.Add(swItem);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // 5. Load DTCs
                    var dtcList = new List<Dictionary<string, object>>();
                    var dtcIdToDict = new Dictionary<int, Dictionary<string, object>>();

                    if (readoutId > 0)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                SELECT d.Id, d.fkEcuInfoId, e.EcuIdentifier, d.HexValue, d.Text, d.Status, d.permanentStatus, 
                                       d.IsActive, d.MileageKilometres, d.MileageMiles, d.symptomId, d.CalculateUsingCounters
                                FROM Dtc d
                                LEFT JOIN EcuInfo e ON d.fkEcuInfoId = e.Id
                                WHERE d.fkDtcReadoutId = @readoutId
                                ORDER BY d.Id";
                            cmd.Parameters.AddWithValue("@readoutId", readoutId);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    int dId = Convert.ToInt32(r["Id"]);
                                    string ecuName = CleanString(r["EcuIdentifier"]);
                                    string hex = CleanString(r["HexValue"]);
                                    string text = CleanString(r["Text"]);
                                    string status = CleanString(r["Status"]);

                                    string code = (!string.IsNullOrEmpty(ecuName) ? ecuName + "-" : "") + hex;

                                    var dDict = new Dictionary<string, object>();
                                    dDict["id"] = dId;
                                    dDict["code"] = code;
                                    dDict["ecu"] = ecuName;
                                    dDict["hexValue"] = hex;
                                    dDict["description"] = text;
                                    dDict["status"] = status;
                                    dDict["permanentStatus"] = r["permanentStatus"] != DBNull.Value ? Convert.ToInt32(r["permanentStatus"]) : 0;
                                    dDict["isActive"] = r["IsActive"] != DBNull.Value ? Convert.ToBoolean(r["IsActive"]) : true;
                                    dDict["mileageKm"] = r["MileageKilometres"] != DBNull.Value ? Convert.ToInt32(r["MileageKilometres"]) : 0;
                                    dDict["mileageMiles"] = r["MileageMiles"] != DBNull.Value ? Convert.ToInt32(r["MileageMiles"]) : 0;
                                    dDict["symptomId"] = r["symptomId"] != DBNull.Value ? Convert.ToInt32(r["symptomId"]) : 0;
                                    dDict["freezeFrames"] = new List<Dictionary<string, object>>();

                                    dtcList.Add(dDict);
                                    dtcIdToDict[dId] = dDict;
                                }
                            }
                        }

                        // 6. Load FreezeFrameParams
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                SELECT f.fkDtcId, f.paramId, f.name, f.value, f.blockId
                                FROM FreezeFrameParam f
                                WHERE f.fkDtcId IN (SELECT Id FROM Dtc WHERE fkDtcReadoutId = @readoutId)
                                ORDER BY f.Id";
                            cmd.Parameters.AddWithValue("@readoutId", readoutId);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    int fkDtcId = Convert.ToInt32(r["fkDtcId"]);
                                    if (dtcIdToDict.ContainsKey(fkDtcId))
                                    {
                                        var ffList = (List<Dictionary<string, object>>)dtcIdToDict[fkDtcId]["freezeFrames"];
                                        var ffItem = new Dictionary<string, object>();
                                        ffItem["paramId"] = r["paramId"] != DBNull.Value ? Convert.ToInt32(r["paramId"]) : 0;
                                        ffItem["name"] = CleanString(r["name"]);
                                        ffItem["value"] = CleanString(r["value"]);
                                        ffList.Add(ffItem);
                                    }
                                }
                            }
                        }
                    }

                    result.DtcCount = dtcList.Count;

                    // 7. Save outputs
                    string readoutsDir = GetReadoutsDirectory();
                    string safeVin = !string.IsNullOrEmpty(vin) ? vin : "UNKNOWN_VIN";
                    string timestamp = readoutTime.ToString("yyyyMMdd_HHmmss");
                    string baseFilename = string.Format("readout_{0}_{1}", safeVin, timestamp);

                    string jsonPath = Path.Combine(readoutsDir, baseFilename + ".json");
                    string xmlPath = Path.Combine(readoutsDir, baseFilename + ".xml");

                    // Build JSON Model
                    var rootDict = new Dictionary<string, object>();
                    var roDict = new Dictionary<string, object>();
                    rootDict["readout"] = roDict;

                    roDict["exportedAt"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    roDict["application"] = "OpenVIDA 2014D Standalone Diagnostics";
                    roDict["sessionId"] = sessionId;
                    roDict["readoutId"] = readoutId;
                    roDict["timestamp"] = readoutTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    roDict["commTool"] = "DiCE-206751";

                    var vehDict = new Dictionary<string, object>();
                    vehDict["vin"] = safeVin;
                    vehDict["model"] = "Volvo V70 (08-)";
                    vehDict["modelYear"] = "2011";
                    vehDict["engine"] = "D5204T2";
                    vehDict["transmission"] = "M66";
                    vehDict["chassisNo"] = chassis;
                    vehDict["factoryCode"] = factoryCode;
                    vehDict["structureWeek"] = structureWeek;
                    vehDict["fyon"] = fyon;
                    vehDict["vehicleType"] = vehType;
                    roDict["vehicle"] = vehDict;

                    var sumDict = new Dictionary<string, object>();
                    sumDict["totalEcus"] = ecus.Count;
                    int respondingCount = 0;
                    foreach (var e in ecus) if (e["status"].ToString() == "Responding") respondingCount++;
                    sumDict["respondingEcus"] = respondingCount;
                    sumDict["dtcCount"] = dtcList.Count;
                    roDict["summary"] = sumDict;

                    roDict["dtcs"] = dtcList;
                    roDict["ecus"] = ecus;

                    var serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 50 * 1024 * 1024;
                    string rawJson = serializer.Serialize(rootDict);
                    string prettyJson = FormatJson(rawJson);
                    File.WriteAllText(jsonPath, prettyJson, Encoding.UTF8);
                    result.JsonFilePath = jsonPath;

                    // Build Volvo DRO XML Model
                    var xDoc = new XDocument(
                        new XDeclaration("1.0", "utf-8", "yes"),
                        new XElement("Dro",
                            new XAttribute("Version", "1.0"),
                            new XElement("GeneralInformation",
                                new XElement("VINNumber", safeVin),
                                new XElement("ChassisNo", chassis),
                                new XElement("FYON", fyon),
                                new XElement("VehicleType", vehType),
                                new XElement("StructureWeek", structureWeek),
                                new XElement("FactoryCode", factoryCode),
                                new XElement("ExecutionDate", readoutTime.ToString("yyyy-MM-dd")),
                                new XElement("ExecutionTime", readoutTime.ToString("HH:mm:ss")),
                                new XElement("GlobalTime", globalTime.ToString()),
                                new XElement("ClientVersion", "OpenVIDA 2014D Standalone"),
                                new XElement("CommTool", "DiCE-206751")
                            ),
                            new XElement("Ecus",
                                BuildEcuXmlElements(ecus)
                            ),
                            new XElement("DTCs",
                                new XAttribute("Count", dtcList.Count),
                                BuildDtcXmlElements(dtcList)
                            )
                        )
                    );

                    xDoc.Save(xmlPath);
                    result.XmlFilePath = xmlPath;

                    result.Success = true;
                    result.Message = string.Format("Exported {0} DTCs and {1} ECUs for VIN {2} to {3}", dtcList.Count, ecus.Count, safeVin, readoutsDir);
                    Console.WriteLine("[ReadoutExporter] " + result.Message);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.ToString();
                Console.WriteLine("[ReadoutExporter] Export failed: " + ex);
            }

            return result;
        }

        private static List<XElement> BuildEcuXmlElements(List<Dictionary<string, object>> ecus)
        {
            var list = new List<XElement>();
            foreach (var ecu in ecus)
            {
                var el = new XElement("Ecu",
                    new XAttribute("Address", ecu["address"]),
                    new XAttribute("Name", ecu["identifier"]),
                    new XAttribute("PartNo", ecu["partNo"]),
                    new XAttribute("DiagnosticPartNo", ecu["diagNo"]),
                    new XAttribute("SerialNo", ecu["serialNo"]),
                    new XAttribute("Status", ecu["status"])
                );

                var swList = (List<Dictionary<string, string>>)ecu["software"];
                if (swList != null && swList.Count > 0)
                {
                    var swContainer = new XElement("SoftwareList");
                    foreach (var sw in swList)
                    {
                        var swEl = new XElement("Software");
                        if (sw.ContainsKey("name") && !string.IsNullOrEmpty(sw["name"])) swEl.Add(new XAttribute("Name", sw["name"]));
                        if (sw.ContainsKey("partNo") && !string.IsNullOrEmpty(sw["partNo"])) swEl.Add(new XAttribute("PartNo", sw["partNo"]));
                        swContainer.Add(swEl);
                    }
                    el.Add(swContainer);
                }

                list.Add(el);
            }
            return list;
        }

        private static List<XElement> BuildDtcXmlElements(List<Dictionary<string, object>> dtcs)
        {
            var list = new List<XElement>();
            foreach (var dtc in dtcs)
            {
                var el = new XElement("DTC",
                    new XAttribute("Code", dtc["code"]),
                    new XAttribute("Ecu", dtc["ecu"]),
                    new XAttribute("HexValue", dtc["hexValue"]),
                    new XAttribute("Status", dtc["status"]),
                    new XAttribute("IsActive", dtc["isActive"]),
                    new XAttribute("MileageKm", dtc["mileageKm"]),
                    new XElement("Description", dtc["description"])
                );

                var ffList = (List<Dictionary<string, object>>)dtc["freezeFrames"];
                if (ffList != null && ffList.Count > 0)
                {
                    var ffContainer = new XElement("FreezeFrames", new XAttribute("Count", ffList.Count));
                    foreach (var ff in ffList)
                    {
                        ffContainer.Add(new XElement("Param",
                            new XAttribute("Id", ff["paramId"]),
                            new XAttribute("Name", ff["name"]),
                            new XAttribute("Value", ff["value"])
                        ));
                    }
                    el.Add(ffContainer);
                }

                list.Add(el);
            }
            return list;
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', ++indent * 2));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', --indent * 2));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && json[--index] == '\\') escaped = !escaped;
                        if (!escaped) quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted) sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
