using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
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

        // 3. Initialize DiagApplication singleton
        DiagApplication.Instance.RootUrl = "http://localhost/Vida/Diagnostics/";
        DiagApplication.Instance.Start();

        // 4. Setup Windows Forms styling
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Form mainForm = new Form();
        mainForm.Text = "Volvo VIDA 2014D - Standalone Diagnostics";
        mainForm.Width = 1366;
        mainForm.Height = 860;
        mainForm.StartPosition = FormStartPosition.CenterScreen;
        mainForm.WindowState = FormWindowState.Maximized;

        // Top Toolbar
        Panel topBar = new Panel();
        topBar.Dock = DockStyle.Top;
        topBar.Height = 44;
        topBar.BackColor = Color.FromArgb(240, 242, 245);
        topBar.BorderStyle = BorderStyle.FixedSingle;

        Button btnReadVehicle = new Button();
        btnReadVehicle.Text = "⚡ READ VEHICLE FROM DiCE (OBD-II)";
        btnReadVehicle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        btnReadVehicle.BackColor = Color.FromArgb(0, 102, 204);
        btnReadVehicle.ForeColor = Color.White;
        btnReadVehicle.FlatStyle = FlatStyle.Flat;
        btnReadVehicle.FlatAppearance.BorderSize = 0;
        btnReadVehicle.SetBounds(10, 6, 280, 30);
        btnReadVehicle.Cursor = Cursors.Hand;
        topBar.Controls.Add(btnReadVehicle);

        Label lblInfo = new Label();
        lblInfo.Text = "Tool: DiCE-206751  |  Status: Mode II (Ignition ON)  |  Click button or 'UPDATE' below to read vehicle";
        lblInfo.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        lblInfo.ForeColor = Color.FromArgb(60, 64, 67);
        lblInfo.AutoSize = true;
        lblInfo.Location = new Point(305, 12);
        topBar.Controls.Add(lblInfo);

        Console.WriteLine("[Launcher] Initializing DiagnosticMainForm...");
        var diag = new DiagnosticMainForm();
        diag.Dock = DockStyle.Fill;

        diag.RootUrl = "http://localhost/Vida/Diagnostics/";
        diag.LogPath = Path.Combine(vidaPath, @"System\Log\Diagnostics\");
        diag.CommToolName = "DiCE-206751";
        diag.CultureName = "en-US";
        diag.UserId = "1";
        diag.TitleName = "VIDA2014D";
        diag.ApplicationTitle = "Volvo VIDA Standalone Diagnostics";
        diag.UserRole = "1";
        diag.AdminRole = "1";
        diag.PartnerGroup = "INT";
        diag.VIN = "";

        mainForm.Controls.Add(diag);
        mainForm.Controls.Add(topBar);

        Control internalReadoutBtn = null;
        object vehInfoInstance = null;

        mainForm.Load += (s, e) =>
        {
            try
            {
                Console.WriteLine("[Launcher] Calling diag.LoadControl()...");
                diag.LoadControl();
                Console.WriteLine("[Launcher] diag.LoadControl() succeeded!");

                // Enable vehicle readout in session via reflection on internal DiagnosticManager
                Type mgrType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");
                if (mgrType != null)
                {
                    object mgr = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    FieldInfo viField = mgrType.GetField("_vehicleInformationForm", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (viField != null)
                    {
                        vehInfoInstance = viField.GetValue(mgr);
                        if (vehInfoInstance != null)
                        {
                            Type viType = vehInfoInstance.GetType();
                            FieldInfo wfField = viType.GetField("_vehicleInformationWf", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (wfField != null)
                            {
                                object wf = wfField.GetValue(vehInfoInstance);
                                if (wf != null)
                                {
                                    FieldInfo sessField = wf.GetType().GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (sessField != null)
                                    {
                                        object sess = sessField.GetValue(wf);
                                        if (sess != null)
                                        {
                                            PropertyInfo allowProp = sess.GetType().GetProperty("AllowUpdate");
                                            if (allowProp != null) allowProp.SetValue(sess, true, null);
                                        }
                                    }
                                }
                            }

                            FieldInfo btnField = viType.GetField("_readOutButton", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (btnField != null)
                            {
                                internalReadoutBtn = btnField.GetValue(vehInfoInstance) as Control;
                                if (internalReadoutBtn != null)
                                {
                                    internalReadoutBtn.Enabled = true;
                                    internalReadoutBtn.EnabledChanged += (sender, ev) =>
                                    {
                                        if (!internalReadoutBtn.Enabled) internalReadoutBtn.Enabled = true;
                                    };
                                }
                            }

                            MethodInfo initButtonsMethod = viType.GetMethod("initButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (initButtonsMethod != null) initButtonsMethod.Invoke(vehInfoInstance, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Launcher] Exception during LoadControl: " + ex);
                MessageBox.Show("Error initializing Diagnostics: " + ex.Message, "VIDA Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnReadVehicle.Click += (s, e) =>
        {
            try
            {
                if (internalReadoutBtn != null)
                {
                    MethodInfo mi = internalReadoutBtn.GetType().GetMethod("PerformClick");
                    if (mi != null)
                    {
                        mi.Invoke(internalReadoutBtn, null);
                        return;
                    }
                }
                if (vehInfoInstance != null)
                {
                    MethodInfo mi = vehInfoInstance.GetType().GetMethod("PerformReadout", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mi != null)
                    {
                        mi.Invoke(vehInfoInstance, null);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Readout error: " + ex.Message, "VIDA Readout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // If run with /test flag, capture screenshot after 5 seconds and exit
        if (args.Length > 0 && args[0] == "/test")
        {
            Timer t = new Timer();
            t.Interval = 5000;
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    Console.WriteLine("[Launcher] Capturing test screenshot...");
                    Bitmap bmp = new Bitmap(mainForm.Width, mainForm.Height);
                    mainForm.DrawToBitmap(bmp, new Rectangle(0, 0, mainForm.Width, mainForm.Height));
                    string ssPath = Path.Combine(vidaPath, @"..\launcher_test.png");
                    bmp.Save(ssPath, ImageFormat.Png);
                    Console.WriteLine("[Launcher] Screenshot saved to " + ssPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Launcher] Screenshot error: " + ex.Message);
                }
                finally
                {
                    Application.Exit();
                }
            };
            t.Start();
        }

        Console.WriteLine("[Launcher] Running Application message loop...");
        Application.Run(mainForm);
    }
}
