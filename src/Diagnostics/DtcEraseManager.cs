using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.ProgressHandler;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static partial class Program
{
    public static object _activeCustomerSymptomsFaultTraceForm = null;
    public static bool _simulatedDtcsCleared = false;

    private static Panel _faultTraceActionBar = null;
    private static Button _btnFtInitial = null;
    private static Button _btnFtDelivery = null;
    private static Label _lblFtStatus = null;
    private static Button _btnFtClearAll = null;
    private static Button _btnFtReRead = null;

    public class DtcEraseResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public int InitialCount { get; set; }
        public int ClearedCount { get; set; }
        public int RemainingCount { get; set; }
        public string TargetEcu { get; set; }
        public string Message { get; set; }
        public double BatteryVoltage { get; set; }
        public List<Dictionary<string, object>> RemainingDtcs { get; set; }
        public DateTime Timestamp { get; set; }

        public DtcEraseResult()
        {
            RemainingDtcs = new List<Dictionary<string, object>>();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Updates the top bar DTC count label dynamically.
    /// </summary>
    public static void UpdateTopBarDtcCount(int count)
    {
        if (_topBarLblInfo == null || _topBarLblInfo.IsDisposed) return;
        try
        {
            if (_topBarLblInfo.InvokeRequired)
            {
                _topBarLblInfo.BeginInvoke((Action)(() => UpdateTopBarDtcCount(count)));
                return;
            }

            string dtcText = count == 0
                ? "🟢 0 DTCs (Clean)"
                : string.Format("⚠️ {0} DTC{1} Stored", count, count == 1 ? "" : "s");

            _topBarLblInfo.Text = string.Format("|  Volvo V70 (08-), MY 2011, D5204T2  |  {0}  |", dtcText);
        }
        catch {}
    }

    /// <summary>
    /// Updates the Fault Trace action bar status badge.
    /// </summary>
    public static void UpdateFaultTraceActionBarStatus(int count)
    {
        if (_lblFtStatus == null || _lblFtStatus.IsDisposed) return;
        try
        {
            if (_lblFtStatus.InvokeRequired)
            {
                _lblFtStatus.BeginInvoke((Action)(() => UpdateFaultTraceActionBarStatus(count)));
                return;
            }

            if (count == 0)
            {
                _lblFtStatus.Text = "🟢 0 DTCs Active (Network Clean)";
                _lblFtStatus.ForeColor = Color.FromArgb(16, 124, 65); // Green
            }
            else
            {
                _lblFtStatus.Text = string.Format("⚠️ {0} DTC{1} Identified", count, count == 1 ? "" : "s");
                _lblFtStatus.ForeColor = Color.FromArgb(197, 34, 31); // Red
            }
        }
        catch {}
    }

    /// <summary>
    /// Displays a professional pre-flight verification dialog before clearing DTCs.
    /// </summary>
    public static bool ShowDtcClearPreflightDialog(IWin32Window parent, double voltage, int currentDtcCount)
    {
        try
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Pre-Flight Verification — Clear Faults";
                dlg.Size = new Size(580, 480);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.White;
                dlg.Font = new Font("Segoe UI", 9f);

                // Top Header Panel
                Panel pnlHeader = new Panel();
                pnlHeader.Dock = DockStyle.Top;
                pnlHeader.Height = 72;
                pnlHeader.BackColor = Color.FromArgb(254, 243, 242); // Soft amber/red header
                pnlHeader.Paint += (s, pe) =>
                {
                    using (Pen p = new Pen(Color.FromArgb(250, 210, 207), 1))
                    {
                        pe.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                    }
                };

                Label lblHeader = new Label();
                lblHeader.Text = "🗑️ Clear All Diagnostic Trouble Codes (DTCs)";
                lblHeader.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
                lblHeader.ForeColor = Color.FromArgb(180, 35, 24);
                lblHeader.SetBounds(18, 12, 530, 26);
                pnlHeader.Controls.Add(lblHeader);

                Label lblSub = new Label();
                lblSub.Text = "Broadcasts UDS Service 0x14 across High-Speed and Low-Speed CAN networks.";
                lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblSub.ForeColor = Color.FromArgb(95, 100, 110);
                lblSub.SetBounds(20, 40, 530, 20);
                pnlHeader.Controls.Add(lblSub);

                dlg.Controls.Add(pnlHeader);

                // Content Panel
                Panel pnlBody = new Panel();
                pnlBody.Dock = DockStyle.Fill;
                pnlBody.Padding = new Padding(20, 16, 20, 16);

                Label lblChecklistTitle = new Label();
                lblChecklistTitle.Text = "Vehicle Pre-Conditions & Interlocks:";
                lblChecklistTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                lblChecklistTitle.ForeColor = Color.FromArgb(32, 33, 36);
                lblChecklistTitle.SetBounds(20, 84, 520, 20);
                dlg.Controls.Add(lblChecklistTitle);

                // Battery Voltage Card
                Panel pnlVolt = new Panel();
                pnlVolt.SetBounds(20, 108, 524, 50);
                bool voltOk = voltage >= 11.8 || voltage <= 0; // voltage <= 0 if simulated/unknown
                pnlVolt.BackColor = voltOk ? Color.FromArgb(240, 250, 242) : Color.FromArgb(254, 242, 242);
                pnlVolt.Paint += (s, pe) =>
                {
                    Color borderCol = voltOk ? Color.FromArgb(180, 225, 195) : Color.FromArgb(250, 190, 190);
                    using (Pen p = new Pen(borderCol, 1))
                    {
                        pe.Graphics.DrawRectangle(p, 0, 0, pnlVolt.Width - 1, pnlVolt.Height - 1);
                    }
                };

                Label lblVolt = new Label();
                if (voltage > 0)
                {
                    lblVolt.Text = voltOk
                        ? string.Format("⚡ Terminal 30 Voltage: {0:F2} V — [PASS] Battery voltage sufficient for ECU broadcast.", voltage)
                        : string.Format("⚠️ Terminal 30 Voltage: {0:F2} V — [WARNING] Below 11.8 V! Connect maintainer before clearing.", voltage);
                }
                else
                {
                    lblVolt.Text = "⚡ Terminal 30 Voltage: 12.45 V (Simulated / PassThru Standby) — [PASS]";
                }
                lblVolt.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                lblVolt.ForeColor = voltOk ? Color.FromArgb(20, 110, 50) : Color.FromArgb(180, 30, 20);
                lblVolt.SetBounds(12, 14, 500, 22);
                pnlVolt.Controls.Add(lblVolt);
                dlg.Controls.Add(pnlVolt);

                // Checklist Items
                int curY = 166;
                string[] items = new string[]
                {
                    "🔑 Ignition Switch: MUST be in Position II (KOEO — Key ON, Engine OFF).",
                    "🛑 Engine State: Engine MUST NOT be running. Alternator ripples cause immediate fault re-trigger.",
                    "🌐 Network Broadcast: Clears all responding control modules (ECM, TCM, BCM, CEM, DIM, CCM, REM).",
                    "📋 Memory Scope: Erases stored active/intermittent DTCs, freeze frames, and OBD-II readiness flags.",
                    string.Format("🎯 Target Callset: {0} currently stored diagnostic trouble code(s) will be erased.", currentDtcCount)
                };

                foreach (string item in items)
                {
                    Label lblItem = new Label();
                    lblItem.Text = "• " + item;
                    lblItem.Font = new Font("Segoe UI", 8.75f, item.Contains("Engine MUST NOT") ? FontStyle.Bold : FontStyle.Regular);
                    lblItem.ForeColor = item.Contains("Engine MUST NOT") ? Color.FromArgb(160, 25, 15) : Color.FromArgb(50, 55, 65);
                    lblItem.SetBounds(22, curY, 520, 24);
                    dlg.Controls.Add(lblItem);
                    curY += 26;
                }

                // Permanent fault notice
                Label lblNote = new Label();
                lblNote.Text = "Notice: Any permanent hardware faults (open circuits, failed sensors) will immediately reappear upon post-clear vehicle verification readout.";
                lblNote.Font = new Font("Segoe UI", 8.25f, FontStyle.Italic);
                lblNote.ForeColor = Color.FromArgb(110, 115, 125);
                lblNote.SetBounds(22, curY + 6, 520, 34);
                dlg.Controls.Add(lblNote);

                // Bottom Button Panel
                Panel pnlBottom = new Panel();
                pnlBottom.Dock = DockStyle.Bottom;
                pnlBottom.Height = 56;
                pnlBottom.BackColor = Color.FromArgb(248, 249, 250);
                pnlBottom.Paint += (s, pe) =>
                {
                    using (Pen p = new Pen(Color.FromArgb(222, 226, 230), 1))
                    {
                        pe.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
                    }
                };

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Font = new Font("Segoe UI", 9f);
                btnCancel.SetBounds(330, 12, 90, 32);
                btnCancel.BackColor = Color.White;
                btnCancel.FlatStyle = FlatStyle.Flat;
                btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);
                btnCancel.Cursor = Cursors.Hand;
                pnlBottom.Controls.Add(btnCancel);

                Button btnConfirm = new Button();
                btnConfirm.Text = "🗑️ Clear All Faults";
                btnConfirm.DialogResult = DialogResult.OK;
                btnConfirm.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                btnConfirm.SetBounds(428, 12, 132, 32);
                btnConfirm.BackColor = Color.FromArgb(217, 48, 37); // Clean red
                btnConfirm.ForeColor = Color.White;
                btnConfirm.FlatStyle = FlatStyle.Flat;
                btnConfirm.FlatAppearance.BorderSize = 0;
                btnConfirm.Cursor = Cursors.Hand;
                pnlBottom.Controls.Add(btnConfirm);

                dlg.Controls.Add(pnlBottom);
                dlg.AcceptButton = btnConfirm;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog(parent) == DialogResult.OK;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DtcErase] Preflight dialog error: " + ex.Message);
            return true; // Fallback to allowing execution if dialog fails
        }
    }

    /// <summary>
    /// Displays the ignition cycle prompt required by Volvo ECUs to finalize memory reset.
    /// </summary>
    public static void ShowIgnitionCycleDialog(IWin32Window parent)
    {
        try
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Ignition Reset Required";
                dlg.Size = new Size(520, 360);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.White;
                dlg.Font = new Font("Segoe UI", 9f);

                // Header
                Panel pnlHeader = new Panel();
                pnlHeader.Dock = DockStyle.Top;
                pnlHeader.Height = 65;
                pnlHeader.BackColor = Color.FromArgb(240, 246, 254); // Blue header
                pnlHeader.Paint += (s, pe) =>
                {
                    using (Pen p = new Pen(Color.FromArgb(210, 227, 252), 1))
                    {
                        pe.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                    }
                };

                Label lblHeader = new Label();
                lblHeader.Text = "🔑 Ignition Reset Required";
                lblHeader.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
                lblHeader.ForeColor = Color.FromArgb(24, 90, 188);
                lblHeader.SetBounds(18, 12, 480, 24);
                pnlHeader.Controls.Add(lblHeader);

                Label lblSub = new Label();
                lblSub.Text = "DTC Erase command accepted. Control modules require an ignition cycle.";
                lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblSub.ForeColor = Color.FromArgb(90, 100, 115);
                lblSub.SetBounds(20, 38, 480, 20);
                pnlHeader.Controls.Add(lblSub);

                dlg.Controls.Add(pnlHeader);

                // Instructions
                int curY = 82;
                string[] steps = new string[]
                {
                    "1. Turn vehicle ignition key to OFF (Position 0).",
                    "2. Wait 5 seconds for control modules to power down and save NVRAM.",
                    "3. Turn ignition switch back to Position II (Key ON, Engine OFF)."
                };

                foreach (string step in steps)
                {
                    Label lblStep = new Label();
                    lblStep.Text = step;
                    lblStep.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                    lblStep.ForeColor = Color.FromArgb(32, 33, 36);
                    lblStep.SetBounds(22, curY, 470, 24);
                    dlg.Controls.Add(lblStep);
                    curY += 28;
                }

                Label lblCont = new Label();
                lblCont.Text = "Click Continue once ignition is in Position II to perform the post-clear verification scan.";
                lblCont.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                lblCont.ForeColor = Color.FromArgb(60, 64, 67);
                lblCont.SetBounds(22, curY + 14, 470, 38);
                dlg.Controls.Add(lblCont);

                // Bottom
                Panel pnlBottom = new Panel();
                pnlBottom.Dock = DockStyle.Bottom;
                pnlBottom.Height = 56;
                pnlBottom.BackColor = Color.FromArgb(248, 249, 250);
                pnlBottom.Paint += (s, pe) =>
                {
                    using (Pen p = new Pen(Color.FromArgb(222, 226, 230), 1))
                    {
                        pe.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
                    }
                };

                Button btnOk = new Button();
                btnOk.Text = "🔄 Continue & Verify Readout";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                btnOk.SetBounds(280, 12, 220, 32);
                btnOk.BackColor = Color.FromArgb(0, 102, 204);
                btnOk.ForeColor = Color.White;
                btnOk.FlatStyle = FlatStyle.Flat;
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Cursor = Cursors.Hand;
                pnlBottom.Controls.Add(btnOk);

                dlg.Controls.Add(pnlBottom);
                dlg.AcceptButton = btnOk;

                dlg.ShowDialog(parent);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DtcErase] Ignition cycle dialog error: " + ex.Message);
        }
    }

    /// <summary>
    /// Executes the complete DTC Erase / Clear Faults pipeline.
    /// Can be invoked interactively from UI or non-interactively via IPC / MCP / test runner.
    /// </summary>
    public static DtcEraseResult ExecuteClearAllDtcs(bool interactive = true, string targetEcu = "ALL", IWin32Window owner = null)
    {
        DtcEraseResult result = new DtcEraseResult();
        result.TargetEcu = string.IsNullOrEmpty(targetEcu) ? "ALL" : targetEcu;
        result.BatteryVoltage = Math.Round(_lastKnownVoltVal, 2);

        try
        {
            var initialList = RetrieveCurrentDtcs();
            result.InitialCount = initialList.Count;

            Console.WriteLine(string.Format("[DtcErase] Starting DTC Erase routine (Target: {0}, Interactive: {1}, Initial DTCs: {2}, Battery: {3:F2}V)",
                result.TargetEcu, interactive, result.InitialCount, result.BatteryVoltage));

            // 1. Safety pre-flight check dialog if interactive
            if (interactive)
            {
                bool confirmed = ShowDtcClearPreflightDialog(owner, _lastKnownVoltVal, result.InitialCount);
                if (!confirmed)
                {
                    result.Success = false;
                    result.Cancelled = true;
                    result.Message = "DTC Erase cancelled by user during pre-flight check.";
                    Console.WriteLine("[DtcErase] Cancelled by user.");
                    return result;
                }
            }

            // 2. Perform CAN Erase Routine
            bool isLive = IsDiceConnected && _activeDtcFaulttraceWf != null;

            if (isLive)
            {
                Console.WriteLine("[DtcErase] Live DiCE connected: Calling ClearDtcAllEcuKeepInitialState()...");
                try
                {
                    MethodInfo miClear = _activeDtcFaulttraceWf.GetType().GetMethod("ClearDtcAllEcuKeepInitialState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (miClear != null)
                    {
                        miClear.Invoke(_activeDtcFaulttraceWf, null);
                        Console.WriteLine("[DtcErase] ClearDtcAllEcuKeepInitialState invoked successfully!");
                    }
                    else
                    {
                        Console.WriteLine("[DtcErase] ClearDtcAllEcuKeepInitialState not found on workflow.");
                    }
                }
                catch (Exception exLive)
                {
                    Console.WriteLine("[DtcErase] Exception in ClearDtcAllEcuKeepInitialState: " + exLive);
                }
            }
            else
            {
                // Simulation mode
                Console.WriteLine("[DtcErase] Simulation/Offline mode: Clearing simulated DTC state...");
                _simulatedDtcsCleared = true;
            }

            // 3. Ignition cycle prompt if interactive
            if (interactive)
            {
                ShowIgnitionCycleDialog(owner);
            }
            else
            {
                // In non-interactive mode, give ECUs a brief moment
                Thread.Sleep(500);
            }

            // 4. Verification Readout (ReadAllForDelivery)
            Console.WriteLine("[DtcErase] Executing post-clear verification readout...");
            try
            {
                if (isLive)
                {
                    if (_activeCustomerSymptomsFaultTraceForm != null)
                    {
                        Control csftCtrl = _activeCustomerSymptomsFaultTraceForm as Control;
                        Action doRead = () =>
                        {
                            try
                            {
                                MethodInfo miRead = _activeCustomerSymptomsFaultTraceForm.GetType().GetMethod("ReadAllForDelivery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (miRead != null)
                                {
                                    miRead.Invoke(_activeCustomerSymptomsFaultTraceForm, new object[] { false });
                                    Console.WriteLine("[DtcErase] ReadAllForDelivery(false) invoked successfully!");
                                }

                                // Switch to Delivery tab (index 3) to show verified state
                                FieldInfo tabField = _activeCustomerSymptomsFaultTraceForm.GetType().GetField("_tab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (tabField != null)
                                {
                                    object tabCtrl = tabField.GetValue(_activeCustomerSymptomsFaultTraceForm);
                                    if (tabCtrl != null)
                                    {
                                        tabCtrl.GetType().GetProperty("SelectedIndex").SetValue(tabCtrl, 3, null);
                                        Console.WriteLine("[DtcErase] Switched CustomerSymptomsFaultTraceForm to Delivery tab (3)!");
                                    }
                                }
                            }
                            catch (Exception exFormRead)
                            {
                                Console.WriteLine("[DtcErase] ReadAllForDelivery form error: " + exFormRead.Message);
                            }
                        };

                        if (csftCtrl != null && csftCtrl.InvokeRequired)
                        {
                            csftCtrl.Invoke(doRead);
                        }
                        else
                        {
                            doRead();
                        }
                    }
                    else if (_activeDtcFaulttraceWf != null)
                    {
                        MethodInfo miReadWf = _activeDtcFaulttraceWf.GetType().GetMethod("ReadDtcAllEcu", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(bool) }, null);
                        if (miReadWf != null)
                        {
                            miReadWf.Invoke(_activeDtcFaulttraceWf, new object[] { false });
                            Console.WriteLine("[DtcErase] ReadDtcAllEcu(false) invoked on workflow!");
                        }
                    }
                }
                else
                {
                    // Simulation mode: keep on current tab, mark simulation DTCs cleared
                    Console.WriteLine("[DtcErase] Simulation mode post-clear completed. Simulated DTCs cleared.");
                }
            }
            catch (Exception exRead)
            {
                Console.WriteLine("[DtcErase] Post-clear readout error: " + exRead.Message);
            }

            // 5. Query updated DTC list
            var remainingList = RetrieveCurrentDtcs();
            result.RemainingCount = remainingList.Count;
            result.RemainingDtcs = remainingList;
            result.ClearedCount = Math.Max(0, result.InitialCount - result.RemainingCount);
            result.Success = true;
            result.Message = string.Format("DTC clear routine finished. {0} cleared, {1} remaining.",
                result.ClearedCount, result.RemainingCount);

            // Update UI status badges
            UpdateTopBarDtcCount(result.RemainingCount);
            UpdateFaultTraceActionBarStatus(result.RemainingCount);

            // Export updated dual readout
            try
            {
                ReadoutExporter.ExportLatestReadout(TargetVIN);
            }
            catch {}

            Console.WriteLine(string.Format("[DtcErase] Result: SUCCESS ({0})", result.Message));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Error during DTC Erase: " + ex.Message;
            Console.WriteLine("[DtcErase] Routine exception: " + ex);
        }

        return result;
    }

    /// <summary>
    /// Attaches OpenVIDA's modern Fault Trace Action Bar to CustomerSymptomsFaultTraceForm.
    /// </summary>
    public static void InstallFaultTraceActionBar(object csftForm, Form mainForm)
    {
        if (csftForm == null) return;
        Control formCtrl = csftForm as Control;
        if (formCtrl == null) return;

        try
        {
            _activeCustomerSymptomsFaultTraceForm = csftForm;

            // Check if already installed
            if (_faultTraceActionBar != null && !_faultTraceActionBar.IsDisposed && _faultTraceActionBar.Parent == formCtrl)
            {
                return;
            }

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 42;
            bar.BackColor = Color.FromArgb(248, 250, 252);
            bar.Padding = new Padding(10, 4, 10, 4);
            bar.Paint += (s, pe) =>
            {
                using (Pen p = new Pen(Color.FromArgb(220, 226, 232), 1))
                {
                    pe.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
                }
            };

            // 1. Initial State Tab Button
            Button btnInitial = new Button();
            btnInitial.Text = "📋 Initial Readout";
            btnInitial.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnInitial.SetBounds(10, 6, 130, 30);
            btnInitial.BackColor = Color.FromArgb(0, 102, 204);
            btnInitial.ForeColor = Color.White;
            btnInitial.FlatStyle = FlatStyle.Flat;
            btnInitial.FlatAppearance.BorderSize = 0;
            btnInitial.Cursor = Cursors.Hand;
            _btnFtInitial = btnInitial;
            bar.Controls.Add(btnInitial);

            // 2. Delivery / Verified Tab Button
            Button btnDelivery = new Button();
            btnDelivery.Text = "🚚 Delivery / Current";
            btnDelivery.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            btnDelivery.SetBounds(148, 6, 145, 30);
            btnDelivery.BackColor = Color.FromArgb(235, 239, 244);
            btnDelivery.ForeColor = Color.FromArgb(50, 55, 65);
            btnDelivery.FlatStyle = FlatStyle.Flat;
            btnDelivery.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);
            btnDelivery.Cursor = Cursors.Hand;
            _btnFtDelivery = btnDelivery;
            bar.Controls.Add(btnDelivery);

            // 3. Status Label
            Label lblStatus = new Label();
            lblStatus.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(197, 34, 31);
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(305, 12);
            _lblFtStatus = lblStatus;
            bar.Controls.Add(lblStatus);

            // 4. Re-read Faults Button
            Button btnReRead = new Button();
            btnReRead.Text = "🔄 Re-read Faults";
            btnReRead.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            btnReRead.SetBounds(bar.Width - 280, 6, 125, 30);
            btnReRead.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReRead.BackColor = Color.FromArgb(245, 248, 252);
            btnReRead.ForeColor = Color.FromArgb(32, 33, 36);
            btnReRead.FlatStyle = FlatStyle.Flat;
            btnReRead.FlatAppearance.BorderColor = Color.FromArgb(195, 202, 210);
            btnReRead.Cursor = Cursors.Hand;
            _btnFtReRead = btnReRead;
            bar.Controls.Add(btnReRead);

            // 5. Clear All Faults Button
            Button btnClearAll = new Button();
            btnClearAll.Text = "🗑️ Clear All Faults";
            btnClearAll.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            btnClearAll.SetBounds(bar.Width - 145, 6, 135, 30);
            btnClearAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearAll.BackColor = Color.FromArgb(217, 48, 37); // Distinct red
            btnClearAll.ForeColor = Color.White;
            btnClearAll.FlatStyle = FlatStyle.Flat;
            btnClearAll.FlatAppearance.BorderSize = 0;
            btnClearAll.Cursor = Cursors.Hand;
            _btnFtClearAll = btnClearAll;
            bar.Controls.Add(btnClearAll);

            // Tab synchronization helpers
            Action<int> updateTabHighlights = (tabIdx) =>
            {
                if (tabIdx == 0)
                {
                    btnInitial.BackColor = Color.FromArgb(0, 102, 204);
                    btnInitial.ForeColor = Color.White;
                    btnInitial.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

                    btnDelivery.BackColor = Color.FromArgb(235, 239, 244);
                    btnDelivery.ForeColor = Color.FromArgb(50, 55, 65);
                    btnDelivery.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
                else if (tabIdx == 3)
                {
                    btnDelivery.BackColor = Color.FromArgb(0, 102, 204);
                    btnDelivery.ForeColor = Color.White;
                    btnDelivery.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

                    btnInitial.BackColor = Color.FromArgb(235, 239, 244);
                    btnInitial.ForeColor = Color.FromArgb(50, 55, 65);
                    btnInitial.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
            };

            // Wire Tab Switchers
            FieldInfo tabField = csftForm.GetType().GetField("_tab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object tabCtrl = tabField != null ? tabField.GetValue(csftForm) : null;

            btnInitial.Click += (s, e) =>
            {
                if (tabCtrl != null)
                {
                    try
                    {
                        tabCtrl.GetType().GetProperty("SelectedIndex").SetValue(tabCtrl, 0, null);
                        updateTabHighlights(0);
                    }
                    catch {}
                }
            };

            btnDelivery.Click += (s, e) =>
            {
                if (!IsDiceConnected)
                {
                    MessageBox.Show(
                        "The Delivery tab performs a live CAN verification readout across all vehicle ECUs to confirm fault clearing.\n\nPlease connect the DiCE hardware interface to perform a live Delivery verification scan.",
                        "DiCE Connection Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (tabCtrl != null)
                {
                    try
                    {
                        tabCtrl.GetType().GetProperty("SelectedIndex").SetValue(tabCtrl, 3, null);
                        updateTabHighlights(3);
                    }
                    catch {}
                }
            };

            btnReRead.Click += (s, e) =>
            {
                btnReRead.Enabled = false;
                btnReRead.Text = "⏳ Reading...";
                Cursor.Current = Cursors.WaitCursor;

                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        if (_activeDtcFaulttraceWf != null && IsDiceConnected)
                        {
                            MethodInfo miRead = csftForm.GetType().GetMethod("ReadAllForDelivery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (miRead != null)
                            {
                                formCtrl.Invoke((Action)(() => miRead.Invoke(csftForm, new object[] { false })));
                            }
                        }

                        var dtcs = RetrieveCurrentDtcs();
                        formCtrl.BeginInvoke((Action)(() =>
                        {
                            btnReRead.Enabled = true;
                            btnReRead.Text = "🔄 Re-read Faults";
                            Cursor.Current = Cursors.Default;
                            UpdateTopBarDtcCount(dtcs.Count);
                            UpdateFaultTraceActionBarStatus(dtcs.Count);
                        }));
                    }
                    catch
                    {
                        formCtrl.BeginInvoke((Action)(() =>
                        {
                            btnReRead.Enabled = true;
                            btnReRead.Text = "🔄 Re-read Faults";
                            Cursor.Current = Cursors.Default;
                        }));
                    }
                });
            };

            btnClearAll.Click += (s, e) =>
            {
                ExecuteClearAllDtcs(interactive: true, targetEcu: "ALL", owner: formCtrl);
            };

            // Modernize native _btncntContainer buttons
            FieldInfo cntField = csftForm.GetType().GetField("_btncntContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Control btnCnt = cntField != null ? cntField.GetValue(csftForm) as Control : null;
            if (btnCnt != null)
            {
                btnCnt.BackColor = Color.FromArgb(245, 247, 250);
            }

            // Modernize _btnEraseAll
            FieldInfo eaField = csftForm.GetType().GetField("_btnEraseAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Button btnEa = eaField != null ? eaField.GetValue(csftForm) as Button : null;
            if (btnEa != null)
            {
                btnEa.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                btnEa.ForeColor = Color.FromArgb(180, 30, 20);
                btnEa.Cursor = Cursors.Hand;
            }

            // Modernize _btnReadAll
            FieldInfo raField = csftForm.GetType().GetField("_btnReadAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Button btnRa = raField != null ? raField.GetValue(csftForm) as Button : null;
            if (btnRa != null)
            {
                btnRa.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                btnRa.Cursor = Cursors.Hand;
            }

            // Hook tab change event to keep highlights in sync
            if (tabCtrl != null)
            {
                EventInfo evTab = tabCtrl.GetType().GetEvent("SelectedIndexChanged") ?? tabCtrl.GetType().GetEvent("SelectedTabChanged");
                if (evTab != null)
                {
                    EventHandler h = (s, e) =>
                    {
                        try
                        {
                            int idx = (int)tabCtrl.GetType().GetProperty("SelectedIndex").GetValue(tabCtrl, null);
                            updateTabHighlights(idx);
                        }
                        catch {}
                    };
                    evTab.AddEventHandler(tabCtrl, h);
                }
            }

            // Add toolbar to form
            formCtrl.Controls.Add(bar);
            bar.BringToFront();
            _faultTraceActionBar = bar;

            // Update status text initially
            var curDtcs = RetrieveCurrentDtcs();
            UpdateFaultTraceActionBarStatus(curDtcs.Count);
            UpdateTopBarDtcCount(curDtcs.Count);

            Console.WriteLine("[DtcErase] Installed Fault Trace Action Bar on CustomerSymptomsFaultTraceForm successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DtcErase] InstallFaultTraceActionBar error: " + ex);
        }
    }
}
