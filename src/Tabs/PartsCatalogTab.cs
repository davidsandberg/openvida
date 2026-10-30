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
    static void SetupPartsCatalogTab(object mObj)
    {
        if (_partsTabConfigured || mObj == null) return;
        try
        {
            PartsCatalogTabControl partsPnl = new PartsCatalogTabControl();
            partsPnl.Dock = DockStyle.Fill;
            partsPnl.BackColor = Color.FromArgb(245, 247, 250);
            partsPnl.Font = new Font("Segoe UI", 9.0f);

            // 1. Top Filter & Search Header
            Panel headerPnl = new Panel();
            headerPnl.Dock = DockStyle.Top;
            headerPnl.Height = 52;
            headerPnl.BackColor = Color.FromArgb(240, 243, 246);
            headerPnl.BorderStyle = BorderStyle.FixedSingle;
            partsPnl.Controls.Add(headerPnl);

            Label lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(12, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblModel);

            ComboBox cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 13), Width = 150, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboModel);

            CheckBox chkActiveEngine = new CheckBox { Text = "Active Car: D5204T2 (2.0D) Only", AutoSize = true, Location = new Point(222, 15), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 204), Checked = true };
            headerPnl.Controls.Add(chkActiveEngine);

            Label lblSearch = new Label { Text = "Search:", AutoSize = true, Location = new Point(445, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblSearch);

            TextBox txtSearch = new TextBox { Location = new Point(500, 13), Width = 210, Font = new Font("Segoe UI", 9.5f) };
            headerPnl.Controls.Add(txtSearch);

            Button btnSearch = new Button { Text = "🔍 Search", Location = new Point(718, 12), Width = 85, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnSearch.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnSearch);

            Button btnReset = new Button { Text = "✕ Reset", Location = new Point(809, 12), Width = 75, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 225, 230), ForeColor = Color.FromArgb(50, 60, 70), Cursor = Cursors.Hand };
            btnReset.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnReset);

            Label lblStatus = new Label { Text = "Loading parts database...", Location = new Point(895, 16), AutoSize = true, Font = new Font("Segoe UI", 9.0f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            headerPnl.Controls.Add(lblStatus);

            // 2. Main SplitContainer
            SplitContainer splitParts = new SplitContainer();
            splitParts.Dock = DockStyle.Fill;
            splitParts.Orientation = Orientation.Vertical;
            splitParts.SplitterDistance = 420;
            splitParts.SplitterWidth = 6;
            partsPnl.Controls.Add(splitParts);
            splitParts.BringToFront();

            // Left panel: System & Assembly Tree
            Panel leftHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(230, 235, 240) };
            Label lblLeftTitle = new Label { Text = "📂 Functional Systems & Assemblies", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(50, 60, 70) };
            leftHeader.Controls.Add(lblLeftTitle);
            splitParts.Panel1.Controls.Add(leftHeader);

            TreeView treeParts = new TreeView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.0f), HideSelection = false, FullRowSelect = true, ShowLines = true, BorderStyle = BorderStyle.None };
            splitParts.Panel1.Controls.Add(treeParts);
            treeParts.BringToFront();

            // Right panel: Parts Table & Action Banner
            Panel bannerPnl = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8, 6, 8, 6) };

            Button btnViewManual = new Button { Text = "📖 Service Manual", Dock = DockStyle.Right, Width = 145, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnViewManual.FlatAppearance.BorderSize = 0;
            bannerPnl.Controls.Add(btnViewManual);

            Panel spacerBtn = new Panel { Dock = DockStyle.Right, Width = 8 };
            bannerPnl.Controls.Add(spacerBtn);

            Button btnCopyPart = new Button { Text = "📋 Copy Part #", Dock = DockStyle.Right, Width = 120, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnCopyPart.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
            bannerPnl.Controls.Add(btnCopyPart);

            Label lblAssemblyTitle = new Label { Text = "Select an assembly on the left or search by part # or keyword", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.0f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 40, 50) };
            bannerPnl.Controls.Add(lblAssemblyTitle);

            splitParts.Panel2.Controls.Add(bannerPnl);

            // Parts DataGridView
            DataGridView dgvParts = new DataGridView();
            dgvParts.Dock = DockStyle.Fill;
            dgvParts.BackgroundColor = Color.White;
            dgvParts.BorderStyle = BorderStyle.None;
            dgvParts.ReadOnly = true;
            dgvParts.AllowUserToAddRows = false;
            dgvParts.AllowUserToDeleteRows = false;
            dgvParts.AllowUserToResizeRows = false;
            dgvParts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.MultiSelect = false;
            dgvParts.RowHeadersVisible = false;
            dgvParts.GridColor = Color.FromArgb(235, 238, 242);
            dgvParts.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f);
            dgvParts.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 238, 255);
            dgvParts.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvParts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvParts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            dgvParts.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 50, 60);
            dgvParts.EnableHeadersVisualStyles = false;
            dgvParts.RowTemplate.Height = 26;

            try
            {
                PropertyInfo pi = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(dgvParts, true, null);
            }
            catch {}

            dgvParts.Columns.Add("colPos", "Pos");
            dgvParts.Columns["colPos"].Width = 45;
            dgvParts.Columns["colPos"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colPartNo", "Part Number");
            dgvParts.Columns["colPartNo"].Width = 115;
            dgvParts.Columns["colPartNo"].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvParts.Columns["colPartNo"].DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);

            dgvParts.Columns.Add("colDesc", "Description");
            dgvParts.Columns["colDesc"].Width = 230;

            dgvParts.Columns.Add("colQty", "Qty");
            dgvParts.Columns["colQty"].Width = 55;
            dgvParts.Columns["colQty"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colUnit", "Unit");
            dgvParts.Columns["colUnit"].Width = 50;
            dgvParts.Columns["colUnit"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvParts.Columns.Add("colNotes", "Fitment & Engineering Notes");
            dgvParts.Columns["colNotes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvParts.Columns.Add("colSecId", "SecId");
            dgvParts.Columns["colSecId"].Visible = false;

            dgvParts.Columns.Add("colFg", "FG");
            dgvParts.Columns["colFg"].Visible = false;

            splitParts.Panel2.Controls.Add(dgvParts);
            dgvParts.BringToFront();

            // Populate models from BaseData
            List<Tuple<int, string>> models = new List<Tuple<int, string>>();
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=BaseData;Integrated Security=True"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Cid, Description FROM VehicleModel WHERE Cid IS NOT NULL ORDER BY Description";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                models.Add(new Tuple<int, string>(reader.GetInt32(0), reader.GetString(1)));
                            }
                        }
                    }
                }
            }
            catch (Exception exM) { Console.WriteLine("[Launcher] Load models error: " + exM.Message); }

            int defIdx = 0;
            for (int i = 0; i < models.Count; i++)
            {
                cboModel.Items.Add(models[i].Item2);
                if (models[i].Item1 == 135) defIdx = i; // V70 (08-)
            }
            if (cboModel.Items.Count > 0) cboModel.SelectedIndex = defIdx;

            string currentFg = "";

            // Load sections
            Action LoadSections = () =>
            {
                int selIdx = cboModel.SelectedIndex;
                if (selIdx < 0 || selIdx >= models.Count) return;
                int modelCid = models[selIdx].Item1;
                string modelName = models[selIdx].Item2;
                bool activeEng = chkActiveEngine.Checked;

                treeParts.BeginUpdate();
                treeParts.Nodes.Clear();

                Dictionary<string, TreeNode> groupNodes = new Dictionary<string, TreeNode>();
                Action<string, string> ensureGroup = (fgKey, title) =>
                {
                    if (!groupNodes.ContainsKey(fgKey))
                    {
                        TreeNode gn = new TreeNode(title);
                        gn.Tag = "GROUP_" + fgKey;
                        groupNodes[fgKey] = gn;
                        treeParts.Nodes.Add(gn);
                    }
                };

                ensureGroup("0", "0 - Standard Parts & Service Material");
                ensureGroup("1", "1 - Administration & Special Tools");
                ensureGroup("2", "2 - Engine with mountings and equipment");
                ensureGroup("3", "3 - Electrical system");
                ensureGroup("4", "4 - Power transmission");
                ensureGroup("5", "5 - Brakes");
                ensureGroup("6", "6 - Suspension and steering");
                ensureGroup("7", "7 - Springs and wheels");
                ensureGroup("8", "8 - Body and interior");
                ensureGroup("9", "9 - Miscellaneous & Heating/AC");

                int totalSecCount = 0;
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            if (activeEng)
                            {
                                cmd.CommandText = "SELECT DISTINCT sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, l.Description, eng.Description AS EngineCode " +
                                                  "FROM ComponentConditions cond " +
                                                  "JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "JOIN CatalogueComponents sec ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE eng.Description = 'D5204T2' AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL AND l.Description IS NOT NULL " +
                                                  "ORDER BY FG, l.Description";
                            }
                            else
                            {
                                cmd.CommandText = "SELECT DISTINCT sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, l.Description, eng.Description AS EngineCode " +
                                                  "FROM ComponentConditions cond " +
                                                  "LEFT JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "LEFT JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "JOIN CatalogueComponents sec ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE cond.ModelCid = @cid AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL AND l.Description IS NOT NULL " +
                                                  "ORDER BY FG, l.Description";
                                cmd.Parameters.AddWithValue("@cid", modelCid);
                            }

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int secId = reader.GetInt32(0);
                                    string fgCode = reader.IsDBNull(1) ? "9" : reader.GetString(1).Trim();
                                    string secName = reader.IsDBNull(2) ? "Assembly " + secId : reader.GetString(2).Trim();
                                    string engCode = reader.IsDBNull(3) ? "" : reader.GetString(3).Trim();
                                    if (string.IsNullOrEmpty(secName)) continue;

                                    string mainFg = fgCode.Length > 0 ? fgCode.Substring(0, 1) : "9";
                                    TreeNode parentGroupNode;
                                    if (!groupNodes.TryGetValue(mainFg, out parentGroupNode))
                                    {
                                        parentGroupNode = groupNodes["9"];
                                    }

                                    if (secName.Length > 1) secName = char.ToUpper(secName[0]) + secName.Substring(1);
                                    string nodeLabel = secName;
                                    if (!string.IsNullOrEmpty(engCode)) nodeLabel += " [" + engCode + "]";
                                    nodeLabel += " (Gr. " + fgCode + ")";

                                    TreeNode secNode = new TreeNode(nodeLabel);
                                    secNode.Tag = secId;
                                    parentGroupNode.Nodes.Add(secNode);
                                    totalSecCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception exS) { Console.WriteLine("[Launcher] Load sections error: " + exS.Message); }

                for (int i = treeParts.Nodes.Count - 1; i >= 0; i--)
                {
                    TreeNode gn = treeParts.Nodes[i];
                    if (gn.Nodes.Count == 0) treeParts.Nodes.RemoveAt(i);
                    else gn.Text = gn.Text + " (" + gn.Nodes.Count + ")";
                }

                treeParts.EndUpdate();
                if (treeParts.Nodes.Count > 0)
                {
                    treeParts.Nodes[0].Expand();
                    if (treeParts.Nodes[0].Nodes.Count > 0)
                    {
                        treeParts.SelectedNode = treeParts.Nodes[0].Nodes[0];
                    }
                }
                lblStatus.Text = string.Format("Model: {0} | {1} assembly sections available", modelName, totalSecCount);
            };

            cboModel.SelectedIndexChanged += (s, e) => LoadSections();
            chkActiveEngine.CheckedChanged += (s, e) => LoadSections();

            // Select section & show parts
            treeParts.AfterSelect += (s, e) =>
            {
                if (e.Node == null || !(e.Node.Tag is int)) return;
                int sectionId = (int)e.Node.Tag;

                lblAssemblyTitle.Text = e.Node.Text;
                dgvParts.Rows.Clear();

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT c.Id, c.HotspotKey, p.ItemNumber, part_lex.Description, c.Quantity, p.UnitType, " +
                                              "(SELECT TOP 1 l2.Description FROM ComponentDescriptions cd JOIN Lexicon l2 ON cd.DescriptionId = l2.DescriptionId AND l2.fkLanguage = 15 WHERE cd.fkCatalogueComponent = c.Id) AS FootNote, " +
                                              "SUBSTRING(sec.VCCSectionId, 8, 2) AS FG " +
                                              "FROM CatalogueComponents c " +
                                              "JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id " +
                                              "LEFT JOIN PartItems p ON c.fkPartItem = p.Id " +
                                              "LEFT JOIN Lexicon part_lex ON p.DescriptionId = part_lex.DescriptionId AND part_lex.fkLanguage = 15 " +
                                              "WHERE c.ParentComponentId = @secId " +
                                              "ORDER BY CASE WHEN ISNUMERIC(c.HotspotKey) = 1 THEN CAST(c.HotspotKey AS INT) ELSE 999 END, c.HotspotKey, p.ItemNumber";
                            cmd.Parameters.AddWithValue("@secId", sectionId);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string pos = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string partNo = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string desc = r.IsDBNull(3) ? (partNo.Length > 0 ? "Part " + partNo : "") : r.GetString(3).Trim();
                                    string qty = r.IsDBNull(4) ? "" : string.Format("{0:0}", r.GetDecimal(4));
                                    string unit = r.IsDBNull(5) ? "" : r.GetString(5).Trim();
                                    string note = r.IsDBNull(6) ? "" : r.GetString(6).Trim();
                                    currentFg = r.IsDBNull(7) ? "" : r.GetString(7).Trim();

                                    dgvParts.Rows.Add(pos, partNo, desc, qty, unit, note, sectionId, currentFg);
                                }
                            }
                        }
                    }
                    lblStatus.Text = string.Format("Assembly: {0} | {1} parts listed", e.Node.Text, dgvParts.Rows.Count);
                }
                catch (Exception exP) { Console.WriteLine("[Launcher] Load parts error: " + exP.Message); }
            };

            // Global search
            Action DoSearch = () =>
            {
                string q = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(q)) { LoadSections(); return; }

                dgvParts.Rows.Clear();
                lblAssemblyTitle.Text = "Search Results for '" + q + "'";

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 100 p.ItemNumber, l.Description, p.UnitType, " +
                                              "(SELECT TOP 1 sec_lex.Description FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id LEFT JOIN Lexicon sec_lex ON sec.DescriptionId = sec_lex.DescriptionId AND sec_lex.fkLanguage = 15 WHERE c.fkPartItem = p.Id) AS SectionName, " +
                                              "(SELECT TOP 1 SUBSTRING(sec.VCCSectionId, 8, 2) FROM CatalogueComponents c JOIN CatalogueComponents sec ON c.ParentComponentId = sec.Id WHERE c.fkPartItem = p.Id) AS FunctionGroup, " +
                                              "(SELECT TOP 1 c.ParentComponentId FROM CatalogueComponents c WHERE c.fkPartItem = p.Id) AS SectionId, " +
                                              "(SELECT TOP 1 c.HotspotKey FROM CatalogueComponents c WHERE c.fkPartItem = p.Id) AS Pos " +
                                              "FROM PartItems p " +
                                              "JOIN Lexicon l ON p.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                              "WHERE p.ItemNumber LIKE @q + '%' OR l.Description LIKE '%' + @q + '%' " +
                                              "ORDER BY p.ItemNumber";
                            cmd.Parameters.AddWithValue("@q", q);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string partNo = r.IsDBNull(0) ? "" : r.GetString(0).Trim();
                                    string desc = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string unit = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string secName = r.IsDBNull(3) ? "" : r.GetString(3).Trim();
                                    string fg = r.IsDBNull(4) ? "" : r.GetString(4).Trim();
                                    int secId = r.IsDBNull(5) ? 0 : r.GetInt32(5);
                                    string pos = r.IsDBNull(6) ? "" : r.GetString(6).Trim();

                                    string notes = secName.Length > 0 ? "Assembly: " + secName + " (Gr. " + fg + ")" : "";
                                    dgvParts.Rows.Add(pos, partNo, desc, "1", unit, notes, secId, fg);
                                }
                            }
                        }
                    }
                    lblStatus.Text = string.Format("Found {0} matching parts for '{1}'", dgvParts.Rows.Count, q);
                }
                catch (Exception exSrch) { Console.WriteLine("[Launcher] Search error: " + exSrch.Message); }
            };

            btnSearch.Click += (s, e) => DoSearch();
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoSearch(); } };
            btnReset.Click += (s, e) => { txtSearch.Clear(); LoadSections(); };

            // Copy part #
            Action CopySelectedPart = () =>
            {
                if (dgvParts.CurrentRow != null)
                {
                    string partNo = Convert.ToString(dgvParts.CurrentRow.Cells["colPartNo"].Value);
                    string desc = Convert.ToString(dgvParts.CurrentRow.Cells["colDesc"].Value);
                    if (!string.IsNullOrEmpty(partNo))
                    {
                        try { Clipboard.SetText(partNo); } catch {}
                        lblStatus.Text = string.Format("📋 Copied Part # {0} ({1}) to clipboard!", partNo, desc);
                    }
                }
            };
            btnCopyPart.Click += (s, e) => CopySelectedPart();
            dgvParts.CellClick += (s, e) => { if (e.RowIndex >= 0) CopySelectedPart(); };

            // Double clicking a search result navigates to its assembly in treeParts
            dgvParts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                object secObj = dgvParts.Rows[e.RowIndex].Cells["colSecId"].Value;
                if (secObj is int && (int)secObj > 0)
                {
                    int targetSecId = (int)secObj;
                    foreach (TreeNode gn in treeParts.Nodes)
                    {
                        foreach (TreeNode sn in gn.Nodes)
                        {
                            if (sn.Tag is int && (int)sn.Tag == targetSecId)
                            {
                                gn.Expand();
                                treeParts.SelectedNode = sn;
                                sn.EnsureVisible();
                                return;
                            }
                        }
                    }
                }
            };

            // Cross-reference Service Manual
            btnViewManual.Click += (s, e) =>
            {
                string fg = currentFg;
                if (dgvParts.CurrentRow != null)
                {
                    object fgObj = dgvParts.CurrentRow.Cells["colFg"].Value;
                    if (fgObj != null && !string.IsNullOrEmpty(fgObj.ToString())) fg = fgObj.ToString();
                }
                if (_btnTabManuals != null)
                {
                    _btnTabManuals.PerformClick();
                    Console.WriteLine("[Launcher] Cross-referencing Service Manuals for Function Group " + fg);
                }
            };

            _partsCatalogPnl = partsPnl;
            LoadSections();
            _partsTabConfigured = true;
            Console.WriteLine("[Launcher] Electronic Parts Catalog (EPC) initialized successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] SetupPartsCatalogTab error: " + ex);
        }
    }

}
