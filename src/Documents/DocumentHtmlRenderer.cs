using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
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
    static string GetImageSrc(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return null;
        try
        {
            byte[] imgBytes = null;
            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=imagerepository;Integrated Security=True"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TOP 1 imageData FROM LocalizedGraphics WHERE path = @p OR path LIKE '%' + @p";
                    cmd.Parameters.AddWithValue("@p", filename);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value) imgBytes = (byte[])obj;
                }
            }
            if (imgBytes != null && imgBytes.Length > 0)
            {
                string mime = filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
                              filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/gif";
                return string.Format("data:{0};base64,{1}", mime, Convert.ToBase64String(imgBytes));
            }
        }
        catch {}
        return null;
    }

    static string GetImageTag(string filename)
    {
        string src = GetImageSrc(filename);
        if (!string.IsNullOrEmpty(src))
        {
            return string.Format("<div style='text-align:center; margin:16px 0;'><img class='doc-img' src='{0}' alt='{1}' /></div>", src, filename);
        }
        return "";
    }

    static string RenderDocumentHtml(string docId, string highlight)

    {

        try

        {

            string cleanId = CleanDocId(docId);
            string targetAnchor = "";
            int hashIdx = (docId ?? "").IndexOf('#');
            if (hashIdx >= 0) targetAnchor = docId.Substring(hashIdx + 1).Trim();

            var docs = new System.Collections.Generic.List<Tuple<string, byte[]>>();

            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))

            {

                conn.Open();

                // Try 1: Direct in Document

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT TOP 1 title, XmlContent FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";

                    cmd.Parameters.AddWithValue("@id", cleanId);

                    using (var r = cmd.ExecuteReader())

                    {

                        if (r.Read() && r[1] != DBNull.Value)

                        {

                            docs.Add(Tuple.Create(r[0] != DBNull.Value ? r[0].ToString().Trim() : "", (byte[])r[1]));

                        }

                    }

                }

                                // If found single document (e.g. DTC Information), also check if it belongs to a TreeItem with associated documents (e.g. Fault-tracing)
                if (docs.Count == 1)
                {
                    try
                    {
                        using (var cmdSib = conn.CreateCommand())
                        {
                            cmdSib.CommandText = @"
                                SELECT d2.projectDocumentId, d2.title, d2.XmlContent, d2.version
                                FROM Document d2
                                WHERE d2.projectDocumentId IN (
                                    SELECT tid2.projectDocumentTo
                                    FROM Document d1
                                    JOIN TreeItemDocument tid1 ON d1.projectDocumentId = tid1.projectDocumentTo
                                    JOIN TreeItemDocument tid2 ON tid1.fkTreeItem = tid2.fkTreeItem
                                    WHERE (d1.chronicleId = @id OR d1.nevisId = @id OR d1.projectDocumentId = @id)
                                      AND tid2.projectDocumentTo != d1.projectDocumentId
                                )
                                ORDER BY d2.version DESC";
                            cmdSib.Parameters.AddWithValue("@id", cleanId);
                            using (var rSib = cmdSib.ExecuteReader())
                            {
                                var seenPds = new System.Collections.Generic.HashSet<string>();
                                foreach (var d in docs) seenPds.Add(d.Item1);
                                while (rSib.Read())
                                {
                                    string sibTitle = rSib[1] != DBNull.Value ? rSib[1].ToString().Trim() : "";
                                    if (!seenPds.Contains(sibTitle) && rSib[2] != DBNull.Value)
                                    {
                                        seenPds.Add(sibTitle);
                                        docs.Add(Tuple.Create(sibTitle, (byte[])rSib[2]));
                                    }
                                }
                            }
                        }
                    }
                    catch {}
                }

                // Try 2: Via TreeItem

                if (docs.Count == 0)

                {

                    using (var cmd = conn.CreateCommand())

                    {

                        cmd.CommandText = @"

                            SELECT d.projectDocumentId, d.title, d.XmlContent, d.version

                            FROM TreeItem ti

                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem

                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId

                            WHERE ti.chronicleId = @id OR ti.NevisId = @id

                            ORDER BY d.id, d.version DESC";

                        cmd.Parameters.AddWithValue("@id", cleanId);

                        using (var r = cmd.ExecuteReader())

                        {

                            var seenPds = new System.Collections.Generic.HashSet<string>();

                            while (r.Read())

                            {

                                string pd = r[0].ToString().Trim();

                                if (!seenPds.Contains(pd))

                                {

                                    seenPds.Add(pd);

                                    if (r[2] != DBNull.Value)

                                    {

                                        docs.Add(Tuple.Create(r[1] != DBNull.Value ? r[1].ToString().Trim() : "", (byte[])r[2]));

                                    }

                                }

                            }

                        }

                    }

                }

            }

            if (docs.Count == 0)

            {

                // Check if this ID is a Diagnostic Script from DiagSwdlRepository
                string scriptHtml = TryRenderDiagnosticScriptHtml(cleanId, highlight);
                if (!string.IsNullOrEmpty(scriptHtml))
                {
                    return scriptHtml;
                }

                string extraInfo = "";

                try

                {

                    using (var conn2 = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))

                    {

                        conn2.Open();

                        using (var cmd2 = conn2.CreateCommand())

                        {

                            cmd2.CommandText = "SELECT TOP 1 vadisFaultDescription, vadisActionDescription FROM CarComSymptom WHERE symptomCode = @id OR symptomId = @id";

                            cmd2.Parameters.AddWithValue("@id", cleanId);

                            using (var r2 = cmd2.ExecuteReader())

                            {

                                if (r2.Read())

                                {

                                    if (r2[0] != DBNull.Value) extraInfo += "<p><strong>Fault Description:</strong> " + r2[0].ToString() + "</p>";

                                    if (r2[1] != DBNull.Value) extraInfo += "<p><strong>Recommended Action:</strong> " + r2[1].ToString() + "</p>";

                                }

                            }

                        }

                    }

                }

                catch {}

                return "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" +

                       "body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 24px 32px; color: #333; line-height: 1.6; }" +

                       ".card { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 5px solid #005087; border-radius: 6px; padding: 20px; }" +

                       "h2 { color: #003057; margin-top: 0; }" +

                       ".badge { display: inline-block; background: #003057; color: white; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 13px; }" +

                       "</style></head><body><div class='card'>" +

                       "<h2><span class='badge'>VIDA Technical Reference</span> " + docId + "</h2>" +

                       "<p style='color: #475569;'>Diagnostic specification & procedure reference for vehicle model <strong>V70 (08-) / D5204T3</strong>.</p>" +

                       extraInfo +

                       "<p style='color: #64748b; font-size: 13px; margin-top: 16px;'>Standard vehicle service information. Use the <strong>📖 Workshop Service Manuals & Search</strong> tab in Reference Information to explore comprehensive wiring diagrams, step-by-step repair guides, and component specifications.</p>" +

                       "</div></body></html>";

            }

            var sb = new System.Text.StringBuilder();

            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><meta http-equiv='X-UA-Compatible' content='IE=edge'>");

            sb.Append("<style>");

            sb.Append("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 24px 32px; padding-bottom: 80px; box-sizing: border-box; line-height: 1.6; color: #222; background: #fff; }");

            sb.Append(".header-bar { display: flex; justify-content: space-between; align-items: baseline; border-bottom: 2px solid #003057; padding-bottom: 10px; margin-bottom: 20px; }");

            sb.Append("h1 { color: #003057; font-size: 20px; margin: 0; font-weight: 600; }");

            sb.Append("h2 { color: #005087; margin-top: 24px; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; font-weight: 600; }");

            sb.Append("h3 { color: #1e293b; margin-top: 18px; font-size: 14.5px; font-weight: 600; }");

            sb.Append(".section-box { margin: 16px 0 28px 0; padding: 18px 22px 24px 22px; border-left: 4px solid #005087; background: #f8fafc; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }");

            sb.Append(".step-box { margin: 14px 0 20px 0; padding: 14px 18px; border-left: 4px solid #0284c7; background: #ffffff; border: 1px solid #e2e8f0; border-left-width: 4px; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.03); }");

            sb.Append(".highlight { background: #fffde7 !important; border-left: 5px solid #f59e0b !important; box-shadow: 0 0 12px rgba(245, 158, 11, 0.25); }");

            sb.Append(".step-title { font-weight: 600; font-size: 15px; color: #003057; margin-bottom: 8px; }");

            sb.Append(".hint { background: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #1e3a8a; }");

            sb.Append(".note { background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #78350f; }");

            sb.Append(".caution { background: #fff7ed; border-left: 4px solid #ea580c; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #9a3412; font-weight: 500; }");

            sb.Append(".warning { background: #fef2f2; border-left: 4px solid #ef4444; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #991b1b; font-weight: 500; }");

            sb.Append(".question-box { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #005087; padding: 12px 16px; margin: 16px 0; border-radius: 4px; font-weight: 500; color: #1e293b; }");

            sb.Append(".btn-print { background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 4px; padding: 4px 12px; font-size: 12px; font-weight: 600; color: #334155; cursor: pointer; text-decoration: none; }");

            sb.Append(".btn-print:hover { background: #e2e8f0; color: #0f172a; }");

            sb.Append("p { margin: 6px 0; font-size: 13.5px; }");

            sb.Append("ul { margin: 6px 0 12px 24px; padding: 0; font-size: 13.5px; }");

            sb.Append("li { margin: 4px 0; }");

            sb.Append("table { border-collapse: collapse; width: 100%; margin: 14px 0; font-size: 13px; }");

            sb.Append("th, td { border: 1px solid #cbd5e1; padding: 8px 12px; text-align: left; }");

            sb.Append("th { background: #f1f5f9; font-weight: 600; color: #1e293b; }");

            sb.Append(".badge-sec { display: inline-block; padding: 2px 8px; font-size: 11px; font-weight: 600; text-transform: uppercase; border-radius: 3px; background: #e0f2fe; color: #0369a1; margin-bottom: 8px; }");

            sb.Append(".doc-link { color: #0284c7; text-decoration: underline; cursor: pointer; font-weight: 500; }");

            sb.Append(".doc-link:hover { color: #0369a1; }");

            sb.Append(".doc-img { max-width: 95%; height: auto; border: 1px solid #cbd5e1; border-radius: 4px; padding: 4px; background: #fff; box-shadow: 0 1px 4px rgba(0,0,0,0.08); }");
            sb.Append(".diag-step { display: flex; gap: 16px; align-items: flex-start; margin: 14px 0; padding: 12px 16px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; }");
            sb.Append(".diag-graphic { flex-shrink: 0; max-width: 200px; text-align: center; }");
            sb.Append(".diag-graphic img { max-width: 180px; max-height: 140px; height: auto; width: auto; border: 1px solid #cbd5e1; border-radius: 4px; padding: 2px; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.06); }");
            sb.Append(".diag-content { flex-grow: 1; }");
            sb.Append(".diag-content ul { margin: 4px 0 8px 18px; padding: 0; }");
            sb.Append(".diag-content li { margin: 4px 0; font-size: 13.5px; color: #1e293b; }");
            sb.Append(".vct-tool-container { margin: 10px 0; }");
            sb.Append(".vct-btn { display: inline-flex; align-items: center; gap: 10px; background: #005087; color: #ffffff !important; text-decoration: none !important; padding: 10px 20px; border-radius: 6px; font-size: 13.5px; font-weight: 600; box-shadow: 0 2px 4px rgba(0,48,87,0.25); transition: all 0.15s ease; cursor: pointer; border: 1px solid #003b66; }");
            sb.Append(".vct-btn:hover { background: #00385e; color: #ffffff !important; box-shadow: 0 3px 8px rgba(0,48,87,0.35); }");
            sb.Append(".vct-btn img, .vct-icon { width: 32px !important; height: 32px !important; max-width: 32px !important; max-height: 32px !important; object-fit: contain; border: none !important; background: transparent !important; box-shadow: none !important; padding: 0 !important; margin: 0 !important; vertical-align: middle; }");
            sb.Append(".vct-btn .vct-badge { background: #003057; color: #93c5fd; font-size: 11px; font-weight: 700; padding: 3px 7px; border-radius: 3px; letter-spacing: 0.5px; }");
            sb.Append(".vct-btn .vct-title { font-size: 13.5px; font-weight: 600; color: #ffffff; }");
            sb.Append(".smarttool-box { background: #f8fafc; border: 1px solid #cbd5e1; border-left: 5px solid #005087; border-radius: 4px; padding: 14px 18px; margin: 16px 0; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }");
            sb.Append(".smarttool-header { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }");
            sb.Append(".smarttool-badge { background: #003057; color: white; font-size: 11px; font-weight: 700; padding: 3px 8px; border-radius: 3px; letter-spacing: 0.5px; }");
            sb.Append(".smarttool-title { font-size: 14px; font-weight: 600; color: #003057; }");
            sb.Append(".smarttool-action { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; }");
            sb.Append(".btn-smarttool { display: inline-flex; align-items: center; background: #005087; color: #ffffff !important; text-decoration: none !important; padding: 8px 18px; border-radius: 4px; font-size: 13px; font-weight: 600; box-shadow: 0 1px 3px rgba(0,0,0,0.12); transition: background 0.15s ease; }");
            sb.Append(".btn-smarttool:hover { background: #003b66; color: #ffffff !important; }");
            sb.Append(".smarttool-id { font-size: 12px; color: #475569; }");
            sb.Append(".smarttool-id code { background: #e2e8f0; padding: 2px 6px; border-radius: 3px; font-family: Consolas, monospace; font-size: 11.5px; }");

            sb.Append("@media print { .btn-print { display: none; } body { margin: 10px; } }");

            sb.Append("</style></head><body>");

            bool foundTarget = false;

            int docIdx = 0;

            foreach (var docPair in docs)

            {

                docIdx++;

                string xmlContent = "";

                using (var ms = new System.IO.MemoryStream(docPair.Item2))

                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))

                {

                    foreach (var entry in zip.Entries)

                    {

                        using (var r = new System.IO.StreamReader(entry.Open(), System.Text.Encoding.UTF8))

                        {

                            xmlContent = r.ReadToEnd();

                            break;

                        }

                    }

                }

                var doc = System.Xml.Linq.XDocument.Parse(xmlContent);

                var mainTitle = doc.Root.Element("title");

                string docTitle = mainTitle != null ? mainTitle.Value.Trim() : docPair.Item1;

                if (string.IsNullOrEmpty(docTitle)) docTitle = "Document " + cleanId;

                sb.Append("<div class='header-bar'>");

                sb.AppendFormat("<h1>{0}</h1>", docTitle);

                if (docIdx == 1) sb.Append("<button class='btn-print' onclick='window.print();'>Print</button>");

                sb.Append("</div>");

                sb.Append("<div class='section-box'>");

                RenderXmlRecursive(doc.Root, sb, highlight, ref foundTarget);

                sb.Append("</div>");

            }

            if (!string.IsNullOrEmpty(targetAnchor))
            {
                sb.AppendFormat("<script>window.onload = function() {{ var el = document.getElementById('{0}'); if(el) el.scrollIntoView({{behavior: 'smooth', block: 'start'}}); }};</script>", targetAnchor);
            }
            else if (foundTarget)
            {
                sb.Append("<script>window.onload = function() { var el = document.getElementById('target'); if(el) el.scrollIntoView({behavior: 'smooth', block: 'center'}); };</script>");
            }

            sb.Append("<div style='height: 60px;'></div>");
            sb.Append("</body></html>");

            return sb.ToString();

        }

        catch (Exception ex)

        {

            return "<html><body><h3>Error rendering document: " + ex.Message + "</h3></body></html>";

        }

    }

    static void RenderXmlRecursive(System.Xml.Linq.XElement el, System.Text.StringBuilder sb, string highlight, ref bool foundTarget)

    {

        string name = el.Name.LocalName;

        if (name == "title" && el.Parent != null && el.Parent.Name.LocalName == "servinfosub") return;

        if (name == "condition")

        {

            string condType = (string)el.Attribute("type") ?? "";

            if (condType == "condition") sb.Append("<div class='badge-sec'>Condition / Description</div>");

            else if (condType == "substitution") sb.Append("<div class='badge-sec'>Substitution Value</div>");

            else if (condType == "symptom") sb.Append("<div class='badge-sec'>Fault Symptom(s)</div>");

            foreach (var child in el.Elements()) RenderXmlRecursive(child, sb, highlight, ref foundTarget);

        }

        else if (name == "ps")

        {

            sb.Append("<div class='badge-sec'>Possible Source(s)</div>");

            foreach (var child in el.Elements()) RenderXmlRecursive(child, sb, highlight, ref foundTarget);

        }

        else if (name == "testgrp")

        {

            var title = el.Element("title");

            string stepTitle = title != null ? title.Value.Trim() : "";

            string tgId = (string)el.Attribute("id") ?? "";
            if (!string.IsNullOrEmpty(tgId))
                sb.AppendFormat("<div class='step-box' id='{0}'>", tgId);
            else
                sb.Append("<div class='step-box'>");

            if (!string.IsNullOrEmpty(stepTitle)) sb.AppendFormat("<div class='step-title'>{0}</div>", stepTitle);

            foreach (var child in el.Elements())

            {

                if (child.Name.LocalName == "title") continue;

                RenderXmlRecursive(child, sb, highlight, ref foundTarget);

            }

            sb.Append("</div>");

        }

        else if (name == "stepgrp")

        {

            var title = el.Element("title");

            if (title != null && !string.IsNullOrEmpty(title.Value.Trim()))

            {

                sb.AppendFormat("<h2>{0}</h2>", title.Value.Trim());

            }

            foreach (var child in el.Elements())

            {

                if (child.Name.LocalName == "title") continue;

                RenderXmlRecursive(child, sb, highlight, ref foundTarget);

            }

        }

        else if (name == "procstep")

        {

            var title = el.Element("title");

            string stepTitle = title != null ? title.Value.Trim() : "";

            bool isMatch = !string.IsNullOrEmpty(highlight) && stepTitle.IndexOf(highlight, StringComparison.OrdinalIgnoreCase) >= 0;

            string targetAttr = "";

            if (isMatch && !foundTarget)

            {

                targetAttr = " id='target'";

                foundTarget = true;

            }

            sb.AppendFormat("<div class='step-box{0}'{1}>", isMatch ? " highlight" : "", targetAttr);

            if (!string.IsNullOrEmpty(stepTitle)) sb.AppendFormat("<div class='step-title'>{0}</div>", stepTitle);

            foreach (var child in el.Elements())

            {

                if (child.Name.LocalName == "title") continue;

                RenderXmlRecursive(child, sb, highlight, ref foundTarget);

            }

            sb.Append("</div>");

        }

        else if (name == "ptxt" || name == "para")

        {

            var xrefs = new System.Collections.Generic.List<System.Xml.Linq.XElement>(el.Descendants("xref"));

            if (xrefs.Count > 0)

            {

                foreach (var xr in xrefs) RenderXref(xr, sb);

            }

            else

            {

                string txt = el.Value.Trim();

                if (!string.IsNullOrEmpty(txt)) sb.AppendFormat("<p>{0}</p>", txt);

            }

        }

        else if (name == "xref")

        {

            RenderXref(el, sb);

        }

        else if (name == "note") RenderNote(el, sb);

        else if (name == "caution") RenderCaution(el, sb);

        else if (name == "warning") RenderWarning(el, sb);

        else if (name == "hint")

        {

            sb.Append("<div class='hint'><strong>Hint:</strong> ");

            foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");

            sb.Append("</div>");

        }

        else if (name == "question")

        {

            sb.AppendFormat("<p class='question'>{0}</p>", el.Value.Trim());

        }

        else if (name == "graphic" || name == "grate")

        {

            foreach (var href in el.Descendants("href"))

            {

                if (!string.IsNullOrEmpty(href.Value))

                {

                    string imgTag = GetImageTag(href.Value.Trim());

                    if (!string.IsNullOrEmpty(imgTag)) sb.Append(imgTag);

                }

            }

            var graphtext = el.Element("graphtext");

            if (graphtext != null) RenderXmlRecursive(graphtext, sb, highlight, ref foundTarget);

        }

        else if (name == "list1" || name == "list2")
        {
            var listTitle = el.Element("title");
            if (listTitle != null) sb.AppendFormat("<h3>{0}</h3>", listTitle.Value.Trim());
            string enumType = (string)el.Attribute("enumtype") ?? "";
            bool isOrdered = enumType == "n" || enumType == "1";
            string listTag = isOrdered ? "ol" : "ul";
            sb.AppendFormat("<{0}>", listTag);
            foreach (var item in el.Elements("item"))
            {
                sb.Append("<li>");
                var xrefs = new System.Collections.Generic.List<System.Xml.Linq.XElement>(item.Descendants("xref"));
                if (xrefs.Count > 0)
                {
                    foreach (var xr in xrefs) RenderXref(xr, sb);
                }
                else
                {
                    foreach (var p in item.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");
                }
                sb.Append("</li>");
            }
            sb.AppendFormat("</{0}>", listTag);
        }
        else if (name == "diaglist")
        {
            RenderDiaglist(el, sb);
        }
        else if (name == "smarttool")
        {
            if (!el.Ancestors("diaglist").Any())
            {
                RenderSmarttool(el, sb, null);
            }
        }

        else if (name == "table1" || name == "table")

        {

            sb.Append("<table>");

            foreach (var row in el.Descendants("row"))

            {

                sb.Append("<tr>");

                foreach (var entry in row.Elements("entry"))

                {

                    sb.AppendFormat("<td>{0}</td>", entry.Value.Trim());

                }

                sb.Append("</tr>");

            }

            sb.Append("</table>");

        }

        else

        {

            foreach (var child in el.Elements())

            {

                RenderXmlRecursive(child, sb, highlight, ref foundTarget);

            }

        }

    }

    static void RenderXref(System.Xml.Linq.XElement xref, System.Text.StringBuilder sb)

    {

        foreach (var href in xref.Descendants("href"))

        {

            string linkTitle = (string)href.Attribute("title") ?? "";

            string target = href.Value != null ? href.Value.Trim() : "";

            if (string.IsNullOrEmpty(linkTitle)) linkTitle = target;

            sb.AppendFormat("<div style='margin:4px 0;'><a class='doc-link' href='vida-doc:{0}'>🔗 {1}</a></div>", target, linkTitle);

        }

    }

    static void RenderNote(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)

    {

        sb.Append("<div class='note'><strong>Note:</strong> ");

        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");

        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());

        sb.Append("</div>");

    }

    static void RenderCaution(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)

    {

        sb.Append("<div class='caution'><strong>Caution:</strong> ");

        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");

        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());

        sb.Append("</div>");

    }

    static void RenderWarning(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)

    {

        sb.Append("<div class='warning'><strong>Warning:</strong> ");

        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");

        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());

        sb.Append("</div>");

    }


    static void RenderDiaglist(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)
    {
        string iconSrc = null;
        var graphicEl = el.Descendants("graphic").FirstOrDefault();
        if (graphicEl != null)
        {
            var hrefEl = graphicEl.Element("href");
            if (hrefEl != null && !string.IsNullOrEmpty(hrefEl.Value))
            {
                iconSrc = GetImageSrc(hrefEl.Value.Trim());
            }
        }

        var smartTools = el.Descendants("smarttool").ToList();
        var ditems = el.Descendants("ditem").ToList();

        sb.Append("<div class='diag-step'>");

        // If there is an illustration graphic (and no smarttools), show as step illustration
        if (!string.IsNullOrEmpty(iconSrc) && smartTools.Count == 0)
        {
            sb.AppendFormat("<div class='diag-graphic'><img src='{0}' alt='Step illustration' /></div>", iconSrc);
        }

        sb.Append("<div class='diag-content'>");

        // Render instruction items
        if (ditems.Count > 0)
        {
            sb.Append("<ul>");
            foreach (var item in ditems)
            {
                var texts = item.Descendants("ptxt").Select(p => p.Value.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                if (texts.Count > 0)
                {
                    foreach (var t in texts)
                    {
                        sb.AppendFormat("<li>{0}</li>", System.Security.SecurityElement.Escape(t));
                    }
                }
                else
                {
                    string raw = item.Value.Trim();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        sb.AppendFormat("<li>{0}</li>", System.Security.SecurityElement.Escape(raw));
                    }
                }
            }
            sb.Append("</ul>");
        }

        // Render smarttools
        foreach (var st in smartTools)
        {
            RenderSmarttool(st, sb, iconSrc);
        }

        sb.Append("</div>"); // .diag-content
        sb.Append("</div>"); // .diag-step
    }

    static void RenderSmarttool(System.Xml.Linq.XElement st, System.Text.StringBuilder sb, string iconSrc)
    {
        var href = st.Element("href");
        string scriptId = href != null ? href.Value.Trim() : st.Value.Trim();
        string routineTitle = href != null ? (string)href.Attribute("title") ?? "" : "";
        string toolName = (string)st.Attribute("toolname") ?? "";

        if (string.IsNullOrEmpty(routineTitle))
        {
            routineTitle = !string.IsNullOrEmpty(toolName) ? ("Routine " + toolName) : "Diagnostic Routine";
        }

        string clickUrl = string.Format("smarttool://{0}?title={1}", Uri.EscapeDataString(scriptId), Uri.EscapeDataString(routineTitle));

        sb.Append("<div class='vct-tool-container'>");
        sb.AppendFormat("<a href='{0}' class='vct-btn' title='Click to execute: {1}'>", clickUrl, System.Security.SecurityElement.Escape(routineTitle));
        
        if (!string.IsNullOrEmpty(iconSrc))
        {
            sb.AppendFormat("<img src='{0}' class='vct-icon' style='width:32px;height:32px;max-width:32px;max-height:32px;object-fit:contain;border:none;margin-right:8px;' alt='VCT2000' />", iconSrc);
        }
        else
        {
            sb.Append("<span style='font-size:18px;'>⚡</span>");
        }

        if (!string.IsNullOrEmpty(toolName))
        {
            sb.AppendFormat("<span class='vct-badge'>{0}</span>", System.Security.SecurityElement.Escape(toolName));
        }

        sb.AppendFormat("<span class='vct-title'>▶ Run Test: {0}</span>", System.Security.SecurityElement.Escape(routineTitle));
        sb.Append("</a></div>");
    }

    static string TryRenderDiagnosticScriptHtml(string scriptId, string highlight)
    {
        if (string.IsNullOrEmpty(scriptId)) return null;

        try
        {
            string cleanId = CleanDocId(scriptId);
            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))
            {
                conn.Open();

                string title = null;
                byte[] xmlBytes = null;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT TOP 1 DisplayText, XmlDataCompressed 
                                        FROM ScriptContent 
                                        WHERE fkScript = @id 
                                        ORDER BY CASE WHEN fkLanguage IN (15, 16) THEN 0 ELSE 1 END, fkLanguage";
                    cmd.Parameters.AddWithValue("@id", cleanId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            if (r[0] != DBNull.Value) title = r[0].ToString().Trim();
                            if (r[1] != DBNull.Value) xmlBytes = (byte[])r[1];
                        }
                    }
                }

                if (string.IsNullOrEmpty(title))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 DisplayText 
                                            FROM IETitle 
                                            WHERE fkIE = @id 
                                            ORDER BY CASE WHEN fkLanguage IN (15, 16) THEN 0 ELSE 1 END, fkLanguage";
                        cmd.Parameters.AddWithValue("@id", cleanId);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) title = obj.ToString().Trim();
                    }
                }

                // If not found in ScriptContent or IETitle, check Script table
                if (string.IsNullOrEmpty(title))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM Script WHERE id = @id";
                        cmd.Parameters.AddWithValue("@id", cleanId);
                        var cnt = cmd.ExecuteScalar();
                        if (cnt == null || cnt == DBNull.Value || Convert.ToInt32(cnt) == 0) return null;
                        title = "Diagnostic Routine " + cleanId;
                    }
                }

                // Parse XML phrases/notes if available
                var phrases = new List<string>();
                var actions = new List<string>();
                var notices = new List<string>();

                if (xmlBytes != null && xmlBytes.Length > 0)
                {
                    try
                    {
                        using (var ms = new System.IO.MemoryStream(xmlBytes))
                        using (var za = new System.IO.Compression.ZipArchive(ms))
                        {
                            if (za.Entries.Count > 0)
                            {
                                using (var entryStream = za.Entries[0].Open())
                                using (var sr = new System.IO.StreamReader(entryStream, System.Text.Encoding.UTF8))
                                {
                                    string xmlContent = sr.ReadToEnd();
                                    var matches = System.Text.RegularExpressions.Regex.Matches(xmlContent, @"<phrase(?:\s+category=""([^""]*)"")?[^>]*>([^<]+)</phrase>");
                                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                    foreach (System.Text.RegularExpressions.Match m in matches)
                                    {
                                        string cat = m.Groups[1].Success ? m.Groups[1].Value : "";
                                        string p = m.Groups[2].Value.Trim();

                                        if (string.IsNullOrEmpty(p) || seen.Contains(p) || p.Equals(title, StringComparison.OrdinalIgnoreCase))
                                            continue;

                                        seen.Add(p);

                                        // Categorize buttons/controls vs instructions/notices
                                        if (p.Length < 35 && (p.StartsWith("Activate", StringComparison.OrdinalIgnoreCase) ||
                                                               p.StartsWith("Windshield", StringComparison.OrdinalIgnoreCase) ||
                                                               p.StartsWith("Rear window", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Stop", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Reset", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Exit", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Continue", StringComparison.OrdinalIgnoreCase) ||
                                                               p.Equals("Hide", StringComparison.OrdinalIgnoreCase) ||
                                                               cat.Equals("Button", StringComparison.OrdinalIgnoreCase) ||
                                                               cat.Equals("Component And Functionality", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            actions.Add(p);
                                        }
                                        else if (p.Length > 30 || p.Contains("!") || p.Contains(".") || p.Contains("voltage") || p.Contains("battery") || p.Contains("control module") || p.Contains("Note"))
                                        {
                                            notices.Add(p);
                                        }
                                        else
                                        {
                                            phrases.Add(p);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {}
                }

                var sb = new System.Text.StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><meta http-equiv='X-UA-Compatible' content='IE=edge'>");
                sb.Append("<style>");
                sb.Append("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 24px 32px; padding-bottom: 80px; box-sizing: border-box; line-height: 1.6; color: #1e293b; background: #fff; }");
                sb.Append(".header { border-bottom: 2px solid #005087; padding-bottom: 12px; margin-bottom: 20px; }");
                sb.Append(".badge-pill { display: inline-block; background: #005087; color: white; padding: 4px 10px; border-radius: 4px; font-weight: 600; font-size: 12px; text-transform: uppercase; margin-bottom: 8px; }");
                sb.Append(".id-pill { display: inline-block; background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1; padding: 2px 8px; border-radius: 4px; font-size: 11px; margin-left: 8px; font-family: Consolas, monospace; }");
                sb.Append("h1 { color: #0f172a; font-size: 24px; margin: 6px 0; font-weight: 700; }");
                sb.Append(".info-card { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #0284c7; border-radius: 6px; padding: 16px 20px; margin: 18px 0; }");
                sb.Append(".exec-btn { display: inline-block; background-color: #005087; color: #ffffff !important; padding: 12px 24px; font-weight: 600; font-size: 14px; text-decoration: none; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.15); margin-top: 10px; margin-bottom: 10px; }");
                sb.Append(".exec-btn:hover { background-color: #00385e; color: #ffffff !important; }");
                sb.Append(".section-title { font-size: 15px; font-weight: 700; color: #005087; margin-top: 22px; margin-bottom: 8px; border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; }");
                sb.Append(".action-tag { display: inline-block; background: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd; padding: 4px 12px; border-radius: 16px; font-weight: 600; font-size: 13px; margin: 4px 6px 4px 0; }");
                sb.Append(".notice-box { background: #fffbeb; border: 1px solid #fef3c7; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 4px; margin: 10px 0; color: #92400e; font-size: 13px; line-height: 1.5; }");
                sb.Append(".bullet-list { margin: 8px 0; padding-left: 20px; color: #334155; }");
                sb.Append(".bullet-list li { margin-bottom: 6px; }");
                sb.Append("</style></head><body>");

                sb.Append("<div class='header'>");
                sb.Append("<div><span class='badge-pill'>Interactive Diagnostic Routine</span><span class='id-pill'>Script ID: ").Append(cleanId).Append("</span></div>");
                sb.Append("<h1>").Append(System.Security.SecurityElement.Escape(title)).Append("</h1>");
                sb.Append("</div>");

                sb.Append("<div class='info-card'>");
                sb.Append("<h3 style='margin-top:0; color:#0369a1;'>⚡ Real-time Vehicle Routine</h3>");
                sb.Append("<p style='margin: 4px 0;'>This function is an interactive diagnostic routine executed directly across the vehicle CAN/LIN bus network via the connected Electronic Control Module (ECU).</p>");
                sb.Append("<p style='margin: 4px 0; color: #64748b; font-size: 13px;'>Interactive activation routines do not have static workshop repair procedures because their controls and parameters operate dynamically in real time.</p>");
                sb.Append("<div style='margin-top: 14px;'>");
                sb.Append("<a href='smarttool:").Append(cleanId).Append("?title=").Append(Uri.EscapeDataString(title)).Append("' class='exec-btn'>▶ Execute Diagnostic Routine</a>");
                sb.Append("</div>");
                sb.Append("</div>");

                if (actions.Count > 0)
                {
                    sb.Append("<div class='section-title'>Available Activation Controls & Functions</div>");
                    sb.Append("<div style='margin: 10px 0;'>");
                    foreach (var act in actions)
                    {
                        sb.Append("<span class='action-tag'>⚙ ").Append(System.Security.SecurityElement.Escape(act)).Append("</span>");
                    }
                    sb.Append("</div>");
                }

                if (notices.Count > 0)
                {
                    sb.Append("<div class='section-title'>Operating Conditions & Instructions</div>");
                    foreach (var note in notices)
                    {
                        sb.Append("<div class='notice-box'>").Append(System.Security.SecurityElement.Escape(note)).Append("</div>");
                    }
                }

                if (phrases.Count > 0)
                {
                    sb.Append("<div class='section-title'>Additional Parameters & Descriptions</div>");
                    sb.Append("<ul class='bullet-list'>");
                    foreach (var ph in phrases)
                    {
                        sb.Append("<li>").Append(System.Security.SecurityElement.Escape(ph)).Append("</li>");
                    }
                    sb.Append("</ul>");
                }

                sb.Append("<div style='margin-top: 28px; padding-top: 12px; border-top: 1px solid #e2e8f0; color: #64748b; font-size: 12px;'>");
                sb.Append("To run this routine, click the <strong>▶ Execute Diagnostic Routine</strong> button above, or click the <strong>Execute</strong> button in the Advanced tab toolbar.");
                sb.Append("</div>");

                sb.Append("</body></html>");
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] TryRenderDiagnosticScriptHtml error: " + ex.Message);
        }

        return null;
    }
}
