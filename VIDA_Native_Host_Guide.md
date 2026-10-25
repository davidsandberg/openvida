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

## 4. Next Steps: Standalone Desktop Launcher (Phase 2)

Because the **Diagnostics** tab in the web interface was built as an embedded .NET Windows Forms UserControl (`Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticMainForm`) that required legacy Internet Explorer ActiveX hosting, Phase 2 implements a standalone C# desktop application:
1. Directly hosts `DiagnosticMainForm` inside a standard Windows Form window.
2. Directly initializes `VehCom2.DLL` and `TSDiCE32.dll`.
3. Bypasses Internet Explorer completely for:
   - Reading and clearing Diagnostic Trouble Codes (DTCs).
   - Viewing live sensor data and ECU parameters.
   - Reading ECU identification and programming parameters.
