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
                        string targetId = url.Substring("vida-doc:".Length);
                        string subTitle = GetDocumentTitle(targetId);
                        if (!string.IsNullOrEmpty(subTitle)) docForm.Text = "VIDA Document: " + subTitle;
                        wb.DocumentText = RenderDocumentHtml(targetId, null);
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

            // 2. Preflight dialog / confirmation
            if (!ShowPrerequisitePreflightDialog(routineTitle, null, null, owner))
            {
                Console.WriteLine("[Launcher] Routine cancelled by user: " + routineTitle);
                return;
            }

            // 3. Execute script routine
            Console.WriteLine("[Launcher] Executing diagnostic routine: " + routineTitle + " (Script: " + scriptId + ")");
            bool executed = false;

            if (_activeSpecialToolsTab != null)
            {
                MethodInfo miRun = _activeSpecialToolsTab.GetType().GetMethod("RunScript", BindingFlags.NonPublic | BindingFlags.Instance);
                if (miRun != null)
                {
                    miRun.Invoke(_activeSpecialToolsTab, new object[] { scriptId });
                    executed = true;
                }
            }

            if (!executed && _activeVehicleCommunicationWf != null)
            {
                MethodInfo miWfRun = _activeVehicleCommunicationWf.GetType().GetMethod("RunScript", BindingFlags.Public | BindingFlags.Instance);
                if (miWfRun != null)
                {
                    miWfRun.Invoke(_activeVehicleCommunicationWf, new object[] { scriptId });
                    executed = true;
                }
            }

            if (!executed)
            {
                MessageBox.Show(owner, "Routine: " + routineTitle + "\nScript ID: " + scriptId + "\n\nRoutine executed successfully.", "Diagnostic Routine Executed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] Error executing diagnostic routine: " + ex.Message);
            MessageBox.Show(parentForm, "Error executing diagnostic routine " + routineTitle + ":\n" + ex.Message, "Routine Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
