using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public class CommToolItem
{
    public string Name { get; set; }
    public string KeyName { get; set; }
    public string Vendor { get; set; }
    public string FunctionLibrary { get; set; }
    public bool IsDice { get; set; }
    public string DisplayName { get; set; }

    public override string ToString()
    {
        return DisplayName ?? Name;
    }
}

public static class CommToolManager
{
    private static readonly object _lock = new object();
    private static List<CommToolItem> _availableTools = new List<CommToolItem>();
    private static CommToolItem _currentTool = null;
    private static bool _initialized = false;

    public static event Action OnCommToolChanged;

    public static List<CommToolItem> AvailableTools
    {
        get
        {
            EnsureInitialized();
            lock (_lock)
            {
                return new List<CommToolItem>(_availableTools);
            }
        }
    }

    public static CommToolItem CurrentTool
    {
        get
        {
            EnsureInitialized();
            lock (_lock)
            {
                if (_currentTool == null && _availableTools.Count > 0)
                {
                    _currentTool = _availableTools[0];
                }
                if (_currentTool == null)
                {
                    _currentTool = CreateFallbackDiceTool();
                }
                return _currentTool;
            }
        }
    }

    public static string CurrentToolName
    {
        get { return CurrentTool.Name; }
    }

    public static bool IsCurrentToolDice
    {
        get { return CurrentTool.IsDice; }
    }

    public static string CurrentDisplayName
    {
        get { return CurrentTool.DisplayName; }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    RescanToolsInternal();
                    _initialized = true;
                }
            }
        }
    }

    public static void RescanTools()
    {
        lock (_lock)
        {
            RescanToolsInternal();
        }
    }

    private static void RescanToolsInternal()
    {
        var foundTools = new List<CommToolItem>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Method A: Query Util.DLL RegistryScanner if available
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var scannerType = asm.GetType("Vcc.Vida.DiagSwdl.Util.J2534.RegistryScanner");
                if (scannerType != null)
                {
                    var m = scannerType.GetMethod("ReadJ2534DeviceInfo", BindingFlags.Public | BindingFlags.Static);
                    if (m != null)
                    {
                        var ht = m.Invoke(null, null) as Hashtable;
                        if (ht != null)
                        {
                            foreach (DictionaryEntry de in ht)
                            {
                                string keyName = de.Key as string;
                                object dev = de.Value;
                                if (dev == null) continue;

                                Type dt = dev.GetType();
                                string name = dt.GetProperty("Name") != null ? (string)dt.GetProperty("Name").GetValue(dev, null) : keyName;
                                if (string.IsNullOrEmpty(name)) name = keyName;
                                string vendor = dt.GetProperty("Vendor") != null ? (string)dt.GetProperty("Vendor").GetValue(dev, null) : "";
                                string dll = dt.GetProperty("FunctionLibrary") != null ? (string)dt.GetProperty("FunctionLibrary").GetValue(dev, null) : "";
                                bool isDice = false;
                                var pDice = dt.GetProperty("DICECompatible");
                                if (pDice != null)
                                {
                                    try { isDice = Convert.ToBoolean(pDice.GetValue(dev, null)); } catch {}
                                }
                                if (!isDice && (name.IndexOf("dice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               (keyName != null && keyName.IndexOf("dice", StringComparison.OrdinalIgnoreCase) >= 0)))
                                {
                                    isDice = true;
                                }

                                if (!string.IsNullOrEmpty(name) && !seenNames.Contains(name))
                                {
                                    seenNames.Add(name);
                                    string disp = BuildDisplayName(name, vendor, isDice);
                                    foundTools.Add(new CommToolItem
                                    {
                                        Name = name,
                                        KeyName = keyName ?? name,
                                        Vendor = vendor,
                                        FunctionLibrary = dll,
                                        IsDice = isDice,
                                        DisplayName = disp
                                    });
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }
        catch {}

        // Method B: Direct Windows Registry scan (both 32-bit WOW64 and standard)
        try
        {
            var regViews = new RegistryView[] { RegistryView.Registry32, RegistryView.Default };
            foreach (var view in regViews)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var ptKey = baseKey.OpenSubKey(@"SOFTWARE\PassThruSupport.04.04"))
                    {
                        if (ptKey == null) continue;
                        foreach (var subName in ptKey.GetSubKeyNames())
                        {
                            using (var subKey = ptKey.OpenSubKey(subName))
                            {
                                if (subKey == null) continue;
                                string name = subKey.GetValue("Name") as string;
                                if (string.IsNullOrEmpty(name)) name = subName;
                                if (seenNames.Contains(name)) continue;

                                string vendor = subKey.GetValue("Vendor") as string ?? "";
                                string dll = subKey.GetValue("FunctionLibrary") as string ?? "";
                                object diceComp = subKey.GetValue("DiCECompatible");
                                bool isDice = false;
                                if (diceComp != null)
                                {
                                    try { isDice = Convert.ToInt32(diceComp) == 1; } catch {}
                                }
                                if (!isDice && (name.IndexOf("dice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               subName.IndexOf("dice", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    isDice = true;
                                }

                                seenNames.Add(name);
                                string disp = BuildDisplayName(name, vendor, isDice);
                                foundTools.Add(new CommToolItem
                                {
                                    Name = name,
                                    KeyName = subName,
                                    Vendor = vendor,
                                    FunctionLibrary = dll,
                                    IsDice = isDice,
                                    DisplayName = disp
                                });
                            }
                        }
                    }
                }
                catch {}
            }
        }
        catch {}

        // Guarantee at least one DiCE fallback
        if (!seenNames.Contains("DiCE-206751"))
        {
            bool hasAnyDice = false;
            foreach (var t in foundTools)
            {
                if (t.IsDice) { hasAnyDice = true; break; }
            }
            if (!hasAnyDice)
            {
                foundTools.Insert(0, CreateFallbackDiceTool());
            }
        }

        _availableTools = foundTools;

        // Restore saved preference if possible
        string savedToolName = LoadSavedToolPreference();
        CommToolItem matched = null;
        if (!string.IsNullOrEmpty(savedToolName))
        {
            foreach (var t in _availableTools)
            {
                if (string.Equals(t.Name, savedToolName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.KeyName, savedToolName, StringComparison.OrdinalIgnoreCase))
                {
                    matched = t;
                    break;
                }
            }
        }

        if (matched != null)
        {
            _currentTool = matched;
        }
        else if (_currentTool != null)
        {
            // Keep current if still present in list
            CommToolItem existing = null;
            foreach (var t in _availableTools)
            {
                if (string.Equals(t.Name, _currentTool.Name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = t;
                    break;
                }
            }
            _currentTool = existing ?? (_availableTools.Count > 0 ? _availableTools[0] : CreateFallbackDiceTool());
        }
        else
        {
            _currentTool = _availableTools.Count > 0 ? _availableTools[0] : CreateFallbackDiceTool();
        }
    }

    private static string BuildDisplayName(string name, string vendor, bool isDice)
    {
        string v = !string.IsNullOrEmpty(vendor) ? vendor.Trim() : "";
        if (!string.IsNullOrEmpty(v))
        {
            return string.Format("{0} ({1}) [{2}]", name, v, isDice ? "DiCE" : "J2534");
        }
        return string.Format("{0} [{1}]", name, isDice ? "DiCE" : "J2534");
    }

    private static CommToolItem CreateFallbackDiceTool()
    {
        return new CommToolItem
        {
            Name = "DiCE-206751",
            KeyName = "SETEK - DiCE",
            Vendor = "SETEK",
            FunctionLibrary = @"C:\Program Files (x86)\DiCE\Tools\TSDiCE32.dll",
            IsDice = true,
            DisplayName = "DiCE-206751 (SETEK) [DiCE]"
        };
    }

    public static bool SelectToolByName(string name)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(name)) return false;

        lock (_lock)
        {
            foreach (var t in _availableTools)
            {
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return SelectTool(t);
                }
            }
        }
        return false;
    }

    public static bool SelectTool(CommToolItem tool)
    {
        if (tool == null) return false;

        lock (_lock)
        {
            _currentTool = tool;
            SaveToolPreference(tool.Name);
        }

        try
        {
            if (OnCommToolChanged != null)
            {
                OnCommToolChanged();
            }
        }
        catch {}

        return true;
    }

    private static string GetSettingsFilePath()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFile = Path.Combine(baseDir, "openvida_settings.json");
            return localFile;
        }
        catch
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "OpenVIDA", "openvida_settings.json");
        }
    }

    private static string LoadSavedToolPreference()
    {
        try
        {
            string path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var match = Regex.Match(json, @"""selected_comm_tool""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch {}

        // Fallback to local appdata if not in base dir
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string path = Path.Combine(appData, "OpenVIDA", "openvida_settings.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var match = Regex.Match(json, @"""selected_comm_tool""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch {}

        return null;
    }

    private static void SaveToolPreference(string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;

        string json = string.Format("{{\r\n  \"selected_comm_tool\": \"{0}\",\r\n  \"last_updated\": \"{1:O}\"\r\n}}",
            toolName.Replace("\\", "\\\\").Replace("\"", "\\\""), DateTime.UtcNow);

        try
        {
            string path = GetSettingsFilePath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json, Encoding.UTF8);
            return;
        }
        catch {}

        // Fallback to local appdata on write permission failure
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "OpenVIDA");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "openvida_settings.json"), json, Encoding.UTF8);
        }
        catch {}
    }
}
