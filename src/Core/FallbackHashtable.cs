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

public class FallbackHashtable : System.Collections.Hashtable
{
    public object FallbackValue;
    public FallbackHashtable(object fallback) { FallbackValue = fallback; }
    public override bool ContainsKey(object key) { return true; }
    public override bool Contains(object key) { return true; }
    public override object this[object key]
    {
        get
        {
            if (base.ContainsKey(key)) return base[key];
            return FallbackValue;
        }
        set { base[key] = value; }
    }
}

