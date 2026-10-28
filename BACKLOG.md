# VIDA Development Backlog & Future Enhancements

This document tracks planned features, pending fixes, and architectural improvements for the VIDA standalone launcher and tooling.

---

## 📋 Open Items

### 1. VidaLauncher Standalone Installer & Dependency Bootstrapper
- **Summary**: Create a streamlined Windows installer (e.g., Inno Setup, WiX, or lightweight PowerShell/C# bootstrapper) to distribute and install `VidaLauncher` and its core dependencies onto fresh machines.
- **Key Requirements**:
  - Automatically install or package required runtimes:
    - .NET Framework (4.x / 4.8)
    - Microsoft Edge WebView2 Runtime (evergreen bootstrapper or bundled fixed version)
    - DiCE USB / J2534 drivers
  - **Dynamic asset downloading**:
    - Large databases and assets (SQL Server database files `*.mdf`/`*.ldf`, EWD PDF archives, JBoss deployment payload) can be fetched during install from predefined remote download links (e.g., Google Drive, Cloudflare R2, or GitHub releases) rather than bundling multi-gigabyte payloads in the installer binary.
    - Checksum validation (SHA-256) of downloaded files.
    - Automatic extraction and directory layout configuration under standard target paths (e.g. `C:\VIDA` or user-chosen directory).
  - Setup shortcut creation for Desktop and Start Menu pointing directly to `VidaLauncher.exe`.
  - Service/background daemon manager (optional prompt to set up JBoss service or attach local DB instances automatically).

---

### 2. EWD PDF Viewer In-Document Navigation
- **Summary**: Fix in-document direct page jumping from the component list in the Wiring Diagrams (EWD) tab.
- **Issue Details**:
  - The embedded WebView2 / Edge PDF viewer ignores `#page=N` fragment positioning on reload/re-navigation, defaulting back to the top of the document (page 1).
- **Proposed Solutions**:
  - Embed a lightweight [PDF.js](https://mozilla.github.io/pdf.js/) viewer instance within WebView2 with full programmatic JavaScript API control (`PDFViewerApplication.page = N`). This provides instant, flicker-free jumps without reloading or re-rendering the whole document.
  - Alternatively, evaluate message posting / virtual host mapping to control Chromium's PDF plugin without full URL round-trips.

---

## 📌 Prior Completed Highlights
- Standalone `VidaLauncher.exe` bypassing legacy browser dependencies.
- Native WebView2 integration for Electronic Wiring Diagrams (EWD) and Electronic Parts Catalog (EPC).
- Diagnostic session manager and J2534 DiCE communication layer.
- Offline workshop manual document browser.
