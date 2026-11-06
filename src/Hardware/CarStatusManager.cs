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
    static object _cachedCarStatusWF = null;
    static object _cachedCarStatusCtrl = null;
    static bool _carStatusEventHooked = false;
    public static Label TopBarCarStatusLabel = null;
    public static Button TopBarDiceConnectButton = null;
    static double _lastKnownVoltVal = 0.0;
    static Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition _lastKnownKeyPos = Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0;
    static Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Disconnected;
    public static event Action OnCarStatusChanged;

    static System.Threading.Timer _reconnectProbeTimer = null;
    static System.Threading.Timer _batteryPollTimer = null;
    static bool _isProbingReconnect = false;
    static bool _userManuallyDisconnected = false;

    public static bool IsDiceConnected
    {
        get
        {
            return !IsMockSimulation && _lastKnownState == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected && _lastKnownVoltVal > 5.0;
        }
    }

    public static void StopActiveParameterReading()
    {
        try
        {
            if (_activeVehicleCommunicationWf != null)
            {
                MethodInfo miStop = _activeVehicleCommunicationWf.GetType().GetMethod("StopReading", BindingFlags.Public | BindingFlags.Instance);
                if (miStop != null) miStop.Invoke(_activeVehicleCommunicationWf, null);

                FieldInfo tField = _activeVehicleCommunicationWf.GetType().GetField("_parameterReadTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tField != null)
                {
                    object tmr = tField.GetValue(_activeVehicleCommunicationWf);
                    if (tmr != null)
                    {
                        MethodInfo stopM = tmr.GetType().GetMethod("Stop");
                        if (stopM != null) stopM.Invoke(tmr, null);
                    }
                }
            }
        }
        catch {}
    }

    static void StartReconnectProbeTimer()
    {
        if (_userManuallyDisconnected) return;
        lock (typeof(Program))
        {
            if (_reconnectProbeTimer == null)
            {
                _reconnectProbeTimer = new System.Threading.Timer(state =>
                {
                    if (_userManuallyDisconnected || _lastKnownState == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected)
                    {
                        return;
                    }
                    if (_isProbingReconnect) return;
                    _isProbingReconnect = true;
                    try
                    {
                        if (_cachedCarStatusWF != null)
                        {
                            MethodInfo miRead = _cachedCarStatusWF.GetType().GetMethod("ReadSupplyKeypos", BindingFlags.Public | BindingFlags.Instance);
                            if (miRead != null) miRead.Invoke(_cachedCarStatusWF, null);
                        }
                    }
                    catch {}
                    finally
                    {
                        _isProbingReconnect = false;
                    }
                }, null, 3000, 3000);
            }
            else
            {
                try { _reconnectProbeTimer.Change(3000, 3000); } catch {}
            }
        }
    }

    static void StopReconnectProbeTimer()
    {
        lock (typeof(Program))
        {
            if (_reconnectProbeTimer != null)
            {
                try { _reconnectProbeTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite); } catch {}
            }
        }
    }

    static void UpdateTopBarStatus(string text, Color fg, bool isConn)
    {
        if (TopBarCarStatusLabel != null && !TopBarCarStatusLabel.IsDisposed)
        {
            try
            {
                Action act = () =>
                {
                    try
                    {
                        TopBarCarStatusLabel.Text = text;
                        TopBarCarStatusLabel.ForeColor = fg;
                        if (TopBarDiceConnectButton != null && !TopBarDiceConnectButton.IsDisposed)
                        {
                            TopBarDiceConnectButton.Text = isConn ? "Disconnect" : "Connect";
                            TopBarDiceConnectButton.BackColor = isConn ? Color.FromArgb(254, 242, 242) : Color.FromArgb(230, 240, 250);
                            TopBarDiceConnectButton.ForeColor = isConn ? Color.FromArgb(185, 28, 28) : Color.FromArgb(0, 102, 204);
                            TopBarDiceConnectButton.FlatAppearance.BorderColor = isConn ? Color.FromArgb(248, 180, 180) : Color.FromArgb(160, 200, 240);
                        }
                    }
                    catch {}
                };
                if (TopBarCarStatusLabel.InvokeRequired) TopBarCarStatusLabel.BeginInvoke(act);
                else act();
            }
            catch {}
        }
    }

    public static void SyncMockSimulationStatus()
    {
        if (IsMockSimulation)
        {
            _lastKnownVoltVal = 12.6;
            _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected;
            string diceText = string.Format("🟢 {0}  |  ⚡ 12.6 V", TargetCommTool);
            Color fg = Color.FromArgb(22, 101, 52);
            UpdateTopBarStatus(diceText, fg, true);
            UpdateAllCarStatusControls(12.6);
            if (OnCarStatusChanged != null)
            {
                try { OnCarStatusChanged(); } catch {}
            }
        }
        else
        {
            if (_cachedVehInfoInstance != null)
            {
                InitializeCarStatus(_cachedVehInfoInstance, _cachedVehicleProfile);
            }
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

    public static void OnGlobalCarStatusUpdated(
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

            bool isConn = (state == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected && (voltVal > 5.0 || _lastKnownVoltVal > 5.0));

            if (isConn)
            {
                if (voltVal > 0.0) _lastKnownVoltVal = voltVal;
                _lastKnownKeyPos = keypos;
                _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected;

                StopReconnectProbeTimer();
                Logger.ResetCommErrorSuppression();

                string voltStr = _lastKnownVoltVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " V";
                string diceText;
                Color fg;

                if (_lastKnownVoltVal >= 12.0)
                {
                    diceText = string.Format("🟢 {0}  |  ⚡ {1}", TargetCommTool, voltStr);
                    fg = Color.FromArgb(22, 101, 52); // Normal healthy voltage
                }
                else if (_lastKnownVoltVal >= 11.5)
                {
                    diceText = string.Format("⚠️ {0}  |  ⚡ {1}", TargetCommTool, voltStr);
                    fg = Color.FromArgb(180, 83, 9); // Low warning
                }
                else
                {
                    diceText = string.Format("🛑 {0}  |  ⚡ {1} (Low!)", TargetCommTool, voltStr);
                    fg = Color.FromArgb(185, 28, 28); // Critical low voltage
                }

                UpdateTopBarStatus(diceText, fg, true);
                UpdateAllCarStatusControls(_lastKnownVoltVal);
            }
            else
            {
                _lastKnownVoltVal = 0.0;
                _lastKnownKeyPos = Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0;
                _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Disconnected;

                // Stop Volvo's rapid reader timer so it doesn't repeatedly throw exceptions
                if (_cachedCarStatusWF != null)
                {
                    try
                    {
                        MethodInfo miStop = _cachedCarStatusWF.GetType().GetMethod("StopSupplyKeyposRead", BindingFlags.Public | BindingFlags.Instance);
                        if (miStop != null) miStop.Invoke(_cachedCarStatusWF, null);
                    }
                    catch {}
                }

                // Stop parameter read timer if running
                StopActiveParameterReading();

                string diceText = string.Format("⚪ {0}", TargetCommTool);
                Color fg = Color.FromArgb(100, 110, 120);

                UpdateTopBarStatus(diceText, fg, false);
                UpdateAllCarStatusControls(0.0);

                // Start throttled reconnection probing
                StartReconnectProbeTimer();
            }

            if (OnCarStatusChanged != null)
            {
                try { OnCarStatusChanged(); } catch {}
            }
        }
        catch {}
    }

    static void DisconnectCarStatus()
    {
        _userManuallyDisconnected = true;
        StopReconnectProbeTimer();

        try
        {
            if (_cachedCarStatusWF != null)
            {
                MethodInfo miStop = _cachedCarStatusWF.GetType().GetMethod("StopSupplyKeyposRead", BindingFlags.Public | BindingFlags.Instance);
                if (miStop != null) miStop.Invoke(_cachedCarStatusWF, null);
            }
            if (_cachedCarStatusCtrl != null)
            {
                MethodInfo miStop = _cachedCarStatusCtrl.GetType().GetMethod("Stop", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (miStop != null) miStop.Invoke(_cachedCarStatusCtrl, null);
            }
        }
        catch {}

        StopActiveParameterReading();
        _lastKnownVoltVal = 0.0;
        OnGlobalCarStatusUpdated(Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Disconnected, "0.0", Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0);
        Logger.Info("CarStatus", "DiCE disconnected by user.");
    }

    static void InitializeCarStatus(object vehInfoInstance, object vehProfile)
    {
        try
        {
            _userManuallyDisconnected = false;
            if (vehInfoInstance == null) return;
            _cachedVehInfoInstance = vehInfoInstance;
            _cachedVehicleProfile = vehProfile;

            Type viType = vehInfoInstance.GetType();

            FieldInfo viInitField = viType.GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (viInitField != null) viInitField.SetValue(vehInfoInstance, true);

            Type cstf = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Components.CarStatusTabForm");
            FieldInfo csField = cstf != null ? cstf.GetField("_carStatus", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            object carStatus = csField != null ? csField.GetValue(vehInfoInstance) : null;
            if (carStatus == null) return;
            _cachedCarStatusCtrl = carStatus;

            Type csType = carStatus.GetType();
            FieldInfo cswfField = csType.GetField("_carStatusWF", BindingFlags.NonPublic | BindingFlags.Instance);
            object cswf = cswfField != null ? cswfField.GetValue(carStatus) : null;
            if (cswf == null) return;
            _cachedCarStatusWF = cswf;

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
                MethodInfo miHandler = typeof(Program).GetMethod("OnGlobalCarStatusUpdated", BindingFlags.Public | BindingFlags.Static);
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

            // Periodic voltage polling to continuously refresh the top-line battery display
            if (_batteryPollTimer == null)
            {
                _batteryPollTimer = new System.Threading.Timer(state =>
                {
                    try
                    {
                        if (IsDiceConnected && !_isProbingReconnect && _cachedCarStatusWF != null)
                        {
                            MethodInfo miRead = _cachedCarStatusWF.GetType().GetMethod("ReadSupplyKeypos", BindingFlags.Public | BindingFlags.Instance);
                            if (miRead != null) miRead.Invoke(_cachedCarStatusWF, null);
                        }
                    }
                    catch {}
                }, null, 2500, 2500);
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
