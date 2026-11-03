using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class Logger
{
    private static readonly object _syncLock = new object();
    private static string _logsDirectory = null;
    private static string _currentLogFile = null;
    private static bool _initialized = false;
    private static readonly Queue<string> _recentBuffer = new Queue<string>();
    private const int MaxBufferLines = 100;
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static TextWriter _originalConsoleOut = null;

    public static void Init()
    {
        if (_initialized) return;
        lock (_syncLock)
        {
            if (_initialized) return;
            try
            {
                string dir = GetLogsDirectory();
                _currentLogFile = Path.Combine(dir, "openvida.log");

                // Check file rotation on startup
                CheckRotation(_currentLogFile);

                // Install console interceptor to capture any Console.WriteLine from legacy components
                _originalConsoleOut = Console.Out;
                Console.SetOut(new MultiTextWriter(_originalConsoleOut));

                _initialized = true;

                Info("Core", "================================================================================");
                Info("Core", string.Format("OpenVIDA Diagnostic Session Started at {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                Info("Core", string.Format("Host OS: {0} ({1}), CLR: {2}, Process: {3}",
                    Environment.OSVersion,
                    Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
                    Environment.Version,
                    Environment.Is64BitProcess ? "64-bit" : "32-bit"));
                Info("Core", string.Format("Log file: {0}", _currentLogFile));
                Info("Core", "================================================================================");
            }
            catch (Exception ex)
            {
                try { Console.WriteLine("[Logger] Init error: " + ex.Message); } catch {}
            }
        }
    }

    public static string GetLogsDirectory()
    {
        if (_logsDirectory != null && Directory.Exists(_logsDirectory)) return _logsDirectory;

        try
        {
            string asmPath = Assembly.GetExecutingAssembly().Location;
            string baseDir = !string.IsNullOrEmpty(asmPath) ? Path.GetDirectoryName(asmPath) : AppDomain.CurrentDomain.BaseDirectory;

            // 1. If running from repository (e.g. c:\Users\david\git\vida\...), find repo root
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src")) && Directory.Exists(Path.Combine(dir.FullName, "assets")))
                {
                    string repoLogs = Path.Combine(dir.FullName, "logs");
                    if (!Directory.Exists(repoLogs)) Directory.CreateDirectory(repoLogs);
                    _logsDirectory = repoLogs;
                    return repoLogs;
                }
                dir = dir.Parent;
            }

            // 2. In production installed mode under C:\OpenVIDA
            if (baseDir.IndexOf(@"\OpenVIDA", StringComparison.OrdinalIgnoreCase) >= 0 || Directory.Exists(@"C:\OpenVIDA\Diagnostics"))
            {
                string prodLogs = @"C:\OpenVIDA\logs";
                if (!Directory.Exists(prodLogs)) Directory.CreateDirectory(prodLogs);
                _logsDirectory = prodLogs;
                return prodLogs;
            }

            // 3. Fallback: logs next to executable
            string candidate = Path.Combine(baseDir, "logs");
            if (!Directory.Exists(candidate)) Directory.CreateDirectory(candidate);
            _logsDirectory = candidate;
            return candidate;
        }
        catch
        {
            string fallback = Path.Combine(Path.GetTempPath(), "OpenVIDA_Logs");
            if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
            _logsDirectory = fallback;
            return fallback;
        }
    }

    public static void OpenLogsFolder()
    {
        try
        {
            string dir = GetLogsDirectory();
            Process.Start("explorer.exe", dir);
        }
        catch (Exception ex)
        {
            try { Console.WriteLine("[Logger] Failed to open logs folder: " + ex.Message); } catch {}
        }
    }

    public static void Info(string component, string message)
    {
        WriteEntry("INFO", component, message);
    }

    public static void Warn(string component, string message)
    {
        WriteEntry("WARN", component, message);
    }

    public static void Debug(string component, string message)
    {
        WriteEntry("DEBUG", component, message);
    }

    public static void Error(string component, string message, Exception ex = null)
    {
        string fullMsg = message;
        if (ex != null)
        {
            fullMsg += "\nException: " + ex.GetType().FullName + ": " + ex.Message + "\nStackTrace: " + ex.StackTrace;
            if (ex.InnerException != null)
            {
                fullMsg += "\nInnerException: " + ex.InnerException.GetType().FullName + ": " + ex.InnerException.Message + "\n" + ex.InnerException.StackTrace;
            }
        }
        WriteEntry("ERROR", component, fullMsg);
    }

    public static void WriteRawLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        // Extract component tag if present like "[Launcher] ..."
        string component = "App";
        string msg = line;
        if (line.StartsWith("[") && line.IndexOf("]") > 1)
        {
            int closeIdx = line.IndexOf("]");
            component = line.Substring(1, closeIdx - 1).Trim();
            msg = line.Substring(closeIdx + 1).Trim();
        }

        string level = "INFO";
        if (msg.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            level = "ERROR";
        }
        else if (msg.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("warn", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            level = "WARN";
        }

        WriteEntry(level, component, msg, false);
    }

    private static void WriteEntry(string level, string component, string message, bool writeToConsole = true)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string formatted = string.Format("[{0}] [{1,-5}] [{2}] {3}", timestamp, level, component, message);

        lock (_syncLock)
        {
            // Update rolling recent buffer for crash dumps
            _recentBuffer.Enqueue(formatted);
            while (_recentBuffer.Count > MaxBufferLines)
            {
                _recentBuffer.Dequeue();
            }

            // Write to console if requested and not from console interceptor
            if (writeToConsole && _originalConsoleOut != null)
            {
                try { _originalConsoleOut.WriteLine(formatted); } catch {}
            }

            // Write to file
            try
            {
                string dir = GetLogsDirectory();
                string logFile = _currentLogFile ?? Path.Combine(dir, "openvida.log");

                CheckRotation(logFile);
                File.AppendAllText(logFile, formatted + Environment.NewLine, Encoding.UTF8);
            }
            catch {}
        }
    }

    private static void CheckRotation(string logPath)
    {
        try
        {
            if (string.IsNullOrEmpty(logPath)) return;
            FileInfo fi = new FileInfo(logPath);
            if (fi.Exists && fi.Length > MaxLogSizeBytes)
            {
                string dir = fi.DirectoryName;
                string baseName = Path.GetFileNameWithoutExtension(logPath);
                string ext = Path.GetExtension(logPath);

                string log3 = Path.Combine(dir, baseName + ".3" + ext);
                string log2 = Path.Combine(dir, baseName + ".2" + ext);
                string log1 = Path.Combine(dir, baseName + ".1" + ext);

                if (File.Exists(log3)) File.Delete(log3);
                if (File.Exists(log2)) File.Move(log2, log3);
                if (File.Exists(log1)) File.Move(log1, log2);
                File.Move(logPath, log1);
            }
        }
        catch {}
    }

    public static string GetRecentLogTail(int lineCount = 50)
    {
        lock (_syncLock)
        {
            string[] items = _recentBuffer.ToArray();
            int start = Math.Max(0, items.Length - lineCount);
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < items.Length; i++)
            {
                sb.AppendLine(items[i]);
            }
            return sb.ToString();
        }
    }

    public static string LogCrash(Exception ex, string context = null)
    {
        string crashPath = null;
        try
        {
            string logsDir = GetLogsDirectory();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            crashPath = Path.Combine(logsDir, string.Format("crash_{0}.log", timestamp));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("                      OPENVIDA POST-MORTEM CRASH DUMP                          ");
            sb.AppendLine("================================================================================");
            sb.AppendLine(string.Format("Timestamp:        {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            sb.AppendLine(string.Format("Context:          {0}", context ?? "Unhandled Exception"));
            sb.AppendLine(string.Format("OS Version:       {0} ({1})", Environment.OSVersion, Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            sb.AppendLine(string.Format("Process:          VidaLauncher.exe ({0})", Environment.Is64BitProcess ? "64-bit" : "32-bit"));
            sb.AppendLine(string.Format("CLR Version:      {0}", Environment.Version));
            sb.AppendLine(string.Format("Machine Name:     {0}", Environment.MachineName));
            sb.AppendLine(string.Format("Working Set:      {0:N0} bytes", Environment.WorkingSet));
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("EXCEPTION DETAILS:");
            if (ex != null)
            {
                sb.AppendLine(string.Format("Type:       {0}", ex.GetType().FullName));
                sb.AppendLine(string.Format("Message:    {0}", ex.Message));
                sb.AppendLine(string.Format("Source:     {0}", ex.Source));
                sb.AppendLine("StackTrace:");
                sb.AppendLine(ex.StackTrace ?? "(No stack trace available)");

                Exception inner = ex.InnerException;
                int innerIdx = 1;
                while (inner != null)
                {
                    sb.AppendLine(string.Format("\n--- Inner Exception #{0} ---", innerIdx++));
                    sb.AppendLine(string.Format("Type:       {0}", inner.GetType().FullName));
                    sb.AppendLine(string.Format("Message:    {0}", inner.Message));
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(inner.StackTrace ?? "(No inner stack trace available)");
                    inner = inner.InnerException;
                }
            }
            else
            {
                sb.AppendLine("No exception object provided.");
            }
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("LOADED RELEVANT ASSEMBLIES:");
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = asm.GetName().Name;
                    if (name.IndexOf("Vida", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Diag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("IsoView", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine(string.Format("  - {0} ({1})", asm.FullName, asm.Location));
                    }
                }
            }
            catch {}
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("RECENT LOG TAIL (Last 50 entries):");
            sb.AppendLine(GetRecentLogTail(50));
            sb.AppendLine("================================================================================");

            File.WriteAllText(crashPath, sb.ToString(), Encoding.UTF8);

            Error("CRASH", string.Format("Post-mortem crash dump saved to {0}", crashPath));
        }
        catch (Exception dumpEx)
        {
            try { Console.WriteLine("[Logger] Failed writing crash dump: " + dumpEx.Message); } catch {}
        }
        return crashPath;
    }

    public static void ShowCrashDialog(Exception ex, string context = null)
    {
        string crashFile = LogCrash(ex, context);
        string msg = string.Format(
            "An unhandled error occurred in OpenVIDA:\n\n{0}\n\n" +
            "A detailed diagnostic crash dump has been saved to:\n{1}\n\n" +
            "Would you like to open the logs folder to inspect or attach this file when reporting the issue?",
            ex != null ? ex.Message : "Unknown error",
            crashFile ?? Path.Combine(GetLogsDirectory(), "openvida.log")
        );

        DialogResult res = MessageBox.Show(msg, "OpenVIDA Error - Post-Mortem Diagnostic", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
        if (res == DialogResult.Yes)
        {
            OpenLogsFolder();
        }
    }

    private class MultiTextWriter : TextWriter
    {
        private readonly TextWriter _consoleOut;

        public MultiTextWriter(TextWriter consoleOut)
        {
            _consoleOut = consoleOut;
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void WriteLine(string value)
        {
            if (_consoleOut != null)
            {
                try { _consoleOut.WriteLine(value); } catch {}
            }
            Logger.WriteRawLog(value);
        }

        public override void Write(string value)
        {
            if (_consoleOut != null)
            {
                try { _consoleOut.Write(value); } catch {}
            }
        }
    }
}
