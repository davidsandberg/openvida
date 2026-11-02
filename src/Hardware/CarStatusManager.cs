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
    static object _cachedVehicleProfile = null;
    static object _cachedVehInfoInstance = null;
    static bool _carStatusEventHooked = false;
    public static Label TopBarCarStatusLabel = null;
    static double _lastKnownVoltVal = 0.0;
    static Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition _lastKnownKeyPos = Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0;
    static Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Disconnected;
    public static event Action OnCarStatusChanged;

    public static bool IsDiceConnected
    {
        get
        {
            return !IsMockSimulation && _lastKnownState == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected && _lastKnownVoltVal > 5.0;
        }
    }

    static void UpdateAllCarStatusControls(double voltVal)
    {
        if (voltVal <= 0.0) return;
        string text = voltVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " V";
        try
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f == null || f.IsDisposed) continue;
                Action act = () =>
                {
                    try
                    {
                        Control[] labels = f.Controls.Find("_supplyLabel", true);
                        foreach (Control c in labels)
                        {
                            Label sl = c as Label;
                            if (sl != null && !sl.IsDisposed)
                            {
                                if (sl.Tag == null || (string)sl.Tag != "hooked")
                                {
                                    sl.Tag = "hooked";
                                    sl.TextChanged += (sLabel, eLabel) =>
                                    {
                                        Label lbl = sLabel as Label;
                                        if (lbl != null && !string.IsNullOrEmpty(lbl.Text))
                                        {
                                            string txt = lbl.Text.Trim();
                                            string num = txt.Replace("V", "").Replace("v", "").Trim().Replace(",", ".");
                                            double v;
                                            if (double.TryParse(num, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
                                            {
                                                if (v > 50.0)
                                                {
                                                    double corrected = v / 10.0;
                                                    if (corrected > 50.0) corrected = corrected / 10.0;
                                                    string newTxt = corrected.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " V";
                                                    if (lbl.Text != newTxt) lbl.Text = newTxt;
                                                }
                                            }
                                        }
                                    };
                                }
                                if (sl.Text != text)
                                {
                                    sl.Text = text;
                                }
                            }
                        }
                    }
                    catch {}
                };
                if (f.InvokeRequired) f.BeginInvoke(act);
                else act();
            }
        }
        catch {}
    }

    static void OnGlobalCarStatusUpdated(
        Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState state, 
        string supply, 
        Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition keypos)
    {
        try
        {
            double voltVal = 0.0;
            if (!string.IsNullOrEmpty(supply))
            {
                string clean = supply.Trim().Replace(",", ".");
                if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out voltVal))
                {
                    if (voltVal > 50.0) voltVal = voltVal / 100.0;
                    if (voltVal > 50.0) voltVal = voltVal / 10.0;
                }
            }
            if (voltVal > 0.0) _lastKnownVoltVal = voltVal;
            _lastKnownKeyPos = keypos;
            _lastKnownState = state;

            bool isConn = (state == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected);
            string diceText = isConn
                ? string.Format("🟢 {0} (connected)", TargetCommTool)
                : string.Format("⚪ {0} (disconnected)", TargetCommTool);
            Color fg = isConn ? Color.FromArgb(22, 101, 52) : Color.FromArgb(100, 110, 120);

            if (TopBarCarStatusLabel != null && !TopBarCarStatusLabel.IsDisposed)
            {
                TopBarCarStatusLabel.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        TopBarCarStatusLabel.Text = diceText;
                        TopBarCarStatusLabel.ForeColor = fg;
                    }
                    catch {}
                }));
            }

            UpdateAllCarStatusControls(_lastKnownVoltVal);
            if (OnCarStatusChanged != null)
            {
                try { OnCarStatusChanged(); } catch {}
            }
        }
        catch {}
    }

    static void InitializeCarStatus(object vehInfoInstance, object vehProfile)
    {
        try
        {
            if (vehInfoInstance == null) return;
            Type viType = vehInfoInstance.GetType();

            FieldInfo viInitField = viType.GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (viInitField != null) viInitField.SetValue(vehInfoInstance, true);

            Type cstf = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Components.CarStatusTabForm");
            FieldInfo csField = cstf != null ? cstf.GetField("_carStatus", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            object carStatus = csField != null ? csField.GetValue(vehInfoInstance) : null;
            if (carStatus == null) return;

            Type csType = carStatus.GetType();
            FieldInfo cswfField = csType.GetField("_carStatusWF", BindingFlags.NonPublic | BindingFlags.Instance);
            object cswf = cswfField != null ? cswfField.GetValue(carStatus) : null;
            if (cswf == null) return;

            FieldInfo wsField = cswf.GetType().GetField("_workshopSession", BindingFlags.NonPublic | BindingFlags.Instance);
            object ws = wsField != null ? wsField.GetValue(cswf) : null;
            if (ws != null)
            {
                SetProp(ws.GetType(), ws, "AllowVehicleCommunication", true);
                SetProp(ws.GetType(), ws, "AllowUpdate", true);
                if (vehProfile != null)
                {
                    SetProp(ws.GetType(), ws, "VehicleProfileObject", vehProfile);
                    SetFieldOrProp(ws, "_vehicleProfileObject", vehProfile);
                }
            }

            PropertyInfo disProp = cswf.GetType().GetProperty("Disable");
            if (disProp != null) disProp.SetValue(cswf, false, null);

            EventInfo evUpdated = cswf.GetType().GetEvent("CarStatusUpdated");
            if (evUpdated != null && !_carStatusEventHooked)
            {
                _carStatusEventHooked = true;
                MethodInfo miHandler = typeof(Program).GetMethod("OnGlobalCarStatusUpdated", BindingFlags.NonPublic | BindingFlags.Static);
                if (miHandler != null)
                {
                    Delegate d = Delegate.CreateDelegate(evUpdated.EventHandlerType, miHandler);
                    evUpdated.AddEventHandler(cswf, d);
                }
            }

            Control csCtrl = carStatus as Control;
            if (csCtrl != null && csCtrl.Tag == null)
            {
                csCtrl.Tag = "refresh_attached";
                Action refreshAct = () =>
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            Console.WriteLine("[Launcher] Rechecking CarStatus (VBatt & Keypos)...");
                            MethodInfo miRead = cswf.GetType().GetMethod("ReadSupplyKeypos", BindingFlags.Public | BindingFlags.Instance);
                            if (miRead != null) miRead.Invoke(cswf, null);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[Launcher] CarStatus read error: " + ex.Message);
                        }
                    });
                };
                Action<Control> attachClick = null;
                attachClick = (c) =>
                {
                    c.Cursor = Cursors.Hand;
                    c.Click += (s, e) => refreshAct();
                    foreach (Control child in c.Controls) attachClick(child);
                };
                attachClick(csCtrl);
            }

            MethodInfo miStart = csType.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
            if (miStart != null)
            {
                miStart.Invoke(carStatus, null);
                Console.WriteLine("[Launcher] CarStatus.Start() invoked successfully!");
            }

            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    System.Threading.Thread.Sleep(200);
                    MethodInfo miRead = cswf.GetType().GetMethod("ReadSupplyKeypos", BindingFlags.Public | BindingFlags.Instance);
                    if (miRead != null) miRead.Invoke(cswf, null);
                }
                catch {}
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] InitializeCarStatus error: " + ex.Message);
        }
    }

}
