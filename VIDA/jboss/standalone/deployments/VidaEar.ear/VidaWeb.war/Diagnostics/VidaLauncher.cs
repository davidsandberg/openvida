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

        mainForm.Load += (s, e) =>
        {
            try
            {
                Console.WriteLine("[Launcher] Calling diag.LoadControl()...");
                diag.LoadControl();
                Console.WriteLine("[Launcher] diag.LoadControl() succeeded!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Launcher] Exception during LoadControl: " + ex);
                MessageBox.Show("Error initializing Diagnostics: " + ex.Message, "VIDA Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
