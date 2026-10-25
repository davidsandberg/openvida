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

## 4. In-VM Verification & VIDA Profile Selection

1. **Verify Windows Device Driver in VM**:
   - Inside the Win7 VM, open **Device Manager** (`devmgmt.msc`).
   - Check under **Universal Serial Bus controllers**. You should see **DiCE Hardware** without any yellow exclamation mark.
2. **Run DiCE Diagnostic Tool**:
   - Open **DiCE Diagnostic** (`C:\Program Files\DiCE\Tools\DiCE Diagnostic.exe` or desktop shortcut).
   - Click **Self Test** / **Test Communication** to test USB, RAM, and CAN transceivers.
3. **Select DiCE in VIDA**:
   - Launch VIDA and log in (typically as `admin` or `1`).
   - Navigate to the **VEHICLE PROFILE** tab.
   - Under **Communication tool**, select your **DiCE-XXXXXX** unit from the drop-down.
   - Click **Read Vehicle** to pull the VIN and scan the ECM/CEM network.
