using System;
using System.Runtime.InteropServices;
using System.Text;

class TestJ2534
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int PassThruOpenDelegate(IntPtr pName, out uint pDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int PassThruCloseDelegate(uint deviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int PassThruReadVersionDelegate(uint deviceId, StringBuilder fwVer, StringBuilder dllVer, StringBuilder apiVer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int PassThruGetLastErrorDelegate(StringBuilder pErrorDescription);

    static void Main()
    {
        string dllPath = @"C:\Program Files (x86)\DiCE\Tools\TSDiCE32.dll";
        Console.WriteLine("Loading J2534 DLL: " + dllPath);
        IntPtr hModule = LoadLibrary(dllPath);
        if (hModule == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load DLL! Win32 Error: " + Marshal.GetLastWin32Error());
            return;
        }

        IntPtr pOpen = GetProcAddress(hModule, "PassThruOpen");
        IntPtr pClose = GetProcAddress(hModule, "PassThruClose");
        IntPtr pReadVersion = GetProcAddress(hModule, "PassThruReadVersion");
        IntPtr pGetLastError = GetProcAddress(hModule, "PassThruGetLastError");

        if (pOpen == IntPtr.Zero || pClose == IntPtr.Zero)
        {
            Console.WriteLine("Failed to resolve PassThruOpen or PassThruClose exports!");
            return;
        }

        var PassThruOpen = (PassThruOpenDelegate)Marshal.GetDelegateForFunctionPointer(pOpen, typeof(PassThruOpenDelegate));
        var PassThruClose = (PassThruCloseDelegate)Marshal.GetDelegateForFunctionPointer(pClose, typeof(PassThruCloseDelegate));
        var PassThruReadVersion = (PassThruReadVersionDelegate)Marshal.GetDelegateForFunctionPointer(pReadVersion, typeof(PassThruReadVersionDelegate));
        var PassThruGetLastError = (PassThruGetLastErrorDelegate)Marshal.GetDelegateForFunctionPointer(pGetLastError, typeof(PassThruGetLastErrorDelegate));

        Console.WriteLine("Calling PassThruOpen()...");
        uint deviceId = 0;
        int status = PassThruOpen(IntPtr.Zero, out deviceId);
        Console.WriteLine("PassThruOpen return status: " + status + " (0=STATUS_NOERROR)");

        if (status != 0)
        {
            var errBuf = new StringBuilder(256);
            PassThruGetLastError(errBuf);
            Console.WriteLine("Error description: " + errBuf.ToString());
            return;
        }

        Console.WriteLine("Successfully opened DiCE! Device ID = " + deviceId);

        var fw = new StringBuilder(256);
        var dll = new StringBuilder(256);
        var api = new StringBuilder(256);
        int verStatus = PassThruReadVersion(deviceId, fw, dll, api);
        Console.WriteLine("PassThruReadVersion status: " + verStatus);
        Console.WriteLine("  Firmware Version : " + fw.ToString());
        Console.WriteLine("  DLL Version      : " + dll.ToString());
        Console.WriteLine("  API Version      : " + api.ToString());

        Console.WriteLine("Closing DiCE connection...");
        PassThruClose(deviceId);
        Console.WriteLine("PassThruClose completed. Device test SUCCESSFUL!");
    }
}
