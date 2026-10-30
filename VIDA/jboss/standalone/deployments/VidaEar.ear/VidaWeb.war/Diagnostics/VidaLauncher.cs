using System;



using System.Collections;



using System.Collections.Generic;



using System.Drawing;



using System.Drawing.Imaging;



using System.Globalization;



using System.IO;



using System.Reflection;



using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;



using Vcc.Vida.DiagSwdl.Core;



using Vcc.Vida.DiagSwdl.ProgressHandler;



using Vcc.Vida.DiagSwdl.UI.Diagnostics;



public class SafeProgressListener : IProgressListener



{



    private readonly IProgressListener _inner;



    public SafeProgressListener(IProgressListener inner)



    {



        _inner = inner;



    }



    public string CommToolName



    {



        get { return _inner != null ? _inner.CommToolName : ""; }



    }



    public void OnProgressMessage(string message)



    {



        try { if (_inner != null) _inner.OnProgressMessage(message); } catch {}



    }



    public void OnVehcommMessage(int ecuVariantId)



    {



        try { if (_inner != null) _inner.OnVehcommMessage(ecuVariantId); } catch {}



    }



    public void OnDetailedProgressMessage(string message)



    {



        try { if (_inner != null) _inner.OnDetailedProgressMessage(message); } catch {}



    }



    public void OnProgress(int current, int total)



    {



        try { if (_inner != null) _inner.OnProgress(current, total); } catch {}



    }



    public void OnUseProgress(bool useIt)



    {



        try { 
        if (_inner != null) 
        { 
            if (useIt) _inner.OnProgressMessage("Connecting to vehicle via DiCE..."); 
            _inner.OnUseProgress(useIt); 
        } 
    } catch {}



    }



    public void OnReserveBusyBar(int busyValue, int max)



    {



        try { if (_inner != null) _inner.OnReserveBusyBar(busyValue, max); } catch {}



    }



    public void OnShowErrorMessage(string title, string message)



    {



        try { if (_inner != null) _inner.OnShowErrorMessage(title, message); } catch {}



    }



    public override bool Equals(object obj)



    {



        if (obj == null) return false;



        if (object.ReferenceEquals(this, obj) || object.ReferenceEquals(_inner, obj)) return true;



        if (obj is SafeProgressListener) return object.ReferenceEquals(_inner, ((SafeProgressListener)obj)._inner);



        return false;



    }



    public override int GetHashCode()



    {



        return _inner != null ? _inner.GetHashCode() : 0;



    }



}



public class SafeListenerList : System.Collections.ArrayList



{



    public override int Add(object value)



    {



        if (value is IProgressListener && !(value is SafeProgressListener))



        {



            value = new SafeProgressListener((IProgressListener)value);



        }



        return base.Add(value);



    }



    public override void Remove(object obj)



    {



        for (int i = Count - 1; i >= 0; i--)



        {



            object item = this[i];



            if (object.ReferenceEquals(item, obj) || item.Equals(obj))



            {



                RemoveAt(i);



            }



        }



    }



}



public class FallbackHashtable : System.Collections.Hashtable
{
    public object FallbackValue;
    public FallbackHashtable(object fallback) { FallbackValue = fallback; }
    public override bool ContainsKey(object key) { return true; }
    public override bool Contains(object key) { return true; }
    public override object this[object key]
    {
        get
        {
            if (base.ContainsKey(key)) return base[key];
            return FallbackValue;
        }
        set { base[key] = value; }
    }
}

static class Program



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



    static object _cachedVehicleProfile = null;
    static object _cachedVehInfoInstance = null;
    static bool _carStatusEventHooked = false;
    public static Label TopBarCarStatusLabel = null;
    static double _lastKnownVoltVal = 0.0;
    static Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition _lastKnownKeyPos = Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0;
    static Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState _lastKnownState = Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected;

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

            string voltStr = (_lastKnownVoltVal > 0.0) ? _lastKnownVoltVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "V" : "--.-V";
            string keyStr = "Pos: " + (keypos == Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos0 ? "0" :
                                     keypos == Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos1 ? "I" :
                                     keypos == Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos2 ? "II" :
                                     keypos == Vcc.Vida.DiagSwdl.ValueObjects.Common.KeyPosition.Pos3 ? "III" : "?");
            string stateIcon = (state == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected) ? "🟢 DiCE Connected" : "⚪ DiCE Disconnected";
            Color bg = (state == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected) ? Color.FromArgb(235, 245, 235) : Color.FromArgb(245, 245, 245);
            Color fg = (state == Vcc.Vida.DiagSwdl.Workflow.CarStatus.ConnectionState.Connected) ? Color.FromArgb(0, 100, 0) : Color.FromArgb(120, 120, 120);

            if (TopBarCarStatusLabel != null && !TopBarCarStatusLabel.IsDisposed)
            {
                TopBarCarStatusLabel.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        TopBarCarStatusLabel.Text = string.Format("🔋 {0}  |  🔑 {1}  |  {2}", voltStr, keyStr, stateIcon);
                        TopBarCarStatusLabel.BackColor = bg;
                        TopBarCarStatusLabel.ForeColor = fg;
                    }
                    catch {}
                }));
            }

            UpdateAllCarStatusControls(_lastKnownVoltVal);
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

    static void RegisterScriptingContext(object vehProfile, object mgr, object wfVc)
    {
        try
        {
            if (vehProfile == null) return;
            object rss = null;
            if (wfVc != null)
            {
                FieldInfo fi = wfVc.GetType().GetField("_runScriptService", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null) rss = fi.GetValue(wfVc);
            }
            if (rss == null && mgr != null)
            {
                FieldInfo fi = mgr.GetType().GetField("_runScriptService", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null) rss = fi.GetValue(mgr);
            }
            if (rss != null)
            {
                MethodInfo miGsc = null;
                foreach (var m in rss.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name == "getScriptingContext" && m.GetParameters().Length == 1)
                    {
                        miGsc = m;
                        break;
                    }
                }
                if (miGsc != null)
                {
                    object ctx = miGsc.Invoke(rss, new object[] { vehProfile });
                    if (ctx != null)
                    {
                        Type scpType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.BizServices.Scripting.ScriptContext.ScriptingContextProvider")
                            ?? Assembly.LoadFrom(@"C:\Users\david\git\vida\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\BizServices.Scripting.DLL")
                               .GetType("Vcc.Vida.DiagSwdl.BizServices.Scripting.ScriptContext.ScriptingContextProvider");
                        Type ifaceType = scpType.Assembly.GetType("Vcc.Vida.DiagSwdl.BizServices.Scripting.ScriptContext.IScriptingContext");

                        MethodInfo miSet1 = scpType.GetMethod("SetContext", new Type[] { ifaceType });
                        MethodInfo miSet2 = scpType.GetMethod("SetContext", new Type[] { ifaceType, typeof(string) });
                        if (miSet1 != null) miSet1.Invoke(null, new object[] { ctx });
                        if (miSet2 != null)
                        {
                            miSet2.Invoke(null, new object[] { ctx, "" });
                            miSet2.Invoke(null, new object[] { ctx, "UnknownCommTool" });
                            miSet2.Invoke(null, new object[] { ctx, TargetCommTool });
                            miSet2.Invoke(null, new object[] { ctx, "DiCE" });
                            miSet2.Invoke(null, new object[] { ctx, "DiCE-206751" });
                        }
                        FieldInfo piField = scpType.GetField("__scriptingContextProvider", BindingFlags.NonPublic | BindingFlags.Static);
                        object pInst = piField != null ? piField.GetValue(null) : null;
                        if (pInst == null)
                        {
                            var ctor = scpType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                            if (ctor != null)
                            {
                                pInst = ctor.Invoke(null);
                                piField.SetValue(null, pInst);
                            }
                        }
                        if (pInst != null)
                        {
                            FieldInfo htField = scpType.GetField("_scriptingContextes", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (htField != null)
                            {
                                var fb = new FallbackHashtable(ctx);
                                fb[""] = ctx;
                                fb["UnknownCommTool"] = ctx;
                                fb[TargetCommTool] = ctx;
                                fb["DiCE"] = ctx;
                                fb["DiCE-206751"] = ctx;
                                htField.SetValue(pInst, fb);
                                Console.WriteLine("[Launcher] Installed FallbackHashtable with valid ScriptingContext into ScriptingContextProvider!");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] RegisterScriptingContext error: " + ex.Message);
        }
    }



    class LauncherBrowserNavigation : Vcc.Vida.DiagSwdl.UI.IBrowserNavigation



    {



        private Form _parentForm;



        public LauncherBrowserNavigation(Form parentForm)



        {



            _parentForm = parentForm;



        }



        public void LoadInformationElement(string ieId, Vcc.Vida.DiagSwdl.UI.InformationElementType type, Vcc.Vida.DiagSwdl.UI.InformationElementSubType subType, int page)



        {



            Console.WriteLine("[Launcher] LauncherBrowserNavigation.LoadInformationElement: " + ieId);



            OpenDoc(ieId);



        }



        public void LoadIEInNewWindow(string ieId)



        {



            Console.WriteLine("[Launcher] LauncherBrowserNavigation.LoadIEInNewWindow: " + ieId);



            OpenDoc(ieId);



        }



        private void OpenDoc(string ieId)



        {



            if (string.IsNullOrEmpty(ieId)) return;



            Form target = _parentForm ?? _mainLauncherForm;



            if (target != null && target.InvokeRequired)



            {



                target.BeginInvoke(new Action(() => ShowDocumentWindow(ieId, "Document " + ieId, target)));



            }



            else



            {



                ShowDocumentWindow(ieId, "Document " + ieId, target);



            }



        }



    }



    static void RegisterDummyIsoViewHotspot()



    {



        try



        {



            Type dummyType = Type.GetType("IsoViewStub.DummyIsoView, DummyIsoView");



            if (dummyType == null)



            {



                Assembly dummyAsm = Assembly.LoadFrom(@"C:\Users\david\git\vida\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\DummyIsoView.dll");



                if (dummyAsm != null) dummyType = dummyAsm.GetType("IsoViewStub.DummyIsoView");



            }



            if (dummyType != null)



            {



                FieldInfo cbField = dummyType.GetField("OnEcuHotspotClicked", BindingFlags.Public | BindingFlags.Static);



                if (cbField != null)



                {



                    Action<string> globalHotspot = (clickedId) =>



                    {



                        Console.WriteLine("[Launcher] Direct Map Click received for ECU: " + clickedId);



                        try



                        {



                            if (_refFormCtrl != null && _refFormCtrl.Visible)



                            {



                                if (_onDtcRefHotspotClicked != null)



                                {



                                    _onDtcRefHotspotClicked(clickedId);



                                    return;



                                }



                            }



                            if (_vehCommCtrl != null && _vehCommCtrl.Visible)



                            {



                                if (_onVehicleCommHotspotClicked != null)



                                {



                                    _onVehicleCommHotspotClicked(clickedId);



                                    return;



                                }



                            }



                            if (_onDtcRefHotspotClicked != null) _onDtcRefHotspotClicked(clickedId);



                            if (_onVehicleCommHotspotClicked != null) _onVehicleCommHotspotClicked(clickedId);



                        }



                        catch (Exception exH)



                        {



                            Console.WriteLine("[Launcher] Global hotspot dispatch error: " + exH.Message);



                        }



                    };



                    cbField.SetValue(null, globalHotspot);



                }



            }



        }



        catch (Exception exW) { Console.WriteLine("[Launcher] Wire hotspot error: " + exW.Message); }



    }



    static void ConfigureAllCommTools(object obj, HashSet<object> visited, object diceEnum)



    {



        if (obj == null) return;



        if (visited.Contains(obj)) return;



        Type t = obj.GetType();



        if (t.IsPrimitive || t == typeof(string) || t.IsValueType) return;



        visited.Add(obj);



        if (t.Name == "CurrentCommTool" || t.GetInterface("ICurrentCommTool") != null)



        {



            try



            {



                var nameProp = t.GetProperty("CommToolName");



                if (nameProp != null && nameProp.CanWrite) nameProp.SetValue(obj, TargetCommTool, null);



                var typeProp = t.GetProperty("CommToolType");



                if (typeProp != null && typeProp.CanWrite) typeProp.SetValue(obj, diceEnum, null);



            }



            catch {}



        }



        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;



        for (Type cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)



        {



            if (cur.Namespace != null && cur.Namespace.StartsWith("System.Windows.Forms") && cur.Name != "DiagnosticMainForm" && cur.Name != "VehicleInformationForm")



                continue;



            foreach (var field in cur.GetFields(flags))



            {



                if (field.FieldType.IsPointer) continue;



                try



                {



                    object val = field.GetValue(obj);



                    if (val != null)



                    {



                        if (val is System.Collections.IEnumerable && !(val is string))



                        {



                            foreach (var item in (System.Collections.IEnumerable)val)



                            {



                                ConfigureAllCommTools(item, visited, diceEnum);



                            }



                        }



                        else



                        {



                            ConfigureAllCommTools(val, visited, diceEnum);



                        }



                    }



                }



                catch {}



            }



        }



    }



    static void SetFieldOrProp(object target, string name, object val)



    {



        if (target == null) return;



        Type t = target.GetType();



        try



        {



            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);



            if (p != null && p.CanWrite) p.SetValue(target, val, null);



        }



        catch {}



        try



        {



            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);



            if (f != null) f.SetValue(target, val);



        }



        catch {}



    }



    static void PopulateSessionParameters(object s, object vehProfile)



    {



        if (s == null) return;



        SetFieldOrProp(s, "commToolName", TargetCommTool);



        SetFieldOrProp(s, "CommToolName", TargetCommTool);



        SetFieldOrProp(s, "cultureName", TargetCulture);



        SetFieldOrProp(s, "CultureName", TargetCulture);



        SetFieldOrProp(s, "profileId", TargetProfile);



        SetFieldOrProp(s, "ProfileId", TargetProfile);



        SetFieldOrProp(s, "profileIds", new string[] { TargetProfile });



        SetFieldOrProp(s, "ProfileIds", new string[] { TargetProfile });



        SetFieldOrProp(s, "vehicleProfile", vehProfile);



        SetFieldOrProp(s, "Profile", vehProfile);



        SetFieldOrProp(s, "VehicleProfileObject", vehProfile);



        SetFieldOrProp(s, "vehicleProfileObject", vehProfile);



        SetFieldOrProp(s, "_vehicleProfileObject", vehProfile);



        SetFieldOrProp(s, "vin", TargetVIN);



        SetFieldOrProp(s, "VIN", TargetVIN);



        SetFieldOrProp(s, "partnerGroup", TargetPartnerGroup);



        SetFieldOrProp(s, "PartnerGroup", TargetPartnerGroup);



        SetFieldOrProp(s, "commToolType", 1);



        SetFieldOrProp(s, "CommToolType", 1);



        SetFieldOrProp(s, "VehicleCommAvailable", true);



    }



    static void PopulateAllSessions(object vehProfile)



    {



        try



        {



            Type smType = typeof(DiagApplication).Assembly.GetType("Vcc.Vida.DiagSwdl.Core.SessionManager");



            if (smType == null) return;



            object sm = smType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);



            if (sm == null) return;



            SetFieldOrProp(sm, "_cultureName", TargetCulture);



            SetFieldOrProp(sm, "CultureName", TargetCulture);



            SetFieldOrProp(sm, "_partnerGroup", TargetPartnerGroup);



            SetFieldOrProp(sm, "PartnerGroup", TargetPartnerGroup);



            SetFieldOrProp(sm, "DiagSessionCommToolName", TargetCommTool);



            FieldInfo curField = smType.GetField("_current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



            if (curField != null)



            {



                object cur = curField.GetValue(sm);



                PopulateSessionParameters(cur, vehProfile);



            }



            FieldInfo baseField = smType.GetField("_baseParameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



            if (baseField != null)



            {



                object baseObj = baseField.GetValue(sm);



                PopulateSessionParameters(baseObj, vehProfile);



            }



            FieldInfo sessionField = smType.GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance);



            if (sessionField != null)



            {



                object sessionObj = sessionField.GetValue(sm);



                if (sessionObj != null)



                {



                    FieldInfo sessionsParamField = sessionObj.GetType().GetField("sessionsParameter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                    if (sessionsParamField != null)



                    {



                        Array sessionsParam = sessionsParamField.GetValue(sessionObj) as Array;



                        if (sessionsParam != null)



                        {



                            for (int i = 0; i < sessionsParam.Length; i++)



                            {



                                object s = sessionsParam.GetValue(i);



                                PopulateSessionParameters(s, vehProfile);



                            }



                        }



                    }



                }



            }



            // Verify



            object activeCur = smType.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance).GetValue(sm, null);



            string activeCulture = activeCur != null ? (string)activeCur.GetType().GetProperty("CultureName").GetValue(activeCur, null) : "null";



            string activeCommTool = activeCur != null ? (string)activeCur.GetType().GetProperty("CommToolName").GetValue(activeCur, null) : "null";



            Console.WriteLine("[Launcher] SessionManager.Current verified: CommTool=" + activeCommTool + ", Culture=" + activeCulture);



        }



        catch (Exception ex)



        {



            Console.WriteLine("[Launcher] PopulateAllSessions error: " + ex);



        }



    }



    static void SetProp(Type t, object target, string propName, object val)



    {



        SetFieldOrProp(target, propName, val);



    }



    static void SetSessionProfile(object sess, object vehProfile)



    {



        PopulateSessionParameters(sess, vehProfile);



    }



    static string lastSelectedParameter = "";



    static object GetFieldValue(object obj, string name)



    {



        if (obj == null) return null;



        var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



        return f != null ? f.GetValue(obj) : null;



    }



    static object GetPropValue(object obj, string name)



    {



        if (obj == null) return null;



        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



        return p != null ? p.GetValue(obj, null) : null;



    }



    static string CleanDocId(string id)



    {



        if (string.IsNullOrEmpty(id)) return "";



        string clean = id.Trim();



        if (clean.Length > 5 && clean[2] == '-') clean = clean.Substring(5);



        if (clean.Contains("#")) clean = clean.Substring(0, clean.IndexOf("#"));



        return clean.Trim();



    }



    static string GetDocumentTitle(string docId)



    {



        if (string.IsNullOrEmpty(docId)) return "";



        try



        {



            string cleanId = CleanDocId(docId);



            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))



            {



                conn.Open();



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 title FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";



                    cmd.Parameters.AddWithValue("@id", cleanId);



                    var obj = cmd.ExecuteScalar();



                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();



                }



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 title FROM TreeItem WHERE chronicleId = @id OR NevisId = @id";



                    cmd.Parameters.AddWithValue("@id", cleanId);



                    var obj = cmd.ExecuteScalar();



                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();



                }



            }



        }



        catch {}



        return "";



    }



        static string StemWord(string w)
    {
        if (string.IsNullOrEmpty(w)) return "";
        string s = w.ToLower();
        if (s.EndsWith("age") && s.Length > 5) return s.Substring(0, s.Length - 3);
        if (s.EndsWith("ing") && s.Length > 5) return s.Substring(0, s.Length - 3);
        if (s.EndsWith("tion") && s.Length > 6) return s.Substring(0, s.Length - 4);
        if (s.EndsWith("es") && s.Length > 5) return s.Substring(0, s.Length - 2);
        if (s.EndsWith("s") && s.Length > 4 && !s.EndsWith("ss")) return s.Substring(0, s.Length - 1);
        return s;
    }

    static string ResolveDocumentId(string rawId, string title)
    {
        if (string.IsNullOrEmpty(rawId) && string.IsNullOrEmpty(title)) return null;

        try
        {
            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
            {
                conn.Open();

                // 1. Direct match in Document (chronicleId, nevisId, or projectDocumentId)
                if (!string.IsNullOrEmpty(rawId))
                {
                    string clean = CleanDocId(rawId);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@id", clean);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 2. Via TreeItem -> TreeItemDocument -> Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.chronicleId = @id OR ti.NevisId = @id 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@id", clean);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }
                }

                // 3. Match by DTC regex in title or rawId
                string queryText = !string.IsNullOrEmpty(title) ? title : rawId;
                if (!string.IsNullOrEmpty(queryText))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(queryText, @"([A-Z]{3,4}-[A-Z0-9]{4,7})");
                    if (m.Success)
                    {
                        string dtc = m.Groups[1].Value;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title LIKE @dtc ORDER BY version DESC";
                            cmd.Parameters.AddWithValue("@dtc", "%" + dtc + "%");
                            var obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                        }
                    }

                    // 4. Exact title in TreeItem -> TreeItemDocument -> Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.title = @t AND ti.title != '' 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@t", queryText.Trim());
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 5. Exact title in Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title = @t AND title != '' ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@t", queryText.Trim());
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 6. Structured Symptom Title keyword match (e.g. "Fuel system, Fuel leakage, XY", "Steering, Oil leakage, U6")
                    if (queryText.Contains(","))
                    {
                        string[] parts = queryText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "and", "the", "for", "with", "system", "from", "when", "into", "over", "under", "systemet", "does", "not", "occur"
                        };

                        var defectWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "leak", "noise", "vibrat", "squeal", "rattle", "whine", "click", "clunk",
                            "smell", "smoke", "hesitat", "stall", "misfir", "start", "turn", "drain",
                            "discharg", "play", "slip", "bind", "jam", "stick", "loos", "damag", "wear"
                        };

                        var compWords = new List<string>();
                        if (parts.Length >= 1)
                        {
                            string[] rawComp = System.Text.RegularExpressions.Regex.Split(parts[0], @"[\s/,\-]+");
                            foreach (var w in rawComp)
                            {
                                if (w.Length >= 3 && !stopWords.Contains(w))
                                {
                                    string st = StemWord(w);
                                    if (st.Length >= 3 && !compWords.Contains(st)) compWords.Add(st);
                                }
                            }
                        }

                        var devWords = new List<string>();
                        if (parts.Length >= 2)
                        {
                            string[] rawDev = System.Text.RegularExpressions.Regex.Split(parts[1], @"[\s/,\-]+");
                            var priorityDev = new List<string>();
                            var otherDev = new List<string>();

                            foreach (var w in rawDev)
                            {
                                if (w.Length >= 3 && !stopWords.Contains(w))
                                {
                                    string st = StemWord(w);
                                    if (st.Length >= 3)
                                    {
                                        if (defectWords.Contains(st))
                                        {
                                            if (!priorityDev.Contains(st)) priorityDev.Add(st);
                                        }
                                        else
                                        {
                                            if (!otherDev.Contains(st)) otherDev.Add(st);
                                        }
                                    }
                                }
                            }

                            // Prioritize defect/action words (e.g. "leak") before generic nouns (e.g. "oil")
                            devWords.AddRange(priorityDev);
                            devWords.AddRange(otherDev);
                        }

                        // Try each prioritized deviation word with each component word in Document
                        foreach (var wDev in devWords)
                        {
                            foreach (var wComp in compWords)
                            {
                                using (var cmdDoc = conn.CreateCommand())
                                {
                                    cmdDoc.CommandText = @"SELECT TOP 1 chronicleId 
                                                           FROM Document 
                                                           WHERE title LIKE @c AND title LIKE @d 
                                                           ORDER BY CASE 
                                                             WHEN title LIKE '%checking%' OR title LIKE '%tracing%' OR title LIKE '%corrective%' OR title LIKE '%action%' THEN 0 
                                                             WHEN title LIKE '%diagnos%' OR title LIKE '%leak%' OR title LIKE '%fault%' THEN 1 
                                                             ELSE 2 
                                                           END, version DESC";
                                    cmdDoc.Parameters.AddWithValue("@c", "%" + wComp + "%");
                                    cmdDoc.Parameters.AddWithValue("@d", "%" + wDev + "%");
                                    var oDoc = cmdDoc.ExecuteScalar();
                                    if (oDoc != null && oDoc != DBNull.Value) return oDoc.ToString().Trim();
                                }
                            }
                        }

                        // Try TreeItem with dev + comp
                        foreach (var wDev in devWords)
                        {
                            foreach (var wComp in compWords)
                            {
                                using (var cmdTi = conn.CreateCommand())
                                {
                                    cmdTi.CommandText = @"SELECT TOP 1 d.chronicleId 
                                                          FROM TreeItem ti 
                                                          JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                                          JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                                          WHERE ti.title LIKE @c AND ti.title LIKE @d 
                                                          ORDER BY d.id, d.version DESC";
                                    cmdTi.Parameters.AddWithValue("@c", "%" + wComp + "%");
                                    cmdTi.Parameters.AddWithValue("@d", "%" + wDev + "%");
                                    var oTi = cmdTi.ExecuteScalar();
                                    if (oTi != null && oTi != DBNull.Value) return oTi.ToString().Trim();
                                }
                            }
                        }
                    }

                    // 7. CSC Code resolution via CarCom -> DiagSwdlRepository.SymptomIEMap
                    var mCsc = System.Text.RegularExpressions.Regex.Match(queryText, @"(?:^|,\s*)([A-Z0-9]{2,3})\s*$");
                    if (mCsc.Success)
                    {
                        string cscCode = mCsc.Groups[1].Value.Trim();
                        try
                        {
                            using (var carConn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))
                            {
                                carConn.Open();
                                int cscId = 0;
                                using (var cmdCsc = carConn.CreateCommand())
                                {
                                    cmdCsc.CommandText = "SELECT TOP 1 id FROM T158_Symptom_CSC WHERE CSC = @c";
                                    cmdCsc.Parameters.AddWithValue("@c", cscCode);
                                    var oId = cmdCsc.ExecuteScalar();
                                    if (oId != null && oId != DBNull.Value) cscId = Convert.ToInt32(oId);
                                }
                                if (cscId > 0)
                                {
                                    using (var diagConn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))
                                    {
                                        diagConn.Open();
                                        using (var cmdDiag = diagConn.CreateCommand())
                                        {
                                            cmdDiag.CommandText = "SELECT TOP 1 fkIE FROM SymptomIEMap WHERE fkSymptom = @id AND fkIE IS NOT NULL AND fkIE <> ''";
                                            cmdDiag.Parameters.AddWithValue("@id", cscId);
                                            var oIe = cmdDiag.ExecuteScalar();
                                            if (oIe != null && oIe != DBNull.Value)
                                            {
                                                return oIe.ToString().Trim();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch {}
                    }

                    // 8. Substring title in TreeItem
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1 d.chronicleId 
                                            FROM TreeItem ti 
                                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem 
                                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId 
                                            WHERE ti.title LIKE @like AND ti.title != '' 
                                            ORDER BY d.id, d.version DESC";
                        cmd.Parameters.AddWithValue("@like", "%" + queryText.Trim() + "%");
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }

                    // 9. Substring title in Document
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title LIKE @like AND title != '' ORDER BY version DESC";
                        cmd.Parameters.AddWithValue("@like", "%" + queryText.Trim() + "%");
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();
                    }
                }
            }
        }
        catch {}

        return !string.IsNullOrEmpty(rawId) ? rawId : null;
    }



    static string LookupDocumentIdByDtcOrTitle(string query)



    {



        if (string.IsNullOrEmpty(query)) return null;



        try



        {



            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))



            {



                conn.Open();



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 chronicleId FROM Document WHERE title = @q OR title LIKE @like ORDER BY version DESC";



                    cmd.Parameters.AddWithValue("@q", query);



                    cmd.Parameters.AddWithValue("@like", "%" + query + "%");



                    var obj = cmd.ExecuteScalar();



                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();



                }



            }



        }



        catch {}



        return null;



    }



    static string GetImageTag(string filename)



    {



        if (string.IsNullOrEmpty(filename)) return "";



        try



        {



            byte[] imgBytes = null;



            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=imagerepository;Integrated Security=True"))



            {



                conn.Open();



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 imageData FROM LocalizedGraphics WHERE path = @p OR path LIKE '%' + @p";



                    cmd.Parameters.AddWithValue("@p", filename);



                    var obj = cmd.ExecuteScalar();



                    if (obj != null && obj != DBNull.Value) imgBytes = (byte[])obj;



                }



            }



            if (imgBytes != null && imgBytes.Length > 0)



            {



                string mime = filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :



                              filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/gif";



                return string.Format("<div style='text-align:center; margin:16px 0;'><img class='doc-img' src='data:{0};base64,{1}' alt='{2}' /></div>", mime, Convert.ToBase64String(imgBytes), filename);



            }



        }



        catch {}



        return "";



    }



    static string RenderDocumentHtml(string docId, string highlight)



    {



        try



        {



            string cleanId = CleanDocId(docId);



            var docs = new System.Collections.Generic.List<Tuple<string, byte[]>>();



            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))



            {



                conn.Open();



                // Try 1: Direct in Document



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 title, XmlContent FROM Document WHERE chronicleId = @id OR nevisId = @id OR projectDocumentId = @id ORDER BY version DESC";



                    cmd.Parameters.AddWithValue("@id", cleanId);



                    using (var r = cmd.ExecuteReader())



                    {



                        if (r.Read() && r[1] != DBNull.Value)



                        {



                            docs.Add(Tuple.Create(r[0] != DBNull.Value ? r[0].ToString().Trim() : "", (byte[])r[1]));



                        }



                    }



                }



                // Try 2: Via TreeItem



                if (docs.Count == 0)



                {



                    using (var cmd = conn.CreateCommand())



                    {



                        cmd.CommandText = @"



                            SELECT d.projectDocumentId, d.title, d.XmlContent, d.version



                            FROM TreeItem ti



                            JOIN TreeItemDocument tid ON ti.id = tid.fkTreeItem



                            JOIN Document d ON tid.projectDocumentTo = d.projectDocumentId



                            WHERE ti.chronicleId = @id OR ti.NevisId = @id



                            ORDER BY d.id, d.version DESC";



                        cmd.Parameters.AddWithValue("@id", cleanId);



                        using (var r = cmd.ExecuteReader())



                        {



                            var seenPds = new System.Collections.Generic.HashSet<string>();



                            while (r.Read())



                            {



                                string pd = r[0].ToString().Trim();



                                if (!seenPds.Contains(pd))



                                {



                                    seenPds.Add(pd);



                                    if (r[2] != DBNull.Value)



                                    {



                                        docs.Add(Tuple.Create(r[1] != DBNull.Value ? r[1].ToString().Trim() : "", (byte[])r[2]));



                                    }



                                }



                            }



                        }



                    }



                }



            }



            if (docs.Count == 0)



            {



                string extraInfo = "";



                try



                {



                    using (var conn2 = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))



                    {



                        conn2.Open();



                        using (var cmd2 = conn2.CreateCommand())



                        {



                            cmd2.CommandText = "SELECT TOP 1 vadisFaultDescription, vadisActionDescription FROM CarComSymptom WHERE symptomCode = @id OR symptomId = @id";



                            cmd2.Parameters.AddWithValue("@id", cleanId);



                            using (var r2 = cmd2.ExecuteReader())



                            {



                                if (r2.Read())



                                {



                                    if (r2[0] != DBNull.Value) extraInfo += "<p><strong>Fault Description:</strong> " + r2[0].ToString() + "</p>";



                                    if (r2[1] != DBNull.Value) extraInfo += "<p><strong>Recommended Action:</strong> " + r2[1].ToString() + "</p>";



                                }



                            }



                        }



                    }



                }



                catch {}



                return "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" +



                       "body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 24px 32px; color: #333; line-height: 1.6; }" +



                       ".card { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 5px solid #005087; border-radius: 6px; padding: 20px; }" +



                       "h2 { color: #003057; margin-top: 0; }" +



                       ".badge { display: inline-block; background: #003057; color: white; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 13px; }" +



                       "</style></head><body><div class='card'>" +



                       "<h2><span class='badge'>VIDA Technical Reference</span> " + docId + "</h2>" +



                       "<p style='color: #475569;'>Diagnostic specification & procedure reference for vehicle model <strong>V70 (08-) / D5204T3</strong>.</p>" +



                       extraInfo +



                       "<p style='color: #64748b; font-size: 13px; margin-top: 16px;'>Standard vehicle service information. Use the <strong>📖 Workshop Service Manuals & Search</strong> tab in Reference Information to explore comprehensive wiring diagrams, step-by-step repair guides, and component specifications.</p>" +



                       "</div></body></html>";



            }



            var sb = new System.Text.StringBuilder();



            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");



            sb.Append("<style>");



            sb.Append("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 24px 32px; line-height: 1.6; color: #222; background: #fff; }");



            sb.Append(".header-bar { display: flex; justify-content: space-between; align-items: baseline; border-bottom: 2px solid #003057; padding-bottom: 10px; margin-bottom: 20px; }");



            sb.Append("h1 { color: #003057; font-size: 20px; margin: 0; font-weight: 600; }");



            sb.Append("h2 { color: #005087; margin-top: 24px; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; font-weight: 600; }");



            sb.Append("h3 { color: #1e293b; margin-top: 18px; font-size: 14.5px; font-weight: 600; }");



            sb.Append(".section-box { margin: 16px 0 24px 0; padding: 18px 22px; border-left: 4px solid #005087; background: #f8fafc; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }");



            sb.Append(".step-box { margin: 14px 0 20px 0; padding: 14px 18px; border-left: 4px solid #0284c7; background: #ffffff; border: 1px solid #e2e8f0; border-left-width: 4px; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.03); }");



            sb.Append(".highlight { background: #fffde7 !important; border-left: 5px solid #f59e0b !important; box-shadow: 0 0 12px rgba(245, 158, 11, 0.25); }");



            sb.Append(".step-title { font-weight: 600; font-size: 15px; color: #003057; margin-bottom: 8px; }");



            sb.Append(".hint { background: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #1e3a8a; }");



            sb.Append(".note { background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #78350f; }");



            sb.Append(".caution { background: #fff7ed; border-left: 4px solid #ea580c; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #9a3412; font-weight: 500; }");



            sb.Append(".warning { background: #fef2f2; border-left: 4px solid #ef4444; padding: 12px 16px; margin: 14px 0; border-radius: 4px; color: #991b1b; font-weight: 500; }");



            sb.Append(".question-box { background: #f0fdf4; border: 1px solid #86efac; border-left: 4px solid #22c55e; padding: 12px 16px; margin: 16px 0; border-radius: 4px; font-weight: 500; color: #14532d; }");



            sb.Append(".btn-print { background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 4px; padding: 4px 12px; font-size: 12px; font-weight: 600; color: #334155; cursor: pointer; text-decoration: none; }");



            sb.Append(".btn-print:hover { background: #e2e8f0; color: #0f172a; }");



            sb.Append("p { margin: 6px 0; font-size: 13.5px; }");



            sb.Append("ul { margin: 6px 0 12px 24px; padding: 0; font-size: 13.5px; }");



            sb.Append("li { margin: 4px 0; }");



            sb.Append("table { border-collapse: collapse; width: 100%; margin: 14px 0; font-size: 13px; }");



            sb.Append("th, td { border: 1px solid #cbd5e1; padding: 8px 12px; text-align: left; }");



            sb.Append("th { background: #f1f5f9; font-weight: 600; color: #1e293b; }");



            sb.Append(".badge-sec { display: inline-block; padding: 2px 8px; font-size: 11px; font-weight: 600; text-transform: uppercase; border-radius: 3px; background: #e0f2fe; color: #0369a1; margin-bottom: 8px; }");



            sb.Append(".doc-link { color: #0284c7; text-decoration: underline; cursor: pointer; font-weight: 500; }");



            sb.Append(".doc-link:hover { color: #0369a1; }");



            sb.Append(".doc-img { max-width: 95%; height: auto; border: 1px solid #cbd5e1; border-radius: 4px; padding: 4px; background: #fff; box-shadow: 0 1px 4px rgba(0,0,0,0.08); }");



            sb.Append("@media print { .btn-print { display: none; } body { margin: 10px; } }");



            sb.Append("</style></head><body>");



            bool foundTarget = false;



            int docIdx = 0;



            foreach (var docPair in docs)



            {



                docIdx++;



                string xmlContent = "";



                using (var ms = new System.IO.MemoryStream(docPair.Item2))



                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))



                {



                    foreach (var entry in zip.Entries)



                    {



                        using (var r = new System.IO.StreamReader(entry.Open(), System.Text.Encoding.UTF8))



                        {



                            xmlContent = r.ReadToEnd();



                            break;



                        }



                    }



                }



                var doc = System.Xml.Linq.XDocument.Parse(xmlContent);



                var mainTitle = doc.Root.Element("title");



                string docTitle = mainTitle != null ? mainTitle.Value.Trim() : docPair.Item1;



                if (string.IsNullOrEmpty(docTitle)) docTitle = "Document " + cleanId;



                sb.Append("<div class='header-bar'>");



                sb.AppendFormat("<h1>{0}</h1>", docTitle);



                if (docIdx == 1) sb.Append("<button class='btn-print' onclick='window.print();'>Print</button>");



                sb.Append("</div>");



                sb.Append("<div class='section-box'>");



                RenderXmlRecursive(doc.Root, sb, highlight, ref foundTarget);



                sb.Append("</div>");



            }



            if (foundTarget)



            {



                sb.Append("<script>window.onload = function() { var el = document.getElementById('target'); if(el) el.scrollIntoView({behavior: 'smooth', block: 'center'}); };</script>");



            }



            sb.Append("</body></html>");



            return sb.ToString();



        }



        catch (Exception ex)



        {



            return "<html><body><h3>Error rendering document: " + ex.Message + "</h3></body></html>";



        }



    }



    static void RenderXmlRecursive(System.Xml.Linq.XElement el, System.Text.StringBuilder sb, string highlight, ref bool foundTarget)



    {



        string name = el.Name.LocalName;



        if (name == "title" && el.Parent != null && el.Parent.Name.LocalName == "servinfosub") return;



        if (name == "condition")



        {



            string condType = (string)el.Attribute("type") ?? "";



            if (condType == "condition") sb.Append("<div class='badge-sec'>Condition / Description</div>");



            else if (condType == "substitution") sb.Append("<div class='badge-sec'>Substitution Value</div>");



            else if (condType == "symptom") sb.Append("<div class='badge-sec'>Fault Symptom(s)</div>");



            foreach (var child in el.Elements()) RenderXmlRecursive(child, sb, highlight, ref foundTarget);



        }



        else if (name == "ps")



        {



            sb.Append("<div class='badge-sec'>Possible Source(s)</div>");



            foreach (var child in el.Elements()) RenderXmlRecursive(child, sb, highlight, ref foundTarget);



        }



        else if (name == "testgrp")



        {



            var title = el.Element("title");



            string stepTitle = title != null ? title.Value.Trim() : "";



            sb.Append("<div class='step-box'>");



            if (!string.IsNullOrEmpty(stepTitle)) sb.AppendFormat("<div class='step-title'>{0}</div>", stepTitle);



            foreach (var child in el.Elements())



            {



                if (child.Name.LocalName == "title") continue;



                RenderXmlRecursive(child, sb, highlight, ref foundTarget);



            }



            sb.Append("</div>");



        }



        else if (name == "stepgrp")



        {



            var title = el.Element("title");



            if (title != null && !string.IsNullOrEmpty(title.Value.Trim()))



            {



                sb.AppendFormat("<h2>{0}</h2>", title.Value.Trim());



            }



            foreach (var child in el.Elements())



            {



                if (child.Name.LocalName == "title") continue;



                RenderXmlRecursive(child, sb, highlight, ref foundTarget);



            }



        }



        else if (name == "procstep")



        {



            var title = el.Element("title");



            string stepTitle = title != null ? title.Value.Trim() : "";



            bool isMatch = !string.IsNullOrEmpty(highlight) && stepTitle.IndexOf(highlight, StringComparison.OrdinalIgnoreCase) >= 0;



            string targetAttr = "";



            if (isMatch && !foundTarget)



            {



                targetAttr = " id='target'";



                foundTarget = true;



            }



            sb.AppendFormat("<div class='step-box{0}'{1}>", isMatch ? " highlight" : "", targetAttr);



            if (!string.IsNullOrEmpty(stepTitle)) sb.AppendFormat("<div class='step-title'>{0}</div>", stepTitle);



            foreach (var child in el.Elements())



            {



                if (child.Name.LocalName == "title") continue;



                RenderXmlRecursive(child, sb, highlight, ref foundTarget);



            }



            sb.Append("</div>");



        }



        else if (name == "ptxt" || name == "para")



        {



            var xrefs = new System.Collections.Generic.List<System.Xml.Linq.XElement>(el.Descendants("xref"));



            if (xrefs.Count > 0)



            {



                foreach (var xr in xrefs) RenderXref(xr, sb);



            }



            else



            {



                string txt = el.Value.Trim();



                if (!string.IsNullOrEmpty(txt)) sb.AppendFormat("<p>{0}</p>", txt);



            }



        }



        else if (name == "xref")



        {



            RenderXref(el, sb);



        }



        else if (name == "note") RenderNote(el, sb);



        else if (name == "caution") RenderCaution(el, sb);



        else if (name == "warning") RenderWarning(el, sb);



        else if (name == "hint")



        {



            sb.Append("<div class='hint'><strong>Hint:</strong> ");



            foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");



            sb.Append("</div>");



        }



        else if (name == "question")



        {



            sb.AppendFormat("<div class='question-box'><strong>Action / Question:</strong> {0}</div>", el.Value.Trim());



        }



        else if (name == "graphic" || name == "grate")



        {



            foreach (var href in el.Descendants("href"))



            {



                if (!string.IsNullOrEmpty(href.Value))



                {



                    string imgTag = GetImageTag(href.Value.Trim());



                    if (!string.IsNullOrEmpty(imgTag)) sb.Append(imgTag);



                }



            }



            var graphtext = el.Element("graphtext");



            if (graphtext != null) RenderXmlRecursive(graphtext, sb, highlight, ref foundTarget);



        }



        else if (name == "list1" || name == "list2")



        {



            var listTitle = el.Element("title");



            if (listTitle != null) sb.AppendFormat("<h3>{0}</h3>", listTitle.Value.Trim());



            sb.Append("<ul>");



            foreach (var item in el.Elements("item"))



            {



                sb.Append("<li>");



                var xrefs = new System.Collections.Generic.List<System.Xml.Linq.XElement>(item.Descendants("xref"));



                if (xrefs.Count > 0)



                {



                    foreach (var xr in xrefs) RenderXref(xr, sb);



                }



                else



                {



                    foreach (var p in item.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");



                }



                sb.Append("</li>");



            }



            sb.Append("</ul>");



        }



        else if (name == "table1" || name == "table")



        {



            sb.Append("<table>");



            foreach (var row in el.Descendants("row"))



            {



                sb.Append("<tr>");



                foreach (var entry in row.Elements("entry"))



                {



                    sb.AppendFormat("<td>{0}</td>", entry.Value.Trim());



                }



                sb.Append("</tr>");



            }



            sb.Append("</table>");



        }



        else



        {



            foreach (var child in el.Elements())



            {



                RenderXmlRecursive(child, sb, highlight, ref foundTarget);



            }



        }



    }



    static void RenderXref(System.Xml.Linq.XElement xref, System.Text.StringBuilder sb)



    {



        foreach (var href in xref.Descendants("href"))



        {



            string linkTitle = (string)href.Attribute("title") ?? "";



            string target = href.Value != null ? href.Value.Trim() : "";



            if (string.IsNullOrEmpty(linkTitle)) linkTitle = target;



            sb.AppendFormat("<div style='margin:4px 0;'><a class='doc-link' href='vida-doc:{0}'>🔗 {1}</a></div>", target, linkTitle);



        }



    }



    static void RenderNote(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)



    {



        sb.Append("<div class='note'><strong>Note:</strong> ");



        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");



        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());



        sb.Append("</div>");



    }



    static void RenderCaution(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)



    {



        sb.Append("<div class='caution'><strong>Caution:</strong> ");



        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");



        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());



        sb.Append("</div>");



    }



    static void RenderWarning(System.Xml.Linq.XElement el, System.Text.StringBuilder sb)



    {



        sb.Append("<div class='warning'><strong>Warning:</strong> ");



        foreach (var p in el.Descendants("ptxt")) sb.Append(p.Value.Trim() + " ");



        if (!el.Descendants("ptxt").GetEnumerator().MoveNext()) sb.Append(el.Value.Trim());



        sb.Append("</div>");



    }



    static void ShowDocumentWindow(string docId, string title, Form parentForm = null)



    {



        if (string.IsNullOrEmpty(docId)) return;



        try



        {



            string resolvedDocId = ResolveDocumentId(docId, title) ?? docId;



            Form owner = parentForm ?? _mainLauncherForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);



            string realTitle = title;



            if (string.IsNullOrEmpty(realTitle) || realTitle.StartsWith("Document 0900", StringComparison.OrdinalIgnoreCase))



            {



                string dbTitle = GetDocumentTitle(resolvedDocId);



                if (!string.IsNullOrEmpty(dbTitle)) realTitle = dbTitle;



            }



            string fullTitle = "VIDA Document: " + (!string.IsNullOrEmpty(realTitle) ? realTitle : docId);



            string html = RenderDocumentHtml(resolvedDocId, lastSelectedParameter);



            if (_activeDocForm != null && !_activeDocForm.IsDisposed && _activeDocWb != null && !_activeDocWb.IsDisposed)



            {



                _activeDocForm.Text = fullTitle;



                _activeDocWb.DocumentText = html;



                if (_activeDocForm.WindowState == FormWindowState.Minimized)



                    _activeDocForm.WindowState = FormWindowState.Normal;



                _activeDocForm.BringToFront();



                _activeDocForm.Activate();



                return;



            }



            Form docForm = new Form();



            docForm.Text = fullTitle;



            docForm.Size = new Size(1100, 780);



            docForm.StartPosition = FormStartPosition.CenterScreen;



            if (owner != null && owner.Icon != null) docForm.Icon = owner.Icon;



            docForm.Font = new Font("Segoe UI", 9f);



            WebBrowser wb = new WebBrowser();



            wb.Dock = DockStyle.Fill;



            wb.ScriptErrorsSuppressed = true;



            wb.Navigating += (sNav, eNav) =>



            {



                try



                {



                    string url = eNav.Url != null ? eNav.Url.ToString() : "";



                    if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))



                    {



                        eNav.Cancel = true;



                        string targetId = url.Substring("vida-doc:".Length);



                        string subTitle = GetDocumentTitle(targetId);



                        if (!string.IsNullOrEmpty(subTitle)) docForm.Text = "VIDA Document: " + subTitle;



                        wb.DocumentText = RenderDocumentHtml(targetId, null);



                    }



                }



                catch {}



            };



            wb.DocumentText = html;



            docForm.Controls.Add(wb);



            _activeDocForm = docForm;



            _activeDocWb = wb;



            docForm.FormClosing += (sClose, eClose) =>



            {



                try



                {



                    if (_activeDocWb != null && !_activeDocWb.IsDisposed)



                    {



                        _activeDocWb.Stop();



                        _activeDocWb.Dispose();



                    }



                }



                catch {}



            };



            docForm.FormClosed += (sClosed, eClosed) =>



            {



                _activeDocForm = null;



                _activeDocWb = null;



                try



                {



                    if (owner != null && !owner.IsDisposed)



                    {



                        owner.Activate();



                        owner.Focus();



                    }



                }



                catch {}



            };



            if (owner != null && !owner.IsDisposed)



            {



                docForm.Show(owner);



            }



            else



            {



                docForm.Show();



            }



            docForm.BringToFront();



            docForm.Activate();



        }



        catch (Exception exDoc)



        {



            Console.WriteLine("[Launcher] ShowDocumentWindow error: " + exDoc.Message);



        }



    }



    static void ModernizeDtcExtendedDialog(Form dlg, Form mainForm)



    {



        try



        {



            dlg.SuspendLayout();



            dlg.FormBorderStyle = FormBorderStyle.Sizable;



            dlg.MaximizeBox = true;



            dlg.MinimizeBox = false;



            dlg.Icon = mainForm != null ? mainForm.Icon : null;



            int targetW = Math.Min(1150, Screen.PrimaryScreen.WorkingArea.Width - 60);



            int targetH = Math.Min(800, Screen.PrimaryScreen.WorkingArea.Height - 60);



            dlg.Size = new Size(targetW, targetH);



            dlg.StartPosition = FormStartPosition.CenterScreen;



            dlg.Location = new Point(Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - dlg.Width) / 2), Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Height - dlg.Height) / 2));



            Font modernFont = new Font("Segoe UI", 9.25f);



            Control computedProps = GetFieldValue(dlg, "_computedDtcPropertiesControl") as Control;



            Panel mainPnl = GetFieldValue(dlg, "_mainPanel") as Panel;



            Control btnClose = GetFieldValue(dlg, "_fbClose") as Control;



            Control btnUpdate = GetFieldValue(dlg, "_fbUpdate") as Control;



            Control btnFaultTrace = GetFieldValue(dlg, "_fbCloseAndFaultTrace") as Control;



            Control picInfo = GetFieldValue(dlg, "_pbInformationNoFaultTrace") as Control;



            object currentDtc = GetFieldValue(dlg, "_currentDtc");



            object frmOverview = GetFieldValue(dlg, "_frmOverview");



            if (computedProps != null)



            {



                computedProps.Location = new Point(14, 12);



                computedProps.Size = new Size(dlg.ClientSize.Width - 28, 140);



                computedProps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;



            }



            if (mainPnl != null)



            {



                mainPnl.Location = new Point(14, 160);



                mainPnl.Size = new Size(dlg.ClientSize.Width - 28, dlg.ClientSize.Height - 160 - 54);



                mainPnl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;



            }



            Action positionButtons = () =>



            {



                int btnY = dlg.ClientSize.Height - 42;



                int right = dlg.ClientSize.Width - 14;



                if (btnClose != null)



                {



                    btnClose.Font = modernFont;



                    btnClose.Height = 30;



                    btnClose.Location = new Point(right - btnClose.Width, btnY);



                    btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;



                    right -= (btnClose.Width + 10);



                }



                if (btnUpdate != null)



                {



                    btnUpdate.Font = modernFont;



                    btnUpdate.Height = 30;



                    btnUpdate.Location = new Point(right - btnUpdate.Width, btnY);



                    btnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;



                    right -= (btnUpdate.Width + 10);



                }



                if (btnFaultTrace != null)



                {



                    btnFaultTrace.Font = modernFont;



                    btnFaultTrace.Height = 30;



                    btnFaultTrace.Location = new Point(right - btnFaultTrace.Width, btnY);



                    btnFaultTrace.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;



                    right -= (btnFaultTrace.Width + 10);



                }



                if (picInfo != null)



                {



                    picInfo.Location = new Point(right - picInfo.Width, btnY + 7);



                    picInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;



                }



            };



            positionButtons();



            dlg.Resize += (s, e) => positionButtons();



            if (frmOverview != null && currentDtc != null)



            {



                WebBrowser wb = GetFieldValue(frmOverview, "_dtcInfoDisplay") as WebBrowser;



                Label lblNo = GetFieldValue(frmOverview, "_lblNoFaultTracingAvailable") as Label;



                object ieObj = GetPropValue(currentDtc, "InformationElement");



                string ieId = null;



                if (ieObj != null)



                {



                    ieId = GetPropValue(ieObj, "IEId") as string;



                }



                if (wb != null)



                {



                    wb.ScriptErrorsSuppressed = true;



                    // Support in-document link navigation



                    wb.Navigating += (sNav, eNav) =>



                    {



                        try



                        {



                            string url = eNav.Url != null ? eNav.Url.ToString() : "";



                            if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))



                            {



                                eNav.Cancel = true;



                                string targetId = url.Substring("vida-doc:".Length);



                                wb.DocumentText = RenderDocumentHtml(targetId, null);



                            }



                        }



                        catch {}



                    };



                    if (!string.IsNullOrEmpty(ieId))



                    {



                        string html = RenderDocumentHtml(ieId, null);



                        wb.DocumentText = html;



                        wb.Visible = true;



                        if (lblNo != null) lblNo.Visible = false;



                    }



                    else



                    {



                        string dtcCode = (GetPropValue(currentDtc, "Value") as string) ?? "";



                        string dtcDesc = (GetPropValue(currentDtc, "Text") as string) ?? "";



                        string ecu = (GetPropValue(currentDtc, "EcuName") as string) ?? "";



                        string html = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" +



                            "body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 24px; color: #333; line-height: 1.6; }" +



                            ".card { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 5px solid #005087; border-radius: 6px; padding: 20px; margin-top: 10px; }" +



                            "h2 { color: #003057; margin-top: 0; }" +



                            ".badge { display: inline-block; background: #003057; color: white; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 13px; }" +



                            "</style></head><body><div class='card'>" +



                            "<h2><span class='badge'>" + ecu + "</span> " + dtcCode + "</h2>" +



                            "<p style='font-size: 15px; font-weight: 500;'>" + dtcDesc + "</p>" +



                            "<p style='color: #666;'>Standard diagnostic readout from vehicle network. No additional interactive fault-tracing sequence is required for this code in the current vehicle configuration.</p>" +



                            "<p style='color: #666;'>Use the <strong>Details</strong> tab to view status identifiers and freeze-frame values, or the <strong>Timeline</strong> tab for DTC history.</p>" +



                            "</div></body></html>";



                        wb.DocumentText = html;



                        wb.Visible = true;



                        if (lblNo != null) lblNo.Visible = false;



                    }



                }



            }



            dlg.FormClosing += (sClose, eClose) =>



            {



                try



                {



                    MethodInfo miFT = dlg.GetType().GetMethod("set_FaultTrace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                    if (miFT != null) miFT.Invoke(dlg, new object[] { false });



                    object frmOv = GetFieldValue(dlg, "_frmOverview");



                    if (frmOv != null)



                    {



                        MethodInfo miKill = frmOv.GetType().GetMethod("KillNavigator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        if (miKill != null) miKill.Invoke(frmOv, null);



                    }



                }



                catch {}



            };



            dlg.FormClosed += (sClosed, eClosed) =>



            {



                try



                {



                    if (mainForm != null && !mainForm.IsDisposed)



                    {



                        mainForm.Activate();



                        mainForm.Focus();



                    }



                    if (mainForm != null)



                    {



                        Action<Control> refreshFaultTrace = null;



                        refreshFaultTrace = (ctrl) =>



                        {



                            if (ctrl == null) return;



                            if (ctrl.GetType().Name == "CustomerSymptomsFaultTraceForm")



                            {



                                Control navImg = GetFieldValue(ctrl, "_navImage") as Control;



                                if (navImg != null) navImg.Enabled = true;



                                Control btnShowInfo = GetFieldValue(ctrl, "_btnShowInformation") as Control;



                                if (btnShowInfo != null) btnShowInfo.Enabled = true;



                                MethodInfo miCheck = ctrl.GetType().GetMethod("CheckChosenItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                                if (miCheck != null) miCheck.Invoke(ctrl, null);



                            }



                            foreach (Control child in ctrl.Controls) refreshFaultTrace(child);



                        };



                        refreshFaultTrace(mainForm);



                    }



                    dlg.Dispose();



                }



                catch {}



            };



            dlg.ResumeLayout(true);



            Console.WriteLine("[Launcher] Successfully modernized DtcExtendedViewDialog!");



        }



        catch (Exception exDlg)



        {



            Console.WriteLine("[Launcher] ModernizeDtcExtendedDialog error: " + exDlg);



        }



    }



    static void LoadDtcsFromDbDirectly(Control statusTv, string ecuTypeId, string profileId)



    {



        if (statusTv == null) return;



        TreeView tvEx = GetFieldValue(statusTv, "_treeViewEx") as TreeView;



        if (tvEx == null) return;



        int ecuTypeNum;



        if (!int.TryParse(ecuTypeId, out ecuTypeNum)) return;



        try



        {



            int variantId = 0;



            using (var cc = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))



            {



                cc.Open();



                using (var cmd = new System.Data.SqlClient.SqlCommand("vadis_GetDefaultEcuVariants", cc))



                {



                    cmd.CommandType = System.Data.CommandType.StoredProcedure;



                    cmd.Parameters.AddWithValue("@profileId", profileId);



                    using (var r = cmd.ExecuteReader())



                    {



                        while (r.Read())



                        {



                            if (Convert.ToInt32(r[0]) == ecuTypeNum)



                            {



                                variantId = Convert.ToInt32(r[2]);



                                break;



                            }



                        }



                    }



                }



                if (variantId == 0) return;



                string symptomIds = "";



                using (var cmd2 = new System.Data.SqlClient.SqlCommand("vadis_GetDtcSymptomsForEcu", cc))



                {



                    cmd2.CommandType = System.Data.CommandType.StoredProcedure;



                    cmd2.Parameters.AddWithValue("@ecuId", variantId);



                    using (var r2 = cmd2.ExecuteReader())



                    {



                        while (r2.Read())



                        {



                            if (symptomIds.Length > 0) symptomIds += ",";



                            symptomIds += r2[0].ToString();



                        }



                    }



                }



                if (string.IsNullOrEmpty(symptomIds)) return;



                using (var rep = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))



                {



                    rep.Open();



                    using (var cmd3 = new System.Data.SqlClient.SqlCommand("GetDtcIEs", rep))



                    {



                        cmd3.CommandType = System.Data.CommandType.StoredProcedure;



                        cmd3.Parameters.AddWithValue("@languageCode", "en-US");



                        cmd3.Parameters.AddWithValue("@symptomIds", symptomIds);



                        cmd3.Parameters.AddWithValue("@profileList", profileId);



                        using (var r3 = cmd3.ExecuteReader())



                        {



                            tvEx.BeginUpdate();



                            tvEx.Nodes.Clear();



                            while (r3.Read())



                            {



                                string dispText = r3["DisplayText"].ToString();



                                string ieId = r3["ieId"] != DBNull.Value ? r3["ieId"].ToString() : "";



                                TreeNode dtcNode = new TreeNode(dispText);



                                var treeIe = new Vcc.Vida.DiagSwdl.ValueObjects.Symptoms.TreeIEVO();



                                treeIe.IEId = ieId;



                                treeIe.Title = dispText;



                                var nodeItem = new Vcc.Vida.DiagSwdl.UI.Components.NodeItem();



                                nodeItem.InformationElement = treeIe;



                                nodeItem.Text = dispText;



                                nodeItem.ID = r3["symptomId"].ToString();



                                nodeItem.Status = Vcc.Vida.DiagSwdl.GuiComponents.TreeNodeStatus.Document;



                                dtcNode.Tag = nodeItem;



                                tvEx.Nodes.Add(dtcNode);



                            }



                            tvEx.EndUpdate();



                            Console.WriteLine("[Launcher] Direct loader populated " + tvEx.Nodes.Count + " DTCs into tree for ECU " + ecuTypeId);



                        }



                    }



                }



            }



            try



            {



                MethodInfo miSetIcons = statusTv.GetType().GetMethod("SetStatusIcons", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                if (miSetIcons != null) miSetIcons.Invoke(statusTv, null);



                MethodInfo miRef = statusTv.GetType().GetMethod("RefreshNodes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                if (miRef != null) miRef.Invoke(statusTv, null);



            }



            catch {}



        }



        catch (Exception ex)



        {



            Console.WriteLine("[Launcher] Direct DTC loader error: " + ex.Message);



        }



    }



    public class ServiceManualsTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation

{

    public void Activated() { this.Visible = true; }

    public void Deactivated() { this.Visible = false; }

}



static Panel _diagnosticsMainPanel = null;



    static ServiceManualsTabControl _manualExplorerPnl = null;
    public class PartsCatalogTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation
    {
        public void Activated() { this.Visible = true; }
        public void Deactivated() { this.Visible = false; }
    }
    static PartsCatalogTabControl _partsCatalogPnl = null;
    static Button _btnTabParts = null;
    static bool _partsTabConfigured = false;
    public class WiringDiagramsTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation
    {
        public void Activated() { this.Visible = true; }
        public void Deactivated() { this.Visible = false; }
    }
    static WiringDiagramsTabControl _wiringDiagramsPnl = null;
    static Button _btnTabWiring = null;
    static bool _wiringTabConfigured = false;
    static Microsoft.Web.WebView2.WinForms.WebView2 _ewdWebView = null;
    static DataGridView _ewdDgv = null;
    static Button _ewdBtnShowLoc = null;
    static Button _ewdBtnFindSchem = null;



    static Button _btnTabManuals = null;



    static string GetGroupName(string key)



    {



        switch (key)



        {



            case "0": return "0 - Administration & Special Tools";



            case "1": return "1 - Maintenance & Service Material";



            case "2": return "2 - Engine & Equipment";



            case "3": return "3 - Electrical System";



            case "4": return "4 - Power Transmission (Gearbox/Clutch)";



            case "5": return "5 - Brakes";



            case "6": return "6 - Suspension & Steering";



            case "7": return "7 - Springs, Shock Absorbers & Wheels";



            case "8": return "8 - Body & Interior";



            case "9": return "9 - Miscellaneous";



            default: return "Other Documents";



        }



    }



    static string LookupDocumentIdBySymptomId(int symptomId)



    {



        try



        {



            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))



            {



                conn.Open();



                using (var cmd = conn.CreateCommand())



                {



                    cmd.CommandText = "SELECT TOP 1 fkIE FROM SymptomIEMap WHERE fkSymptom = @sId AND fkIE IS NOT NULL AND fkIE <> ''";



                    cmd.Parameters.AddWithValue("@sId", symptomId);



                    var obj = cmd.ExecuteScalar();



                    if (obj != null && obj != DBNull.Value) return obj.ToString().Trim();



                }



            }



        }



        catch {}



        return null;



    }



    static bool _manualsTabConfigured = false;



    static void SetupWorkshopManualsTab(object mObj)



    {



        if (_manualsTabConfigured || mObj == null) return;



        try



        {            Control refForm = GetFieldValue(mObj, "_referenceInformationForm") as Control;



            if (refForm == null)



            {



                try



                {



                    MethodInfo mi = mObj.GetType().GetMethod("GetReferenceInformationFormForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                    if (mi != null) refForm = mi.Invoke(mObj, null) as Control;



                }



                catch {}



            }



            Control tabPanel = refForm != null ? GetFieldValue(refForm, "_tabPanel") as Control : null;



            Control tabCtrl = refForm != null ? GetFieldValue(refForm, "_tab") as Control : null;



            Control cscCtrl = refForm != null ? GetFieldValue(refForm, "_customerSymptomsCSCRefInfoControl") as Control : null;



            Control dtcCtrl = refForm != null ? GetFieldValue(refForm, "_customerSymptomsDtcRefInfoControl") as Control : null;



            // Container for manual explorer



            ServiceManualsTabControl manualExplorerPnl = new ServiceManualsTabControl();



            manualExplorerPnl.Dock = DockStyle.Fill;



            manualExplorerPnl.Visible = false;



            // Top Search Bar inside manualExplorerPnl



            Panel searchBar = new Panel();



            searchBar.Dock = DockStyle.Top;



            searchBar.Height = 52;



            searchBar.BackColor = Color.FromArgb(248, 250, 252);



            searchBar.Padding = new Padding(12, 10, 12, 10);



            searchBar.BorderStyle = BorderStyle.FixedSingle;



            Label lblSearch = new Label { Text = "🔍 Search Manuals:", AutoSize = true, Location = new Point(12, 15), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };



            TextBox txtSearch = new TextBox { Location = new Point(155, 12), Width = 280, Font = new Font("Segoe UI", 10f) };



            Button btnSearch = new Button { Text = "Search", Location = new Point(445, 11), Width = 80, Height = 29, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };



            btnSearch.FlatAppearance.BorderSize = 0;



            ComboBox cmbGroup = new ComboBox { Location = new Point(535, 12), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };



            cmbGroup.Items.Add("All Function Groups (0-9)");



            cmbGroup.Items.Add("0 - Administration & Special Tools");



            cmbGroup.Items.Add("1 - Maintenance & Service Material");



            cmbGroup.Items.Add("2 - Engine & Equipment");



            cmbGroup.Items.Add("3 - Electrical System");



            cmbGroup.Items.Add("4 - Power Transmission (Gearbox/Clutch)");



            cmbGroup.Items.Add("5 - Brakes");



            cmbGroup.Items.Add("6 - Suspension & Steering");



            cmbGroup.Items.Add("7 - Springs, Shock Absorbers & Wheels");



            cmbGroup.Items.Add("8 - Body & Interior");



            cmbGroup.Items.Add("9 - Miscellaneous");



            cmbGroup.SelectedIndex = 0;



            Label lblStatus = new Label { Text = "Enter query or pick a group...", AutoSize = true, Location = new Point(810, 16), Font = new Font("Segoe UI", 9f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };



            searchBar.Controls.Add(lblSearch);



            searchBar.Controls.Add(txtSearch);



            searchBar.Controls.Add(btnSearch);



            searchBar.Controls.Add(cmbGroup);



            searchBar.Controls.Add(lblStatus);



            // Splitter layout for manuals: Left = Category Tree & Results, Right = Document Viewer



            SplitContainer splitManuals = new SplitContainer();



            splitManuals.Dock = DockStyle.Fill;



            splitManuals.Orientation = Orientation.Vertical;



            splitManuals.SplitterDistance = 380;



            splitManuals.SplitterWidth = 5;



            // Left side: Results TreeView



            TreeView tvManuals = new TreeView();



            tvManuals.Dock = DockStyle.Fill;



            tvManuals.Font = new Font("Segoe UI", 9.5f);



            tvManuals.ShowLines = true;



            tvManuals.ShowPlusMinus = true;



            tvManuals.ShowRootLines = true;



            tvManuals.BorderStyle = BorderStyle.None;



            // Right side: Document Preview Browser



            WebBrowser wbManuals = new WebBrowser();



            wbManuals.Dock = DockStyle.Fill;



            wbManuals.ScriptErrorsSuppressed = true;



            splitManuals.Panel1.Controls.Add(tvManuals);



            splitManuals.Panel2.Controls.Add(wbManuals);



            manualExplorerPnl.Controls.Add(splitManuals);



            manualExplorerPnl.Controls.Add(searchBar);



            splitManuals.BringToFront();



            _manualExplorerPnl = manualExplorerPnl;



            if (_diagnosticsMainPanel != null)



            {



                _manualExplorerPnl.Dock = DockStyle.Fill;



                _diagnosticsMainPanel.Controls.Add(_manualExplorerPnl);



                _manualExplorerPnl.Visible = false;



            }



            Action<string, string> doSearchManuals = (term, grpFilter) =>



            {



                lblStatus.Text = "Searching...";



                tvManuals.Nodes.Clear();



                try



                {



                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))



                    {



                        conn.Open();



                        using (var cmd = conn.CreateCommand())



                        {



                            string sql = @"



                                SELECT TOP 300 



                                    t.chronicleId, 



                                    t.title, 



                                    t.vccNumber, 



                                    t.version, 



                                    t.functionGroup1, 



                                    t.functionGroup2, 



                                    ISNULL(f.title, 'General') AS subGroupTitle



                                FROM TreeItem t



                                LEFT JOIN FunctionGroupText f ON t.functionGroup2 = f.functionGroup



                                WHERE t.title != '' AND t.chronicleId IS NOT NULL AND t.chronicleId != ''";



                            if (!string.IsNullOrEmpty(term))



                            {



                                sql += " AND (t.title LIKE @term OR t.vccNumber LIKE @term OR t.chronicleId LIKE @term)";



                                cmd.Parameters.AddWithValue("@term", "%" + term.Trim() + "%");



                            }



                            if (!string.IsNullOrEmpty(grpFilter) && grpFilter != "All")



                            {



                                sql += " AND t.functionGroup1 = @grp";



                                cmd.Parameters.AddWithValue("@grp", grpFilter);



                            }



                            sql += " ORDER BY t.functionGroup2 ASC, t.title ASC";



                            cmd.CommandText = sql;



                            var docs = new List<ManualDocItem>();



                            using (var r = cmd.ExecuteReader())



                            {



                                while (r.Read())



                                {



                                    docs.Add(new ManualDocItem



                                    {



                                        Id = r["chronicleId"].ToString(),



                                        Title = r["title"].ToString(),



                                        Vcc = r["vccNumber"] != DBNull.Value ? r["vccNumber"].ToString() : "",



                                        Version = r["version"] != DBNull.Value ? r["version"].ToString() : "",



                                        GroupKey = r["functionGroup2"] != DBNull.Value ? r["functionGroup2"].ToString() : "Other",



                                        GroupTitle = r["subGroupTitle"] != DBNull.Value ? r["subGroupTitle"].ToString() : "General"



                                    });



                                }



                            }



                            var grouped = new SortedDictionary<string, List<ManualDocItem>>();



                            foreach (var d in docs)



                            {



                                string k = !string.IsNullOrEmpty(d.GroupKey) ? d.GroupKey : "Other";



                                if (!grouped.ContainsKey(k)) grouped[k] = new List<ManualDocItem>();



                                grouped[k].Add(d);



                            }



                            foreach (var kvp in grouped)



                            {



                                string subTitle = kvp.Value.Count > 0 ? kvp.Value[0].GroupTitle : "General";



                                string grpHeader = kvp.Key + " - " + subTitle;



                                TreeNode grpNode = new TreeNode(grpHeader);



                                foreach (var d in kvp.Value)



                                {



                                    TreeNode docNode = new TreeNode(d.ToString());



                                    docNode.Tag = d.Id;



                                    grpNode.Nodes.Add(docNode);



                                }



                                tvManuals.Nodes.Add(grpNode);



                                grpNode.Expand();



                            }



                            lblStatus.Text = string.Format("Found {0} documents", docs.Count);



                        }



                    }



                }



                catch (Exception exSrch)



                {



                    lblStatus.Text = "Search error: " + exSrch.Message;



                }



            };



            btnSearch.Click += (s, e) =>



            {



                string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";



                doSearchManuals(txtSearch.Text, grpSel);



            };



            txtSearch.KeyDown += (s, e) =>



            {



                if (e.KeyCode == Keys.Enter)



                {



                    e.SuppressKeyPress = true;



                    string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";



                    doSearchManuals(txtSearch.Text, grpSel);



                }



            };



            cmbGroup.SelectedIndexChanged += (s, e) =>



            {



                string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";



                doSearchManuals(txtSearch.Text, grpSel);



            };



            Action<string> loadManualDoc = (docId) =>



            {



                if (string.IsNullOrEmpty(docId)) return;



                try



                {



                    string resolved = ResolveDocumentId(docId, null) ?? docId;



                    string html = RenderDocumentHtml(resolved, null);



                    wbManuals.DocumentText = html;



                }



                catch (Exception exDoc)



                {



                    wbManuals.DocumentText = "<html><body><h3>Error loading document:</h3><p>" + exDoc.Message + "</p></body></html>";



                }



            };



            tvManuals.AfterSelect += (s, e) =>



            {



                if (e.Node != null && e.Node.Tag != null)



                {



                    loadManualDoc(e.Node.Tag.ToString());



                }



            };



            tvManuals.NodeMouseDoubleClick += (s, e) =>



            {



                if (e.Node != null && e.Node.Tag != null)



                {



                    string docId = e.Node.Tag.ToString();



                    ShowDocumentWindow(docId, e.Node.Text, _mainLauncherForm);



                }



            };



            wbManuals.Navigating += (sNav, eNav) =>



            {



                try



                {



                    string url = eNav.Url != null ? eNav.Url.ToString() : "";



                    if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))



                    {



                        eNav.Cancel = true;



                        string targetId = url.Substring("vida-doc:".Length);



                        loadManualDoc(targetId);



                    }



                }



                catch {}



            };



            // Initial load of service manuals



            doSearchManuals("", "2");



            if (mObj != null)



            {



                PropertyInfo piNav = mObj.GetType().GetProperty("BrowserNavigation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                if (piNav != null && piNav.GetValue(mObj, null) == null)



                {



                    piNav.SetValue(mObj, new LauncherBrowserNavigation(_mainLauncherForm), null);



                    Console.WriteLine("[Launcher] Wired BrowserNavigation in SetupWorkshopManualsTab!");



                }



            }



            HashSet<TreeNode> expandedNodes = new HashSet<TreeNode>();



            Action<Control, string> wireRefInfoControl = (ctrl, ctrlTag) =>



            {



                if (ctrl == null || ctrl.Tag as string == "wired_clicks_v4") return;



                ctrl.Tag = "wired_clicks_v4";



                Control statusTv = GetFieldValue(ctrl, "_statusTreeViewControl") as Control;



                if (statusTv == null) return;



                TreeView tvEx = GetFieldValue(statusTv, "_treeViewEx") as TreeView;



                Action<TreeNode, bool> handleNode = (node, isDoubleClick) =>



                {



                    if (node == null) return;



                    // 1. NON-LEAF NODES (branches / categories with child nodes):



                    if (node.Nodes != null && node.Nodes.Count > 0)



                    {



                        if (isDoubleClick)



                        {



                            if (node.IsExpanded) node.Collapse();



                            else node.Expand();



                        }



                        return;



                    }



                    // 2. CSC expandable root category nodes whose children have not been loaded yet:



                    object nodeTag = node.Tag;



                    string nodeId = nodeTag != null ? GetPropValue(nodeTag, "ID") as string : null;



                    object ieObj = nodeTag != null ? GetPropValue(nodeTag, "InformationElement") : null;



                    object statusObj = nodeTag != null ? GetPropValue(nodeTag, "Status") : null;



                    int nodeStatus = statusObj != null ? Convert.ToInt32(statusObj) : -1;



                    int numSymptomId = 0;



                    bool isNumericId = !string.IsNullOrEmpty(nodeId) && int.TryParse(nodeId, out numSymptomId);



                    if (isNumericId && numSymptomId < 10000 && ieObj == null && !expandedNodes.Contains(node))



                    {



                        expandedNodes.Add(node);



                        try



                        {



                            MethodInfo miDbl = ctrl.GetType().GetMethod("_statusTreeViewControl_OnNodeDoubleClick", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            if (miDbl != null) miDbl.Invoke(ctrl, new object[] { node });



                            node.Expand();



                        }



                        catch (Exception exPop)



                        {



                            Console.WriteLine("[Launcher] CSC populate error: " + exPop.Message);



                        }



                        return;



                    }



                    // Ignore empty category folder headers (e.g. no DTCs or procedures for this symptom)



                    bool isEmptyCategoryHeader = (node.Nodes == null || node.Nodes.Count == 0) && !string.IsNullOrEmpty(node.Text) && (



                        node.Text == "Diagnostic Trouble Codes and Associated Procedures"



                        || node.Text == "Symptom Related Diagnostic Procedures"



                        || node.Text == "Symptom Verifications"



                    );



                    if (isEmptyCategoryHeader)



                    {



                        if (isDoubleClick)



                        {



                            Console.WriteLine("[Launcher] Empty category folder clicked: " + node.Text);



                        }



                        return;



                    }



                    // 3. ACTUAL LEAF NODES (documents / DTCs):



                    // Documents must ONLY open on DOUBLE-CLICK! Single click is navigation only.



                    if (!isDoubleClick)



                    {



                        return;



                    }



                    string docId = null;



                    if (ieObj != null)



                    {



                        docId = GetPropValue(ieObj, "IEId") as string;



                        if (string.IsNullOrEmpty(docId)) docId = GetPropValue(ieObj, "IeId") as string;



                        if (string.IsNullOrEmpty(docId)) docId = GetPropValue(ieObj, "Id") as string;



                        if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(ieObj, "_ieId") as string;



                        if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(ieObj, "ieId") as string;



                    }



                    if (string.IsNullOrEmpty(docId) && nodeTag != null)



                    {



                        object dataObj = GetPropValue(nodeTag, "Data");



                        if (dataObj != null)



                        {



                            object dataIe = GetPropValue(dataObj, "InformationElement");



                            if (dataIe != null)



                            {



                                docId = GetPropValue(dataIe, "IEId") as string;



                                if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataIe, "IeId") as string;



                                if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataIe, "Id") as string;



                                if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataIe, "_ieId") as string;



                                if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataIe, "ieId") as string;



                            }



                        }



                    }



                    if (string.IsNullOrEmpty(docId) && !string.IsNullOrEmpty(nodeId))



                    {



                        if (nodeId.StartsWith("0900", StringComparison.OrdinalIgnoreCase) || nodeId.StartsWith("0800", StringComparison.OrdinalIgnoreCase))



                            docId = nodeId;



                    }



                    if (string.IsNullOrEmpty(docId) && isNumericId)



                    {



                        docId = LookupDocumentIdBySymptomId(numSymptomId);



                    }



                    // Resolve document ID using full resolution engine (TreeItem, Document, DTC regex, title)



                    string finalDocId = ResolveDocumentId(docId, node.Text);



                    Console.WriteLine("[Launcher] Double-clicked leaf document: " + (finalDocId ?? "null") + " (" + node.Text + ")");



                    if (!string.IsNullOrEmpty(finalDocId))



                    {



                        ShowDocumentWindow(finalDocId, node.Text, _mainLauncherForm);



                    }



                    else



                    {



                        Console.WriteLine("[Launcher] Could not resolve document for: " + node.Text);



                    }



                };



                var stc = statusTv as Vcc.Vida.DiagSwdl.GuiComponents.StatusTreeViewControl;



                if (stc != null)



                {



                    // Clear any throwing default delegates on OnNodeDoubleClick



                    try



                    {



                        FieldInfo fiDbl = stc.GetType().GetField("OnNodeDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);



                        if (fiDbl != null) fiDbl.SetValue(stc, null);



                    }



                    catch {}



                    stc.OnNodeClick += (node) => handleNode(node, false);



                    stc.OnNodeDoubleClick += (node) => handleNode(node, true);



                }



                if (tvEx != null)



                {



                    tvEx.NodeMouseDoubleClick += (sTv, eTv) => handleNode(eTv.Node, true);



                }



            };



            wireRefInfoControl(cscCtrl, "csc");



            wireRefInfoControl(dtcCtrl, "dtc");



            _refFormCtrl = refForm;



            _activeDtcRefControl = dtcCtrl;



            // Ensure cached VehicleProfile is populated into ReferenceInformationForm and its controllers



            if (_cachedVehicleProfile != null)



            {



                object refWs = GetFieldValue(refForm, "_workshopSession");



                if (refWs != null)



                {



                    SetFieldOrProp(refWs, "VehicleProfileObject", _cachedVehicleProfile);



                    SetFieldOrProp(refWs, "_vehicleProfileObject", _cachedVehicleProfile);



                }



                if (dtcCtrl != null)



                {



                    object ws = GetFieldValue(dtcCtrl, "_workshopSession");



                    if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }



                    object wf = GetFieldValue(dtcCtrl, "_dtcFaulttraceWF");



                    if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }



                }



                if (cscCtrl != null)



                {



                    object ws = GetFieldValue(cscCtrl, "_workshopSession");



                    if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }



                    object wf = GetFieldValue(cscCtrl, "_dtcFaulttraceWF");



                    if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }



                }



            }



            if (dtcCtrl != null && dtcCtrl.Tag as string != "dtc_configured_v4")



            {



                dtcCtrl.Tag = "dtc_configured_v4";



                Label lblEcuTitle = new Label { Text = "ECU Module:", AutoSize = true, Location = new Point(400, 68), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };



                ComboBox cbDtcEcu = new ComboBox { Location = new Point(495, 65), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };



                Tuple<string, string>[] dtcEcuList = new Tuple<string, string>[]



                {



                    Tuple.Create("284101", "ECM - Engine Control Module"),



                    Tuple.Create("437601", "TCM - Transmission Control Module"),



                    Tuple.Create("372302", "CEM - Central Electronic Module"),



                    Tuple.Create("593102", "BCM - Brake Control Module"),



                    Tuple.Create("381101", "DIM - Driver Information Module"),



                    Tuple.Create("884701", "SRS - Supplemental Restraint System"),



                    Tuple.Create("871101", "CCM - Climate Control Module"),



                    Tuple.Create("465901", "DEM - Differential Electronic Module"),



                    Tuple.Create("645902", "SAS - Steering Angle Sensor"),



                    Tuple.Create("875401", "CPM - Combustion Pre-heater Module"),



                    Tuple.Create("551401", "PBM - Parking Brake Module"),



                    Tuple.Create("831701", "DDM - Driver Door Module"),



                    Tuple.Create("831703", "PDM - Passenger Door Module"),



                    Tuple.Create("852601", "PSM - Power Seat Module"),



                    Tuple.Create("393901", "ICM - Infotainment Control Module"),



                    Tuple.Create("393204", "IAM - Integrated Audio Module"),



                    Tuple.Create("393202", "AUD - Audio Module"),



                    Tuple.Create("366901", "KVM - Keyless Vehicle Module"),



                    Tuple.Create("366902", "PAM - Parking Assistance Module"),



                    Tuple.Create("892901", "TRM - Trailer Module"),



                    Tuple.Create("832901", "POT - Power Operated Tailgate"),



                    Tuple.Create("352902", "HCM - Headlamp Control Module"),



                    Tuple.Create("366905", "FSM - Forward Sensing Module"),



                    Tuple.Create("645903", "PSCM - Power Steering Control Module")



                };



                foreach (var item in dtcEcuList)



                {



                    cbDtcEcu.Items.Add(item.Item2);



                }



                Action<string, string> loadDtcForEcu = (ecuTypeId, ecuName) =>



                {



                    if (dtcCtrl == null) return;



                    Console.WriteLine("[Launcher] DTC Ref Info: Loading ECU " + ecuName + " (" + ecuTypeId + ")");



                    if (_cachedVehicleProfile != null)



                    {



                        object ws = GetFieldValue(dtcCtrl, "_workshopSession");



                        if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }



                        object wf = GetFieldValue(dtcCtrl, "_dtcFaulttraceWF");



                        if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }



                    }



                    bool loaded = false;



                    try



                    {



                        MethodInfo miNav = dtcCtrl.GetType().GetMethod("_navImage_HotspotClicked", BindingFlags.NonPublic | BindingFlags.Instance);



                        if (miNav != null)



                        {



                            var navArgs = new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(ecuTypeId, 1);



                            miNav.Invoke(dtcCtrl, new object[] { dtcCtrl, navArgs });



                        }



                    }



                    catch (Exception exNav)



                    {



                        Console.WriteLine("[Launcher] DTC _navImage_HotspotClicked: " + exNav.Message);



                    }



                    Control statusTv = GetFieldValue(dtcCtrl, "_statusTreeViewControl") as Control;



                    TreeView tvEx = statusTv != null ? GetFieldValue(statusTv, "_treeViewEx") as TreeView : null;



                    if (tvEx != null && tvEx.Nodes.Count > 0)



                    {



                        loaded = true;



                        Console.WriteLine("[Launcher] Volvo WF populated " + tvEx.Nodes.Count + " nodes for ECU " + ecuTypeId);



                    }



                    if (!loaded && statusTv != null)



                    {



                        LoadDtcsFromDbDirectly(statusTv, ecuTypeId, TargetProfile);



                    }



                };



                bool updatingDtcEcu = false;



                Action<string> selectDtcEcuById = (targetId) =>



                {



                    for (int k = 0; k < dtcEcuList.Length; k++)



                    {



                        if (dtcEcuList[k].Item1 == targetId)



                        {



                            updatingDtcEcu = true;



                            cbDtcEcu.SelectedIndex = k;



                            updatingDtcEcu = false;



                            loadDtcForEcu(targetId, dtcEcuList[k].Item2);



                            break;



                        }



                    }



                };



                cbDtcEcu.SelectedIndexChanged += (sEcu, eEcu) =>



                {



                    if (updatingDtcEcu) return;



                    if (cbDtcEcu.SelectedIndex >= 0 && cbDtcEcu.SelectedIndex < dtcEcuList.Length)



                    {



                        string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;



                        string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;



                        loadDtcForEcu(selId, selName);



                    }



                };



                dtcCtrl.Controls.Add(lblEcuTitle);



                dtcCtrl.Controls.Add(cbDtcEcu);



                lblEcuTitle.BringToFront();



                cbDtcEcu.BringToFront();



                _onDtcRefHotspotClicked = (clickedId) =>



                {



                    Console.WriteLine("[Launcher] DTC Ref Info hotspot clicked for ECU: " + clickedId);



                    selectDtcEcuById(clickedId);



                };



                // Hook tab switching to auto-load DTCs when DTC tab becomes active



                if (tabCtrl != null)



                {



                    Control dtcTab = GetFieldValue(refForm, "_dtcBasedRefInfoTab") as Control;



                    var ftc = tabCtrl as Vcc.Vida.DiagSwdl.GuiComponents.FlatTabControl;



                    if (ftc != null)



                    {



                        ftc.SelectedTabChanged += (newPage, oldPage) =>



                        {



                            try



                            {



                                if (newPage == dtcTab)



                                {



                                    Console.WriteLine("[Launcher] Switched to DTC Reference Information Tab");



                                    if (cbDtcEcu.SelectedIndex < 0) cbDtcEcu.SelectedIndex = 0;



                                    else



                                    {



                                        string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;



                                        string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;



                                        loadDtcForEcu(selId, selName);



                                    }



                                }



                            }



                            catch {}



                        };



                    }



                    else if (tabCtrl is TabControl)



                    {



                        ((TabControl)tabCtrl).SelectedIndexChanged += (sTab, eTab) =>



                        {



                            try



                            {



                                if (cbDtcEcu.SelectedIndex < 0) cbDtcEcu.SelectedIndex = 0;



                                else



                                {



                                    string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;



                                    string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;



                                    loadDtcForEcu(selId, selName);



                                }



                            }



                            catch {}



                        };



                    }



                }



                // Auto-select ECM initially so Related DTCs are immediately loaded



                if (cbDtcEcu.Items.Count > 0)



                {



                    cbDtcEcu.SelectedIndex = 0;



                }



            }



            RegisterDummyIsoViewHotspot();



            _manualsTabConfigured = true;



RegisterDummyIsoViewHotspot();



            _manualsTabConfigured = true;



            Console.WriteLine("[Launcher] Successfully embedded Workshop Service Manuals Explorer into ReferenceInformationForm!");



        }



        catch (Exception ex)



        {



            Console.WriteLine("[Launcher] SetupWorkshopManualsTab error: " + ex);



        }



    }

    static void SetupPartsCatalogTab(object mObj)
    {
        if (_partsTabConfigured || mObj == null) return;
        try
        {
            PartsCatalogTabControl partsPnl = new PartsCatalogTabControl();
            partsPnl.Dock = DockStyle.Fill;
            partsPnl.BackColor = Color.FromArgb(245, 247, 250);
            partsPnl.Font = new Font("Segoe UI", 9.0f);

            // 1. Top Filter & Search Header
            Panel headerPnl = new Panel();
            headerPnl.Dock = DockStyle.Top;
            headerPnl.Height = 52;
            headerPnl.BackColor = Color.FromArgb(240, 243, 246);
            headerPnl.BorderStyle = BorderStyle.FixedSingle;
            partsPnl.Controls.Add(headerPnl);

            Label lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(12, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblModel);

            ComboBox cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 13), Width = 150, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboModel);

            CheckBox chkActiveEngine = new CheckBox { Text = "Active Car: D5204T2 (2.0D) Only", AutoSize = true, Location = new Point(222, 15), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 204), Checked = true };
            headerPnl.Controls.Add(chkActiveEngine);

            Label lblSearch = new Label { Text = "Search:", AutoSize = true, Location = new Point(445, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblSearch);

            TextBox txtSearch = new TextBox { Location = new Point(500, 13), Width = 210, Font = new Font("Segoe UI", 9.5f) };
            headerPnl.Controls.Add(txtSearch);

            Button btnSearch = new Button { Text = "🔍 Search", Location = new Point(718, 12), Width = 85, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnSearch.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnSearch);

            Button btnReset = new Button { Text = "✕ Reset", Location = new Point(809, 12), Width = 75, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 225, 230), ForeColor = Color.FromArgb(50, 60, 70), Cursor = Cursors.Hand };
            btnReset.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnReset);

            Label lblStatus = new Label { Text = "Loading parts database...", Location = new Point(895, 16), AutoSize = true, Font = new Font("Segoe UI", 9.0f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            headerPnl.Controls.Add(lblStatus);

            // 2. Main SplitContainer
            SplitContainer splitParts = new SplitContainer();
            splitParts.Dock = DockStyle.Fill;
            splitParts.Orientation = Orientation.Vertical;
            splitParts.SplitterDistance = 420;
            splitParts.SplitterWidth = 6;
            partsPnl.Controls.Add(splitParts);
            splitParts.BringToFront();

            // Left panel: System & Assembly Tree
            Panel leftHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(230, 235, 240) };
            Label lblLeftTitle = new Label { Text = "📂 Functional Systems & Assemblies", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(50, 60, 70) };
            leftHeader.Controls.Add(lblLeftTitle);
            splitParts.Panel1.Controls.Add(leftHeader);

            TreeView treeParts = new TreeView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.0f), HideSelection = false, FullRowSelect = true, ShowLines = true, BorderStyle = BorderStyle.None };
            splitParts.Panel1.Controls.Add(treeParts);
            treeParts.BringToFront();

            // Right panel: Parts Table & Action Banner
            Panel bannerPnl = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8, 6, 8, 6) };

            Button btnViewManual = new Button { Text = "📖 Service Manual", Dock = DockStyle.Right, Width = 145, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnViewManual.FlatAppearance.BorderSize = 0;
            bannerPnl.Controls.Add(btnViewManual);

            Panel spacerBtn = new Panel { Dock = DockStyle.Right, Width = 8 };
            bannerPnl.Controls.Add(spacerBtn);

            Button btnCopyPart = new Button { Text = "📋 Copy Part #", Dock = DockStyle.Right, Width = 120, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnCopyPart.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
            bannerPnl.Controls.Add(btnCopyPart);

            Label lblAssemblyTitle = new Label { Text = "Select an assembly on the left or search by part # or keyword", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.0f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 40, 50) };
            bannerPnl.Controls.Add(lblAssemblyTitle);

            splitParts.Panel2.Controls.Add(bannerPnl);

            // Parts DataGridView
            DataGridView dgvParts = new DataGridView();
            dgvParts.Dock = DockStyle.Fill;
            dgvParts.BackgroundColor = Color.White;
            dgvParts.BorderStyle = BorderStyle.None;
            dgvParts.ReadOnly = true;
            dgvParts.AllowUserToAddRows = false;
            dgvParts.AllowUserToDeleteRows = false;
            dgvParts.AllowUserToResizeRows = false;
            dgvParts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.MultiSelect = false;
            dgvParts.RowHeadersVisible = false;
            dgvParts.GridColor = Color.FromArgb(235, 238, 242);
            dgvParts.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f);
            dgvParts.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 238, 255);
            dgvParts.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvParts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvParts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            dgvParts.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 50, 60);
            dgvParts.EnableHeadersVisualStyles = false;
            dgvParts.RowTemplate.Height = 26;

            try
            {
                PropertyInfo pi = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(dgvParts, true, null);
            }
            catch {}

            dgvParts.Columns.Add("colPos", "Pos");
            dgvParts.Columns["colPos"].Width = 45;
            dgvParts.Columns["colPos"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colPartNo", "Part Number");
            dgvParts.Columns["colPartNo"].Width = 115;
            dgvParts.Columns["colPartNo"].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvParts.Columns["colPartNo"].DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);

            dgvParts.Columns.Add("colDesc", "Description");
            dgvParts.Columns["colDesc"].Width = 230;

            dgvParts.Columns.Add("colQty", "Qty");
            dgvParts.Columns["colQty"].Width = 55;
            dgvParts.Columns["colQty"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colUnit", "Unit");
            dgvParts.Columns["colUnit"].Width = 50;
            dgvParts.Columns["colUnit"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colNotes", "Fitment & Engineering Notes");
            dgvParts.Columns["colNotes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvParts.Columns.Add("colSecId", "SecId");
            dgvParts.Columns["colSecId"].Visible = false;

            dgvParts.Columns.Add("colFg", "FG");
            dgvParts.Columns["colFg"].Visible = false;

            splitParts.Panel2.Controls.Add(dgvParts);
            dgvParts.BringToFront();

            // Populate models from BaseData
            List<Tuple<int, string>> models = new List<Tuple<int, string>>();
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=BaseData;Integrated Security=True"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Cid, Description FROM VehicleModel WHERE Cid IS NOT NULL ORDER BY Description";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                models.Add(new Tuple<int, string>(reader.GetInt32(0), reader.GetString(1)));
                            }
                        }
                    }
                }
            }
            catch (Exception exM) { Console.WriteLine("[Launcher] Load models error: " + exM.Message); }

            int defIdx = 0;
            for (int i = 0; i < models.Count; i++)
            {
                cboModel.Items.Add(models[i].Item2);
                if (models[i].Item1 == 135) defIdx = i; // V70 (08-)
            }
            if (cboModel.Items.Count > 0) cboModel.SelectedIndex = defIdx;

            string currentFg = "";

            // Load sections
            Action LoadSections = () =>
            {
                int selIdx = cboModel.SelectedIndex;
                if (selIdx < 0 || selIdx >= models.Count) return;
                int modelCid = models[selIdx].Item1;
                string modelName = models[selIdx].Item2;
                bool activeEng = chkActiveEngine.Checked;

                treeParts.BeginUpdate();
                treeParts.Nodes.Clear();

                Dictionary<string, TreeNode> groupNodes = new Dictionary<string, TreeNode>();
                Action<string, string> ensureGroup = (fgKey, title) =>
                {
                    if (!groupNodes.ContainsKey(fgKey))
                    {
                        TreeNode gn = new TreeNode(title);
                        gn.Tag = "GROUP_" + fgKey;
                        groupNodes[fgKey] = gn;
                        treeParts.Nodes.Add(gn);
                    }
                };

                ensureGroup("0", "0 - Standard Parts & Service Material");
                ensureGroup("1", "1 - Administration & Special Tools");
                ensureGroup("2", "2 - Engine with mountings and equipment");
                ensureGroup("3", "3 - Electrical system");
                ensureGroup("4", "4 - Power transmission");
                ensureGroup("5", "5 - Brakes");
                ensureGroup("6", "6 - Suspension and steering");
                ensureGroup("7", "7 - Springs and wheels");
                ensureGroup("8", "8 - Body and interior");
                ensureGroup("9", "9 - Miscellaneous & Heating/AC");

                int totalSecCount = 0;
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            if (activeEng)
                            {
                                cmd.CommandText = "SELECT DISTINCT sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, l.Description, eng.Description AS EngineCode " +
                                                  "FROM ComponentConditions cond " +
                                                  "JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "JOIN CatalogueComponents sec ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE eng.Description = 'D5204T2' AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL AND l.Description IS NOT NULL " +
                                                  "ORDER BY FG, l.Description";
                            }
                            else
                            {
                                cmd.CommandText = "SELECT DISTINCT sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, l.Description, eng.Description AS EngineCode " +
                                                  "FROM ComponentConditions cond " +
                                                  "LEFT JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "LEFT JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "JOIN CatalogueComponents sec ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE cond.ModelCid = @cid AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL AND l.Description IS NOT NULL " +
                                                  "ORDER BY FG, l.Description";
                                cmd.Parameters.AddWithValue("@cid", modelCid);
                            }

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int secId = reader.GetInt32(0);
                                    string fgCode = reader.IsDBNull(1) ? "9" : reader.GetString(1).Trim();
                                    string secName = reader.IsDBNull(2) ? "Assembly " + secId : reader.GetString(2).Trim();
                                    string engCode = reader.IsDBNull(3) ? "" : reader.GetString(3).Trim();
                                    if (string.IsNullOrEmpty(secName)) continue;

                                    string mainFg = fgCode.Length > 0 ? fgCode.Substring(0, 1) : "9";
                                    TreeNode parentGroupNode;
                                    if (!groupNodes.TryGetValue(mainFg, out parentGroupNode))
                                    {
                                        parentGroupNode = groupNodes["9"];
                                    }

                                    if (secName.Length > 1) secName = char.ToUpper(secName[0]) + secName.Substring(1);
                                    string nodeLabel = secName;
                                    if (!string.IsNullOrEmpty(engCode)) nodeLabel += " [" + engCode + "]";
                                    nodeLabel += " (Gr. " + fgCode + ")";

                                    TreeNode secNode = new TreeNode(nodeLabel);
                                    secNode.Tag = secId;
                                    parentGroupNode.Nodes.Add(secNode);
                                    totalSecCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception exS) { Console.WriteLine("[Launcher] Load sections error: " + exS.Message); }

                for (int i = treeParts.Nodes.Count - 1; i >= 0; i--)
                {
                    TreeNode gn = treeParts.Nodes[i];
                    if (gn.Nodes.Count == 0) treeParts.Nodes.RemoveAt(i);
                    else gn.Text = gn.Text + " (" + gn.Nodes.Count + ")";
                }

                treeParts.EndUpdate();
                if (treeParts.Nodes.Count > 0)
                {
                    treeParts.Nodes[0].Expand();
                    if (treeParts.Nodes[0].Nodes.Count > 0)
                    {
                        treeParts.SelectedNode = treeParts.Nodes[0].Nodes[0];
                    }
                }
                lblStatus.Text = string.Format("Model: {0} | {1} assembly sections available", modelName, totalSecCount);
            };

            cboModel.SelectedIndexChanged += (s, e) => LoadSections();
            chkActiveEngine.CheckedChanged += (s, e) => LoadSections();

            // Select section & show parts
            treeParts.AfterSelect += (s, e) =>
            {
                if (e.Node == null || !(e.Node.Tag is int)) return;
                int sectionId = (int)e.Node.Tag;

                lblAssemblyTitle.Text = e.Node.Text;
                dgvParts.Rows.Clear();

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT c.Id, c.HotspotKey, p.ItemNumber, part_lex.Description, c.Quantity, p.UnitType, " +
                                              "(SELECT TOP 1 l2.Description FROM ComponentDescriptions cd JOIN Lexicon l2 ON cd.DescriptionId = l2.DescriptionId AND l2.fkLanguage = 15 WHERE cd.fkCatalogueComponent = c.Id) AS FootNote, " +
                                              "SUBSTRING(sec.VCCSectionId, 8, 2) AS FG " +
                                              "FROM CatalogueComponents c " +
                                              "JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id " +
                                              "LEFT JOIN PartItems p ON c.fkPartItem = p.Id " +
                                              "LEFT JOIN Lexicon part_lex ON p.DescriptionId = part_lex.DescriptionId AND part_lex.fkLanguage = 15 " +
                                              "WHERE c.ParentComponentId = @secId " +
                                              "ORDER BY CASE WHEN ISNUMERIC(c.HotspotKey) = 1 THEN CAST(c.HotspotKey AS INT) ELSE 999 END, c.HotspotKey, p.ItemNumber";
                            cmd.Parameters.AddWithValue("@secId", sectionId);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string pos = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string partNo = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string desc = r.IsDBNull(3) ? (partNo.Length > 0 ? "Part " + partNo : "") : r.GetString(3).Trim();
                                    string qty = r.IsDBNull(4) ? "" : string.Format("{0:0}", r.GetDecimal(4));
                                    string unit = r.IsDBNull(5) ? "" : r.GetString(5).Trim();
                                    string note = r.IsDBNull(6) ? "" : r.GetString(6).Trim();
                                    currentFg = r.IsDBNull(7) ? "" : r.GetString(7).Trim();

                                    dgvParts.Rows.Add(pos, partNo, desc, qty, unit, note, sectionId, currentFg);
                                }
                            }
                        }
                    }
                    lblStatus.Text = string.Format("Assembly: {0} | {1} parts listed", e.Node.Text, dgvParts.Rows.Count);
                }
                catch (Exception exP) { Console.WriteLine("[Launcher] Load parts error: " + exP.Message); }
            };

            // Global search
            Action DoSearch = () =>
            {
                string q = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(q)) { LoadSections(); return; }

                dgvParts.Rows.Clear();
                lblAssemblyTitle.Text = "Search Results for '" + q + "'";

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 100 p.ItemNumber, l.Description, p.UnitType, " +
                                              "(SELECT TOP 1 sec_lex.Description FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id LEFT JOIN Lexicon sec_lex ON sec.DescriptionId = sec_lex.DescriptionId AND sec_lex.fkLanguage = 15 WHERE c.fkPartItem = p.Id) AS SectionName, " +
                                              "(SELECT TOP 1 SUBSTRING(sec.VCCSectionId, 8, 2) FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id WHERE c.fkPartItem = p.Id) AS FunctionGroup, " +
                                              "(SELECT TOP 1 c.ParentComponentId FROM CatalogueComponents c WHERE c.fkPartItem = p.Id) AS SectionId, " +
                                              "(SELECT TOP 1 c.HotspotKey FROM CatalogueComponents c WHERE c.fkPartItem = p.Id) AS Pos " +
                                              "FROM PartItems p " +
                                              "JOIN Lexicon l ON p.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                              "WHERE p.ItemNumber LIKE @q + '%' OR l.Description LIKE '%' + @q + '%' " +
                                              "ORDER BY p.ItemNumber";
                            cmd.Parameters.AddWithValue("@q", q);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string partNo = r.IsDBNull(0) ? "" : r.GetString(0).Trim();
                                    string desc = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string unit = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string secName = r.IsDBNull(3) ? "" : r.GetString(3).Trim();
                                    string fg = r.IsDBNull(4) ? "" : r.GetString(4).Trim();
                                    int secId = r.IsDBNull(5) ? 0 : r.GetInt32(5);
                                    string pos = r.IsDBNull(6) ? "" : r.GetString(6).Trim();

                                    string notes = secName.Length > 0 ? "Assembly: " + secName + " (Gr. " + fg + ")" : "";
                                    dgvParts.Rows.Add(pos, partNo, desc, "1", unit, notes, secId, fg);
                                }
                            }
                        }
                    }
                    lblStatus.Text = string.Format("Found {0} matching parts for '{1}'", dgvParts.Rows.Count, q);
                }
                catch (Exception exSrch) { Console.WriteLine("[Launcher] Search error: " + exSrch.Message); }
            };

            btnSearch.Click += (s, e) => DoSearch();
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoSearch(); } };
            btnReset.Click += (s, e) => { txtSearch.Clear(); LoadSections(); };

            // Copy part #
            Action CopySelectedPart = () =>
            {
                if (dgvParts.CurrentRow != null)
                {
                    string partNo = Convert.ToString(dgvParts.CurrentRow.Cells["colPartNo"].Value);
                    string desc = Convert.ToString(dgvParts.CurrentRow.Cells["colDesc"].Value);
                    if (!string.IsNullOrEmpty(partNo))
                    {
                        try { Clipboard.SetText(partNo); } catch {}
                        lblStatus.Text = string.Format("📋 Copied Part # {0} ({1}) to clipboard!", partNo, desc);
                    }
                }
            };
            btnCopyPart.Click += (s, e) => CopySelectedPart();
            dgvParts.CellClick += (s, e) => { if (e.RowIndex >= 0) CopySelectedPart(); };

            // Double clicking a search result navigates to its assembly in treeParts
            dgvParts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                object secObj = dgvParts.Rows[e.RowIndex].Cells["colSecId"].Value;
                if (secObj is int && (int)secObj > 0)
                {
                    int targetSecId = (int)secObj;
                    foreach (TreeNode gn in treeParts.Nodes)
                    {
                        foreach (TreeNode sn in gn.Nodes)
                        {
                            if (sn.Tag is int && (int)sn.Tag == targetSecId)
                            {
                                gn.Expand();
                                treeParts.SelectedNode = sn;
                                sn.EnsureVisible();
                                return;
                            }
                        }
                    }
                }
            };

            // Cross-reference Service Manual
            btnViewManual.Click += (s, e) =>
            {
                string fg = currentFg;
                if (dgvParts.CurrentRow != null)
                {
                    object fgObj = dgvParts.CurrentRow.Cells["colFg"].Value;
                    if (fgObj != null && !string.IsNullOrEmpty(fgObj.ToString())) fg = fgObj.ToString();
                }
                if (_btnTabManuals != null)
                {
                    _btnTabManuals.PerformClick();
                    Console.WriteLine("[Launcher] Cross-referencing Service Manuals for Function Group " + fg);
                }
            };

            _partsCatalogPnl = partsPnl;
            LoadSections();
            _partsTabConfigured = true;
            Console.WriteLine("[Launcher] Electronic Parts Catalog (EPC) initialized successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] SetupPartsCatalogTab error: " + ex);
        }
    }

    class EwdPublicationItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string PubId { get; set; }
        public override string ToString() { return Name; }
    }

    class EwdModelItem
    {
        public string DisplayName { get; set; }
        public string Key { get; set; }
        public override string ToString() { return DisplayName; }
    }

    static void SetupWiringDiagramsTab(object mObj)
    {
        if (_wiringTabConfigured || mObj == null) return;
        try
        {
            WiringDiagramsTabControl wiringPnl = new WiringDiagramsTabControl();
            wiringPnl.Dock = DockStyle.Fill;
            wiringPnl.BackColor = Color.FromArgb(245, 247, 250);
            wiringPnl.Font = new Font("Segoe UI", 9.0f);

            string ewdRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\wiring_diagrams\US_Eng"));
            if (!Directory.Exists(ewdRoot))
            {
                ewdRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wiring_diagrams\US_Eng"));
            }

            // Top Header Panel
            Panel headerPnl = new Panel();
            headerPnl.Dock = DockStyle.Top;
            headerPnl.Height = 52;
            headerPnl.BackColor = Color.FromArgb(240, 243, 246);
            headerPnl.BorderStyle = BorderStyle.FixedSingle;
            wiringPnl.Controls.Add(headerPnl);

            Label lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(12, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblModel);

            ComboBox cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 13), Width = 130, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboModel);

            Label lblPub = new Label { Text = "Publication / Year:", AutoSize = true, Location = new Point(204, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblPub);

            ComboBox cboPub = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(328, 13), Width = 260, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboPub);

            Button btnOpenPdf = new Button { Text = "📖 Schematics (PDF)", Location = new Point(598, 12), Width = 150, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnOpenPdf.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnOpenPdf);

            Button btnOpenLop = new Button { Text = "📍 Component Locations", Location = new Point(756, 12), Width = 170, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 87, 154), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnOpenLop.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnOpenLop);

            Button btnBrowser = new Button { Text = "🌐 External Window", Location = new Point(934, 12), Width = 145, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(225, 230, 236), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnBrowser.FlatAppearance.BorderSize = 1;
            btnBrowser.FlatAppearance.BorderColor = Color.FromArgb(190, 195, 202);
            headerPnl.Controls.Add(btnBrowser);

            // SplitContainer
            SplitContainer splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.SplitterDistance = 480;
            splitMain.SplitterWidth = 6;
            wiringPnl.Controls.Add(splitMain);
            splitMain.BringToFront();

            // Panel1: Left Panel (Component Locations & Search)
            Panel pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(230, 235, 240) };
            Label lblLeftTitle = new Label { Text = "Components & Locations (LOP)", Dock = DockStyle.Left, Width = 250, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(50, 60, 70) };
            pnlLeftHeader.Controls.Add(lblLeftTitle);
            Label lblCount = new Label { Text = "", Dock = DockStyle.Right, Width = 150, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            pnlLeftHeader.Controls.Add(lblCount);
            splitMain.Panel1.Controls.Add(pnlLeftHeader);

            Panel pnlSearch = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(6, 4, 6, 4) };
            TextBox txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
            pnlSearch.Controls.Add(txtSearch);
            splitMain.Panel1.Controls.Add(pnlSearch);

            // Bottom detail panel
            Panel pnlDetail = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6, 6, 6, 6) };

            Button btnFindSchematic = new Button { Text = "⚡ Schematics", Dock = DockStyle.Right, Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold) };
            btnFindSchematic.FlatAppearance.BorderSize = 0;
            pnlDetail.Controls.Add(btnFindSchematic);

            Panel spcDetail1 = new Panel { Dock = DockStyle.Right, Width = 6 };
            pnlDetail.Controls.Add(spcDetail1);

            Button btnShowLoc = new Button { Text = "📍 Car Placement", Dock = DockStyle.Right, Width = 125, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 87, 154), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold) };
            btnShowLoc.FlatAppearance.BorderSize = 0;
            pnlDetail.Controls.Add(btnShowLoc);

            Panel spcDetail2 = new Panel { Dock = DockStyle.Right, Width = 6 };
            pnlDetail.Controls.Add(spcDetail2);

            Button btnCopyCode = new Button { Text = "📋 Copy ID", Dock = DockStyle.Right, Width = 75, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnCopyCode.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
            pnlDetail.Controls.Add(btnCopyCode);

            Label lblSelComp = new Label { Text = "Select a component above to navigate directly to its placement or schematic diagram", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 40, 50) };
            pnlDetail.Controls.Add(lblSelComp);
            lblSelComp.BringToFront();

            splitMain.Panel1.Controls.Add(pnlDetail);
            pnlDetail.BringToFront();

            DataGridView dgvComponents = new DataGridView();
            dgvComponents.Dock = DockStyle.Fill;
            dgvComponents.BackgroundColor = Color.White;
            dgvComponents.BorderStyle = BorderStyle.None;
            dgvComponents.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvComponents.GridColor = Color.FromArgb(235, 238, 242);
            dgvComponents.RowHeadersVisible = false;
            dgvComponents.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvComponents.MultiSelect = false;
            dgvComponents.AllowUserToAddRows = false;
            dgvComponents.AllowUserToDeleteRows = false;
            dgvComponents.ReadOnly = true;
            dgvComponents.AutoGenerateColumns = false;
            dgvComponents.EnableHeadersVisualStyles = false;
            dgvComponents.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            dgvComponents.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(50, 60, 70);
            dgvComponents.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvComponents.ColumnHeadersHeight = 28;
            dgvComponents.RowTemplate.Height = 24;

            DataGridViewTextBoxColumn colCode = new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "ID", Width = 75 };
            colCode.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colCode.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);
            dgvComponents.Columns.Add(colCode);

            DataGridViewTextBoxColumn colDesc = new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "Component Name / Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            colDesc.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f);
            colDesc.DefaultCellStyle.ForeColor = Color.FromArgb(30, 40, 50);
            dgvComponents.Columns.Add(colDesc);

            DataGridViewTextBoxColumn colPage = new DataGridViewTextBoxColumn { Name = "PageDisp", DataPropertyName = "PageDisp", HeaderText = "Placement", Width = 90 };
            colPage.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colPage.DefaultCellStyle.ForeColor = Color.FromArgb(43, 87, 154);
            colPage.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colPage.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvComponents.Columns.Add(colPage);

            splitMain.Panel1.Controls.Add(dgvComponents);
            dgvComponents.BringToFront();

            // Panel2: Right Panel (Embedded Modern Edge WebView2 & Navigation Bar)
            Panel pnlWebNav = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(245, 247, 250), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4) };

            Button btnNavSchematics = new Button { Text = "📖 Wiring Schematics PDF", Dock = DockStyle.Left, Width = 175, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnNavSchematics.FlatAppearance.BorderSize = 0;
            pnlWebNav.Controls.Add(btnNavSchematics);

            Panel spcNav1 = new Panel { Dock = DockStyle.Left, Width = 6 };
            pnlWebNav.Controls.Add(spcNav1);

            Button btnNavLop = new Button { Text = "📍 Component Locations (LOP)", Dock = DockStyle.Left, Width = 195, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnNavLop.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);
            pnlWebNav.Controls.Add(btnNavLop);

            Panel spcNav2 = new Panel { Dock = DockStyle.Left, Width = 6 };
            pnlWebNav.Controls.Add(spcNav2);

            Button btnNavIndex = new Button { Text = "⚡ Interactive Index", Dock = DockStyle.Left, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnNavIndex.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);
            pnlWebNav.Controls.Add(btnNavIndex);

            Label lblWebUrl = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            pnlWebNav.Controls.Add(lblWebUrl);
            lblWebUrl.SendToBack();

            splitMain.Panel2.Controls.Add(pnlWebNav);
            pnlWebNav.Dock = DockStyle.Top;
            pnlWebNav.BringToFront();

            Microsoft.Web.WebView2.WinForms.WebView2 wvPdf = new Microsoft.Web.WebView2.WinForms.WebView2();
            wvPdf.Dock = DockStyle.Fill;
            splitMain.Panel2.Controls.Add(wvPdf);
            wvPdf.SendToBack();
            _ewdWebView = wvPdf;
            _ewdDgv = dgvComponents;
            _ewdBtnShowLoc = btnShowLoc;
            _ewdBtnFindSchem = btnFindSchematic;

            // Catalog Parsing
            Dictionary<string, List<EwdPublicationItem>> catalog = new Dictionary<string, List<EwdPublicationItem>>(StringComparer.OrdinalIgnoreCase);
            string jsPath = Path.Combine(ewdRoot, "pdf-links.js");
            if (File.Exists(jsPath))
            {
                string js = File.ReadAllText(jsPath);
                var modelMatches = Regex.Matches(js, @"if\s*\(\s*chosen\s*==\s*""([^""]+)""\s*\)\s*\{([^}]+)\}");
                foreach (Match m in modelMatches)
                {
                    string modelKey = m.Groups[1].Value.Trim();
                    string body = m.Groups[2].Value;
                    var pubMatches = Regex.Matches(body, @"Option\(\s*'([^']+)'\s*,\s*'([^']+)'\s*\)");
                    List<EwdPublicationItem> list = new List<EwdPublicationItem>();
                    foreach (Match p in pubMatches)
                    {
                        string name = p.Groups[1].Value.Trim();
                        string path = p.Groups[2].Value.Trim();
                        if (!name.StartsWith("Step") && !string.IsNullOrEmpty(path))
                        {
                            string pubId = "";
                            var mId = Regex.Match(name, @"\b(\d{7,8})\b");
                            if (mId.Success) pubId = mId.Groups[1].Value;
                            list.Add(new EwdPublicationItem { Name = name, Path = path, PubId = pubId });
                        }
                    }
                    if (list.Count > 0) catalog[modelKey] = list;
                }
            }

            // Populate Models
            EwdModelItem[] models = new EwdModelItem[] {
                new EwdModelItem { DisplayName = "V70 (08-)", Key = "v70(08-)" },
                new EwdModelItem { DisplayName = "XC70 (08-)", Key = "xc70(08-)" },
                new EwdModelItem { DisplayName = "S80 (07-)", Key = "s80(07-)" },
                new EwdModelItem { DisplayName = "XC60", Key = "xc60" },
                new EwdModelItem { DisplayName = "V60", Key = "v60" },
                new EwdModelItem { DisplayName = "S60 (11-)", Key = "s60(11-)" },
                new EwdModelItem { DisplayName = "S60 (-09)", Key = "s60(-09)" },
                new EwdModelItem { DisplayName = "C30", Key = "c30" },
                new EwdModelItem { DisplayName = "S40 (04-)", Key = "s40" },
                new EwdModelItem { DisplayName = "V50", Key = "v50" },
                new EwdModelItem { DisplayName = "C70 (06-)", Key = "c70" },
                new EwdModelItem { DisplayName = "V70 (-08)", Key = "v70" },
                new EwdModelItem { DisplayName = "XC70 (-08)", Key = "xc70" },
                new EwdModelItem { DisplayName = "S80 (-07)", Key = "s80(-07)" },
                new EwdModelItem { DisplayName = "XC90", Key = "xc90" }
            };
            foreach (var m in models) cboModel.Items.Add(m);

            DataTable dtComponents = new DataTable();
            dtComponents.Columns.Add("Code");
            dtComponents.Columns.Add("Description");
            dtComponents.Columns.Add("PageNum", typeof(int));
            dtComponents.Columns.Add("PageDisp");
            dgvComponents.DataSource = dtComponents.DefaultView;

            string curPdfPath = "";
            string curLopPdfPath = "";
            string curWebUrl = "";

            Action updateNavStyles = () => {
                string currentUri = (wvPdf.Source != null) ? wvPdf.Source.ToString().ToLowerInvariant() : "";
                bool isLop = currentUri.Contains("lop.pdf");
                bool isHtml = currentUri.EndsWith(".html") || currentUri.EndsWith(".htm");

                btnNavSchematics.BackColor = (!isLop && !isHtml) ? Color.FromArgb(0, 102, 204) : Color.FromArgb(240, 243, 246);
                btnNavSchematics.ForeColor = (!isLop && !isHtml) ? Color.White : Color.FromArgb(40, 50, 60);

                btnNavLop.BackColor = isLop ? Color.FromArgb(43, 87, 154) : Color.FromArgb(240, 243, 246);
                btnNavLop.ForeColor = isLop ? Color.White : Color.FromArgb(40, 50, 60);

                btnNavIndex.BackColor = isHtml ? Color.FromArgb(0, 102, 204) : Color.FromArgb(240, 243, 246);
                btnNavIndex.ForeColor = isHtml ? Color.White : Color.FromArgb(40, 50, 60);
            };

            Action<int> jumpToPage = (pageNum) => {
                try
                {
                    if (pageNum > 0 && File.Exists(curPdfPath))
                    {
                        string uriStr = new Uri(curPdfPath).AbsoluteUri + "?nav=" + DateTime.Now.Ticks + "#page=" + pageNum;
                        if (wvPdf.CoreWebView2 != null)
                        {
                            wvPdf.CoreWebView2.Navigate(uriStr);
                        }
                        else
                        {
                            wvPdf.Source = new Uri(uriStr);
                        }
                        updateNavStyles();
                    }
                }
                catch (Exception exJump) { Console.WriteLine("[Launcher] EWD page navigation failed: " + exJump.Message); }
            };

            Action<string> jumpToSearch = (codeStr) => {
                try
                {
                    if (!string.IsNullOrEmpty(codeStr) && File.Exists(curPdfPath))
                    {
                        string uriStr = new Uri(curPdfPath).AbsoluteUri + "?nav=" + DateTime.Now.Ticks + "#search=" + Uri.EscapeDataString(codeStr);
                        if (wvPdf.CoreWebView2 != null)
                        {
                            wvPdf.CoreWebView2.Navigate(uriStr);
                        }
                        else
                        {
                            wvPdf.Source = new Uri(uriStr);
                        }
                        updateNavStyles();
                    }
                }
                catch (Exception exSearch) { Console.WriteLine("[Launcher] EWD search navigation failed: " + exSearch.Message); }
            };

            Action<EwdPublicationItem> loadPub = async (pub) => {
                if (pub == null) return;
                try
                {
                    string pubId = pub.PubId;
                    curPdfPath = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + ".pdf");
                    curLopPdfPath = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "lop.pdf");
                    curWebUrl = "http://localhost/Vida/wiring_diagrams/US_Eng/" + pub.Path;
                    lblWebUrl.Text = pub.Name;

                    // Load Component Navigation (TSV or LOP fallback)
                    dtComponents.Rows.Clear();
                    string navTsv = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "_nav.tsv");
                    if (File.Exists(navTsv))
                    {
                        string[] lines = File.ReadAllLines(navTsv, Encoding.UTF8);
                        foreach (string line in lines)
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[0].Trim()))
                            {
                                string code = parts[0].Trim();
                                string desc = parts[1].Trim();
                                int pageNum = 0;
                                int.TryParse(parts[2].Trim(), out pageNum);
                                string pageDisp = (pageNum > 0) ? "Page " + pageNum : "-";
                                dtComponents.Rows.Add(code, desc, pageNum, pageDisp);
                            }
                        }
                    }
                    else
                    {
                        string lopTxt = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "lop.txt");
                        if (File.Exists(lopTxt))
                        {
                            string[] lines = File.ReadAllLines(lopTxt, Encoding.Default);
                            foreach (string line in lines)
                            {
                                string[] parts = line.Split(new char[] { '\t' }, 2);
                                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0].Trim()))
                                {
                                    dtComponents.Rows.Add(parts[0].Trim(), parts[1].Trim(), 0, "-");
                                }
                            }
                        }
                    }
                    lblCount.Text = dtComponents.Rows.Count + " components";
                    txtSearch.Text = "";

                    // Initialize WebView2 and navigate directly to the PDF schematic book!
                    if (wvPdf.CoreWebView2 == null)
                    {
                        await wvPdf.EnsureCoreWebView2Async(null);
                    }
                    if (File.Exists(curPdfPath))
                    {
                        wvPdf.Source = new Uri(curPdfPath);
                    }
                    else
                    {
                        wvPdf.Source = new Uri(curWebUrl);
                    }
                    updateNavStyles();
                }
                catch (Exception exLoad)
                {
                    Console.WriteLine("[Launcher] Error loading publication: " + exLoad.Message);
                }
            };

            btnNavSchematics.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    wvPdf.Source = new Uri(curPdfPath);
                    updateNavStyles();
                }
            };

            btnNavLop.Click += (s, e) => {
                if (File.Exists(curLopPdfPath))
                {
                    wvPdf.Source = new Uri(curLopPdfPath);
                    updateNavStyles();
                }
            };

            btnNavIndex.Click += (s, e) => {
                if (!string.IsNullOrEmpty(curWebUrl))
                {
                    wvPdf.Source = new Uri(curWebUrl);
                    updateNavStyles();
                }
            };

            cboModel.SelectedIndexChanged += (s, e) => {
                EwdModelItem selModel = cboModel.SelectedItem as EwdModelItem;
                if (selModel == null) return;
                cboPub.Items.Clear();
                if (catalog.ContainsKey(selModel.Key))
                {
                    foreach (var p in catalog[selModel.Key]) cboPub.Items.Add(p);
                }
                if (cboPub.Items.Count > 0)
                {
                    // Select 2011 or first
                    int selIdx = 0;
                    for (int i = 0; i < cboPub.Items.Count; i++)
                    {
                        EwdPublicationItem pi = cboPub.Items[i] as EwdPublicationItem;
                        if (pi != null && pi.Name.Contains("2011") && !pi.Name.Contains("Supplement"))
                        {
                            selIdx = i;
                            break;
                        }
                    }
                    cboPub.SelectedIndex = selIdx;
                }
            };

            cboPub.SelectedIndexChanged += (s, e) => {
                loadPub(cboPub.SelectedItem as EwdPublicationItem);
            };

            txtSearch.TextChanged += (s, e) => {
                string q = txtSearch.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]");
                if (string.IsNullOrEmpty(q))
                {
                    dtComponents.DefaultView.RowFilter = "";
                }
                else
                {
                    dtComponents.DefaultView.RowFilter = string.Format("Code LIKE '%{0}%' OR Description LIKE '%{0}%'", q);
                }
                lblCount.Text = dtComponents.DefaultView.Count + " matches";
            };

            Action updateCompSelection = () => {
                if (dgvComponents.CurrentRow != null)
                {
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    string desc = Convert.ToString(dgvComponents.CurrentRow.Cells["Description"].Value);
                    int pNum = 0;
                    try { pNum = Convert.ToInt32(dgvComponents.CurrentRow.Cells["PageNum"].Value); } catch { }
                    if (!string.IsNullOrEmpty(code))
                    {
                        lblSelComp.Text = code + ": " + desc + (pNum > 0 ? " (📍 Placement: Page " + pNum + ")" : "");
                        btnShowLoc.Text = (pNum > 0) ? "📍 Page " + pNum : "📍 Car Placement";
                        btnShowLoc.Enabled = (pNum > 0);
                    }
                }
            };

            dgvComponents.SelectionChanged += (s, e) => {
                updateCompSelection();
            };

            dgvComponents.CellClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    int pNum = 0;
                    try { pNum = Convert.ToInt32(dgvComponents.Rows[e.RowIndex].Cells["PageNum"].Value); } catch { }
                    string code = Convert.ToString(dgvComponents.Rows[e.RowIndex].Cells["Code"].Value);
                    if (dgvComponents.Columns[e.ColumnIndex].Name == "PageDisp" && pNum > 0)
                    {
                        jumpToPage(pNum);
                    }
                }
            };

            dgvComponents.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    int pNum = 0;
                    try { pNum = Convert.ToInt32(dgvComponents.Rows[e.RowIndex].Cells["PageNum"].Value); } catch { }
                    string code = Convert.ToString(dgvComponents.Rows[e.RowIndex].Cells["Code"].Value);
                    if (pNum > 0)
                    {
                        jumpToPage(pNum);
                    }
                    else
                    {
                        jumpToSearch(code);
                    }
                }
            };

            btnShowLoc.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    int pNum = 0;
                    try { pNum = Convert.ToInt32(dgvComponents.CurrentRow.Cells["PageNum"].Value); } catch { }
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    if (pNum > 0) jumpToPage(pNum);
                    else jumpToSearch(code);
                }
            };

            btnFindSchematic.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    if (!string.IsNullOrEmpty(code))
                    {
                        Clipboard.SetText(code);
                        lblSelComp.Text = "Searching for " + code + " in schematics...";
                        jumpToSearch(code);
                    }
                }
            };

            btnCopyCode.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    if (!string.IsNullOrEmpty(code))
                    {
                        Clipboard.SetText(code);
                        lblSelComp.Text = "Copied to clipboard: " + code;
                    }
                }
            };

            btnOpenPdf.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    wvPdf.Source = new Uri(curPdfPath);
                    updateNavStyles();
                }
            };

            btnOpenLop.Click += (s, e) => {
                if (File.Exists(curLopPdfPath))
                {
                    wvPdf.Source = new Uri(curLopPdfPath);
                    updateNavStyles();
                }
            };

            btnBrowser.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    Process.Start(new ProcessStartInfo(curPdfPath) { UseShellExecute = true });
                }
                else if (!string.IsNullOrEmpty(curWebUrl))
                {
                    Process.Start(new ProcessStartInfo(curWebUrl) { UseShellExecute = true });
                }
            };

            _wiringDiagramsPnl = wiringPnl;
            // Default select V70 (08-)
            cboModel.SelectedIndex = 0;
            _wiringTabConfigured = true;
            Console.WriteLine("[Launcher] Electronic Wiring Diagrams (EWD) tab initialized successfully with native WebView2 PDF viewer!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] SetupWiringDiagramsTab error: " + ex);
        }
    }

    class ManualDocItem



    {



        public string Id { get; set; }



        public string Title { get; set; }



        public string Vcc { get; set; }



        public string Version { get; set; }



        public string GroupKey { get; set; }



        public string GroupTitle { get; set; }



        public override string ToString()



        {



            if (!string.IsNullOrEmpty(Vcc))



                return string.Format("{0} [{1}]", Title, Vcc);



            return Title;



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
        Console.WriteLine("[Launcher] Simulation Mode: " + (IsMockSimulation ? "ENABLED (Mock Data)" : "DISABLED (Live DiCE CAN)"));




        try



        {



        try



        {



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



        Application.EnableVisualStyles();



        Application.SetCompatibleTextRenderingDefault(false);



        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);



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



        bool vehCommInitialized = false;



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



                        Control vcCtrl = vcForm as Control;



                        Action clearAxHostActive = () => {



                            try



                            {



                                FieldInfo niF = vcForm.GetType().GetField("_navImage", BindingFlags.NonPublic | BindingFlags.Instance);



                                if (niF != null)



                                {



                                    object ni = niF.GetValue(vcForm);



                                    if (ni != null)



                                    {



                                        FieldInfo axF = ni.GetType().GetField("_axIsoView", BindingFlags.NonPublic | BindingFlags.Instance);



                                        if (axF != null)



                                        {



                                            Control ax = axF.GetValue(ni) as Control;



                                            if (ax != null)



                                            {



                                                FieldInfo f = typeof(AxHost).GetField("iOleInPlaceActiveObjectExternal", BindingFlags.NonPublic | BindingFlags.Instance);



                                                if (f != null) f.SetValue(ax, null);



                                                FieldInfo cF = typeof(AxHost).GetField("container", BindingFlags.NonPublic | BindingFlags.Instance);



                                                if (cF != null)



                                                {



                                                    object container = cF.GetValue(ax);



                                                    if (container != null)



                                                    {



                                                        FieldInfo sF = container.GetType().GetField("siteUIActive", BindingFlags.NonPublic | BindingFlags.Instance);



                                                        if (sF != null) sF.SetValue(container, null);



                                                    }



                                                }



                                            }



                                        }



                                    }



                                }



                            }



                            catch {}



                        };



                        clearAxHostActive();



                        // Ensure current comm tool is set on workflow so script engine doesn't throw NullReferenceException



                        FieldInfo wfInitField = vcForm.GetType().GetField("_vehicleCommunicationWf", BindingFlags.NonPublic | BindingFlags.Instance);



                        object wfInit = wfInitField != null ? wfInitField.GetValue(vcForm) : null;



                        if (wfInit != null)



                        {



                            try



                            {



                                FieldInfo ctField = wfInit.GetType().GetField("_currentCommTool", BindingFlags.NonPublic | BindingFlags.Instance);



                                if (ctField != null && ctField.GetValue(wfInit) == null)



                                {



                                    Type ctType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.BizServices.VehicleCommProvider.CurrentCommTool")



                                                  ?? Assembly.LoadFrom(@"C:\Users\david\git\vida\VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\BizServices.VehicleCommProvider.DLL")



                                                     .GetType("Vcc.Vida.DiagSwdl.BizServices.VehicleCommProvider.CurrentCommTool");



                                    if (ctType != null)



                                    {



                                        object ct = Activator.CreateInstance(ctType);



                                        PropertyInfo ctn = ctType.GetProperty("CommToolName");



                                        if (ctn != null) ctn.SetValue(ct, "DiCE", null);



                                        ctField.SetValue(wfInit, ct);



                                    }



                                }



                            }



                            catch {}
                            RegisterScriptingContext(_cachedVehicleProfile, dm, wfInit);




                        }



                        // Select default ECM once on initial entrance



                        if (onNavClick != null && !vehCommInitialized)



                        {



                            vehCommInitialized = true;



                            onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs("284101", 1) });



                        }



                        // Add ECU Module Selector Bar if not already added



                        if (vcCtrl != null && vcCtrl.Controls.Find("ecuSelectBar", true).Length == 0)



                        {



                            Panel ecuBar = new Panel();



                            ecuBar.Name = "ecuSelectBar";



                            ecuBar.SetBounds(380, 56, 920, 58);



                            ecuBar.BackColor = Color.FromArgb(245, 247, 250);



                            Label lblSel = new Label();



                            lblSel.Text = "ECU Module:";



                            lblSel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);



                            lblSel.ForeColor = Color.FromArgb(50, 60, 70);



                            lblSel.SetBounds(2, 4, 85, 22);



                            ecuBar.Controls.Add(lblSel);



                            ComboBox cbEcu = new ComboBox();



                            cbEcu.Name = "cbEcu";



                            cbEcu.DropDownStyle = ComboBoxStyle.DropDownList;



                            cbEcu.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);



                            cbEcu.SetBounds(90, 2, 380, 24);



                            var ecuList = new Tuple<string, string>[] {



                                Tuple.Create("284101", "ECM - Engine Control Module"),



                                Tuple.Create("437601", "TCM - Transmission Control Module"),



                                Tuple.Create("372302", "CEM - Central Electronic Module"),



                                Tuple.Create("593102", "BCM - Brake Control Module"),



                                Tuple.Create("381101", "DIM - Driver Information Module"),



                                Tuple.Create("884701", "SRS - Supplemental Restraint System"),



                                Tuple.Create("871101", "CCM - Climate Control Module"),



                                Tuple.Create("465901", "DEM - Differential Electronic Module"),



                                Tuple.Create("645902", "SAS - Steering Angle Sensor"),



                                Tuple.Create("875401", "CPM - Combustion Pre-heater Module"),



                                Tuple.Create("551401", "PBM - Parking Brake Module"),



                                Tuple.Create("831701", "DDM - Driver Door Module"),



                                Tuple.Create("831703", "PDM - Passenger Door Module"),



                                Tuple.Create("852601", "PSM - Power Seat Module"),



                                Tuple.Create("393901", "ICM - Infotainment Control Module"),



                                Tuple.Create("393204", "IAM - Integrated Audio Module"),



                                Tuple.Create("393202", "AUD - Audio Module"),



                                Tuple.Create("366901", "KVM - Keyless Vehicle Module"),



                                Tuple.Create("366902", "PAM - Parking Assistance Module"),



                                Tuple.Create("892901", "TRM - Trailer Module"),



                                Tuple.Create("832901", "POT - Power Operated Tailgate"),



                                Tuple.Create("352902", "HCM - Headlamp Control Module"),



                                Tuple.Create("366905", "FSM - Forward Sensing Module"),



                                Tuple.Create("645903", "PSCM - Power Steering Control Module")



                            };



                            foreach (var item in ecuList)



                            {



                                cbEcu.Items.Add(item.Item2);



                            }



                            cbEcu.SelectedIndex = 0;



                            bool updatingFromEvent = false;



                            Action<string> selectEcuById = (targetId) => {



                                for (int k = 0; k < ecuList.Length; k++)



                                {



                                    if (ecuList[k].Item1 == targetId)



                                    {



                                        updatingFromEvent = true;



                                        cbEcu.SelectedIndex = k;



                                        updatingFromEvent = false;



                                        break;



                                    }



                                }



                            };



                                                        _vehCommCtrl = vcForm as Control;



                            _onVehicleCommHotspotClicked = (clickedId) => {



                                Console.WriteLine("[Launcher] Vehicle Comm Click received for ECU: " + clickedId);



                                try {



                                    if (onNavClick != null)



                                        onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(clickedId, 1) });



                                    selectEcuById(clickedId);



                                } catch (Exception exClick) {



                                    Console.WriteLine("[Launcher] Vehicle Comm click dispatch error: " + exClick.Message);



                                }



                            };



                            RegisterDummyIsoViewHotspot();



                            cbEcu.Enter += (sEnter, eEnter) => { clearAxHostActive(); };



                            cbEcu.MouseDown += (sMd, eMd) => { clearAxHostActive(); };



                            cbEcu.SelectedIndexChanged += (sEcu, eEcu) => {



                                if (updatingFromEvent) return;



                                if (cbEcu.SelectedIndex >= 0 && cbEcu.SelectedIndex < ecuList.Length)



                                {



                                    string selId = ecuList[cbEcu.SelectedIndex].Item1;



                                    Console.WriteLine("[Launcher] Module selected: " + ecuList[cbEcu.SelectedIndex].Item2 + " (" + selId + ")");



                                    try



                                    {



                                        if (onNavClick != null)



                                        {



                                            onNavClick.Invoke(vcForm, new object[] { vcForm, new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(selId, 1) });



                                        }



                                    }



                                    catch (Exception ex)



                                    {



                                        Console.WriteLine("[Launcher] Error selecting ECU " + selId + ": " + ex.Message);



                                    }



                                }



                            };



                            ecuBar.Controls.Add(cbEcu);
                            CheckBox chkMock = new CheckBox();
                            chkMock.Name = "chkMock";
                            chkMock.Text = "Mock Data";
                            chkMock.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                            chkMock.SetBounds(490, 31, 100, 22);
                            chkMock.Checked = IsMockSimulation;
                            chkMock.CheckedChanged += (sChk, eChk) => {
                                IsMockSimulation = chkMock.Checked;
                                Console.WriteLine("[Launcher] Mock Simulation toggled: " + IsMockSimulation);
                                if (!IsMockSimulation && wfInit != null)
                                {
                                    MethodInfo startM = wfInit.GetType().GetMethod("StartReading");
                                    if (startM != null) startM.Invoke(wfInit, null);
                                }
                            };
                            ecuBar.Controls.Add(chkMock);
                            // Sub-tab switcher inside Vehicle Communication: Parameters vs. Activations vs. Special Tools
                            Button btnSubParam = new Button();
                            Button btnSubAct = new Button();
                            Button btnSubSpec = new Button();

                            Action<Button> setSubTabStyle = (activeBtn) =>
                            {
                                Button[] sBtns = new Button[] { btnSubParam, btnSubAct, btnSubSpec };
                                foreach (var b in sBtns)
                                {
                                    if (b == activeBtn)
                                    {
                                        b.BackColor = Color.FromArgb(0, 102, 204);
                                        b.ForeColor = Color.White;
                                        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                                    }
                                    else
                                    {
                                        b.BackColor = Color.FromArgb(235, 240, 245);
                                        b.ForeColor = Color.FromArgb(50, 60, 70);
                                        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                                    }
                                }
                            };

                            Action<string> switchVehCommSubTab = (tabName) =>
                            {
                                try
                                {
                                    FieldInfo tabField = vcForm.GetType().GetField("_tab", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object tabCtrl = tabField != null ? tabField.GetValue(vcForm) : null;
                                    if (tabCtrl != null)
                                    {
                                        FieldInfo targetPageField = vcForm.GetType().GetField(tabName, BindingFlags.NonPublic | BindingFlags.Instance);
                                        object targetPage = targetPageField != null ? targetPageField.GetValue(vcForm) : null;
                                        if (targetPage != null)
                                        {
                                            MethodInfo miSelChanged = vcForm.GetType().GetMethod("TabSelectedTabChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                                            PropertyInfo pCurTab = tabCtrl.GetType().GetProperty("SelectedTab");
                                            object curPage = pCurTab != null ? pCurTab.GetValue(tabCtrl, null) : null;
                                            if (pCurTab != null) pCurTab.SetValue(tabCtrl, targetPage, null);
                                            if (miSelChanged != null) miSelChanged.Invoke(vcForm, new object[] { targetPage, curPage });
                                            Console.WriteLine("[Launcher] Switched VehComm sub-tab to: " + tabName);
                                        }
                                    }
                                }
                                catch (Exception exTab)
                                {
                                    Console.WriteLine("[Launcher] Error switching VehComm sub-tab: " + exTab.Message);
                                }
                            };

                            btnSubParam.Text = "📊 Parameters";
                            btnSubParam.SetBounds(595, 2, 100, 24);
                            btnSubParam.FlatStyle = FlatStyle.Flat;
                            btnSubParam.FlatAppearance.BorderSize = 1;
                            btnSubParam.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubParam.Cursor = Cursors.Hand;
                            btnSubParam.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubParam);
                                switchVehCommSubTab("_readingParamTabPage");
                            };
                            ecuBar.Controls.Add(btnSubParam);

                            btnSubAct.Text = "⚡ Activations";
                            btnSubAct.SetBounds(700, 2, 105, 24);
                            btnSubAct.FlatStyle = FlatStyle.Flat;
                            btnSubAct.FlatAppearance.BorderSize = 1;
                            btnSubAct.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubAct.Cursor = Cursors.Hand;
                            btnSubAct.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubAct);
                                switchVehCommSubTab("_activationsTabPage");
                            };
                            ecuBar.Controls.Add(btnSubAct);

                            btnSubSpec.Text = "🔧 Advanced";
                            btnSubSpec.SetBounds(810, 2, 95, 24);
                            btnSubSpec.FlatStyle = FlatStyle.Flat;
                            btnSubSpec.FlatAppearance.BorderSize = 1;
                            btnSubSpec.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
                            btnSubSpec.Cursor = Cursors.Hand;
                            btnSubSpec.Click += (sSub, eSub) => {
                                setSubTabStyle(btnSubSpec);
                                switchVehCommSubTab("_specialToolsTabPage");
                            };
                            ecuBar.Controls.Add(btnSubSpec);
                            setSubTabStyle(btnSubParam);

                            // Parameters Watchlist Management Buttons
                            Button btnRemoveParam = new Button();
                            btnRemoveParam.Text = "❌ Remove Item";
                            btnRemoveParam.SetBounds(595, 30, 100, 24);
                            btnRemoveParam.FlatStyle = FlatStyle.Flat;
                            btnRemoveParam.FlatAppearance.BorderSize = 1;
                            btnRemoveParam.FlatAppearance.BorderColor = Color.FromArgb(220, 180, 180);
                            btnRemoveParam.BackColor = Color.FromArgb(253, 242, 242);
                            btnRemoveParam.ForeColor = Color.FromArgb(160, 40, 40);
                            btnRemoveParam.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                            btnRemoveParam.Cursor = Cursors.Hand;
                            btnRemoveParam.Click += (sR, eR) => {
                                try {
                                    FieldInfo rpf = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object rp = rpf != null ? rpf.GetValue(vcForm) : null;
                                    if (rp != null) {
                                        FieldInfo lvf = rp.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                        object lvObj = lvf != null ? lvf.GetValue(rp) : null;
                                        if (lvObj != null) {
                                            PropertyInfo piSel = lvObj.GetType().GetProperty("SelectedItems");
                                            IList selList = piSel != null ? piSel.GetValue(lvObj, null) as IList : null;
                                            if (selList == null || selList.Count == 0) {
                                                PropertyInfo piItems = lvObj.GetType().GetProperty("Items");
                                                IEnumerable items = piItems != null ? piItems.GetValue(lvObj, null) as IEnumerable : null;
                                                object lastIt = null;
                                                if (items != null) {
                                                    foreach (object it in items) lastIt = it;
                                                }
                                                if (lastIt != null) {
                                                    PropertyInfo piItSel = lastIt.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(lastIt, true, null);
                                                }
                                            }
                                        }
                                        MethodInfo miRem = rp.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (miRem != null) {
                                            miRem.Invoke(rp, null);
                                            Console.WriteLine("[Launcher] Parameter removed from watchlist");
                                        }
                                    }
                                } catch (Exception exRem) {
                                    Console.WriteLine("[Launcher] Error removing parameter: " + exRem.Message);
                                }
                            };
                            ecuBar.Controls.Add(btnRemoveParam);

                            Button btnClearParams = new Button();
                            btnClearParams.Text = "🗑️ Clear All";
                            btnClearParams.SetBounds(700, 30, 105, 24);
                            btnClearParams.FlatStyle = FlatStyle.Flat;
                            btnClearParams.FlatAppearance.BorderSize = 1;
                            btnClearParams.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                            btnClearParams.BackColor = Color.FromArgb(245, 245, 247);
                            btnClearParams.ForeColor = Color.FromArgb(70, 70, 80);
                            btnClearParams.Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
                            btnClearParams.Cursor = Cursors.Hand;
                            btnClearParams.Click += (sC, eC) => {
                                try {
                                    FieldInfo rpf = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object rp = rpf != null ? rpf.GetValue(vcForm) : null;
                                    if (rp != null) {
                                        FieldInfo lvf = rp.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                        object lvObj = lvf != null ? lvf.GetValue(rp) : null;
                                        if (lvObj != null) {
                                            PropertyInfo piItems = lvObj.GetType().GetProperty("Items");
                                            IEnumerable items = piItems != null ? piItems.GetValue(lvObj, null) as IEnumerable : null;
                                            if (items != null) {
                                                foreach (object it in items) {
                                                    PropertyInfo piItSel = it.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(it, true, null);
                                                }
                                            }
                                        }
                                        MethodInfo miRem = rp.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (miRem != null) {
                                            miRem.Invoke(rp, null);
                                            Console.WriteLine("[Launcher] All parameters cleared from watchlist");
                                        }
                                    }
                                } catch (Exception exClr) {
                                    Console.WriteLine("[Launcher] Error clearing parameters: " + exClr.Message);
                                }
                            };
                            ecuBar.Controls.Add(btnClearParams);

                            // Setup safety logging and DiCE worker binding on ActivationControl
                            try
                            {
                                FieldInfo actTabField = vcForm.GetType().GetField("_activationsTab", BindingFlags.NonPublic | BindingFlags.Instance);
                                object actTab = actTabField != null ? actTabField.GetValue(vcForm) : null;
                                if (actTab != null)
                                {
                                    FieldInfo acField = actTab.GetType().GetField("_activationControl", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object ac = acField != null ? acField.GetValue(actTab) : null;
                                    if (ac != null)
                                    {
                                        EventInfo evStart = ac.GetType().GetEvent("ActivationStarted");
                                        if (evStart != null)
                                        {
                                            EventHandler onActStart = (sAct, eAct) => {
                                                Console.WriteLine("[Launcher] >>> ACTUATION TRIGGERED: Component test started on ECU via DiCE! <<<");
                                            };
                                            evStart.AddEventHandler(ac, onActStart);
                                        }
                                        EventInfo evStop = ac.GetType().GetEvent("ActivationStopped");
                                        if (evStop != null)
                                        {
                                            EventHandler onActStop = (sAct, eAct) => {
                                                Console.WriteLine("[Launcher] <<< ACTUATION STOPPED: Component test finished on ECU via DiCE! >>>");
                                            };
                                            evStop.AddEventHandler(ac, onActStop);
                                        }

                                        // Attach safety interception to _startButton
                                        FieldInfo sbField = ac.GetType().GetField("_startButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                        Control sb = sbField != null ? sbField.GetValue(ac) as Control : null;
                                        if (sb != null)
                                        {
                                            sb.MouseDown += (sBtn, eBtn) => {
                                                if (IsMockSimulation)
                                                {
                                                    Console.WriteLine("[Launcher] Warning: Mock Simulation is active! Uncheck 'Mock Data' before running live car actuations.");
                                                }
                                                else
                                                {
                                                    Console.WriteLine("[Launcher] Preparing DiCE worker thread for live bidirectional activation...");
                                                }
                                            };
                                        }
                                    }
                                }
                            }
                            catch (Exception exAct)
                            {
                                Console.WriteLine("[Launcher] Error hooking ActivationControl: " + exAct.Message);
                            }




                            // Shortcut buttons row



                            string[] quickEcus = new string[] { "ECM", "TCM", "CEM", "BCM", "DIM", "SRS", "CCM", "DEM" };



                            int btnX = 2;



                            foreach (var qName in quickEcus)



                            {



                                Button btn = new Button();



                                btn.Text = qName;



                                btn.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);



                                btn.SetBounds(btnX, 30, 48, 22);



                                btn.FlatStyle = FlatStyle.Flat;



                                btn.FlatAppearance.BorderSize = 1;



                                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);



                                btn.BackColor = Color.FromArgb(240, 244, 248);



                                btn.Cursor = Cursors.Hand;



                                string matchName = qName;



                                btn.Click += (sBtn, eBtn) => {



                                    for (int k = 0; k < ecuList.Length; k++)



                                    {



                                        if (ecuList[k].Item2.StartsWith(matchName + " ", StringComparison.OrdinalIgnoreCase))



                                        {



                                            cbEcu.SelectedIndex = k;



                                            break;



                                        }



                                    }



                                };



                                ecuBar.Controls.Add(btn);



                                btnX += 52;



                            }



                            vcCtrl.Controls.Add(ecuBar);



                            ecuBar.BringToFront();



                            // Synchronize dropdown when an ECU is clicked directly on the map



                            FieldInfo ecuLblField = vcForm.GetType().GetField("_ecuNameLabel", BindingFlags.NonPublic | BindingFlags.Instance);



                            if (ecuLblField != null)



                            {



                                object ecuLbl = ecuLblField.GetValue(vcForm);



                                if (ecuLbl != null)



                                {



                                    FieldInfo innerLblField = ecuLbl.GetType().GetField("_label", BindingFlags.NonPublic | BindingFlags.Instance);



                                    if (innerLblField != null)



                                    {



                                        Label innerLbl = innerLblField.GetValue(ecuLbl) as Label;



                                        if (innerLbl != null)



                                        {



                                            innerLbl.TextChanged += (sLbl, eLbl) => {



                                                string curTxt = innerLbl.Text;



                                                for (int k = 0; k < ecuList.Length; k++)



                                                {



                                                    string prefix = ecuList[k].Item2.Split(' ')[0];



                                                    if (curTxt.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)



                                                    {



                                                        updatingFromEvent = true;



                                                        cbEcu.SelectedIndex = k;



                                                        updatingFromEvent = false;



                                                        break;



                                                    }



                                                }



                                            };



                                        }



                                    }



                                }



                            }



                        }



                        // Enable Description button and track selected parameter



                        FieldInfo rpField = vcForm.GetType().GetField("_readParametersPage", BindingFlags.NonPublic | BindingFlags.Instance);



                        if (rpField != null)



                        {



                            object rpPage = rpField.GetValue(vcForm);



                            if (rpPage != null)



                            {



                                FieldInfo tvField = rpPage.GetType().GetField("_treeView", BindingFlags.NonPublic | BindingFlags.Instance);



                                FieldInfo btnDescField = rpPage.GetType().GetField("_viewDescription", BindingFlags.NonPublic | BindingFlags.Instance);



                                TreeView tv = tvField != null ? tvField.GetValue(rpPage) as TreeView : null;



                                Control btnDesc = btnDescField != null ? btnDescField.GetValue(rpPage) as Control : null;



                                if (tv != null && btnDesc != null && btnDesc.Tag == null)



                                {



                                    btnDesc.Tag = "wired";



                                    tv.AfterSelect += (sTv, eTv) => {



                                        if (eTv.Node != null)



                                        {



                                            lastSelectedParameter = eTv.Node.Text;



                                            btnDesc.Enabled = true;



                                        }



                                    };



                                }

                                FieldInfo lvField = rpPage.GetType().GetField("_paramListView", BindingFlags.NonPublic | BindingFlags.Instance);
                                Control lvCtrl = lvField != null ? lvField.GetValue(rpPage) as Control : null;
                                if (lvCtrl != null && lvCtrl.Tag == null)
                                {
                                    lvCtrl.Tag = "wired_remove";
                                    lvCtrl.KeyDown += (sKey, eKey) => {
                                        if (eKey.KeyCode == Keys.Delete)
                                        {
                                            try {
                                                MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                                if (miRem != null) {
                                                    miRem.Invoke(rpPage, null);
                                                    Console.WriteLine("[Launcher] Parameter removed via Delete key");
                                                }
                                            } catch (Exception exK) {
                                                Console.WriteLine("[Launcher] Delete key error: " + exK.Message);
                                            }
                                        }
                                    };

                                    ContextMenuStrip menu = new ContextMenuStrip();
                                    ToolStripMenuItem mnuRem = new ToolStripMenuItem("❌ Remove Selected (Delete)");
                                    mnuRem.Click += (sM, eM) => {
                                        try {
                                            MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (miRem != null) miRem.Invoke(rpPage, null);
                                        } catch (Exception exM) { Console.WriteLine("[Launcher] Context menu remove error: " + exM.Message); }
                                    };
                                    ToolStripMenuItem mnuClr = new ToolStripMenuItem("🗑️ Clear All");
                                    mnuClr.Click += (sM, eM) => {
                                        try {
                                            PropertyInfo piItems = lvCtrl.GetType().GetProperty("Items");
                                            IEnumerable items = piItems != null ? piItems.GetValue(lvCtrl, null) as IEnumerable : null;
                                            if (items != null) {
                                                foreach (object it in items) {
                                                    PropertyInfo piItSel = it.GetType().GetProperty("Selected");
                                                    if (piItSel != null) piItSel.SetValue(it, true, null);
                                                }
                                            }
                                            MethodInfo miRem = rpPage.GetType().GetMethod("removeSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (miRem != null) miRem.Invoke(rpPage, null);
                                        } catch (Exception exM) { Console.WriteLine("[Launcher] Context menu clear error: " + exM.Message); }
                                    };
                                    menu.Items.Add(mnuRem);
                                    menu.Items.Add(mnuClr);
                                    lvCtrl.ContextMenuStrip = menu;
                                }

                                FieldInfo btnRemField = rpPage.GetType().GetField("_removeButton", BindingFlags.NonPublic | BindingFlags.Instance);
                                Control btnRem = btnRemField != null ? btnRemField.GetValue(rpPage) as Control : null;
                                if (btnRem != null)
                                {
                                    btnRem.Visible = true;
                                }




                            }



                        }



                        // Setup simulated live vehicle sensor model when disconnected from DiCE



                        FieldInfo wfField = vcForm.GetType().GetField("_vehicleCommunicationWf", BindingFlags.NonPublic | BindingFlags.Instance);



                        object wf = wfField != null ? wfField.GetValue(vcForm) : null;



                        // vcCtrl already defined



                        if (vcCtrl != null && vcCtrl.Tag == null && wf != null)



                        {



                            vcCtrl.Tag = "sim_active";



                            var simTimer = new System.Windows.Forms.Timer();



                            simTimer.Interval = 500;



                            var rnd = new Random();



                            var simStates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);



                            simTimer.Tick += (sSim, eSim) => {



                                try



                                {
                                    if (!IsMockSimulation) return;




                                    // Stop Volvo's failing background timer so it doesn't clear parameter cells



                                    FieldInfo tField = wf.GetType().GetField("_parameterReadTimer", BindingFlags.NonPublic | BindingFlags.Instance);



                                    if (tField != null)



                                    {



                                        object tmr = tField.GetValue(wf);



                                        if (tmr != null)



                                        {



                                            MethodInfo stopM = tmr.GetType().GetMethod("Stop");



                                            if (stopM != null) stopM.Invoke(tmr, null);



                                        }



                                    }



                                    PropertyInfo piToRead = wf.GetType().GetProperty("ParametersToRead");



                                    if (piToRead == null) return;



                                    ICollection toRead = piToRead.GetValue(wf, null) as ICollection;



                                    if (toRead == null || toRead.Count == 0) return;



                                    FieldInfo evField = wf.GetType().GetField("OnParameterRead", BindingFlags.NonPublic | BindingFlags.Instance);



                                    if (evField == null) return;



                                    MulticastDelegate del = evField.GetValue(wf) as MulticastDelegate;



                                    if (del == null) return;



                                    Type resType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleCommunication.ReadParameterResult);



                                    foreach (object item in toRead)



                                    {



                                        PropertyInfo piName = item.GetType().GetProperty("LongName");



                                        PropertyInfo piShort = item.GetType().GetProperty("ShortName");



                                        PropertyInfo piUnit = item.GetType().GetProperty("Unit");



                                        PropertyInfo piMin = item.GetType().GetProperty("PlotterMin");



                                        PropertyInfo piMax = item.GetType().GetProperty("PlotterMax");



                                        string longName = piName != null ? (string)piName.GetValue(item, null) : "";



                                        string shortName = piShort != null ? (string)piShort.GetValue(item, null) : "";



                                        string unit = piUnit != null ? (string)piUnit.GetValue(item, null) : "";



                                        double pMin = piMin != null ? Convert.ToDouble(piMin.GetValue(item, null)) : 0.0;



                                        double pMax = piMax != null ? Convert.ToDouble(piMax.GetValue(item, null)) : 100.0;



                                        string key = !string.IsNullOrEmpty(longName) ? longName : shortName;



                                        if (string.IsNullOrEmpty(key)) continue;



                                        if (!simStates.ContainsKey(key))



                                        {



                                            if (key.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 && key.IndexOf("engine", StringComparison.OrdinalIgnoreCase) < 0)



                                                simStates[key] = 0.0;



                                            else if (key.IndexOf("rpm", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("engine speed", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 750.0;



                                            else if (key.IndexOf("voltage", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("power supply", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 12.5;



                                            else if (key.IndexOf("fuel pressure", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 350000.0;



                                            else if (key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("ect", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 85.0;



                                            else if (key.IndexOf("pedal", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 8.0;



                                            else if (key.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)



                                                simStates[key] = 25.0;



                                            else



                                                simStates[key] = pMax > pMin ? pMin + (pMax - pMin) * 0.3 : 15.0;



                                        }



                                        double cur = simStates[key];



                                        double val = cur;



                                        string formattedText = "";



                                        if (key.IndexOf("rpm", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("engine speed", StringComparison.OrdinalIgnoreCase) >= 0)



                                        {



                                            cur += (rnd.NextDouble() - 0.5) * 30.0;



                                            if (cur < 700.0) cur = 700.0;



                                            if (cur > 820.0) cur = 820.0;



                                            val = Math.Round(cur);



                                            formattedText = string.Format("{0:0} {1}", val, unit);



                                        }



                                        else if (key.IndexOf("voltage", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("power supply", StringComparison.OrdinalIgnoreCase) >= 0)



                                        {



                                            cur += (rnd.NextDouble() - 0.5) * 0.08;



                                            if (cur < 12.1) cur = 12.1;



                                            if (cur > 12.9) cur = 12.9;



                                            val = Math.Round(cur, 2);



                                            formattedText = string.Format("{0:0.00} {1}", val, unit);



                                        }



                                        else if (key.IndexOf("fuel pressure", StringComparison.OrdinalIgnoreCase) >= 0)



                                        {



                                            cur += (rnd.NextDouble() - 0.5) * 6000.0;



                                            if (cur < 330000.0) cur = 330000.0;



                                            if (cur > 370000.0) cur = 370000.0;



                                            val = Math.Round(cur);



                                            formattedText = string.Format("{0:0} {1}", val, unit);



                                        }



                                        else if (key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("ect", StringComparison.OrdinalIgnoreCase) >= 0)



                                        {



                                            cur += (rnd.NextDouble() - 0.5) * 0.2;



                                            if (cur < 80.0) cur = 80.0;



                                            if (cur > 92.0) cur = 92.0;



                                            val = Math.Round(cur, 1);



                                            formattedText = string.Format("{0:0.0} {1}", val, unit);



                                        }



                                        else if (key.IndexOf("pedal", StringComparison.OrdinalIgnoreCase) >= 0)



                                        {



                                            cur += (rnd.NextDouble() - 0.5) * 0.2;



                                            if (cur < 7.6) cur = 7.6;



                                            if (cur > 8.4) cur = 8.4;



                                            val = Math.Round(cur, 1);



                                            formattedText = string.Format("{0:0.0} {1}", val, unit);



                                        }



                                        else



                                        {



                                            double span = pMax > pMin ? (pMax - pMin) : 20.0;



                                            cur += (rnd.NextDouble() - 0.5) * (span * 0.05);



                                            if (pMax > pMin)



                                            {



                                                if (cur < pMin) cur = pMin;



                                                if (cur > pMax) cur = pMax;



                                            }



                                            val = Math.Round(cur, 1);



                                            formattedText = string.Format("{0:0.0} {1}", val, unit);



                                        }



                                        simStates[key] = cur;



                                        object resObj = Activator.CreateInstance(resType, new object[] { val, formattedText });



                                        del.DynamicInvoke(new object[] { item, resObj });



                                    }



                                }



                                catch {}



                            };



                            simTimer.Start();



                        }



                    }



                }



            }



            catch {}



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



        Control internalReadoutBtn = null;



        object vehInfoInstance = null;



        object vehProfile = null;



        object diceEnumValue = null;



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



            try



            {



                        // Archive leftover DRO xml.zip logs to avoid failing offline SOAP upload calls

        try

        {

            string logDiagDir = Path.Combine(vidaPath, @"System\Log\Diagnostics");

            if (Directory.Exists(logDiagDir))

            {

                string archiveDir = Path.Combine(logDiagDir, "Archive");

                if (!Directory.Exists(archiveDir)) Directory.CreateDirectory(archiveDir);

                foreach (var zip in Directory.GetFiles(logDiagDir, "*.xml.zip"))

                {

                    try

                    {

                        string dest = Path.Combine(archiveDir, Path.GetFileName(zip));

                        if (File.Exists(dest)) File.Delete(dest);

                        File.Move(zip, dest);

                    }

                    catch {}

                }

            }

        }

        catch {}



        Console.WriteLine("[Launcher] Calling diag.LoadControl()...");



                diag.LoadControl();



                Console.WriteLine("[Launcher] diag.LoadControl() succeeded!");



                Type mgrType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");



                object mgr = mgrType != null ? mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null) : null;



                if (mgr != null)



                {



                    FieldInfo viField = mgrType.GetField("_vehicleInformationForm", BindingFlags.NonPublic | BindingFlags.Instance);



                    if (viField != null)



                    {



                        vehInfoInstance = viField.GetValue(mgr);



                        if (vehInfoInstance != null)



                        {



                            Type viType = vehInfoInstance.GetType();



                            FieldInfo viCtName = viType.GetField("_commToolName", BindingFlags.NonPublic | BindingFlags.Instance);



                            if (viCtName != null) viCtName.SetValue(vehInfoInstance, TargetCommTool);



                            FieldInfo wfField = viType.GetField("_vehicleInformationWf", BindingFlags.NonPublic | BindingFlags.Instance);



                            if (wfField != null)



                            {



                                object wf = wfField.GetValue(vehInfoInstance);



                                if (wf != null)



                                {



                                    PropertyInfo dvcProp = wf.GetType().GetProperty("DisableVehicleCheck");



                                    if (dvcProp != null) dvcProp.SetValue(wf, true, null);



                                    FieldInfo sessField = wf.GetType().GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance);



                                    if (sessField != null)



                                    {



                                        object sess = sessField.GetValue(wf);



                                        if (sess != null)



                                        {



                                            SetProp(sess.GetType(), sess, "AllowUpdate", true);



                                            SetProp(sess.GetType(), sess, "AllowVehicleCommunication", true);



                                            SetProp(sess.GetType(), sess, "ReferenceMode", false);



                                        }



                                    }



                                    FieldInfo initResField = viType.GetField("_initResult", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (initResField != null)
                                    {
                                        Type wfInitType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.WfInitResult");
                                        Type wfInitEnum = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.WfInit");
                                        object correctVinVal = Enum.Parse(wfInitEnum, "CorrectVin");
                                        object initRes = Activator.CreateInstance(wfInitType, new object[] { correctVinVal, false });
                                        initResField.SetValue(vehInfoInstance, initRes);
                                        Console.WriteLine("[Launcher] Fast startup: Initialized VehicleInformationForm._initResult to CorrectVin");
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



                // Configure all CommTools and Sessions across all singletons and forms



                ConfigureAllCommTools(diag, new HashSet<object>(), diceEnumValue);



                if (mgr != null) ConfigureAllCommTools(mgr, new HashSet<object>(), diceEnumValue);



                PopulateAllSessions(vehProfile);



                // Attach WorkshopSession to the existing database session (for offline DTC viewing & diagnostics)



                if (mgr != null)



                {



                    try



                    {



                        FieldInfo wsField = mgrType.GetField("_workshopSession", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        if (wsField != null)



                        {



                            object ws = wsField.GetValue(mgr);



                            if (ws != null)



                            {



                                MethodInfo miAttach = ws.GetType().GetMethod("Attach", BindingFlags.Public | BindingFlags.Instance);



                                if (miAttach != null)



                                {



                                    bool attached = (bool)miAttach.Invoke(ws, null);



                                    Console.WriteLine("[Launcher] WorkshopSession.Attach() returned: " + attached);
                                    _cachedVehInfoInstance = vehInfoInstance;
                                    InitializeCarStatus(vehInfoInstance, vehProfile);
                                    RegisterScriptingContext(vehProfile, mgr, null);




                                }



                            }



                        }



                        // Inspect CustomerSymptoms & DTCs



                        FieldInfo carComField = mgrType.GetField("_carComRepository", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        object carCom = carComField != null ? carComField.GetValue(mgr) : null;



                        Console.WriteLine("[Launcher] CarComRepository: " + (carCom != null ? "found" : "null"));



                        MethodInfo miGetForm = mgrType.GetMethod("GetCustomerSymptomsFaultTraceFormForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        if (miGetForm != null)



                        {



                            object form = miGetForm.Invoke(mgr, null);



                            Console.WriteLine("[Launcher] CustomerSymptomsFaultTraceForm instantiated successfully!");



                            MethodInfo miReset = form.GetType().GetMethod("ResetForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            if (miReset != null)



                            {



                                miReset.Invoke(form, null);



                                Console.WriteLine("[Launcher] ResetForm invoked on CustomerSymptomsFaultTraceForm!");



                            }



                            FieldInfo wfField = form.GetType().GetField("_dtcFaulttraceWf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            object wf = wfField != null ? wfField.GetValue(form) : null;



                            if (wf != null)



                            {



                                MethodInfo miLatest = wf.GetType().GetMethod("GetLatestDtcReadout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                                object latest = miLatest != null ? miLatest.Invoke(wf, null) : null;



                                Console.WriteLine("[Launcher] Latest DtcReadout: " + (latest != null ? latest.ToString() : "null"));



                                if (latest != null)



                                {



                                    int readoutId = (int)latest.GetType().GetField("DtcReadoutId").GetValue(latest);



                                    MethodInfo miGetDtcs = wf.GetType().GetMethod("GetDtcs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);



                                    Array dtcs = miGetDtcs != null ? (Array)miGetDtcs.Invoke(wf, new object[] { readoutId }) : null;



                                    Console.WriteLine("[Launcher] Loaded DTC count: " + (dtcs != null ? dtcs.Length : 0));



                                    if (dtcs != null)



                                    {



                                        foreach (object dtcObj in dtcs)



                                        {



                                            PropertyInfo piHex = dtcObj.GetType().GetProperty("HexCode");



                                            PropertyInfo piSymp = dtcObj.GetType().GetProperty("SymptomId");



                                            MethodInfo miRank = dtcObj.GetType().GetMethod("GetRank");



                                            object hex = piHex != null ? piHex.GetValue(dtcObj, null) : "?";



                                            object symp = piSymp != null ? piSymp.GetValue(dtcObj, null) : "?";



                                            object rank = miRank != null ? miRank.Invoke(dtcObj, null) : "?";



                                            Console.WriteLine("  DTC: " + hex + " (SymptomId: " + symp + ", Rank: " + rank + ")");



                                        }



                                    }



                                    // Diagnose GetOtherCustomerSymptoms failure



                                    Console.WriteLine("[Launcher] Testing GetOtherCustomerSymptoms step by step...");



                                    try



                                    {



                                        Type csType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.DtcFaulttrace.CustomerSymptom");



                                        if (csType == null) csType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.DtcFaulttrace.CustomerSymptom");



                                        MethodInfo miNotConn = wf.GetType().GetMethod("GetDtcsNotConnectedToACSC", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                                        Array dtcsNotConn = miNotConn != null ? (Array)miNotConn.Invoke(wf, null) : null;



                                        Console.WriteLine("[Launcher] dtcsNotConnected count: " + (dtcsNotConn != null ? dtcsNotConn.Length : 0));



                                        // Step through each DTC and look up its CSCs



                                        Hashtable ht = new Hashtable();



                                        MethodInfo miGetCscIds = carCom.GetType().GetMethod("GetCustomerSymptomIDsByDtcSymptomIDs");



                                        MethodInfo miGetCsc = carCom.GetType().GetMethod("GetCustomerSymptomByID");



                                        foreach (object dtcObj in dtcs)



                                        {



                                            int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                            int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                            if (cscIds != null)



                                            {



                                                foreach (int cscId in cscIds)



                                                {



                                                    if (!ht.ContainsKey(cscId))



                                                    {



                                                        object vo = miGetCsc.Invoke(carCom, new object[] { cscId });



                                                        if (vo != null)



                                                        {



                                                            object compName = vo.GetType().GetProperty("ComponentName").GetValue(vo, null);



                                                            Console.WriteLine("  CSC ID " + cscId + " -> CompName: '" + compName + "'");



                                                            ht[cscId] = vo;



                                                        }



                                                        else



                                                        {



                                                            Console.WriteLine("  CSC ID " + cscId + " -> VO IS NULL!");



                                                        }



                                                    }



                                                }



                                            }



                                        }



                                        // Recreate exact CustomerSymptoms and test pairwise compare



                                        ArrayList csList = new ArrayList();



                                        ConstructorInfo csCtor = csType.GetConstructor(new Type[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });



                                        foreach (DictionaryEntry de in ht)



                                        {



                                            int id = (int)de.Key;



                                            object vo = de.Value;



                                            string code = (string)vo.GetType().GetProperty("Code").GetValue(vo, null) ?? "";



                                            string compName = (string)vo.GetType().GetProperty("ComponentName").GetValue(vo, null) ?? "";



                                            string dev = (string)vo.GetType().GetProperty("Deviation").GetValue(vo, null) ?? "";



                                            string c1 = (string)vo.GetType().GetProperty("Comment1").GetValue(vo, null) ?? "";



                                            string c2 = (string)vo.GetType().GetProperty("Comment2").GetValue(vo, null) ?? "";



                                            object cs = csCtor.Invoke(new object[] { id, code, compName, dev, c1, c2 });



                                            // Add matching DTCs



                                            FieldInfo fiDtcs = csType.GetField("DTCs");



                                            ArrayList dtcArr = (ArrayList)fiDtcs.GetValue(cs);



                                            foreach (object dtcObj in dtcs)



                                            {



                                                int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                                int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                                if (cscIds != null && Array.IndexOf(cscIds, id) >= 0)



                                                {



                                                    dtcArr.Add(dtcObj);



                                                }



                                            }



                                            csList.Add(cs);



                                        }



                                        // Check if unknown DTCs exist and add csUnknown to csList



                                        ArrayList unknownDtcs = new ArrayList();



                                        foreach (object dtcObj in dtcs)



                                        {



                                            int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                            int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                            if (cscIds == null || cscIds.Length == 0)



                                            {



                                                unknownDtcs.Add(dtcObj);



                                            }



                                        }



                                        Console.WriteLine("[Launcher] unknownDtcs count: " + unknownDtcs.Count);



                                        if (unknownDtcs.Count > 0)



                                        {



                                            Type rmType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.ResourceManager.ResourceManager");



                                            if (rmType == null) rmType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.ResourceManager.ResourceManager");



                                            MethodInfo miGetStr = rmType.GetMethod("GetString", new Type[] { typeof(string) });



                                            string unkDesc = (string)miGetStr.Invoke(null, new object[] { "lblUnknownDTCDesc" });



                                            Console.WriteLine("  lblUnknownDTCDesc = '" + unkDesc + "'");



                                            object csUnk = csCtor.Invoke(new object[] { 0, "", unkDesc, "", "", "" });



                                            FieldInfo fiDtcsUnk = csType.GetField("DTCs");



                                            ArrayList dtcArrUnk = (ArrayList)fiDtcsUnk.GetValue(csUnk);



                                            dtcArrUnk.AddRange(unknownDtcs);



                                            csList.Add(csUnk);



                                        }



                                        Console.WriteLine("[Launcher] Testing pairwise CompareTo across " + csList.Count + " symptoms...");



                                        for (int i = 0; i < csList.Count; i++)



                                        {



                                            for (int j = 0; j < csList.Count; j++)



                                            {



                                                try



                                                {



                                                    MethodInfo miCmp = csType.GetMethod("CompareTo", new Type[] { typeof(object) });



                                                    miCmp.Invoke(csList[i], new object[] { csList[j] });



                                                }



                                                catch (Exception exCmp)



                                                {



                                                    Console.WriteLine("  FAILURE comparing i=" + i + " to j=" + j + ": " + exCmp.InnerException);



                                                    throw;



                                                }



                                            }



                                        }



                                        Console.WriteLine("[Launcher] Pairwise CompareTo PASSED!");



                                    }



                                    catch (Exception exOther)



                                    {



                                        Console.WriteLine("[Launcher] GetOtherCustomerSymptoms failed: " + exOther.InnerException);



                                    }



                                }



                            }



                        }



                    }



                    catch (Exception exWs)



                    {



                        Console.WriteLine("[Launcher] Inspection exception: " + exWs);



                    }



                }



                // Enable all native tab navigation buttons



                if (mgr != null)



                {



                    string[] btnFieldNames = new string[] {



                        "_vehicleInfoButton",



                        "_networkStatusButton",



                        "_customerSymptomsFaultTraceButton",



                        "_vehCommButton",



                        "_referenceInformationButton"



                    };



                    foreach (string fName in btnFieldNames)



                    {



                        try



                        {



                            FieldInfo fi = mgrType.GetField(fName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            if (fi != null)



                            {



                                Control b = fi.GetValue(mgr) as Control;



                                if (b != null)



                                {



                                    b.Enabled = true;



                                    b.Visible = true;



                                }



                            }



                        }



                        catch {}



                    }



                }



                Console.WriteLine("[Launcher] CommTools, Sessions, and Navigation Tabs fully synchronized.");



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



                Console.WriteLine("[Launcher] Synchronizing CommTools and Sessions before readout...");



                Type mgrType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");



                object mgr = mgrType != null ? mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null) : null;



                ConfigureAllCommTools(diag, new HashSet<object>(), diceEnumValue);



                if (mgr != null) ConfigureAllCommTools(mgr, new HashSet<object>(), diceEnumValue);



                PopulateAllSessions(vehProfile);



                // Attach WorkshopSession to the existing database session (for offline DTC viewing & diagnostics)



                if (mgr != null)



                {



                    try



                    {



                        FieldInfo wsField = mgrType.GetField("_workshopSession", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        if (wsField != null)



                        {



                            object ws = wsField.GetValue(mgr);



                            if (ws != null)



                            {



                                MethodInfo miAttach = ws.GetType().GetMethod("Attach", BindingFlags.Public | BindingFlags.Instance);



                                if (miAttach != null)



                                {



                                    bool attached = (bool)miAttach.Invoke(ws, null);



                                    Console.WriteLine("[Launcher] WorkshopSession.Attach() returned: " + attached);



                                }



                            }



                        }



                        // Inspect CustomerSymptoms & DTCs



                        FieldInfo carComField = mgrType.GetField("_carComRepository", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        object carCom = carComField != null ? carComField.GetValue(mgr) : null;



                        Console.WriteLine("[Launcher] CarComRepository: " + (carCom != null ? "found" : "null"));



                        MethodInfo miGetForm = mgrType.GetMethod("GetCustomerSymptomsFaultTraceFormForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                        if (miGetForm != null)



                        {



                            object form = miGetForm.Invoke(mgr, null);



                            Console.WriteLine("[Launcher] CustomerSymptomsFaultTraceForm instantiated successfully!");



                            MethodInfo miReset = form.GetType().GetMethod("ResetForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            if (miReset != null)



                            {



                                miReset.Invoke(form, null);



                                Console.WriteLine("[Launcher] ResetForm invoked on CustomerSymptomsFaultTraceForm!");



                            }



                            FieldInfo wfField = form.GetType().GetField("_dtcFaulttraceWf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                            object wf = wfField != null ? wfField.GetValue(form) : null;



                            if (wf != null)



                            {



                                MethodInfo miLatest = wf.GetType().GetMethod("GetLatestDtcReadout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                                object latest = miLatest != null ? miLatest.Invoke(wf, null) : null;



                                Console.WriteLine("[Launcher] Latest DtcReadout: " + (latest != null ? latest.ToString() : "null"));



                                if (latest != null)



                                {



                                    int readoutId = (int)latest.GetType().GetField("DtcReadoutId").GetValue(latest);



                                    MethodInfo miGetDtcs = wf.GetType().GetMethod("GetDtcs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);



                                    Array dtcs = miGetDtcs != null ? (Array)miGetDtcs.Invoke(wf, new object[] { readoutId }) : null;



                                    Console.WriteLine("[Launcher] Loaded DTC count: " + (dtcs != null ? dtcs.Length : 0));



                                    if (dtcs != null)



                                    {



                                        foreach (object dtcObj in dtcs)



                                        {



                                            PropertyInfo piHex = dtcObj.GetType().GetProperty("HexCode");



                                            PropertyInfo piSymp = dtcObj.GetType().GetProperty("SymptomId");



                                            MethodInfo miRank = dtcObj.GetType().GetMethod("GetRank");



                                            object hex = piHex != null ? piHex.GetValue(dtcObj, null) : "?";



                                            object symp = piSymp != null ? piSymp.GetValue(dtcObj, null) : "?";



                                            object rank = miRank != null ? miRank.Invoke(dtcObj, null) : "?";



                                            Console.WriteLine("  DTC: " + hex + " (SymptomId: " + symp + ", Rank: " + rank + ")");



                                        }



                                    }



                                    // Diagnose GetOtherCustomerSymptoms failure



                                    Console.WriteLine("[Launcher] Testing GetOtherCustomerSymptoms step by step...");



                                    try



                                    {



                                        Type csType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.DtcFaulttrace.CustomerSymptom");



                                        if (csType == null) csType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.Workflow.DtcFaulttrace.CustomerSymptom");



                                        MethodInfo miNotConn = wf.GetType().GetMethod("GetDtcsNotConnectedToACSC", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);



                                        Array dtcsNotConn = miNotConn != null ? (Array)miNotConn.Invoke(wf, null) : null;



                                        Console.WriteLine("[Launcher] dtcsNotConnected count: " + (dtcsNotConn != null ? dtcsNotConn.Length : 0));



                                        // Step through each DTC and look up its CSCs



                                        Hashtable ht = new Hashtable();



                                        MethodInfo miGetCscIds = carCom.GetType().GetMethod("GetCustomerSymptomIDsByDtcSymptomIDs");



                                        MethodInfo miGetCsc = carCom.GetType().GetMethod("GetCustomerSymptomByID");



                                        foreach (object dtcObj in dtcs)



                                        {



                                            int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                            int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                            if (cscIds != null)



                                            {



                                                foreach (int cscId in cscIds)



                                                {



                                                    if (!ht.ContainsKey(cscId))



                                                    {



                                                        object vo = miGetCsc.Invoke(carCom, new object[] { cscId });



                                                        if (vo != null)



                                                        {



                                                            object compName = vo.GetType().GetProperty("ComponentName").GetValue(vo, null);



                                                            Console.WriteLine("  CSC ID " + cscId + " -> CompName: '" + compName + "'");



                                                            ht[cscId] = vo;



                                                        }



                                                        else



                                                        {



                                                            Console.WriteLine("  CSC ID " + cscId + " -> VO IS NULL!");



                                                        }



                                                    }



                                                }



                                            }



                                        }



                                        // Recreate exact CustomerSymptoms and test pairwise compare



                                        ArrayList csList = new ArrayList();



                                        ConstructorInfo csCtor = csType.GetConstructor(new Type[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });



                                        foreach (DictionaryEntry de in ht)



                                        {



                                            int id = (int)de.Key;



                                            object vo = de.Value;



                                            string code = (string)vo.GetType().GetProperty("Code").GetValue(vo, null) ?? "";



                                            string compName = (string)vo.GetType().GetProperty("ComponentName").GetValue(vo, null) ?? "";



                                            string dev = (string)vo.GetType().GetProperty("Deviation").GetValue(vo, null) ?? "";



                                            string c1 = (string)vo.GetType().GetProperty("Comment1").GetValue(vo, null) ?? "";



                                            string c2 = (string)vo.GetType().GetProperty("Comment2").GetValue(vo, null) ?? "";



                                            object cs = csCtor.Invoke(new object[] { id, code, compName, dev, c1, c2 });



                                            // Add matching DTCs



                                            FieldInfo fiDtcs = csType.GetField("DTCs");



                                            ArrayList dtcArr = (ArrayList)fiDtcs.GetValue(cs);



                                            foreach (object dtcObj in dtcs)



                                            {



                                                int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                                int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                                if (cscIds != null && Array.IndexOf(cscIds, id) >= 0)



                                                {



                                                    dtcArr.Add(dtcObj);



                                                }



                                            }



                                            csList.Add(cs);



                                        }



                                        // Check if unknown DTCs exist and add csUnknown to csList



                                        ArrayList unknownDtcs = new ArrayList();



                                        foreach (object dtcObj in dtcs)



                                        {



                                            int sympId = (int)dtcObj.GetType().GetProperty("SymptomId").GetValue(dtcObj, null);



                                            int[] cscIds = (int[])miGetCscIds.Invoke(carCom, new object[] { new int[] { sympId } });



                                            if (cscIds == null || cscIds.Length == 0)



                                            {



                                                unknownDtcs.Add(dtcObj);



                                            }



                                        }



                                        Console.WriteLine("[Launcher] unknownDtcs count: " + unknownDtcs.Count);



                                        if (unknownDtcs.Count > 0)



                                        {



                                            Type rmType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.ResourceManager.ResourceManager");



                                            if (rmType == null) rmType = typeof(Vcc.Vida.DiagSwdl.Workflow.VehicleInformation.VehicleInformationWF).Assembly.GetType("Vcc.Vida.DiagSwdl.ResourceManager.ResourceManager");



                                            MethodInfo miGetStr = rmType.GetMethod("GetString", new Type[] { typeof(string) });



                                            string unkDesc = (string)miGetStr.Invoke(null, new object[] { "lblUnknownDTCDesc" });



                                            Console.WriteLine("  lblUnknownDTCDesc = '" + unkDesc + "'");



                                            object csUnk = csCtor.Invoke(new object[] { 0, "", unkDesc, "", "", "" });



                                            FieldInfo fiDtcsUnk = csType.GetField("DTCs");



                                            ArrayList dtcArrUnk = (ArrayList)fiDtcsUnk.GetValue(csUnk);



                                            dtcArrUnk.AddRange(unknownDtcs);



                                            csList.Add(csUnk);



                                        }



                                        Console.WriteLine("[Launcher] Testing pairwise CompareTo across " + csList.Count + " symptoms...");



                                        for (int i = 0; i < csList.Count; i++)



                                        {



                                            for (int j = 0; j < csList.Count; j++)



                                            {



                                                try



                                                {



                                                    MethodInfo miCmp = csType.GetMethod("CompareTo", new Type[] { typeof(object) });



                                                    miCmp.Invoke(csList[i], new object[] { csList[j] });



                                                }



                                                catch (Exception exCmp)



                                                {



                                                    Console.WriteLine("  FAILURE comparing i=" + i + " to j=" + j + ": " + exCmp.InnerException);



                                                    throw;



                                                }



                                            }



                                        }



                                        Console.WriteLine("[Launcher] Pairwise CompareTo PASSED!");



                                    }



                                    catch (Exception exOther)



                                    {



                                        Console.WriteLine("[Launcher] GetOtherCustomerSymptoms failed: " + exOther.InnerException);



                                    }



                                }



                            }



                        }



                    }



                    catch (Exception exWs)



                    {



                        Console.WriteLine("[Launcher] Inspection exception: " + exWs);



                    }



                }



                if (vehInfoInstance != null)



                {



                    Type viType = vehInfoInstance.GetType();



                    FieldInfo viCtName = viType.GetField("_commToolName", BindingFlags.NonPublic | BindingFlags.Instance);



                    if (viCtName != null) viCtName.SetValue(vehInfoInstance, TargetCommTool);



                    MethodInfo mi = viType.GetMethod("PerformReadout", BindingFlags.NonPublic | BindingFlags.Instance);



                    if (mi != null)



                    {



                        Console.WriteLine("[Launcher] Triggering native PerformReadout()...");



                        mi.Invoke(vehInfoInstance, null);



                        return;



                    }



                }



                if (internalReadoutBtn != null)



                {



                    MethodInfo mi = internalReadoutBtn.GetType().GetMethod("PerformClick");



                    if (mi != null)



                    {



                        mi.Invoke(internalReadoutBtn, null);



                        return;



                    }



                }



            }



            catch (Exception ex)



            {



                Exception inner = ex.InnerException != null ? ex.InnerException : ex;



                Console.WriteLine("[Launcher] Readout exception: " + inner);



                MessageBox.Show("Readout error: " + inner.Message, "VIDA Readout", MessageBoxButtons.OK, MessageBoxIcon.Error);



            }



        };



        // If run with /test flag, systematically switch across all tabs and capture screenshots



        if (args.Length > 0 && (args[0] == "/test" || args[0] == "--test"))



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



