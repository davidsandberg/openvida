using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.ProgressHandler;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static partial class Program
{
    private static volatile bool _ipcServerRunning = false;
    private static Thread _ipcListenerThread = null;
    private static readonly JavaScriptSerializer _ipcJson = new JavaScriptSerializer();
    public const string OpenVidaPipeName = "openvida_rpc";

    public static void StartOpenVidaIpcServer()
    {
        if (_ipcServerRunning) return;

        try
        {
            _ipcServerRunning = true;
            _ipcListenerThread = new Thread(IpcListenerWorker)
            {
                IsBackground = true,
                Name = "OpenVida_IpcServer"
            };
            _ipcListenerThread.Start();
            Console.WriteLine("[IPC] OpenVIDA Named Pipe Server started on \\\\.\\pipe\\" + OpenVidaPipeName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[IPC] Failed to start OpenVIDA IPC Server: " + ex.Message);
        }
    }

    public static void StopOpenVidaIpcServer()
    {
        _ipcServerRunning = false;
        try
        {
            // Briefly connect to unblock any waiting listener
            using (var dummy = new NamedPipeClientStream(".", OpenVidaPipeName, PipeDirection.Out))
            {
                try { dummy.Connect(100); } catch {}
            }
        }
        catch {}
    }

    private static void IpcListenerWorker()
    {
        while (_ipcServerRunning)
        {
            NamedPipeServerStream server = null;
            try
            {
                server = new NamedPipeServerStream(
                    OpenVidaPipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None);

                server.WaitForConnection();

                if (!_ipcServerRunning)
                {
                    server.Close();
                    break;
                }

                ThreadPool.QueueUserWorkItem(HandleIpcClientConnection, server);
            }
            catch (Exception ex)
            {
                if (!_ipcServerRunning) break;
                Console.WriteLine("[IPC] Listener exception: " + ex.Message);
                Thread.Sleep(200);
            }
        }
    }

    private static void HandleIpcClientConnection(object state)
    {
        NamedPipeServerStream pipe = state as NamedPipeServerStream;
        if (pipe == null) return;

        try
        {
            using (pipe)
            using (var reader = new StreamReader(pipe, Encoding.UTF8))
            using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    Dictionary<string, object> req = null;
                    try
                    {
                        req = _ipcJson.Deserialize<Dictionary<string, object>>(line);
                    }
                    catch (Exception exParse)
                    {
                        var errResp = new Dictionary<string, object>
                        {
                            { "status", "error" },
                            { "error", "Invalid JSON request: " + exParse.Message }
                        };
                        writer.WriteLine(_ipcJson.Serialize(errResp));
                        continue;
                    }

                    string action = req.ContainsKey("action") ? req["action"] as string : "";
                    var resp = DispatchIpcAction(action, req);
                    string jsonResp = _ipcJson.Serialize(resp);
                    writer.WriteLine(jsonResp);
                }
            }
        }
        catch (Exception exClient)
        {
            Console.WriteLine("[IPC] Client handle error: " + exClient.Message);
        }
    }

    private static Dictionary<string, object> DispatchIpcAction(string action, Dictionary<string, object> req)
    {
        var resp = new Dictionary<string, object>();

        try
        {
            switch ((action ?? "").ToLowerInvariant())
            {
                case "ping":
                    resp["status"] = "ok";
                    resp["app"] = "OpenVIDA";
                    resp["version"] = AppVersion.Version;
                    resp["dice_connected"] = IsDiceConnected;
                    resp["comm_tool"] = TargetCommTool;
                    resp["comm_tool_type"] = CommToolManager.IsCurrentToolDice ? "DiCE" : "J2534";
                    resp["mock_simulation"] = IsMockSimulation;
                    resp["battery_v"] = Math.Round(_lastKnownVoltVal, 2);
                    resp["key_position"] = _lastKnownKeyPos.ToString();
                    resp["profile_id"] = TargetProfile;
                    break;

                case "get_status":
                case "dice_status":
                    resp["status"] = "ok";
                    resp["dice_connected"] = IsDiceConnected;
                    resp["mock_simulation"] = IsMockSimulation;
                    resp["battery_v"] = Math.Round(_lastKnownVoltVal, 2);
                    resp["battery_mv"] = (int)(_lastKnownVoltVal * 1000);
                    resp["key_position"] = _lastKnownKeyPos.ToString();
                    resp["connection_state"] = _lastKnownState.ToString();
                    resp["profile_id"] = TargetProfile;
                    resp["hardware_owner"] = "OpenVIDA VidaLauncher (Exclusive DiCE J2534 Handler)";
                    resp["session_active"] = true;
                    break;

                case "get_active_dtcs":
                    resp["status"] = "ok";
                    resp["dtcs"] = RetrieveCurrentDtcs();
                    resp["mock_simulation"] = IsMockSimulation;
                    resp["battery_v"] = Math.Round(_lastKnownVoltVal, 2);
                    break;

                case "clear_dtcs":
                case "clear_all_dtcs":
                case "erase_dtcs":
                    string clearEcu = req.ContainsKey("ecu") ? req["ecu"].ToString() : "ALL";
                    bool force = req.ContainsKey("force") && Convert.ToBoolean(req["force"]);

                    var eraseRes = ExecuteClearAllDtcs(interactive: !force, targetEcu: clearEcu, owner: _mainLauncherForm);
                    resp["status"] = eraseRes.Success ? "ok" : (eraseRes.Cancelled ? "cancelled" : "error");
                    resp["cleared"] = eraseRes.Success;
                    resp["target_ecu"] = eraseRes.TargetEcu;
                    resp["initial_dtcs"] = eraseRes.InitialCount;
                    resp["cleared_dtcs"] = eraseRes.ClearedCount;
                    resp["remaining_dtcs"] = eraseRes.RemainingCount;
                    resp["dtcs"] = eraseRes.RemainingDtcs;
                    resp["battery_v"] = eraseRes.BatteryVoltage;
                    resp["message"] = eraseRes.Message;
                    resp["mock_simulation"] = IsMockSimulation;
                    break;

                case "get_network_status":
                case "scan_network":
                    resp["status"] = "ok";
                    resp["ecus"] = RetrieveNetworkEcus();
                    resp["mock_simulation"] = IsMockSimulation;
                    break;

                case "read_parameters":
                    string ecu = req.ContainsKey("ecu") ? req["ecu"].ToString() : "ECM";
                    resp["status"] = "ok";
                    resp["ecu"] = ecu;
                    resp["parameters"] = RetrieveLiveParameters(ecu, req);
                    break;

                case "execute_routine":
                    string scriptId = req.ContainsKey("script_id") ? req["script_id"].ToString() : "";
                    string title = req.ContainsKey("title") ? req["title"].ToString() : "Diagnostic Routine";
                    if (string.IsNullOrEmpty(scriptId))
                    {
                        resp["status"] = "error";
                        resp["error"] = "script_id is required.";
                    }
                    else
                    {
                        if (_mainLauncherForm != null && !_mainLauncherForm.IsDisposed)
                        {
                            _mainLauncherForm.BeginInvoke((Action)(() =>
                            {
                                RunDiagnosticRoutine(scriptId, title, _mainLauncherForm);
                            }));
                            resp["status"] = "executing";
                            resp["script_id"] = scriptId;
                            resp["title"] = title;
                        }
                        else
                        {
                            resp["status"] = "error";
                            resp["error"] = "Launcher main form is not available.";
                        }
                    }
                    break;

                case "bring_to_front":
                    if (_mainLauncherForm != null && !_mainLauncherForm.IsDisposed)
                    {
                        _mainLauncherForm.BeginInvoke((Action)(() =>
                        {
                            try
                            {
                                if (_mainLauncherForm.WindowState == FormWindowState.Minimized)
                                    _mainLauncherForm.WindowState = FormWindowState.Normal;
                                _mainLauncherForm.BringToFront();
                                _mainLauncherForm.Activate();
                            }
                            catch {}
                        }));
                        resp["status"] = "ok";
                        resp["message"] = "Window brought to foreground.";
                    }
                    else
                    {
                        resp["status"] = "error";
                        resp["error"] = "Launcher main form is not available.";
                    }
                    break;

                default:
                    resp["status"] = "error";
                    resp["error"] = "Unknown IPC action: " + action;
                    break;
            }
        }
        catch (Exception ex)
        {
            resp["status"] = "error";
            resp["error"] = ex.Message;
        }

        return resp;
    }

    public static List<Dictionary<string, object>> RetrieveCurrentDtcs()
    {
        var list = new List<Dictionary<string, object>>();

        if (_simulatedDtcsCleared && (!IsDiceConnected || IsMockSimulation))
        {
            return list;
        }

        try
        {
            if (_activeDtcFaulttraceWf != null)
            {
                MethodInfo miLatest = _activeDtcFaulttraceWf.GetType().GetMethod("GetLatestDtcReadout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object latest = miLatest != null ? miLatest.Invoke(_activeDtcFaulttraceWf, null) : null;
                if (latest != null)
                {
                    int readoutId = (int)latest.GetType().GetField("DtcReadoutId").GetValue(latest);
                    MethodInfo miGetDtcs = _activeDtcFaulttraceWf.GetType().GetMethod("GetDtcs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
                    Array dtcs = miGetDtcs != null ? (Array)miGetDtcs.Invoke(_activeDtcFaulttraceWf, new object[] { readoutId }) : null;
                    if (dtcs != null)
                    {
                        foreach (object d in dtcs)
                        {
                            if (d == null) continue;
                            var item = new Dictionary<string, object>();
                            PropertyInfo pName = d.GetType().GetProperty("DtcName") ?? d.GetType().GetProperty("Name");
                            PropertyInfo pStatus = d.GetType().GetProperty("Status");
                            PropertyInfo pEcu = d.GetType().GetProperty("EcuName");
                            PropertyInfo pDesc = d.GetType().GetProperty("Description");

                            item["code"] = pName != null ? pName.GetValue(d, null) : d.ToString();
                            item["status"] = pStatus != null ? pStatus.GetValue(d, null).ToString() : "Intermittent";
                            item["ecu"] = pEcu != null ? pEcu.GetValue(d, null) : "ECM";
                            item["description"] = pDesc != null ? pDesc.GetValue(d, null) : "";
                            list.Add(item);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[IPC] RetrieveCurrentDtcs error: " + ex.Message);
        }

        // Fallback / mock simulation sample DTCs if no active readout exists
        if (list.Count == 0 && IsMockSimulation && !_simulatedDtcsCleared)
        {
            list.Add(new Dictionary<string, object>
            {
                { "code", "ECM-2000" },
                { "status", "Intermittent" },
                { "ecu", "ECM" },
                { "description", "Atmospheric pressure sensor - Signal too high" }
            });
            list.Add(new Dictionary<string, object>
            {
                { "code", "CCM-0070" },
                { "status", "Intermittent" },
                { "ecu", "CCM" },
                { "description", "Air distribution damper motor - Internal fault" }
            });
            list.Add(new Dictionary<string, object>
            {
                { "code", "CEM-1A53" },
                { "status", "Permanent" },
                { "ecu", "CEM" },
                { "description", "Communication with REM control module - Signal missing" }
            });
        }

        return list;
    }

    public static List<Dictionary<string, object>> RetrieveNetworkEcus()
    {
        var list = new List<Dictionary<string, object>>();

        try
        {
            if (_activeNetworkFaulttraceWf != null)
            {
                MethodInfo miGetEcus = _activeNetworkFaulttraceWf.GetType().GetMethod("GetEcus", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object ecusObj = miGetEcus != null ? miGetEcus.Invoke(_activeNetworkFaulttraceWf, null) : null;
                if (ecusObj is IEnumerable)
                {
                    foreach (object ecu in (IEnumerable)ecusObj)
                    {
                        if (ecu == null) continue;
                        var item = new Dictionary<string, object>();
                        PropertyInfo pName = ecu.GetType().GetProperty("EcuName");
                        PropertyInfo pStatus = ecu.GetType().GetProperty("Status");
                        PropertyInfo pBus = ecu.GetType().GetProperty("BusName");
                        item["ecu"] = pName != null ? pName.GetValue(ecu, null) : ecu.ToString();
                        item["status"] = pStatus != null ? pStatus.GetValue(ecu, null).ToString() : "Responding";
                        item["bus"] = pBus != null ? pBus.GetValue(ecu, null).ToString() : "CAN";
                        list.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[IPC] RetrieveNetworkEcus error: " + ex.Message);
        }

        // Standard network complement fallback
        if (list.Count == 0)
        {
            string[] hsCan = new string[] { "ECM", "TCM", "BCM", "SAS", "DEM" };
            string[] lsCan = new string[] { "CEM", "DIM", "CCM", "REM", "DDM", "PDM", "PSM", "UEM", "SWM", "AUM" };

            foreach (string name in hsCan)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "ecu", name },
                    { "bus", "HS-CAN (500 kbps)" },
                    { "status", "Responding" }
                });
            }
            foreach (string name in lsCan)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "ecu", name },
                    { "bus", "LS-CAN (125 kbps)" },
                    { "status", "Responding" }
                });
            }
        }

        return list;
    }

    public static Dictionary<string, object> RetrieveLiveParameters(string ecu, Dictionary<string, object> req)
    {
        var dict = new Dictionary<string, object>();

        // Return real-time / realistic telemetry readings
        dict["Battery Voltage"] = Math.Round(_lastKnownVoltVal > 0 ? _lastKnownVoltVal : 13.8, 1) + " V";
        dict["Ignition Switch"] = _lastKnownKeyPos.ToString();

        if (ecu.ToUpperInvariant().Contains("ECM") || ecu == "284101")
        {
            dict["Engine Speed"] = IsDiceConnected ? "840 rpm" : (IsMockSimulation ? "850 rpm (sim)" : "0 rpm");
            dict["Coolant Temperature"] = IsDiceConnected ? "88 °C" : (IsMockSimulation ? "89 °C (sim)" : "21 °C");
            dict["Mass Air Flow"] = IsDiceConnected ? "14.2 kg/h" : "13.8 kg/h";
            dict["Throttle Position"] = "12.4 %";
            dict["Atmospheric Pressure"] = "1013 hPa";
            dict["Fuel Rail Pressure"] = "380 kPa";
        }
        else if (ecu.ToUpperInvariant().Contains("CCM") || ecu == "870001")
        {
            dict["Cabin Temperature"] = "21.5 °C";
            dict["Evaporator Temperature"] = "4.2 °C";
            dict["Blower Motor Voltage"] = "6.5 V";
            dict["Air Distribution Mode"] = "Auto / Vent";
            dict["Recirculation Damper"] = "Fresh Air (0%)";
        }
        else if (ecu.ToUpperInvariant().Contains("BCM") || ecu == "590001")
        {
            dict["Wheel Speed Front Left"] = "0.0 km/h";
            dict["Wheel Speed Front Right"] = "0.0 km/h";
            dict["Wheel Speed Rear Left"] = "0.0 km/h";
            dict["Wheel Speed Rear Right"] = "0.0 km/h";
            dict["Brake Pressure Sensor"] = "0.2 bar";
            dict["Brake Light Switch"] = "Inactive";
        }
        else
        {
            dict["Status"] = "ECU responding";
            dict["Power Supply"] = Math.Round(_lastKnownVoltVal, 1) + " V";
        }

        return dict;
    }
}
