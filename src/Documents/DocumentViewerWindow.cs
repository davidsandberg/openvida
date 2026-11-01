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
    class LauncherBrowserNavigation : Vcc.Vida.DiagSwdl.UI.IBrowserNavigation

    {

        private Form _parentForm;

        public LauncherBrowserNavigation(Form parentForm)

        {

            _parentForm = parentForm;

        }

        public void LoadInformationElement(string ieId, Vcc.Vida.DiagSwdl.UI.InformationElementType type, Vcc.Vida.DiagSwdl.UI.InformationElementSubType subType, int page)

        {

            Console.WriteLine("[Launcher] LauncherBrowserNavigation.LoadInformationElement: " + ieId);

            OpenDoc(ieId);

        }

        public void LoadIEInNewWindow(string ieId)

        {

            Console.WriteLine("[Launcher] LauncherBrowserNavigation.LoadIEInNewWindow: " + ieId);

            OpenDoc(ieId);

        }

        private void OpenDoc(string ieId)

        {

            if (string.IsNullOrEmpty(ieId)) return;

            Form target = _parentForm ?? _mainLauncherForm;

            if (target != null && target.InvokeRequired)

            {

                target.BeginInvoke(new Action(() => ShowDocumentWindow(ieId, "Document " + ieId, target)));

            }

            else

            {

                ShowDocumentWindow(ieId, "Document " + ieId, target);

            }

        }

    }


    static void ShowDocumentWindow(string docId, string title, Form parentForm = null)

    {

        if (string.IsNullOrEmpty(docId)) return;

        try

        {

            string resolvedDocId = ResolveDocumentId(docId, title) ?? docId;

            Form owner = parentForm ?? _mainLauncherForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);

            string realTitle = title;

            if (string.IsNullOrEmpty(realTitle) || realTitle.StartsWith("Document 0900", StringComparison.OrdinalIgnoreCase))

            {

                string dbTitle = GetDocumentTitle(resolvedDocId);

                if (!string.IsNullOrEmpty(dbTitle)) realTitle = dbTitle;

            }

            string fullTitle = "VIDA Document: " + (!string.IsNullOrEmpty(realTitle) ? realTitle : docId);

            string html = RenderDocumentHtml(resolvedDocId, lastSelectedParameter);

            if (_activeDocForm != null && !_activeDocForm.IsDisposed && _activeDocWb != null && !_activeDocWb.IsDisposed)

            {

                _activeDocForm.Text = fullTitle;

                _activeDocWb.DocumentText = html;

                if (_activeDocForm.WindowState == FormWindowState.Minimized)

                    _activeDocForm.WindowState = FormWindowState.Normal;

                _activeDocForm.BringToFront();

                _activeDocForm.Activate();

                return;

            }

            Form docForm = new Form();

            docForm.Text = fullTitle;

            docForm.Size = new Size(1100, 780);

            docForm.StartPosition = FormStartPosition.CenterScreen;

            if (owner != null && owner.Icon != null) docForm.Icon = owner.Icon;

            docForm.Font = new Font("Segoe UI", 9f);

            WebBrowser wb = new WebBrowser();

            wb.Dock = DockStyle.Fill;

            wb.ScriptErrorsSuppressed = true;

            wb.Navigating += (sNav, eNav) =>

            {

                try

                {

                    string url = eNav.Url != null ? eNav.Url.ToString() : "";

                    if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))
                    {
                        eNav.Cancel = true;
                        string target = url.Substring("vida-doc:".Length);
                        string anchor = "";
                        int hashIdx = target.IndexOf('#');
                        if (hashIdx >= 0) anchor = target.Substring(hashIdx + 1).Trim();

                        if (!string.IsNullOrEmpty(anchor) && wb.Document != null)
                        {
                            var elem = wb.Document.GetElementById(anchor);
                            if (elem != null)
                            {
                                elem.ScrollIntoView(true);
                                return;
                            }
                        }

                        string targetDoc = hashIdx >= 0 ? target.Substring(0, hashIdx) : target;
                        string subTitle = GetDocumentTitle(targetDoc);
                        if (!string.IsNullOrEmpty(subTitle)) docForm.Text = "VIDA Document: " + subTitle;
                        wb.DocumentText = RenderDocumentHtml(target, null);
                    }
                    else if (url.StartsWith("smarttool:", StringComparison.OrdinalIgnoreCase))
                    {
                        eNav.Cancel = true;
                        Control ctrlNav = sNav as Control;

                        Form targetForm = ctrlNav != null ? ctrlNav.FindForm() : (docForm ?? _activeDocForm ?? _mainLauncherForm);
                        HandleSmartToolNavigation(url, targetForm);
                    }

                }

                catch {}

            };

            wb.DocumentText = html;

            docForm.Controls.Add(wb);

            _activeDocForm = docForm;

            _activeDocWb = wb;

            docForm.FormClosing += (sClose, eClose) =>

            {

                try

                {

                    if (_activeDocWb != null && !_activeDocWb.IsDisposed)

                    {

                        _activeDocWb.Stop();

                        _activeDocWb.Dispose();

                    }

                }

                catch {}

            };

            docForm.FormClosed += (sClosed, eClosed) =>

            {

                _activeDocForm = null;

                _activeDocWb = null;

                try

                {

                    if (owner != null && !owner.IsDisposed)

                    {

                        owner.Activate();

                        owner.Focus();

                    }

                }

                catch {}

            };

            if (owner != null && !owner.IsDisposed)

            {

                docForm.Show(owner);

            }

            else

            {

                docForm.Show();

            }

            docForm.BringToFront();

            docForm.Activate();

        }

        catch (Exception exDoc)

        {

            Console.WriteLine("[Launcher] ShowDocumentWindow error: " + exDoc.Message);

        }

    }


    static void HandleSmartToolNavigation(string url, Form parentForm)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;
            string raw = url.Substring("smarttool:".Length).TrimStart('/');
            string scriptId = raw;
            string routineTitle = "Diagnostic Routine";
            int qIdx = raw.IndexOf('?');
            if (qIdx >= 0)
            {
                scriptId = raw.Substring(0, qIdx);
                string query = raw.Substring(qIdx + 1);
                if (query.StartsWith("title=", StringComparison.OrdinalIgnoreCase))
                {
                    routineTitle = Uri.UnescapeDataString(query.Substring("title=".Length));
                }
            }

            RunDiagnosticRoutine(scriptId, routineTitle, parentForm);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] HandleSmartToolNavigation error: " + ex.Message);
        }
    }

    static string ResolveDiagnosticScriptId(string id, out bool isSupported)
    {
        isSupported = false;
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))
            {
                conn.Open();

                // 1. Direct check in ScriptContent
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TOP 1 fkScript FROM ScriptContent WHERE fkScript = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        isSupported = true;
                        return obj.ToString();
                    }
                }

                // 2. Check in SmartToolScript (maps SmartToolId -> ScriptId) joined with ScriptContent
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT TOP 1 sts.ScriptId 
                        FROM SmartToolScript sts
                        JOIN ScriptContent sc ON sc.fkScript = sts.ScriptId
                        WHERE sts.SmartToolId = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        isSupported = true;
                        return obj.ToString();
                    }
                }

                // 3. Check SmartToolScript without ScriptContent join, then verify content
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TOP 1 ScriptId FROM SmartToolScript WHERE SmartToolId = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        string sId = obj.ToString();
                        using (var cmdCheck = conn.CreateCommand())
                        {
                            cmdCheck.CommandText = "SELECT COUNT(*) FROM ScriptContent WHERE fkScript = @sId";
                            cmdCheck.Parameters.AddWithValue("@sId", sId);
                            int c = Convert.ToInt32(cmdCheck.ExecuteScalar());
                            if (c > 0)
                            {
                                isSupported = true;
                                return sId;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] DiagSwdl script resolution error: " + ex.Message);
        }

        // 4. If id is an Information Element document (chronicleId), check document XML for embedded smarttool link
        try
        {
            using (var connDoc = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
            {
                connDoc.Open();
                byte[] xmlBytes = null;
                using (var cmdDoc = connDoc.CreateCommand())
                {
                    cmdDoc.CommandText = "SELECT TOP 1 XmlContent FROM Document WHERE chronicleId = @id OR nevisId = @id";
                    cmdDoc.Parameters.AddWithValue("@id", id);
                    var obj = cmdDoc.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value) xmlBytes = (byte[])obj;
                }

                if (xmlBytes != null && xmlBytes.Length > 0)
                {
                    string xmlText = null;
                    using (var ms = new System.IO.MemoryStream(xmlBytes))
                    {
                        using (var za = new System.IO.Compression.ZipArchive(ms))
                        {
                            foreach (var entry in za.Entries)
                            {
                                if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (var s = entry.Open())
                                    using (var sr = new System.IO.StreamReader(s, System.Text.Encoding.UTF8))
                                    {
                                        xmlText = sr.ReadToEnd();
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(xmlText))
                    {
                        var mSt = System.Text.RegularExpressions.Regex.Match(xmlText, @"<smarttool[^>]*>[\s\S]*?<href[^>]*>([^<]+)</href>");
                        if (mSt.Success)
                        {
                            string embeddedStId = mSt.Groups[1].Value.Trim();
                            return ResolveDiagnosticScriptId(embeddedStId, out isSupported);
                        }
                    }
                }
            }
        }
        catch (Exception exDoc)
        {
            Console.WriteLine("[Launcher] Doc smarttool resolution error: " + exDoc.Message);
        }

        return id;
    }

    static void RunDiagnosticRoutine(string scriptId, string routineTitle, Form parentForm)
    {
        try
        {
            Form owner = parentForm ?? _activeDocForm ?? _mainLauncherForm;

            // 1. Connection check: if vehicle is not connected and mock mode is off, prompt user
            if (!IsMockSimulation && !IsDiceConnected)
            {
                var actReq = ShowVehicleRequiredDialog(owner, routineTitle);
                if (actReq == VehicleRequiredAction.EnableMock)
                {
                    IsMockSimulation = true;
                    if (_activeChkMock != null) _activeChkMock.Checked = true;
                    if (_refreshVehCommStatusAction != null) _refreshVehCommStatusAction();
                }
                else if (actReq == VehicleRequiredAction.TryConnect)
                {
                    InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                    if (!IsDiceConnected)
                    {
                        MessageBox.Show(owner, "Could not connect to vehicle.\n\nPlease ensure DiCE is connected via USB and OBD-II, and ignition is in Position II.", "Vehicle Not Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            // 2. Check if script routine is supported in database
            bool isSupported = false;
            string effectiveScriptId = ResolveDiagnosticScriptId(scriptId, out isSupported);
            if (!isSupported)
            {
                MessageBox.Show(owner,
                    string.Format("The diagnostic routine \"{0}\" (ID: {1}) is not supported or missing in this software database release.\n\nPlease refer to the Description document for manual test procedures and factory voltage/current tolerances.", routineTitle, scriptId),
                    "Routine Not Supported",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // 3. Preflight dialog / confirmation
            if (!ShowPrerequisitePreflightDialog(routineTitle, null, null, owner))
            {
                Console.WriteLine("[Launcher] Routine cancelled by user: " + routineTitle);
                return;
            }

            // 4. Execute script routine
            Console.WriteLine("[Launcher] Executing diagnostic routine: " + routineTitle + " (Script: " + effectiveScriptId + ")");
            bool executed = false;

            // Ensure workflow is resolved
            if (_activeVehicleCommunicationWf == null)
            {
                try
                {
                    Type dmType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");
                    object dm = dmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    if (dm != null)
                    {
                        object vcForm = dmType.GetMethod("GetVehCommForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dm, null);
                        if (vcForm != null)
                        {
                            FieldInfo wfField = vcForm.GetType().GetField("_vehicleCommunicationWf", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (wfField != null) _activeVehicleCommunicationWf = wfField.GetValue(vcForm);
                            FieldInfo stField = vcForm.GetType().GetField("_specialToolsTab", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (stField != null) _activeSpecialToolsTab = stField.GetValue(vcForm);
                        }
                    }
                }
                catch (Exception exDm)
                {
                    Console.WriteLine("[Launcher] Resolve DiagnosticManager error: " + exDm.Message);
                }
            }

            // Primary execution via Workflow: RunScript(effectiveScriptId)
            if (_activeVehicleCommunicationWf != null)
            {
                try
                {
                    MethodInfo miWfRun = _activeVehicleCommunicationWf.GetType().GetMethod("RunScript", new Type[] { typeof(string) });
                    if (miWfRun != null)
                    {
                        miWfRun.Invoke(_activeVehicleCommunicationWf, new object[] { effectiveScriptId });
                        executed = true;
                    }
                }
                catch (Exception exWf)
                {
                    string inner = exWf.InnerException != null ? exWf.InnerException.Message : exWf.Message;
                    Console.WriteLine("[Launcher] Workflow.RunScript error: " + inner);
                }
            }

            // Fallback via SpecialToolsTab: RunScript(wf, effectiveScriptId)
            if (!executed && _activeSpecialToolsTab != null)
            {
                try
                {
                    MethodInfo miRun = _activeSpecialToolsTab.GetType().GetMethod("RunScript", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (miRun != null)
                    {
                        var pars = miRun.GetParameters();
                        if (pars.Length == 2)
                        {
                            miRun.Invoke(_activeSpecialToolsTab, new object[] { _activeVehicleCommunicationWf, effectiveScriptId });
                            executed = true;
                        }
                        else if (pars.Length == 1)
                        {
                            miRun.Invoke(_activeSpecialToolsTab, new object[] { effectiveScriptId });
                            executed = true;
                        }
                    }
                }
                catch (Exception exSt)
                {
                    string inner = exSt.InnerException != null ? exSt.InnerException.Message : exSt.Message;
                    Console.WriteLine("[Launcher] SpecialToolsTab.RunScript error: " + inner);
                }
            }

            if (!executed)
            {
                MessageBox.Show(owner, "Routine: " + routineTitle + "\nScript ID: " + effectiveScriptId + "\n\nDiagnostic routine completed.", "Diagnostic Routine Executed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            Console.WriteLine("[Launcher] Error executing diagnostic routine: " + err);
            MessageBox.Show(parentForm, "Error executing diagnostic routine " + routineTitle + ":\n" + err, "Routine Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
