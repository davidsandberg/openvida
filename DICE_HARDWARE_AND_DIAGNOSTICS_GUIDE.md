# Volvo DiCE Hardware & Diagnostic Communication Guide

This guide details how the Volvo DiCE (Diagnostic Communication Equipment) hardware functions, how to power and test it without connecting to the vehicle, how 64-bit Windows drivers work, and how the diagnostic communication stack operates.

---

## 1. Powering DiCE: USB (5V) vs. 12V Supply

Because your car alternator is currently not charging the battery, **you do not need to connect DiCE to the car for software setup and driver testing**. You can perform almost the entire setup and validation from your desk.

### What Works on USB Power (5V) Alone:
When plugged into a PC USB port without any 12V power:
* **Microcontroller & USB Bus**: The internal ARM controller, USB interface, and firmware boot up completely on 5V USB power.
* **Status LEDs**: The DiCE firmware status LED will light up / flash.
* **Windows Detection**: Windows detects the device as `USB\VID_17AA&PID_D1CE`.
* **Driver Validation**: You can install and verify the 64-bit USB driver in Device Manager.
* **J2534 Layer**: `TSDiCE32.dll` can open the USB device handle (`PassThruOpen`) and retrieve firmware and device metadata.
* **DiCE Self-Test (`DiagApp.exe`)**: You can run the official DiCE diagnostic utility to test USB communication, verify firmware version (e.g. `5.6.2`), and self-test RAM and flash memory.
* **Diagnostic App Development**: We can build and test the Standalone Desktop Launcher (`Phase 2`) on your PC to ensure it connects and talks to DiCE over USB.

### What Specifically Requires 12V:
* **OBD-II Physical Transceivers**: The High-Speed CAN, Mid-Speed CAN, and K-Line bus transceivers (TJA1040/TJA1050 chips) require 12V to generate the differential signal voltages to talk to the car's ECUs.
* **Desk 12V Option (Optional)**: Most DiCE units feature a standard DC barrel power jack on the side (center-positive, 12V DC, 1A). If you have an ordinary 12V wall power supply (e.g. from an old Wi-Fi router, external hard drive enclosure, or monitor), you can plug it into DiCE on your desk to power the 12V rail without touching the car.

---

## 2. DiCE Communication Architecture

```
+-------------------------------------------------------------+
|              Standalone Desktop Launcher (Phase 2)          |
|    - DiagnosticMainForm (UI.DLL)                            |
|    - Parameter Read / Write & DTC Management               |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|             Vehicle Communication Engine (VehCom2.DLL)       |
|    - Protocol translation (D2, GGD, ISO14229 / UDS)         |
|    - ECU parameter addressing (queried from CarCom database)|
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|             SAE J2534-1 PassThru API (TSDiCE32.dll)         |
|    - PassThruOpen / PassThruClose                           |
|    - PassThruConnect (CAN 500k, CAN 125k, ISO9141)          |
|    - PassThruWriteMsgs / PassThruReadMsgs                   |
|    - Configured in Registry: PassThruSupport.04.04          |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|             Windows USB Driver (DiceUsb / WinUSB)           |
|    - Hardware ID: USB\VID_17AA&PID_D1CE                     |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|                     Physical DiCE Unit                      |
+-------------------------------------------------------------+
```

---

## 3. Step-by-Step Desk Setup & Verification

### Step 1: Register DiCE in the 64-bit Windows Registry
VIDA's communication library (`VehCom2.DLL`) and any 32-bit diagnostic tools locate the J2534 DLL via the 32-bit registry hive:
`HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PassThruSupport.04.04`

1. Open Windows Explorer and navigate to:
   `C:\Users\david\git\vida\DiCE`
2. Double-click **`dice_j2534.reg`**.
3. Click **Yes** when prompted by Windows to merge the settings.

### Step 2: Connect DiCE to PC via USB
1. Plug the DiCE unit into a USB port on your host PC.
2. Observe the LEDs on the DiCE unit:
   * The firmware/communication LED should illuminate or blink.
3. Open Windows **Device Manager** (`devmgmt.msc`):
   * Look for an entry under **DiCE Hardware**, **Universal Serial Bus devices**, or **Other Devices**.
   * Hardware ID will be `USB\VID_17AA&PID_D1CE`.

### Step 3: Verify or Install Driver
* If Device Manager shows DiCE with an exclamation mark:
  1. Right-click the device -> **Update driver**.
  2. Select **Browse my computer for drivers**.
  3. Point to: `C:\Users\david\git\vida\DiCE\Tools\Driver`.
  4. If Windows 64-bit reports that the driver lacks a digital signature or is 32-bit, we can install the signed 64-bit DiCE driver package or assign the Microsoft WinUSB driver.

### Step 4: Run the DiCE Diagnostic Self-Test
Once the USB connection is recognized:
1. Navigate to `C:\Users\david\git\vida\DiCE\Tools`.
2. Run **`DiagApp.exe`**.
3. Select your connected DiCE device from the dropdown and click **Test**.
4. DiagApp will execute self-tests on the unit:
   * USB communication loopback.
   * Internal memory (RAM & Flash) tests.
   * Firmware version readout.

---

## 4. Phase 2: Standalone Desktop Launcher (Complete)

Because modern browsers (Chrome and Edge) cannot execute the embedded .NET Windows Forms control in the web version's Diagnostics tab, the standalone launcher is the definitive solution:

1. **Hosts `DiagnosticMainForm`**: Integrates `Vcc.Vida.DiagSwdl.UI.Diagnostics.DiagnosticMainForm` directly inside a modern C# Form window.
2. **Direct Hardware Access**: Binds directly to `VehCom2.DLL` and `TSDiCE32.dll` via the installed 64-bit DiCE driver.
3. **Database Integration**: Automatically configured for `(local)\SQLEXPRESS` and authenticates via repaired SQL Server user `ASUser` (`GunnarS3g3`).
4. **Zero VM & Zero IE Dependency**: Lightweight, native 64-bit host execution with instant startup.

### How to Run:
Simply execute:
```cmd
run_diagnostics.bat
```
or launch directly:
```cmd
VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\VidaLauncher.exe
```
