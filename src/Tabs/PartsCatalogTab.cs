using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Vcc.Vida.DiagSwdl.Core;
using Vcc.Vida.DiagSwdl.Documents;
using Vcc.Vida.DiagSwdl.ProgressHandler;
using Vcc.Vida.DiagSwdl.UI.Diagnostics;

static partial class Program
{
    public class FgNodeData
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public string Path { get; set; }
        public string Description { get; set; }
        public List<SectionInfo> Sections { get; set; }

        public FgNodeData()
        {
            Sections = new List<SectionInfo>();
        }
    }

    public class SectionInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string FunctionGroup { get; set; }
        public string DiagramCode { get; set; }
        public string EngineCode { get; set; }
    }

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

            // 2. Main 3-Pane SplitContainer Layout
            SplitContainer splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.FixedPanel = FixedPanel.Panel1;
            splitMain.SplitterDistance = 270;
            splitMain.SplitterWidth = 5;
            partsPnl.Controls.Add(splitMain);
            splitMain.BringToFront();

            // Left panel: Function Group Hierarchy Tree
            Panel leftHeader = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(230, 235, 240), BorderStyle = BorderStyle.FixedSingle };
            Label lblLeftTitle = new Label { Text = "📂 Function Group Tree", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            leftHeader.Controls.Add(lblLeftTitle);
            splitMain.Panel1.Controls.Add(leftHeader);

            TreeView treeParts = new TreeView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.0f), HideSelection = false, FullRowSelect = true, ShowLines = true, BorderStyle = BorderStyle.None };
            splitMain.Panel1.Controls.Add(treeParts);
            treeParts.BringToFront();

            // Right split: Middle (Diagram) + Right (Parts List)
            SplitContainer splitDiagParts = new SplitContainer();
            splitDiagParts.Dock = DockStyle.Fill;
            splitDiagParts.Orientation = Orientation.Vertical;
            splitDiagParts.SplitterDistance = 480;
            splitDiagParts.SplitterWidth = 5;
            splitMain.Panel2.Controls.Add(splitDiagParts);

            // Ensure layout maintains clean 270px tree and ~50/50 diagram & parts on display
            bool initialSplitSet = false;
            partsPnl.SizeChanged += (s, e) =>
            {
                if (!initialSplitSet && partsPnl.Width > 600)
                {
                    try
                    {
                        splitMain.SplitterDistance = 270;
                        int rem = partsPnl.Width - 275;
                        if (rem > 400)
                        {
                            splitDiagParts.SplitterDistance = rem / 2;
                        }
                        initialSplitSet = true;
                    }
                    catch {}
                }
            };

            // Middle panel: Diagram Viewer
            Panel diagContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            splitDiagParts.Panel1.Controls.Add(diagContainer);

            Panel diagHeader = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(240, 243, 246), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8, 4, 8, 4) };
            diagContainer.Controls.Add(diagHeader);

            Label lblDiagCode = new Label { Text = "Exploded Diagram", Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(50, 60, 70) };
            diagHeader.Controls.Add(lblDiagCode);

            FlowLayoutPanel pnlZoomTools = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            Button btnZoomOut = new Button { Text = "－", Width = 30, Height = 26, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            Button btnZoomFit = new Button { Text = "Fit", Width = 38, Height = 26, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            Button btnZoomIn = new Button { Text = "＋", Width = 30, Height = 26, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            pnlZoomTools.Controls.Add(btnZoomOut);
            pnlZoomTools.Controls.Add(btnZoomFit);
            pnlZoomTools.Controls.Add(btnZoomIn);
            diagHeader.Controls.Add(pnlZoomTools);

            Panel diagScrollPnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            diagContainer.Controls.Add(diagScrollPnl);
            diagScrollPnl.BringToFront();

            PictureBox picDiag = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Location = new Point(0, 0) };
            diagScrollPnl.Controls.Add(picDiag);

            // Right panel: Parts Table & Actions
            Panel rightContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            splitDiagParts.Panel2.Controls.Add(rightContainer);

            Panel bannerPnl = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10, 6, 10, 6) };
            rightContainer.Controls.Add(bannerPnl);

            ToolTip tipBanner = new ToolTip();

            Button btnCopyPart = new Button { Text = "📋 Copy #", Dock = DockStyle.Right, Width = 80, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.0f) };
            btnCopyPart.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
            tipBanner.SetToolTip(btnCopyPart, "Copy selected part number to clipboard");
            bannerPnl.Controls.Add(btnCopyPart);

            Panel spacerBtn = new Panel { Dock = DockStyle.Right, Width = 6 };
            bannerPnl.Controls.Add(spacerBtn);

            Button btnViewManual = new Button { Text = "📖", Dock = DockStyle.Right, Width = 36, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 11.0f) };
            btnViewManual.FlatAppearance.BorderSize = 0;
            tipBanner.SetToolTip(btnViewManual, "Open Workshop Service Manual for this function group");
            bannerPnl.Controls.Add(btnViewManual);

            Panel titleTextPnl = new Panel { Dock = DockStyle.Fill };
            bannerPnl.Controls.Add(titleTextPnl);

            Label lblBreadcrumb = new Label { Text = "Parts Catalog", Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8.0f), ForeColor = Color.FromArgb(120, 130, 140) };
            titleTextPnl.Controls.Add(lblBreadcrumb);

            Label lblAssemblyTitle = new Label { Text = "Select a Function Group to view parts", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11.0f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 40, 50) };
            titleTextPnl.Controls.Add(lblAssemblyTitle);
            lblAssemblyTitle.BringToFront();

            DataGridView dgvParts = new DataGridView();
            dgvParts.Dock = DockStyle.Fill;
            dgvParts.BackgroundColor = Color.White;
            dgvParts.BorderStyle = BorderStyle.None;
            dgvParts.ReadOnly = true;
            dgvParts.AllowUserToAddRows = false;
            dgvParts.AllowUserToDeleteRows = false;
            dgvParts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.MultiSelect = false;
            dgvParts.RowHeadersVisible = false;
            dgvParts.Font = new Font("Segoe UI", 9.0f);
            dgvParts.RowTemplate.Height = 28;
            dgvParts.EnableHeadersVisualStyles = false;
            dgvParts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            dgvParts.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 50, 60);
            dgvParts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvParts.ColumnHeadersHeight = 32;

            dgvParts.Columns.Add("colPos", "#");
            dgvParts.Columns["colPos"].Width = 45;
            dgvParts.Columns["colPos"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvParts.Columns["colPos"].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            dgvParts.Columns.Add("colPartNo", "Part No.");
            dgvParts.Columns["colPartNo"].Width = 100;
            dgvParts.Columns["colPartNo"].DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            dgvParts.Columns.Add("colDesc", "Description");
            dgvParts.Columns["colDesc"].Width = 240;

            dgvParts.Columns.Add("colQty", "Qty");
            dgvParts.Columns["colQty"].Width = 50;
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

            rightContainer.Controls.Add(dgvParts);
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
            Bitmap currentRawDiagram = null;
            Dictionary<string, RectangleF> currentHotspots = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
            string activeHighlightedHotspot = null;
            float currentZoom = 1.0f;

            // Diagram rendering & hotspot highlight overlay
            Action RedrawDiagram = () =>
            {
                if (currentRawDiagram == null)
                {
                    picDiag.Image = null;
                    picDiag.Visible = false;
                    return;
                }

                int targetW = (int)(diagScrollPnl.ClientSize.Width * currentZoom);
                if (targetW < 200) targetW = 200;
                int targetH = (int)((float)targetW / currentRawDiagram.Width * currentRawDiagram.Height);

                Bitmap bmp = new Bitmap(targetW, targetH);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.White);
                    g.DrawImage(currentRawDiagram, 0, 0, targetW, targetH);

                    // Draw hotspot highlights
                    if (!string.IsNullOrEmpty(activeHighlightedHotspot) && currentHotspots.ContainsKey(activeHighlightedHotspot))
                    {
                        RectangleF normRect = currentHotspots[activeHighlightedHotspot];
                        float padX = Math.Max(12, targetW * 0.015f);
                        float padY = Math.Max(10, targetH * 0.015f);
                        float rx = normRect.X * targetW - padX;
                        float ry = normRect.Y * targetH - padY;
                        float rw = normRect.Width * targetW + padX * 2;
                        float rh = normRect.Height * targetH + padY * 2;

                        using (SolidBrush fillB = new SolidBrush(Color.FromArgb(60, 0, 102, 204)))
                        {
                            g.FillRectangle(fillB, rx, ry, rw, rh);
                        }
                        using (Pen pen = new Pen(Color.FromArgb(0, 102, 204), 2.5f))
                        {
                            g.DrawRectangle(pen, rx, ry, rw, rh);
                        }
                    }
                }

                if (picDiag.Image != null) picDiag.Image.Dispose();
                picDiag.Image = bmp;
                picDiag.Size = new Size(targetW, targetH);
                picDiag.Visible = true;
            };

            // Zoom tool handlers
            btnZoomIn.Click += (s, e) => { currentZoom = Math.Min(3.0f, currentZoom + 0.25f); RedrawDiagram(); };
            btnZoomOut.Click += (s, e) => { currentZoom = Math.Max(0.5f, currentZoom - 0.25f); RedrawDiagram(); };
            btnZoomFit.Click += (s, e) => { currentZoom = 1.0f; RedrawDiagram(); };
            diagScrollPnl.Resize += (s, e) => { if (currentZoom <= 1.0f) RedrawDiagram(); };

            // Load Section Diagram & Parts
            Action<int, string, string, string> LoadSectionDetails = (sectionId, fg, breadcrumb, sectionTitle) =>
            {
                currentFg = fg;
                lblBreadcrumb.Text = breadcrumb;
                lblAssemblyTitle.Text = sectionTitle;
                dgvParts.Rows.Clear();
                activeHighlightedHotspot = null;

                // 1. Fetch Diagram Image from imagerepository via ComponentAttachments
                string diagCode = "";
                byte[] cgmBytes = null;
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 1 ad.URL " +
                                              "FROM ComponentAttachments ca " +
                                              "JOIN AttachmentData ad ON ca.fkAttachmentData = ad.Id " +
                                              "WHERE ca.fkCatalogueComponent = @secId";
                            cmd.Parameters.AddWithValue("@secId", sectionId);
                            object val = cmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value) diagCode = val.ToString().Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(diagCode))
                    {
                        using (var connImg = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=imagerepository;Integrated Security=True"))
                        {
                            connImg.Open();
                            using (var cmd = connImg.CreateCommand())
                            {
                                cmd.CommandText = "SELECT TOP 1 imageData FROM LocalizedGraphics WHERE path = @code OR path = @code + '.cgm'";
                                cmd.Parameters.AddWithValue("@code", diagCode);
                                object val = cmd.ExecuteScalar();
                                if (val != null && val != DBNull.Value) cgmBytes = (byte[])val;
                            }
                        }
                    }
                }
                catch (Exception exDiag) { Console.WriteLine("[Launcher] Load diagram error: " + exDiag.Message); }

                if (cgmBytes != null && cgmBytes.Length > 0)
                {
                    using (var decodeRes = CgmDecoder.Decode(cgmBytes))
                    {
                        if (decodeRes.Success && decodeRes.Image != null)
                        {
                            if (currentRawDiagram != null) currentRawDiagram.Dispose();
                            currentRawDiagram = new Bitmap(decodeRes.Image);
                            currentHotspots = new Dictionary<string, RectangleF>(decodeRes.NormalizedHotspots, StringComparer.OrdinalIgnoreCase);
                            lblDiagCode.Text = "Diagram: " + diagCode;
                        }
                        else
                        {
                            if (currentRawDiagram != null) currentRawDiagram.Dispose();
                            currentRawDiagram = null;
                            currentHotspots.Clear();
                            lblDiagCode.Text = "Diagram: " + diagCode + " (Vector Preview Unavailable)";
                        }
                    }
                }
                else
                {
                    if (currentRawDiagram != null) currentRawDiagram.Dispose();
                    currentRawDiagram = null;
                    currentHotspots.Clear();
                    lblDiagCode.Text = string.IsNullOrEmpty(diagCode) ? "No diagram attached" : "Diagram: " + diagCode;
                }
                RedrawDiagram();

                // 2. Fetch Parts for Section
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

                                    dgvParts.Rows.Add(pos, partNo, desc, qty, unit, note, sectionId, fg);
                                }
                            }
                        }
                    }
                    lblStatus.Text = string.Format("{0} | {1} parts listed", sectionTitle, dgvParts.Rows.Count);
                }
                catch (Exception exP) { Console.WriteLine("[Launcher] Load parts error: " + exP.Message); }
            };

            // Hotspot click & hover on diagram
            picDiag.MouseMove += (s, e) =>
            {
                if (currentRawDiagram == null || currentHotspots.Count == 0 || picDiag.Image == null) return;
                float normX = (float)e.X / picDiag.Image.Width;
                float normY = (float)e.Y / picDiag.Image.Height;

                string hitKey = null;
                foreach (var kvp in currentHotspots)
                {
                    RectangleF r = kvp.Value;
                    float expand = 0.02f;
                    if (normX >= r.X - expand && normX <= r.Right + expand &&
                        normY >= r.Y - expand && normY <= r.Bottom + expand)
                    {
                        hitKey = kvp.Key;
                        break;
                    }
                }

                if (hitKey != null)
                {
                    picDiag.Cursor = Cursors.Hand;
                }
                else
                {
                    picDiag.Cursor = Cursors.Default;
                }
            };

            picDiag.MouseClick += (s, e) =>
            {
                if (currentRawDiagram == null || currentHotspots.Count == 0 || picDiag.Image == null) return;
                float normX = (float)e.X / picDiag.Image.Width;
                float normY = (float)e.Y / picDiag.Image.Height;

                string hitKey = null;
                foreach (var kvp in currentHotspots)
                {
                    RectangleF r = kvp.Value;
                    float expand = 0.02f;
                    if (normX >= r.X - expand && normX <= r.Right + expand &&
                        normY >= r.Y - expand && normY <= r.Bottom + expand)
                    {
                        hitKey = kvp.Key;
                        break;
                    }
                }

                if (hitKey != null)
                {
                    activeHighlightedHotspot = hitKey;
                    RedrawDiagram();

                    // Find and select corresponding row in dgvParts
                    foreach (DataGridViewRow row in dgvParts.Rows)
                    {
                        string pVal = Convert.ToString(row.Cells["colPos"].Value);
                        if (pVal == hitKey)
                        {
                            row.Selected = true;
                            dgvParts.CurrentCell = row.Cells[0];
                            dgvParts.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index - 2);
                            break;
                        }
                    }
                }
            };

            // Table row selection highlights hotspot on diagram
            dgvParts.SelectionChanged += (s, e) =>
            {
                if (dgvParts.CurrentRow != null)
                {
                    string pVal = Convert.ToString(dgvParts.CurrentRow.Cells["colPos"].Value);
                    if (!string.IsNullOrEmpty(pVal) && pVal != activeHighlightedHotspot)
                    {
                        activeHighlightedHotspot = pVal;
                        RedrawDiagram();
                    }
                }
            };

            // Load Function Group Hierarchy & Sections
            Action LoadHierarchyAndSections = () =>
            {
                int selIdx = cboModel.SelectedIndex;
                if (selIdx < 0 || selIdx >= models.Count) return;
                int modelCid = models[selIdx].Item1;
                string modelName = models[selIdx].Item2;
                bool activeEng = chkActiveEngine.Checked;

                treeParts.BeginUpdate();
                treeParts.Nodes.Clear();
                lblStatus.Text = "Building Volvo Function Group Tree...";

                // 1. Fetch all TypeId = 2 Function Groups
                Dictionary<int, FgNodeData> fgDict = new Dictionary<int, FgNodeData>();
                List<FgNodeData> fgList = new List<FgNodeData>();

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection("Server=localhost\\SQLEXPRESS;Database=EPC;Integrated Security=True"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT cc.Id, cc.FunctionGroupLabel, cc.FunctionGroupPath, l.Description " +
                                              "FROM CatalogueComponents cc " +
                                              "LEFT JOIN Lexicon l ON cc.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                              "WHERE cc.TypeId = 2 " +
                                              "ORDER BY cc.FunctionGroupPath";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    int id = r.GetInt32(0);
                                    string lbl = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                                    string path = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string desc = r.IsDBNull(3) ? "" : r.GetString(3).Trim();

                                    var fgData = new FgNodeData { Id = id, Label = lbl, Path = path, Description = desc };
                                    fgDict[id] = fgData;
                                    fgList.Add(fgData);
                                }
                            }
                        }

                        // 2. Fetch Sections mapped via VirtualToShared and filtered by ComponentConditions
                        using (var cmd = conn.CreateCommand())
                        {
                            if (activeEng)
                            {
                                cmd.CommandText = "SELECT DISTINCT vts.fkCatalogueComponent_Parent, sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, " +
                                                  "l.Description AS SectionTitle, eng.Description AS EngineCode " +
                                                  "FROM VirtualToShared vts " +
                                                  "JOIN CatalogueComponents sec ON vts.fkCatalogueComponent = sec.Id " +
                                                  "JOIN ComponentConditions cond ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "LEFT JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE cond.ModelCid = @cid AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL " +
                                                  "AND (eng.Description = 'D5204T2' OR vp.fkEngine IS NULL) " +
                                                  "ORDER BY FG, SectionTitle";
                                cmd.Parameters.AddWithValue("@cid", modelCid);
                            }
                            else
                            {
                                cmd.CommandText = "SELECT DISTINCT vts.fkCatalogueComponent_Parent, sec.Id, SUBSTRING(sec.VCCSectionId, 8, 2) AS FG, " +
                                                  "l.Description AS SectionTitle, eng.Description AS EngineCode " +
                                                  "FROM VirtualToShared vts " +
                                                  "JOIN CatalogueComponents sec ON vts.fkCatalogueComponent = sec.Id " +
                                                  "JOIN ComponentConditions cond ON cond.fkCatalogueComponent = sec.Id " +
                                                  "LEFT JOIN BaseData.dbo.VehicleProfile vp ON cond.fkProfile = vp.Id " +
                                                  "LEFT JOIN BaseData.dbo.Engine eng ON vp.fkEngine = eng.Id " +
                                                  "LEFT JOIN Lexicon l ON sec.DescriptionId = l.DescriptionId AND l.fkLanguage = 15 " +
                                                  "WHERE cond.ModelCid = @cid AND sec.TypeId = 3 AND sec.VCCSectionId IS NOT NULL " +
                                                  "ORDER BY FG, SectionTitle";
                                cmd.Parameters.AddWithValue("@cid", modelCid);
                            }

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    int parentFgId = r.GetInt32(0);
                                    int secId = r.GetInt32(1);
                                    string fgCode = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                                    string sTitle = r.IsDBNull(3) ? "Assembly " + secId : r.GetString(3).Trim();
                                    string engCode = r.IsDBNull(4) ? "" : r.GetString(4).Trim();

                                    FgNodeData targetFg;
                                    if (fgDict.TryGetValue(parentFgId, out targetFg))
                                    {
                                        targetFg.Sections.Add(new SectionInfo
                                        {
                                            Id = secId,
                                            Title = sTitle,
                                            FunctionGroup = fgCode,
                                            EngineCode = engCode
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception exH) { Console.WriteLine("[Launcher] Load hierarchy error: " + exH.Message); }

                // 3. Mark which nodes (and parent branches) have active sections
                HashSet<int> activeNodeIds = new HashSet<int>();
                foreach (var kvp in fgDict)
                {
                    if (kvp.Value.Sections.Count > 0)
                    {
                        activeNodeIds.Add(kvp.Key);
                        // Also mark all ancestors in FunctionGroupPath
                        string[] parts = kvp.Value.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        string runningPath = "";
                        foreach (string p in parts)
                        {
                            runningPath += "/" + p;
                            foreach (var ancestor in fgList)
                            {
                                if (ancestor.Path == runningPath) activeNodeIds.Add(ancestor.Id);
                            }
                        }
                    }
                }

                // 4. Build TreeView nodes
                Dictionary<string, TreeNode> pathToNode = new Dictionary<string, TreeNode>();
                int totalActiveSections = 0;

                foreach (var fg in fgList)
                {
                    string title = fg.Label;
                    if (!string.IsNullOrEmpty(fg.Description)) title = fg.Label + " - " + fg.Description;

                    TreeNode node = new TreeNode(title);
                    node.Tag = fg;

                    string path = fg.Path.TrimEnd('/');
                    int lastSlash = path.LastIndexOf('/');
                    string parentPath = lastSlash > 0 ? path.Substring(0, lastSlash) : "";

                    TreeNode parentNode;
                    if (!string.IsNullOrEmpty(parentPath) && pathToNode.TryGetValue(parentPath, out parentNode))
                    {
                        parentNode.Nodes.Add(node);
                    }
                    else
                    {
                        treeParts.Nodes.Add(node);
                    }
                    pathToNode[path] = node;

                    // If leaf node has multiple section variants, add them as sub-nodes
                    if (fg.Sections.Count > 1)
                    {
                        foreach (var sec in fg.Sections)
                        {
                            string sLabel = sec.Title;
                            if (!string.IsNullOrEmpty(sec.EngineCode)) sLabel += " [" + sec.EngineCode + "]";
                            TreeNode sNode = new TreeNode(sLabel);
                            sNode.Tag = sec;
                            node.Nodes.Add(sNode);
                        }
                    }
                    totalActiveSections += fg.Sections.Count;
                }

                treeParts.EndUpdate();

                // Expand first active main group
                if (treeParts.Nodes.Count > 0)
                {
                    treeParts.Nodes[0].Expand();
                    if (treeParts.Nodes[0].Nodes.Count > 0)
                    {
                        treeParts.Nodes[0].Nodes[0].Expand();
                        treeParts.SelectedNode = treeParts.Nodes[0].Nodes[0];
                    }
                    else
                    {
                        treeParts.SelectedNode = treeParts.Nodes[0];
                    }
                }

                lblStatus.Text = string.Format("Model: {0} | {1} active sections across {2} function groups", modelName, totalActiveSections, activeNodeIds.Count);
            };

            cboModel.SelectedIndexChanged += (s, e) => LoadHierarchyAndSections();
            chkActiveEngine.CheckedChanged += (s, e) => LoadHierarchyAndSections();

            // AfterSelect: drill down to section and display diagram & parts
            treeParts.AfterSelect += (s, e) =>
            {
                if (e.Node == null || e.Node.Tag == null) return;

                if (e.Node.Tag is SectionInfo)
                {
                    SectionInfo sec = (SectionInfo)e.Node.Tag;
                    TreeNode pNode = e.Node.Parent;
                    string fgLabel = pNode != null && pNode.Tag is FgNodeData ? ((FgNodeData)pNode.Tag).Label : sec.FunctionGroup;
                    string breadcrumb = "Parts / " + (pNode != null ? pNode.FullPath.Replace("\\", " / ") : sec.Title);
                    LoadSectionDetails(sec.Id, fgLabel, breadcrumb, sec.Title);
                }
                else if (e.Node.Tag is FgNodeData)
                {
                    FgNodeData fg = (FgNodeData)e.Node.Tag;
                    if (fg.Sections.Count > 0)
                    {
                        SectionInfo primarySec = fg.Sections[0];
                        string breadcrumb = "Parts / " + e.Node.FullPath.Replace("\\", " / ");
                        string title = fg.Description.Length > 0 ? fg.Description : ("Group " + fg.Label);
                        LoadSectionDetails(primarySec.Id, fg.Label, breadcrumb, title);
                    }
                    else
                    {
                        lblBreadcrumb.Text = "Parts / " + e.Node.FullPath.Replace("\\", " / ");
                        lblAssemblyTitle.Text = e.Node.Text;
                        dgvParts.Rows.Clear();
                        if (currentRawDiagram != null) currentRawDiagram.Dispose();
                        currentRawDiagram = null;
                        currentHotspots.Clear();
                        lblDiagCode.Text = "Select a sub-assembly to view parts";
                        RedrawDiagram();
                    }
                }
            };

            // Global search
            Action DoSearch = () =>
            {
                string q = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(q)) { LoadHierarchyAndSections(); return; }

                dgvParts.Rows.Clear();
                lblBreadcrumb.Text = "Search Results";
                lblAssemblyTitle.Text = "Matching Parts for '" + q + "'";
                if (currentRawDiagram != null) currentRawDiagram.Dispose();
                currentRawDiagram = null;
                currentHotspots.Clear();
                lblDiagCode.Text = "Search Results (Double-click part to navigate to assembly)";
                RedrawDiagram();

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
            btnReset.Click += (s, e) => { txtSearch.Clear(); LoadHierarchyAndSections(); };

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
                    if (_navigateToManualFgAction != null && !string.IsNullOrEmpty(fg))
                    {
                        _navigateToManualFgAction(fg);
                    }
                }
            };

            _partsCatalogPnl = partsPnl;
            LoadHierarchyAndSections();
            _partsTabConfigured = true;
            Console.WriteLine("[Launcher] Electronic Parts Catalog (EPC) initialized successfully with 3-tier hierarchy & diagram viewer!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] SetupPartsCatalogTab error: " + ex);
        }
    }
}
