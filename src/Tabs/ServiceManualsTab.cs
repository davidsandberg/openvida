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
    static bool _manualsTabConfigured = false;

    static void SetupWorkshopManualsTab(object mObj)

    {

        if (_manualsTabConfigured || mObj == null) return;

        try

        {            Control refForm = GetFieldValue(mObj, "_referenceInformationForm") as Control;

            if (refForm == null)

            {

                try

                {

                    MethodInfo mi = mObj.GetType().GetMethod("GetReferenceInformationFormForm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (mi != null) refForm = mi.Invoke(mObj, null) as Control;

                }

                catch {}

            }

            Control tabPanel = refForm != null ? GetFieldValue(refForm, "_tabPanel") as Control : null;

            Control tabCtrl = refForm != null ? GetFieldValue(refForm, "_tab") as Control : null;

            Control cscCtrl = refForm != null ? GetFieldValue(refForm, "_customerSymptomsCSCRefInfoControl") as Control : null;

            Control dtcCtrl = refForm != null ? GetFieldValue(refForm, "_customerSymptomsDtcRefInfoControl") as Control : null;

            // Container for manual explorer

            ServiceManualsTabControl manualExplorerPnl = new ServiceManualsTabControl();

            manualExplorerPnl.Dock = DockStyle.Fill;

            manualExplorerPnl.Visible = false;

            // Top Search Bar inside manualExplorerPnl

            Panel searchBar = new Panel();

            searchBar.Dock = DockStyle.Top;

            searchBar.Height = 52;

            searchBar.BackColor = Color.FromArgb(248, 250, 252);

            searchBar.Padding = new Padding(12, 10, 12, 10);

            searchBar.BorderStyle = BorderStyle.FixedSingle;

            Label lblSearch = new Label { Text = "🔍 Search Manuals:", AutoSize = true, Location = new Point(12, 15), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };

            TextBox txtSearch = new TextBox { Location = new Point(155, 12), Width = 280, Font = new Font("Segoe UI", 10f) };

            Button btnSearch = new Button { Text = "Search", Location = new Point(445, 11), Width = 80, Height = 29, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };

            btnSearch.FlatAppearance.BorderSize = 0;

            ComboBox cmbGroup = new ComboBox { Location = new Point(535, 12), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };

            cmbGroup.Items.Add("All Function Groups (0-9)");

            cmbGroup.Items.Add("0 - Administration & Special Tools");

            cmbGroup.Items.Add("1 - Maintenance & Service Material");

            cmbGroup.Items.Add("2 - Engine & Equipment");

            cmbGroup.Items.Add("3 - Electrical System");

            cmbGroup.Items.Add("4 - Power Transmission (Gearbox/Clutch)");

            cmbGroup.Items.Add("5 - Brakes");

            cmbGroup.Items.Add("6 - Suspension & Steering");

            cmbGroup.Items.Add("7 - Springs, Shock Absorbers & Wheels");

            cmbGroup.Items.Add("8 - Body & Interior");

            cmbGroup.Items.Add("9 - Miscellaneous");

            cmbGroup.SelectedIndex = 0;

            Label lblStatus = new Label { Text = "Enter query or pick a group...", AutoSize = true, Location = new Point(810, 16), Font = new Font("Segoe UI", 9f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };

            searchBar.Controls.Add(lblSearch);

            searchBar.Controls.Add(txtSearch);

            searchBar.Controls.Add(btnSearch);

            searchBar.Controls.Add(cmbGroup);

            searchBar.Controls.Add(lblStatus);

            // Splitter layout for manuals: Left = Category Tree & Results, Right = Document Viewer

            SplitContainer splitManuals = new SplitContainer();

            splitManuals.Dock = DockStyle.Fill;

            splitManuals.Orientation = Orientation.Vertical;

            splitManuals.SplitterDistance = 380;

            splitManuals.SplitterWidth = 5;

            // Left side: Results TreeView

            TreeView tvManuals = new TreeView();

            tvManuals.Dock = DockStyle.Fill;

            tvManuals.Font = new Font("Segoe UI", 9.5f);

            tvManuals.ShowLines = true;

            tvManuals.ShowPlusMinus = true;

            tvManuals.ShowRootLines = true;

            tvManuals.BorderStyle = BorderStyle.None;

            // Right side: Document Preview Browser

            WebBrowser wbManuals = new WebBrowser();

            wbManuals.Dock = DockStyle.Fill;

            wbManuals.ScriptErrorsSuppressed = true;

            splitManuals.Panel1.Controls.Add(tvManuals);

            splitManuals.Panel2.Controls.Add(wbManuals);

            manualExplorerPnl.Controls.Add(splitManuals);

            manualExplorerPnl.Controls.Add(searchBar);

            splitManuals.BringToFront();

            _manualExplorerPnl = manualExplorerPnl;

            if (_diagnosticsMainPanel != null)

            {

                _manualExplorerPnl.Dock = DockStyle.Fill;

                _diagnosticsMainPanel.Controls.Add(_manualExplorerPnl);

                _manualExplorerPnl.Visible = false;

            }

            Action<string, string> doSearchManuals = (term, grpFilter) =>

            {

                lblStatus.Text = "Searching...";

                tvManuals.Nodes.Clear();

                try

                {

                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))

                    {

                        conn.Open();

                        using (var cmd = conn.CreateCommand())

                        {

                            string sql = @"

                                SELECT TOP 300 

                                    t.chronicleId, 

                                    t.title, 

                                    t.vccNumber, 

                                    t.version, 

                                    t.functionGroup1, 

                                    t.functionGroup2, 

                                    ISNULL(f.title, 'General') AS subGroupTitle

                                FROM TreeItem t

                                LEFT JOIN FunctionGroupText f ON t.functionGroup2 = f.functionGroup

                                WHERE t.title != '' AND t.chronicleId IS NOT NULL AND t.chronicleId != ''";

                            if (!string.IsNullOrEmpty(term))

                            {

                                sql += " AND (t.title LIKE @term OR t.vccNumber LIKE @term OR t.chronicleId LIKE @term)";

                                cmd.Parameters.AddWithValue("@term", "%" + term.Trim() + "%");

                            }

                            if (!string.IsNullOrEmpty(grpFilter) && grpFilter != "All")

                            {

                                sql += " AND t.functionGroup1 = @grp";

                                cmd.Parameters.AddWithValue("@grp", grpFilter);

                            }

                            sql += " ORDER BY t.functionGroup2 ASC, t.title ASC";

                            cmd.CommandText = sql;

                            var docs = new List<ManualDocItem>();

                            using (var r = cmd.ExecuteReader())

                            {

                                while (r.Read())

                                {

                                    docs.Add(new ManualDocItem

                                    {

                                        Id = r["chronicleId"].ToString(),

                                        Title = r["title"].ToString(),

                                        Vcc = r["vccNumber"] != DBNull.Value ? r["vccNumber"].ToString() : "",

                                        Version = r["version"] != DBNull.Value ? r["version"].ToString() : "",

                                        GroupKey = r["functionGroup2"] != DBNull.Value ? r["functionGroup2"].ToString() : "Other",

                                        GroupTitle = r["subGroupTitle"] != DBNull.Value ? r["subGroupTitle"].ToString() : "General"

                                    });

                                }

                            }

                            var grouped = new SortedDictionary<string, List<ManualDocItem>>();

                            foreach (var d in docs)

                            {

                                string k = !string.IsNullOrEmpty(d.GroupKey) ? d.GroupKey : "Other";

                                if (!grouped.ContainsKey(k)) grouped[k] = new List<ManualDocItem>();

                                grouped[k].Add(d);

                            }

                            foreach (var kvp in grouped)

                            {

                                string subTitle = kvp.Value.Count > 0 ? kvp.Value[0].GroupTitle : "General";

                                string grpHeader = kvp.Key + " - " + subTitle;

                                TreeNode grpNode = new TreeNode(grpHeader);

                                foreach (var d in kvp.Value)

                                {

                                    TreeNode docNode = new TreeNode(d.ToString());

                                    docNode.Tag = d.Id;

                                    grpNode.Nodes.Add(docNode);

                                }

                                tvManuals.Nodes.Add(grpNode);

                                grpNode.Expand();

                            }

                            lblStatus.Text = string.Format("Found {0} documents", docs.Count);

                        }

                    }

                }

                catch (Exception exSrch)

                {

                    lblStatus.Text = "Search error: " + exSrch.Message;

                }

            };

            btnSearch.Click += (s, e) =>

            {

                string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";

                doSearchManuals(txtSearch.Text, grpSel);

            };

            txtSearch.KeyDown += (s, e) =>

            {

                if (e.KeyCode == Keys.Enter)

                {

                    e.SuppressKeyPress = true;

                    string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";

                    doSearchManuals(txtSearch.Text, grpSel);

                }

            };

            cmbGroup.SelectedIndexChanged += (s, e) =>

            {

                string grpSel = cmbGroup.SelectedIndex > 0 ? (cmbGroup.SelectedIndex - 1).ToString() : "All";

                doSearchManuals(txtSearch.Text, grpSel);

            };

            Action<string> loadManualDoc = (docId) =>

            {

                if (string.IsNullOrEmpty(docId)) return;

                try

                {

                    string resolved = ResolveDocumentId(docId, null) ?? docId;

                    string html = RenderDocumentHtml(resolved, null);

                    wbManuals.DocumentText = html;

                }

                catch (Exception exDoc)

                {

                    wbManuals.DocumentText = "<html><body><h3>Error loading document:</h3><p>" + exDoc.Message + "</p></body></html>";

                }

            };

            tvManuals.AfterSelect += (s, e) =>

            {

                if (e.Node != null && e.Node.Tag != null)

                {

                    loadManualDoc(e.Node.Tag.ToString());

                }

            };

            tvManuals.NodeMouseDoubleClick += (s, e) =>

            {

                if (e.Node != null && e.Node.Tag != null)

                {

                    string docId = e.Node.Tag.ToString();

                    ShowDocumentWindow(docId, e.Node.Text, _mainLauncherForm);

                }

            };

            wbManuals.Navigating += (sNav, eNav) =>

            {

                try

                {

                    string url = eNav.Url != null ? eNav.Url.ToString() : "";

                    if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))

                    {

                        eNav.Cancel = true;

                        string targetId = url.Substring("vida-doc:".Length);

                        loadManualDoc(targetId);

                    }

                }

                catch {}

            };

            // Initial load of service manuals

            doSearchManuals("", "2");

            if (mObj != null)

            {

                PropertyInfo piNav = mObj.GetType().GetProperty("BrowserNavigation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (piNav != null && piNav.GetValue(mObj, null) == null)

                {

                    piNav.SetValue(mObj, new LauncherBrowserNavigation(_mainLauncherForm), null);

                    Console.WriteLine("[Launcher] Wired BrowserNavigation in SetupWorkshopManualsTab!");

                }

            }

            HashSet<TreeNode> expandedNodes = new HashSet<TreeNode>();

            Action<Control, string> wireRefInfoControl = (ctrl, ctrlTag) =>

            {

                if (ctrl == null || ctrl.Tag as string == "wired_clicks_v4") return;

                ctrl.Tag = "wired_clicks_v4";

                Control statusTv = GetFieldValue(ctrl, "_statusTreeViewControl") as Control;

                if (statusTv == null) return;

                TreeView tvEx = GetFieldValue(statusTv, "_treeViewEx") as TreeView;

                Action<TreeNode, bool> handleNode = (node, isDoubleClick) =>

                {

                    if (node == null) return;

                    // 1. NON-LEAF NODES (branches / categories with child nodes):

                    if (node.Nodes != null && node.Nodes.Count > 0)

                    {

                        if (isDoubleClick)

                        {

                            if (node.IsExpanded) node.Collapse();

                            else node.Expand();

                        }

                        return;

                    }

                    // 2. CSC expandable root category nodes whose children have not been loaded yet:

                    object nodeTag = node.Tag;

                    string nodeId = nodeTag != null ? GetPropValue(nodeTag, "ID") as string : null;

                    object ieObj = nodeTag != null ? GetPropValue(nodeTag, "InformationElement") : null;

                    object statusObj = nodeTag != null ? GetPropValue(nodeTag, "Status") : null;

                    int nodeStatus = statusObj != null ? Convert.ToInt32(statusObj) : -1;

                    int numSymptomId = 0;

                    bool isNumericId = !string.IsNullOrEmpty(nodeId) && int.TryParse(nodeId, out numSymptomId);

                    if (isNumericId && numSymptomId < 10000 && ieObj == null && !expandedNodes.Contains(node))

                    {

                        expandedNodes.Add(node);

                        try

                        {

                            MethodInfo miDbl = ctrl.GetType().GetMethod("_statusTreeViewControl_OnNodeDoubleClick", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (miDbl != null) miDbl.Invoke(ctrl, new object[] { node });

                            node.Expand();

                        }

                        catch (Exception exPop)

                        {

                            Console.WriteLine("[Launcher] CSC populate error: " + exPop.Message);

                        }

                        return;

                    }

                    // Ignore empty category folder headers (e.g. no DTCs or procedures for this symptom)

                    bool isEmptyCategoryHeader = (node.Nodes == null || node.Nodes.Count == 0) && !string.IsNullOrEmpty(node.Text) && (

                        node.Text == "Diagnostic Trouble Codes and Associated Procedures"

                        || node.Text == "Symptom Related Diagnostic Procedures"

                        || node.Text == "Symptom Verifications"

                    );

                    if (isEmptyCategoryHeader)

                    {

                        if (isDoubleClick)

                        {

                            Console.WriteLine("[Launcher] Empty category folder clicked: " + node.Text);

                        }

                        return;

                    }

                    // 3. ACTUAL LEAF NODES (documents / DTCs):

                    // Documents must ONLY open on DOUBLE-CLICK! Single click is navigation only.

                    if (!isDoubleClick)

                    {

                        return;

                    }

                    string docId = null;

                    if (ieObj != null)

                    {

                        docId = GetPropValue(ieObj, "IEId") as string;

                        if (string.IsNullOrEmpty(docId)) docId = GetPropValue(ieObj, "IeId") as string;

                        if (string.IsNullOrEmpty(docId)) docId = GetPropValue(ieObj, "Id") as string;

                        if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(ieObj, "_ieId") as string;

                        if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(ieObj, "ieId") as string;

                    }

                    if (string.IsNullOrEmpty(docId) && nodeTag != null)

                    {

                        object dataObj = GetPropValue(nodeTag, "Data");

                        if (dataObj != null)

                        {

                            object dataIe = GetPropValue(dataObj, "InformationElement");

                            if (dataIe != null)

                            {

                                docId = GetPropValue(dataIe, "IEId") as string;

                                if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataIe, "IeId") as string;

                                if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataIe, "Id") as string;

                                if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataIe, "_ieId") as string;

                                if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataIe, "ieId") as string;

                            }

                        }

                    }

                    if (string.IsNullOrEmpty(docId) && !string.IsNullOrEmpty(nodeId))

                    {

                        if (nodeId.StartsWith("0900", StringComparison.OrdinalIgnoreCase) || nodeId.StartsWith("0800", StringComparison.OrdinalIgnoreCase))

                            docId = nodeId;

                    }

                    if (string.IsNullOrEmpty(docId) && isNumericId)

                    {

                        docId = LookupDocumentIdBySymptomId(numSymptomId);

                    }

                    // Resolve document ID using full resolution engine (TreeItem, Document, DTC regex, title)

                    string finalDocId = ResolveDocumentId(docId, node.Text);

                    Console.WriteLine("[Launcher] Double-clicked leaf document: " + (finalDocId ?? "null") + " (" + node.Text + ")");

                    if (!string.IsNullOrEmpty(finalDocId))

                    {

                        ShowDocumentWindow(finalDocId, node.Text, _mainLauncherForm);

                    }

                    else

                    {

                        Console.WriteLine("[Launcher] Could not resolve document for: " + node.Text);

                    }

                };

                var stc = statusTv as Vcc.Vida.DiagSwdl.GuiComponents.StatusTreeViewControl;

                if (stc != null)

                {

                    // Clear any throwing default delegates on OnNodeDoubleClick

                    try

                    {

                        FieldInfo fiDbl = stc.GetType().GetField("OnNodeDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                        if (fiDbl != null) fiDbl.SetValue(stc, null);

                    }

                    catch {}

                    stc.OnNodeClick += (node) => handleNode(node, false);

                    stc.OnNodeDoubleClick += (node) => handleNode(node, true);

                }

                if (tvEx != null)

                {

                    tvEx.NodeMouseDoubleClick += (sTv, eTv) => handleNode(eTv.Node, true);

                }

            };

            wireRefInfoControl(cscCtrl, "csc");

            wireRefInfoControl(dtcCtrl, "dtc");

            _refFormCtrl = refForm;

            _activeDtcRefControl = dtcCtrl;

            // Ensure cached VehicleProfile is populated into ReferenceInformationForm and its controllers

            if (_cachedVehicleProfile != null)

            {

                object refWs = GetFieldValue(refForm, "_workshopSession");

                if (refWs != null)

                {

                    SetFieldOrProp(refWs, "VehicleProfileObject", _cachedVehicleProfile);

                    SetFieldOrProp(refWs, "_vehicleProfileObject", _cachedVehicleProfile);

                }

                if (dtcCtrl != null)

                {

                    object ws = GetFieldValue(dtcCtrl, "_workshopSession");

                    if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }

                    object wf = GetFieldValue(dtcCtrl, "_dtcFaulttraceWF");

                    if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }

                }

                if (cscCtrl != null)

                {

                    object ws = GetFieldValue(cscCtrl, "_workshopSession");

                    if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }

                    object wf = GetFieldValue(cscCtrl, "_dtcFaulttraceWF");

                    if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }

                }

            }

            if (dtcCtrl != null && dtcCtrl.Tag as string != "dtc_configured_v4")

            {

                dtcCtrl.Tag = "dtc_configured_v4";

                Label lblEcuTitle = new Label { Text = "ECU Module:", AutoSize = true, Location = new Point(400, 68), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };

                ComboBox cbDtcEcu = new ComboBox { Location = new Point(495, 65), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };

                Tuple<string, string>[] dtcEcuList = new Tuple<string, string>[]

                {

                    Tuple.Create("284101", "ECM - Engine Control Module"),

                    Tuple.Create("437601", "TCM - Transmission Control Module"),

                    Tuple.Create("372302", "CEM - Central Electronic Module"),

                    Tuple.Create("593102", "BCM - Brake Control Module"),

                    Tuple.Create("381101", "DIM - Driver Information Module"),

                    Tuple.Create("884701", "SRS - Supplemental Restraint System"),

                    Tuple.Create("871101", "CCM - Climate Control Module"),

                    Tuple.Create("465901", "DEM - Differential Electronic Module"),

                    Tuple.Create("645902", "SAS - Steering Angle Sensor"),

                    Tuple.Create("875401", "CPM - Combustion Pre-heater Module"),

                    Tuple.Create("551401", "PBM - Parking Brake Module"),

                    Tuple.Create("831701", "DDM - Driver Door Module"),

                    Tuple.Create("831703", "PDM - Passenger Door Module"),

                    Tuple.Create("852601", "PSM - Power Seat Module"),

                    Tuple.Create("393901", "ICM - Infotainment Control Module"),

                    Tuple.Create("393204", "IAM - Integrated Audio Module"),

                    Tuple.Create("393202", "AUD - Audio Module"),

                    Tuple.Create("366901", "KVM - Keyless Vehicle Module"),

                    Tuple.Create("366902", "PAM - Parking Assistance Module"),

                    Tuple.Create("892901", "TRM - Trailer Module"),

                    Tuple.Create("832901", "POT - Power Operated Tailgate"),

                    Tuple.Create("352902", "HCM - Headlamp Control Module"),

                    Tuple.Create("366905", "FSM - Forward Sensing Module"),

                    Tuple.Create("645903", "PSCM - Power Steering Control Module")

                };

                foreach (var item in dtcEcuList)

                {

                    cbDtcEcu.Items.Add(item.Item2);

                }

                Action<string, string> loadDtcForEcu = (ecuTypeId, ecuName) =>

                {

                    if (dtcCtrl == null) return;

                    Console.WriteLine("[Launcher] DTC Ref Info: Loading ECU " + ecuName + " (" + ecuTypeId + ")");

                    if (_cachedVehicleProfile != null)

                    {

                        object ws = GetFieldValue(dtcCtrl, "_workshopSession");

                        if (ws != null) { SetFieldOrProp(ws, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(ws, "_vehicleProfileObject", _cachedVehicleProfile); }

                        object wf = GetFieldValue(dtcCtrl, "_dtcFaulttraceWF");

                        if (wf != null) { object wfWs = GetFieldValue(wf, "_workshopSession"); if (wfWs != null) { SetFieldOrProp(wfWs, "VehicleProfileObject", _cachedVehicleProfile); SetFieldOrProp(wfWs, "_vehicleProfileObject", _cachedVehicleProfile); } }

                    }

                    bool loaded = false;

                    try

                    {

                        MethodInfo miNav = dtcCtrl.GetType().GetMethod("_navImage_HotspotClicked", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (miNav != null)

                        {

                            var navArgs = new Vcc.Vida.DiagSwdl.UI.Components.NavImageEventArgs(ecuTypeId, 1);

                            miNav.Invoke(dtcCtrl, new object[] { dtcCtrl, navArgs });

                        }

                    }

                    catch (Exception exNav)

                    {

                        Console.WriteLine("[Launcher] DTC _navImage_HotspotClicked: " + exNav.Message);

                    }

                    Control statusTv = GetFieldValue(dtcCtrl, "_statusTreeViewControl") as Control;

                    TreeView tvEx = statusTv != null ? GetFieldValue(statusTv, "_treeViewEx") as TreeView : null;

                    if (tvEx != null && tvEx.Nodes.Count > 0)

                    {

                        loaded = true;

                        Console.WriteLine("[Launcher] Volvo WF populated " + tvEx.Nodes.Count + " nodes for ECU " + ecuTypeId);

                    }

                    if (!loaded && statusTv != null)

                    {

                        LoadDtcsFromDbDirectly(statusTv, ecuTypeId, TargetProfile);

                    }

                };

                bool updatingDtcEcu = false;

                Action<string> selectDtcEcuById = (targetId) =>

                {

                    for (int k = 0; k < dtcEcuList.Length; k++)

                    {

                        if (dtcEcuList[k].Item1 == targetId)

                        {

                            updatingDtcEcu = true;

                            cbDtcEcu.SelectedIndex = k;

                            updatingDtcEcu = false;

                            loadDtcForEcu(targetId, dtcEcuList[k].Item2);

                            break;

                        }

                    }

                };

                cbDtcEcu.SelectedIndexChanged += (sEcu, eEcu) =>

                {

                    if (updatingDtcEcu) return;

                    if (cbDtcEcu.SelectedIndex >= 0 && cbDtcEcu.SelectedIndex < dtcEcuList.Length)

                    {

                        string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;

                        string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;

                        loadDtcForEcu(selId, selName);

                    }

                };

                dtcCtrl.Controls.Add(lblEcuTitle);

                dtcCtrl.Controls.Add(cbDtcEcu);

                lblEcuTitle.BringToFront();

                cbDtcEcu.BringToFront();

                _onDtcRefHotspotClicked = (clickedId) =>

                {

                    Console.WriteLine("[Launcher] DTC Ref Info hotspot clicked for ECU: " + clickedId);

                    selectDtcEcuById(clickedId);

                };

                // Hook tab switching to auto-load DTCs when DTC tab becomes active

                if (tabCtrl != null)

                {

                    Control dtcTab = GetFieldValue(refForm, "_dtcBasedRefInfoTab") as Control;

                    var ftc = tabCtrl as Vcc.Vida.DiagSwdl.GuiComponents.FlatTabControl;

                    if (ftc != null)

                    {

                        ftc.SelectedTabChanged += (newPage, oldPage) =>

                        {

                            try

                            {

                                if (newPage == dtcTab)

                                {

                                    Console.WriteLine("[Launcher] Switched to DTC Reference Information Tab");

                                    if (cbDtcEcu.SelectedIndex < 0) cbDtcEcu.SelectedIndex = 0;

                                    else

                                    {

                                        string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;

                                        string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;

                                        loadDtcForEcu(selId, selName);

                                    }

                                }

                            }

                            catch {}

                        };

                    }

                    else if (tabCtrl is TabControl)

                    {

                        ((TabControl)tabCtrl).SelectedIndexChanged += (sTab, eTab) =>

                        {

                            try

                            {

                                if (cbDtcEcu.SelectedIndex < 0) cbDtcEcu.SelectedIndex = 0;

                                else

                                {

                                    string selId = dtcEcuList[cbDtcEcu.SelectedIndex].Item1;

                                    string selName = dtcEcuList[cbDtcEcu.SelectedIndex].Item2;

                                    loadDtcForEcu(selId, selName);

                                }

                            }

                            catch {}

                        };

                    }

                }

                // Auto-select ECM initially so Related DTCs are immediately loaded

                if (cbDtcEcu.Items.Count > 0)

                {

                    cbDtcEcu.SelectedIndex = 0;

                }

            }

            RegisterDummyIsoViewHotspot();

            _manualsTabConfigured = true;

RegisterDummyIsoViewHotspot();

            _manualsTabConfigured = true;

            Console.WriteLine("[Launcher] Successfully embedded Workshop Service Manuals Explorer into ReferenceInformationForm!");

        }

        catch (Exception ex)

        {

            Console.WriteLine("[Launcher] SetupWorkshopManualsTab error: " + ex);

        }

    }

}
