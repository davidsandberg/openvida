---
name: Hardware Compatibility Report (DiCE / J2534)
about: Report compatibility results for DiCE or alternative J2534 diagnostic adapters
title: "[HARDWARE] "
labels: hardware, compatibility
assignees: ''
---

### Hardware Information
- **Adapter Name & Manufacturer**: (e.g. Tactrix OpenPort 2.0, Scanmatik 2 Pro, VXDIAG VCX Nano, DiCE Clone)
- **Interface / Connection Type**: (USB / Bluetooth / WiFi)
- **J2534 DLL Registry Key**: (Found in `HKLM\SOFTWARE\PassThruSupport.04.04\...`)
- **Driver & Firmware Version**: 

### Vehicle Tested
- **Model & Chassis**: (e.g. 2008 XC70 P3, 2004 V70 P2, 2012 S60)
- **Model Year**: 
- **Engine / Transmission**: 

### Test Checklist
Please check which operations succeeded with your adapter:
- [ ] Tool detected and identified in OpenVIDA
- [ ] Battery voltage reading displayed accurately in top bar
- [ ] Read vehicle VIN & profile configuration
- [ ] Full vehicle DTC readout (Initial State)
- [ ] Network ECU status scan (Network tab)
- [ ] Live sensor parameter streaming (Vehicle Comm -> Parameters)
- [ ] Parameter graphing & watchlist
- [ ] Actuator / component activations (Vehicle Comm -> Activations)
- [ ] Read / write programmed values
- [ ] Diagnostic Readout Export (`[📁 Readouts]`)

### Issues / Anomalies Encountered
Describe any communication timeouts, dropped CAN frames, or glitches observed:

### Relevant Log Output
> Attach or paste relevant log messages from `logs/openvida.log`:

```text
[Paste logs here]
```
