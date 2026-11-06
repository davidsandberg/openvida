using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace OpenVidaInstaller
{
    static class Program
    {
        public static bool TestUiMode = false;

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { SetCurrentProcessExplicitAppUserModelID("OpenVIDA.Installer"); } catch { }

            bool isUninstall = Path.GetFileNameWithoutExtension(Application.ExecutablePath)
                .IndexOf("Uninstall", StringComparison.OrdinalIgnoreCase) >= 0;
            bool fromTemp = false;
            bool quietUninstall = false;
            string uninstallTarget = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--test-ui", StringComparison.OrdinalIgnoreCase))
                {
                    TestUiMode = true;
                }
                else if (string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "-u", StringComparison.OrdinalIgnoreCase))
                {
                    isUninstall = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        uninstallTarget = args[++i];
                }
                else if (string.Equals(a, "--uninstall-from-temp", StringComparison.OrdinalIgnoreCase))
                {
                    isUninstall = true;
                    fromTemp = true;
                    if (i + 1 < args.Length)
                        uninstallTarget = args[++i];
                }
                else if (string.Equals(a, "--quiet", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase))
                {
                    quietUninstall = true;
                }
            }

            if (isUninstall)
            {
                if (!string.IsNullOrEmpty(uninstallTarget))
                {
                    uninstallTarget = uninstallTarget.Trim('"', '\'').TrimEnd('\\');
                }
                RunUninstallFlow(uninstallTarget, fromTemp, quietUninstall);
                return;
            }

            Application.Run(new SetupWizardForm());
        }

        private static void RunUninstallFlow(string targetDir, bool fromTemp, bool quiet)
        {
            if (!string.IsNullOrEmpty(targetDir))
            {
                targetDir = targetDir.Trim('"', '\'').TrimEnd('\\');
            }

            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
            }
            catch { }

            if (fromTemp)
            {
                Thread.Sleep(800);
            }

            if (string.IsNullOrEmpty(targetDir))
            {
                // Check if running inside an OpenVIDA installation directory
                string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                if (Directory.Exists(Path.Combine(exeDir, "VIDA")))
                {
                    targetDir = exeDir;
                }
                else
                {
                    try
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenVIDA"))
                        {
                            if (k != null && k.GetValue("InstallLocation") != null)
                                targetDir = k.GetValue("InstallLocation").ToString();
                        }
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(targetDir))
                    targetDir = @"C:\OpenVIDA";
            }

            targetDir = Path.GetFullPath(targetDir).TrimEnd('\\');

            // Safety guard: never allow uninstalling the developer git repository if someone accidentally passes it
            if (Directory.Exists(Path.Combine(targetDir, ".git")) || Directory.Exists(Path.Combine(targetDir, "src")))
            {
                MessageBox.Show(
                    "Refusing to run uninstaller on source code repository directory:\r\n" + targetDir,
                    "OpenVIDA Uninstaller — Safety Guard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(targetDir))
            {
                if (!quiet)
                {
                    MessageBox.Show(
                        "OpenVIDA installation directory was not found at:\r\n" + targetDir,
                        "OpenVIDA Uninstaller",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }

            // If the current executable is running from inside targetDir, copy to %TEMP% and re-launch so targetDir is not locked!
            string currentExe = Path.GetFullPath(Application.ExecutablePath);
            if (!fromTemp && currentExe.StartsWith(targetDir + "\\", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string tempExe = Path.Combine(Path.GetTempPath(), "OpenVidaUninstall_Runner.exe");
                    File.Copy(currentExe, tempExe, true);
                    string qArg = quiet ? " --quiet" : "";
                    ProcessStartInfo psiRunner = new ProcessStartInfo(tempExe, string.Format("--uninstall-from-temp \"{0}\"{1}", targetDir, qArg))
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetTempPath()
                    };
                    Process.Start(psiRunner);
                    Environment.Exit(0);
                    return;
                }
                catch { }
            }

            if (!quiet)
            {
                DialogResult dr = MessageBox.Show(
                    "Are you sure you want to completely uninstall OpenVIDA Diagnostics from:\r\n\r\n" +
                    "    " + targetDir + "\r\n\r\n" +
                    "This will:\r\n" +
                    "  • Stop any running OpenVIDA & JBoss background processes\r\n" +
                    "  • Safely detach SQL Server databases mounted from " + targetDir + "\\VIDA\\db\r\n" +
                    "  • Remove Desktop & Start Menu shortcuts and registry entries\r\n" +
                    "  • Permanently delete " + targetDir + " and all extracted files",
                    "Uninstall OpenVIDA Diagnostics",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes)
                    return;
            }

            using (Form progressForm = new Form())
            {
                progressForm.Text = "Uninstalling OpenVIDA Diagnostics...";
                progressForm.Size = new Size(540, 260);
                progressForm.StartPosition = FormStartPosition.CenterScreen;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.ControlBox = false;
                progressForm.BackColor = Color.FromArgb(245, 247, 250);
                progressForm.Font = new Font("Segoe UI", 9.25f);

                Label lblStatus = new Label
                {
                    Text = "Preparing to uninstall OpenVIDA...",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59),
                    Location = new Point(20, 18),
                    Size = new Size(485, 24)
                };

                ProgressBar pb = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 25,
                    Location = new Point(20, 48),
                    Size = new Size(485, 22)
                };

                TextBox txtLog = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.FromArgb(15, 23, 42),
                    ForeColor = Color.FromArgb(226, 232, 240),
                    Font = new Font("Consolas", 8.5f),
                    Location = new Point(20, 80),
                    Size = new Size(485, 125)
                };

                progressForm.Controls.AddRange(new Control[] { lblStatus, pb, txtLog });

                Action<string> log = (msg) =>
                {
                    try
                    {
                        progressForm.Invoke((MethodInvoker)delegate
                        {
                            lblStatus.Text = msg;
                            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
                        });
                    }
                    catch { }
                };

                string uninstallError = null;
                progressForm.Shown += (s, e) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            PerformUninstallSteps(targetDir, log);
                        }
                        catch (Exception ex)
                        {
                            uninstallError = ex.Message;
                        }
                        finally
                        {
                            try { progressForm.Invoke((MethodInvoker)delegate { progressForm.Close(); }); } catch { }
                        }
                    });
                };

                if (quiet)
                {
                    PerformUninstallSteps(targetDir, (m) => { });
                }
                else
                {
                    progressForm.ShowDialog();
                }

                if (!quiet)
                {
                    if (uninstallError != null)
                    {
                        MessageBox.Show(
                            "Uninstall encountered an issue:\r\n" + uninstallError,
                            "OpenVIDA Uninstaller",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            "OpenVIDA Diagnostics has been completely uninstalled from:\r\n" + targetDir,
                            "Uninstall Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
        }

        private static void PerformUninstallSteps(string targetDir, Action<string> log)
        {
            // 1. Stop VidaLauncher, VidaMcp, and JBoss java.exe/cmd.exe running from targetDir
            log("Step 1/4: Stopping OpenVIDA and JBoss background processes...");
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == Process.GetCurrentProcess().Id) continue;
                        string pName = p.ProcessName;
                        if (string.Equals(pName, "VidaLauncher", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "VidaMcp", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "OpenVidaUninstall", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "OpenVidaUninstall_Runner", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "Uninstall", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "OpenVidaSetup", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pName, "java", StringComparison.OrdinalIgnoreCase))
                        {
                            bool match = true;
                            try
                            {
                                string modPath = p.MainModule.FileName;
                                if (!string.IsNullOrEmpty(modPath) &&
                                    !modPath.StartsWith(targetDir + "\\", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(pName, "java", StringComparison.OrdinalIgnoreCase))
                                {
                                    match = false;
                                }
                            }
                            catch { }

                            if (match)
                            {
                                p.Kill();
                                p.WaitForExit(4000);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }

                // Also terminate any cmd.exe wrapper running jboss.bat
                ProcessStartInfo psiWmic = new ProcessStartInfo("wmic.exe", "process where \"name='cmd.exe' and commandline like '%jboss%'\" call terminate")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process pw = Process.Start(psiWmic))
                {
                    pw.WaitForExit(5000);
                }
                Thread.Sleep(800);
            }
            catch { }

            // 2. Detach any SQL Server databases whose physical .mdf file resides inside targetDir
            log("Step 2/4: Checking SQL Server databases in " + targetDir + "\\VIDA\\db...");
            try
            {
                string connStr = @"Server=localhost\SQLEXPRESS;Database=master;Integrated Security=True;Connection Timeout=8";
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    List<string> dbsToDetach = new List<string>();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT d.name, mf.physical_name
FROM sys.databases d
JOIN sys.master_files mf ON d.database_id = mf.database_id AND mf.file_id = 1";
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string dbName = rdr.GetString(0);
                                string phys = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                                if (phys.StartsWith(targetDir + "\\", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbsToDetach.Add(dbName);
                                }
                            }
                        }
                    }

                    foreach (string db in dbsToDetach)
                    {
                        log("Detaching SQL database [" + db + "]...");
                        try
                        {
                            using (SqlCommand detCmd = conn.CreateCommand())
                            {
                                detCmd.CommandTimeout = 30;
                                detCmd.CommandText = string.Format(
                                    "ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; EXEC sp_detach_db '{0}', 'true';",
                                    db);
                                detCmd.ExecuteNonQuery();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 3. Remove Desktop & Start Menu shortcuts and Registry entries
            log("Step 3/4: Removing shortcuts and registry entries...");
            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OpenVIDA Diagnostics.lnk");
                string programsLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OpenVIDA Diagnostics.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
                if (File.Exists(programsLnk)) File.Delete(programsLnk);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenVIDA", false);
            }
            catch { }

            try
            {
                string clsidKeyPath = @"Software\Classes\CLSID\{865B2280-2B71-11D1-BC01-006097AC382A}";
                string devDll = @"C:\Users\david\git\openvida\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\DummyIsoView.dll";
                if (File.Exists(devDll))
                {
                    using (RegistryKey ipsKey = Registry.CurrentUser.CreateSubKey(clsidKeyPath + @"\InprocServer32"))
                    {
                        ipsKey.SetValue("CodeBase", new Uri(devDll).AbsoluteUri);
                    }
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(clsidKeyPath, false);
                }
            }
            catch { }

            // 4. Delete targetDir (with retry loop while OS releases handles)
            log("Step 4/4: Removing directory " + targetDir + " and all files...");
            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
            }
            catch { }

            // Clear read-only and hidden attributes
            try
            {
                if (Directory.Exists(targetDir))
                {
                    File.SetAttributes(targetDir, FileAttributes.Normal);
                    foreach (string file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    }
                }
            }
            catch { }

            for (int attempt = 1; attempt <= 10 && Directory.Exists(targetDir); attempt++)
            {
                try
                {
                    ProcessStartInfo psiRd = new ProcessStartInfo("cmd.exe", string.Format("/c rd /s /q \"{0}\"", targetDir))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetTempPath()
                    };
                    using (Process pRd = Process.Start(psiRd))
                    {
                        pRd.WaitForExit(15000);
                    }
                }
                catch { }

                if (Directory.Exists(targetDir))
                {
                    try
                    {
                        Directory.Delete(targetDir, true);
                    }
                    catch { }
                }

                if (Directory.Exists(targetDir))
                {
                    Thread.Sleep(600);
                }
            }

            // If targetDir STILL exists (e.g. held briefly by open File Explorer window or antivirus scanner),
            // launch a detached background script that retries removal once handles are completely released.
            if (Directory.Exists(targetDir))
            {
                try
                {
                    string cleanupBat = Path.Combine(Path.GetTempPath(), "openvida_uninstall_cleanup.bat");
                    File.WriteAllText(cleanupBat, string.Format(
                        "@echo off\r\n" +
                        ":retry\r\n" +
                        "ping 127.0.0.1 -n 2 > nul\r\n" +
                        "rd /s /q \"{0}\" > nul 2>&1\r\n" +
                        "if exist \"{0}\" goto retry\r\n" +
                        "del \"%~f0\" & exit\r\n", targetDir));
                    ProcessStartInfo psiClean = new ProcessStartInfo("cmd.exe", "/c \"" + cleanupBat + "\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetTempPath()
                    };
                    Process.Start(psiClean);
                }
                catch { }
            }
        }
    }

    public class SetupWizardForm : Form
    {
        private int _currentStep = 0;
        private Panel _headerPanel;
        private Label _lblStepTitle;
        private Label _lblStepSubtitle;
        private Panel _bodyContainer;
        private Panel _footerPanel;
        private Button _btnBack;
        private Button _btnNext;
        private Button _btnCancel;

        private Panel[] _pages;

        // Persistent Log File Paths
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        private string _tempLogFilePath;
        private string _targetLogFilePath;
        private readonly object _logLock = new object();

        // Step 1 controls (Prerequisites)
        private Label _lblDotNetStatus;
        private Label _lblWebViewStatus;
        private Label _lblSqlStatus;
        private Label _lblAdminStatus;
        private Button _btnDownloadSql;
        private Button _btnDownloadWebView;
        private Button _btnRefreshPrereq;

        // Step 2 controls (Paths & Data Sources)
        private TextBox _txtTargetDir;
        private TextBox _txtVidaSource;
        private TextBox _txtEwdSource;
        private CheckBox _chkInstallDiceDriverAndReg;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private Label _lblSourceValidation;

        // Step 3 controls (Installation Progress)
        private ProgressBar _progressBar;
        private Label _lblProgressPhase;
        private Button _btnOpenLogStep3;
        private TextBox _txtInstallLog;
        private bool _isInstalling = false;
        private bool _installSucceeded = false;

        // Step 4 controls (Finish)
        private CheckBox _chkLaunchNow;
        private Label _lblFinishSummary;
        private Button _btnOpenLogStep4;

        public SetupWizardForm()
        {
            _tempLogFilePath = Path.Combine(Path.GetTempPath(), "OpenVidaSetup.log");
            InitializeLogFile();

            this.Text = "OpenVIDA Setup " + AppVersion.Tag + " — Standalone Diagnostic Suite";
            this.Size = new Size(760, 560);
            this.MinimumSize = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.25f);

            this.ShowIcon = true;
            this.HandleCreated += (s, e) =>
            {
                try
                {
                    if (this.Icon != null)
                    {
                        SendMessage(this.Handle, 0x0080, 1, this.Icon.Handle);
                        SendMessage(this.Handle, 0x0080, 0, this.Icon.Handle);
                    }
                }
                catch { }
            };
            TryLoadEmbeddedIcon();
            BuildLayout();
            ShowStep(0);

            if (Program.TestUiMode)
            {
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 1200;
                int tick = 0;
                t.Tick += (s, e) =>
                {
                    tick++;
                    if (tick == 1)
                    {
                        CaptureScreenshot("installer_step1_prereq.png");
                        ShowStep(1);
                    }
                    else if (tick == 2)
                    {
                        CaptureScreenshot("installer_step2_sources.png");
                        t.Stop();
                        this.Close();
                    }
                };
                t.Start();
            }
        }

        private void InitializeLogFile()
        {
            try
            {
                string header = string.Format(
                    "============================================================\r\n" +
                    "  OpenVIDA Setup Log — Started {0:yyyy-MM-dd HH:mm:ss}\r\n" +
                    "  OS: {1} ({2}-bit)\r\n" +
                    "  User: {3} (Admin: {4})\r\n" +
                    "============================================================\r\n",
                    DateTime.Now,
                    Environment.OSVersion,
                    Environment.Is64BitOperatingSystem ? "64" : "32",
                    Environment.UserName,
                    IsRunningAsAdministrator());
                File.WriteAllText(_tempLogFilePath, header, Encoding.UTF8);
            }
            catch { }
        }

        private string GetActiveLogPath()
        {
            if (!string.IsNullOrEmpty(_targetLogFilePath) && File.Exists(_targetLogFilePath))
                return _targetLogFilePath;
            return _tempLogFilePath;
        }

        private void OpenSetupLogFile()
        {
            try
            {
                string logPath = GetActiveLogPath();
                if (File.Exists(logPath))
                {
                    Process.Start("notepad.exe", "\"" + logPath + "\"");
                }
                else
                {
                    MessageBox.Show("Log file not found at: " + logPath, "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open log file: " + ex.Message, "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CaptureScreenshot(string fileName)
        {
            try
            {
                this.Refresh();
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(this.Width, this.Height))
                {
                    this.DrawToBitmap(bmp, new Rectangle(0, 0, this.Width, this.Height));
                    string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch { }
        }

        private void TryLoadEmbeddedIcon()
        {
            try
            {
                string localIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"assets\vida_icon.ico");
                if (!File.Exists(localIcon))
                    localIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
                if (File.Exists(localIcon))
                {
                    this.Icon = new Icon(localIcon);
                }
                else
                {
                    Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (exeIcon != null) this.Icon = exeIcon;
                }
            }
            catch { }
        }

        private void BuildLayout()
        {
            // Top Dark Nordic Banner
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            _headerPanel.Paint += (s, e) =>
            {
                using (LinearGradientBrush br = new LinearGradientBrush(
                    _headerPanel.ClientRectangle,
                    Color.FromArgb(15, 23, 42),
                    Color.FromArgb(26, 46, 77),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(br, _headerPanel.ClientRectangle);
                }
                using (Pen cyanPen = new Pen(Color.FromArgb(0, 194, 255), 3f))
                {
                    e.Graphics.DrawLine(cyanPen, 0, _headerPanel.Height - 2, _headerPanel.Width, _headerPanel.Height - 2);
                }
            };

            Label lblBrandBadge = new Label
            {
                Text = "⚡",
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 255),
                BackColor = Color.Transparent,
                Location = new Point(20, 16),
                Size = new Size(48, 48),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblStepTitle = new Label
            {
                Text = "Welcome to OpenVIDA Setup",
                Font = new Font("Segoe UI", 14.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(74, 15),
                AutoSize = true,
                UseMnemonic = false
            };

            _lblStepSubtitle = new Label
            {
                Text = "Native Windows 10/11 Volvo Diagnostic, Repair & Wiring Suite",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(175, 195, 220),
                BackColor = Color.Transparent,
                Location = new Point(76, 46),
                AutoSize = true,
                UseMnemonic = false
            };

            _headerPanel.Controls.Add(lblBrandBadge);
            _headerPanel.Controls.Add(_lblStepTitle);
            _headerPanel.Controls.Add(_lblStepSubtitle);

            // Bottom Navigation Bar
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Color.FromArgb(235, 239, 245)
            };
            _footerPanel.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(205, 213, 224), 1f))
                {
                    e.Graphics.DrawLine(borderPen, 0, 0, _footerPanel.Width, 0);
                }
            };

            Label lblVersion = new Label
            {
                Text = AppVersion.DisplayName + " • Native Standalone",
                ForeColor = Color.FromArgb(110, 125, 145),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 21),
                AutoSize = true
            };

            _btnBack = new Button
            {
                Text = "← Back",
                Size = new Size(100, 34),
                Location = new Point(405, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 50, 65),
                Cursor = Cursors.Hand
            };
            _btnBack.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 212);
            _btnBack.Click += (s, e) => { if (_currentStep > 0 && !_isInstalling) ShowStep(_currentStep - 1); };

            _btnNext = new Button
            {
                Text = "Next →",
                Size = new Size(125, 34),
                Location = new Point(515, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += OnNextClicked;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 34),
                Location = new Point(650, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 90, 105),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 212);
            _btnCancel.Click += (s, e) =>
            {
                if (_isInstalling)
                {
                    MessageBox.Show("Installation is currently in progress. Please wait for the current operation to complete.", "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                this.Close();
            };

            _footerPanel.Controls.AddRange(new Control[] { lblVersion, _btnBack, _btnNext, _btnCancel });

            // Main Body Container
            _bodyContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 18, 24, 18),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            _pages = new Panel[]
            {
                BuildStep1PrerequisitesPage(),
                BuildStep2SourcesPage(),
                BuildStep3ProgressPage(),
                BuildStep4FinishPage()
            };

            foreach (Panel p in _pages)
            {
                p.Dock = DockStyle.Fill;
                p.Visible = false;
                _bodyContainer.Controls.Add(p);
            }

            this.Controls.Add(_bodyContainer);
            this.Controls.Add(_footerPanel);
            this.Controls.Add(_headerPanel);
        }

        private Panel BuildStep1PrerequisitesPage()
        {
            Panel page = new Panel { BackColor = Color.FromArgb(245, 247, 250) };

            Label lblIntro = new Label
            {
                Text = "This wizard installs OpenVIDA natively on Windows 10/11 without requiring a virtual machine or Internet Explorer.\nBefore proceeding, let's verify your system environment:",
                Location = new Point(4, 2),
                Size = new Size(690, 42),
                ForeColor = Color.FromArgb(40, 50, 65)
            };
            page.Controls.Add(lblIntro);

            // Card container for checks
            Panel card = new Panel
            {
                Location = new Point(4, 52),
                Size = new Size(696, 260),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _lblDotNetStatus = CreateCheckRow(card, 16, ".NET Framework 4.x Runtime", "Checking...");
            _lblWebViewStatus = CreateCheckRow(card, 74, "Microsoft Edge WebView2 Runtime (for EWD PDF Schematics)", "Checking...");
            _btnDownloadWebView = new Button
            {
                Text = "Get WebView2",
                Location = new Point(560, 74),
                Size = new Size(118, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(235, 242, 250),
                ForeColor = Color.FromArgb(0, 102, 204),
                Visible = false
            };
            _btnDownloadWebView.Click += (s, e) => Process.Start("https://developer.microsoft.com/en-us/microsoft-edge/webview2/");
            card.Controls.Add(_btnDownloadWebView);

            _lblSqlStatus = CreateCheckRow(card, 132, "Microsoft SQL Server Express (localhost\\SQLEXPRESS)", "Checking...");
            _btnDownloadSql = new Button
            {
                Text = "Get SQL Express",
                Location = new Point(560, 132),
                Size = new Size(118, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(235, 242, 250),
                ForeColor = Color.FromArgb(0, 102, 204),
                Visible = false
            };
            _btnDownloadSql.Click += (s, e) => Process.Start("https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
            card.Controls.Add(_btnDownloadSql);

            _lblAdminStatus = CreateCheckRow(card, 190, "Administrator Privileges (for DiCE Driver, SQL TCP/IP & J2534 Registry)", "Checking...");

            page.Controls.Add(card);

            _btnRefreshPrereq = new Button
            {
                Text = "🔄 Re-check Prerequisites",
                Location = new Point(4, 326),
                Size = new Size(185, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 50, 65),
                Cursor = Cursors.Hand
            };
            _btnRefreshPrereq.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 212);
            _btnRefreshPrereq.Click += (s, e) => RunPrerequisiteChecks();
            page.Controls.Add(_btnRefreshPrereq);

            Label lblNote = new Label
            {
                Text = "Tip: OpenVIDA uses a 'Bring Your Own Data' setup. On the next screen, point the installer to your VIDA 2014D data source (archive, folder, or VM image) and optionally the EWD ISO.",
                Location = new Point(205, 322),
                Size = new Size(495, 52),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 105, 125),
                UseMnemonic = false
            };
            page.Controls.Add(lblNote);

            return page;
        }

        private Label CreateCheckRow(Panel parent, int top, string title, string initialStatus)
        {
            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(18, top),
                AutoSize = true,
                UseMnemonic = false
            };
            Label lblStatus = new Label
            {
                Text = initialStatus,
                Font = new Font("Segoe UI", 9.0f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(18, top + 22),
                Size = new Size(530, 22),
                UseMnemonic = false
            };
            parent.Controls.Add(lblTitle);
            parent.Controls.Add(lblStatus);
            return lblStatus;
        }

        private void RunPrerequisiteChecks()
        {
            // 1. .NET 4.x
            _lblDotNetStatus.Text = "✅ Detected (.NET CLR " + Environment.Version.ToString() + ")";
            _lblDotNetStatus.ForeColor = Color.FromArgb(22, 101, 52);

            // 2. WebView2 Runtime
            bool hasWebView = CheckWebView2Installed();
            if (hasWebView)
            {
                _lblWebViewStatus.Text = "✅ Microsoft Edge WebView2 Runtime is installed.";
                _lblWebViewStatus.ForeColor = Color.FromArgb(22, 101, 52);
                _btnDownloadWebView.Visible = false;
            }
            else
            {
                _lblWebViewStatus.Text = "⚠️ WebView2 Runtime not found (recommended for EWD PDF viewer).";
                _lblWebViewStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _btnDownloadWebView.Visible = true;
            }

            // 3. SQL Server Express
            string sqlState = CheckSqlExpressState();
            if (sqlState == "RUNNING")
            {
                _lblSqlStatus.Text = "✅ SQL Server service (MSSQL$SQLEXPRESS) is installed and running.";
                _lblSqlStatus.ForeColor = Color.FromArgb(22, 101, 52);
                _btnDownloadSql.Visible = false;
            }
            else if (sqlState == "STOPPED")
            {
                _lblSqlStatus.Text = "⚠️ SQL Server Express is installed but stopped (Installer will start it).";
                _lblSqlStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _btnDownloadSql.Visible = false;
            }
            else
            {
                _lblSqlStatus.Text = "❌ SQL Server Express (SQLEXPRESS) not detected. Install SQL Express first.";
                _lblSqlStatus.ForeColor = Color.FromArgb(185, 28, 28);
                _btnDownloadSql.Visible = true;
            }

            // 4. Admin status
            bool isAdmin = IsRunningAsAdministrator();
            if (isAdmin)
            {
                _lblAdminStatus.Text = "✅ Running with Administrator privileges.";
                _lblAdminStatus.ForeColor = Color.FromArgb(22, 101, 52);
            }
            else
            {
                _lblAdminStatus.Text = "ℹ️ Running as Standard User (Run as Admin to install 64-bit DiCE USB driver).";
                _lblAdminStatus.ForeColor = Color.FromArgb(71, 85, 105);
            }
        }

        private Panel BuildStep2SourcesPage()
        {
            Panel page = new Panel { BackColor = Color.FromArgb(245, 247, 250) };

            // 1. Target Installation Directory
            Label lblTarget = new Label
            {
                Text = "1. Target Installation Directory (where OpenVIDA will be installed):",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(4, 2),
                AutoSize = true,
                UseMnemonic = false
            };
            _txtTargetDir = new TextBox
            {
                Text = @"C:\OpenVIDA",
                Location = new Point(4, 25),
                Size = new Size(580, 26),
                Font = new Font("Segoe UI", 9.5f)
            };
            Button btnBrowseTarget = CreateBrowseButton(592, 24, "Browse...");
            btnBrowseTarget.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Target Folder for OpenVIDA Installation";
                    fbd.SelectedPath = _txtTargetDir.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                        _txtTargetDir.Text = fbd.SelectedPath;
                }
            };

            // 2. VIDA 2014D Prerequisite Source
            Label lblVidaSrc = new Label
            {
                Text = "2. VIDA 2014D Data Source (Required — .7z/.zip archive, Win7-x86-Vida.ova/.vmdk, or VIDA folder):",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(4, 66),
                AutoSize = true,
                UseMnemonic = false
            };
            _txtVidaSource = new TextBox
            {
                Text = AutoDetectDefaultVidaSource(),
                Location = new Point(4, 89),
                Size = new Size(495, 26),
                Font = new Font("Segoe UI", 9.5f)
            };
            _txtVidaSource.TextChanged += (s, e) => ValidateSourcesUI();

            Button btnBrowseVidaFile = CreateBrowseButton(506, 88, "📄 File...");
            btnBrowseVidaFile.Width = 92;
            btnBrowseVidaFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Select VIDA 2014D Data Archive or VirtualBox Appliance";
                    ofd.Filter = "VIDA Archives & VM Images (*.7z;*.zip;*.ova;*.vmdk;*.iso;*.rar)|*.7z;*.zip;*.ova;*.vmdk;*.iso;*.rar|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                        _txtVidaSource.Text = ofd.FileName;
                }
            };

            Button btnBrowseVidaFolder = CreateBrowseButton(604, 88, "📁 Folder...");
            btnBrowseVidaFolder.Width = 96;
            btnBrowseVidaFolder.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Existing VIDA Folder (containing db, jboss, jdk)";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        _txtVidaSource.Text = fbd.SelectedPath;
                }
            };

            // 3. Electronic Wiring Diagrams Source (Optional)
            Label lblEwdSrc = new Label
            {
                Text = "3. Electronic Wiring Diagrams (Optional — Volvo_EWD_2014D.iso, .part1.rar, or US_Eng folder):",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(4, 130),
                AutoSize = true,
                UseMnemonic = false
            };
            _txtEwdSource = new TextBox
            {
                Text = AutoDetectDefaultEwdSource(),
                Location = new Point(4, 153),
                Size = new Size(495, 26),
                Font = new Font("Segoe UI", 9.5f)
            };
            _txtEwdSource.TextChanged += (s, e) => ValidateSourcesUI();

            Button btnBrowseEwdFile = CreateBrowseButton(506, 152, "📄 ISO/RAR...");
            btnBrowseEwdFile.Width = 92;
            btnBrowseEwdFile.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Select Volvo EWD 2014D ISO or Archive";
                    ofd.Filter = "EWD ISO & Archives (*.iso;*.rar;*.7z;*.zip)|*.iso;*.rar;*.7z;*.zip|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                        _txtEwdSource.Text = ofd.FileName;
                }
            };

            Button btnBrowseEwdFolder = CreateBrowseButton(604, 152, "📁 Folder...");
            btnBrowseEwdFolder.Width = 96;
            btnBrowseEwdFolder.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Extracted EWD Folder (US_Eng)";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        _txtEwdSource.Text = fbd.SelectedPath;
                }
            };

            // Validation Summary Box
            _lblSourceValidation = new Label
            {
                Location = new Point(4, 190),
                Size = new Size(696, 42),
                BackColor = Color.FromArgb(238, 244, 252),
                ForeColor = Color.FromArgb(30, 58, 138),
                Padding = new Padding(10, 6, 10, 6),
                Font = new Font("Segoe UI", 8.75f),
                Text = "Select your VIDA data source above.",
                UseMnemonic = false
            };

            // Options Group
            GroupBox grpOptions = new GroupBox
            {
                Text = "Installation && Configuration Options",
                Location = new Point(4, 240),
                Size = new Size(696, 128),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            _chkInstallDiceDriverAndReg = new CheckBox
            {
                Text = "Install Volvo DiCE 64-bit USB Driver & J2534 Interface (uncheck to use another J2534 device)",
                Checked = true,
                Location = new Point(16, 26),
                Size = new Size(665, 24),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                UseMnemonic = false
            };

            _chkDesktopShortcut = new CheckBox
            {
                Text = "Create Desktop Shortcut (OpenVIDA Diagnostics)",
                Checked = true,
                Location = new Point(16, 56),
                Size = new Size(650, 24),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                UseMnemonic = false
            };

            _chkStartMenuShortcut = new CheckBox
            {
                Text = "Create Start Menu Program Shortcut",
                Checked = true,
                Location = new Point(16, 86),
                Size = new Size(650, 24),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                UseMnemonic = false
            };

            grpOptions.Controls.AddRange(new Control[] { _chkInstallDiceDriverAndReg, _chkDesktopShortcut, _chkStartMenuShortcut });

            page.Controls.AddRange(new Control[]
            {
                lblTarget, _txtTargetDir, btnBrowseTarget,
                lblVidaSrc, _txtVidaSource, btnBrowseVidaFile, btnBrowseVidaFolder,
                lblEwdSrc, _txtEwdSource, btnBrowseEwdFile, btnBrowseEwdFolder,
                _lblSourceValidation, grpOptions
            });

            ValidateSourcesUI();
            return page;
        }

        private Button CreateBrowseButton(int x, int y, string text)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(108, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Segoe UI", 8.75f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(185, 195, 210);
            return btn;
        }

        private string AutoDetectDefaultVidaSource()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "OpenVIDA_Data_2014D.7z"),
                Path.Combine(baseDir, @"..\OpenVIDA_Data_2014D.7z"),
                Path.Combine(baseDir, @"..\openvida-install\OpenVIDA_Data_2014D.7z"),
                Path.Combine(baseDir, "Win7-x86-Vida.ova"),
                Path.Combine(baseDir, @"..\openvida-install\Win7-x86-Vida.ova"),
                Path.Combine(userProfile, @"git\openvida-install\Win7-x86-Vida.ova"),
                Path.Combine(userProfile, @"git\openvida-install\OpenVIDA_Data_2014D.7z"),
                Path.Combine(baseDir, "VIDA")
            };
            foreach (string c in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(c);
                    if (File.Exists(full) || Directory.Exists(full))
                        return full;
                }
                catch { }
            }
            return "";
        }

        private string AutoDetectDefaultEwdSource()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string userDownloads = Path.Combine(userProfile, "Downloads");
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "Volvo_EWD_2014D.iso"),
                Path.Combine(baseDir, @"..\openvida-install\Volvo_EWD_2014D.iso"),
                Path.Combine(userProfile, @"git\openvida-install\Volvo_EWD_2014D.iso"),
                Path.Combine(baseDir, @"ewd\Volvo_EWD_2014D.iso"),
                Path.Combine(userDownloads, "Volvo_EWD_2014D.iso"),
                Path.Combine(userDownloads, "Volvo_EWD_2014D.part1.rar"),
                Path.Combine(baseDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\wiring_diagrams\US_Eng")
            };
            foreach (string c in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(c);
                    if (File.Exists(full) || Directory.Exists(full))
                        return full;
                }
                catch { }
            }
            return "";
        }

        private void ValidateSourcesUI()
        {
            if (_lblSourceValidation == null) return;

            string vidaSrc = (_txtVidaSource.Text ?? "").Trim();
            string ewdSrc = (_txtEwdSource.Text ?? "").Trim();

            bool vidaOk = File.Exists(vidaSrc) || Directory.Exists(vidaSrc);
            bool ewdProvided = !string.IsNullOrEmpty(ewdSrc);
            bool ewdOk = !ewdProvided || File.Exists(ewdSrc) || Directory.Exists(ewdSrc);

            if (!vidaOk)
            {
                _lblSourceValidation.Text = "⚠️ Please select a valid VIDA 2014D source file (.7z, .zip, .ova, .vmdk) or existing VIDA directory.";
                _lblSourceValidation.BackColor = Color.FromArgb(254, 243, 199);
                _lblSourceValidation.ForeColor = Color.FromArgb(146, 64, 14);
            }
            else if (!ewdOk)
            {
                _lblSourceValidation.Text = "⚠️ The specified EWD path does not exist. Clear the field to skip EWD or select a valid ISO/folder.";
                _lblSourceValidation.BackColor = Color.FromArgb(254, 243, 199);
                _lblSourceValidation.ForeColor = Color.FromArgb(146, 64, 14);
            }
            else
            {
                string ewdMsg = ewdProvided ? "EWD wiring schematics will also be installed and indexed." : "EWD wiring schematics skipped (Wiring Diagrams tab will be greyed out).";
                _lblSourceValidation.Text = "✅ VIDA source verified. " + ewdMsg;
                _lblSourceValidation.BackColor = Color.FromArgb(220, 252, 231);
                _lblSourceValidation.ForeColor = Color.FromArgb(22, 101, 52);
            }
        }

        private Panel BuildStep3ProgressPage()
        {
            Panel page = new Panel { BackColor = Color.FromArgb(245, 247, 250) };

            _lblProgressPhase = new Label
            {
                Text = "Preparing installation...",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(4, 4),
                Size = new Size(540, 26),
                UseMnemonic = false
            };

            _btnOpenLogStep3 = new Button
            {
                Text = "📄 Open Setup Log",
                Location = new Point(556, 2),
                Size = new Size(144, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            _btnOpenLogStep3.FlatAppearance.BorderColor = Color.FromArgb(185, 195, 210);
            _btnOpenLogStep3.Click += (s, e) => OpenSetupLogFile();

            _progressBar = new ProgressBar
            {
                Location = new Point(4, 34),
                Size = new Size(696, 26),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            _txtInstallLog = new TextBox
            {
                Location = new Point(4, 72),
                Size = new Size(696, 295),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9.0f),
                BorderStyle = BorderStyle.FixedSingle
            };

            page.Controls.AddRange(new Control[] { _lblProgressPhase, _btnOpenLogStep3, _progressBar, _txtInstallLog });
            return page;
        }

        private Panel BuildStep4FinishPage()
        {
            Panel page = new Panel { BackColor = Color.FromArgb(245, 247, 250) };

            Panel card = new Panel
            {
                Location = new Point(4, 10),
                Size = new Size(696, 315),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblDoneTitle = new Label
            {
                Text = "🎉 OpenVIDA Has Been Installed Successfully!",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 101, 52),
                Location = new Point(24, 20),
                AutoSize = true,
                UseMnemonic = false
            };

            _lblFinishSummary = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(24, 58),
                Size = new Size(648, 195),
                UseMnemonic = false
            };

            _chkLaunchNow = new CheckBox
            {
                Text = "Launch OpenVIDA Diagnostics now",
                Checked = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Location = new Point(26, 264),
                Size = new Size(340, 28),
                UseMnemonic = false
            };

            _btnOpenLogStep4 = new Button
            {
                Text = "📄 View Full Setup Log",
                Location = new Point(512, 262),
                Size = new Size(160, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 244, 248),
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Segoe UI", 8.75f),
                Cursor = Cursors.Hand
            };
            _btnOpenLogStep4.FlatAppearance.BorderColor = Color.FromArgb(195, 205, 215);
            _btnOpenLogStep4.Click += (s, e) => OpenSetupLogFile();

            card.Controls.AddRange(new Control[] { lblDoneTitle, _lblFinishSummary, _chkLaunchNow, _btnOpenLogStep4 });
            page.Controls.Add(card);
            return page;
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = (i == step);

            if (step == 0)
            {
                _lblStepTitle.Text = "Step 1 of 4 — System Prerequisites";
                _lblStepSubtitle.Text = "Verify SQL Server Express, .NET Framework, and WebView2 Runtime";
                _btnBack.Enabled = false;
                _btnNext.Text = "Next →";
                _btnNext.Enabled = true;
                RunPrerequisiteChecks();
            }
            else if (step == 1)
            {
                _lblStepTitle.Text = "Step 2 of 4 — Target Folder & Data Sources";
                _lblStepSubtitle.Text = "Select installation folder, VIDA 2014D data source, and optional EWD schematics";
                _btnBack.Enabled = true;
                _btnNext.Text = "Install Now ⚡";
                _btnNext.Enabled = true;
                ValidateSourcesUI();
            }
            else if (step == 2)
            {
                _lblStepTitle.Text = "Step 3 of 4 — Installing OpenVIDA";
                _lblStepSubtitle.Text = "Extracting archives, deploying OpenVIDA binaries, and configuring SQL databases";
                _btnBack.Enabled = false;
                _btnNext.Enabled = false;
            }
            else if (step == 3)
            {
                _lblStepTitle.Text = "Step 4 of 4 — Setup Complete";
                _lblStepSubtitle.Text = "OpenVIDA is ready to diagnose your Volvo";
                _btnBack.Enabled = false;
                _btnCancel.Visible = false;
                _btnNext.Text = "Finish";
                _btnNext.Enabled = true;
            }
        }

        private void OnNextClicked(object sender, EventArgs e)
        {
            if (_currentStep == 0)
            {
                ShowStep(1);
            }
            else if (_currentStep == 1)
            {
                string targetDir = (_txtTargetDir.Text ?? "").Trim();
                string vidaSrc = (_txtVidaSource.Text ?? "").Trim();
                string ewdSrc = (_txtEwdSource.Text ?? "").Trim();

                if (string.IsNullOrEmpty(targetDir))
                {
                    MessageBox.Show("Please specify a target installation directory.", "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(vidaSrc) || (!File.Exists(vidaSrc) && !Directory.Exists(vidaSrc)))
                {
                    MessageBox.Show("Please select a valid VIDA 2014D data source (archive, .ova/.vmdk, or folder).", "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!string.IsNullOrEmpty(ewdSrc) && !File.Exists(ewdSrc) && !Directory.Exists(ewdSrc))
                {
                    MessageBox.Show("The specified Electronic Wiring Diagrams path does not exist. Clear the field to skip EWD, or choose a valid file/folder.", "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShowStep(2);
                StartInstallationAsync(targetDir, vidaSrc, ewdSrc, _chkInstallDiceDriverAndReg.Checked, _chkDesktopShortcut.Checked, _chkStartMenuShortcut.Checked);
            }
            else if (_currentStep == 2 && _installSucceeded)
            {
                ShowStep(3);
            }
            else if (_currentStep == 3)
            {
                if (_chkLaunchNow.Checked)
                {
                    try
                    {
                        string targetDir = (_txtTargetDir.Text ?? "").Trim();
                        string launcherExe = Path.Combine(targetDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\VidaLauncher.exe");
                        if (File.Exists(launcherExe))
                        {
                            ProcessStartInfo psi = new ProcessStartInfo(launcherExe);
                            psi.WorkingDirectory = Path.GetDirectoryName(launcherExe);
                            Process.Start(psi);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not launch OpenVIDA automatically: " + ex.Message, "OpenVIDA Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                this.Close();
            }
        }

        private void AppendLog(string message)
        {
            string line = string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, message);
            lock (_logLock)
            {
                try { File.AppendAllText(_tempLogFilePath, line, Encoding.UTF8); } catch { }
                try
                {
                    if (!string.IsNullOrEmpty(_targetLogFilePath))
                        File.AppendAllText(_targetLogFilePath, line, Encoding.UTF8);
                }
                catch { }
            }

            if (this.IsHandleCreated)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>((l) => { _txtInstallLog.AppendText(l); }), line);
                }
                else
                {
                    _txtInstallLog.AppendText(line);
                }
            }
        }

        private void UpdateProgress(int percent, string phase)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<int, string>(UpdateProgress), percent, phase);
                return;
            }
            _progressBar.Value = Math.Max(0, Math.Min(100, percent));
            if (!string.IsNullOrEmpty(phase))
                _lblProgressPhase.Text = phase;
        }

        private void StartInstallationAsync(string targetDir, string vidaSrc, string ewdSrc, bool diceDriverAndReg, bool desktopShortcut, bool startMenuShortcut)
        {
            _isInstalling = true;
            _installSucceeded = false;
            _txtInstallLog.Clear();

            Thread worker = new Thread(() =>
            {
                try
                {
                    ExecuteInstallation(targetDir, vidaSrc, ewdSrc, diceDriverAndReg, desktopShortcut, startMenuShortcut);
                    _installSucceeded = true;
                    _isInstalling = false;

                    this.BeginInvoke(new Action(() =>
                    {
                        _lblFinishSummary.Text = string.Format(
                            "• Target Directory: {0}\r\n" +
                            "• Setup Log File: {1}\r\n" +
                            "• VIDA 2014D Core & 9 SQL Databases: Installed & Attached to SQLEXPRESS\r\n" +
                            "• Electronic Wiring Diagrams (EWD): {2}\r\n" +
                            "• IsoView ActiveX Replacement (DummyIsoView COM): Registered\r\n" +
                            "• Volvo DiCE 64-bit Driver & J2534 Interface: {3}\r\n\r\n" +
                            "You can start OpenVIDA anytime from your Desktop shortcut or by running VidaLauncher.exe.",
                            targetDir,
                            GetActiveLogPath(),
                            string.IsNullOrEmpty(ewdSrc) ? "Skipped (Wiring Diagrams button will be greyed out)" : "Installed & 54 Publications Indexed",
                            diceDriverAndReg ? "Installed & Configured" : "Skipped (Using alternative J2534 device)");
                        ShowStep(3);
                    }));
                }
                catch (Exception ex)
                {
                    _isInstalling = false;
                    AppendLog("============================================================");
                    AppendLog("FATAL INSTALLATION ERROR: " + ex.Message);
                    AppendLog("Stack Trace: " + ex.StackTrace);
                    AppendLog("Log File Location: " + GetActiveLogPath());
                    AppendLog("============================================================");

                    this.BeginInvoke(new Action(() =>
                    {
                        _lblProgressPhase.Text = "❌ Installation failed — see log for details";
                        _btnBack.Enabled = true;

                        string logPath = GetActiveLogPath();
                        string friendlyError = string.Format(
                            "Installation could not be completed:\r\n\r\n{0}\r\n\r\n" +
                            "Full diagnostic details have been written to the setup log:\r\n{1}\r\n\r\n" +
                            "Would you like to open the setup log file now?",
                            ex.Message,
                            logPath);

                        DialogResult dr = MessageBox.Show(friendlyError, "OpenVIDA Setup — Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                        if (dr == DialogResult.Yes)
                        {
                            OpenSetupLogFile();
                        }
                    }));
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void ExecuteInstallation(string targetDir, string vidaSrc, string ewdSrc, bool diceDriverAndReg, bool desktopShortcut, bool startMenuShortcut)
        {
            targetDir = Path.GetFullPath(targetDir);
            Directory.CreateDirectory(targetDir);

            // Mirror existing temp log to targetDir\OpenVidaSetup.log
            _targetLogFilePath = Path.Combine(targetDir, "OpenVidaSetup.log");
            try
            {
                if (File.Exists(_tempLogFilePath))
                    File.Copy(_tempLogFilePath, _targetLogFilePath, true);
            }
            catch { }

            AppendLog("Target installation directory: " + targetDir);
            AppendLog("Persistent setup log file: " + GetActiveLogPath());

            // Step A: Unpack embedded OpenVIDA payload (includes tools\7z.exe, VidaLauncher.exe, DummyIsoView.dll, DiCE\Tools\Driver_amd64, etc.)
            UpdateProgress(5, "Step 1/7: Extracting OpenVIDA core binaries & tools...");
            ExtractOpenVidaPayload(targetDir);

            string sevenZipExe = Path.Combine(targetDir, @"tools\7z.exe");
            if (!File.Exists(sevenZipExe))
                sevenZipExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"tools\7z.exe");

            // Step B: Pre-Flight Content Validation of VIDA & EWD Data Sources
            UpdateProgress(10, "Step 2/7: Verifying VIDA & EWD source file contents...");
            VerifyVidaDataSourceContent(vidaSrc, sevenZipExe);
            if (!string.IsNullOrEmpty(ewdSrc))
            {
                VerifyEwdDataSourceContent(ewdSrc, sevenZipExe);
            }

            // Step C: Install / Extract VIDA 2014D Prerequisite Data
            UpdateProgress(18, "Step 3/7: Deploying VIDA 2014D runtime & databases...");
            DeployVidaDataSource(vidaSrc, targetDir, sevenZipExe);

            // Re-extract OpenVIDA payload on top so our patched VidaLauncher.exe, DummyIsoView.dll, and components.config.xml take precedence over raw VIDA files!
            UpdateProgress(52, "Step 4/7: Applying OpenVIDA native patches & assemblies...");
            ExtractOpenVidaPayload(targetDir);

            // Step D: Optional EWD Extraction + 54 Publication Nav TSV Overlay
            if (!string.IsNullOrEmpty(ewdSrc))
            {
                UpdateProgress(60, "Step 5/7: Extracting Electronic Wiring Diagrams (EWD)...");
                DeployEwdDataSource(ewdSrc, targetDir, sevenZipExe);
            }
            else
            {
                UpdateProgress(68, "Step 5/7: Skipping Electronic Wiring Diagrams (not selected)...");
                AppendLog("EWD source not specified; Wiring Diagrams tab will be greyed out in OpenVIDA.");
            }

            // Always unpack ewd_nav_index.zip into wiring_diagrams\US_Eng if US_Eng exists
            string usEngDir = Path.Combine(targetDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\wiring_diagrams\US_Eng");
            string navZip = Path.Combine(targetDir, @"assets\ewd_nav_index.zip");
            if (Directory.Exists(usEngDir) && File.Exists(navZip) && File.Exists(sevenZipExe))
            {
                AppendLog("Installing pre-indexed component schematic & placement tables (54 publications)...");
                RunProcessLogged(sevenZipExe, string.Format("x -y \"{0}\" -o\"{1}\"", navZip, usEngDir));
            }

            // Step E: Configure SQL Server Express & Attach 9 Databases
            UpdateProgress(78, "Step 6/7: Configuring SQL Server Express & attaching 9 VIDA databases...");
            ConfigureSqlServerAndDatabases(targetDir);

            // Step F: Register COM (DummyIsoView), Optional DiCE Driver + J2534 Registry, and Shortcuts
            UpdateProgress(92, "Step 7/7: Registering COM components, DiCE driver/J2534, and shortcuts...");
            RegisterDummyIsoViewCom(targetDir);

            if (diceDriverAndReg)
            {
                InstallDiceUsbDriver(targetDir);
                RegisterDiceJ2534(targetDir);
            }
            else
            {
                AppendLog("Skipped Volvo DiCE USB driver and J2534 registry configuration (user unchecked option).");
            }

            CreateShortcuts(targetDir, desktopShortcut, startMenuShortcut);
            RegisterUninstaller(targetDir);

            UpdateProgress(100, "Installation completed successfully!");
            AppendLog("OpenVIDA installation finished successfully.");
        }

        private void RegisterUninstaller(string targetDir)
        {
            try
            {
                string uninstallExe = Path.Combine(targetDir, "OpenVidaUninstall.exe");
                File.Copy(Application.ExecutablePath, uninstallExe, true);

                string uninstallBat = Path.Combine(targetDir, "uninstall.bat");
                File.WriteAllText(uninstallBat,
                    "@echo off\r\n" +
                    "set \"TARGET=%~dp0\"\r\n" +
                    "if \"%TARGET:~-1%\"==\"\\\" set \"TARGET=%TARGET:~0,-1%\"\r\n" +
                    "start \"\" \"%TARGET%\\OpenVidaUninstall.exe\" --uninstall \"%TARGET%\"\r\n");

                string iconPath = Path.Combine(targetDir, @"assets\vida_icon.ico");
                if (!File.Exists(iconPath)) iconPath = uninstallExe;

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenVIDA"))
                {
                    if (k != null)
                    {
                        string safeTarget = targetDir.TrimEnd('\\');
                        k.SetValue("DisplayName", "OpenVIDA " + AppVersion.Tag + " Diagnostics (Volvo VIDA 2014D)");
                        k.SetValue("DisplayVersion", AppVersion.Version);
                        k.SetValue("Publisher", "OpenVIDA Project");
                        k.SetValue("InstallLocation", safeTarget);
                        k.SetValue("DisplayIcon", iconPath);
                        k.SetValue("UninstallString", string.Format("\"{0}\" --uninstall \"{1}\"", uninstallExe, safeTarget));
                        k.SetValue("QuietUninstallString", string.Format("\"{0}\" --uninstall \"{1}\" --quiet", uninstallExe, safeTarget));
                        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
                AppendLog("Registered OpenVIDA uninstaller (OpenVidaUninstall.exe, uninstall.bat, and Windows Installed Apps).");
            }
            catch (Exception ex)
            {
                AppendLog("Warning registering uninstaller: " + ex.Message);
            }
        }

        private void VerifyVidaDataSourceContent(string vidaSrc, string sevenZipExe)
        {
            vidaSrc = Path.GetFullPath(vidaSrc);
            AppendLog("Verifying VIDA 2014D data source structure: " + vidaSrc);

            if (Directory.Exists(vidaSrc))
            {
                string checkDir = vidaSrc;
                if (Directory.Exists(Path.Combine(vidaSrc, "VIDA")) && Directory.Exists(Path.Combine(vidaSrc, @"VIDA\db")))
                    checkDir = Path.Combine(vidaSrc, "VIDA");

                string baseDataMdf = Path.Combine(checkDir, @"db\BaseData_Data.MDF");
                string carComMdf = Path.Combine(checkDir, @"db\CarComRT_Data.MDF");
                if (!File.Exists(baseDataMdf) || !File.Exists(carComMdf))
                {
                    throw new Exception(
                        "Invalid VIDA 2014D folder selected:\r\n" +
                        "Could not find required SQL database files (db\\BaseData_Data.MDF and db\\CarComRT_Data.MDF) inside:\r\n" +
                        vidaSrc + "\r\n\r\nPlease point to a valid VIDA 2014D directory or archive.");
                }
                AppendLog("Verified VIDA folder contains required database files (BaseData_Data.MDF, CarComRT_Data.MDF).");
                return;
            }

            if (!File.Exists(sevenZipExe)) return;

            string ext = Path.GetExtension(vidaSrc).ToLowerInvariant();
            if (ext == ".ova" || ext == ".vmdk")
            {
                // Directly inspect the NTFS filesystem inside the VMDK/OVA using 7-Zip's compound handler (-tntfs.mbr.vmdk).
                // Note: VirtualBox .ova TAR containers often omit trailing zero blocks, causing raw TAR listing to return exit code 2;
                // opening via -tntfs.mbr.vmdk scans directly to the VMDK stream at offset 12800 and verifies VIDA\db inside NTFS with exit code 0.
                string ntfsCheck = RunProcessCaptureOutput(sevenZipExe, string.Format("l \"-tntfs.mbr.vmdk\" \"{0}\" \"VIDA\\db\"", vidaSrc), true);
                string ntfsLower = ntfsCheck.ToLowerInvariant();
                if (ntfsLower.Contains("basedata_data.mdf") && ntfsLower.Contains("carcomrt_data.mdf"))
                {
                    AppendLog("Verified virtual disk image (" + Path.GetFileName(vidaSrc) + ") contains NTFS partition with VIDA\\db\\BaseData_Data.MDF and CarComRT_Data.MDF.");
                    return;
                }

                // Fallback inspection for custom VMDK/OVA layouts
                string listOutput = RunProcessCaptureOutput(sevenZipExe, string.Format("l \"{0}\"", vidaSrc), true);
                string lower = listOutput.ToLowerInvariant();
                if (ext == ".ova" && !lower.Contains(".vmdk"))
                {
                    throw new Exception(
                        "Invalid OVA appliance selected:\r\n" +
                        "The file '" + Path.GetFileName(vidaSrc) + "' does not contain a VirtualBox .vmdk disk image.");
                }
                if (ext == ".vmdk" && !lower.Contains("vida") && !lower.Contains(".ntfs"))
                {
                    throw new Exception(
                        "Invalid VMDK disk image selected:\r\n" +
                        "The file '" + Path.GetFileName(vidaSrc) + "' does not contain a VIDA installation or NTFS partition.");
                }
                AppendLog("Verified virtual appliance container structure (" + Path.GetFileName(vidaSrc) + ").");
            }
            else
            {
                string listOutput = RunProcessCaptureOutput(sevenZipExe, string.Format("l \"{0}\"", vidaSrc), true);
                string lower = listOutput.ToLowerInvariant();
                if (!lower.Contains("basedata_data.mdf") && !lower.Contains("carcomrt_data.mdf"))
                {
                    throw new Exception(
                        "Invalid VIDA 2014D archive selected:\r\n" +
                        "The archive '" + Path.GetFileName(vidaSrc) + "' does not contain the VIDA database files (BaseData_Data.MDF / CarComRT_Data.MDF).\r\n\r\n" +
                        "Please select a valid OpenVIDA_Data_2014D.7z archive, Win7-x86-Vida.ova, or VIDA folder.");
                }
                AppendLog("Verified VIDA archive contains required database files.");
            }
        }

        private void VerifyEwdDataSourceContent(string ewdSrc, string sevenZipExe)
        {
            ewdSrc = Path.GetFullPath(ewdSrc);
            AppendLog("Verifying Electronic Wiring Diagrams (EWD) source structure: " + ewdSrc);

            if (Directory.Exists(ewdSrc))
            {
                string checkDir = ewdSrc;
                if (Directory.Exists(Path.Combine(ewdSrc, "US_Eng")))
                    checkDir = Path.Combine(ewdSrc, "US_Eng");

                if (!File.Exists(Path.Combine(checkDir, "pdf-links.js")) && !File.Exists(Path.Combine(checkDir, "index.htm")) && !File.Exists(Path.Combine(checkDir, "index.html")))
                {
                    throw new Exception(
                        "Invalid EWD folder selected:\r\n" +
                        "Could not find 'pdf-links.js' or 'US_Eng\\pdf-links.js' inside:\r\n" +
                        ewdSrc + "\r\n\r\nPlease select the Volvo_EWD_2014D.iso file, US_Eng folder, or clear the EWD field to skip it.");
                }
                AppendLog("Verified EWD folder contains US_Eng publication index (pdf-links.js).");
                return;
            }

            if (!File.Exists(sevenZipExe)) return;

            string listOutput = RunProcessCaptureOutput(sevenZipExe, string.Format("l \"{0}\"", ewdSrc), true);
            string lower = listOutput.ToLowerInvariant();

            if (!lower.Contains("us_eng") && !lower.Contains("pdf-links.js") && !lower.Contains("ewd") && !lower.Contains(".iso"))
            {
                throw new Exception(
                    "Invalid Electronic Wiring Diagrams archive selected:\r\n" +
                    "The file '" + Path.GetFileName(ewdSrc) + "' does not contain 'US_Eng' or Volvo EWD schematic files.\r\n\r\n" +
                    "Please select Volvo_EWD_2014D.iso (or Volvo_EWD_2014D.part1.rar), or clear the field to install without EWD.");
            }
            AppendLog("Verified EWD archive contains Volvo wiring diagram assets.");
        }

        private void ExtractOpenVidaPayload(string targetDir)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream resStream = asm.GetManifestResourceStream("OpenVidaPayload.zip");
            if (resStream != null)
            {
                string tempZip = Path.Combine(Path.GetTempPath(), "OpenVidaPayload_" + Process.GetCurrentProcess().Id + ".zip");
                try
                {
                    using (FileStream fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                    {
                        resStream.CopyTo(fs);
                    }
                    using (ZipArchive archive = ZipFile.OpenRead(tempZip))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destPath = Path.Combine(targetDir, entry.FullName.Replace('/', '\\'));
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destPath);
                                continue;
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            entry.ExtractToFile(destPath, true);
                        }
                    }
                    AppendLog("Extracted embedded OpenVIDA payload into " + targetDir);
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    resStream.Dispose();
                }
            }
            else
            {
                AppendLog("Embedded resource not found; copying from current repository directory...");
                CopyOpenVidaRepoFiles(AppDomain.CurrentDomain.BaseDirectory, targetDir);
            }
        }

        private void CopyOpenVidaRepoFiles(string repoRoot, string targetDir)
        {
            if (Path.GetFullPath(repoRoot).TrimEnd('\\').Equals(Path.GetFullPath(targetDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("Target directory matches source directory; in-place setup verified.");
                return;
            }

            string[] subDirs = new string[] { "tools", "assets", "DiCE", @"mcp\vida-mcp" };
            foreach (string sd in subDirs)
            {
                string src = Path.Combine(repoRoot, sd);
                if (Directory.Exists(src))
                    CopyDirectoryRecursive(src, Path.Combine(targetDir, sd));
            }
        }

        private void DeployVidaDataSource(string vidaSrc, string targetDir, string sevenZipExe)
        {
            vidaSrc = Path.GetFullPath(vidaSrc);
            string targetVidaDir = Path.Combine(targetDir, "VIDA");
            Directory.CreateDirectory(targetVidaDir);

            if (Directory.Exists(vidaSrc))
            {
                string actualVidaSrc = vidaSrc;
                if (Directory.Exists(Path.Combine(vidaSrc, "VIDA")) && Directory.Exists(Path.Combine(vidaSrc, @"VIDA\db")))
                    actualVidaSrc = Path.Combine(vidaSrc, "VIDA");

                if (Path.GetFullPath(actualVidaSrc).TrimEnd('\\').Equals(Path.GetFullPath(targetVidaDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("VIDA folder is already in target location (" + targetVidaDir + "). Skipping copy.");
                    return;
                }

                AppendLog("Copying VIDA directory from: " + actualVidaSrc);
                CopyDirectoryRecursive(actualVidaSrc, targetVidaDir);
                return;
            }

            string ext = Path.GetExtension(vidaSrc).ToLowerInvariant();
            if (ext == ".ova" || ext == ".vmdk")
            {
            // Single-pass stream extraction of ONLY the required VIDA assemblies & databases directly from the NTFS partition inside the VMDK/OVA (-tntfs.mbr.vmdk)
            // without creating an intermediate 11.7 GB temporary .vmdk file or extracting ~1 GB of unneeded legacy JBoss/JDK/InstallShield bloat!
            AppendLog("Extracting VIDA 2014D databases & diagnostic assemblies directly from virtual disk image (" + Path.GetFileName(vidaSrc) + ")...");
            string directArgs = string.Format(
                "x -y -aos \"-tntfs.mbr.vmdk\" \"{0}\" \"VIDA\\db\\*\" \"VIDA\\jboss\\standalone\\deployments\\VidaEar.ear\\VidaWeb.war\\Diagnostics\\*\" \"VIDA\\System\\config\\*\" \"VIDA\\commToolInformation.xml\" -o\"{1}\"",
                vidaSrc, targetDir);
            RunProcessLogged(sevenZipExe, directArgs, true);

                // Ensure VIDA\System\Log\Diagnostics directory exists for runtime logging
                Directory.CreateDirectory(Path.Combine(targetVidaDir, @"System\Log\Diagnostics"));

                // Verify if VIDA\db\BaseData_Data.MDF was extracted; if the VMDK had multiple partitions (e.g. 1.ntfs), fall back to partition-prefixed extraction
                if (!File.Exists(Path.Combine(targetVidaDir, @"db\BaseData_Data.MDF")))
                {
                    AppendLog("Primary NTFS partition did not place VIDA\\db directly; probing multi-partition VMDK layout...");
                    RunProcessLogged(sevenZipExe, string.Format("x -y -aos \"-tvmdk\" \"{0}\" \"VIDA\\db\\*\" \"VIDA\\jboss\\standalone\\deployments\\VidaEar.ear\\VidaWeb.war\\Diagnostics\\*\" \"0.ntfs\\VIDA\\db\\*\" \"0.ntfs\\VIDA\\jboss\\standalone\\deployments\\VidaEar.ear\\VidaWeb.war\\Diagnostics\\*\" \"1.ntfs\\VIDA\\db\\*\" \"1.ntfs\\VIDA\\jboss\\standalone\\deployments\\VidaEar.ear\\VidaWeb.war\\Diagnostics\\*\" -o\"{1}\"", vidaSrc, targetDir), true);

                    foreach (string partDir in new[] { "0.ntfs", "1.ntfs" })
                    {
                        string ntfsVida = Path.Combine(targetDir, partDir + @"\VIDA");
                        if (Directory.Exists(ntfsVida))
                        {
                            CopyDirectoryRecursive(ntfsVida, targetVidaDir);
                            try { Directory.Delete(Path.Combine(targetDir, partDir), true); } catch { }
                        }
                    }
                }
            }
            else
            {
                AppendLog("Extracting VIDA data archive: " + Path.GetFileName(vidaSrc));
                RunProcessLogged(sevenZipExe, string.Format("x -y -aos \"{0}\" -o\"{1}\"", vidaSrc, targetVidaDir), true);

                string nestedVida = Path.Combine(targetVidaDir, "VIDA");
                if (Directory.Exists(nestedVida) && Directory.Exists(Path.Combine(nestedVida, "db")) && !Directory.Exists(Path.Combine(targetVidaDir, "db")))
                {
                    AppendLog("Normalizing nested VIDA\\ directory structure...");
                    CopyDirectoryRecursive(nestedVida, targetVidaDir);
                    try { Directory.Delete(nestedVida, true); } catch { }
                }
            }
        }

        private void DeployEwdDataSource(string ewdSrc, string targetDir, string sevenZipExe)
        {
            ewdSrc = Path.GetFullPath(ewdSrc);
            string wiringDiagramsRoot = Path.Combine(targetDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\wiring_diagrams");
            string targetUsEng = Path.Combine(wiringDiagramsRoot, "US_Eng");
            Directory.CreateDirectory(targetUsEng);

            if (Directory.Exists(ewdSrc))
            {
                string actualSrc = ewdSrc;
                if (Directory.Exists(Path.Combine(ewdSrc, "US_Eng")))
                    actualSrc = Path.Combine(ewdSrc, "US_Eng");

                if (Path.GetFullPath(actualSrc).TrimEnd('\\').Equals(Path.GetFullPath(targetUsEng).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("EWD US_Eng folder is already in target location. Skipping copy.");
                    return;
                }
                AppendLog("Copying EWD US_Eng files from: " + actualSrc);
                CopyDirectoryRecursive(actualSrc, targetUsEng);
                return;
            }

            string ext = Path.GetExtension(ewdSrc).ToLowerInvariant();
            if (ext == ".rar")
            {
                AppendLog("Extracting multi-part RAR EWD archive: " + Path.GetFileName(ewdSrc));
                string tempIsoDir = Path.Combine(targetDir, "_temp_ewd_extract");
                Directory.CreateDirectory(tempIsoDir);
                try
                {
                    RunProcessLogged(sevenZipExe, string.Format("x -y \"{0}\" -o\"{1}\"", ewdSrc, tempIsoDir), true);
                    string[] isos = Directory.GetFiles(tempIsoDir, "*.iso", SearchOption.AllDirectories);
                    if (isos.Length > 0)
                    {
                        DeployEwdDataSource(isos[0], targetDir, sevenZipExe);
                    }
                    else
                    {
                        CopyDirectoryRecursive(tempIsoDir, targetUsEng);
                    }
                }
                finally
                {
                    try { if (Directory.Exists(tempIsoDir)) Directory.Delete(tempIsoDir, true); } catch { }
                }
                return;
            }

            // Extract US_Eng\* directly into wiring_diagrams\ without creating a 2.5 GB temporary folder on disk
            AppendLog("Extracting US_Eng wiring diagrams directly from: " + Path.GetFileName(ewdSrc));
            RunProcessLogged(sevenZipExe, string.Format("x -y \"{0}\" \"US_Eng\\*\" -o\"{1}\"", ewdSrc, wiringDiagramsRoot), true);

            if (!File.Exists(Path.Combine(targetUsEng, "pdf-links.js")))
            {
                AppendLog("Archive root is already US_Eng content; extracting directly into US_Eng...");
                RunProcessLogged(sevenZipExe, string.Format("x -y \"{0}\" -o\"{1}\"", ewdSrc, targetUsEng), true);
            }
        }

        private void ConfigureSqlServerAndDatabases(string targetDir)
        {
            // 1. Ensure MSSQL$SQLEXPRESS service is running
            try
            {
                using (ServiceController sc = new ServiceController("MSSQL$SQLEXPRESS"))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        AppendLog("Starting SQL Server (MSSQL$SQLEXPRESS) service...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch (Exception exSvc)
            {
                throw new Exception(
                    "Could not start or locate SQL Server Express service (MSSQL$SQLEXPRESS).\r\n" +
                    "Please ensure Microsoft SQL Server Express is installed and running.\r\n" +
                    "Details: " + exSvc.Message);
            }

            // 2. Enable TCP/IP port 1433 & Mixed Mode Auth in Registry if running as Admin
            if (IsRunningAsAdministrator())
            {
                try
                {
                    using (RegistryKey sqlRoot = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server"))
                    {
                        if (sqlRoot != null)
                        {
                            foreach (string sub in sqlRoot.GetSubKeyNames())
                            {
                                if (sub.EndsWith(".SQLEXPRESS", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (RegistryKey mssqlKey = sqlRoot.OpenSubKey(sub + @"\MSSQLServer", true))
                                    {
                                        if (mssqlKey != null) mssqlKey.SetValue("LoginMode", 2, RegistryValueKind.DWord);
                                    }
                                    using (RegistryKey tcpKey = sqlRoot.OpenSubKey(sub + @"\MSSQLServer\SuperSocketNetLib\Tcp", true))
                                    {
                                        if (tcpKey != null) tcpKey.SetValue("Enabled", 1, RegistryValueKind.DWord);
                                    }
                                    using (RegistryKey ipAll = sqlRoot.OpenSubKey(sub + @"\MSSQLServer\SuperSocketNetLib\Tcp\IPAll", true))
                                    {
                                        if (ipAll != null)
                                        {
                                            ipAll.SetValue("TcpPort", "1433", RegistryValueKind.String);
                                            ipAll.SetValue("TcpDynamicPorts", "", RegistryValueKind.String);
                                        }
                                    }
                                    AppendLog("Configured Mixed Mode Auth & TCP port 1433 on " + sub);
                                }
                            }
                        }
                    }
                }
                catch (Exception exReg)
                {
                    AppendLog("Note configuring SQL registry: " + exReg.Message);
                }
            }

            // 3. Grant NT SERVICE\MSSQL$SQLEXPRESS full permissions on VIDA\db and clear ReadOnly attributes so SQL Server can attach .mdf/.ldf files
            string dbFolder = Path.Combine(targetDir, @"VIDA\db");
            if (Directory.Exists(dbFolder))
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dbFolder))
                    {
                        try
                        {
                            FileAttributes attr = File.GetAttributes(f);
                            if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                File.SetAttributes(f, attr & ~FileAttributes.ReadOnly);
                        }
                        catch { }
                    }
                    ProcessStartInfo psiAcl = new ProcessStartInfo("icacls.exe", string.Format("\"{0}\" /grant \"NT SERVICE\\MSSQL$SQLEXPRESS:(OI)(CI)F\" /T /C /Q", dbFolder))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process pAcl = Process.Start(psiAcl))
                    {
                        pAcl.WaitForExit(15000);
                    }
                    AppendLog("Granted NT SERVICE\\MSSQL$SQLEXPRESS access permissions on " + dbFolder);
                }
                catch (Exception exAcl)
                {
                    AppendLog("Note setting database folder ACLs: " + exAcl.Message);
                }
            }

            // 4. Connect to localhost\SQLEXPRESS and attach all 9 databases
            var dbMap = new Dictionary<string, string[]>()
            {
                { "BaseData", new[] { "BaseData_Data.MDF", "BaseData_log.LDF" } },
                { "EPC", new[] { "EPC_Data.mdf", "EPC_Log.ldf" } },
                { "AccessServer", new[] { "AccessServer.mdf", "AccessServer.ldf" } },
                { "DiagSwdlRepository", new[] { "DiagSwdlRepository_Data.MDF", "DiagSwdlRepository_log.LDF" } },
                { "CarCom", new[] { "CarComRT_Data.MDF", "CarComRT_Log.LDF" } },
                { "DiagSwdlSession", new[] { "DiagSwdlSession_Data.MDF", "DiagSwdlSession_Log.LDF" } },
                { "DiceTiming", new[] { "DiceTiming_Data.MDF", "DiceTiming_Log.LDF" } },
                { "imagerepository", new[] { "imagerepository_data.mdf", "imagerepository_log.ldf" } },
                { "servicerep_en-US", new[] { "servicerep_en-US.MDF", "servicerep_en-US.LDF" } }
            };

            string connStr = @"Server=localhost\SQLEXPRESS;Database=master;Integrated Security=True;Connection Timeout=10";
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                AppendLog("Connected to localhost\\SQLEXPRESS.");

                foreach (var kvp in dbMap)
                {
                    string dbName = kvp.Key;
                    string mdfPath = Path.Combine(dbFolder, kvp.Value[0]);
                    string ldfPath = Path.Combine(dbFolder, kvp.Value[1]);

                    bool alreadyOnline = false;
                    using (SqlCommand checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = @"
SELECT d.state, mf.physical_name
FROM sys.databases d
LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id AND mf.file_id = 1
WHERE d.name = @db";
                        checkCmd.Parameters.AddWithValue("@db", dbName);
                        using (SqlDataReader rdr = checkCmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                int state = rdr.IsDBNull(0) ? -1 : Convert.ToInt32(rdr.GetValue(0));
                                string physName = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                                if (state == 0 && (string.IsNullOrEmpty(physName) || File.Exists(physName)))
                                {
                                    alreadyOnline = true;
                                }
                            }
                        }
                    }

                    if (alreadyOnline)
                    {
                        AppendLog("Database [" + dbName + "] is already attached and online.");
                    }
                    else if (File.Exists(mdfPath))
                    {
                        // If a stale/offline entry exists in sys.databases, detach it first
                        try
                        {
                            ExecuteSqlNonQuery(conn, string.Format("IF EXISTS (SELECT * FROM sys.databases WHERE name = '{0}') EXEC sp_detach_db '{0}', 'true';", dbName));
                        }
                        catch { }

                        AppendLog("Attaching database [" + dbName + "] from " + mdfPath + "...");
                        try
                        {
                            using (SqlCommand attachCmd = conn.CreateCommand())
                            {
                                attachCmd.CommandTimeout = 120;
                                if (File.Exists(ldfPath))
                                {
                                    attachCmd.CommandText = string.Format(
                                        "CREATE DATABASE [{0}] ON (FILENAME = N'{1}'), (FILENAME = N'{2}') FOR ATTACH;",
                                        dbName, mdfPath.Replace("'", "''"), ldfPath.Replace("'", "''"));
                                }
                                else
                                {
                                    attachCmd.CommandText = string.Format(
                                        "CREATE DATABASE [{0}] ON (FILENAME = N'{1}') FOR ATTACH_REBUILD_LOG;",
                                        dbName, mdfPath.Replace("'", "''"));
                                }
                                attachCmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception exAttach)
                        {
                            AppendLog("Standard attach noted (" + exAttach.Message + "); retrying with FOR ATTACH_REBUILD_LOG...");
                            try { if (File.Exists(ldfPath)) File.Delete(ldfPath); } catch { }
                            using (SqlCommand rebuildCmd = conn.CreateCommand())
                            {
                                rebuildCmd.CommandTimeout = 120;
                                rebuildCmd.CommandText = string.Format(
                                    "CREATE DATABASE [{0}] ON (FILENAME = N'{1}') FOR ATTACH_REBUILD_LOG;",
                                    dbName, mdfPath.Replace("'", "''"));
                                rebuildCmd.ExecuteNonQuery();
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Required database file was not found after extraction: " + mdfPath);
                    }
                }

                // 4. Configure Logins ASUser & PPUsers and repair SIDs across all 9 databases
                AppendLog("Configuring SQL logins [ASUser] and [PPUsers]...");
                ExecuteSqlNonQuery(conn, @"
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'ASUser')
    CREATE LOGIN [ASUser] WITH PASSWORD = N'GunnarS3g3', DEFAULT_DATABASE = [master], CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN [ASUser] WITH PASSWORD = N'GunnarS3g3', CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'PPUsers')
    CREATE LOGIN [PPUsers] WITH PASSWORD = N'PPUsers', DEFAULT_DATABASE = [master], CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;
");

                foreach (string dbName in dbMap.Keys)
                {
                    try
                    {
                        ExecuteSqlNonQuery(conn, string.Format(@"
USE [{0}];
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'ASUser')
    ALTER USER [ASUser] WITH LOGIN = [ASUser];
ELSE
    CREATE USER [ASUser] FOR LOGIN [ASUser];
EXEC sp_addrolemember N'db_owner', N'ASUser';
", dbName));
                    }
                    catch (Exception exUsr)
                    {
                        AppendLog("Note mapping ASUser on " + dbName + ": " + exUsr.Message);
                    }
                }

                // 5. Apply DiagSwdlSession.dbo.Dtc nullable fkEcuInfoId schema fix
                try
                {
                    ExecuteSqlNonQuery(conn, "ALTER TABLE [DiagSwdlSession].[dbo].[Dtc] ALTER COLUMN [fkEcuInfoId] INT NULL;");
                    AppendLog("Verified DiagSwdlSession.dbo.Dtc schema compatibility fix.");
                }
                catch { }
            }
        }

        private void ExecuteSqlNonQuery(SqlConnection conn, string sql)
        {
            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandTimeout = 60;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void RegisterDummyIsoViewCom(string targetDir)
        {
            try
            {
                string dllPath = Path.Combine(targetDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\DummyIsoView.dll");
                string codeBaseUri = new Uri(dllPath).AbsoluteUri;
                string clsidKeyPath = @"Software\Classes\CLSID\{865B2280-2B71-11D1-BC01-006097AC382A}";

                using (RegistryKey clsidKey = Registry.CurrentUser.CreateSubKey(clsidKeyPath))
                {
                    clsidKey.SetValue("", "IsoViewStub.DummyIsoView");
                    using (RegistryKey ipsKey = clsidKey.CreateSubKey("InprocServer32"))
                    {
                        ipsKey.SetValue("", "mscoree.dll");
                        ipsKey.SetValue("ThreadingModel", "Both");
                        ipsKey.SetValue("Class", "IsoViewStub.DummyIsoView");
                        ipsKey.SetValue("Assembly", "DummyIsoView, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                        ipsKey.SetValue("RuntimeVersion", "v4.0.30319");
                        ipsKey.SetValue("CodeBase", codeBaseUri);
                    }
                    using (RegistryKey ctrlKey = clsidKey.CreateSubKey("Control")) { }
                    using (RegistryKey miscKey = clsidKey.CreateSubKey("MiscStatus"))
                    {
                        miscKey.SetValue("", "0");
                        using (RegistryKey m1 = miscKey.CreateSubKey("1"))
                        {
                            m1.SetValue("", "131473");
                        }
                    }
                }
                AppendLog("Registered DummyIsoView CGM/ActiveX Replacement in HKCU.");
            }
            catch (Exception ex)
            {
                AppendLog("Warning registering DummyIsoView COM: " + ex.Message);
            }
        }

        private void InstallDiceUsbDriver(string targetDir)
        {
            try
            {
                string inf64 = Path.Combine(targetDir, @"DiCE\Tools\Driver_amd64\DiceUsb.inf");
                string inf32 = Path.Combine(targetDir, @"DiCE\Tools\Driver\DiceUsb.inf");
                string infToInstall = Environment.Is64BitOperatingSystem && File.Exists(inf64) ? inf64 : inf32;

                if (!File.Exists(infToInstall))
                {
                    AppendLog("Note: DiCE USB INF file not found at " + infToInstall);
                    return;
                }

                if (!IsRunningAsAdministrator())
                {
                    AppendLog("Note: Running as standard user; skipping pnputil kernel driver stage (run installer as Admin to stage 64-bit DiceUsb.inf into DriverStore).");
                    return;
                }

                AppendLog("Installing signed 64-bit Volvo DiCE USB driver (" + infToInstall + ") via pnputil...");
                ProcessStartInfo psi = new ProcessStartInfo("pnputil.exe", string.Format("/add-driver \"{0}\" /install", infToInstall))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string outText = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    AppendLog("pnputil output: " + outText.Trim());
                }
            }
            catch (Exception ex)
            {
                AppendLog("Warning installing DiCE USB driver: " + ex.Message);
            }
        }

        private void RegisterDiceJ2534(string targetDir)
        {
            try
            {
                string tsDiceDll = Path.Combine(targetDir, @"DiCE\Tools\TSDiCE32.dll");
                if (!File.Exists(tsDiceDll) && File.Exists(@"C:\Program Files (x86)\DiCE\Tools\TSDiCE32.dll"))
                    tsDiceDll = @"C:\Program Files (x86)\DiCE\Tools\TSDiCE32.dll";

                string[] toolNames = new string[] { "SETEK - DiCE", "DiCE-206751" };
                RegistryKey baseRoot = IsRunningAsAdministrator() ? Registry.LocalMachine : Registry.CurrentUser;
                string subPath = @"SOFTWARE\WOW6432Node\PassThruSupport.04.04";

                foreach (string tName in toolNames)
                {
                    using (RegistryKey k = baseRoot.CreateSubKey(subPath + "\\" + tName))
                    {
                        if (k != null)
                        {
                            k.SetValue("Name", tName);
                            k.SetValue("Vendor", "SETEK AB");
                            k.SetValue("ConfigURL", "");
                            k.SetValue("FunctionLibrary", tsDiceDll);
                            k.SetValue("CAN", 1, RegistryValueKind.DWord);
                            k.SetValue("ISO15765", 1, RegistryValueKind.DWord);
                            k.SetValue("ISO9141", 1, RegistryValueKind.DWord);
                            k.SetValue("ISO14230", 1, RegistryValueKind.DWord);
                            k.SetValue("J1850VPW", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                AppendLog("Configured J2534 PassThru registry entries pointing to: " + tsDiceDll);
            }
            catch (Exception ex)
            {
                AppendLog("Note writing J2534 registry keys: " + ex.Message);
            }
        }

        private void CreateShortcuts(string targetDir, bool desktop, bool startMenu)
        {
            string exePath = Path.Combine(targetDir, @"VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\VidaLauncher.exe");
            string iconPath = Path.Combine(targetDir, @"assets\vida_icon.ico");
            if (!File.Exists(iconPath))
                iconPath = exePath;

            if (desktop)
            {
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcutFile(Path.Combine(desktopDir, "OpenVIDA Diagnostics.lnk"), exePath, Path.GetDirectoryName(exePath), iconPath);
                AppendLog("Created Desktop shortcut: OpenVIDA Diagnostics.lnk");
            }

            if (startMenu)
            {
                string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                CreateShortcutFile(Path.Combine(programsDir, "OpenVIDA Diagnostics.lnk"), exePath, Path.GetDirectoryName(exePath), iconPath);
                AppendLog("Created Start Menu shortcut: OpenVIDA Diagnostics.lnk");
            }
        }

        private void CreateShortcutFile(string lnkPath, string targetExe, string workingDir, string iconLocation)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "OpenVIDA Standalone Volvo Diagnostic & Repair Suite" });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch (Exception ex)
            {
                AppendLog("Warning creating shortcut " + lnkPath + ": " + ex.Message);
            }
        }

        private string RunProcessCaptureOutput(string fileName, string arguments, bool allowWarnings = false)
        {
            ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    if (allowWarnings && p.ExitCode <= 2 && !string.IsNullOrEmpty(stdout))
                    {
                        AppendLog("Note: " + Path.GetFileName(fileName) + " returned non-fatal code " + p.ExitCode + " during archive inspection.");
                        return stdout;
                    }
                    throw new Exception(string.Format(
                        "Failed to inspect archive with {0} (exit code {1}):\r\n{2}",
                        Path.GetFileName(fileName), p.ExitCode, string.IsNullOrEmpty(stderr) ? stdout : stderr));
                }
                return stdout;
            }
        }

        private void RunProcessLogged(string fileName, string arguments, bool allowNonFatalWarnings = false)
        {
            AppendLog("Executing: " + Path.GetFileName(fileName) + " " + arguments);
            ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            StringBuilder outBuf = new StringBuilder();
            StringBuilder errBuf = new StringBuilder();
            using (Process p = Process.Start(psi))
            {
                p.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outBuf.AppendLine(e.Data);
                        AppendLog("  " + e.Data);
                    }
                };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errBuf.AppendLine(e.Data);
                        AppendLog("  [WARN] " + e.Data);
                    }
                };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string outDetail = outBuf.ToString();
                    string errDetail = errBuf.ToString().Trim();
                    // 7-Zip exit code 1 = Warning (e.g. locked file skipped with -aos), or exit code 2 when "Everything is Ok" / TAR trailing zero block warning
                    if (allowNonFatalWarnings && (p.ExitCode == 1 || (p.ExitCode == 2 && (outDetail.Contains("Everything is Ok") || errDetail.Contains("Unexpected end of archive")))))
                    {
                        AppendLog("Note: " + Path.GetFileName(fileName) + " finished with non-fatal code " + p.ExitCode + ".");
                        return;
                    }
                    throw new Exception(string.Format(
                        "Archive extraction process ({0}) failed with exit code {1}.\r\n{2}",
                        Path.GetFileName(fileName),
                        p.ExitCode,
                        string.IsNullOrEmpty(errDetail) ? "Check OpenVidaSetup.log for details." : errDetail));
                }
            }
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSub = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSub);
            }
        }

        private bool CheckWebView2Installed()
        {
            string[] regPaths = new string[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
            };
            foreach (string rp in regPaths)
            {
                using (RegistryKey k1 = Registry.LocalMachine.OpenSubKey(rp))
                {
                    if (k1 != null && k1.GetValue("pv") != null) return true;
                }
                using (RegistryKey k2 = Registry.CurrentUser.OpenSubKey(rp))
                {
                    if (k2 != null && k2.GetValue("pv") != null) return true;
                }
            }
            return false;
        }

        private string CheckSqlExpressState()
        {
            try
            {
                using (ServiceController sc = new ServiceController("MSSQL$SQLEXPRESS"))
                {
                    return (sc.Status == ServiceControllerStatus.Running) ? "RUNNING" : "STOPPED";
                }
            }
            catch
            {
                return "MISSING";
            }
        }

        private bool IsRunningAsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
