# Volvo VIDA Native Standalone — Implementation & Test Plan

> [!NOTE]
> All core databases (`BaseData`, `EPC`, `CarCom`, `DiagSwdlRepository`, `DiagSwdlSession`, `imagerepository`, `servicerep_en-US`) and .NET business/diagnostic engines are installed and running locally on Windows 11 without any virtual machines or Internet Explorer.

---

## 1. Executive Summary: The Five Completed Core Pillars

| Pillar | Status | Core Verification & Capabilities |
| :--- | :---: | :--- |
| **1. Live Sensor Streams & Parameters** | ✅ **Completed & Verified** | Live CAN parameter streaming directly from vehicle ECUs (ECM, BCM, CEM) via DiCE J2534. Multi-ECU watchlist, real-time decoding, **automatic continuous graph auto-scaling** with 20% visual headroom, and convenient watchlist removal (<kbd>Delete</kbd> key, context menu, top bar action buttons). |
| **2. Bidirectional Actuators & Activations** | ✅ **Completed & Verified** | Live bidirectional component actuation over DiCE J2534 on real vehicle. Verified tests: **fuel flap solenoid**, **front/rear washer sprinklers**, **brake/stop lights**, and **wiper sweeps**. Features sub-tab switching between **Parameters**, **Activations**, and **Advanced** (Special Tools). |
| **3. Service & Repair Manuals** | ✅ **Completed & Verified** | 68,000+ official repair manuals loaded from `servicerep_en-US`. Full hierarchical tree organized by genuine Volvo Function Groups with clean engineering titles, embedded technical diagrams, and clickable cross-references. |
| **4. Electronic Parts Catalog (EPC)** | ✅ **Completed & Verified** | 150,436 parts indexed from the `EPC` database. Features model/engine filtering (`V70 08- D5204T2 (2.0D)`), 384 assemblies across Function Groups 0–9, interactive parts grid with Pos callouts, Part Numbers, Descriptions, Quantities, fitment notes, and 1-click clipboard copy. |
| **5. Electronic Wiring Diagrams (EWD)** | ✅ **Completed & Verified** *(Navigation polish remaining)* | Extracted official `Volvo_EWD_2014D.iso` integrated into the standalone app. Full 2011 V70 publication (`39180202`) with 688 component index, instant keyword search, and in-app high-DPI vector PDF rendering via **Microsoft Edge WebView2**. *(Remaining polish: deep-linking component clicks directly to exact schematic page/coordinates in WebView2).* |

---

## 2. Detailed Component Implementation Status

| Component | Status | Verification & Capabilities |
| :--- | :--- | :--- |
| **DiCE USB Hardware Communication** | ✅ **Working** | Communicates directly over native USB (`dice_usb.dll` / J2534) with `DiCE-206751` to car OBD-II port. |
| **Bidirectional Component Actuation** | ✅ **Working** | Triggered real vehicle actuators via DiCE: fuel flap solenoid, front & rear washer sprinklers, stop lights, wipers. Safety logging and worker hooks verified. |
| **VehComm Sub-Tab Switcher** | ✅ **Working** | Integrated 1-click switching inside Vehicle Communication between **📊 Parameters**, **⚡ Activations**, and **🔧 Advanced** (Special Tools / Calibrations). |
| **Live Vehicle Sensor CAN Streaming** | ✅ **Working** | Real-time parameter streaming from vehicle ECUs. Decodes live CAN frames across ECM, BCM, and CEM simultaneously without mock interference. |
| **Parameter Watchlist Management & Removal** | ✅ **Working** | Three intuitive ways to remove parameters: top-bar buttons (`[❌ Remove Item]`, `[🗑️ Clear All]`), keyboard <kbd>Delete</kbd> key shortcut, and right-click context menu. |
| **Automatic Graph Auto-Scaling & Range Recalculation** | ✅ **Working** | Auto-scale dynamically expands viewport on incoming values; continuous background range synchronizer applies 20% visual headroom padding so no curve clips outside the chart. |
| **Opt-in Mock Simulation** | ✅ **Working** | Mock data simulation is completely opt-in via `--mock` / `/mock` CLI flags or an interactive "Mock Data" checkbox on the Vehicle Comm tab. Unchecked by default for live diagnostics. |
| **Live Battery Voltage & Key Position** | ✅ **Working** | Synchronously and asynchronously queries DiCE J2534 for battery voltage (`12.3 V` / `12.25V`) and key position (`Pos 0`, `Pos I`, `Pos II`, `Pos III`). |
| **Multi-Tab Battery Voltage Normalization** | ✅ **Working** | Solved locale-specific comma-delimited numeric bug (`125.0V` display on Swedish systems). Synchronizes live voltage correctly across all four diagnostic tabs with automated `TextChanged` guards. |
| **Instant Launch & In-App DiCE Connect** | ✅ **Working** | Eliminated the floating pre-launch blank modal progress bar. Main application loads immediately, with background DiCE status polling and a dedicated `[⚡ Connect / Status]` button in the top navigation bar. |
| **Vehicle Identification & Decoding** | ✅ **Working** | Decodes VIN `YV1BW5251B1189886`, MY 2011 V70, D5204T2 diesel, M66 manual, profile `0b00c8af84add183`. |
| **All-ECU DTC Readout & Storage** | ✅ **Working** | Extracted and stored all 13 active DTCs (`ECM`, `CEM`, `HCM`, `FSM`) with freeze-frame timestamps into `DiagSwdlSession`. |
| **Offline Diagnostic Viewer** | ✅ **Working** | Loads all 13 DTCs without car connected; resolves 20 Customer Symptoms (CSC) in English with status ranks. |
| **DTC Timelines & Details** | ✅ **Working** | Displays occurrence timelines and freeze-frame details when selecting any fault code. |
| **ECU Vehicle Network Overview Map** | ✅ **Working** | Real-time GDI+ CGM vector renderer displays the V70 chassis outline, CAN/LIN buses, and 36 ECU modules with live status coloring (Green = OK, Red/Orange = Fault, Yellow = Intermittent, Gray = Inactive). |
| **Interactive Map Filtering** | ✅ **Working** | Hover hand cursor and clicking an ECU module filters the fault list directly. |
| **Modernized DTC View & Document Viewer** | ✅ **Working** | Responsive dialog with Segoe UI typography. Automatically resolves Information Element IDs to render **both** DTC condition details and step-by-step Fault-Tracing procedures with base64 embedded technical diagrams and clickable in-document links (`<xref>`). |
| **CSC-Based Reference Documents** | ✅ **Working** | Hierarchical navigation: single-clicking expands symptom folders; double-clicking leaf nodes reliably opens full technical procedures. |
| **DTC-Based Reference Documents** | ✅ **Working** | Full ECU module selection via top dropdown toolbar and interactive car graphic hotspot clicks. Populates 387+ related DTCs under ECM; double-clicking opens fault-tracing procedure. |
| **Genuine Volvo Service Manuals Explorer** | ✅ **Working** | Dedicated top-level toolbar tab (`📚 SERVICE MANUALS`). Organizes 68,000+ repair manuals using official Volvo `FunctionGroupText` + `TreeItem` hierarchy with descriptive engineering titles. |
| **Electronic Parts Catalog (EPC) Explorer** | ✅ **Working** | Dedicated top-level toolbar tab (`📦 PARTS CATALOG`). Interfaces the 150,436-part `EPC` database with dynamic model selection, engine filtering, 384 assemblies, and interactive parts table. |
| **Electronic Wiring Diagrams (EWD) Explorer** | ✅ **Working** | Dedicated top-level toolbar tab (`⚡ WIRING DIAGRAMS`). Extracted official `Volvo_EWD_2014D.iso` with 688 component index, instant search, and native **Microsoft Edge WebView2** vector PDF rendering. |
| **Visual Identity & Application Icon** | ✅ **Working** | Modern Nordic emblem combining the classic Volvo Iron Mark with a high-contrast electric cyan CAN telemetry pulse, created with **VectorCraft**. Multi-resolution Windows icon (`.ico`) embedded into executable shell and form headers. Native `.vectorcraft`, `.svg`, and 512×512 master `.png` generated. |

---

## 3. Testing & Verification Summary

| Test Task | Environment | Status | Verification Details |
| :--- | :--- | :---: | :--- |
| **DiCE USB Hardware Connection** | 🚗 In-Car | ✅ Pass | DiCE unit `DiCE-206751` recognized over USB; J2534 PassThru operational. |
| **Bidirectional Actuators / Component Testing** | 🚗 In-Car | ✅ Pass | Verified fuel flap solenoid, front & rear washer sprinklers, brake lights, wipers on real car. |
| **Multi-ECU Live CAN Parameter Streaming** | 🚗 In-Car | ✅ Pass | Streamed live parameters across ECM, BCM, and CEM simultaneously without errors. |
| **Watchlist Parameter Removal** | 🚗 In-Car / Desk | ✅ Pass | Verified item removal via top-bar `[❌ Remove Item]`, `[🗑️ Clear All]`, <kbd>Delete</kbd> key, and right-click context menu. |
| **Automatic Graph View Auto-Scaling** | 🚗 In-Car / Desk | ✅ Pass | Continuous range recalculation verified; curves remain centered with 20% headroom padding. |
| **Battery Voltage & Key Position Display** | 🚗 In-Car | ✅ Pass | Centivolt scaling and Swedish comma normalization verified across all 4 diagnostic tabs (`12.3 V` / `12.25V`, Key Pos II). |
| **Instant Launch UI (No Floating Dialogs)** | 🚗 In-Car / Desk | ✅ Pass | Pre-launch blocking progress bar eliminated; main window displays immediately; in-app `[⚡ Connect / Status]` button verified. |
| **All-ECU DTC & Symptom View** | 🏠 Desk / Offline | ✅ Pass | 13 DTCs and 20 symptoms loaded with freeze frames. |
| **Modernized DTC View & Documents** | 🏠 Desk / Offline | ✅ Pass | Full fault-tracing + DTC conditions + embedded technical diagrams. |
| **CSC & DTC Reference Info Document Resolution** | 🏠 Desk / Offline | ✅ Pass | 100% resolution verified across all symptom nodes including leakage, DTC codes, and workshop procedures. |
| **Workshop Service Manual Explorer** | 🏠 Desk / Offline | ✅ Pass | Dedicated tab; organized by official Volvo Function Groups with descriptive engineering titles. |
| **Electronic Parts Catalog (EPC) Explorer** | 🏠 Desk / Offline | ✅ Pass | Dedicated tab; auto-selects V70 D5204T2; 384 specific assemblies + 15,418 parts loaded; 100% round-trip tab switching verified. |
| **Electronic Wiring Diagrams (EWD) Explorer** | 🏠 Desk / Offline | ✅ Pass | Dedicated tab; auto-selects V70 (08-) MY 2011 (`39180202`); loads 688 components with instant search filter; round-trip tab switching verified across all 8 tabs. |
| **Full 8-Tab Navigation Round-Trip** | 🏠 Desk / Offline | ✅ Pass | Bidirectional tab switching between all 8 tabs verified with zero `InvalidCastException`. |
| **Startup Cleanliness (No SOAP Errors)** | 🏠 Desk / Offline | ✅ Pass | Auto-archives DRO `.xml.zip` logs; `diag.LoadControl()` succeeds with zero SOAP exceptions. |

---

## 4. Engineering, Packaging & Architecture Roadmap

The project roadmap combines diagnostic feature verification with codebase engineering, documentation, and standalone distribution (tracking items from `notes.md`):

| Priority | Item / Milestone | Area | Status | Scope & Objective |
| :---: | :--- | :---: | :---: | :--- |
| **1** | **Actuation Prerequisites Pre-Flight Popup** | Diagnostics | ✅ **Completed** | Informative pre-flight verification dialog intercepting double-clicks and activation triggers, showing physical KOEO/KOER prerequisites with `[▶️ Proceed]` / `[Cancel]` confirmation. |
| **2** | **Project Icon & Visual Identity** | Branding | ✅ **Completed** | Designed modern Nordic emblem with Volvo Iron Mark and cyan CAN pulse using **VectorCraft**; generated `.ico`, `.svg`, `.png`, and `.vectorcraft`; embedded into binary and window headers. |
| **3** | **Codebase Modularization & Refactoring** | Architecture | ✅ **Completed** | Split the 15,500-line monolithic `VidaLauncher.cs` and `DummyIsoView.cs` into a top-level `src/` architecture with 20 well-organized files across `Core/`, `Hardware/`, `Diagnostics/`, `Documents/`, `Tabs/`, `Testing/`, and `IsoView/`, plus `src/OpenVida.csproj` and root `build.bat`. *(From `notes.md`)* |
| **4** | **Startup JBoss & SQL Service Health Probe** | Core Lifecycle | ✅ **Completed** | Pre-flight probe (`StartupHealthCheck.cs`) verifying SQL Server socket responsiveness (with 1-click elevated auto-recovery for sleep hangs), JBoss port 80 auto-launch, and Edge WebView2 runtime detection. *(From `notes.md`)* |
| **5** | **Disconnected Vehicle Comm Safety Guard** | Diagnostics | ✅ **Completed** | Context-aware status banner (Connected vs. Disconnected vs. Mock) on Vehicle Comm tab; guarded action dialog offering 1-click Mock switch or DiCE re-probe when triggering actuations offline. |
| **6** | **Programmed Values Sub-Tab Integration** | Diagnostics | ✅ **Completed** | Full 4-tab vehicle communication interface adding **📝 Programmed Values** (`_programmedValuesTabPage`) alongside Parameters, Activations, and Advanced. Unlocks reading and writing non-volatile ECU configuration parameters (DRL modes, SRI intervals, seat heat presets, tire circumference, afterblow) with preflight safety guards and user confirmation. |
| **7** | **DTC Erase / Clear Faults ("Clear All")** | Diagnostics | ⏳ **Queued** | Test executing the all-ECU DTC clear sequence via DiCE J2534 on car, followed by a re-readout cycle to verify cleared vs. permanent DTCs. |
| **8** | **Service Functions, Adaptations & Calibrations** | Diagnostics | ⏳ **Queued** | Reset Service Reminder Indicator (SRI), Battery Monitoring Sensor (BMS), transmission adaptations, and DPF soot level check / forced regeneration. |
| **9** | **Project README Documentation** | Documentation | ✅ **Completed** | Authored comprehensive top-level [`README.md`](file:///c:/Users/david/git/vida/README.md) covering capabilities, the 5 pillars, hardware setup, quick start, architecture, and roadmap. *(From `notes.md`)* |
| **10** | **"Behind the Scenes" Technical Deep-Dive** | Documentation | ✅ **Completed** | Detailed technical reverse-engineering retrospective incorporated directly into [`README.md` (Section 6)](file:///c:/Users/david/git/vida/README.md#6-behind-the-scenes-modernizing-legacy-vida) detailing CGM parsing, ActiveX replacement, `ProgressControler` race condition fix, and SQL document extraction. *(From `notes.md`)* |
| **11** | **EWD Component Deep-Link Navigation Polish** | Wiring Diagrams | ✅ **Completed & Verified** | Indexed and cross-referenced all 688 electrical components across 54 Volvo EWD publications. Added dual-column navigation in dgvComponents for **⚡ Schematics** and **📍 Placement**, dynamic button labels (⚡ Schematic (P.xx) and 📍 Placement (P.xxx)), direct row click/double-click navigation, and seamless WebView2 PDF page switching via reset bounce. Verified live via --ewd-test. |
| **12** | **Standalone Windows Installer** | Distribution | ✅ **Completed & Verified** | Built self-contained Windows Setup Wizard (`OpenVidaSetup.exe`, `src/Installer/OpenVidaSetup.cs`) embedding all OpenVIDA binaries, `7z.exe`/`7z.dll`, and the 54-publication EWD component index (`assets/ewd_nav_index.zip`) as a single ~4 MB executable. Supports "Bring Your Own Data" installation from `.7z`/`.zip`, `Win7-x86-Vida.ova`/`.vmdk`, or existing `VIDA` folders + optional `Volvo_EWD_2014D.iso`/`.rar`. Automatically configures SQL Express TCP/IP & Mixed Mode, attaches all 9 databases, registers `DummyIsoView` COM & DiCE J2534 registry keys, and creates Desktop/Start Menu shortcuts. Paired with `pack_data.bat` for creating clean portable data archives. *(From `notes.md`)* |
| **13** | **Responsive Window Layout & Small Screen Audit** | UI / Ergonomics | ⏳ **Queued** | Audit and optimize layout behavior across compact displays (1024×768, 1366×768 laptops) up to 4K. Ensure toolbars wrap gracefully, grids auto-fit available space, and splitters maintain usable proportions. *(From `notes.md`)* |
| **14** | **Diagnostic Readout Export & Backup (JSON Sessions)** | Diagnostics / Data | ⏳ **Queued** | Store and retrieve full vehicle diagnostic readouts (VIN, ECU profile, DTC codes, status ranks, freeze-frame timestamps, and live parameter logs) in portable JSON format for offline review, diffing, and backup without vehicle connection. *(From `notes.md`)* |
| **15** | **MCP Server Evolution & In-App MCP Integration** | Tooling / AI | ✅ **Completed & Verified** | Built in-app Named Pipe server (`\\.\pipe\openvida_rpc`) in `src/Core/OpenVidaIpcServer.cs` granting `VidaLauncher` exclusive ownership of the DiCE J2534 hardware connection to eliminate `ERR_DEVICE_IN_USE` contention. Upgraded to `mcp/vida-mcp/VidaMcp.exe` as a unified MCP bridge: live vehicle tools (`dice_status`, `scan_network`, `read_active_dtcs`, `read_parameters`) query `VidaLauncher` via IPC with friendly start prompts when offline; technical knowledge tools (`lookup_dtc`, `search_repair_manual`, `get_repair_document`, `get_wiring_schematic`, `search_parts_catalog`, `decode_vin`, `list_ecus`) operate 100% offline directly against SQL Express and the EWD TSV index. End-to-end multi-tool querying verified live. *(From `notes.md`)* |
| **16** | **Post-Mortem Logging & Contribution Guidelines** | Community / QA | ⏳ **Queued** | Implement enhanced error telemetry and crash log dumps for post-mortem analysis. Document clear guidelines on how to file diagnostic issues, capture logs, and contribute bugfixes via Pull Requests. *(From `notes.md`)* |
