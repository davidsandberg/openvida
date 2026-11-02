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
                            ?? Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BizServices.Scripting.DLL"))
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


    static void RegisterDummyIsoViewHotspot()

    {

        try

        {

            Type dummyType = Type.GetType("IsoViewStub.DummyIsoView, DummyIsoView");

            if (dummyType == null)

            {

                Assembly dummyAsm = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DummyIsoView.dll"));

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

}
