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
    public class ServiceManualsTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation

{

    public void Activated() { this.Visible = true; }

    public void Deactivated() { this.Visible = false; }

}

static Panel _diagnosticsMainPanel = null;

    static ServiceManualsTabControl _manualExplorerPnl = null;
    public class PartsCatalogTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation
    {
        public void Activated() { this.Visible = true; }
        public void Deactivated() { this.Visible = false; }
    }
    static PartsCatalogTabControl _partsCatalogPnl = null;
    static Button _btnTabParts = null;
    static bool _partsTabConfigured = false;
    public class WiringDiagramsTabControl : Panel, Vcc.Vida.DiagSwdl.UI.Components.ITabActivation
    {
        public void Activated() { this.Visible = true; }
        public void Deactivated() { this.Visible = false; }
    }
    static WiringDiagramsTabControl _wiringDiagramsPnl = null;
    static Button _btnTabWiring = null;
    static bool _wiringTabConfigured = false;
    static Microsoft.Web.WebView2.WinForms.WebView2 _ewdWebView = null;
    static DataGridView _ewdDgv = null;
    static Button _ewdBtnShowLoc = null;
    static Button _ewdBtnFindSchem = null;

    static Button _btnTabManuals = null;


    class EwdPublicationItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string PubId { get; set; }
        public override string ToString() { return Name; }
    }

    class EwdModelItem
    {
        public string DisplayName { get; set; }
        public string Key { get; set; }
        public override string ToString() { return DisplayName; }
    }


    class ManualDocItem

    {

        public string Id { get; set; }

        public string Title { get; set; }

        public string Vcc { get; set; }

        public string Version { get; set; }

        public string GroupKey { get; set; }

        public string GroupTitle { get; set; }

        public override string ToString()

        {

            if (!string.IsNullOrEmpty(Vcc))

                return string.Format("{0} [{1}]", Title, Vcc);

            return Title;

        }

    }

}
