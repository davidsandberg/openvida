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
    static void ConfigureVehicleCommTab()
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

                        MethodInfo onNavClick = vcForm.GetType().GetMethod("OnNavImageClick", BindingFlags.NonPublic | BindingFlags.Instance);

                        Control vcCtrl = vcForm as Control;

                        Action clearAxHostActive = () => {

                            try

                            {

                                FieldInfo niF = vcForm.GetType().GetField("_navImage", BindingFlags.NonPublic | BindingFlags.Instance);

                                if (niF != null)

                                {

                                    object ni = niF.GetValue(vcForm);

                                    if (ni != null)

                                    {

                                        FieldInfo axF = ni.GetType().GetField("_axIsoView", BindingFlags.NonPublic | BindingFlags.Instance);

                                        if (axF != null)

                                        {

                                            Control ax = axF.GetValue(ni) as Control;

                                            if (ax != null)

                                            {

                                                FieldInfo f = typeof(AxHost).GetField("iOleInPlaceActiveObjectExternal", BindingFlags.NonPublic | BindingFlags.Instance);

                                                if (f != null) f.SetValue(ax, null);

                                                FieldInfo cF = typeof(AxHost).GetField("container", BindingFlags.NonPublic | BindingFlags.Instance);

                                                if (cF != null)

                                                {

                                                    object container = cF.GetValue(ax);

                                                    if (container != null)

                                                    {

                                                        FieldInfo sF = container.GetType().GetField("siteUIActive", BindingFlags.NonPublic | BindingFlags.Instance);

                                                        if (sF != null) sF.SetValue(container, null);

                                                    }

                                                }

                                            }

                                        }

                                    }

                                }

                            }

                            catch {}

                        };

                        clearAxHostActive();

                        // Ensure current comm tool is set on workflow so script engine doesn't throw NullReferenceException

                        FieldInfo wfInitField = vcForm.GetType().GetField("_vehicleCommunicationWf", BindingFlags.NonPublic | BindingFlags.Instance);

                        object wfInit = wfInitField != null ? wfInitField.GetValue(vcForm) : null;

                        _activeVehicleCommunicationWf = wfInit;

                        if (wfInit != null)

                        {

                            try

                            {

                                FieldInfo ctField = wfInit.GetType().GetField("_currentCommTool", BindingFlags.NonPublic | BindingFlags.Instance);

                                if (ctField != null && ctField.GetValue(wfInit) == null)

                                {

                                    Type ctType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.BizServices.VehicleCommProvider.CurrentCommTool")

                                                  ?? Assembly.LoadFrom(@"C:\Users\david\git\vida\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\BizServices.VehicleCommProvider.DLL")

                                                     .GetType("Vcc.Vida.DiagSwdl.BizServices.VehicleCommProvider.CurrentCommTool");

                                    if (ctType != null)

                                    {

                                        object ct = Activator.CreateInstance(ctType);

                                        PropertyInfo ctn = ctType.GetProperty("CommToolName");

                                        if (ctn != null) ctn.SetValue(ct, "DiCE", null);

                                        ctField.SetValue(wfInit, ct);

                                    }

                                }

                            }

                            catch {}
                            RegisterScriptingContext(_cachedVehicleProfile, dm, wfInit);

                        }

                        // Select default ECM once on initial entrance

                        if (onNavClick != null && !vehCommInitialized)

                        {

                            vehCommInitialized = true;

                            onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs("284101", 1) });

                        }

                        // Add ECU Module Selector Bar if not already added

                        if (vcCtrl != null && vcCtrl.Controls.Find("ecuSelectBar", true).Length == 0)

                        {

                            Panel ecuBar = new Panel();

                            ecuBar.Name = "ecuSelectBar";

                            ecuBar.SetBounds(380, 56, 920, 58);

                            ecuBar.BackColor = Color.FromArgb(245, 247, 250);

                            Label lblSel = new Label();

                            lblSel.Text = "ECU Module:";

                            lblSel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

                            lblSel.ForeColor = Color.FromArgb(50, 60, 70);

                            lblSel.SetBounds(2, 4, 85, 22);

                            ecuBar.Controls.Add(lblSel);

                            ComboBox cbEcu = new ComboBox();

                            cbEcu.Name = "cbEcu";

                            cbEcu.DropDownStyle = ComboBoxStyle.DropDownList;

                            cbEcu.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

                            cbEcu.SetBounds(90, 2, 380, 24);

                            var ecuList = new Tuple<string, string>[] {

                                Tuple.Create("284101", "ECM - Engine Control Module"),

                                Tuple.Create("437601", "TCM - Transmission Control Module"),

                                Tuple.Create("372302", "CEM - Central Electronic Module"),

                                Tuple.Create("593102", "BCM - Brake Control Module"),

                                Tuple.Create("381101", "DIM - Driver Information Module"),

                                Tuple.Create("884701", "SRS - Supplemental Restraint System"),

                                Tuple.Create("871101", "CCM - Climate Control Module"),

                                Tuple.Create("465901", "DEM - Differential Electronic Module"),

                                Tuple.Create("645902", "SAS - Steering Angle Sensor"),

                                Tuple.Create("875401", "CPM - Combustion Pre-heater Module"),

                                Tuple.Create("551401", "PBM - Parking Brake Module"),

                                Tuple.Create("831701", "DDM - Driver Door Module"),

                                Tuple.Create("831703", "PDM - Passenger Door Module"),

                                Tuple.Create("852601", "PSM - Power Seat Module"),

                                Tuple.Create("393901", "ICM - Infotainment Control Module"),

                                Tuple.Create("393204", "IAM - Integrated Audio Module"),

                                Tuple.Create("393202", "AUD - Audio Module"),

                                Tuple.Create("366901", "KVM - Keyless Vehicle Module"),

                                Tuple.Create("366902", "PAM - Parking Assistance Module"),

                                Tuple.Create("892901", "TRM - Trailer Module"),

                                Tuple.Create("832901", "POT - Power Operated Tailgate"),

                                Tuple.Create("352902", "HCM - Headlamp Control Module"),

                                Tuple.Create("366905", "FSM - Forward Sensing Module"),

                                Tuple.Create("645903", "PSCM - Power Steering Control Module")

                            };

                            foreach (var item in ecuList)

                            {

                                cbEcu.Items.Add(item.Item2);

                            }

                            cbEcu.SelectedIndex = 0;

                            bool updatingFromEvent = false;

                            Action<string> selectEcuById = (targetId) => {

                                for (int k = 0; k < ecuList.Length; k++)

                                {

                                    if (ecuList[k].Item1 == targetId)

                                    {

                                        updatingFromEvent = true;

                                        cbEcu.SelectedIndex = k;

                                        updatingFromEvent = false;

                                        break;

                                    }

                                }

                            };

                                                        _vehCommCtrl = vcForm as Control;

                            _onVehicleCommHotspotClicked = (clickedId) => {

                                Console.WriteLine("[Launcher] Vehicle Comm Click received for ECU: " + clickedId);

                                try {

                                    if (onNavClick != null)

                                        onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(clickedId, 1) });

                                    selectEcuById(clickedId);

                                } catch (Exception exClick) {

                                    Console.WriteLine("[Launcher] Vehicle Comm click dispatch error: " + exClick.Message);

                                }

                            };

                            RegisterDummyIsoViewHotspot();

                            cbEcu.Enter += (sEnter, eEnter) => { clearAxHostActive(); };

                            cbEcu.MouseDown += (sMd, eMd) => { clearAxHostActive(); };

                            cbEcu.SelectedIndexChanged += (sEcu, eEcu) => {

                                if (updatingFromEvent) return;

                                if (cbEcu.SelectedIndex >= 0 && cbEcu.SelectedIndex < ecuList.Length)

                                {

                                    string selId = ecuList[cbEcu.SelectedIndex].Item1;

                                    Console.WriteLine("[Launcher] Module selected: " + ecuList[cbEcu.SelectedIndex].Item2 + " (" + selId + ")");

                                    try

                                    {

                                        if (onNavClick != null)

                                        {

                                            onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(selId, 1) });

                                        }

                                    }

                                    catch (Exception ex)

                                    {

                                        Console.WriteLine("[Launcher] Error selecting ECU " + selId + ": " + ex.Message);

                                    }

                                }

                            };

                            ecuBar.Controls.Add(cbEcu);
                            CheckBox chkMock = new CheckBox();
                            chkMock.Name = "chkMock";
                            chkMock.Text = "Mock Data";
                            chkMock.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                            chkMock.SetBounds(475, 31, 95, 22);
                            chkMock.Checked = IsMockSimulation;

                            _activeChkMock = chkMock;
                            Label lblVehCommStatus = new Label();
                            lblVehCommStatus.Name = "lblVehCommStatus";
                            lblVehCommStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                            lblVehCommStatus.SetBounds(230, 4, 340, 20);
                            lblVehCommStatus.AutoEllipsis = true;



                            Action updateVehCommStatusText = () =>
                            {
                                try
                                {
                                    if (lblVehCommStatus.IsDisposed) return;
                                    if (IsMockSimulation)
                                    {
                                        lblVehCommStatus.Text = "🧪 Mock Simulation Mode — Simulating live vehicle telemetry for desk testing.";
                                        lblVehCommStatus.ForeColor = Color.FromArgb(2, 132, 199);
                                    }
                                    else if (IsDiceConnected)
                                    {
                                        lblVehCommStatus.Text = string.Format("🟢 Vehicle Connected — DiCE J2534 ({0:F1}V, Key Pos {1}). Ready for live polling & actuation.", _lastKnownVoltVal, _lastKnownKeyPos);
                                        lblVehCommStatus.ForeColor = Color.FromArgb(22, 163, 74);
                                    }
                                    else
                                    {
                                        lblVehCommStatus.Text = "⚪ Vehicle Disconnected — Hardware offline. Browse parameters freely, or enable [☑️ Mock Data].";
                                        lblVehCommStatus.ForeColor = Color.FromArgb(100, 116, 139);
                                    }
                                }
                                catch {}
                            };

                            _refreshVehCommStatusAction = updateVehCommStatusText;
                            updateVehCommStatusText();
                            ecuBar.Controls.Add(lblVehCommStatus);

                            OnCarStatusChanged += () => {
                                try {
                                    if (lblVehCommStatus.InvokeRequired) lblVehCommStatus.BeginInvoke(updateVehCommStatusText);
                                    else updateVehCommStatusText();
                                } catch {}
                            };

                            chkMock.CheckedChanged += (sChk, eChk) => {
                                IsMockSimulation = chkMock.Checked;
                                Console.WriteLine("[Launcher] Mock Simulation toggled: " + IsMockSimulation);
                                updateVehCommStatusText();
                                if (!IsMockSimulation && wfInit != null && IsDiceConnected)
                                {
                                    MethodInfo startM = wfInit.GetType().GetMethod("StartReading");
                                    if (startM != null) startM.Invoke(wfInit, null);
                                }
                            };
                            ecuBar.Controls.Add(chkMock);
                            // Sub-tab switcher inside Vehicle Communication: Parameters vs. Activations vs. Special Tools
                            Button btnSubParam = new Button();
                            Button btnSubAct = new Button();
                            Button btnSubProg = new Button();
                            Button btnSubSpec = new Button();

                            Action<Button> setSubTabStyle = (activeBtn) =>
                            {
                                Button[] sBtns = new Button[] { btnSubParam, btnSubAct, btnSubProg, btnSubSpec };
                                foreach (var b in sBtns)
                                {
                                    if (b == activeBtn)
                                    {
                                        b.BackColor = Color.FromArgb(0, 102, 204);
                                        b.ForeColor = Color.White;
                                        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                                    }
                                    else
                                    {
                                        b.BackColor = Color.FromArgb(235, 240, 245);
                                        b.ForeColor = Color.FromArgb(50, 60, 70);
                                        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                                    }
                                }
                            };

                            Action<string> switchVehCommSubTab = (tabName) =>
                            {
                                try
                                {
                                    FieldInfo tabField = vcForm.GetType().GetField("_tab", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object tabCtrl = tabField != null ? tabField.GetValue(vcForm) : null;
                                    if (tabCtrl != null)
                                    {
                                        FieldInfo targetPageField = vcForm.GetType().GetField(tabName, BindingFlags.NonPublic | BindingFlags.Instance);
                                        object targetPage = targetPageField != null ? targetPageField.GetValue(vcForm) : null;
                                        if (targetPage != null)
                                        {
                                            MethodInfo miSelChanged = vcForm.GetType().GetMethod("TabSelectedTabChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                                            PropertyInfo pCurTab = tabCtrl.GetType().GetProperty("SelectedTab");
                                            object curPage = pCurTab != null ? pCurTab.GetValue(tabCtrl, null) : null;
                                            if (pCurTab != null) pCurTab.SetValue(tabCtrl, targetPage, null);
                                            if (miSelChanged != null) miSelChanged.Invoke(vcForm, new object[] { targetPage, curPage });
                                            Console.WriteLine("[Launcher] Switched VehComm sub-tab to: " + tabName);
                                        }
                                    }
                                }
                                catch (Exception exTab)
                                {
                                    Console.WriteLine("[Launcher] Error switching VehComm sub-tab: " + exTab.Message);
                                }
                            };

                            btnSubParam.Text = "📊 Parameters";
                            btnSubParam.SetBounds(580, 2, 95, 24);
                            btnSubParam.FlatStyle = FlatStyle.Flat;
                            btnSubParam.FlatAppearance.BorderSize = 1;
                            btnSubParam.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubParam.Cursor = Cursors.Hand;
                            btnSubParam.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubParam);
                                switchVehCommSubTab("_readingParamTabPage");
                            };
                            ecuBar.Controls.Add(btnSubParam);

                            btnSubAct.Text = "⚡ Activations";
                            btnSubAct.SetBounds(680, 2, 95, 24);
                            btnSubAct.FlatStyle = FlatStyle.Flat;
                            btnSubAct.FlatAppearance.BorderSize = 1;
                            btnSubAct.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubAct.Cursor = Cursors.Hand;
                            btnSubAct.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubAct);
                                switchVehCommSubTab("_activationsTabPage");
                            };
                            ecuBar.Controls.Add(btnSubAct);

                            btnSubProg.Text = "📝 Programmed Values";
                            btnSubProg.SetBounds(780, 2, 135, 24);
                            btnSubProg.FlatStyle = FlatStyle.Flat;
                            btnSubProg.FlatAppearance.BorderSize = 1;
                            btnSubProg.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubProg.Cursor = Cursors.Hand;
                            btnSubProg.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubProg);
                                switchVehCommSubTab("_programmedValuesTabPage");
                            };
                            ecuBar.Controls.Add(btnSubProg);

                            btnSubSpec.Text = "🔧 Advanced";
                            btnSubSpec.SetBounds(920, 2, 90, 24);
                            btnSubSpec.FlatStyle = FlatStyle.Flat;
                            btnSubSpec.FlatAppearance.BorderSize = 1;
                            btnSubSpec.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubSpec.Cursor = Cursors.Hand;
                            btnSubSpec.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubSpec);
                                switchVehCommSubTab("_specialToolsTabPage");
                            };
                            ecuBar.Controls.Add(btnSubSpec);
                            setSubTabStyle(btnSubParam);

                            // Parameters Watchlist Management Buttons
                            Button btnRemoveParam = new Button();
                            btnRemoveParam.Text = "❌ Remove Item";
                            btnRemoveParam.SetBounds(580, 30, 95, 24);
                            btnRemoveParam.FlatStyle = FlatStyle.Flat;
                            btnRemoveParam.FlatAppearance.BorderSize = 1;
                            btnRemoveParam.FlatAppearance.BorderColor = Color.FromArgb(220, 180, 180);
                            btnRemoveParam.BackColor = Color.FromArgb(253, 242, 242);
                            btnRemoveParam.ForeColor = Color.FromArgb(160, 40, 40);
                            btnRemoveParam.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                            btnRemoveParam.Cursor = Cursors.Hand;
                            btnRemoveParam.Click += (sR, eR) => {
                                try {
                                    FieldInfo rpf = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object rp = rpf != null ? rpf.GetValue(vcForm) : null;
                                    if (rp != null) {
                                        FieldInfo lvf = rp.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                        object lvObj = lvf != null ? lvf.GetValue(rp) : null;
                                        if (lvObj != null) {
                                            PropertyInfo piSel = lvObj.GetType().GetProperty("SelectedItems");
                                            IList selList = piSel != null ? piSel.GetValue(lvObj, null) as IList : null;
                                            if (selList == null || selList.Count == 0) {
                                                PropertyInfo piItems = lvObj.GetType().GetProperty("Items");
                                                IEnumerable items = piItems != null ? piItems.GetValue(lvObj, null) as IEnumerable : null;
                                                object lastIt = null;
                                                if (items != null) {
                                                    foreach (object it in items) lastIt = it;
                                                }
                                                if (lastIt != null) {
                                                    PropertyInfo piItSel = lastIt.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(lastIt, true, null);
                                                }
                                            }
                                        }
                                        MethodInfo miRem = rp.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (miRem != null) {
                                            miRem.Invoke(rp, null);
                                            Console.WriteLine("[Launcher] Parameter removed from watchlist");
                                        }
                                    }
                                } catch (Exception exRem) {
                                    Console.WriteLine("[Launcher] Error removing parameter: " + exRem.Message);
                                }
                            };
                            ecuBar.Controls.Add(btnRemoveParam);

                            Button btnClearParams = new Button();
                            btnClearParams.Text = "🗑️ Clear All";
                            btnClearParams.SetBounds(680, 30, 95, 24);
                            btnClearParams.FlatStyle = FlatStyle.Flat;
                            btnClearParams.FlatAppearance.BorderSize = 1;
                            btnClearParams.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                            btnClearParams.BackColor = Color.FromArgb(245, 245, 247);
                            btnClearParams.ForeColor = Color.FromArgb(70, 70, 80);
                            btnClearParams.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                            btnClearParams.Cursor = Cursors.Hand;
                            btnClearParams.Click += (sC, eC) => {
                                try {
                                    FieldInfo rpf = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object rp = rpf != null ? rpf.GetValue(vcForm) : null;
                                    if (rp != null) {
                                        FieldInfo lvf = rp.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                        object lvObj = lvf != null ? lvf.GetValue(rp) : null;
                                        if (lvObj != null) {
                                            PropertyInfo piItems = lvObj.GetType().GetProperty("Items");
                                            IEnumerable items = piItems != null ? piItems.GetValue(lvObj, null) as IEnumerable : null;
                                            if (items != null) {
                                                foreach (object it in items) {
                                                    PropertyInfo piItSel = it.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(it, true, null);
                                                }
                                            }
                                        }
                                        MethodInfo miRem = rp.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (miRem != null) {
                                            miRem.Invoke(rp, null);
                                            Console.WriteLine("[Launcher] All parameters cleared from watchlist");
                                        }
                                    }
                                } catch (Exception exClr) {
                                    Console.WriteLine("[Launcher] Error clearing parameters: " + exClr.Message);
                                }
                            };
                            ecuBar.Controls.Add(btnClearParams);

                            // Setup safety logging and DiCE worker binding on ActivationControl
                            try
                            {
                                FieldInfo actTabField = vcForm.GetType().GetField("_activationsTab", BindingFlags.NonPublic | BindingFlags.Instance);
                                object actTab = actTabField != null ? actTabField.GetValue(vcForm) : null;
                                if (actTab != null)
                                {
                                    FieldInfo acField = actTab.GetType().GetField("_activationControl", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object ac = acField != null ? acField.GetValue(actTab) : null;
                                    if (ac != null)
                                    {
                                        EventInfo evStart = ac.GetType().GetEvent("ActivationStarted");
                                        if (evStart != null)
                                        {
                                            EventHandler onActStart = (sAct, eAct) => {
                                                Console.WriteLine("[Launcher] >>> ACTUATION TRIGGERED: Component test started on ECU via DiCE! <<<");
                                            };
                                            evStart.AddEventHandler(ac, onActStart);
                                        }
                                        EventInfo evStop = ac.GetType().GetEvent("ActivationStopped");
                                        if (evStop != null)
                                        {
                                            EventHandler onActStop = (sAct, eAct) => {
                                                Console.WriteLine("[Launcher] <<< ACTUATION STOPPED: Component test finished on ECU via DiCE! >>>");
                                            };
                                            evStop.AddEventHandler(ac, onActStop);
                                        }

                                        // Attach safety interception and prerequisite verification to _startButton
                                        FieldInfo sbField = ac.GetType().GetField("_startButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                        Control sb = sbField != null ? sbField.GetValue(ac) as Control : null;
                                        MethodInfo mStartOrig = ac.GetType().GetMethod("_startButton_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (sb != null && mStartOrig != null && sb.Tag == null)
                                        {
                                            sb.Tag = "wired_preflight";
                                            Delegate delStart = Delegate.CreateDelegate(typeof(EventHandler), ac, mStartOrig);
                                            sb.Click -= (EventHandler)delStart;
                                            sb.Click += (sBtn, eBtn) => {
                                                if (IsMockSimulation)
                                                {
                                                    Console.WriteLine("[Launcher] Warning: Mock Simulation is active!");
                                                }
                                                FieldInfo fCur = ac.GetType().GetField("_currentActivation", BindingFlags.NonPublic | BindingFlags.Instance);
                                                object curAct = fCur != null ? fCur.GetValue(ac) : null;
                                                string actName = "Component Activation";
                                                string actWarn = null;
                                                IList preConds = null;
                                                if (curAct != null)
                                                {
                                                    PropertyInfo piName = curAct.GetType().GetProperty("Name");
                                                    if (piName != null) actName = piName.GetValue(curAct, null) as string ?? actName;
                                                    PropertyInfo piWarn = curAct.GetType().GetProperty("Warning");
                                                    if (piWarn != null) actWarn = piWarn.GetValue(curAct, null) as string;
                                                    PropertyInfo piPre = curAct.GetType().GetProperty("Preconditions");
                                                    if (piPre != null) preConds = piPre.GetValue(curAct, null) as IList;
                                                }
                                                if (!IsMockSimulation && !IsDiceConnected)
                                                {
                                                    var actReq = ShowVehicleRequiredDialog(sb.FindForm(), actName);
                                                    if (actReq == VehicleRequiredAction.EnableMock)
                                                    {
                                                        chkMock.Checked = true;
                                                        IsMockSimulation = true;
                                                        updateVehCommStatusText();
                                                    }
                                                    else if (actReq == VehicleRequiredAction.TryConnect)
                                                    {
                                                        InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                                                        if (!IsDiceConnected)
                                                        {
                                                            MessageBox.Show("Could not connect to vehicle.\n\nPlease ensure DiCE is connected via USB and OBD-II, and ignition is in Position II.", "Vehicle Not Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                                            return;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        return;
                                                    }
                                                }

                                                if (!ShowPrerequisitePreflightDialog(actName, actWarn, preConds, sb.FindForm()))
                                                {
                                                    Console.WriteLine("[Launcher] Activation cancelled by user: " + actName);
                                                    return;
                                                }
                                                try
                                                {
                                                    mStartOrig.Invoke(ac, new object[] { sBtn, eBtn });
                                                }
                                                catch (Exception exStart)
                                                {
                                                    string msg = exStart.InnerException != null ? exStart.InnerException.Message : exStart.Message;
                                                    Console.WriteLine("[Launcher] Activation execution error: " + msg);
                                                    MessageBox.Show(sb.FindForm(), "Failed to execute activation '" + actName + "':\n\n" + msg, "Activation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                                }
                                            };
                                        }
                                    }
                                }
                            }
                            catch (Exception exAct)
                            {
                                Console.WriteLine("[Launcher] Error hooking ActivationControl: " + exAct.Message);
                            }
                            // Setup prerequisite pre-flight verification on SpecialToolsTab (Advanced)
                            try
                            {
                                FieldInfo stTabField = vcForm.GetType().GetField("_specialToolsTab", BindingFlags.NonPublic | BindingFlags.Instance);
                                object stTab = stTabField != null ? stTabField.GetValue(vcForm) : null;

                                _activeSpecialToolsTab = stTab;
                                if (stTab != null)
                                {
                                    FieldInfo tvField = stTab.GetType().GetField("_treeView", BindingFlags.NonPublic | BindingFlags.Instance);
                                    TreeView stTv = tvField != null ? tvField.GetValue(stTab) as TreeView : null;
                                    MethodInfo mDbClickOrig = stTab.GetType().GetMethod("_treeView_DoubleClick", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (stTv != null && mDbClickOrig != null && stTv.Tag == null)
                                    {
                                        stTv.Tag = "wired_preflight";
                                        Delegate delTv = Delegate.CreateDelegate(typeof(EventHandler), stTab, mDbClickOrig);
                                        stTv.DoubleClick -= (EventHandler)delTv;
                                        stTv.DoubleClick += (sTv, eTv) => {
                                            Point pt = stTv.PointToClient(Cursor.Position);
                                            TreeNode selNode = stTv.GetNodeAt(pt) ?? stTv.SelectedNode;
                                            if (selNode != null && selNode.Nodes.Count == 0)
                                            {
                                                PropertyInfo piId = selNode.Tag != null ? selNode.Tag.GetType().GetProperty("Id") : null;
                                                string ieId = piId != null ? piId.GetValue(selNode.Tag, null) as string : null;
                                                if (!string.IsNullOrEmpty(ieId))
                                                {
                                                    // Double-clicking always opens the documentation
                                                    ShowDocumentWindow(ieId, selNode.Text, stTv.FindForm());
                                                    return;
                                                }
                                            }
                                            mDbClickOrig.Invoke(stTab, new object[] { sTv, eTv });
                                        };
                                    }

                                    // Hook Description button to always show documentation
                                    FieldInfo btnDescField = stTab.GetType().GetField("_viewDescriptionButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                    Control btnDesc = btnDescField != null ? btnDescField.GetValue(stTab) as Control : null;
                                    MethodInfo mDescOrig = stTab.GetType().GetMethod("_viewDescriptionButton_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (btnDesc != null && mDescOrig != null && btnDesc.Tag == null)
                                    {
                                        btnDesc.Tag = "wired_desc_only";
                                        Delegate delDesc = Delegate.CreateDelegate(typeof(EventHandler), stTab, mDescOrig);
                                        btnDesc.Click -= (EventHandler)delDesc;
                                        btnDesc.Click += (sD, eD) => {
                                            TreeNode selNode = stTv != null ? stTv.SelectedNode : null;
                                            if (selNode != null && selNode.Nodes.Count == 0)
                                            {
                                                PropertyInfo piId = selNode.Tag != null ? selNode.Tag.GetType().GetProperty("Id") : null;
                                                string ieId = piId != null ? piId.GetValue(selNode.Tag, null) as string : null;
                                                if (!string.IsNullOrEmpty(ieId))
                                                {
                                                    ShowDocumentWindow(ieId, selNode.Text, btnDesc.FindForm());
                                                    return;
                                                }
                                            }
                                            mDescOrig.Invoke(stTab, new object[] { sD, eD });
                                        };
                                    }

                                    // Add Execute button next to Description in _buttonContainer
                                    FieldInfo btnContainerField = stTab.GetType().GetField("_buttonContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                                    Control btnContainer = btnContainerField != null ? btnContainerField.GetValue(stTab) as Control : null;

                                    if (btnContainer != null && btnDesc != null && btnContainer.Controls["_executeButton"] == null)
                                    {
                                        var btnExec = new Vcc.Vida.DiagSwdl.GuiComponents.FlatButton();
                                        btnExec.Name = "_executeButton";
                                        btnExec.Text = "Execute";
                                        btnExec.Size = btnDesc.Size;
                                        btnExec.Cursor = Cursors.Hand;
                                        if (btnDesc is Vcc.Vida.DiagSwdl.GuiComponents.FlatButton)
                                        {
                                            var fbDesc = (Vcc.Vida.DiagSwdl.GuiComponents.FlatButton)btnDesc;
                                            btnExec.HighlightColor = fbDesc.HighlightColor;
                                            btnExec.HighlightTextColor = fbDesc.HighlightTextColor;
                                        }
                                        btnExec.Enabled = (stTv != null && stTv.SelectedNode != null && stTv.SelectedNode.Nodes.Count == 0);

                                        btnExec.Click += (sEx, eEx) => {
                                            TreeNode selNode = stTv != null ? stTv.SelectedNode : null;
                                            if (selNode == null || selNode.Nodes.Count > 0)
                                            {
                                                MessageBox.Show(btnExec.FindForm(), "Please select an activation or diagnostic routine to execute.", "No Routine Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                                return;
                                            }

                                            string routineTitle = selNode.Text;
                                            PropertyInfo piId = selNode.Tag != null ? selNode.Tag.GetType().GetProperty("Id") : null;
                                            string ieId = piId != null ? piId.GetValue(selNode.Tag, null) as string : null;

                                            RunDiagnosticRoutine(ieId, routineTitle, btnExec.FindForm());
                                        };

                                        if (stTv != null)
                                        {
                                            stTv.AfterSelect += (sAs, eAs) => {
                                                btnExec.Enabled = (eAs.Node != null && eAs.Node.Nodes.Count == 0);
                                            };
                                        }

                                        btnContainer.Controls.Add(btnExec);
                                    }
                                }
                            }
                            catch (Exception exSt)
                            {
                                Console.WriteLine("[Launcher] Error hooking SpecialToolsTab: " + exSt.Message);
                            }

                            // Wire ProgrammedValuesTab preflight safety guard
                            try
                            {
                                FieldInfo pvTabField = vcForm.GetType().GetField("_programmedValuesTab", BindingFlags.NonPublic | BindingFlags.Instance);
                                object pvTab = pvTabField != null ? pvTabField.GetValue(vcForm) : null;
                                if (pvTab != null)
                                {
                                    FieldInfo pbField = pvTab.GetType().GetField("_programButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                    Control pb = pbField != null ? pbField.GetValue(pvTab) as Control : null;
                                    MethodInfo mProgOrig = pvTab.GetType().GetMethod("_programButton_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (pb != null && mProgOrig != null && pb.Tag == null)
                                    {
                                        pb.Tag = "wired_preflight";
                                        Delegate delProg = Delegate.CreateDelegate(typeof(EventHandler), pvTab, mProgOrig);
                                        pb.Click -= (EventHandler)delProg;
                                        pb.Click += (sPb, ePb) => {
                                            if (!IsMockSimulation && !IsDiceConnected)
                                            {
                                                var actReq = ShowVehicleRequiredDialog(pb.FindForm(), "Programmed Values / ECU Parameter Programming");
                                                if (actReq == VehicleRequiredAction.EnableMock)
                                                {
                                                    chkMock.Checked = true;
                                                    IsMockSimulation = true;
                                                    updateVehCommStatusText();
                                                }
                                                else if (actReq == VehicleRequiredAction.TryConnect)
                                                {
                                                    InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                                                    if (!IsDiceConnected)
                                                    {
                                                        MessageBox.Show("DiCE hardware interface is still not detected or car ignition is OFF.\n\nPlease connect DiCE USB cable to vehicle OBD-II port and retry.", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                                        return;
                                                    }
                                                }
                                                else
                                                {
                                                    return;
                                                }
                                            }

                                            // Confirm programming action with user
                                            DialogResult progDr = MessageBox.Show(
                                                "Are you sure you want to write and program the new configuration value(s) into the ECU non-volatile memory?\n\nEnsure vehicle ignition is ON (Position II) and battery voltage is stable before proceeding.",
                                                "Confirm ECU Programming",
                                                MessageBoxButtons.YesNo,
                                                MessageBoxIcon.Question);

                                            if (progDr != DialogResult.Yes)
                                            {
                                                Console.WriteLine("[Launcher] Programming cancelled by user.");
                                                return;
                                            }

                                            mProgOrig.Invoke(pvTab, new object[] { sPb, ePb });
                                        };
                                    }
                                }
                            }
                            catch (Exception exPv)
                            {
                                Console.WriteLine("[Launcher] Error hooking ProgrammedValuesTab: " + exPv.Message);
                            }

                            // Shortcut buttons row

                            string[] quickEcus = new string[] { "ECM", "TCM", "CEM", "BCM", "DIM", "SRS", "CCM", "DEM" };

                            int btnX = 2;

                            foreach (var qName in quickEcus)

                            {

                                Button btn = new Button();

                                btn.Text = qName;

                                btn.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);

                                btn.SetBounds(btnX, 30, 48, 22);

                                btn.FlatStyle = FlatStyle.Flat;

                                btn.FlatAppearance.BorderSize = 1;

                                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);

                                btn.BackColor = Color.FromArgb(240, 244, 248);

                                btn.Cursor = Cursors.Hand;

                                string matchName = qName;

                                btn.Click += (sBtn, eBtn) => {

                                    for (int k = 0; k < ecuList.Length; k++)

                                    {

                                        if (ecuList[k].Item2.StartsWith(matchName + " ", StringComparison.OrdinalIgnoreCase))

                                        {

                                            cbEcu.SelectedIndex = k;

                                            break;

                                        }

                                    }

                                };

                                ecuBar.Controls.Add(btn);

                                btnX += 52;

                            }

                            vcCtrl.Controls.Add(ecuBar);

                            ecuBar.BringToFront();

                            // Synchronize dropdown when an ECU is clicked directly on the map

                            FieldInfo ecuLblField = vcForm.GetType().GetField("_ecuNameLabel", BindingFlags.NonPublic | BindingFlags.Instance);

                            if (ecuLblField != null)

                            {

                                object ecuLbl = ecuLblField.GetValue(vcForm);

                                if (ecuLbl != null)

                                {

                                    FieldInfo innerLblField = ecuLbl.GetType().GetField("_label", BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (innerLblField != null)

                                    {

                                        Label innerLbl = innerLblField.GetValue(ecuLbl) as Label;

                                        if (innerLbl != null)

                                        {

                                            innerLbl.TextChanged += (sLbl, eLbl) => {

                                                string curTxt = innerLbl.Text;

                                                for (int k = 0; k < ecuList.Length; k++)

                                                {

                                                    string prefix = ecuList[k].Item2.Split(' ')[0];

                                                    if (curTxt.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)

                                                    {

                                                        updatingFromEvent = true;

                                                        cbEcu.SelectedIndex = k;

                                                        updatingFromEvent = false;

                                                        break;

                                                    }

                                                }

                                            };

                                        }

                                    }

                                }

                            }

                        }

                        // Enable Description button and track selected parameter

                        FieldInfo rpField = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (rpField != null)

                        {

                            object rpPage = rpField.GetValue(vcForm);

                            if (rpPage != null)

                            {

                                FieldInfo tvField = rpPage.GetType().GetField("_treeView", BindingFlags.NonPublic | BindingFlags.Instance);

                                FieldInfo btnDescField = rpPage.GetType().GetField("_viewDescription", BindingFlags.NonPublic | BindingFlags.Instance);

                                TreeView tv = tvField != null ? tvField.GetValue(rpPage) as TreeView : null;

                                Control btnDesc = btnDescField != null ? btnDescField.GetValue(rpPage) as Control : null;

                                if (tv != null && btnDesc != null && btnDesc.Tag == null)

                                {

                                    btnDesc.Tag = "wired";

                                    tv.AfterSelect += (sTv, eTv) => {

                                        if (eTv.Node != null)

                                        {

                                            lastSelectedParameter = eTv.Node.Text;

                                            btnDesc.Enabled = true;

                                        }

                                    };

                                }

                                // Gate TreeView double-click on parameters when disconnected from car
                                MethodInfo mDblOrig = rpPage.GetType().GetMethod("_parameterTreeView_DoubleClick", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (tv != null && mDblOrig != null && tv.Tag == null)
                                {
                                    tv.Tag = "wired_gated_dblclick";
                                    Delegate delTv = Delegate.CreateDelegate(typeof(EventHandler), rpPage, mDblOrig);
                                    tv.DoubleClick -= (EventHandler)delTv;
                                    tv.DoubleClick += (sTv, eTv) => {
                                        Point pt = tv.PointToClient(Cursor.Position);
                                        TreeNode clickedNode = tv.GetNodeAt(pt);
                                        TreeNode targetNode = clickedNode ?? tv.SelectedNode;
                                        if (targetNode == null) return;

                                        // Only gate actual parameter leaves, let category/folder nodes expand normally
                                        if (targetNode.Nodes.Count > 0)
                                        {
                                            return;
                                        }

                                        tv.SelectedNode = targetNode;

                                        if (!IsMockSimulation && !IsDiceConnected)
                                        {
                                            string paramTitle = targetNode.Text;
                                            var actReq = ShowVehicleRequiredDialog(tv.FindForm(), paramTitle, "parameter");
                                            if (actReq == VehicleRequiredAction.EnableMock)
                                            {
                                                if (_activeChkMock != null) _activeChkMock.Checked = true;
                                                IsMockSimulation = true;
                                                if (_refreshVehCommStatusAction != null) _refreshVehCommStatusAction();
                                                mDblOrig.Invoke(rpPage, new object[] { sTv, eTv });
                                            }
                                            else if (actReq == VehicleRequiredAction.TryConnect)
                                            {
                                                InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                                                if (!IsDiceConnected)
                                                {
                                                    MessageBox.Show(tv.FindForm(), "Could not connect to vehicle.\n\nPlease ensure DiCE is connected via USB and OBD-II, and ignition is in Position II.", "Vehicle Not Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                                    return;
                                                }
                                                if (_refreshVehCommStatusAction != null) _refreshVehCommStatusAction();
                                                mDblOrig.Invoke(rpPage, new object[] { sTv, eTv });
                                            }
                                            return;
                                        }

                                        mDblOrig.Invoke(rpPage, new object[] { sTv, eTv });
                                    };
                                }

                                // Also gate the >> Add button when disconnected
                                FieldInfo btnAddField = rpPage.GetType().GetField("_addButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                Control btnAdd = btnAddField != null ? btnAddField.GetValue(rpPage) as Control : null;
                                MethodInfo mAddOrig = rpPage.GetType().GetMethod("_addButton_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (btnAdd != null && mAddOrig != null && btnAdd.Tag == null)
                                {
                                    btnAdd.Tag = "wired_gated_add";
                                    Delegate delAdd = Delegate.CreateDelegate(typeof(EventHandler), rpPage, mAddOrig);
                                    btnAdd.Click -= (EventHandler)delAdd;
                                    btnAdd.Click += (sBtn, eBtn) => {
                                        TreeNode selNode = tv != null ? tv.SelectedNode : null;
                                        if (selNode != null && selNode.Nodes.Count == 0)
                                        {
                                            if (!IsMockSimulation && !IsDiceConnected)
                                            {
                                                string paramTitle = selNode.Text;
                                                var actReq = ShowVehicleRequiredDialog(btnAdd.FindForm(), paramTitle, "parameter");
                                                if (actReq == VehicleRequiredAction.EnableMock)
                                                {
                                                    if (_activeChkMock != null) _activeChkMock.Checked = true;
                                                    IsMockSimulation = true;
                                                    if (_refreshVehCommStatusAction != null) _refreshVehCommStatusAction();
                                                    mAddOrig.Invoke(rpPage, new object[] { sBtn, eBtn });
                                                }
                                                else if (actReq == VehicleRequiredAction.TryConnect)
                                                {
                                                    InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                                                    if (!IsDiceConnected)
                                                    {
                                                        MessageBox.Show(btnAdd.FindForm(), "Could not connect to vehicle.\n\nPlease ensure DiCE is connected via USB and OBD-II, and ignition is in Position II.", "Vehicle Not Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                                        return;
                                                    }
                                                    if (_refreshVehCommStatusAction != null) _refreshVehCommStatusAction();
                                                    mAddOrig.Invoke(rpPage, new object[] { sBtn, eBtn });
                                                }
                                                return;
                                            }
                                        }
                                        mAddOrig.Invoke(rpPage, new object[] { sBtn, eBtn });
                                    };
                                }

                                FieldInfo lvField = rpPage.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                Control lvCtrl = lvField != null ? lvField.GetValue(rpPage) as Control : null;
                                if (lvCtrl != null && lvCtrl.Tag == null)
                                {
                                    lvCtrl.Tag = "wired_remove";
                                    lvCtrl.KeyDown += (sKey, eKey) => {
                                        if (eKey.KeyCode == Keys.Delete)
                                        {
                                            try {
                                                MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                                if (miRem != null) {
                                                    miRem.Invoke(rpPage, null);
                                                    Console.WriteLine("[Launcher] Parameter removed via Delete key");
                                                }
                                            } catch (Exception exK) {
                                                Console.WriteLine("[Launcher] Delete key error: " + exK.Message);
                                            }
                                        }
                                    };

                                    ContextMenuStrip menu = new ContextMenuStrip();
                                    ToolStripMenuItem mnuRem = new ToolStripMenuItem("❌ Remove Selected (Delete)");
                                    mnuRem.Click += (sM, eM) => {
                                        try {
                                            MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (miRem != null) miRem.Invoke(rpPage, null);
                                        } catch (Exception exM) { Console.WriteLine("[Launcher] Context menu remove error: " + exM.Message); }
                                    };
                                    ToolStripMenuItem mnuClr = new ToolStripMenuItem("🗑️ Clear All");
                                    mnuClr.Click += (sM, eM) => {
                                        try {
                                            PropertyInfo piItems = lvCtrl.GetType().GetProperty("Items");
                                            IEnumerable items = piItems != null ? piItems.GetValue(lvCtrl, null) as IEnumerable : null;
                                            if (items != null) {
                                                foreach (object it in items) {
                                                    PropertyInfo piItSel = it.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(it, true, null);
                                                }
                                            }
                                            MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (miRem != null) miRem.Invoke(rpPage, null);
                                        } catch (Exception exM) { Console.WriteLine("[Launcher] Context menu clear error: " + exM.Message); }
                                    };
                                    menu.Items.Add(mnuRem);
                                    menu.Items.Add(mnuClr);
                                    lvCtrl.ContextMenuStrip = menu;
                                }

                                FieldInfo btnRemField = rpPage.GetType().GetField("_removeButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                Control btnRem = btnRemField != null ? btnRemField.GetValue(rpPage) as Control : null;
                                if (btnRem != null)
                                {
                                    btnRem.Visible = true;
                                }

                                // Wire Automatic Range Recalculation for Plotter / Graph View
                                try
                                {
                                    FieldInfo pdField = rpPage.GetType().GetField("_plotterDialog", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object pd = pdField != null ? pdField.GetValue(rpPage) : null;
                                    Form pdForm = pd as Form;
                                    if (pdForm != null && pdForm.Tag == null)
                                    {
                                        pdForm.Tag = "wired_autorange";
                                        FieldInfo pField = pd.GetType().GetField("_plotter", BindingFlags.NonPublic | BindingFlags.Instance);
                                        Control plotterCtrl = pField != null ? pField.GetValue(pd) as Control : null;

                                        Action performAutoRangeSafe = () =>
                                        {
                                            try
                                            {
                                                if (plotterCtrl != null)
                                                {
                                                    FieldInfo icField = plotterCtrl.GetType().GetField("_inputChannels", BindingFlags.NonPublic | BindingFlags.Instance);
                                                    IList channels = icField != null ? icField.GetValue(plotterCtrl) as IList : null;
                                                    if (channels != null && channels.Count > 0)
                                                    {
                                                        foreach (object ch in channels)
                                                        {
                                                            PropertyInfo piAuto = ch.GetType().GetProperty("AutoScale");
                                                            if (piAuto != null) piAuto.SetValue(ch, true, null);
                                                        }
                                                    }
                                                    MethodInfo miAuto = plotterCtrl.GetType().GetMethod("PerformAutoRange", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                    if (miAuto != null) miAuto.Invoke(plotterCtrl, null);
                                                    plotterCtrl.Invalidate();
                                                }
                                            }
                                            catch {}
                                        };

                                        pdForm.VisibleChanged += (sV, eV) => {
                                            if (pdForm.Visible) performAutoRangeSafe();
                                        };

                                        Timer autoRangeTimer = new Timer();
                                        autoRangeTimer.Interval = 1500;
                                        autoRangeTimer.Tick += (sT, eT) => {
                                            if (pdForm.Visible)
                                            {
                                                performAutoRangeSafe();
                                            }
                                        };
                                        autoRangeTimer.Start();
                                        Console.WriteLine("[Launcher] Automatic graph range recalculation active");
                                    }
                                }
                                catch (Exception exPlot)
                                {
                                    Console.WriteLine("[Launcher] Error wiring graph auto-range: " + exPlot.Message);
                                }

                            }

                        }

                        // Setup simulated live vehicle sensor model when disconnected from DiCE

                        FieldInfo wfField = vcForm.GetType().GetField("_vehicleCommunicationWf", BindingFlags.NonPublic | BindingFlags.Instance);

                        object wf = wfField != null ? wfField.GetValue(vcForm) : null;

                        // vcCtrl already defined

                        if (vcCtrl != null && vcCtrl.Tag == null && wf != null)

                        {

                            vcCtrl.Tag = "sim_active";

                            var simTimer = new System.Windows.Forms.Timer();

                            simTimer.Interval = 500;

                            var rnd = new Random();

                            var simStates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                            simTimer.Tick += (sSim, eSim) => {

                                try

                                {
                                    // When disconnected from physical vehicle and DiCE is offline,
                                    // stop Volvo's background timer so it doesn't poll DiCE and spew exceptions/call stacks into the terminal
                                    if (!IsDiceConnected)
                                    {
                                        FieldInfo tField = wf.GetType().GetField("_parameterReadTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (tField != null)
                                        {
                                            object tmr = tField.GetValue(wf);
                                            if (tmr != null)
                                            {
                                                MethodInfo stopM = tmr.GetType().GetMethod("Stop");
                                                if (stopM != null) stopM.Invoke(tmr, null);
                                            }
                                        }
                                    }

                                    if (!IsMockSimulation) return;

                                    PropertyInfo piToRead = wf.GetType().GetProperty("ParametersToRead");

                                    if (piToRead == null) return;

                                    ICollection toRead = piToRead.GetValue(wf, null) as ICollection;

                                    if (toRead == null || toRead.Count == 0) return;

                                    FieldInfo evField = wf.GetType().GetField("OnParameterRead", BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (evField == null) return;

                                    MulticastDelegate del = evField.GetValue(wf) as MulticastDelegate;

                                    if (del == null) return;

                                    Type resType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleCommunication.ReadParameterResult);

                                    foreach (object item in toRead)

                                    {

                                        PropertyInfo piName = item.GetType().GetProperty("LongName");

                                        PropertyInfo piShort = item.GetType().GetProperty("ShortName");

                                        PropertyInfo piUnit = item.GetType().GetProperty("Unit");

                                        PropertyInfo piMin = item.GetType().GetProperty("PlotterMin");

                                        PropertyInfo piMax = item.GetType().GetProperty("PlotterMax");

                                        string longName = piName != null ? (string)piName.GetValue(item, null) : "";

                                        string shortName = piShort != null ? (string)piShort.GetValue(item, null) : "";

                                        string unit = piUnit != null ? (string)piUnit.GetValue(item, null) : "";

                                        double pMin = piMin != null ? Convert.ToDouble(piMin.GetValue(item, null)) : 0.0;

                                        double pMax = piMax != null ? Convert.ToDouble(piMax.GetValue(item, null)) : 100.0;

                                        string key = !string.IsNullOrEmpty(longName) ? longName : shortName;

                                        if (string.IsNullOrEmpty(key)) continue;

                                        if (!simStates.ContainsKey(key))

                                        {

                                            if (key.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 && key.IndexOf("engine", StringComparison.OrdinalIgnoreCase) < 0)

                                                simStates[key] = 0.0;

                                            else if (key.IndexOf("rpm", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("engine speed", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 750.0;

                                            else if (key.IndexOf("voltage", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("power supply", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 12.5;

                                            else if (key.IndexOf("fuel pressure", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 350000.0;

                                            else if (key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("ect", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 85.0;

                                            else if (key.IndexOf("pedal", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 8.0;

                                            else if (key.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)

                                                simStates[key] = 25.0;

                                            else

                                                simStates[key] = pMax > pMin ? pMin + (pMax - pMin) * 0.3 : 15.0;

                                        }

                                        double cur = simStates[key];

                                        double val = cur;

                                        string formattedText = "";

                                        if (key.IndexOf("rpm", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("engine speed", StringComparison.OrdinalIgnoreCase) >= 0)

                                        {

                                            cur += (rnd.NextDouble() - 0.5) * 30.0;

                                            if (cur < 700.0) cur = 700.0;

                                            if (cur > 820.0) cur = 820.0;

                                            val = Math.Round(cur);

                                            formattedText = string.Format("{0:0} {1}", val, unit);

                                        }

                                        else if (key.IndexOf("voltage", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("power supply", StringComparison.OrdinalIgnoreCase) >= 0)

                                        {

                                            cur += (rnd.NextDouble() - 0.5) * 0.08;

                                            if (cur < 12.1) cur = 12.1;

                                            if (cur > 12.9) cur = 12.9;

                                            val = Math.Round(cur, 2);

                                            formattedText = string.Format("{0:0.00} {1}", val, unit);

                                        }

                                        else if (key.IndexOf("fuel pressure", StringComparison.OrdinalIgnoreCase) >= 0)

                                        {

                                            cur += (rnd.NextDouble() - 0.5) * 6000.0;

                                            if (cur < 330000.0) cur = 330000.0;

                                            if (cur > 370000.0) cur = 370000.0;

                                            val = Math.Round(cur);

                                            formattedText = string.Format("{0:0} {1}", val, unit);

                                        }

                                        else if (key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("ect", StringComparison.OrdinalIgnoreCase) >= 0)

                                        {

                                            cur += (rnd.NextDouble() - 0.5) * 0.2;

                                            if (cur < 80.0) cur = 80.0;

                                            if (cur > 92.0) cur = 92.0;

                                            val = Math.Round(cur, 1);

                                            formattedText = string.Format("{0:0.0} {1}", val, unit);

                                        }

                                        else if (key.IndexOf("pedal", StringComparison.OrdinalIgnoreCase) >= 0)

                                        {

                                            cur += (rnd.NextDouble() - 0.5) * 0.2;

                                            if (cur < 7.6) cur = 7.6;

                                            if (cur > 8.4) cur = 8.4;

                                            val = Math.Round(cur, 1);

                                            formattedText = string.Format("{0:0.0} {1}", val, unit);

                                        }

                                        else

                                        {

                                            double span = pMax > pMin ? (pMax - pMin) : 20.0;

                                            cur += (rnd.NextDouble() - 0.5) * (span * 0.05);

                                            if (pMax > pMin)

                                            {

                                                if (cur < pMin) cur = pMin;

                                                if (cur > pMax) cur = pMax;

                                            }

                                            val = Math.Round(cur, 1);

                                            formattedText = string.Format("{0:0.0} {1}", val, unit);

                                        }

                                        simStates[key] = cur;

                                        object resObj = Activator.CreateInstance(resType, new object[] { val, formattedText });

                                        del.DynamicInvoke(new object[] { item, resObj });

                                    }

                                }

                                catch {}

                            };

                            simTimer.Start();

                        }

                    }

                }

            }

            catch {}

    }
}
