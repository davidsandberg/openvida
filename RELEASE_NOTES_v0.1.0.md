# OpenVIDA v0.1.0 — Standalone Volvo Diagnostic & Repair Suite

Welcome to the initial release of **OpenVIDA**! 

OpenVIDA is a native, modern C# / .NET reimagining of **Volvo VIDA 2014D**, completely liberated from 32-bit virtual machines, Internet Explorer, legacy IsoView ActiveX plugins, and fragile JBoss/Java middleware. It brings dealer-level bidirectional vehicle diagnostics, live CAN bus telemetry streaming, ECU adaptation routines, and the complete Volvo factory technical library directly to modern 64-bit Windows 10 and Windows 11.

---

## 🌟 Key Highlights & Features

### 1. Native Windows 10 / 11 Diagnostics (No Virtual Machines)
* **Pure .NET Application**: Direct execution on modern 64-bit Windows without the latency and memory overhead of virtual machines.
* **Modern Vector & Web Rendering**: Replaced legacy IsoView ActiveX plugins with a custom high-performance GDI+ CGM vector renderer and integrated Microsoft Edge WebView2 for crystal-clear vector PDF schematics.
* **Direct SQL Express Architecture**: Interfaces directly with local SQL Server Express databases for instant sub-second procedure lookups and parts catalog searches.

### 2. Live Vehicle Telemetry & Advanced Service Functions
* **Multi-ECU Concurrent Parameter Polling**: Real-time reading and auto-scaling graphical plotting across ECM, TCM, BCM, CEM, and CCM modules.
* **Top-Bar Battery Voltage Monitoring**: Continuous Terminal 30 voltage display with safety color-coding to prevent battery exhaustion during diagnostic sessions.
* **DTC Management & "Clear All" Fault Erase**: Full fault tracing, freeze-frame inspection, and safe all-module DTC clearing with pre-flight voltage checks and dual-state delivery verification.
* **Bidirectional Component Actuations**: Command vehicle relays, solenoids, lights, wipers, and pumps directly with safety interlocks.
* **Factory Service Routines**: Run DPF soot regeneration, reset particle counters, adapt transmissions, calibrate throttles, and sweep DIM instrument gauges.

### 3. Complete OEM Technical Library
* **68,000+ Genuine Service Procedures**: Instant access to workshop manuals, step-by-step procedures, tightening torques, and diagnostic decision trees.
* **150,000+ Parts Catalog (EPC)**: Fast parts search, exploded component diagrams, Volvo part numbers, and 1-click clipboard copying.
* **54 Publication Wiring Diagrams (EWD)**: 680+ indexed electrical components across all 54 publications with fast search and high-DPI zoom.

### 4. Model Context Protocol (MCP) AI Bridge
* **First-of-its-Kind Automotive MCP Server**: Bundles `VidaMcp.exe` to connect AI agents (Claude Desktop, Google Antigravity, Cursor, Windsurf) directly to live vehicle telemetry and the offline Volvo database library via Named Pipe IPC.
* Enables closed-loop troubleshooting: AI assistants can read live DTCs, suggest OEM repair procedures, trace circuits in wiring diagrams, and lookup replacement part numbers in real time.

### 5. Turnkey Standalone Windows Installer (`OpenVidaSetup.exe`)
* **Bring Your Own Data**: 4-step setup wizard that extracts and configures data from `.7z` archives, `.iso` images, folders, or VMDKs.
* **Automated Environment Configuration**: Automates SQL Server Express TCP/IP configuration, mounts databases, and stages signed 64-bit Volvo DiCE USB drivers.
* **Clean Uninstaller**: Easily uninstall via Windows Settings or silent CLI (`--uninstall "C:\OpenVIDA" --quiet`).

---

## 📦 Download & Verification

| Asset | Description | Size | SHA-256 Checksum |
| :--- | :--- | :--- | :--- |
| **`OpenVidaSetup.exe`** | Standalone Turnkey Installer | ~5.87 MB | `ce9541e91d6b1ee5ee905a2992b59d0227b79581c3f738cb47d6e7882ed9a642` |

---

## 🚀 Quick Start Guide

1. Download **[`OpenVidaSetup.exe`](https://github.com/davidsandberg/openvida/releases/download/v0.1.0/OpenVidaSetup.exe)**.
2. Run `OpenVidaSetup.exe` (as Administrator).
3. The wizard will verify prerequisites (.NET Framework 4.x, WebView2, and SQL Server Express).
4. Point the installer to your VIDA 2014D data archive and (optionally) `Volvo_EWD_2014D.iso`.
5. Connect your Volvo DiCE hardware to your vehicle's OBD-II port (Ignition Position II) and laptop via USB.
6. Launch **OpenVIDA Diagnostics** from your Desktop shortcut!
