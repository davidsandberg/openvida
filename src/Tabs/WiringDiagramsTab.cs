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
    static void SetupWiringDiagramsTab(object mObj)
    {
        if (_wiringTabConfigured || mObj == null) return;
        try
        {
            WiringDiagramsTabControl wiringPnl = new WiringDiagramsTabControl();
            wiringPnl.Dock = DockStyle.Fill;
            wiringPnl.BackColor = Color.FromArgb(245, 247, 250);
            wiringPnl.Font = new Font("Segoe UI", 9.0f);

            string ewdRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\wiring_diagrams\US_Eng"));
            if (!Directory.Exists(ewdRoot))
            {
                ewdRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wiring_diagrams\US_Eng"));
            }

            // Top Header Panel
            Panel headerPnl = new Panel();
            headerPnl.Dock = DockStyle.Top;
            headerPnl.Height = 52;
            headerPnl.BackColor = Color.FromArgb(240, 243, 246);
            headerPnl.BorderStyle = BorderStyle.FixedSingle;
            wiringPnl.Controls.Add(headerPnl);

            Label lblModel = new Label { Text = "Model:", AutoSize = true, Location = new Point(12, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblModel);

            ComboBox cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 13), Width = 130, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboModel);

            Label lblPub = new Label { Text = "Publication / Year:", AutoSize = true, Location = new Point(204, 16), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 60) };
            headerPnl.Controls.Add(lblPub);

            ComboBox cboPub = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(328, 13), Width = 260, Font = new Font("Segoe UI", 9.0f) };
            headerPnl.Controls.Add(cboPub);

            Button btnOpenPdf = new Button { Text = "📖 Schematics (PDF)", Location = new Point(598, 12), Width = 150, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnOpenPdf.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnOpenPdf);

            Button btnOpenLop = new Button { Text = "📍 Component Locations", Location = new Point(756, 12), Width = 170, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 87, 154), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnOpenLop.FlatAppearance.BorderSize = 0;
            headerPnl.Controls.Add(btnOpenLop);

            Button btnBrowser = new Button { Text = "🌐 External Window", Location = new Point(934, 12), Width = 145, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(225, 230, 236), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnBrowser.FlatAppearance.BorderSize = 1;
            btnBrowser.FlatAppearance.BorderColor = Color.FromArgb(190, 195, 202);
            headerPnl.Controls.Add(btnBrowser);

            // SplitContainer
            SplitContainer splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.SplitterDistance = 480;
            splitMain.SplitterWidth = 6;
            wiringPnl.Controls.Add(splitMain);
            splitMain.BringToFront();

            // Panel1: Left Panel (Component Locations & Search)
            Panel pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(230, 235, 240) };
            Label lblLeftTitle = new Label { Text = "Components & Locations (LOP)", Dock = DockStyle.Left, Width = 250, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(50, 60, 70) };
            pnlLeftHeader.Controls.Add(lblLeftTitle);
            Label lblCount = new Label { Text = "", Dock = DockStyle.Right, Width = 150, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            pnlLeftHeader.Controls.Add(lblCount);
            splitMain.Panel1.Controls.Add(pnlLeftHeader);

            Panel pnlSearch = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(6, 4, 6, 4) };
            TextBox txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
            pnlSearch.Controls.Add(txtSearch);
            splitMain.Panel1.Controls.Add(pnlSearch);

            // Bottom detail panel
            Panel pnlDetail = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6, 6, 6, 6) };

            Button btnFindSchematic = new Button { Text = "⚡ Schematics", Dock = DockStyle.Right, Width = 145, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold) };
            btnFindSchematic.FlatAppearance.BorderSize = 0;
            pnlDetail.Controls.Add(btnFindSchematic);

            Panel spcDetail1 = new Panel { Dock = DockStyle.Right, Width = 6 };
            pnlDetail.Controls.Add(spcDetail1);

            Button btnShowLoc = new Button { Text = "📍 Car Placement", Dock = DockStyle.Right, Width = 145, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 87, 154), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold) };
            btnShowLoc.FlatAppearance.BorderSize = 0;
            pnlDetail.Controls.Add(btnShowLoc);

            Panel spcDetail2 = new Panel { Dock = DockStyle.Right, Width = 6 };
            pnlDetail.Controls.Add(spcDetail2);

            Button btnCopyCode = new Button { Text = "📋 Copy ID", Dock = DockStyle.Right, Width = 75, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnCopyCode.FlatAppearance.BorderColor = Color.FromArgb(180, 185, 190);
            pnlDetail.Controls.Add(btnCopyCode);

            Label lblSelComp = new Label { Text = "Select a component above to navigate directly to its placement or schematic diagram", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.0f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 40, 50) };
            pnlDetail.Controls.Add(lblSelComp);
            lblSelComp.BringToFront();

            splitMain.Panel1.Controls.Add(pnlDetail);
            pnlDetail.BringToFront();

            DataGridView dgvComponents = new DataGridView();
            dgvComponents.Dock = DockStyle.Fill;
            dgvComponents.BackgroundColor = Color.White;
            dgvComponents.BorderStyle = BorderStyle.None;
            dgvComponents.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvComponents.GridColor = Color.FromArgb(235, 238, 242);
            dgvComponents.RowHeadersVisible = false;
            dgvComponents.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvComponents.MultiSelect = false;
            dgvComponents.AllowUserToAddRows = false;
            dgvComponents.AllowUserToDeleteRows = false;
            dgvComponents.ReadOnly = true;
            dgvComponents.AutoGenerateColumns = false;
            dgvComponents.EnableHeadersVisualStyles = false;
            dgvComponents.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            dgvComponents.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(50, 60, 70);
            dgvComponents.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            dgvComponents.ColumnHeadersHeight = 28;
            dgvComponents.RowTemplate.Height = 24;

            DataGridViewTextBoxColumn colCode = new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "ID", Width = 68 };
            colCode.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colCode.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);
            dgvComponents.Columns.Add(colCode);

            DataGridViewTextBoxColumn colDesc = new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "Component Name / Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            colDesc.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f);
            colDesc.DefaultCellStyle.ForeColor = Color.FromArgb(30, 40, 50);
            dgvComponents.Columns.Add(colDesc);

            DataGridViewTextBoxColumn colSchem = new DataGridViewTextBoxColumn { Name = "SchematicDisp", DataPropertyName = "SchematicDisp", HeaderText = "⚡ Schematics", Width = 95 };
            colSchem.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colSchem.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);
            colSchem.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colSchem.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvComponents.Columns.Add(colSchem);

            DataGridViewTextBoxColumn colPlace = new DataGridViewTextBoxColumn { Name = "PlacementDisp", DataPropertyName = "PlacementDisp", HeaderText = "📍 Placement", Width = 90 };
            colPlace.DefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            colPlace.DefaultCellStyle.ForeColor = Color.FromArgb(43, 87, 154);
            colPlace.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colPlace.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvComponents.Columns.Add(colPlace);

            // Hidden columns to support robust direct value binding
            dgvComponents.Columns.Add(new DataGridViewTextBoxColumn { Name = "SchematicPage", DataPropertyName = "SchematicPage", Visible = false });
            dgvComponents.Columns.Add(new DataGridViewTextBoxColumn { Name = "PlacementPage", DataPropertyName = "PlacementPage", Visible = false });
            dgvComponents.Columns.Add(new DataGridViewTextBoxColumn { Name = "PageNum", DataPropertyName = "PageNum", Visible = false });

            splitMain.Panel1.Controls.Add(dgvComponents);
            dgvComponents.BringToFront();

            // Panel2: Right Panel (Embedded Modern Edge WebView2 & Navigation Bar)
            Panel pnlWebNav = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(245, 247, 250), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4) };

            Button btnNavSchematics = new Button { Text = "📖 Wiring Schematics PDF", Dock = DockStyle.Left, Width = 175, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnNavSchematics.FlatAppearance.BorderSize = 0;
            pnlWebNav.Controls.Add(btnNavSchematics);

            Panel spcNav1 = new Panel { Dock = DockStyle.Left, Width = 6 };
            pnlWebNav.Controls.Add(spcNav1);

            Button btnNavLop = new Button { Text = "📍 Component Locations (LOP)", Dock = DockStyle.Left, Width = 195, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnNavLop.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);
            pnlWebNav.Controls.Add(btnNavLop);

            Panel spcNav2 = new Panel { Dock = DockStyle.Left, Width = 6 };
            pnlWebNav.Controls.Add(spcNav2);

            Button btnNavIndex = new Button { Text = "⚡ Interactive Index", Dock = DockStyle.Left, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 243, 246), ForeColor = Color.FromArgb(40, 50, 60), Cursor = Cursors.Hand };
            btnNavIndex.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);
            pnlWebNav.Controls.Add(btnNavIndex);

            Label lblWebUrl = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Color.FromArgb(100, 110, 120) };
            pnlWebNav.Controls.Add(lblWebUrl);
            lblWebUrl.SendToBack();

            splitMain.Panel2.Controls.Add(pnlWebNav);
            pnlWebNav.Dock = DockStyle.Top;
            pnlWebNav.BringToFront();

            Microsoft.Web.WebView2.WinForms.WebView2 wvPdf = new Microsoft.Web.WebView2.WinForms.WebView2();
            wvPdf.Dock = DockStyle.Fill;
            splitMain.Panel2.Controls.Add(wvPdf);
            wvPdf.SendToBack();
            _ewdWebView = wvPdf;
            _ewdDgv = dgvComponents;
            _ewdBtnShowLoc = btnShowLoc;
            _ewdBtnFindSchem = btnFindSchematic;

            // Catalog Parsing
            Dictionary<string, List<EwdPublicationItem>> catalog = new Dictionary<string, List<EwdPublicationItem>>(StringComparer.OrdinalIgnoreCase);
            string jsPath = Path.Combine(ewdRoot, "pdf-links.js");
            if (File.Exists(jsPath))
            {
                string js = File.ReadAllText(jsPath);
                var modelMatches = Regex.Matches(js, @"if\s*\(\s*chosen\s*==\s*""([^""]+)""\s*\)\s*\{([^}]+)\}");
                foreach (Match m in modelMatches)
                {
                    string modelKey = m.Groups[1].Value.Trim();
                    string body = m.Groups[2].Value;
                    var pubMatches = Regex.Matches(body, @"Option\(\s*'([^']+)'\s*,\s*'([^']+)'\s*\)");
                    List<EwdPublicationItem> list = new List<EwdPublicationItem>();
                    foreach (Match p in pubMatches)
                    {
                        string name = p.Groups[1].Value.Trim();
                        string path = p.Groups[2].Value.Trim();
                        if (!name.StartsWith("Step") && !string.IsNullOrEmpty(path))
                        {
                            string pubId = "";
                            var mId = Regex.Match(name, @"\b(\d{7,8})\b");
                            if (mId.Success) pubId = mId.Groups[1].Value;
                            list.Add(new EwdPublicationItem { Name = name, Path = path, PubId = pubId });
                        }
                    }
                    if (list.Count > 0) catalog[modelKey] = list;
                }
            }

            // Populate Models
            EwdModelItem[] models = new EwdModelItem[] {
                new EwdModelItem { DisplayName = "V70 (08-)", Key = "v70(08-)" },
                new EwdModelItem { DisplayName = "XC70 (08-)", Key = "xc70(08-)" },
                new EwdModelItem { DisplayName = "S80 (07-)", Key = "s80(07-)" },
                new EwdModelItem { DisplayName = "XC60", Key = "xc60" },
                new EwdModelItem { DisplayName = "V60", Key = "v60" },
                new EwdModelItem { DisplayName = "S60 (11-)", Key = "s60(11-)" },
                new EwdModelItem { DisplayName = "S60 (-09)", Key = "s60(-09)" },
                new EwdModelItem { DisplayName = "C30", Key = "c30" },
                new EwdModelItem { DisplayName = "S40 (04-)", Key = "s40" },
                new EwdModelItem { DisplayName = "V50", Key = "v50" },
                new EwdModelItem { DisplayName = "C70 (06-)", Key = "c70" },
                new EwdModelItem { DisplayName = "V70 (-08)", Key = "v70" },
                new EwdModelItem { DisplayName = "XC70 (-08)", Key = "xc70" },
                new EwdModelItem { DisplayName = "S80 (-07)", Key = "s80(-07)" },
                new EwdModelItem { DisplayName = "XC90", Key = "xc90" }
            };
            foreach (var m in models) cboModel.Items.Add(m);

            DataTable dtComponents = new DataTable();
            dtComponents.Columns.Add("Code");
            dtComponents.Columns.Add("Description");
            dtComponents.Columns.Add("SchematicPage", typeof(int));
            dtComponents.Columns.Add("SchematicDisp");
            dtComponents.Columns.Add("PlacementPage", typeof(int));
            dtComponents.Columns.Add("PlacementDisp");
            dtComponents.Columns.Add("PageNum", typeof(int));
            dtComponents.Columns.Add("PageDisp");
            dgvComponents.DataSource = dtComponents.DefaultView;

            string curPdfPath = "";
            string curLopPdfPath = "";
            string curWebUrl = "";

            Action updateNavStyles = () => {
                string currentUri = (wvPdf.Source != null) ? wvPdf.Source.ToString().ToLowerInvariant() : "";
                bool isLop = currentUri.Contains("lop.pdf");
                bool isHtml = currentUri.EndsWith(".html") || currentUri.EndsWith(".htm");

                btnNavSchematics.BackColor = (!isLop && !isHtml) ? Color.FromArgb(0, 102, 204) : Color.FromArgb(240, 243, 246);
                btnNavSchematics.ForeColor = (!isLop && !isHtml) ? Color.White : Color.FromArgb(40, 50, 60);

                btnNavLop.BackColor = isLop ? Color.FromArgb(43, 87, 154) : Color.FromArgb(240, 243, 246);
                btnNavLop.ForeColor = isLop ? Color.White : Color.FromArgb(40, 50, 60);

                btnNavIndex.BackColor = isHtml ? Color.FromArgb(0, 102, 204) : Color.FromArgb(240, 243, 246);
                btnNavIndex.ForeColor = isHtml ? Color.White : Color.FromArgb(40, 50, 60);
            };

            Action<int> jumpToPage = (pageNum) => {
                try
                {
                    Console.WriteLine("[Launcher] jumpToPage requested for page " + pageNum + ", curPdf: " + curPdfPath);
                    if (pageNum > 0 && File.Exists(curPdfPath))
                    {
                        string targetUri = new Uri(curPdfPath).AbsoluteUri + "#page=" + pageNum;
                        if (wvPdf.CoreWebView2 != null)
                        {
                            string curUri = (wvPdf.Source != null) ? wvPdf.Source.ToString() : "";
                            string baseUri = new Uri(curPdfPath).AbsoluteUri;
                            Console.WriteLine("[Launcher] curUri: " + curUri + " vs baseUri: " + baseUri);
                            if (curUri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("[Launcher] Already on PDF, resetting via NavigateToString...");
                                EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs> navHandler = null;
                                navHandler = (s, e) => {
                                    wvPdf.CoreWebView2.NavigationCompleted -= navHandler;
                                    Console.WriteLine("[Launcher] Reset completed! Navigating to: " + targetUri);
                                    wvPdf.CoreWebView2.Navigate(targetUri);
                                    updateNavStyles();
                                };
                                wvPdf.CoreWebView2.NavigationCompleted += navHandler;
                                wvPdf.CoreWebView2.NavigateToString("<html><body style='background:#f0f2f5;'></body></html>");
                            }
                            else
                            {
                                Console.WriteLine("[Launcher] Direct navigation to: " + targetUri);
                                wvPdf.CoreWebView2.Navigate(targetUri);
                                updateNavStyles();
                            }
                        }
                        else
                        {
                            Console.WriteLine("[Launcher] CoreWebView2 is null, setting wvPdf.Source: " + targetUri);
                            wvPdf.Source = new Uri(targetUri);
                            updateNavStyles();
                        }
                    }
                }
                catch (Exception exJump) { Console.WriteLine("[Launcher] EWD page navigation failed: " + exJump.Message); }
            };

            Action<string> jumpToSearch = (codeStr) => {
                try
                {
                    if (!string.IsNullOrEmpty(codeStr) && File.Exists(curPdfPath))
                    {
                        string uriStr = new Uri(curPdfPath).AbsoluteUri + "?nav=" + DateTime.Now.Ticks + "#search=" + Uri.EscapeDataString(codeStr);
                        if (wvPdf.CoreWebView2 != null)
                        {
                            wvPdf.CoreWebView2.Navigate(uriStr);
                        }
                        else
                        {
                            wvPdf.Source = new Uri(uriStr);
                        }
                        updateNavStyles();
                    }
                }
                catch (Exception exSearch) { Console.WriteLine("[Launcher] EWD search navigation failed: " + exSearch.Message); }
            };

            Action<EwdPublicationItem> loadPub = async (pub) => {
                if (pub == null) return;
                try
                {
                    string pubId = pub.PubId;
                    curPdfPath = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + ".pdf");
                    curLopPdfPath = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "lop.pdf");
                    curWebUrl = "http://localhost/Vida/wiring_diagrams/US_Eng/" + pub.Path;
                    lblWebUrl.Text = pub.Name;

                    // Load Component Navigation (TSV or LOP fallback)
                    dtComponents.Rows.Clear();
                    string navTsv = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "_nav.tsv");
                    if (File.Exists(navTsv))
                    {
                        string[] lines = File.ReadAllLines(navTsv, Encoding.UTF8);
                        foreach (string line in lines)
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("Code\t", StringComparison.OrdinalIgnoreCase))
                                continue;
                            string[] parts = trimmed.Split('\t');
                            if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[0].Trim()))
                            {
                                string code = parts[0].Trim();
                                string desc = parts[1].Trim();
                                int schemPage = 0;
                                int placePage = 0;
                                int.TryParse(parts[2].Trim(), out schemPage);
                                int.TryParse(parts[3].Trim(), out placePage);
                                string schemDisp = (schemPage > 0) ? "Page " + schemPage : "-";
                                string placeDisp = (placePage > 0) ? "Page " + placePage : "-";
                                int defaultPage = (schemPage > 0) ? schemPage : placePage;
                                dtComponents.Rows.Add(code, desc, schemPage, schemDisp, placePage, placeDisp, defaultPage, (defaultPage > 0 ? "Page " + defaultPage : "-"));
                            }
                            else if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[0].Trim()))
                            {
                                string code = parts[0].Trim();
                                string desc = parts[1].Trim();
                                int pNum = 0;
                                int.TryParse(parts[2].Trim(), out pNum);
                                string pDisp = (pNum > 0) ? "Page " + pNum : "-";
                                dtComponents.Rows.Add(code, desc, 0, "-", pNum, pDisp, pNum, pDisp);
                            }
                        }
                    }
                    else
                    {
                        string lopTxt = Path.Combine(ewdRoot, pubId + @"\images\" + pubId + "lop.txt");
                        if (File.Exists(lopTxt))
                        {
                            string[] lines = File.ReadAllLines(lopTxt, Encoding.Default);
                            foreach (string line in lines)
                            {
                                string[] parts = line.Split(new char[] { '\t' }, 2);
                                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0].Trim()))
                                {
                                    dtComponents.Rows.Add(parts[0].Trim(), parts[1].Trim(), 0, "-", 0, "-", 0, "-");
                                }
                            }
                        }
                    }
                    lblCount.Text = dtComponents.Rows.Count + " components";
                    txtSearch.Text = "";

                    // Initialize WebView2 and navigate directly to the PDF schematic book!
                    if (wvPdf.CoreWebView2 == null)
                    {
                        await wvPdf.EnsureCoreWebView2Async(null);
                    }
                    if (File.Exists(curPdfPath))
                    {
                        wvPdf.Source = new Uri(curPdfPath);
                    }
                    else
                    {
                        wvPdf.Source = new Uri(curWebUrl);
                    }
                    updateNavStyles();
                }
                catch (Exception exLoad)
                {
                    Console.WriteLine("[Launcher] Error loading publication: " + exLoad.Message);
                }
            };

            btnNavSchematics.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    wvPdf.Source = new Uri(curPdfPath);
                    updateNavStyles();
                }
            };

            btnNavLop.Click += (s, e) => {
                if (File.Exists(curLopPdfPath))
                {
                    wvPdf.Source = new Uri(curLopPdfPath);
                    updateNavStyles();
                }
            };

            btnNavIndex.Click += (s, e) => {
                if (!string.IsNullOrEmpty(curWebUrl))
                {
                    wvPdf.Source = new Uri(curWebUrl);
                    updateNavStyles();
                }
            };

            cboModel.SelectedIndexChanged += (s, e) => {
                EwdModelItem selModel = cboModel.SelectedItem as EwdModelItem;
                if (selModel == null) return;
                cboPub.Items.Clear();
                if (catalog.ContainsKey(selModel.Key))
                {
                    foreach (var p in catalog[selModel.Key]) cboPub.Items.Add(p);
                }
                if (cboPub.Items.Count > 0)
                {
                    // Select 2011 or first
                    int selIdx = 0;
                    for (int i = 0; i < cboPub.Items.Count; i++)
                    {
                        EwdPublicationItem pi = cboPub.Items[i] as EwdPublicationItem;
                        if (pi != null && pi.Name.Contains("2011") && !pi.Name.Contains("Supplement"))
                        {
                            selIdx = i;
                            break;
                        }
                    }
                    cboPub.SelectedIndex = selIdx;
                }
            };

            cboPub.SelectedIndexChanged += (s, e) => {
                loadPub(cboPub.SelectedItem as EwdPublicationItem);
            };

            txtSearch.TextChanged += (s, e) => {
                string q = txtSearch.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]");
                if (string.IsNullOrEmpty(q))
                {
                    dtComponents.DefaultView.RowFilter = "";
                }
                else
                {
                    dtComponents.DefaultView.RowFilter = string.Format("Code LIKE '%{0}%' OR Description LIKE '%{0}%'", q);
                }
                lblCount.Text = dtComponents.DefaultView.Count + " matches";
            };

            Func<DataGridViewRow, string, int> getRowInt = (row, col) => {
                if (row == null) return 0;
                try {
                    DataRowView drv = row.DataBoundItem as DataRowView;
                    if (drv != null && drv.Row.Table.Columns.Contains(col))
                    {
                        object o = drv[col];
                        if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                    }
                    if (row.DataGridView != null && row.DataGridView.Columns.Contains(col))
                    {
                        object o = row.Cells[col].Value;
                        if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                    }
                } catch {}
                return 0;
            };

            Action updateCompSelection = () => {
                if (dgvComponents.CurrentRow != null)
                {
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    string desc = Convert.ToString(dgvComponents.CurrentRow.Cells["Description"].Value);
                    int schPage = getRowInt(dgvComponents.CurrentRow, "SchematicPage");
                    int locPage = getRowInt(dgvComponents.CurrentRow, "PlacementPage");

                    if (!string.IsNullOrEmpty(code))
                    {
                        string details = code + ": " + desc;
                        if (schPage > 0) details += " | ⚡ Schematic: Page " + schPage;
                        if (locPage > 0) details += " | 📍 Placement: Page " + locPage;
                        lblSelComp.Text = details;

                        btnFindSchematic.Text = (schPage > 0) ? "⚡ Schematic (P." + schPage + ")" : "⚡ Schematics";
                        btnFindSchematic.Enabled = (schPage > 0);

                        btnShowLoc.Text = (locPage > 0) ? "📍 Placement (P." + locPage + ")" : "📍 Car Placement";
                        btnShowLoc.Enabled = (locPage > 0);
                    }
                }
            };

            dgvComponents.SelectionChanged += (s, e) => {
                updateCompSelection();
            };

            dgvComponents.CellClick += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    string colName = dgvComponents.Columns[e.ColumnIndex].Name;
                    int schPage = getRowInt(dgvComponents.Rows[e.RowIndex], "SchematicPage");
                    int locPage = getRowInt(dgvComponents.Rows[e.RowIndex], "PlacementPage");

                    if (colName == "SchematicDisp" && schPage > 0)
                    {
                        jumpToPage(schPage);
                    }
                    else if ((colName == "PlacementDisp" || colName == "PageDisp") && locPage > 0)
                    {
                        jumpToPage(locPage);
                    }
                }
            };

            dgvComponents.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    int schPage = getRowInt(dgvComponents.Rows[e.RowIndex], "SchematicPage");
                    int locPage = getRowInt(dgvComponents.Rows[e.RowIndex], "PlacementPage");

                    if (schPage > 0)
                    {
                        jumpToPage(schPage);
                    }
                    else if (locPage > 0)
                    {
                        jumpToPage(locPage);
                    }
                }
            };

            btnShowLoc.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    int locPage = getRowInt(dgvComponents.CurrentRow, "PlacementPage");
                    if (locPage > 0) jumpToPage(locPage);
                }
            };

            btnFindSchematic.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    int schPage = getRowInt(dgvComponents.CurrentRow, "SchematicPage");
                    if (schPage > 0)
                    {
                        jumpToPage(schPage);
                    }
                    else
                    {
                        string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                        if (!string.IsNullOrEmpty(code))
                        {
                            Clipboard.SetText(code);
                            lblSelComp.Text = "Code " + code + " copied to clipboard (search in PDF)";
                        }
                    }
                }
            };

            btnCopyCode.Click += (s, e) => {
                if (dgvComponents.CurrentRow != null)
                {
                    string code = Convert.ToString(dgvComponents.CurrentRow.Cells["Code"].Value);
                    if (!string.IsNullOrEmpty(code))
                    {
                        Clipboard.SetText(code);
                        lblSelComp.Text = "Copied to clipboard: " + code;
                    }
                }
            };

            btnOpenPdf.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    wvPdf.Source = new Uri(curPdfPath);
                    updateNavStyles();
                }
            };

            btnOpenLop.Click += (s, e) => {
                if (File.Exists(curLopPdfPath))
                {
                    wvPdf.Source = new Uri(curLopPdfPath);
                    updateNavStyles();
                }
            };

            btnBrowser.Click += (s, e) => {
                if (File.Exists(curPdfPath))
                {
                    Process.Start(new ProcessStartInfo(curPdfPath) { UseShellExecute = true });
                }
                else if (!string.IsNullOrEmpty(curWebUrl))
                {
                    Process.Start(new ProcessStartInfo(curWebUrl) { UseShellExecute = true });
                }
            };

            _wiringDiagramsPnl = wiringPnl;
            // Default select V70 (08-)
            cboModel.SelectedIndex = 0;
            _wiringTabConfigured = true;
            Console.WriteLine("[Launcher] Electronic Wiring Diagrams (EWD) tab initialized successfully with native WebView2 PDF viewer!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Launcher] SetupWiringDiagramsTab error: " + ex);
        }
    }

}
