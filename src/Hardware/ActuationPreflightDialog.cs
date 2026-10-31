using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.ProgressHandler;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static partial class Program
{
    static bool ShowPrerequisitePreflightDialog(string title, string nativeWarning, IList nativePreconds, IWin32Window parent)
    {
        try
        {
            if (string.IsNullOrEmpty(title)) title = "Component Actuation / Diagnostic Routine";

            string engineReq = "";
            string voltageReq = "• ⚡ Battery Voltage: > 12.0 V (Connect battery maintainer for extended tests)";
            string transReq = "";
            string safetyReq = "";
            string summary = "";

            string tLower = title.ToLowerInvariant();

            if (tLower.Contains("alternator") || tLower.Contains("generator") || tLower.Contains("laddning"))
            {
                summary = "Alternator and charging system tests evaluate regulator LIN-bus communication and voltage regulation across 11 duty cycles.";
                engineReq = "• 🔑 Engine State: MUST BE RUNNING AT IDLE (Alternator rotor must spin to generate charging current).";
                voltageReq = "• ⚡ Battery Monitoring: Battery Monitoring Sensor (BMS) connected; battery > 12.2 V.";
                transReq = "• 🛑 Transmission: Gear selector in Park or Neutral, parking brake firmly engaged.";
            }
            else if (tLower.Contains("wiper") || tLower.Contains("washer") || tLower.Contains("torkar") || tLower.Contains("spolar"))
            {
                summary = "Wiper/washer component test triggers mechanical sweep and high-pressure fluid pumps directly via CEM relays.";
                engineReq = "• 🔑 Ignition: Position II (Key ON, Engine OFF recommended).";
                safetyReq = "• ⚠️ Safety: Verify wiper arm sweep path is clear of ice, dry grime, and obstacles.";
            }
            else if (tLower.Contains("damper") || tLower.Contains("climate") || tLower.Contains("ccm") || tLower.Contains("spjäll") || tLower.Contains("kalibrer"))
            {
                summary = "CCM self-calibration sweeps all air distribution and blend damper stepper motors from stop to stop.";
                engineReq = "• 🔑 Ignition: Position II (Key ON, Engine OFF).";
                safetyReq = "• ❄️ Control Panel: Do NOT touch any buttons or dials on the climate panel while calibration runs (~30-45s).";
            }
            else if (tLower.Contains("fuel") || tLower.Contains("inject") || tLower.Contains("dpf") || tLower.Contains("bränsle") || tLower.Contains("spridare") || tLower.Contains("partikel"))
            {
                summary = "High-pressure fuel injection and exhaust aftertreatment routines require thermal stability and safety interlocks.";
                engineReq = "• 🔑 Engine State: Running at operating temperature (Coolant temp > 70 °C / 158 °F).";
                transReq = "• 🛑 Transmission: In Park / Neutral, parking brake engaged.";
                safetyReq = "• ⛽ Fuel Level: Minimum 20% fuel in tank (never run regeneration on low fuel reserve).";
            }
            else if (tLower.Contains("brake") || tLower.Contains("pbm") || tLower.Contains("parkering") || tLower.Contains("broms") || tLower.Contains("luftning"))
            {
                summary = "Brake and Electronic Parking Brake (EPB) service routines control hydraulic valves and caliper servo motors.";
                engineReq = "• 🔑 Ignition: Position II (Key ON, Engine OFF).";
                safetyReq = "• 🛑 Safety: Vehicle must be on completely level ground; chock wheels if releasing EPB into service mode!";
            }
            else
            {
                summary = "Diagnostic routines and component actuations command vehicle modules via CAN/LIN bus.";
                engineReq = "• 🔑 Ignition: Position II (Key ON). Note that routines testing rotation or charging require engine at idle.";
                safetyReq = "• ⚠️ Preconditions Notice: If the module rejects the request or no movement occurs, ensure physical preconditions (engine state, gear position, doors/hood closed) are satisfied.";
            }

            using (Form dlg = new Form())
            {
                dlg.Text = "Prerequisite Verification";
                dlg.Size = new Size(540, 390);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.White;

                Panel pnlHeader = new Panel();
                pnlHeader.Dock = DockStyle.Top;
                pnlHeader.Height = 60;
                pnlHeader.BackColor = Color.FromArgb(240, 246, 252);

                Label lblHeader = new Label();
                lblHeader.Text = "📋 " + title;
                lblHeader.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                lblHeader.ForeColor = Color.FromArgb(15, 45, 85);
                lblHeader.SetBounds(16, 10, 490, 24);

                Label lblSub = new Label();
                lblSub.Text = "Verify physical vehicle prerequisites before triggering:";
                lblSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblSub.ForeColor = Color.FromArgb(85, 95, 110);
                lblSub.SetBounds(18, 34, 490, 18);

                pnlHeader.Controls.Add(lblHeader);
                pnlHeader.Controls.Add(lblSub);
                dlg.Controls.Add(pnlHeader);

                RichTextBox txtBody = new RichTextBox();
                txtBody.ReadOnly = true;
                txtBody.BorderStyle = BorderStyle.None;
                txtBody.BackColor = Color.White;
                txtBody.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                txtBody.SetBounds(20, 72, 485, 215);

                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(summary)) sb.AppendLine(summary).AppendLine();
                sb.AppendLine("Required Operating Conditions:");
                if (!string.IsNullOrEmpty(engineReq)) sb.AppendLine(engineReq);
                if (!string.IsNullOrEmpty(voltageReq)) sb.AppendLine(voltageReq);
                if (!string.IsNullOrEmpty(transReq)) sb.AppendLine(transReq);
                if (!string.IsNullOrEmpty(safetyReq)) sb.AppendLine(safetyReq);
                if (!string.IsNullOrEmpty(nativeWarning)) sb.AppendLine().AppendLine("⚠️ Volvo Specific Warning: " + nativeWarning);
                if (nativePreconds != null && nativePreconds.Count > 0)
                {
                    sb.AppendLine().AppendLine("Module Preconditions:");
                    foreach (var pc in nativePreconds)
                    {
                        if (pc != null) sb.AppendLine("• " + pc.ToString());
                    }
                }
                txtBody.Text = sb.ToString();
                dlg.Controls.Add(txtBody);

                Panel pnlBtns = new Panel();
                pnlBtns.Dock = DockStyle.Bottom;
                pnlBtns.Height = 52;
                pnlBtns.BackColor = Color.FromArgb(248, 249, 250);

                Button btnProceed = new Button();
                btnProceed.Text = "▶️ Proceed & Execute";
                btnProceed.DialogResult = DialogResult.OK;
                btnProceed.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                btnProceed.BackColor = Color.FromArgb(0, 102, 204);
                btnProceed.ForeColor = Color.White;
                btnProceed.FlatStyle = FlatStyle.Flat;
                btnProceed.SetBounds(230, 10, 175, 32);
                btnProceed.Cursor = Cursors.Hand;

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                btnCancel.SetBounds(415, 10, 95, 32);
                btnCancel.Cursor = Cursors.Hand;

                pnlBtns.Controls.Add(btnProceed);
                pnlBtns.Controls.Add(btnCancel);
                dlg.Controls.Add(pnlBtns);
                dlg.AcceptButton = btnProceed;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog(parent) == DialogResult.OK;
            }
        }
        catch (Exception exDlg)
        {
            Console.WriteLine("[Launcher] Preflight dialog error: " + exDlg.Message);
            return true;
        }
    }


    public enum VehicleRequiredAction
    {
        Cancel,
        EnableMock,
        TryConnect
    }

    public static VehicleRequiredAction ShowVehicleRequiredDialog(IWin32Window parent, string actionName)
    {
        using (Form dlg = new Form())
        {
            dlg.Text = "OpenVIDA — Vehicle Communication Required";
            dlg.Size = new Size(540, 270);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            dlg.BackColor = Color.White;
            dlg.Font = new Font("Segoe UI", 9.0f);

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vida_icon.ico");
                if (File.Exists(icoPath)) dlg.Icon = new Icon(icoPath);
            }
            catch {}

            Panel header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(239, 246, 255) };
            Label lblIcon = new Label { Text = "🚗", Font = new Font("Segoe UI", 20f), Location = new Point(14, 10), Size = new Size(46, 42), AutoSize = false, BackColor = Color.Transparent };
            Label lblTitle = new Label { Text = "Vehicle Communication Required", Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 64, 175), Location = new Point(68, 12), AutoSize = true, BackColor = Color.Transparent };
            Label lblSub = new Label { Text = "Live component actuations require an active DiCE vehicle connection.", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(59, 130, 246), Location = new Point(68, 35), AutoSize = true, BackColor = Color.Transparent };
            header.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblSub });

            Label lblMsg = new Label
            {
                Text = string.Format("'{0}' communicates directly with vehicle control modules over DiCE.\n\nNo vehicle is currently detected (DiCE disconnected or ignition in Position 0).\n\nWould you like to enable Mock Simulation Mode to safely test this control on your desktop?", actionName),
                Location = new Point(20, 75),
                Size = new Size(465, 80),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(248, 250, 252) };

            Button btnMock = new Button
            {
                Text = "🧪 Enable Mock Mode",
                Location = new Point(20, 14),
                Size = new Size(160, 34),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMock.FlatAppearance.BorderSize = 0;

            Button btnConnect = new Button
            {
                Text = "⚡ Try DiCE Connect",
                Location = new Point(190, 14),
                Size = new Size(160, 34),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnConnect.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(360, 14),
                Size = new Size(90, 34),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(100, 116, 139),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            VehicleRequiredAction action = VehicleRequiredAction.Cancel;

            btnMock.Click += (s, e) => { action = VehicleRequiredAction.EnableMock; dlg.Close(); };
            btnConnect.Click += (s, e) => { action = VehicleRequiredAction.TryConnect; dlg.Close(); };
            btnCancel.Click += (s, e) => { action = VehicleRequiredAction.Cancel; dlg.Close(); };

            btnPanel.Controls.AddRange(new Control[] { btnMock, btnConnect, btnCancel });
            dlg.Controls.AddRange(new Control[] { header, lblMsg, btnPanel });

            if (parent != null) dlg.ShowDialog(parent);
            else dlg.ShowDialog();

            return action;
        }
    }

}
