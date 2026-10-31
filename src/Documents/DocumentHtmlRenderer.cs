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

            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");

            sb.Append("<style>");

            sb.Append("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 24px 32px; line-height: 1.6; color: #222; background: #fff; }");

            sb.Append(".header-bar { display: flex; justify-content: space-between; align-items: baseline; border-bottom: 2px solid #003057; padding-bottom: 10px; margin-bottom: 20px; }");

            sb.Append("h1 { color: #003057; font-size: 20px; margin: 0; font-weight: 600; }");

            sb.Append("h2 { color: #005087; margin-top: 24px; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; font-weight: 600; }");

            sb.Append("h3 { color: #1e293b; margin-top: 18px; font-size: 14.5px; font-weight: 600; }");

            sb.Append(".section-box { margin: 16px 0 24px 0; padding: 18px 22px; border-left: 4px solid #005087; background: #f8fafc; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }");

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

            if (foundTarget)

            {

                sb.Append("<script>window.onload = function() { var el = document.getElementById('target'); if(el) el.scrollIntoView({behavior: 'smooth', block: 'center'}); };</script>");

            }

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

            sb.AppendFormat("<div class='question-box'><strong>Action / Question:</strong> {0}</div>", el.Value.Trim());

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
        if (smartTools.Count > 0)
        {
            foreach (var st in smartTools)
            {
                RenderSmarttool(st, sb, iconSrc);
            }
        }
        else if (!string.IsNullOrEmpty(iconSrc))
        {
            sb.AppendFormat("<div style='margin:12px 0;'><img src='{0}' alt='Diagnostic Tool' /></div>", iconSrc);
        }
    }

    static void RenderSmarttool(System.Xml.Linq.XElement st, System.Text.StringBuilder sb, string iconSrc)
    {
        var href = st.Element("href");
        string scriptId = href != null ? href.Value.Trim() : st.Value.Trim();
        string routineTitle = href != null ? (string)href.Attribute("title") ?? "" : "";
        if (string.IsNullOrEmpty(routineTitle)) routineTitle = "Diagnostic Routine";

        string iconHtml = "";
        if (!string.IsNullOrEmpty(iconSrc))
        {
            iconHtml = string.Format("<img src='{0}' style='width:22px;height:22px;vertical-align:middle;margin-right:8px;border:none;' alt='VCT2000' />", iconSrc);
        }

        string clickUrl = string.Format("smarttool://{0}?title={1}", Uri.EscapeDataString(scriptId), Uri.EscapeDataString(routineTitle));

        sb.Append("<div class='smarttool-box'>");
        sb.Append("<div class='smarttool-header'>");
        sb.Append("<span class='smarttool-badge'>⚡ VEHICLE ROUTINE</span>");
        sb.AppendFormat("<span class='smarttool-title'>{0}</span>", System.Security.SecurityElement.Escape(routineTitle));
        sb.Append("</div>");
        sb.Append("<div class='smarttool-action'>");
        sb.AppendFormat("<a href='{0}' class='btn-smarttool'>{1}Run Adaptation on ECU</a>", clickUrl, iconHtml);
        sb.AppendFormat("<span class='smarttool-id'>Script: <code>{0}</code></span>", System.Security.SecurityElement.Escape(scriptId));
        sb.Append("</div>");
        sb.Append("</div>");
    }
}
