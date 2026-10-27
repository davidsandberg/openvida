# Volvo VIDA & DiCE VirtualBox Setup Guide

This guide covers troubleshooting VirtualBox startup errors, configuring USB pass-through for the Volvo DiCE hardware, and connecting VIDA to a Volvo V70 (2011).

---

## 1. Troubleshooting VirtualBox Startup Errors

### Error: "An unexpected process (PID=...) has tried to lock the machine..."
This error occurs when VirtualBox's hardening or session manager encounters a privilege or process mismatch during `LaunchVMProcess`—commonly caused by orphaned background processes, "Run as Administrator" flag mismatches, or GUI process races.

#### Step 1: Terminate lingering VirtualBox processes
1. Close VirtualBox Manager.
2. Open **Task Manager** (`Ctrl + Shift + Esc`).
3. Switch to the **Details** tab and sort by name.
4. Kill any of the following running processes:
   - `VirtualBox.exe`
   - `VirtualBoxVM.exe`
   - `VBoxSVC.exe`
   - `VBoxSDS.exe`
   - `VBoxNetDHCP.exe`
*(Restarting the host laptop also clears all lingering locks).*

#### Step 2: Remove "Run as Administrator" from VirtualBox binaries
If compatibility flags or elevation were applied:
1. Open File Explorer to `C:\Program Files\Oracle\VirtualBox`.
2. Right-click `VirtualBox.exe` → **Properties** → **Compatibility** tab.
3. Ensure **Run this program as an administrator** is **unchecked**.
4. Check the same for `VirtualBoxVM.exe`.
5. Click **Change settings for all users** at the bottom to verify it isn't enabled globally.
6. Click **Apply** and **OK**.

#### Step 3: Work around GUI launch locking
If regular double-clicking continues to fail:
1. In VirtualBox Manager, select **Win7-x86-Vida Backup 20210627**.
2. Click the small drop-down arrow next to the green **Start** button in the toolbar.
3. Select **Headless Start** or **Detached Start**.
4. Once marked as *Running*, click the green arrow / **Show** button to open the VM display.

---

## 2. VirtualBox Configuration for DiCE USB Pass-Through

DiCE units (especially clone units like green-board or blue-board DiCE) require proper USB controller pass-through.

### 1. Install the VirtualBox Extension Pack
- Open VirtualBox Manager → **File** → **Tools** → **Extension Pack Manager** (or `File` → `Preferences` → `Extensions`).
- Verify that the **Oracle VM VirtualBox Extension Pack** version matches your installed VirtualBox version. Without this, VirtualBox only provides USB 1.1, causing DiCE communication to drop or fail.

### 2. Set the USB Controller for the VM
1. Ensure the VM is powered down (shut down, not saved state).
2. Right-click the VM → **Settings** → **Ports** → **USB**.
3. Enable the USB Controller:
   - Select **USB 2.0 (EHCI) Controller** (standard recommendation for DiCE).
   - If your host laptop only has USB 3.x ports and USB 2.0 fails to detect, test with **USB 3.0 (xHCI)**.

### 3. Add a DiCE USB Filter
1. On the same USB settings screen, click the **USB cable with the green plus icon** on the right side.
2. With DiCE plugged in, look for an entry named:
   - `DiCE-XXXXXX`
   - `SETek DiCE`
   - Or a device with Vendor ID `0F03` or Cypress/FTDI chipset.
3. Add the device to the filter list.
4. **Edit the filter**: Double-click the newly added filter, and **clear the "Serial Number" and "Port" fields** (leave them blank). DiCE clones often change reported serial or sub-port IDs during handshakes.

---

## 3. Hardware & Vehicle Connection Checklist (Volvo V70 2011)

1. **DiCE Needs 12V OBD Power**:
   - DiCE's main processor and firmware cannot fully run on laptop 5V USB power alone.
   - Plug the DiCE into your V70's OBD-II port (by the hood release latch under the dashboard on the driver side) **before** plugging the USB into your laptop.
   - Turn the V70 ignition to **Position II** (press the start button for ~2 seconds without pressing the brake pedal until all dash warning lights illuminate).
   - Verify DiCE LEDs:
     - The **Firmware status LED** should flash and stabilize.
     - A solid red error LED usually indicates the DiCE clone firmware crashed or cannot negotiate.
2. **Laptop USB 3.0 / USB-C Port Mitigation**:
   - Modern USB 3.x host controllers can disrupt DiCE clone USB transceiver chips.
   - If DiCE disconnects or gives an unknown device error, insert a **passive USB 2.0 hub** between the laptop and the DiCE cable. This forces the port to downgrade to USB 2.0 signalling.

---

## 4. In-VM Verification, Licensing & Time Sync

### 1. Fix "Logged in at a later time than current time" & Expired License
If the VM clock synced to a future date or the license shows expired, open **Command Prompt as Administrator** in Windows 7:
```cmd
"%ProgramFiles%\Microsoft SQL Server\100\Tools\Binn\OSQL.EXE" -S localhost\Vida -U sa -P GunnarS3g3 -Q "USE AccessServer; UPDATE Users SET LicenceKey = (SELECT * FROM OPENROWSET(BULK 'C:\VIDA\patch\license', SINGLE_BLOB) AS x);"
"%ProgramFiles%\Microsoft SQL Server\100\Tools\Binn\OSQL.EXE" -S localhost\Vida -U sa -P GunnarS3g3 -Q "USE AccessServer; UPDATE SERVERCONSISTENCY SET lastLogin = GETDATE();"
```
Restart the VIDA Monitor service via the system tray. This applies the 2050 license and synchronizes the recorded login timestamp to the current system time.

### 2. Verify Windows Device Driver in VM
- Open **Device Manager** (`devmgmt.msc`).
- Verify **DiCE Hardware** appears without a yellow exclamation mark.
- If an exclamation mark appears, verify the VM settings under **Ports → USB** use **USB 2.0 (EHCI) Controller**.

### 3. Run DiCE Diagnostic Tool
- Open **DiCE Diagnostic** (`C:\Program Files\DiCE\Tools\DiCE Diagnostic.exe`).
- Click **Test Communication Tool** / **Self Test** to verify USB and CAN transceivers.

### 4. Select DiCE in VIDA & Read Vehicle
- Open VIDA, log in with username **`1`**.
- Go to **VEHICLE PROFILE** tab.
- Under **Communication tool**, select your **DiCE-XXXXXX** unit.
- Ensure the car is in **Position II** with adequate battery voltage (>12V), then click **Read Vehicle**.

---

## 5. Vehicle Communication & Low Battery Troubleshooting (Volvo V70 P3)

If VIDA returns *"Not possible to read out information from the vehicle. Possible causes are: Ignition is not on..."*:

1. **Low Battery Voltage (CEM Power-Save Mode)**:
   - On Volvo P3 platform cars (V70 2008–2016), when the Central Electronic Module (CEM) detects battery voltage dropping below ~11.8V, it triggers the "Low battery" cluster warning and enters power-save mode, shutting down diagnostic gateway communications.
   - **Fix**: Connect a battery charger (10A+ recommended) or jump pack to the battery, or start the engine and let it idle during reading.
2. **Proper Ignition Position II**:
   - Insert key fob into the dock (if equipped).
   - Press and hold the **START/STOP** button for **2–3 seconds without pressing the brake pedal**.
   - Verify that all instrument cluster warning lamps turn on and stay on (Position I only powers the radio/windows).
3. **Engine Running Workaround**:
   - For basic vehicle identification, viewing live parameters, and reading/clearing DTC error codes, having the engine idling in Park is completely safe and supplies ~14V alternator power to keep all CAN modules awake.
