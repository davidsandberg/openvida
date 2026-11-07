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
    static void ExecuteVehicleReadout(DiagnosticMainForm diag)
    {
            try

            {

                Console.WriteLine("[Launcher] Synchronizing CommTools and Sessions before readout...");

                Type mgrType = typeof(DiagnosticMainForm).Assembly.GetType("Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticManager");

                object mgr = mgrType != null ? mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null) : null;

                ConfigureAllCommTools(diag, new HashSet<object>(), CurrentCommToolEnumValue);

                if (mgr != null) ConfigureAllCommTools(mgr, new HashSet<object>(), CurrentCommToolEnumValue);

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
                        try { Vcc.Vida.DiagSwdl.UI.Diagnostics.ReadoutExporter.ExportLatestReadout(TargetVIN); } catch (Exception exExp) { Console.WriteLine("[Launcher] Readout auto-export notice: " + exExp.Message); }

                        return;

                    }

                }

                if (internalReadoutBtn != null)

                {

                    MethodInfo mi = internalReadoutBtn.GetType().GetMethod("PerformClick");

                    if (mi != null)

                    {

                        mi.Invoke(internalReadoutBtn, null);
                        try { Vcc.Vida.DiagSwdl.UI.Diagnostics.ReadoutExporter.ExportLatestReadout(TargetVIN); } catch (Exception exExp) { Console.WriteLine("[Launcher] Readout auto-export notice: " + exExp.Message); }

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

    }
}
