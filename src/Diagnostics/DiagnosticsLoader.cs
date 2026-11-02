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
    static void HideLegacyDiagnosticChrome(object mgr)
    {
        if (mgr == null) return;
        try
        {
            Type mgrType = mgr.GetType();
            FieldInfo navPnlField = mgrType.GetField("_navPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo workPnlField = mgrType.GetField("_workAreaPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Control navPnl = navPnlField != null ? navPnlField.GetValue(mgr) as Control : null;
            Control workPnl = workPnlField != null ? workPnlField.GetValue(mgr) as Control : null;
            if (navPnl != null && navPnl.Visible)
            {
                int navH = navPnl.Height > 0 ? navPnl.Height : 28;
                navPnl.Visible = false;
                navPnl.Height = 0;
                if (workPnl != null && workPnl.Dock == DockStyle.None && workPnl.Top >= navH)
                {
                    workPnl.Top -= navH;
                    workPnl.Height += navH;
                }
            }

            // Hide the legacy right-hand CarStatus box inside each CarStatusTabForm so it never overlaps Vehicle Comm buttons
            Type cstfType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Components.CarStatusTabForm");
            FieldInfo csField = cstfType != null ? cstfType.GetField("_carStatus", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;
            if (csField != null)
            {
                string[] formFields = new string[] { "_vehicleInformationForm", "_customerSymptomsFaultTraceForm", "_networkForm", "_vehCommForm" };
                foreach (string ff in formFields)
                {
                    FieldInfo fInfo = mgrType.GetField(ff, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object fObj = fInfo != null ? fInfo.GetValue(mgr) : null;
                    Control csCtrl = (fObj != null) ? csField.GetValue(fObj) as Control : null;
                    if (csCtrl != null) csCtrl.Visible = false;
                }
            }

            // Ensure ReferenceInformationForm fills _workAreaPanel cleanly without overriding its inner _tabPanel
            FieldInfo refField = mgrType.GetField("_referenceInformationForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Control refCtrl = refField != null ? refField.GetValue(mgr) as Control : null;
            if (refCtrl != null)
            {
                refCtrl.Dock = DockStyle.Fill;
            }
        }
        catch {}
    }

    static void InitializeDiagnosticsControl(string vidaPath, DiagnosticMainForm diag)
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
                            _activeDtcFaulttraceWf = wf;

                            try
                            {
                                MethodInfo miGetNet = mgrType.GetMethod("GetNetworkForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (miGetNet != null)
                                {
                                    object netForm = miGetNet.Invoke(mgr, null);
                                    if (netForm != null)
                                    {
                                        FieldInfo nwfField = netForm.GetType().GetField("_networkFaulttraceWf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (nwfField != null) _activeNetworkFaulttraceWf = nwfField.GetValue(netForm);
                                    }
                                }
                            }
                            catch (Exception exNet)
                            {
                                Console.WriteLine("[Launcher] NetworkForm resolution error: " + exNet.Message);
                            }

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

                // Enable all native tab navigation buttons and hide the redundant legacy gray navigation ribbon (_navPanel)
                // by collapsing _navPanel to 0px and expanding _workAreaPanel up by its original height.

                if (mgr != null)

                {

                    HideLegacyDiagnosticChrome(mgr);

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

    }
}
