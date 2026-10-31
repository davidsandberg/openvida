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
    public static bool IsMockSimulation = false;

    const string TargetVIN = "YV1BW5251B1189886";

    const string TargetProfile = "0b00c8af84add183";

    const string TargetPartnerGroup = "1002";

    const string TargetCommTool = "DiCE-206751";

    const string TargetCulture = "en-US";

    static Form _mainLauncherForm = null;

    static Form _activeDocForm = null;

    static WebBrowser _activeDocWb = null;

    static Control _refFormCtrl = null;

    static Control _vehCommCtrl = null;

    static Control _activeDtcRefControl = null;

    static Action<string> _onVehicleCommHotspotClicked = null;

    static Action<string> _onDtcRefHotspotClicked = null;


    static bool vehCommInitialized = false;
    static object _activeVehicleCommunicationWf = null;
    static object _activeSpecialToolsTab = null;
    static CheckBox _activeChkMock = null;
    static Action _refreshVehCommStatusAction = null;
    static Control internalReadoutBtn = null;
    static object vehInfoInstance = null;
    static object vehProfile = null;
    static object diceEnumValue = null;

    [STAThread]

    static void Main(string[] args)

    {
        if (args != null)
        {
            foreach (var arg in args)
            {
                if (arg.Equals("--mock", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--simulate", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/mock", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/sim", StringComparison.OrdinalIgnoreCase))
                {
                    IsMockSimulation = true;
                }
            }
        }
        Console.WriteLine("[Launcher] Simulation Mode: " + (IsMockSimulation ? "ENABLED (Mock Data)" : "DISABLED (Live DiCE CAN)"));

        try

        {

        try

        {

            Application.EnableVisualStyles();

            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

        }

        catch {}

        // 1. Ensure VIDA_HOME is set

        string vidaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\..\"));

        if (!Directory.Exists(Path.Combine(vidaPath, "jboss")))

        {

            vidaPath = @"C:\Users\david\git\vida\VIDA\";

        }

        Environment.SetEnvironmentVariable("VIDA_HOME", vidaPath);

        // 2. Set assembly resolver for any Diagnostics dependencies

        string diagDir = Path.Combine(vidaPath, @"jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\");

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>

        {

            string name = new AssemblyName(e.Name).Name;

            string file = Path.Combine(diagDir, name + ".DLL");

            if (File.Exists(file)) return Assembly.LoadFrom(file);

            return null;

        };

        // Pre-flight environment & database health check
        if (!PerformStartupHealthCheck(vidaPath))
        {
            return;
        }

        // 3. Initialize DiagApplication singleton

        DiagApplication.Instance.RootUrl = "http://localhost/Vida/Diagnostics/";

        DiagApplication.Instance.Start();

        if (DiagApplication.Instance.ComponentsConfig == null)

        {

            try

            {

                string cfgFile = Path.Combine(diagDir, "components.config.xml");

                if (File.Exists(cfgFile))

                {

                    Assembly coreAsm = typeof(DiagApplication).Assembly;

                    Type ccType = coreAsm.GetType("Vcc.Vida.DiagSwdl.Core.ServiceManagement.ComponentsConfig");

                    Type parserType = coreAsm.GetType("Vcc.Vida.DiagSwdl.Core.ServiceManagement.XmlComponentConfigParser");

                    var doc = new System.Xml.XmlDocument();

                    doc.Load(cfgFile);

                    object compCfg = Activator.CreateInstance(ccType);

                    MethodInfo miFill = parserType.GetMethod("FillConfig", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (miFill != null) miFill.Invoke(null, new object[] { doc, compCfg });

                    MethodInfo miOverride = ccType.GetMethod("OverrideDatasources", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (miOverride != null) miOverride.Invoke(compCfg, new object[] { @"localhost\SQLEXPRESS" });

                    FieldInfo fiCC = typeof(DiagApplication).GetField("__componentsConfig", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fiCC != null) fiCC.SetValue(DiagApplication.Instance, compCfg);

                    Console.WriteLine("[Launcher] Loaded ComponentsConfig from local fallback successfully!");

                }

            }

            catch (Exception exCfg)

            {

                Console.WriteLine("[Launcher] Fallback ComponentsConfig error: " + exCfg.Message);

            }

        }

        // Install SafeListenerList to eliminate ProgressDialog Invoke race condition

        try

        {

            Type pcType = typeof(ProgressControler);

            FieldInfo listenersField = pcType.GetField("__listeners", BindingFlags.NonPublic | BindingFlags.Static);

            if (listenersField != null)

            {

                listenersField.SetValue(null, new SafeListenerList());

                Console.WriteLine("[Launcher] Installed SafeListenerList in ProgressControler!");

            }

        }

        catch (Exception ex)

        {

            Console.WriteLine("[Launcher] Failed to install SafeListenerList: " + ex.Message);

        }

        // 4. Setup Windows Forms styling & unhandled exception safeguards

        Application.ThreadException += (s, e) => {

            if (e.Exception != null && e.Exception.Message.IndexOf("__ComObject", StringComparison.OrdinalIgnoreCase) >= 0)

            {

                // Silently swallow Microsoft AxHost managed-control release quirk

                return;

            }

            Console.WriteLine("[Launcher] ThreadException (caught safely): " + e.Exception.Message);

        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => {

            Console.WriteLine("[Launcher] DomainException (caught safely): " + e.ExceptionObject);

        };

        Form mainForm = new Form();

        _mainLauncherForm = mainForm;

        mainForm.Text = "Volvo VIDA 2014D - Standalone Diagnostics";

        try
        {
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
            if (File.Exists(icoPath))
                mainForm.Icon = new Icon(icoPath);
            else
                mainForm.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch {}

        mainForm.Width = 1366;

        mainForm.Height = 860;

        mainForm.StartPosition = FormStartPosition.CenterScreen;

        mainForm.WindowState = FormWindowState.Normal;

        mainForm.FormClosing += (s, e) => {

            try { Environment.Exit(0); } catch {}

        };

        // Top Toolbar

        Panel topBar = new Panel();

        topBar.Dock = DockStyle.Top;

        topBar.Height = 74;

        topBar.BackColor = Color.FromArgb(240, 242, 245);

        topBar.BorderStyle = BorderStyle.FixedSingle;

        Button btnReadVehicle = new Button();

        btnReadVehicle.Text = "⚡ READ VEHICLE (DiCE OBD-II)";

        btnReadVehicle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        btnReadVehicle.BackColor = Color.FromArgb(0, 102, 204);

        btnReadVehicle.ForeColor = Color.White;

        btnReadVehicle.FlatStyle = FlatStyle.Flat;

        btnReadVehicle.FlatAppearance.BorderSize = 0;

        btnReadVehicle.SetBounds(10, 6, 240, 28);

        btnReadVehicle.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnReadVehicle);

        Label lblVin = new Label();

        lblVin.Text = "VIN:";

        lblVin.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        lblVin.SetBounds(260, 10, 35, 20);

        topBar.Controls.Add(lblVin);

        TextBox txtVin = new TextBox();

        txtVin.Text = TargetVIN;

        txtVin.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        txtVin.SetBounds(298, 7, 175, 25);

        txtVin.ReadOnly = true;

        topBar.Controls.Add(txtVin);

        Label lblInfo = new Label();

        lblInfo.Text = "|  Volvo V70 (08-), MY 2011, D5204T2  |  DiCE-206751  |  13 DTCs Stored";

        lblInfo.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        lblInfo.ForeColor = Color.FromArgb(60, 64, 67);

        lblInfo.AutoSize = true;

        lblInfo.Location = new Point(485, 10);

        topBar.Controls.Add(lblInfo);

        TopBarCarStatusLabel = new Label();
        TopBarCarStatusLabel.Text = "🔋 --.-V  |  🔑 Pos: --  |  ⚪ DiCE: Connecting...";
        TopBarCarStatusLabel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
        TopBarCarStatusLabel.ForeColor = Color.FromArgb(80, 80, 80);
        TopBarCarStatusLabel.BackColor = Color.FromArgb(240, 243, 246);
        TopBarCarStatusLabel.BorderStyle = BorderStyle.FixedSingle;
        TopBarCarStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        TopBarCarStatusLabel.SetBounds(topBar.Width - 460, 8, 300, 25);
        TopBarCarStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        TopBarCarStatusLabel.Cursor = Cursors.Hand;
        TopBarCarStatusLabel.Click += (s, e) => {
            if (TopBarCarStatusLabel != null) TopBarCarStatusLabel.Text = "⏳ Querying DiCE (VBatt & Keypos)...";
            InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
        };
        topBar.Controls.Add(TopBarCarStatusLabel);

        Button btnConnect = new Button();
        btnConnect.Text = "⚡ Connect / Status";
        btnConnect.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnConnect.BackColor = Color.FromArgb(230, 240, 250);
        btnConnect.ForeColor = Color.FromArgb(0, 102, 204);
        btnConnect.FlatStyle = FlatStyle.Flat;
        btnConnect.FlatAppearance.BorderColor = Color.FromArgb(160, 200, 240);
        btnConnect.SetBounds(topBar.Width - 150, 8, 140, 25);
        btnConnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnConnect.Cursor = Cursors.Hand;
        btnConnect.Click += (s, e) =>
        {
            if (TopBarCarStatusLabel != null) TopBarCarStatusLabel.Text = "⏳ Querying DiCE (VBatt & Keypos)...";
            InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
        };
        topBar.Controls.Add(btnConnect);

        // Row 2: Tab Navigation Buttons

        Button btnTabVehProfile = new Button();

        btnTabVehProfile.Text = "🚗 VEHICLE PROFILE";

        btnTabVehProfile.SetBounds(10, 38, 140, 30);

        btnTabVehProfile.FlatStyle = FlatStyle.Flat;

        btnTabVehProfile.FlatAppearance.BorderSize = 1;

        btnTabVehProfile.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabVehProfile.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabVehProfile);

        Button btnTabFaultTrace = new Button();

        btnTabFaultTrace.Text = "🔍 FAULT TRACE (DTCs)";

        btnTabFaultTrace.SetBounds(155, 38, 160, 30);

        btnTabFaultTrace.FlatStyle = FlatStyle.Flat;

        btnTabFaultTrace.FlatAppearance.BorderSize = 1;

        btnTabFaultTrace.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabFaultTrace.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabFaultTrace);

        Button btnTabNetwork = new Button();

        btnTabNetwork.Text = "🌐 NETWORK";

        btnTabNetwork.SetBounds(320, 38, 115, 30);

        btnTabNetwork.FlatStyle = FlatStyle.Flat;

        btnTabNetwork.FlatAppearance.BorderSize = 1;

        btnTabNetwork.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabNetwork.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabNetwork);

        Button btnTabVehComm = new Button();

        btnTabVehComm.Text = "⚡ VEHICLE COMM";

        btnTabVehComm.SetBounds(440, 38, 140, 30);

        btnTabVehComm.FlatStyle = FlatStyle.Flat;

        btnTabVehComm.FlatAppearance.BorderSize = 1;

        btnTabVehComm.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabVehComm.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabVehComm);

        Button btnTabRefInfo = new Button();

        btnTabRefInfo.Text = "📖 REFERENCE INFO";

        btnTabRefInfo.SetBounds(585, 38, 150, 30);

        btnTabRefInfo.FlatStyle = FlatStyle.Flat;

        btnTabRefInfo.FlatAppearance.BorderSize = 1;

        btnTabRefInfo.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabRefInfo.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabRefInfo);

        Button btnTabManuals = new Button();

        btnTabManuals.Text = "📚 SERVICE MANUALS";

        btnTabManuals.SetBounds(740, 38, 165, 30);

        btnTabManuals.FlatStyle = FlatStyle.Flat;

        btnTabManuals.FlatAppearance.BorderSize = 1;

        btnTabManuals.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabManuals.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabManuals);

        _btnTabManuals = btnTabManuals;

        Button btnTabParts = new Button();
        btnTabParts.Text = "📦 PARTS CATALOG";
        btnTabParts.SetBounds(910, 38, 165, 30);
        btnTabParts.FlatStyle = FlatStyle.Flat;
        btnTabParts.FlatAppearance.BorderSize = 1;
        btnTabParts.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
        btnTabParts.Cursor = Cursors.Hand;
        topBar.Controls.Add(btnTabParts);
        _btnTabParts = btnTabParts;

        Button btnTabWiring = new Button();
        btnTabWiring.Text = "⚡ WIRING DIAGRAMS";
        btnTabWiring.SetBounds(1080, 38, 180, 30);
        btnTabWiring.FlatStyle = FlatStyle.Flat;
        btnTabWiring.FlatAppearance.BorderSize = 1;
        btnTabWiring.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
        btnTabWiring.Cursor = Cursors.Hand;
        topBar.Controls.Add(btnTabWiring);
        _btnTabWiring = btnTabWiring;

        Button[] tabButtons = new Button[] { btnTabVehProfile, btnTabFaultTrace, btnTabNetwork, btnTabVehComm, btnTabRefInfo, btnTabManuals, btnTabParts, btnTabWiring };

        Action<Button> setActiveTabButton = (activeBtn) =>

        {

            foreach (var b in tabButtons)

            {

                if (b == activeBtn)

                {

                    b.BackColor = Color.FromArgb(0, 102, 204);

                    b.ForeColor = Color.White;

                    b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

                }

                else

                {

                    b.BackColor = Color.FromArgb(232, 236, 240);

                    b.ForeColor = Color.FromArgb(32, 33, 36);

                    b.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

                }

            }

        };

        setActiveTabButton(btnTabVehProfile);

        Action<string> switchTab = (methodName) =>

        {

            try

            {

                Type mType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");

                object mObj = mType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);

                MethodInfo mi = mType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (mi != null) mi.Invoke(mObj, new object[] { null, EventArgs.Empty });
                UpdateAllCarStatusControls(_lastKnownVoltVal);

                if (methodName.IndexOf("referenceInformation", StringComparison.OrdinalIgnoreCase) >= 0)

                {

                    SetupWorkshopManualsTab(mObj);

                }

            }

            catch (Exception ex)

            {

                Console.WriteLine("[Launcher] SwitchTab " + methodName + " error: " + ex);

            }

        };

        btnTabVehProfile.Click += (s, e) => { if (_manualExplorerPnl != null)

            {

                if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            } switchTab("_vehicleInfoButton_Click"); setActiveTabButton(btnTabVehProfile); InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile); };

        btnTabFaultTrace.Click += (s, e) => { if (_manualExplorerPnl != null)

            {

                if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            }

            switchTab("_customerSymptomsFaultTraceButton_Click");

            setActiveTabButton(btnTabFaultTrace);

            try

            {

                Type dmType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");

                object dm = dmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);

                if (dm != null)

                {

                    object csftForm = dmType.GetMethod("GetCustomerSymptomsFaultTraceFormForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dm, null);

                    if (csftForm != null)

                    {

                        FieldInfo tabField = csftForm.GetType().GetField("_tab", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (tabField != null)

                        {

                            object tabCtrl = tabField.GetValue(csftForm);

                            if (tabCtrl != null)

                            {

                                tabCtrl.GetType().GetProperty("SelectedIndex").SetValue(tabCtrl, 0, null);

                            }

                        }

                    }

                }

            }

            catch {}

        };

        btnTabNetwork.Click += (s, e) => { if (_manualExplorerPnl != null)

            {

                if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            } switchTab("_networkStatusButton_Click"); setActiveTabButton(btnTabNetwork); };

        btnTabVehComm.Click += (s, e) => { if (_manualExplorerPnl != null)

            {

                if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            }

            switchTab("_vehCommButton_Click");

            setActiveTabButton(btnTabVehComm);

            ConfigureVehicleCommTab();

        };

        btnTabRefInfo.Click += (s, e) => {

            if (_manualExplorerPnl != null)

            {

                if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            }

            switchTab("_referenceInformationButton_Click");

            setActiveTabButton(btnTabRefInfo);

        };

        btnTabParts.Click += (s, e) => {
            try
            {
                Type mType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");
                object mObj = mType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                if (!_partsTabConfigured)
                {
                    SetupPartsCatalogTab(mObj);
                }
                if (_manualExplorerPnl != null)
                {
                    if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);
                    _manualExplorerPnl.Visible = false;
                }
            if (_partsCatalogPnl != null)
            {
                if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                _partsCatalogPnl.Visible = false;
            }
            if (_wiringDiagramsPnl != null)
            {
                if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                _wiringDiagramsPnl.Visible = false;
            }
                if (_partsCatalogPnl != null && mObj != null)
                {
                    Control workArea = GetFieldValue(mObj, "_workAreaPanel") as Control;
                    if (workArea != null)
                    {
                        workArea.Controls.Clear();
                        _partsCatalogPnl.Dock = DockStyle.Fill;
                        workArea.Controls.Add(_partsCatalogPnl);
                        _partsCatalogPnl.Visible = true;
                        _partsCatalogPnl.BringToFront();
                        Console.WriteLine("[Launcher] Placed _partsCatalogPnl into _workAreaPanel successfully!");
                    }
                }
                setActiveTabButton(btnTabParts);
            }
            catch (Exception exParts)
            {
                Console.WriteLine("[Launcher] Error opening Parts Catalog tab: " + exParts.Message);
            }
        };

        btnTabWiring.Click += (s, e) => {
            try
            {
                Type mType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");
                object mObj = mType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                if (!_wiringTabConfigured)
                {
                    SetupWiringDiagramsTab(mObj);
                }
                if (_manualExplorerPnl != null)
                {
                    if (_manualExplorerPnl.Parent != null) _manualExplorerPnl.Parent.Controls.Remove(_manualExplorerPnl);
                    _manualExplorerPnl.Visible = false;
                }
                if (_partsCatalogPnl != null)
                {
                    if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                    _partsCatalogPnl.Visible = false;
                }
                if (_wiringDiagramsPnl != null)
                {
                    if (_wiringDiagramsPnl.Parent != null) _wiringDiagramsPnl.Parent.Controls.Remove(_wiringDiagramsPnl);
                    _wiringDiagramsPnl.Visible = false;
                }
                if (_wiringDiagramsPnl != null && mObj != null)
                {
                    Control workArea = GetFieldValue(mObj, "_workAreaPanel") as Control;
                    if (workArea != null)
                    {
                        workArea.Controls.Clear();
                        _wiringDiagramsPnl.Dock = DockStyle.Fill;
                        workArea.Controls.Add(_wiringDiagramsPnl);
                        _wiringDiagramsPnl.Visible = true;
                        _wiringDiagramsPnl.BringToFront();
                        Console.WriteLine("[Launcher] Placed _wiringDiagramsPnl into _workAreaPanel successfully!");
                    }
                }
                setActiveTabButton(btnTabWiring);
            }
            catch (Exception exWiring)
            {
                Console.WriteLine("[Launcher] Error opening Wiring Diagrams tab: " + exWiring.Message);
            }
        };

        btnTabManuals.Click += (s, e) => {

            try

            {

                Type mType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");

                object mObj = mType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);

                if (!_manualsTabConfigured)

                {

                    SetupWorkshopManualsTab(mObj);

                }

                if (_partsCatalogPnl != null)
                {
                    if (_partsCatalogPnl.Parent != null) _partsCatalogPnl.Parent.Controls.Remove(_partsCatalogPnl);
                    _partsCatalogPnl.Visible = false;
                }
                if (_manualExplorerPnl != null && mObj != null)

                {

                    Control workArea = GetFieldValue(mObj, "_workAreaPanel") as Control;

                    if (workArea != null)

                    {

                        workArea.Controls.Clear();

                        _manualExplorerPnl.Dock = DockStyle.Fill;

                        workArea.Controls.Add(_manualExplorerPnl);

                        _manualExplorerPnl.Visible = true;

                        _manualExplorerPnl.BringToFront();

                        Console.WriteLine("[Launcher] Placed _manualExplorerPnl into _workAreaPanel successfully!");

                    }

                }

            }

            catch (Exception exMan)

            {

                Console.WriteLine("[Launcher] Error opening Service Manuals: " + exMan);

            }

            setActiveTabButton(btnTabManuals);

        };

        Console.WriteLine("[Launcher] Initializing DiagnosticMainForm...");

        var diag = new DiagnosticMainForm();

        _diagnosticsMainPanel = diag.GetType().GetField("_mainPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(diag) as Panel;

        diag.Dock = DockStyle.Fill;

        Action<string, string> showDocument = (docId, title) => ShowDocumentWindow(docId, title, mainForm);

        diag.NavigateIE += (id, type, subType, functionGroup) =>

        {

            Console.WriteLine("[Launcher] NavigateIE: id=" + id + ", type=" + type);

            ShowDocumentWindow(id, "Document " + id, mainForm);

        };

        diag.NavigateIEPopupWindow += (id, type, subType, functionGroup) =>

        {

            Console.WriteLine("[Launcher] NavigateIEPopupWindow: id=" + id + ", type=" + type);

            ShowDocumentWindow(id, "Document " + id, mainForm);

        };

        diag.RootUrl = "http://localhost/Vida/Diagnostics/";

        diag.LogPath = Path.Combine(vidaPath, @"System\Log\Diagnostics\");

        diag.CommToolName = TargetCommTool;

        Type dmTypeInit = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");

        if (dmTypeInit != null)

        {

            object dmInst = dmTypeInit.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);

            if (dmInst != null)

            {

                PropertyInfo piNav = dmTypeInit.GetProperty("BrowserNavigation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (piNav != null)

                {

                    piNav.SetValue(dmInst, new LauncherBrowserNavigation(mainForm), null);

                    Console.WriteLine("[Launcher] Successfully wired LauncherBrowserNavigation into DiagnosticManager.Instance.BrowserNavigation!");

                }

            }

        }

        Timer dialogStylerTimer = new Timer();

        dialogStylerTimer.Interval = 100;

        dialogStylerTimer.Tick += (sStyler, eStyler) =>

        {

            foreach (Form openForm in Application.OpenForms)

            {

                if (openForm.GetType().Name == "DtcExtendedViewDialog" && (openForm.Tag as string) != "StyledModern")

                {

                    openForm.Tag = "StyledModern";

                    ModernizeDtcExtendedDialog(openForm, mainForm);

                }

            }

        };

        dialogStylerTimer.Start();

        diag.CultureName = TargetCulture;

        diag.UserId = "1";

        diag.TitleName = "VIDA2014D";

        diag.ApplicationTitle = "Volvo VIDA Standalone Diagnostics";

        diag.UserRole = "1";

        diag.AdminRole = "1";

        diag.PartnerGroup = TargetPartnerGroup;

        diag.VIN = TargetVIN;

        diag.ProfileId = TargetProfile;

        mainForm.Controls.Add(diag);

        mainForm.Controls.Add(topBar);

        // Resolve CommToolType.DICE enum

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())

        {

            Type t = asm.GetType("Vcc.Vida.DiagSwdl.VehCom2.CommToolType");

            if (t != null) { diceEnumValue = Enum.Parse(t, "DICE"); break; }

        }

        if (diceEnumValue == null)

        {

            Type t = Assembly.LoadFrom(Path.Combine(diagDir, "VehCom2.DLL")).GetType("Vcc.Vida.DiagSwdl.VehCom2.CommToolType");

            diceEnumValue = Enum.Parse(t, "DICE");

        }

        Type vpType = typeof(DiagApplication).Assembly.GetType("Vcc.Vida.DiagSwdl.Core.VehicleProfile");

        vehProfile = Activator.CreateInstance(vpType, new object[] { TargetProfile });

        _cachedVehicleProfile = vehProfile;

        mainForm.Load += (s, e) =>

        {

            InitializeDiagnosticsControl(vidaPath, diag);

        };

        btnReadVehicle.Click += (s, e) =>

        {

            ExecuteVehicleReadout(diag);

        };

        // If run with /test flag, systematically switch across all tabs and capture screenshots

        if (args.Length > 0 && (args[0] == "/test" || args[0] == "--test"))
        {
            StartAutomatedTestSequence(vidaPath, mainForm, btnTabFaultTrace, btnTabNetwork, btnTabVehComm, btnTabRefInfo, _btnTabManuals, _btnTabParts, _btnTabWiring, switchTab, setActiveTabButton);
        }
        else if (args.Length > 0 && (args[0] == "/ewd-test" || args[0] == "--ewd-test"))
        {
            StartEwdTestSequence(vidaPath, mainForm, _btnTabWiring);
        }

        Console.WriteLine("[Launcher] Running Application message loop...");

        mainForm.Shown += (sShown, eShown) =>
        {
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    System.Threading.Thread.Sleep(300);
                    InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
                }
                catch {}
            });
        };

        Application.Run(mainForm);

            }

        catch (Exception exFatal)

        {

            string err = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] FATAL CRASH: {1}\r\n{2}\r\n", DateTime.Now, exFatal.Message, exFatal.ToString());

            Console.Error.WriteLine(err);

            try

            {

                File.AppendAllText("vida_launcher_crash.log", err);

            }

            catch {}

            MessageBox.Show(exFatal.ToString(), "VIDA Launcher Fatal Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        }

    }

}

