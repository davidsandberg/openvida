using System;

using System.Collections;

using System.Collections.Generic;

using System.Drawing;

using System.Drawing.Imaging;

using System.Globalization;

using System.IO;

using System.Reflection;

using System.Windows.Forms;

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

        try { if (_inner != null) _inner.OnUseProgress(useIt); } catch {}

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



static class Program

{

    const string TargetVIN = "YV1BW5251B1189886";

    const string TargetProfile = "0b00c8af84add183";

    const string TargetPartnerGroup = "1002";

    const string TargetCommTool = "DiCE-206751";

    const string TargetCulture = "en-US";



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



    static string RenderDocumentHtml(string docId, string highlight)

    {

        try

        {

            byte[] bytes = null;

            using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))

            {

                conn.Open();

                using (var cmd = conn.CreateCommand())

                {

                    cmd.CommandText = "SELECT XmlContent FROM Document WHERE chronicleId = @id";

                    cmd.Parameters.AddWithValue("@id", docId);

                    var obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value) bytes = (byte[])obj;

                }

            }



            if (bytes == null)

            {

                return "<html><body style='font-family:sans-serif;padding:20px;'><h3>Document " + docId + " not found in service repository.</h3></body></html>";

            }



            string xmlContent = "";

            using (var ms = new System.IO.MemoryStream(bytes))

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

            var sb = new System.Text.StringBuilder();

            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");

            sb.Append("<style>");

            sb.Append("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 24px; line-height: 1.6; color: #222; background: #fff; }");

            sb.Append("h1 { color: #003057; border-bottom: 2px solid #003057; padding-bottom: 8px; font-size: 22px; margin-top: 0; }");

            sb.Append("h2 { color: #005087; margin-top: 28px; font-size: 17px; border-bottom: 1px solid #ddd; padding-bottom: 4px; }");

            sb.Append(".procstep { margin: 12px 0 16px 0; padding: 12px 16px; border-left: 3px solid #005087; background: #f8fafc; border-radius: 3px; }");

            sb.Append(".highlight { background: #fff9c4 !important; border-left: 5px solid #f57f17 !important; box-shadow: 0 0 10px rgba(0,0,0,0.12); }");

            sb.Append(".procstep-title { font-weight: bold; font-size: 15px; color: #003057; margin-bottom: 6px; }");

            sb.Append(".hint { background: #e3f2fd; border-left: 4px solid #1976d2; padding: 12px; margin: 16px 0; border-radius: 3px; }");

            sb.Append("p { margin: 6px 0; font-size: 13px; }");

            sb.Append("</style></head><body>");



            var mainTitle = doc.Root.Element("title");

            if (mainTitle != null) sb.AppendFormat("<h1>{0}</h1>", mainTitle.Value);



            foreach (var hint in doc.Descendants("hint"))

            {

                sb.Append("<div class='hint'>");

                foreach (var p in hint.Descendants("ptxt")) sb.AppendFormat("<p>{0}</p>", p.Value);

                sb.Append("</div>");

            }



            bool foundTarget = false;

            foreach (var grp in doc.Descendants("stepgrp"))

            {

                var grpTitle = grp.Element("title");

                if (grpTitle != null) sb.AppendFormat("<h2>{0}</h2>", grpTitle.Value);



                foreach (var step in grp.Elements("procstep"))

                {

                    var title = step.Element("title");

                    string titleText = title != null ? title.Value : "";

                    bool isMatch = !string.IsNullOrEmpty(highlight) && titleText.IndexOf(highlight, StringComparison.OrdinalIgnoreCase) >= 0;



                    string targetAttr = "";

                    if (isMatch && !foundTarget)

                    {

                        targetAttr = " id='target'";

                        foundTarget = true;

                    }



                    sb.AppendFormat("<div class='procstep{0}'{1}>", isMatch ? " highlight" : "", targetAttr);

                    sb.AppendFormat("<div class='procstep-title'>{0}</div>", titleText);



                    foreach (var para in step.Descendants("para"))

                    {

                        foreach (var ptxt in para.Elements("ptxt"))

                        {

                            sb.AppendFormat("<p>{0}</p>", ptxt.Value);

                        }

                    }

                    sb.Append("</div>");

                }

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



    [STAThread]

    static void Main(string[] args)

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



        // Row 2: Tab Navigation Buttons

        Button btnTabVehProfile = new Button();

        btnTabVehProfile.Text = "🚗 VEHICLE PROFILE";

        btnTabVehProfile.SetBounds(10, 38, 155, 30);

        btnTabVehProfile.FlatStyle = FlatStyle.Flat;

        btnTabVehProfile.FlatAppearance.BorderSize = 1;

        btnTabVehProfile.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabVehProfile.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabVehProfile);



        Button btnTabFaultTrace = new Button();

        btnTabFaultTrace.Text = "🔍 FAULT TRACE (DTCs)";

        btnTabFaultTrace.SetBounds(170, 38, 175, 30);

        btnTabFaultTrace.FlatStyle = FlatStyle.Flat;

        btnTabFaultTrace.FlatAppearance.BorderSize = 1;

        btnTabFaultTrace.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabFaultTrace.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabFaultTrace);



        Button btnTabNetwork = new Button();

        btnTabNetwork.Text = "🌐 NETWORK";

        btnTabNetwork.SetBounds(350, 38, 120, 30);

        btnTabNetwork.FlatStyle = FlatStyle.Flat;

        btnTabNetwork.FlatAppearance.BorderSize = 1;

        btnTabNetwork.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabNetwork.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabNetwork);



        Button btnTabVehComm = new Button();

        btnTabVehComm.Text = "⚡ VEHICLE COMM";

        btnTabVehComm.SetBounds(475, 38, 150, 30);

        btnTabVehComm.FlatStyle = FlatStyle.Flat;

        btnTabVehComm.FlatAppearance.BorderSize = 1;

        btnTabVehComm.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabVehComm.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabVehComm);



        Button btnTabRefInfo = new Button();

        btnTabRefInfo.Text = "📖 REFERENCE INFO";

        btnTabRefInfo.SetBounds(630, 38, 160, 30);

        btnTabRefInfo.FlatStyle = FlatStyle.Flat;

        btnTabRefInfo.FlatAppearance.BorderSize = 1;

        btnTabRefInfo.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);

        btnTabRefInfo.Cursor = Cursors.Hand;

        topBar.Controls.Add(btnTabRefInfo);



        Button[] tabButtons = new Button[] { btnTabVehProfile, btnTabFaultTrace, btnTabNetwork, btnTabVehComm, btnTabRefInfo };



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

            }

            catch (Exception ex)

            {

                Console.WriteLine("[Launcher] SwitchTab " + methodName + " error: " + ex);

            }

        };



        bool vehCommInitialized = false;

        btnTabVehProfile.Click += (s, e) => { switchTab("_vehicleInfoButton_Click"); setActiveTabButton(btnTabVehProfile); };

        btnTabFaultTrace.Click += (s, e) => {

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

        btnTabNetwork.Click += (s, e) => { switchTab("_networkStatusButton_Click"); setActiveTabButton(btnTabNetwork); };

        btnTabVehComm.Click += (s, e) => {

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

                            ecuBar.SetBounds(400, 56, 480, 58);

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

        btnTabRefInfo.Click += (s, e) => { switchTab("_referenceInformationButton_Click"); setActiveTabButton(btnTabRefInfo); };



        Console.WriteLine("[Launcher] Initializing DiagnosticMainForm...");

        var diag = new DiagnosticMainForm();

        diag.Dock = DockStyle.Fill;



        Action<string, string> showDocument = (docId, title) =>

        {

            if (string.IsNullOrEmpty(docId)) return;

            try

            {

                Form docForm = new Form();

                docForm.Text = "VIDA Document: " + (!string.IsNullOrEmpty(title) ? title : docId);

                docForm.Size = new Size(1000, 750);

                docForm.StartPosition = FormStartPosition.CenterParent;

                WebBrowser wb = new WebBrowser();

                wb.Dock = DockStyle.Fill;

                wb.ScriptErrorsSuppressed = true;



                string html = RenderDocumentHtml(docId, lastSelectedParameter);

                wb.DocumentText = html;



                docForm.Controls.Add(wb);

                docForm.Show(mainForm);

            }

            catch (Exception exDoc)

            {

                Console.WriteLine("[Launcher] ShowDoc error: " + exDoc.Message);

            }

        };



        diag.NavigateIE += (id, type, subType, functionGroup) =>

        {

            Console.WriteLine("[Launcher] NavigateIE: id=" + id + ", type=" + type);

            showDocument(id, "Document " + id);

        };

        diag.NavigateIEPopupWindow += (id, type, subType, functionGroup) =>

        {

            Console.WriteLine("[Launcher] NavigateIEPopupWindow: id=" + id + ", type=" + type);

            showDocument(id, "Document " + id);

        };



        diag.RootUrl = "http://localhost/Vida/Diagnostics/";

        diag.LogPath = Path.Combine(vidaPath, @"System\Log\Diagnostics\");

        diag.CommToolName = TargetCommTool;

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



        mainForm.Load += (s, e) =>

        {

            try

            {

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

                                    MethodInfo initMethod = wf.GetType().GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);

                                    if (initMethod != null && initResField != null)

                                    {

                                        object initRes = initMethod.Invoke(wf, null);

                                        initResField.SetValue(vehInfoInstance, initRes);

                                        Console.WriteLine("[Launcher] Workflow Initialize result: " + initRes);

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

                            captureTab("Reference Info", "launcher_test_refinfo.png");

                            seqTimer.Stop();

                            Console.WriteLine("[Launcher] All tabs captured successfully! Exiting test...");

                            Application.Exit();

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

        Application.Run(mainForm);

    }

}

