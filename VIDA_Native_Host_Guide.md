# Volvo VIDA 2014D Native Host & Diagnostic Setup Guide

This guide documents the architecture, database configurations, backend setup, web login fixes, and hardware communication setup to run Volvo VIDA natively on modern 64-bit Windows (Windows 10 / 11) without requiring a virtual machine or legacy Internet Explorer.

---

## 1. Application Architecture

Volvo VIDA (Vehicle Information and Diagnostics for Aftersales) 2014D is composed of three primary layers:

1. **Database Tier (Microsoft SQL Server)**:
   - 9 relational databases storing vehicle profiles, diagnostic fault trees, parts catalogs, and user access:
     - `BaseData`: Vehicle models, years, chassis ranges, parts catalog data.
     - `EPC`: Electronic Parts Catalog structure and graphics mappings.
     - `AccessServer`: User accounts, subscriptions, licenses, and computer registrations.
     - `DiagSwdlRepository`: Diagnostic fault trees, ECU software download packages, and DTC definitions.
     - `CarCom`: ECU variant parameters, communication protocol specs, ECU addresses, and CAN networks.
     - `DiagSwdlSession`: Current and historical diagnostic/SWDL session logs.
     - `DiceTiming`: Timing constraints for J2534 / DiCE message framing.
     - `imagerepository`: Binary images for parts illustrations and wiring schematics.
     - `servicerep_en-US`: Service procedures, fault-tracing articles, and repair manuals.
   - Hosted on Microsoft SQL Server 2022 Express (`SQLEXPRESS`) on host TCP port `1433`.
   - Dedicated service logins: `sa`, `ASUser` (`gunnarsege`), and `PPUsers` (`PPUsers`).

2. **Backend Application Server (JBoss AS 7.1.1.Final)**:
   - Runs on bundled 32-bit Java 6 JDK (`VIDA\jdk\bin\java.exe`).
   - Starts via `VIDA\jboss.bat start` (configured in `VIDA\setEnv.bat` to resolve `%VIDA_HOME%` dynamically).
   - Datasources configured in `VIDA\jboss\standalone\deployments\sqlserver-ds.xml` connecting to `127.0.0.1:1433` via Microsoft JDBC 4.0 (`sqljdbc4.jar`).
   - Web application deployed as `VidaEar.ear` / `VidaWeb.war`, listening on HTTP port `80` at `http://localhost/Vida`.

3. **Client & Diagnostic Interface**:
   - Web frontend: Struts 1.x + DWR AJAX + JSP (`http://localhost/Vida`).
   - Vehicle communication: `VehCom2.DLL` implementing SAE J2534-1 PassThru calls.
   - DiCE PassThru library: `TSDiCE32.dll` communicating with the physical DiCE adapter.

---

## 2. Key Fixes Applied

### A. SQL Server TCP/IP and Authentication Setup
Fresh SQL Server Express installations disable TCP/IP by default and only allow Windows Authentication.
- Executed `enable_sql_tcp.bat` to:
  - Enable Mixed Mode Authentication (SQL Server and Windows Authentication).
  - Enable the TCP/IP protocol listening on port `1433`.
  - Start and set SQL Server Browser service to Automatic.
- Attached all 9 `.mdf` database files from `VIDA\db\`.
- Created logins `ASUser` and `PPUsers`, granting `db_owner` rights across all databases.

### B. License Expiration Bypass (Post-2020 Patch)
Original VIDA 2014D licenses expired on `2020-12-31 23:59:59`, triggering a redirect to the offline central server `vidaca.volvocars.biz`.
- Patched `VIDA\patch\license` XML to extend `SubscriptionExpirationDate` to `2050-12-31`.
- Updated `AccessServer.dbo.Users.LicenceKey` with the extended license XML.
- Activated all accounts by setting `IsActive = 1` in `AccessServer.dbo.Users`.
- Valid active accounts: `1`, `2`, `3`, `4`, `admin`.

### C. Modern Browser Web Login Fix
In modern browsers (Edge, Chrome, Firefox), pressing **Enter** in the login form previously triggered native HTML form submission without setting the legacy Struts `actionEvent=login` parameter, resulting in `LoginForm: Error during Validate: username is null or length == 0`.
- Updated `VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\jsp\login\login.jsp`:
  - Added native form submission and Enter-key handling (`onkeydown` / `onsubmit`).
  - Added a visible standard **Log in** button alongside the legacy anchor tag.
  - Trimmed and validated input to guarantee `username`, `hostName`, and `actionEvent=login` are sent properly.

---

## 3. Vehicle Communication (DiCE & J2534)

### Hardware Identification
- Volvo DiCE clone hardware: `USB\VID_17AA&PID_D1CE`.
- Communication Protocol: Standard SAE J2534-1 PassThru API.

### J2534 PassThru Registry Setup
For VIDA's `VehCom2.DLL` and any diagnostic launcher to locate the DiCE driver, import the registry configuration file:
- Location: `DiCE\dice_j2534.reg`
- Registers `TSDiCE32.dll` under `HKLM\SOFTWARE\WOW6432Node\PassThruSupport.04.04\SETEK - DiCE` and `DiCE-206751`.

### 64-bit Windows Driver Notes
- The 32-bit `DiceUsb.sys` kernel driver from Windows 7 cannot be loaded directly by 64-bit Windows 10/11 kernels.
- When plugging in DiCE via USB:
  - If Windows Device Manager shows an unrecognized device (`USB\VID_17AA&PID_D1CE`), install the signed 64-bit DiCE driver package or use WinUSB.
  - If using Bluetooth, Windows assigns a standard Virtual Serial (COM) port that works natively across all Windows versions without custom USB kernel drivers.

---

## 4. Standalone Desktop Launcher (Phase 2 - Complete)

Because the **Diagnostics** tab in the web interface was built as an embedded .NET Windows Forms UserControl (`Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticMainForm`) that originally required legacy Internet Explorer ActiveX hosting, we created a native standalone C# desktop application: [`VidaLauncher.cs`](file:///C:/Users/david/git/vida/VIDA/jboss/standalone/deployments/VidaEar.ear/VidaWeb.war/Diagnostics/VidaLauncher.cs).

### Key Architecture & Implementation Details:
1. **Hosting Form**: Wraps `DiagnosticMainForm` in a standard WinForms window maximized to full screen.
2. **Environment & Paths**: Automatically sets `VIDA_HOME` so configuration files (`diagnostic.config.xml`) and logs are discovered cleanly without hardcoded directory paths.
3. **Database Integration**:
   - Updated `components.config.xml` to point from legacy `(local)\VIDA` to active SQL instance `(local)\SQLEXPRESS`.
   - Granted database permissions and repaired orphaned user SIDs with [`repair_sql_users.ps1`](file:///C:/Users/david/git/vida/repair_sql_users.ps1) for `ASUser` (`GunnarS3g3`).
4. **Hardware Driver**:
   - Official 64-bit DiCE drivers installed via `SetupDiCE.exe`.
   - Verified J2534 PassThru communication with physical DiCE unit (`DICE-206751`).
5. **Direct Execution**:
   - Bypasses Internet Explorer completely.
   - Run anytime via [`run_diagnostics.bat`](file:///C:/Users/david/git/vida/run_diagnostics.bat).

---

## 5. Quick Start: How to Run VIDA Diagnostics

To start using VIDA Diagnostics on Windows 11:
1. Power your DiCE unit (either via vehicle OBD-II port or 12V DC desk adapter) and plug in the USB cable.
2. Double-click [`run_diagnostics.bat`](file:///C:/Users/david/git/vida/run_diagnostics.bat) in the repository root.
   - It will verify SQL Server Express and JBoss services are active.
   - It will launch the native VIDA Diagnostics application maximized on your desktop.
3. Use the Diagnostics interface to read vehicle DTCs, inspect ECU parameters, and view live vehicle data!

---

## 6. Vehicle Communication & Live Readout Resolution

During live vehicle readout over DiCE (OBD-II), several internal architecture obstacles in VIDA 2014D were resolved:

### A. Multi-Threaded Session State Propagation
- `SessionManager.Instance` maintains a table of 20 background sessions indexed by comm tool name.
- When background script threads execute (e.g. `DroScriptThread`, `ReadDtcScriptThread`), they look up parameters by comm tool name.
- Solved by synchronizing all 20 session slots with the vehicle profile (`0b00c8af84add183`), VIN (`YV1BW5251B1189886`), culture (`en-US`), partner group (`1002`), and comm tool (`DiCE-206751` / `DICE = 1`).

### B. Multiple `CurrentCommTool` Instances in IoC Container
- In VIDA's component architecture, separate `CurrentCommTool` instances are instantiated for `VehicleInformationWF`, `VehicleConnectionProvider`, and `RunScriptService._vehCommProvider`.
- Without synchronization, `ensureCommToolConnection()` in `VehCom2.DLL` sees `CommToolType = NOCOMM (3)` and aborts with `"Unsupported communication tool"`.
- Solved by `ConfigureAllCommTools()` recursively synchronizing every `CurrentCommTool` across the entire object tree to `DiCE-206751` and `CommToolType.DICE`.

### C. SQL Server Constraint on Table `DiagSwdlSession.dbo.Dtc`
- Column `fkEcuInfoId` had a `NOT NULL` constraint without a foreign key, causing SQL Exception 515 when saving DTCs read prior to ECU hardware catalog matching.
- Solved by `ALTER TABLE dbo.Dtc ALTER COLUMN fkEcuInfoId int NULL;`.

Real vehicle readout was confirmed against vehicle VIN `YV1BW5251B1189886`, successfully interrogating vehicle ECUs over CAN and reading live DTCs (e.g. `P067300`, `P067400` from ECM).

---

## 7. Offline Diagnostics & Multi-Tab Architecture

All diagnostic data (13 DTCs, 60+ ECUs, vehicle profiles) read from the car are permanently stored in local SQL Server database `DiagSwdlSession`. The application fully supports working offline (disconnected from the vehicle):

### A. IsoView ActiveX Elimination
- Native VIDA controls (`NavImage`) require an ActiveX viewer (`IsoViewX`, CLSID `{865b2280-2b71-11d1-bc01-006097ac382a}`).
- In modern 64-bit Windows without Internet Explorer or legacy IsoView installers, this threw `COMException: Class not registered (0x80040154)`.
- Solved by implementing a native .NET COM component [`DummyIsoView.cs`](file:///C:/Users/david/.gemini/antigravity/brain/582883f0-afd8-4231-87cd-0add866e7f19/scratch/DummyIsoView.cs) compiled to `DummyIsoView.dll` and registered into HKCU without requiring administrator rights.

### B. Offline Database Session Attachment
- In offline mode, `WorkshopSession.Attach()` attaches to the existing database session (ID `371`) in `DiagSwdlSession`.
- All readouts, DTC ranks, and ECU states are immediately populated into `DiagnosticManager` memory upon startup without requiring vehicle communication.

### C. Culture Synchronization for Diagnostic Stored Procedures
- VIDA's stored procedures (e.g. `vadis_GetCSCByID`) return localized component names based on the session's language code.
- When `SessionManager.Instance.Current.CultureName` was unpopulated, `GetCustomerSymptomByID` returned `NULL` component names, causing `NullReferenceException` during `CustomerSymptom.CompareTo()` sorting.
- Solved by synchronizing `en-US` culture across `SessionManager.Instance`, `_current`, `_baseParameters`, and `sessionsParameter[]`, as well as setting UI thread cultures.

### D. All Diagnostic Tabs Unlocked & Verified
- **Fault Trace (DTCs)**: Full interactive list of Customer Symptoms and DTCs (`ECM-P067200`, `ECM-P067300`, `CEM-U300316`, `ECM-P067400`), fault tracing trees, test procedures, and `DETAILS` (freeze frames / counters / status bits).
- **Network**: Network status and ECU identification overview.
- **Vehicle Communication**: Parameters, activations, programmed values, and advanced tests.
- **Reference Information**: Technical specifications, repair documents, leakage/noise/vibration guides.

