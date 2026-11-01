using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

static partial class Program
{
    public static bool PerformStartupHealthCheck(string vidaPath)
    {
        Console.WriteLine("[HealthCheck] Running OpenVIDA startup pre-flight health probe...");

        // 1. Verify SQL Server Responsiveness
        if (!EnsureSqlServerResponsive(vidaPath))
        {
            Console.WriteLine("[HealthCheck] User chose not to proceed without SQL Server. Exiting.");
            return false;
        }

        // 2. Verify JBoss Application Server (Port 80)
        if (!EnsureJBossRunning(vidaPath))
        {
            Console.WriteLine("[HealthCheck] User chose not to proceed without JBoss server. Exiting.");
            return false;
        }

        // 3. Informational Check: Edge WebView2 Runtime
        CheckWebView2Runtime();

        Console.WriteLine("[HealthCheck] Pre-flight checks passed successfully!");
        return true;
    }

    private static bool ProbeSqlConnection(string serverName, int timeoutSeconds)
    {
        try
        {
            string connStr = string.Format("Server={0};Database=master;Integrated Security=True;Connection Timeout={1}", serverName, timeoutSeconds);
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureSqlServerResponsive(string vidaPath)
    {
        // Fast probe localhost\SQLEXPRESS, then (local)\SQLEXPRESS
        if (ProbeSqlConnection(@"localhost\SQLEXPRESS", 2) || ProbeSqlConnection(@"(local)\SQLEXPRESS", 2) || ProbeSqlConnection(@".\SQLEXPRESS", 2))
        {
            Console.WriteLine("[HealthCheck] SQL Server (SQLEXPRESS): Responding normally.");
            return true;
        }

        Console.WriteLine("[HealthCheck] WARNING: SQL Server is not responding to connections (possible socket hang or stopped).");

        // Show recovery dialog to the user
        while (true)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "OpenVIDA — Database Sentinel";
                dlg.Size = new Size(520, 270);
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.White;
                dlg.Font = new Font("Segoe UI", 9.0f);

                try
                {
                    string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
                    if (File.Exists(icoPath)) dlg.Icon = new Icon(icoPath);
                }
                catch {}

                Panel header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(254, 243, 199) };
                Label lblIcon = new Label { Text = "⚠️", Font = new Font("Segoe UI", 20f), Location = new Point(14, 10), Size = new Size(46, 42), AutoSize = false, BackColor = Color.Transparent };
                Label lblTitle = new Label { Text = "SQL Server (SQLEXPRESS) Not Responding", Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = Color.FromArgb(146, 64, 14), Location = new Point(68, 12), AutoSize = true, BackColor = Color.Transparent };
                Label lblSub = new Label { Text = "The database service is stopped or stuck in a socket timeout.", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(180, 83, 9), Location = new Point(68, 35), AutoSize = true, BackColor = Color.Transparent };
                header.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblSub });

                Label lblMsg = new Label
                {
                    Text = "OpenVIDA requires local SQL Express databases to access vehicle communication mappings, diagnostic sessions, and repair manuals.\n\nThis connection timeout commonly occurs after Windows sleep or network adapter changes. Would you like to restart the SQL service now?",
                    Location = new Point(20, 75),
                    Size = new Size(465, 80),
                    ForeColor = Color.FromArgb(51, 65, 85)
                };

                Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(248, 250, 252) };

                Button btnRestart = new Button
                {
                    Text = "🔄 Auto-Restart Service (Admin)",
                    Location = new Point(20, 14),
                    Size = new Size(205, 34),
                    BackColor = Color.FromArgb(0, 102, 204),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnRestart.FlatAppearance.BorderSize = 0;

                Button btnRetry = new Button
                {
                    Text = "🔁 Retry",
                    Location = new Point(235, 14),
                    Size = new Size(80, 34),
                    BackColor = Color.FromArgb(226, 232, 240),
                    ForeColor = Color.FromArgb(30, 41, 59),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnRetry.FlatAppearance.BorderSize = 0;

                Button btnIgnore = new Button
                {
                    Text = "Ignore",
                    Location = new Point(325, 14),
                    Size = new Size(80, 34),
                    BackColor = Color.FromArgb(241, 245, 249),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnIgnore.FlatAppearance.BorderSize = 0;

                Button btnExit = new Button
                {
                    Text = "Exit",
                    Location = new Point(415, 14),
                    Size = new Size(80, 34),
                    BackColor = Color.FromArgb(241, 245, 249),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnExit.FlatAppearance.BorderSize = 0;

                int actionChosen = 0;

                btnRestart.Click += (s, e) => {
                    actionChosen = 1;
                    dlg.Close();
                };
                btnRetry.Click += (s, e) => {
                    actionChosen = 2;
                    dlg.Close();
                };
                btnIgnore.Click += (s, e) => {
                    actionChosen = 3;
                    dlg.Close();
                };
                btnExit.Click += (s, e) => {
                    actionChosen = 4;
                    dlg.Close();
                };

                btnPanel.Controls.AddRange(new Control[] { btnRestart, btnRetry, btnIgnore, btnExit });
                dlg.Controls.AddRange(new Control[] { header, lblMsg, btnPanel });

                dlg.ShowDialog();

                if (actionChosen == 1)
                {
                    // Attempt elevated restart of SQL service
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c net stop MSSQL$SQLEXPRESS & net start MSSQL$SQLEXPRESS",
                            Verb = "runas",
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Normal
                        };
                        Process p = Process.Start(psi);
                        if (p != null) p.WaitForExit();

                        // Short delay to let socket bind
                        Thread.Sleep(1500);
                        if (ProbeSqlConnection(@"localhost\SQLEXPRESS", 3) || ProbeSqlConnection(@"(local)\SQLEXPRESS", 3))
                        {
                            Console.WriteLine("[HealthCheck] SQL Server successfully restarted and responding!");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not elevate SQL Server restart: " + ex.Message, "SQL Restart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (actionChosen == 2)
                {
                    if (ProbeSqlConnection(@"localhost\SQLEXPRESS", 3) || ProbeSqlConnection(@"(local)\SQLEXPRESS", 3))
                    {
                        Console.WriteLine("[HealthCheck] SQL Server is now responding!");
                        return true;
                    }
                }
                else if (actionChosen == 3)
                {
                    Console.WriteLine("[HealthCheck] User chose to ignore SQL warning and continue.");
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    private static bool ProbeJBossPort80()
    {
        try
        {
            using (var client = new TcpClient())
            {
                var ar = client.BeginConnect("127.0.0.1", 80, null, null);
                bool success = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                if (success)
                {
                    client.EndConnect(ar);
                    return true;
                }
            }
        }
        catch {}
        return false;
    }

    private static bool ProbeJBossVidaReady()
    {
        try
        {
            var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://127.0.0.1/Vida/version.txt");
            req.Timeout = 1500;
            req.Method = "GET";
            using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
            {
                return (int)resp.StatusCode == 200;
            }
        }
        catch
        {
            try
            {
                var reqRoot = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://127.0.0.1/");
                reqRoot.Timeout = 1500;
                reqRoot.Method = "GET";
                using (var respRoot = (System.Net.HttpWebResponse)reqRoot.GetResponse())
                {
                    return (int)respRoot.StatusCode == 200;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool EnsureJBossRunning(string vidaPath)
    {
        if (ProbeJBossVidaReady())
        {
            Console.WriteLine("[HealthCheck] JBoss Application Server & VidaWeb: Responding on port 80.");
            return true;
        }

        bool port80Open = ProbeJBossPort80();
        if (port80Open)
        {
            Console.WriteLine("[HealthCheck] JBoss port 80 is open, but VidaWeb deployment is still initializing. Waiting for readiness...");
        }
        else
        {
            Console.WriteLine("[HealthCheck] JBoss server is not responding on port 80. Attempting auto-start...");

            string jbossBat = Path.Combine(vidaPath, "jboss.bat");
            if (!File.Exists(jbossBat))
            {
                Console.WriteLine("[HealthCheck] Warning: jboss.bat not found at " + jbossBat);
                return true;
            }

            // Auto-launch JBoss in background
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    WorkingDirectory = vidaPath,
                    FileName = "cmd.exe",
                    Arguments = "/c jboss.bat start",
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HealthCheck] Failed to launch jboss.bat: " + ex.Message);
                return true;
            }
        }

        // Display sleek progress dialog while polling ProbeJBossVidaReady() (up to 40 seconds)
        using (Form splash = new Form())
        {
            splash.Text = "OpenVIDA — Initializing Backend";
            splash.Size = new Size(440, 175);
            splash.StartPosition = FormStartPosition.CenterScreen;
            splash.FormBorderStyle = FormBorderStyle.FixedDialog;
            splash.ControlBox = false;
            splash.BackColor = Color.White;
            splash.Font = new Font("Segoe UI", 9.0f);

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
                if (File.Exists(icoPath)) splash.Icon = new Icon(icoPath);
            }
            catch {}

            Label lblStatus = new Label
            {
                Text = port80Open ? "Initializing Diagnostic Web Services..." : "Starting JBoss Application Server...",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(20, 20),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = "Waiting for VidaWeb deployment to mount on port 80...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(22, 45),
                AutoSize = true
            };

            ProgressBar pb = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(20, 80),
                Size = new Size(385, 20)
            };

            splash.Controls.AddRange(new Control[] { lblStatus, lblSub, pb });

            bool jbossReady = false;
            System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            int elapsedSec = 0;

            pollTimer.Tick += (s, e) => {
                elapsedSec++;
                if (ProbeJBossVidaReady())
                {
                    jbossReady = true;
                    pollTimer.Stop();
                    splash.Close();
                }
                else if (elapsedSec >= 40)
                {
                    pollTimer.Stop();
                    splash.Close();
                }
            };

            splash.Shown += (s, e) => { pollTimer.Start(); };
            splash.ShowDialog();

            if (jbossReady)
            {
                Console.WriteLine("[HealthCheck] JBoss backend & VidaWeb initialized successfully on port 80!");
                return true;
            }
            else
            {
                Console.WriteLine("[HealthCheck] JBoss VidaWeb wait timed out after 40s. Continuing anyway...");
                return true;
            }
        }
    }

    private static void CheckWebView2Runtime()
    {
        try
        {
            string ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
            Console.WriteLine("[HealthCheck] Edge WebView2 Runtime: Detected version " + ver);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[HealthCheck] Warning: Edge WebView2 Runtime not detected: " + ex.Message);
        }
    }
}
