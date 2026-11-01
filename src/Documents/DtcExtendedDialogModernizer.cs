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
    static void ModernizeDtcExtendedDialog(Form dlg, Form mainForm)

    {

        try

        {

            dlg.SuspendLayout();

            dlg.FormBorderStyle = FormBorderStyle.Sizable;

            dlg.MaximizeBox = true;

            dlg.MinimizeBox = false;

            dlg.Icon = mainForm != null ? mainForm.Icon : null;

            int targetW = Math.Min(1150, Screen.PrimaryScreen.WorkingArea.Width - 60);

            int targetH = Math.Min(800, Screen.PrimaryScreen.WorkingArea.Height - 60);

            dlg.Size = new Size(targetW, targetH);

            dlg.StartPosition = FormStartPosition.CenterScreen;

            dlg.Location = new Point(Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - dlg.Width) / 2), Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Height - dlg.Height) / 2));

            Font modernFont = new Font("Segoe UI", 9.25f);

            Control computedProps = GetFieldValue(dlg, "_computedDtcPropertiesControl") as Control;

            Panel mainPnl = GetFieldValue(dlg, "_mainPanel") as Panel;

            Control btnClose = GetFieldValue(dlg, "_fbClose") as Control;

            Control btnUpdate = GetFieldValue(dlg, "_fbUpdate") as Control;

            Control btnFaultTrace = GetFieldValue(dlg, "_fbCloseAndFaultTrace") as Control;

            Control picInfo = GetFieldValue(dlg, "_pbInformationNoFaultTrace") as Control;

            object currentDtc = GetFieldValue(dlg, "_currentDtc");

            object frmOverview = GetFieldValue(dlg, "_frmOverview");

            if (computedProps != null)

            {

                computedProps.Location = new Point(14, 12);

                computedProps.Size = new Size(dlg.ClientSize.Width - 28, 140);

                computedProps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            }

            if (mainPnl != null)

            {

                mainPnl.Location = new Point(14, 160);

                mainPnl.Size = new Size(dlg.ClientSize.Width - 28, dlg.ClientSize.Height - 160 - 54);

                mainPnl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            }

            Action positionButtons = () =>

            {

                int btnY = dlg.ClientSize.Height - 42;

                int right = dlg.ClientSize.Width - 14;

                if (btnClose != null)

                {

                    btnClose.Font = modernFont;

                    btnClose.Height = 30;

                    btnClose.Location = new Point(right - btnClose.Width, btnY);

                    btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                    right -= (btnClose.Width + 10);

                }

                if (btnUpdate != null)

                {

                    btnUpdate.Font = modernFont;

                    btnUpdate.Height = 30;

                    btnUpdate.Location = new Point(right - btnUpdate.Width, btnY);

                    btnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                    right -= (btnUpdate.Width + 10);

                }

                if (btnFaultTrace != null)

                {

                    btnFaultTrace.Font = modernFont;

                    btnFaultTrace.Height = 30;

                    btnFaultTrace.Location = new Point(right - btnFaultTrace.Width, btnY);

                    btnFaultTrace.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                    right -= (btnFaultTrace.Width + 10);

                }

                if (picInfo != null)

                {

                    picInfo.Location = new Point(right - picInfo.Width, btnY + 7);

                    picInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                }

            };

            positionButtons();

            dlg.Resize += (s, e) => positionButtons();

            if (frmOverview != null && currentDtc != null)

            {

                WebBrowser wb = GetFieldValue(frmOverview, "_dtcInfoDisplay") as WebBrowser;

                Label lblNo = GetFieldValue(frmOverview, "_lblNoFaultTracingAvailable") as Label;

                object ieObj = GetPropValue(currentDtc, "InformationElement");

                string ieId = null;

                if (ieObj != null)

                {

                    ieId = GetPropValue(ieObj, "IEId") as string;

                }

                if (wb != null)

                {

                    wb.ScriptErrorsSuppressed = true;

                    // Support in-document link navigation

                    wb.Navigating += (sNav, eNav) =>

                    {

                        try

                        {

                            string url = eNav.Url != null ? eNav.Url.ToString() : "";

                            if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))

                            {

                                eNav.Cancel = true;

                                string targetId = url.Substring("vida-doc:".Length);

                                wb.DocumentText = RenderDocumentHtml(targetId, null);

                            }

                        }

                        catch {}

                    };

                    if (!string.IsNullOrEmpty(ieId))

                    {

                        string html = RenderDocumentHtml(ieId, null);

                        wb.DocumentText = html;

                        wb.Visible = true;

                        if (lblNo != null) lblNo.Visible = false;

                    }

                    else

                    {

                        string dtcCode = (GetPropValue(currentDtc, "Value") as string) ?? "";

                        string dtcDesc = (GetPropValue(currentDtc, "Text") as string) ?? "";

                        string ecu = (GetPropValue(currentDtc, "EcuName") as string) ?? "";

                        string html = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" +

                            "body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 24px; color: #333; line-height: 1.6; }" +

                            ".card { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 5px solid #005087; border-radius: 6px; padding: 20px; margin-top: 10px; }" +

                            "h2 { color: #003057; margin-top: 0; }" +

                            ".badge { display: inline-block; background: #003057; color: white; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 13px; }" +

                            "</style></head><body><div class='card'>" +

                            "<h2><span class='badge'>" + ecu + "</span> " + dtcCode + "</h2>" +

                            "<p style='font-size: 15px; font-weight: 500;'>" + dtcDesc + "</p>" +

                            "<p style='color: #666;'>Standard diagnostic readout from vehicle network. No additional interactive fault-tracing sequence is required for this code in the current vehicle configuration.</p>" +

                            "<p style='color: #666;'>Use the <strong>Details</strong> tab to view status identifiers and freeze-frame values, or the <strong>Timeline</strong> tab for DTC history.</p>" +

                            "</div></body></html>";

                        wb.DocumentText = html;

                        wb.Visible = true;

                        if (lblNo != null) lblNo.Visible = false;

                    }

                }

            }

            dlg.FormClosing += (sClose, eClose) =>

            {

                try

                {

                    MethodInfo miFT = dlg.GetType().GetMethod("set_FaultTrace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (miFT != null) miFT.Invoke(dlg, new object[] { false });

                    object frmOv = GetFieldValue(dlg, "_frmOverview");

                    if (frmOv != null)

                    {

                        MethodInfo miKill = frmOv.GetType().GetMethod("KillNavigator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (miKill != null) miKill.Invoke(frmOv, null);

                    }

                }

                catch {}

            };

            dlg.FormClosed += (sClosed, eClosed) =>

            {

                try

                {

                    if (mainForm != null && !mainForm.IsDisposed)

                    {

                        mainForm.Activate();

                        mainForm.Focus();

                    }

                    if (mainForm != null)

                    {

                        Action<Control> refreshFaultTrace = null;

                        refreshFaultTrace = (ctrl) =>

                        {

                            if (ctrl == null) return;

                            if (ctrl.GetType().Name == "CustomerSymptomsFaultTraceForm")

                            {

                                Control navImg = GetFieldValue(ctrl, "_navImage") as Control;

                                if (navImg != null) navImg.Enabled = true;

                                Control btnShowInfo = GetFieldValue(ctrl, "_btnShowInformation") as Control;

                                if (btnShowInfo != null) btnShowInfo.Enabled = true;

                                MethodInfo miCheck = ctrl.GetType().GetMethod("CheckChosenItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                if (miCheck != null) miCheck.Invoke(ctrl, null);

                            }

                            foreach (Control child in ctrl.Controls) refreshFaultTrace(child);

                        };

                        refreshFaultTrace(mainForm);

                    }

                    dlg.Dispose();

                }

                catch {}

            };

            dlg.ResumeLayout(true);

            Console.WriteLine("[Launcher] Successfully modernized DtcExtendedViewDialog!");

        }

        catch (Exception exDlg)

        {

            Console.WriteLine("[Launcher] ModernizeDtcExtendedDialog error: " + exDlg);

        }

    }

    static void LoadDtcsFromDbDirectly(Control statusTv, string ecuTypeId, string profileId)

    {

        if (statusTv == null) return;

        TreeView tvEx = GetFieldValue(statusTv, "_treeViewEx") as TreeView;

        if (tvEx == null) return;

        int ecuTypeNum;

        if (!int.TryParse(ecuTypeId, out ecuTypeNum)) return;

        try

        {

            int variantId = 0;

            using (var cc = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=CarCom;Integrated Security=True"))

            {

                cc.Open();

                using (var cmd = new System.Data.SqlClient.SqlCommand("vadis_GetDefaultEcuVariants", cc))

                {

                    cmd.CommandType = System.Data.CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@profileId", profileId);

                    using (var r = cmd.ExecuteReader())

                    {

                        while (r.Read())

                        {

                            if (Convert.ToInt32(r[0]) == ecuTypeNum)

                            {

                                variantId = Convert.ToInt32(r[2]);

                                break;

                            }

                        }

                    }

                }

                if (variantId == 0) return;

                string symptomIds = "";

                using (var cmd2 = new System.Data.SqlClient.SqlCommand("vadis_GetDtcSymptomsForEcu", cc))

                {

                    cmd2.CommandType = System.Data.CommandType.StoredProcedure;

                    cmd2.Parameters.AddWithValue("@ecuId", variantId);

                    using (var r2 = cmd2.ExecuteReader())

                    {

                        while (r2.Read())

                        {

                            if (symptomIds.Length > 0) symptomIds += ",";

                            symptomIds += r2[0].ToString();

                        }

                    }

                }

                if (string.IsNullOrEmpty(symptomIds)) return;

                using (var rep = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=DiagSwdlRepository;Integrated Security=True"))

                {

                    rep.Open();

                    using (var cmd3 = new System.Data.SqlClient.SqlCommand("GetDtcIEs", rep))

                    {

                        cmd3.CommandType = System.Data.CommandType.StoredProcedure;

                        cmd3.Parameters.AddWithValue("@languageCode", "en-US");

                        cmd3.Parameters.AddWithValue("@symptomIds", symptomIds);

                        cmd3.Parameters.AddWithValue("@profileList", profileId);

                        using (var r3 = cmd3.ExecuteReader())

                        {

                            tvEx.BeginUpdate();

                            tvEx.Nodes.Clear();

                            while (r3.Read())

                            {

                                string dispText = r3["DisplayText"].ToString();

                                string ieId = r3["ieId"] != DBNull.Value ? r3["ieId"].ToString() : "";

                                TreeNode dtcNode = new TreeNode(dispText);

                                var treeIe = new Vcc.Vida.DiagSwdl.ValueObjects.Symptoms.TreeIEVO();

                                treeIe.IEId = ieId;

                                treeIe.Title = dispText;

                                var nodeItem = new Vcc.Vida.DiagSwdl.UI.Components.NodeItem();

                                nodeItem.InformationElement = treeIe;

                                nodeItem.Text = dispText;

                                nodeItem.ID = r3["symptomId"].ToString();

                                nodeItem.Status = Vcc.Vida.DiagSwdl.GuiComponents.TreeNodeStatus.Document;

                                dtcNode.Tag = nodeItem;

                                tvEx.Nodes.Add(dtcNode);

                            }

                            tvEx.EndUpdate();
                            StyleTreeViewClean(tvEx);

                            Console.WriteLine("[Launcher] Direct loader populated " + tvEx.Nodes.Count + " DTCs into tree for ECU " + ecuTypeId);

                        }

                    }

                }

            }

            try

            {

                MethodInfo miSetIcons = statusTv.GetType().GetMethod("SetStatusIcons", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (miSetIcons != null) miSetIcons.Invoke(statusTv, null);

                MethodInfo miRef = statusTv.GetType().GetMethod("RefreshNodes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (miRef != null) miRef.Invoke(statusTv, null);

            }

            catch {}

        }

        catch (Exception ex)

        {

            Console.WriteLine("[Launcher] Direct DTC loader error: " + ex.Message);

        }

    }

}
