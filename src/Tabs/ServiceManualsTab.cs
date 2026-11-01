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
    public static Action<string> _navigateToManualFgAction = null;
    static WebBrowser _wbRefDocCsc = null;
    static Label _lblRefDocTitleCsc = null;
    static WebBrowser _wbRefDocDtc = null;
    static Label _lblRefDocTitleDtc = null;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    const int TVM_SETIMAGELIST = 0x1109;
    const int TVSIL_NORMAL = 0;
    const int TVSIL_STATE = 2;

    static ImageList _cleanTreeDummyIl = null;

    static ImageList GetCleanTreeDummyImageList()
    {
        if (_cleanTreeDummyIl == null)
        {
            _cleanTreeDummyIl = new ImageList();
            _cleanTreeDummyIl.ImageSize = new Size(1, 1);
            IntPtr h = _cleanTreeDummyIl.Handle; // ensure native handle created
        }
        return _cleanTreeDummyIl;
    }

    public static void StyleTreeViewClean(TreeView tv)
    {
        if (tv == null) return;
        try
        {
            ImageList dummyIl = GetCleanTreeDummyImageList();
            tv.ImageList = dummyIl;
            tv.StateImageList = null;
            if (tv.IsHandleCreated)
            {
                SendMessage(tv.Handle, TVM_SETIMAGELIST, (IntPtr)TVSIL_STATE, dummyIl.Handle);
                SendMessage(tv.Handle, TVM_SETIMAGELIST, (IntPtr)TVSIL_NORMAL, IntPtr.Zero);
            }
            tv.Font = new Font("Segoe UI", 9.0f);
            tv.FullRowSelect = true;
            tv.ShowLines = true;
            tv.ShowPlusMinus = true;
            tv.ShowRootLines = true;
            tv.BorderStyle = BorderStyle.None;
            tv.HideSelection = false;
            tv.BackColor = Color.White;
            tv.BackgroundImage = null;
        }
        catch {}
    }

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

            // Top Search & Filter Bar
            Panel searchBar = new Panel();
            searchBar.Dock = DockStyle.Top;
            searchBar.Height = 52;
            searchBar.BackColor = Color.FromArgb(248, 250, 252);
            searchBar.Padding = new Padding(12, 10, 12, 10);
            searchBar.BorderStyle = BorderStyle.FixedSingle;

            Label lblSearch = new Label { Text = "Search Manuals:", AutoSize = true, Location = new Point(12, 15), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };
            TextBox txtSearch = new TextBox { Location = new Point(155, 12), Width = 300, Font = new Font("Segoe UI", 10f) };
            Button btnSearch = new Button { Text = "Search", Location = new Point(465, 11), Width = 80, Height = 29, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSearch.FlatAppearance.BorderSize = 0;

            Button btnReset = new Button { Text = "Reset", Location = new Point(555, 11), Width = 75, Height = 29, BackColor = Color.FromArgb(220, 225, 230), ForeColor = Color.FromArgb(50, 60, 70), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand };
            btnReset.FlatAppearance.BorderSize = 0;

            Label lblStatus = new Label { Text = "Loading Volvo service manuals...", AutoSize = true, Location = new Point(645, 16), Font = new Font("Segoe UI", 9f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };

            searchBar.Controls.Add(lblSearch);
            searchBar.Controls.Add(txtSearch);
            searchBar.Controls.Add(btnSearch);
            searchBar.Controls.Add(btnReset);
            searchBar.Controls.Add(lblStatus);

            // Splitter layout for manuals: Left = Category Tree & Procedures, Right = Document Viewer
            SplitContainer splitManuals = new SplitContainer();
            splitManuals.Size = new Size(1200, 800);
            splitManuals.Dock = DockStyle.Fill;
            splitManuals.Orientation = Orientation.Vertical;
            splitManuals.FixedPanel = FixedPanel.Panel1;
            splitManuals.Panel1MinSize = 100;
            splitManuals.Panel2MinSize = 100;
            try { splitManuals.SplitterDistance = 300; } catch {}
            splitManuals.SplitterWidth = 5;

            // Keep left navigation balanced at ~300px on resize
            manualExplorerPnl.SizeChanged += (s, e) =>
            {
                try
                {
                    if (manualExplorerPnl.Width > 500)
                    {
                        splitManuals.SplitterDistance = 300;
                    }
                }
                catch {}
            };
            manualExplorerPnl.VisibleChanged += (s, e) =>
            {
                try
                {
                    if (manualExplorerPnl.Visible && manualExplorerPnl.Width > 500)
                    {
                        splitManuals.SplitterDistance = 300;
                    }
                }
                catch {}
            };

            // Left side: Sub-split into Function Groups Tree (Top) and Procedures List (Bottom)
            SplitContainer splitManualNav = new SplitContainer();
            splitManualNav.Size = new Size(300, 800);
            splitManualNav.Dock = DockStyle.Fill;
            splitManualNav.Orientation = Orientation.Horizontal;
            splitManualNav.Panel1MinSize = 50;
            splitManualNav.Panel2MinSize = 50;
            try { splitManualNav.SplitterDistance = 280; } catch {}
            splitManualNav.SplitterWidth = 5;
            splitManuals.Panel1.Controls.Add(splitManualNav);

            // Top: Function Group Hierarchy Tree
            Panel pnlFgHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(235, 240, 245), BorderStyle = BorderStyle.FixedSingle };
            Label lblFgTitle = new Label { Text = "Function Group Tree", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            pnlFgHeader.Controls.Add(lblFgTitle);
            splitManualNav.Panel1.Controls.Add(pnlFgHeader);

            TreeView tvGroups = new TreeView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.0f), HideSelection = false, FullRowSelect = true, ShowLines = true, BorderStyle = BorderStyle.None };
            splitManualNav.Panel1.Controls.Add(tvGroups);
            tvGroups.BringToFront();

            // Bottom: Document List
            Panel pnlDocsHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(235, 240, 245), BorderStyle = BorderStyle.FixedSingle };
            Label lblDocsTitle = new Label { Text = "Service Documents & Bulletins", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            pnlDocsHeader.Controls.Add(lblDocsTitle);
            splitManualNav.Panel2.Controls.Add(pnlDocsHeader);

            TreeView tvDocs = new TreeView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), HideSelection = false, FullRowSelect = true, ShowLines = true, BorderStyle = BorderStyle.None };
            splitManualNav.Panel2.Controls.Add(tvDocs);
            tvDocs.BringToFront();

            // Right side: Document Preview Browser
            WebBrowser wbManuals = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };
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

            tvDocs.AfterSelect += (s, e) =>
            {
                if (e.Node != null && e.Node.Tag != null)
                {
                    loadManualDoc(e.Node.Tag.ToString());
                }
            };

            tvDocs.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null && e.Node.Tag != null)
                {
                    string docId = e.Node.Tag.ToString();
                    loadManualDoc(docId);
                }
            };

            // Query documents for a function group
            Action<string, string> loadDocsForGroup = (fgCode, fgTitle) =>
            {
                tvDocs.BeginUpdate();
                tvDocs.Nodes.Clear();
                lblDocsTitle.Text = string.Format("Documents for {0} - {1}", fgCode, fgTitle);
                lblStatus.Text = "Loading documents...";

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            string sql = "SELECT TOP 300 t.chronicleId, t.title, t.vccNumber, t.version, t.functionGroup1, t.functionGroup2, t.functionGroup3 " +
                                         "FROM TreeItem t " +
                                         "WHERE t.title != '' AND t.chronicleId IS NOT NULL AND t.chronicleId != '' ";

                            if (fgCode.Length == 1)
                            {
                                sql += "AND t.functionGroup1 = @fg ";
                                cmd.Parameters.AddWithValue("@fg", fgCode);
                            }
                            else if (fgCode.Length == 2)
                            {
                                sql += "AND t.functionGroup2 = @fg ";
                                cmd.Parameters.AddWithValue("@fg", fgCode);
                            }
                            else
                            {
                                sql += "AND (t.functionGroup3 = @fg OR (t.functionGroup2 = @fg2 AND (t.functionGroup3 IS NULL OR t.functionGroup3 = ''))) ";
                                cmd.Parameters.AddWithValue("@fg", fgCode);
                                cmd.Parameters.AddWithValue("@fg2", fgCode.Substring(0, 2));
                            }
                            sql += "ORDER BY t.title ASC";
                            cmd.CommandText = sql;

                            int count = 0;
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string cid = r["chronicleId"].ToString();
                                    string title = r["title"].ToString();
                                    string vcc = r["vccNumber"] != DBNull.Value ? r["vccNumber"].ToString() : "";
                                    string ver = r["version"] != DBNull.Value ? r["version"].ToString() : "";

                                    string label = title;
                                    if (!string.IsNullOrEmpty(vcc)) label += " [" + vcc + "]";

                                    TreeNode docNode = new TreeNode(label);
                                    docNode.Tag = cid;
                                    tvDocs.Nodes.Add(docNode);
                                    count++;
                                }
                            }
                            lblStatus.Text = string.Format("{0} documents available for {1}", count, fgTitle);
                        }
                    }
                }
                catch (Exception exLd) { lblStatus.Text = "Load error: " + exLd.Message; }
                tvDocs.EndUpdate();

                if (tvDocs.Nodes.Count > 0)
                {
                    tvDocs.SelectedNode = tvDocs.Nodes[0];
                }
                else
                {
                    wbManuals.DocumentText = "<html><body style='font-family:Segoe UI;padding:24px;color:#64748b;text-align:center;'><h3>No documents in this specific sub-group</h3><p>Try selecting the parent function group above or use global search.</p></body></html>";
                }
            };

            // Global search
            Action<string> doSearchManuals = (term) =>
            {
                if (string.IsNullOrEmpty(term)) return;
                tvDocs.BeginUpdate();
                tvDocs.Nodes.Clear();
                lblDocsTitle.Text = string.Format("Search Results for '{0}'", term);
                lblStatus.Text = "Searching...";

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 300 t.chronicleId, t.title, t.vccNumber, t.version, t.functionGroup1, t.functionGroup2, t.functionGroup3 " +
                                              "FROM TreeItem t " +
                                              "WHERE t.title != '' AND t.chronicleId IS NOT NULL AND t.chronicleId != '' " +
                                              "AND (t.title LIKE @term OR t.vccNumber LIKE @term OR t.chronicleId LIKE @term) " +
                                              "ORDER BY t.functionGroup1, t.functionGroup2, t.title";
                            cmd.Parameters.AddWithValue("@term", "%" + term.Trim() + "%");

                            int count = 0;
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string cid = r["chronicleId"].ToString();
                                    string title = r["title"].ToString();
                                    string vcc = r["vccNumber"] != DBNull.Value ? r["vccNumber"].ToString() : "";
                                    string fg1 = r["functionGroup1"] != DBNull.Value ? r["functionGroup1"].ToString() : "";
                                    string fg2 = r["functionGroup2"] != DBNull.Value ? r["functionGroup2"].ToString() : "";
                                    string fg3 = r["functionGroup3"] != DBNull.Value ? r["functionGroup3"].ToString() : "";

                                    string fgDisplay = !string.IsNullOrEmpty(fg3) ? fg3 : (!string.IsNullOrEmpty(fg2) ? fg2 : fg1);
                                    string label = title;
                                    if (!string.IsNullOrEmpty(fgDisplay)) label = "[" + fgDisplay + "] " + label;
                                    if (!string.IsNullOrEmpty(vcc)) label += " (" + vcc + ")";

                                    TreeNode docNode = new TreeNode(label);
                                    docNode.Tag = cid;
                                    tvDocs.Nodes.Add(docNode);
                                    count++;
                                }
                            }
                            lblStatus.Text = string.Format("Found {0} matching documents", count);
                        }
                    }
                }
                catch (Exception exSrch) { lblStatus.Text = "Search error: " + exSrch.Message; }
                tvDocs.EndUpdate();

                if (tvDocs.Nodes.Count > 0)
                {
                    tvDocs.SelectedNode = tvDocs.Nodes[0];
                }
            };

            btnSearch.Click += (s, e) => doSearchManuals(txtSearch.Text);
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; doSearchManuals(txtSearch.Text); } };
            btnReset.Click += (s, e) =>
            {
                txtSearch.Clear();
                if (tvGroups.SelectedNode != null)
                {
                    string fg = tvGroups.SelectedNode.Tag as string ?? "";
                    loadDocsForGroup(fg, tvGroups.SelectedNode.Text);
                }
                else
                {
                    loadDocsForGroup("2", "Engine with mountings and equipment");
                }
            };

            // Build Function Group Tree from FunctionGroupText
            Action LoadFunctionGroupTree = () =>
            {
                tvGroups.BeginUpdate();
                tvGroups.Nodes.Clear();

                Dictionary<string, string> fgTitles = new Dictionary<string, string>();
                List<string> sortedKeys = new List<string>();

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=servicerep_en-US;Integrated Security=True"))
                    {
                        conn.Open();

                        // 1. Get active function groups from TreeItem
                        HashSet<string> activeFgs = new HashSet<string>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT DISTINCT functionGroup1, functionGroup2, functionGroup3 FROM TreeItem WHERE chronicleId IS NOT NULL AND chronicleId != ''";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string f1 = r.IsDBNull(0) ? "" : r.GetString(0).Trim();
                                    string f2 = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string f3 = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    if (f1.Length > 0) activeFgs.Add(f1);
                                    if (f2.Length > 0) { activeFgs.Add(f2); activeFgs.Add(f2.Substring(0, 1)); }
                                    if (f3.Length > 0) { activeFgs.Add(f3); activeFgs.Add(f3.Substring(0, 2)); activeFgs.Add(f3.Substring(0, 1)); }
                                }
                            }
                        }

                        // 2. Fetch all titles from FunctionGroupText
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT functionGroup, title FROM FunctionGroupText ORDER BY functionGroup";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string fg = r.GetString(0).Trim();
                                    string title = r.GetString(1).Trim();
                                    fgTitles[fg] = title;
                                    sortedKeys.Add(fg);
                                }
                            }
                        }

                        // 3. Build tree
                        Dictionary<string, TreeNode> nodeMap = new Dictionary<string, TreeNode>();
                        foreach (string fg in sortedKeys)
                        {
                            string title = fgTitles.ContainsKey(fg) ? fgTitles[fg] : ("Group " + fg);
                            TreeNode node = new TreeNode(fg + " - " + title);
                            node.Tag = fg;

                            if (fg.Length == 1)
                            {
                                tvGroups.Nodes.Add(node);
                                nodeMap[fg] = node;
                            }
                            else if (fg.Length == 2)
                            {
                                string pKey = fg.Substring(0, 1);
                                TreeNode pNode;
                                if (nodeMap.TryGetValue(pKey, out pNode)) pNode.Nodes.Add(node);
                                else tvGroups.Nodes.Add(node);
                                nodeMap[fg] = node;
                            }
                            else if (fg.Length >= 3)
                            {
                                string pKey = fg.Substring(0, 2);
                                TreeNode pNode;
                                if (nodeMap.TryGetValue(pKey, out pNode)) pNode.Nodes.Add(node);
                                else
                                {
                                    string pKey1 = fg.Substring(0, 1);
                                    if (nodeMap.TryGetValue(pKey1, out pNode)) pNode.Nodes.Add(node);
                                    else tvGroups.Nodes.Add(node);
                                }
                                nodeMap[fg] = node;
                            }
                        }
                    }
                }
                catch (Exception exTree) { Console.WriteLine("[Launcher] Load FG tree error: " + exTree.Message); }
                tvGroups.EndUpdate();
            };

            tvGroups.AfterSelect += (s, e) =>
            {
                if (e.Node != null && e.Node.Tag != null)
                {
                    string fg = e.Node.Tag.ToString();
                    loadDocsForGroup(fg, e.Node.Text);
                }
            };

            // Wire cross-tab navigation from Parts Catalog
            _navigateToManualFgAction = (targetFg) =>
            {
                if (string.IsNullOrEmpty(targetFg)) return;
                try
                {
                    TreeNode match = null;
                    Action<TreeNodeCollection> searchNodes = null;
                    searchNodes = (nodes) =>
                    {
                        foreach (TreeNode n in nodes)
                        {
                            if (n.Tag != null && n.Tag.ToString() == targetFg) { match = n; return; }
                            if (n.Nodes.Count > 0) searchNodes(n.Nodes);
                            if (match != null) return;
                        }
                    };
                    searchNodes(tvGroups.Nodes);

                    if (match == null && targetFg.Length > 2)
                    {
                        string pfx2 = targetFg.Substring(0, 2);
                        searchNodes(tvGroups.Nodes);
                    }

                    if (match != null)
                    {
                        tvGroups.SelectedNode = match;
                        match.EnsureVisible();
                    }
                    else
                    {
                        loadDocsForGroup(targetFg, "Group " + targetFg);
                    }
                }
                catch (Exception exNav) { Console.WriteLine("[Launcher] Navigation to manual error: " + exNav.Message); }
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

            LoadFunctionGroupTree();
            // Select Group 2 by default
            foreach (TreeNode n in tvGroups.Nodes)
            {
                if (n.Tag != null && n.Tag.ToString() == "2")
                {
                    n.Expand();
                    tvGroups.SelectedNode = n;
                    break;
                }
            }

            if (mObj != null)

            {

                PropertyInfo piNav = mObj.GetType().GetProperty("BrowserNavigation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (piNav != null && piNav.GetValue(mObj, null) == null)

                {

                    piNav.SetValue(mObj, new LauncherBrowserNavigation(_mainLauncherForm), null);

                    Console.WriteLine("[Launcher] Wired BrowserNavigation in SetupWorkshopManualsTab!");

                }

            }

            Action<WebBrowser, Label> attachRefBrowserNav = (wb, lblTitle) =>
            {
                wb.Navigating += (sNav, eNav) =>
                {
                    try
                    {
                        string url = eNav.Url != null ? eNav.Url.ToString() : "";
                        if (url.StartsWith("vida-doc:", StringComparison.OrdinalIgnoreCase))
                        {
                            eNav.Cancel = true;
                            string target = url.Substring("vida-doc:".Length);
                            string anchor = "";
                            int hashIdx = target.IndexOf('#');
                            if (hashIdx >= 0) anchor = target.Substring(hashIdx + 1).Trim();

                            if (!string.IsNullOrEmpty(anchor) && wb.Document != null)
                            {
                                var elem = wb.Document.GetElementById(anchor);
                                if (elem != null) { elem.ScrollIntoView(true); return; }
                            }

                            string targetDoc = hashIdx >= 0 ? target.Substring(0, hashIdx) : target;
                            string subTitle = GetDocumentTitle(targetDoc);
                            if (!string.IsNullOrEmpty(subTitle) && lblTitle != null) lblTitle.Text = subTitle;
                            wb.DocumentText = RenderDocumentHtml(target, null);
                        }
                        else if (url.StartsWith("smarttool:", StringComparison.OrdinalIgnoreCase))
                        {
                            eNav.Cancel = true;
                            Control ctrlNav = sNav as Control;
                            Form targetForm = ctrlNav != null ? ctrlNav.FindForm() : _mainLauncherForm;
                            HandleSmartToolNavigation(url, targetForm);
                        }
                    }
                    catch {}
                };
            };

            HashSet<TreeNode> expandedNodes = new HashSet<TreeNode>();
            Action<Control, string> wireRefInfoControl = (ctrl, ctrlTag) =>
            {
                if (ctrl == null || ctrl.Tag as string == "wired_clicks_v5") return;
                ctrl.Tag = "wired_clicks_v5";

                Control statusTv = GetFieldValue(ctrl, "_statusTreeViewControl") as Control;
                if (statusTv == null) return;
                try
                {
                    FieldInfo fiIl = statusTv.GetType().GetField("il", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fiIl != null) fiIl.SetValue(statusTv, GetCleanTreeDummyImageList());
                    statusTv.BackgroundImage = null;
                }
                catch {}
                ctrl.BackgroundImage = null;

                TreeView tvEx = GetFieldValue(statusTv, "_treeViewEx") as TreeView;

                // Style TreeView clean, matching Parts Catalog and Service Manuals
                if (tvEx != null)
                {
                    StyleTreeViewClean(tvEx);
                    tvEx.HandleCreated += (sH, eH) => StyleTreeViewClean(tvEx);
                    tvEx.AfterExpand += (sExp, eExp) => StyleTreeViewClean(tvEx);
                }

                // Layout with embedded Document Viewer in the main window
                if (ctrlTag == "csc")
                {
                    Control pnlTree = GetFieldValue(ctrl, "_pnlTreeView") as Control;
                    if (pnlTree != null) pnlTree.BackgroundImage = null;
                    if (pnlTree != null && ctrl.Controls.Contains(pnlTree))
                    {
                        ctrl.Controls.Remove(pnlTree);
                        pnlTree.Dock = DockStyle.Fill;

                        SplitContainer splitCsc = new SplitContainer();
                        splitCsc.Size = new Size(1200, 800);
                        splitCsc.Dock = DockStyle.Fill;
                        splitCsc.Orientation = Orientation.Vertical;
                        splitCsc.FixedPanel = FixedPanel.Panel1;
                        splitCsc.Panel1MinSize = 100;
                        splitCsc.Panel2MinSize = 100;
                        try { splitCsc.SplitterDistance = 340; } catch {}
                        splitCsc.SplitterWidth = 5;

                        Panel pnlTreeHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(235, 240, 245), BorderStyle = BorderStyle.FixedSingle };
                        Label lblTreeTitle = new Label { Text = "Symptom Categories", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
                        pnlTreeHeader.Controls.Add(lblTreeTitle);

                        splitCsc.Panel1.Controls.Add(pnlTree);
                        splitCsc.Panel1.Controls.Add(pnlTreeHeader);
                        pnlTree.BringToFront();

                        Panel pnlDocViewer = new Panel { Dock = DockStyle.Fill };
                        WebBrowser wbRefDoc = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };

                        Panel pnlDocHeader = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.FixedSingle };
                        Label lblDocTitle = new Label { Text = "Select a symptom to view reference document", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };
                        Button btnPrint = new Button { Text = "Print", Dock = DockStyle.Right, Width = 70, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), BackColor = Color.FromArgb(230, 235, 240), Cursor = Cursors.Hand };
                        btnPrint.FlatAppearance.BorderSize = 0;
                        btnPrint.Click += (sPrint, ePrint) => { try { wbRefDoc.ShowPrintDialog(); } catch {} };

                        pnlDocHeader.Controls.Add(lblDocTitle);
                        pnlDocHeader.Controls.Add(btnPrint);

                        pnlDocViewer.Controls.Add(wbRefDoc);
                        pnlDocViewer.Controls.Add(pnlDocHeader);
                        wbRefDoc.BringToFront();

                        splitCsc.Panel2.Controls.Add(pnlDocViewer);

                        ctrl.Controls.Add(splitCsc);
                        splitCsc.BringToFront();

                        _wbRefDocCsc = wbRefDoc;
                        _lblRefDocTitleCsc = lblDocTitle;
                        attachRefBrowserNav(wbRefDoc, lblDocTitle);
                    }
                }
                else if (ctrlTag == "dtc")
                {
                    Control pnlList = statusTv.Parent;
                    if (pnlList != null && pnlList.Parent != null)
                    {
                        Control parentContainer = pnlList.Parent;
                        parentContainer.Controls.Remove(pnlList);

                        SplitContainer splitDtc = new SplitContainer();
                        splitDtc.Size = new Size(1200, 800);
                        splitDtc.Dock = DockStyle.Fill;
                        splitDtc.Orientation = Orientation.Vertical;
                        splitDtc.FixedPanel = FixedPanel.Panel1;
                        splitDtc.Panel1MinSize = 100;
                        splitDtc.Panel2MinSize = 100;
                        try { splitDtc.SplitterDistance = 460; } catch {}
                        splitDtc.SplitterWidth = 5;

                        pnlList.Dock = DockStyle.Fill;
                        splitDtc.Panel1.Controls.Add(pnlList);

                        Panel pnlDocViewer = new Panel { Dock = DockStyle.Fill };
                        WebBrowser wbRefDoc = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };

                        Panel pnlDocHeader = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.FixedSingle };
                        Label lblDocTitle = new Label { Text = "Select a DTC to view fault tracing document", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 48, 87) };
                        Button btnPrint = new Button { Text = "Print", Dock = DockStyle.Right, Width = 70, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), BackColor = Color.FromArgb(230, 235, 240), Cursor = Cursors.Hand };
                        btnPrint.FlatAppearance.BorderSize = 0;
                        btnPrint.Click += (sPrint, ePrint) => { try { wbRefDoc.ShowPrintDialog(); } catch {} };

                        pnlDocHeader.Controls.Add(lblDocTitle);
                        pnlDocHeader.Controls.Add(btnPrint);

                        pnlDocViewer.Controls.Add(wbRefDoc);
                        pnlDocViewer.Controls.Add(pnlDocHeader);
                        wbRefDoc.BringToFront();

                        splitDtc.Panel2.Controls.Add(pnlDocViewer);

                        parentContainer.Controls.Add(splitDtc);
                        splitDtc.BringToFront();

                        _wbRefDocDtc = wbRefDoc;
                        _lblRefDocTitleDtc = lblDocTitle;
                        attachRefBrowserNav(wbRefDoc, lblDocTitle);
                    }
                }

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
                            if (tvEx != null) StyleTreeViewClean(tvEx);
                        }
                        catch (Exception exPop)
                        {
                            Console.WriteLine("[Launcher] CSC populate error: " + exPop.Message);
                        }
                        return;
                    }

                    // Ignore empty category folder headers
                    bool isEmptyCategoryHeader = (node.Nodes == null || node.Nodes.Count == 0) && !string.IsNullOrEmpty(node.Text) && (
                        node.Text == "Diagnostic Trouble Codes and Associated Procedures"
                        || node.Text == "Symptom Related Diagnostic Procedures"
                        || node.Text == "Symptom Verifications"
                    );
                    if (isEmptyCategoryHeader)
                    {
                        if (isDoubleClick) Console.WriteLine("[Launcher] Empty category folder clicked: " + node.Text);
                        return;
                    }

                    // 3. ACTUAL LEAF NODES (documents / DTCs):
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
                            docId = GetPropValue(dataObj, "IEId") as string;
                            if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataObj, "IeId") as string;
                            if (string.IsNullOrEmpty(docId)) docId = GetPropValue(dataObj, "Id") as string;
                            if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataObj, "_ieId") as string;
                            if (string.IsNullOrEmpty(docId)) docId = GetFieldValue(dataObj, "ieId") as string;

                            if (string.IsNullOrEmpty(docId))
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
                    Console.WriteLine("[Launcher] Reference Info document node: " + (finalDocId ?? "null") + " (" + node.Text + ") [dbl=" + isDoubleClick + "]");

                    if (!string.IsNullOrEmpty(finalDocId))
                    {
                        string html = RenderDocumentHtml(finalDocId, node.Text);
                        if (ctrlTag == "csc" && _wbRefDocCsc != null)
                        {
                            if (_lblRefDocTitleCsc != null) _lblRefDocTitleCsc.Text = node.Text;
                            _wbRefDocCsc.DocumentText = html;
                        }
                        else if (ctrlTag == "dtc" && _wbRefDocDtc != null)
                        {
                            if (_lblRefDocTitleDtc != null) _lblRefDocTitleDtc.Text = node.Text;
                            _wbRefDocDtc.DocumentText = html;
                        }
                        else
                        {
                            ShowDocumentWindow(finalDocId, node.Text, _mainLauncherForm);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Launcher] Could not resolve document for: " + node.Text);
                    }
                };

                var stc = statusTv as Vcc.Vida.DiagSwdl.GuiComponents.StatusTreeViewControl;
                if (stc != null)
                {
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
                    tvEx.AfterSelect += (sTv, eTv) => { if (eTv.Node != null) handleNode(eTv.Node, false); };
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
