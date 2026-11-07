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

                if (typeProp != null && typeProp.CanWrite) typeProp.SetValue(obj, CurrentCommToolEnumValue ?? diceEnum, null);

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

        SetFieldOrProp(s, "commToolType", CurrentCommToolTypeInt);

        SetFieldOrProp(s, "CommToolType", CurrentCommToolTypeInt);

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

}
