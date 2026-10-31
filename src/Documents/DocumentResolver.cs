using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.ProgressHandler;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static partial class Program
{
    static string CleanDocId(string id)

    {

        if (string.IsNullOrEmpty(id)) return "";

        string clean = id.Trim();

        if (clean.Length > 5 && clean[2] == '-') clean = clean.Substring(5);

        if (clean.Contains("#")) clean = clean.Substring(0, clean.IndexOf("#"));
        if (clean.Contains("_nev")) clean = clean.Substring(0, clean.IndexOf("_nev"));

        return clean.Trim();

    }

    static string GetDocumentTitle(string docId)

    {

        if (string.IsNullOrEmpty(docId)) return "";

        try

        {

            string cleanId = CleanDocId(docId);

            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))

            {

                conn.Open();

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT TOP 1 title FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";

                    cmd.Parameters.AddWithValue("@id", cleanId);

                    var obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();

                }

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT TOP 1 title FROM TreeItem WHERE chronicleId = @id OR NevisId = @id";

                    cmd.Parameters.AddWithValue("@id", cleanId);

                    var obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();

                }

            }

        }

        catch {}

        return "";

    }

        static string StemWord(string w)
    {
        if (string.IsNullOrEmpty(w)) return "";
        string s = w.ToLower();
        if (s.EndsWith("age") && s.Length > 5) return s.Substring(0, s.Length - 3);
        if (s.EndsWith("ing") && s.Length > 5) return s.Substring(0, s.Length - 3);
        if (s.EndsWith("tion") && s.Length > 6) return s.Substring(0, s.Length - 4);
        if (s.EndsWith("es") && s.Length > 5) return s.Substring(0, s.Length - 2);
        if (s.EndsWith("s") && s.Length > 4 && !s.EndsWith("ss")) return s.Substring(0, s.Length - 1);
        return s;
    }

    static string ResolveDocumentId(string rawId, string title)
    {
        if (string.IsNullOrEmpty(rawId) && string.IsNullOrEmpty(title)) return null;

        try
        {
            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
            {
                conn.Open();

                // 1. Direct match in Document (chronicleId, nevisId, or projectDocumentId)
                if (!string.IsNullOrEmpty(rawId))
                {
                    string clean = CleanDocId(rawId);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@id", clean);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 2. Via TreeItem -> TreeItemDocument -> Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.chronicleId = @id OR ti.NevisId = @id 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@id", clean);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }
                }

                // 3. Match by DTC regex in title or rawId
                string queryText = !string.IsNullOrEmpty(title) ? title : rawId;
                if (!string.IsNullOrEmpty(queryText))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(queryText, @"([A-Z]{3,4}-[A-Z0-9]{4,7})");
                    if (m.Success)
                    {
                        string dtc = m.Groups[1].Value;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title LIKE @dtc ORDER BY version DESC";
                            cmd.Parameters.AddWithValue("@dtc", "%" + dtc + "%");
                            var obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                        }
                    }

                    // 4. Exact title in TreeItem -> TreeItemDocument -> Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.title = @t AND ti.title != '' 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@t", queryText.Trim());
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 5. Exact title in Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title = @t AND title != '' ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@t", queryText.Trim());
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 6. Structured Symptom Title keyword match (e.g. "Fuel system, Fuel leakage, XY", "Steering, Oil leakage, U6")
                    if (queryText.Contains(","))
                    {
                        string[] parts = queryText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "and", "the", "for", "with", "system", "from", "when", "into", "over", "under", "systemet", "does", "not", "occur"
                        };

                        var defectWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "leak", "noise", "vibrat", "squeal", "rattle", "whine", "click", "clunk",
                            "smell", "smoke", "hesitat", "stall", "misfir", "start", "turn", "drain",
                            "discharg", "play", "slip", "bind", "jam", "stick", "loos", "damag", "wear"
                        };

                        var compWords = new List<string>();
                        if (parts.Length >= 1)
                        {
                            string[] rawComp = System.Text.RegularExpressions.Regex.Split(parts[0], @"[\s/,\-]+");
                            foreach (var w in rawComp)
                            {
                                if (w.Length >= 3 && !stopWords.Contains(w))
                                {
                                    string st = StemWord(w);
                                    if (st.Length >= 3 && !compWords.Contains(st)) compWords.Add(st);
                                }
                            }
                        }

                        var devWords = new List<string>();
                        if (parts.Length >= 2)
                        {
                            string[] rawDev = System.Text.RegularExpressions.Regex.Split(parts[1], @"[\s/,\-]+");
                            var priorityDev = new List<string>();
                            var otherDev = new List<string>();

                            foreach (var w in rawDev)
                            {
                                if (w.Length >= 3 && !stopWords.Contains(w))
                                {
                                    string st = StemWord(w);
                                    if (st.Length >= 3)
                                    {
                                        if (defectWords.Contains(st))
                                        {
                                            if (!priorityDev.Contains(st)) priorityDev.Add(st);
                                        }
                                        else
                                        {
                                            if (!otherDev.Contains(st)) otherDev.Add(st);
                                        }
                                    }
                                }
                            }

                            // Prioritize defect/action words (e.g. "leak") before generic nouns (e.g. "oil")
                            devWords.AddRange(priorityDev);
                            devWords.AddRange(otherDev);
                        }

                        // Try each prioritized deviation word with each component word in Document
                        foreach (var wDev in devWords)
                        {
                            foreach (var wComp in compWords)
                            {
                                using (var cmdDoc = conn.CreateCommand())
                                {
                                    cmdDoc.CommandText = @"SELECT TOP 1 chronicleId 
                                                           FROM Document 
                                                           WHERE title LIKE @c AND title LIKE @d 
                                                           ORDER BY CASE 
                                                             WHEN title LIKE '%checking%' OR title LIKE '%tracing%' OR title LIKE '%corrective%' OR title LIKE '%action%' THEN 0 
                                                             WHEN title LIKE '%diagnos%' OR title LIKE '%leak%' OR title LIKE '%fault%' THEN 1 
                                                             ELSE 2 
                                                           END, version DESC";
                                    cmdDoc.Parameters.AddWithValue("@c", "%" + wComp + "%");
                                    cmdDoc.Parameters.AddWithValue("@d", "%" + wDev + "%");
                                    var oDoc = cmdDoc.ExecuteScalar();
                                    if (oDoc != null && oDoc != DBNull.Value) return oDoc.ToString().Trim();
                                }
                            }
                        }

                        // Try TreeItem with dev + comp
                        foreach (var wDev in devWords)
                        {
                            foreach (var wComp in compWords)
                            {
                                using (var cmdTi = conn.CreateCommand())
                                {
                                    cmdTi.CommandText = @"SELECT TOP 1 d.chronicleId 
                                                          FROM TreeItem ti 
                                                          JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                                          JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                                          WHERE ti.title LIKE @c AND ti.title LIKE @d 
                                                          ORDER BY d.id, d.version DESC";
                                    cmdTi.Parameters.AddWithValue("@c", "%" + wComp + "%");
                                    cmdTi.Parameters.AddWithValue("@d", "%" + wDev + "%");
                                    var oTi = cmdTi.ExecuteScalar();
                                    if (oTi != null && oTi != DBNull.Value) return oTi.ToString().Trim();
                                }
                            }
                        }
                    }

                    // 7. CSC Code resolution via CarCom -> DiagSwdlRepository.SymptomIEMap
                    var mCsc = System.Text.RegularExpressions.Regex.Match(queryText, @"(?:^|,\s*)([A-Z0-9]{2,3})\s*$");
                    if (mCsc.Success)
                    {
                        string cscCode = mCsc.Groups[1].Value.Trim();
                        try
                        {
                            using (var carConn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))
                            {
                                carConn.Open();
                                int cscId = 0;
                                using (var cmdCsc = carConn.CreateCommand())
                                {
                                    cmdCsc.CommandText = "SELECT TOP 1 id FROM T158_Symptom_CSC WHERE CSC = @c";
                                    cmdCsc.Parameters.AddWithValue("@c", cscCode);
                                    var oId = cmdCsc.ExecuteScalar();
                                    if (oId != null && oId != DBNull.Value) cscId = Convert.ToInt32(oId);
                                }
                                if (cscId > 0)
                                {
                                    using (var diagConn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))
                                    {
                                        diagConn.Open();
                                        using (var cmdDiag = diagConn.CreateCommand())
                                        {
                                            cmdDiag.CommandText = "SELECT TOP 1 fkIE FROM SymptomIEMap WHERE fkSymptom = @id AND fkIE IS NOT NULL AND fkIE <> ''";
                                            cmdDiag.Parameters.AddWithValue("@id", cscId);
                                            var oIe = cmdDiag.ExecuteScalar();
                                            if (oIe != null && oIe != DBNull.Value)
                                            {
                                                return oIe.ToString().Trim();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch {}
                    }

                    // 8. Substring title in TreeItem
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.title LIKE @like AND ti.title != '' 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@like", "%" + queryText.Trim() + "%");
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 9. Substring title in Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title LIKE @like AND title != '' ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@like", "%" + queryText.Trim() + "%");
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }
                }
            }
        }
        catch {}

        return !string.IsNullOrEmpty(rawId) ? rawId : null;
    }

    static string LookupDocumentIdByDtcOrTitle(string query)

    {

        if (string.IsNullOrEmpty(query)) return null;

        try

        {

            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))

            {

                conn.Open();

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title = @q OR title LIKE @like ORDER BY version DESC";

                    cmd.Parameters.AddWithValue("@q", query);

                    cmd.Parameters.AddWithValue("@like", "%" + query + "%");

                    var obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();

                }

            }

        }

        catch {}

        return null;

    }


    static string GetGroupName(string key)

    {

        switch (key)

        {

            case "0": return "0 - Administration & Special Tools";

            case "1": return "1 - Maintenance & Service Material";

            case "2": return "2 - Engine & Equipment";

            case "3": return "3 - Electrical System";

            case "4": return "4 - Power Transmission (Gearbox/Clutch)";

            case "5": return "5 - Brakes";

            case "6": return "6 - Suspension & Steering";

            case "7": return "7 - Springs, Shock Absorbers & Wheels";

            case "8": return "8 - Body & Interior";

            case "9": return "9 - Miscellaneous";

            default: return "Other Documents";

        }

    }

    static string LookupDocumentIdBySymptomId(int symptomId)

    {

        try

        {

            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))

            {

                conn.Open();

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT TOP 1 fkIE FROM SymptomIEMap WHERE fkSymptom = @sId AND fkIE IS NOT NULL AND fkIE <> ''";

                    cmd.Parameters.AddWithValue("@sId", symptomId);

                    var obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();

                }

            }

        }

        catch {}

        return null;

    }

}
