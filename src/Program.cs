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
using System.Runtime.InteropServices;
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
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

    public static bool IsMockSimulation = false;

    const string TargetVIN = "YV1BW5251B1189886";

    const string TargetProfile = "0b00c8af84add183";

    const string TargetPartnerGroup = "1002";

    public static string TargetCommTool
    {
        get { return CommToolManager.CurrentToolName; }
    }

    public static object CurrentCommToolEnumValue
    {
        get { return CommToolManager.IsCurrentToolDice ? diceEnumValue : (j2534EnumValue ?? diceEnumValue); }
    }

    public static int CurrentCommToolTypeInt
    {
        get { return CommToolManager.IsCurrentToolDice ? 1 : 0; }
    }

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
    static object _activeDtcFaulttraceWf = null;
    static object _activeNetworkFaulttraceWf = null;
    static CheckBox _activeChkMock = null;
    static Action _refreshVehCommStatusAction = null;
    static Control internalReadoutBtn = null;
    static object vehInfoInstance = null;
    static object vehProfile = null;
    static object diceEnumValue = null;
    static object j2534EnumValue = null;
    static Button TopBarDeviceSelectButton = null;
    static TextBox _topBarTxtVin = null;
    static Label _topBarLblInfo = null;

    public static void SwitchDiagnosticAdapter(CommToolItem newTool)
    {
        if (newTool == null) return;
        if (string.Equals(newTool.Name, CommToolManager.CurrentToolName, StringComparison.OrdinalIgnoreCase))
            return;

        if (IsDiceConnected)
        {
            var dr = MessageBox.Show(
                "Changing the diagnostic adapter will disconnect the active car session.\r\n\r\nSwitch adapter to: " + newTool.DisplayName + "?",
                "Switch Diagnostic Adapter",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
            DisconnectCarStatus();
        }

        CommToolManager.SelectTool(newTool);
        Logger.Info("CommTool", "Switched diagnostic communication adapter to: " + newTool.DisplayName);

        if (vehInfoInstance != null)
        {
            try
            {
                FieldInfo viCtName = vehInfoInstance.GetType().GetField("_commToolName", BindingFlags.NonPublic | BindingFlags.Instance);
                if (viCtName != null) viCtName.SetValue(vehInfoInstance, TargetCommTool);
            }
            catch {}
        }
        if (_cachedVehInfoInstance != null && _cachedVehInfoInstance != vehInfoInstance)
        {
            try
            {
                FieldInfo viCtName = _cachedVehInfoInstance.GetType().GetField("_commToolName", BindingFlags.NonPublic | BindingFlags.Instance);
                if (viCtName != null) viCtName.SetValue(_cachedVehInfoInstance, TargetCommTool);
            }
            catch {}
        }

        if (_mainLauncherForm != null)
        {
            ConfigureAllCommTools(_mainLauncherForm, new HashSet<object>(), CurrentCommToolEnumValue);
        }

        _activeVehicleCommunicationWf = null;
        vehCommInitialized = false;

        if (TopBarDeviceSelectButton != null && !TopBarDeviceSelectButton.IsDisposed)
        {
            TopBarDeviceSelectButton.Text = "🔌 " + newTool.Name + " ▾";
        }
        if (TopBarCarStatusLabel != null && !TopBarCarStatusLabel.IsDisposed)
        {
            TopBarCarStatusLabel.Text = "⚪ " + newTool.Name;
            TopBarCarStatusLabel.ForeColor = Color.FromArgb(100, 110, 120);
        }
    }

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
        Logger.Init();
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

            vidaPath = Directory.Exists(@"C:\Users\david\git\openvida\VIDA\") ? @"C:\Users\david\git\openvida\VIDA\" : @"C:\Users\david\git\vida\VIDA\";

        }

        if (!vidaPath.EndsWith("\\"))
        {
            vidaPath += "\\";
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

        DiagApplication.Instance.RootUrl = diagDir;

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

            Logger.Error("App", "ThreadException caught: " + (e.Exception != null ? e.Exception.Message : "Unknown"), e.Exception);
            Logger.ShowCrashDialog(e.Exception, "Application.ThreadException");

        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => {

            Exception unhandledEx = e.ExceptionObject as Exception;
            Logger.Error("App", "DomainException caught: " + (unhandledEx != null ? unhandledEx.Message : "" + e.ExceptionObject), unhandledEx);
            Logger.LogCrash(unhandledEx, "AppDomain.CurrentDomain.UnhandledException");

        };

        Form mainForm = new Form();

        _mainLauncherForm = mainForm;
        StartOpenVidaIpcServer();

        mainForm.Text = "OpenVIDA " + AppVersion.Tag + " - Standalone Diagnostics";

        mainForm.ShowIcon = true;
        try
        {
            SetCurrentProcessExplicitAppUserModelID("OpenVIDA.Diagnostics.App");
        }
        catch {}

        Action applyWindowIcon = () =>
        {
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
                if (!File.Exists(icoPath))
                {
                    string cand = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\assets\vida_icon.ico");
                    if (File.Exists(cand)) icoPath = cand;
                }

                if (File.Exists(icoPath))
                {
                    Icon ico32 = new Icon(icoPath, 32, 32);
                    Icon ico16 = new Icon(icoPath, 16, 16);
                    mainForm.Icon = ico32;
                    if (mainForm.IsHandleCreated)
                    {
                        SendMessage(mainForm.Handle, 0x0080, 1, ico32.Handle); // WM_SETICON, ICON_BIG
                        SendMessage(mainForm.Handle, 0x0080, 0, ico16.Handle); // WM_SETICON, ICON_SMALL
                    }
                }
                else
                {
                    mainForm.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch (Exception exIco)
            {
                Console.WriteLine("[Launcher] Warning loading icon: " + exIco.Message);
            }
        };

        applyWindowIcon();
        mainForm.HandleCreated += (sH, eH) => applyWindowIcon();
        mainForm.Shown += (sS, eS) => applyWindowIcon();

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
        _topBarTxtVin = txtVin;

        topBar.Controls.Add(txtVin);

        Label lblInfo = new Label();
        lblInfo.Text = "|  Volvo V70 (08-), MY 2011, D5204T2  |  13 DTCs Stored  |";
        lblInfo.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        lblInfo.ForeColor = Color.FromArgb(60, 64, 67);
        lblInfo.AutoSize = true;
        lblInfo.Location = new Point(485, 10);
        _topBarLblInfo = lblInfo;
        topBar.Controls.Add(lblInfo);

        Button btnDevice = new Button();
        btnDevice.Text = "🔌 " + CommToolManager.CurrentToolName + " ▾";
        btnDevice.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnDevice.BackColor = Color.FromArgb(245, 247, 250);
        btnDevice.ForeColor = Color.FromArgb(40, 44, 48);
        btnDevice.FlatStyle = FlatStyle.Flat;
        btnDevice.FlatAppearance.BorderColor = Color.FromArgb(195, 200, 205);
        btnDevice.SetBounds(750, 7, 130, 24);
        btnDevice.Cursor = Cursors.Hand;
        btnDevice.AutoSize = true;
        btnDevice.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnDevice.Padding = new Padding(3, 0, 3, 0);

        TopBarDeviceSelectButton = btnDevice;
        topBar.Controls.Add(btnDevice);

        ToolTip devTip = new ToolTip();
        devTip.SetToolTip(btnDevice, "Diagnostic Adapter Selection\r\n\r\nCurrent: " + CommToolManager.CurrentDisplayName + "\r\n\r\nClick to switch between Volvo DiCE and any installed J2534 PassThru adapters (Mongoose, Tactrix, Scanmatik, VCX Nano, etc.).");

        btnDevice.Click += (s, e) =>
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9f);

            var tools = CommToolManager.AvailableTools;
            foreach (var tool in tools)
            {
                var item = new ToolStripMenuItem();
                bool isSelected = string.Equals(tool.Name, CommToolManager.CurrentToolName, StringComparison.OrdinalIgnoreCase);
                item.Text = tool.DisplayName;
                item.Checked = isSelected;
                item.Font = new Font("Segoe UI", 9f, isSelected ? FontStyle.Bold : FontStyle.Regular);

                var captured = tool;
                item.Click += (ts, te) =>
                {
                    SwitchDiagnosticAdapter(captured);
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            var rescanItem = new ToolStripMenuItem("🔄 Rescan J2534 Adapters");
            rescanItem.Click += (ts, te) =>
            {
                CommToolManager.RescanTools();
                if (TopBarDeviceSelectButton != null && !TopBarDeviceSelectButton.IsDisposed)
                {
                    TopBarDeviceSelectButton.Text = "🔌 " + CommToolManager.CurrentToolName + " ▾";
                    devTip.SetToolTip(TopBarDeviceSelectButton, "Diagnostic Adapter Selection\r\n\r\nCurrent: " + CommToolManager.CurrentDisplayName + "\r\n\r\nClick to switch between Volvo DiCE and any installed J2534 PassThru adapters (Mongoose, Tactrix, Scanmatik, VCX Nano, etc.).");
                }
            };
            menu.Items.Add(rescanItem);

            menu.Show(btnDevice, new Point(0, btnDevice.Height));
        };

        TopBarCarStatusLabel = new Label();
        TopBarCarStatusLabel.Text = "⚪ " + TargetCommTool;
        TopBarCarStatusLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        TopBarCarStatusLabel.ForeColor = Color.FromArgb(100, 110, 120);
        TopBarCarStatusLabel.BackColor = Color.Transparent;
        TopBarCarStatusLabel.AutoSize = true;
        TopBarCarStatusLabel.Location = new Point(822, 10);
        TopBarCarStatusLabel.Cursor = Cursors.Hand;

        ToolTip statusTip = new ToolTip();
        statusTip.SetToolTip(TopBarCarStatusLabel, "DiCE Hardware & Battery Voltage Monitor\n\n🟢 Normal: >= 12.0 V\n⚠️ Warning: 11.5 - 11.9 V\n🛑 Critical: < 11.5 V\n\nClick to toggle DiCE connection.");

        Button btnConnect = new Button();
        btnConnect.Text = "Connect";
        btnConnect.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnConnect.BackColor = Color.FromArgb(230, 240, 250);
        btnConnect.ForeColor = Color.FromArgb(0, 102, 204);
        btnConnect.FlatStyle = FlatStyle.Flat;
        btnConnect.FlatAppearance.BorderColor = Color.FromArgb(160, 200, 240);
        btnConnect.SetBounds(936, 7, 78, 24);
        btnConnect.Cursor = Cursors.Hand;
        TopBarDiceConnectButton = btnConnect;

        Button btnReadouts = new Button();
        btnReadouts.Text = "📁 Readouts";
        btnReadouts.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnReadouts.BackColor = Color.FromArgb(245, 247, 250);
        btnReadouts.ForeColor = Color.FromArgb(60, 64, 67);
        btnReadouts.FlatStyle = FlatStyle.Flat;
        btnReadouts.FlatAppearance.BorderColor = Color.FromArgb(195, 200, 205);
        btnReadouts.SetBounds(1020, 7, 85, 24);
        btnReadouts.Cursor = Cursors.Hand;
        btnReadouts.Click += (s, e) =>
        {
            ReadoutExporter.OpenReadoutsFolder();
        };

        Button btnLogs = new Button();
        btnLogs.Text = "📋 Logs";
        btnLogs.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnLogs.BackColor = Color.FromArgb(245, 247, 250);
        btnLogs.ForeColor = Color.FromArgb(60, 64, 67);
        btnLogs.FlatStyle = FlatStyle.Flat;
        btnLogs.FlatAppearance.BorderColor = Color.FromArgb(195, 200, 205);
        btnLogs.SetBounds(1111, 7, 72, 24);
        btnLogs.Cursor = Cursors.Hand;
        btnLogs.Click += (s, e) =>
        {
            Logger.OpenLogsFolder();
        };

        Action repositionConnectBtn = () =>
        {
            if (btnDevice != null && !btnDevice.IsDisposed && _topBarLblInfo != null && !_topBarLblInfo.IsDisposed)
            {
                btnDevice.Left = Math.Max(730, _topBarLblInfo.Right + 12);
            }
            if (TopBarCarStatusLabel != null && !TopBarCarStatusLabel.IsDisposed)
            {
                if (btnDevice != null && !btnDevice.IsDisposed)
                {
                    TopBarCarStatusLabel.Left = btnDevice.Right + 8;
                }
                if (btnConnect != null && !btnConnect.IsDisposed)
                {
                    btnConnect.Left = TopBarCarStatusLabel.Right + 8;
                    if (btnReadouts != null && !btnReadouts.IsDisposed)
                    {
                        btnReadouts.Left = btnConnect.Right + 6;
                        if (btnLogs != null && !btnLogs.IsDisposed)
                        {
                            btnLogs.Left = btnReadouts.Right + 6;
                        }
                    }
                }
            }
        };
        btnDevice.SizeChanged += (s, e) => repositionConnectBtn();
        TopBarCarStatusLabel.SizeChanged += (s, e) => repositionConnectBtn();

        Action triggerDiceToggle = () =>
        {
            if (IsDiceConnected)
            {
                DisconnectCarStatus();
                return;
            }
            if (TopBarCarStatusLabel != null)
            {
                TopBarCarStatusLabel.Text = "⏳ " + TargetCommTool;
                TopBarCarStatusLabel.ForeColor = Color.FromArgb(0, 102, 204);
                repositionConnectBtn();
            }
            InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
        };
        TopBarCarStatusLabel.Click += (s, e) => triggerDiceToggle();
        btnConnect.Click += (s, e) => triggerDiceToggle();

        topBar.Controls.Add(TopBarCarStatusLabel);
        topBar.Controls.Add(btnConnect);
        topBar.Controls.Add(btnReadouts);
        topBar.Controls.Add(btnLogs);
        repositionConnectBtn();

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

        string ewdCheck1 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\wiring_diagrams\US_Eng"));
        string ewdCheck2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wiring_diagrams\US_Eng"));
        bool hasEwdData = (Directory.Exists(ewdCheck1) && File.Exists(Path.Combine(ewdCheck1, "pdf-links.js")))
                       || (Directory.Exists(ewdCheck2) && File.Exists(Path.Combine(ewdCheck2, "pdf-links.js")));

        Button btnTabWiring = new Button();
        btnTabWiring.Text = "⚡ WIRING DIAGRAMS";
        btnTabWiring.SetBounds(1080, 38, 180, 30);
        btnTabWiring.FlatStyle = FlatStyle.Flat;
        btnTabWiring.FlatAppearance.BorderSize = 1;
        btnTabWiring.FlatAppearance.BorderColor = hasEwdData ? Color.FromArgb(180, 185, 190) : Color.FromArgb(205, 210, 215);
        btnTabWiring.Cursor = hasEwdData ? Cursors.Hand : Cursors.Default;
        btnTabWiring.Enabled = hasEwdData;
        topBar.Controls.Add(btnTabWiring);
        _btnTabWiring = btnTabWiring;

        Button[] tabButtons = new Button[] { btnTabVehProfile, btnTabFaultTrace, btnTabNetwork, btnTabVehComm, btnTabRefInfo, btnTabManuals, btnTabParts, btnTabWiring };

        Action<Button> setActiveTabButton = (activeBtn) =>

        {

            foreach (var b in tabButtons)

            {

                if (!b.Enabled)
                {
                    b.BackColor = Color.FromArgb(220, 224, 228);
                    b.ForeColor = Color.FromArgb(145, 152, 160);
                    b.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                }
                else if (b == activeBtn)

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
                HideLegacyDiagnosticChrome(mObj);
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
                                InstallFaultTraceActionBar(csftForm, mainForm);
                                var curDtcs = RetrieveCurrentDtcs();
                                UpdateTopBarDtcCount(curDtcs.Count);
                                UpdateFaultTraceActionBarStatus(curDtcs.Count);

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

        diag.RootUrl = diagDir;

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

        diag.ApplicationTitle = "OpenVIDA 2014D - Standalone Diagnostics";

        diag.UserRole = "1";

        diag.AdminRole = "1";

        diag.PartnerGroup = TargetPartnerGroup;

        diag.VIN = TargetVIN;

        diag.ProfileId = TargetProfile;

        mainForm.Controls.Add(diag);

        mainForm.Controls.Add(topBar);

        // Resolve CommToolType.DICE and J2534 enums
        Type commToolTypeEnum = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            commToolTypeEnum = asm.GetType("Vcc.Vida.DiagSwdl.VehCom2.CommToolType");
            if (commToolTypeEnum != null) break;
        }

        if (commToolTypeEnum == null)
        {
            commToolTypeEnum = Assembly.LoadFrom(Path.Combine(diagDir, "VehCom2.DLL")).GetType("Vcc.Vida.DiagSwdl.VehCom2.CommToolType");
        }

        if (commToolTypeEnum != null)
        {
            try { diceEnumValue = Enum.Parse(commToolTypeEnum, "DICE"); } catch {}
            try { j2534EnumValue = Enum.Parse(commToolTypeEnum, "J2534"); } catch {}
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

        if (args.Length > 0 && Array.Exists(args, a => a == "/test" || a == "--test"))
        {
            StartAutomatedTestSequence(vidaPath, mainForm, btnTabFaultTrace, btnTabNetwork, btnTabVehComm, btnTabRefInfo, _btnTabManuals, _btnTabParts, _btnTabWiring, switchTab, setActiveTabButton);
        }
        else if (args.Length > 0 && Array.Exists(args, a => a == "/ewd-test" || a == "--ewd-test"))
        {
            StartEwdTestSequence(vidaPath, mainForm, _btnTabWiring);
        }
        else if (args.Length > 0 && Array.Exists(args, a => a == "/advanced-test" || a == "--advanced-test"))
        {
            StartAdvancedAuditSequence(vidaPath, mainForm, btnTabVehComm);
        }
        else if (args.Length > 0 && Array.Exists(args, a => a == "/car-test" || a == "--car-test"))
        {
            StartInCarTestSequence(vidaPath, mainForm, btnTabFaultTrace, btnTabNetwork, btnTabVehComm);
        }
        else if (args.Length > 0 && Array.Exists(args, a => a == "/clear-dtc-test" || a == "--clear-dtc-test"))
        {
            StartClearDtcTestSequence(vidaPath, mainForm, btnTabFaultTrace, switchTab, setActiveTabButton);
        }

        Console.WriteLine("[Launcher] Running Application message loop...");

        mainForm.Shown += (sShown, eShown) =>
        {
            StartOpenVidaIpcServer();
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

        mainForm.FormClosed += (sClosed, eClosed) =>
        {
            StopOpenVidaIpcServer();
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

