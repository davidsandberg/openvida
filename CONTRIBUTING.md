# Contributing to OpenVIDA

Thank you for your interest in contributing to **OpenVIDA**! 

OpenVIDA is an open-source, community-driven effort to liberate Volvo VIDA 2014D from obsolete virtual machines, Internet Explorer, and bloated Java/JBoss middleware—transforming it into a high-performance, standalone, native C# / .NET diagnostic suite with modern Edge WebView2 rendering and an AI Model Context Protocol (MCP) bridge.

---

## 🚧 Project Status: Early Stage Development

> [!IMPORTANT]
> **OpenVIDA is currently in active Community Technical Preview (Alpha).**  
> We have achieved a working standalone architecture that connects to real vehicles via DiCE, reads and clears DTCs, streams live multi-ECU parameters, displays wiring diagrams, and interfaces with AI agents.
> 
> **However, help is very much needed to make it solid, robust, and mature.** Because legacy VIDA was a massive enterprise software suite spanning 15 years of Volvo vehicle architectures, many vehicle platform configurations (P1, P2, P3, early SPA), unusual ECU combinations, and hardware edge cases remain untested.

Whether you are a software developer, automotive reverse engineer, independent Volvo technician, or car enthusiast with an OBD-II adapter, your feedback, bug reports, logs, and pull requests are essential!

---

## 🔌 Hardware Support & DiCE Alternatives (J2534 PassThru)

OpenVIDA interfaces with vehicles using the industry-standard **SAE J2534 (PassThru)** API.

While the original Volvo **DiCE** (Diagnostic Communication Equipment) unit is our primary benchmark hardware, **we want OpenVIDA to support alternative J2534-compliant adapters**. Many Volvo owners do not own an authentic or clone DiCE, but have access to other popular J2534 hardware:

- **Tactrix OpenPort 2.0**
- **Scanmatik 2 Pro / SM2**
- **VXDIAG VCX Nano (Volvo / Multi-tool edition)**
- **Mongoose JLR / Pro**
- **Godiag GD101 / OBDX Pro / DrewTech Mongoose**

### How to Help with Hardware Testing
1. Connect your J2534 device to your PC via USB.
2. Verify that your device driver installed its registry entry under:
   ```text
   HKEY_LOCAL_MACHINE\SOFTWARE\PassThruSupport.04.04\<DeviceName>
   ```
3. Run OpenVIDA and test communication with your vehicle.
4. Report your findings using our **[Hardware Compatibility Report Template](.github/ISSUE_TEMPLATE/hardware_compatibility.md)**, attaching your `logs/openvida.log`.

---

## 🤖 Using AI Coding Assistants for Reports & Contributions

We actively encourage and embrace the use of AI coding assistants (such as **Google Antigravity**, **Claude**, **ChatGPT**, **GitHub Copilot**, etc.) when exploring the codebase, debugging issues, filing trouble reports, or preparing pull requests.

### When Filing Bug Reports
If OpenVIDA throws an error or fails to connect:
1. Locate your log file (see below).
2. Feed your `openvida.log` and relevant code snippets to your AI assistant.
3. Prompt your assistant:
   > *"I encountered this error in OpenVIDA on a [Year/Model] Volvo. Here is the relevant log section from `openvida.log`: `[paste log]`. What is the root cause in the OpenVIDA architecture, and what changes might resolve it?"*
4. Include both the **raw log file** and any **AI diagnosis or proposed fix** in your GitHub issue!

### When Writing Pull Requests
AI coding assistants can help write C# code, design WinForms controls, and optimize database queries. Just ensure the assistant follows our **Technical Constraints** below.

---

## 📋 Diagnostic Logs: Where to Find Them

Whenever you experience a crash, unexpected behavior, or communication glitch, **always attach your log file**. OpenVIDA features built-in post-mortem logging that records every diagnostic event, hardware probe, and database transaction.

### How to Access Logs:
- **1-Click in Application**: In the top navigation toolbar, click the **`[📋 Logs]`** button. Windows File Explorer will open directly to your log directory.
- **File System Locations**:
  - **Repository / Development**: `<repoRoot>\logs\openvida.log`
  - **Installed Standalone**: `C:\OpenVIDA\logs\openvida.log`
  - **Crash Dumps**: If an unhandled crash occurs, an emergency snapshot is saved to `logs/crash_YYYYMMDD_HHMMSS.log` containing the exception stack trace, machine specs, and recent log tail.

---

## 🛠️ Development & Coding Standards

Before opening a Pull Request, please ensure your changes adhere to these architectural guidelines:

### 1. Language Level & Target Framework
- **Target**: `.NET Framework 4.0` / C# 4.0.
- **Compiler**: Built using `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`.
- **Syntax Constraint**: Do **not** use C# 6.0+ language features (e.g., string interpolation `$"..."`, null-conditional operator `?.`, or expression-bodied members `=>`). Use `string.Format(...)` and explicit null checks instead.

### 2. Architecture & Modular Structure
- All active application code is located in the top-level [`src/`](file:///c:/Users/david/git/vida/src) folder:
  - `src/Core/`: Program entry point, IPC Named Pipe server, health probes, and `Logger.cs`.
  - `src/Diagnostics/`: Vehicle readout handling, diagnostic managers, and `ReadoutExporter.cs`.
  - `src/Hardware/`: J2534 DiCE communication, live voltage polling, and connection state.
  - `src/Documents/`: Repair manuals, service procedures, and HTML/WebView2 renderers.
  - `src/Tabs/`: Tab page implementations (Vehicle Comm, Wiring Diagrams, Manuals, EPC).
  - `src/Installer/`: Standalone self-contained Windows setup engine (`OpenVidaSetup.cs`).
- **No Java / JBoss Dependencies**: Never reintroduce dependencies on JBoss, Java runtimes, or external web services. OpenVIDA runs 100% locally.

### 3. Unified Logging
- Do not use raw `Console.WriteLine` for error logging.
- Use the central logger:
  ```csharp
  Logger.Info("Component", "Informational status message");
  Logger.Warn("Component", "Recoverable anomaly detected");
  Logger.Error("Component", "Failed to communicate with ECU", ex);
  ```

### 4. Build & Verify Locally
Always verify that your code compiles cleanly and passes automated tests:
```cmd
# 1. Compile all binaries (Diagnostics, MCP Bridge, Installer)
build.bat

# 2. Run automated test sequence
VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\VidaLauncher.exe /test
```

---

## 📬 Submitting a Pull Request

1. **Fork** the repository and create a feature branch (`git checkout -b feature/tactrix-j2534-support`).
2. Make your changes adhering to the guidelines above.
3. Run `build.bat` to confirm a 0-error build.
4. Commit your changes with clear, descriptive commit messages.
5. Push to your fork and submit a **Pull Request** detailing:
   - What problem is solved or feature is added.
   - What vehicle platform and hardware were tested.
   - Any log output or screenshots demonstrating the change.

Thank you for helping make OpenVIDA the premier open diagnostic platform for Volvo owners!
