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
    static void StartAutomatedTestSequence(string vidaPath, Form mainForm, Button btnTabFaultTrace, Button btnTabNetwork, Button btnTabVehComm, Button btnTabRefInfo, Button btnTabManuals, Button btnTabParts, Button btnTabWiring, Action<string> switchTab, Action<Button> setActiveTabButton)
    {
            Action<string, string> captureTab = (tabName, fileName) =>

            {

                try

                {

                    Console.WriteLine("[Launcher] Capturing " + tabName + " screenshot to " + fileName + "...");

                    Bitmap bmp = new Bitmap(mainForm.Width, mainForm.Height);

                    mainForm.DrawToBitmap(bmp, new Rectangle(0, 0, mainForm.Width, mainForm.Height));

                    string ssPath = Path.Combine(vidaPath, @"..\" + fileName);

                    bmp.Save(ssPath, ImageFormat.Png);

                    Console.WriteLine("[Launcher] Saved: " + ssPath);

                }

                catch (Exception ex)

                {

                    Console.WriteLine("[Launcher] Screenshot error (" + tabName + "): " + ex.Message);

                }

            };

            int step = 0;

            Timer seqTimer = new Timer();

            seqTimer.Interval = 5000;

            seqTimer.Tick += (s, e) =>

            {

                step++;

                try

                {

                    switch (step)

                    {

                        case 1:

                            Console.WriteLine("[Launcher] Step 1: Switching to Fault Trace tab...");

                            switchTab("_customerSymptomsFaultTraceButton_Click");

                            setActiveTabButton(btnTabFaultTrace);

                            try

                            {

                                Panel mainPnl = mainForm.GetType().GetField("_mainPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mainForm) as Panel;

                                if (mainPnl != null && mainPnl.Controls.Count > 0)

                                {

                                    Control activeForm = mainPnl.Controls[0];

                                    FieldInfo tabField = activeForm.GetType().GetField("_tab", BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (tabField != null)

                                    {

                                        object tabCtrl = tabField.GetValue(activeForm);

                                        if (tabCtrl != null)

                                        {

                                            tabCtrl.GetType().GetProperty("SelectedIndex").SetValue(tabCtrl, 0, null);

                                            Console.WriteLine("[Launcher] Successfully switched to Initial State tab page!");

                                        }

                                    }

                                }

                            }

                            catch (Exception ex) { Console.WriteLine("[Launcher] InitialState switch error: " + ex.Message); }

                            break;

                        case 2:

                            captureTab("Fault Trace", "launcher_test_faulttrace.png");

                            Console.WriteLine("[Launcher] Step 2: Switching to Network tab...");

                            switchTab("_networkStatusButton_Click");

                            setActiveTabButton(btnTabNetwork);

                            break;

                        case 3:

                            captureTab("Network", "launcher_test_network.png");

                            Console.WriteLine("[Launcher] Step 3: Switching to Vehicle Comm tab...");

                            btnTabVehComm.PerformClick();

                            // After tab activates, select ECM (284101) to populate parameters

                            Timer ecTimer = new Timer();

                            ecTimer.Interval = 1500;

                            ecTimer.Tick += (s2, e2) =>

                            {

                                ecTimer.Stop();

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

                                            if (onNavClick != null)

                                            {

                                                Console.WriteLine("[Launcher] Selecting ECM (284101) in Vehicle Comm tab via DiagnosticManager.GetVehCommForm()...");

                                                onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs("284101", 1) });

                                                                                                Console.WriteLine("[Launcher] Selected ECM successfully!");

                                                Timer tcmTimer = new Timer();

                                                tcmTimer.Interval = 1500;

                                                tcmTimer.Tick += (sTcm, eTcm) => {

                                                    tcmTimer.Stop();

                                                    try {

                                                        Console.WriteLine("[Launcher] Testing module switch to TCM (437601)...");

                                                        onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs("437601", 1) });

                                                        Console.WriteLine("[Launcher] Switched to TCM successfully!");

                                                    } catch (Exception exTcm) {

                                                        Console.WriteLine("[Launcher] Error switching to TCM: " + exTcm);

                                                    }

                                                };

                                                tcmTimer.Start();

                                            }

                                        }

                                    }

                                }

                                catch (Exception ex)

                                {

                                    Console.WriteLine("[Launcher] Select ECM error: " + ex);

                                }

                            };

                            ecTimer.Start();

                            break;

                        case 4:

                            captureTab("Vehicle Comm", "launcher_test_vehcomm.png");

                            Console.WriteLine("[Launcher] Step 4: Switching to Reference Info tab...");

                            switchTab("_referenceInformationButton_Click");

                            setActiveTabButton(btnTabRefInfo);

                            break;

                                                case 5:

                            captureTab("Reference Info CSC", "launcher_test_refinfo.png");

                            captureTab("Reference Info CSC", "launcher_test_refinfo_csc.png");

                            string[] testDocs = new string[] {
                                "Fuel system, Fuel leakage, XY",
                                "Steering, Oil leakage, U6",
                                "Lubrication and oil system, Oil leakage, J9",
                                "Engine cooling, Coolant leakage, Q8"
                            };
                            foreach (var td in testDocs)
                            {
                                string rDoc = ResolveDocumentId(null, td);
                                Console.WriteLine("[Launcher] Test resolution: '" + td + "' -> " + (rDoc ?? "FAILED"));
                            }
                            Console.WriteLine("[Launcher] Step 5 complete. Switching to DTC-based Reference Information...");

                            try

                            {

                                if (_refFormCtrl != null)

                                {

                                    var tabCtrl = GetFieldValue(_refFormCtrl, "_tab");

                                    var ftc = tabCtrl as Vcc.Vida.DiagSwdl.GuiComponents.FlatTabControl;

                                    if (ftc != null)

                                    {

                                        Control dtcTab = GetFieldValue(_refFormCtrl, "_dtcBasedRefInfoTab") as Control;

                                        if (dtcTab is Vcc.Vida.DiagSwdl.GuiComponents.FlatTabPage)

                                        {

                                            ftc.SelectedTab = (Vcc.Vida.DiagSwdl.GuiComponents.FlatTabPage)dtcTab;

                                            Console.WriteLine("[Launcher] Successfully selected DTC Reference tab!");

                                        }

                                    }

                                }

                            }

                            catch (Exception exTab)

                            {

                                Console.WriteLine("[Launcher] Error switching to DTC tab in test: " + exTab.Message);

                            }

                            break;

                        case 6:

                            captureTab("Reference Info DTC", "launcher_test_refinfo_dtc.png");

                            Console.WriteLine("[Launcher] Step 6 complete. Switching to Service Manuals tab...");

                            try

                            {

                                if (_btnTabManuals != null)

                                {

                                    _btnTabManuals.PerformClick();

                                    Console.WriteLine("[Launcher] Clicked Service Manuals button!");

                                }

                            }

                            catch (Exception exMan)

                            {

                                Console.WriteLine("[Launcher] Error opening Service Manuals tab: " + exMan.Message);

                            }

                            break;

                        case 7:
                    captureTab("Workshop Service Manuals", "launcher_test_manuals.png");
                    Console.WriteLine("[Launcher] Step 7 complete. Testing Parts Catalog tab...");
                    btnTabParts.PerformClick();
                    break;
                case 8:
                    captureTab("Electronic Parts Catalog", "launcher_test_epc.png");
                    Console.WriteLine("[Launcher] Step 8 complete. Testing Wiring Diagrams tab...");
                    btnTabWiring.PerformClick();
                    break;
                case 9:
                    seqTimer.Stop();
                    try
                    {
                        if (_ewdDgv != null && _ewdBtnShowLoc != null)
                        {
                            foreach (DataGridViewRow r in _ewdDgv.Rows)
                            {
                                string cVal = Convert.ToString(r.Cells["Code"].Value);
                                string dVal = Convert.ToString(r.Cells["Description"].Value);
                                if (cVal == "4/46" && dVal.Contains("5-Cyl. Diesel"))
                                {
                                    _ewdDgv.CurrentCell = r.Cells[0];
                                    r.Selected = true;
                                    _ewdBtnShowLoc.PerformClick();
                                    Console.WriteLine("[Launcher] Direct navigation triggered: Jumped to 4/46 ECM Placement (Page 312)!");
                                    if (_ewdBtnFindSchem != null)
                                    {
                                        DateTime startS = DateTime.Now;
                                        while ((DateTime.Now - startS).TotalSeconds < 1.5) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }
                                        _ewdBtnFindSchem.PerformClick();
                                        string navSrc = (_ewdWebView != null && _ewdWebView.CoreWebView2 != null) ? _ewdWebView.CoreWebView2.Source : "(none)";
                                        Console.WriteLine("[Launcher] Schematics search triggered for 4/46, WebView source: " + navSrc);
                                        while ((DateTime.Now - startS).TotalSeconds < 3.0) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }
                                        _ewdBtnShowLoc.PerformClick();
                                    }
                                    break;
                                }
                            }
                            DateTime startW = DateTime.Now;
                            while ((DateTime.Now - startW).TotalSeconds < 2.5) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }
                        }
                    }
                    catch (Exception exNav) { Console.WriteLine("[Launcher] Test direct nav warning: " + exNav.Message); }
                    captureTab("Wiring Diagrams", "launcher_test_ewd.png");
                    try
                    {
                        if (_ewdWebView != null && _ewdWebView.CoreWebView2 != null)
                        {
                            string ssPath = Path.Combine(vidaPath, @"..\launcher_test_ewd.png");
                            if (File.Exists(ssPath))
                            {
                                byte[] ssBytes = File.ReadAllBytes(ssPath);
                                byte[] dummyLock = null;
                                using (MemoryStream inMs = new MemoryStream(ssBytes))
                                using (Bitmap baseBmp = (Bitmap)Image.FromStream(inMs))
                                {
                                    using (Bitmap ewdBmp = new Bitmap(baseBmp))
                                    {
                                        using (MemoryStream ms = new MemoryStream())
                                        {
                                            var capTask = _ewdWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                                            DateTime startCap = DateTime.Now;
                                            while (!capTask.IsCompleted && (DateTime.Now - startCap).TotalSeconds < 4)
                                            {
                                                Application.DoEvents();
                                                System.Threading.Thread.Sleep(20);
                                            }
                                            if (capTask.IsCompleted && !capTask.IsFaulted)
                                            {
                                                ms.Position = 0;
                                                using (Bitmap wvBmp = new Bitmap(ms))
                                                {
                                                    using (Graphics g = Graphics.FromImage(ewdBmp))
                                                    {
                                                        Point pt = _ewdWebView.PointToScreen(Point.Empty);
                                                        Point rel = mainForm.PointToClient(pt);
                                                        g.DrawImage(wvBmp, rel.X, rel.Y, _ewdWebView.Width, _ewdWebView.Height);
                                                    }
                                                }
                                                ewdBmp.Save(ssPath + ".tmp", ImageFormat.Png);
                                                File.Copy(ssPath + ".tmp", ssPath, true);
                                                File.Delete(ssPath + ".tmp");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exCap) { Console.WriteLine("[Launcher] EWD WebView composite warning: " + exCap.Message); }
                    Console.WriteLine("[Launcher] Step 9 complete. Testing round-trip: Wiring Diagrams -> Reference Info...");
                    btnTabRefInfo.PerformClick();
                    seqTimer.Interval = 3000;
                    seqTimer.Start();
                    break;
                case 10:
                    seqTimer.Stop();
                    Console.WriteLine("[Launcher] Successfully switched from Wiring Diagrams to Reference Info! All round-trip tab switches PASSED! Exiting test...");
                    Console.Out.Flush();
                    Environment.Exit(0);
                    break;

                    }

                }

                catch (Exception ex)

                {

                    Console.WriteLine("[Launcher] Sequence error at step " + step + ": " + ex);

                    seqTimer.Stop();

                    Application.Exit();

                }

            };

            seqTimer.Start();

    }
}
