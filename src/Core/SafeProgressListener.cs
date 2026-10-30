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

public class SafeProgressListener : IProgressListener

{

    private readonly IProgressListener _inner;

    public SafeProgressListener(IProgressListener inner)

    {

        _inner = inner;

    }

    public string CommToolName

    {

        get { return _inner != null ? _inner.CommToolName : ""; }

    }

    public void OnProgressMessage(string message)

    {

        try { if (_inner != null) _inner.OnProgressMessage(message); } catch {}

    }

    public void OnVehcommMessage(int ecuVariantId)

    {

        try { if (_inner != null) _inner.OnVehcommMessage(ecuVariantId); } catch {}

    }

    public void OnDetailedProgressMessage(string message)

    {

        try { if (_inner != null) _inner.OnDetailedProgressMessage(message); } catch {}

    }

    public void OnProgress(int current, int total)

    {

        try { if (_inner != null) _inner.OnProgress(current, total); } catch {}

    }

    public void OnUseProgress(bool useIt)

    {

        try { 
        if (_inner != null) 
        { 
            if (useIt) _inner.OnProgressMessage("Connecting to vehicle via DiCE..."); 
            _inner.OnUseProgress(useIt); 
        } 
    } catch {}

    }

    public void OnReserveBusyBar(int busyValue, int max)

    {

        try { if (_inner != null) _inner.OnReserveBusyBar(busyValue, max); } catch {}

    }

    public void OnShowErrorMessage(string title, string message)

    {

        try { if (_inner != null) _inner.OnShowErrorMessage(title, message); } catch {}

    }

    public override bool Equals(object obj)

    {

        if (obj == null) return false;

        if (object.ReferenceEquals(this, obj) || object.ReferenceEquals(_inner, obj)) return true;

        if (obj is SafeProgressListener) return object.ReferenceEquals(_inner, ((SafeProgressListener)obj)._inner);

        return false;

    }

    public override int GetHashCode()

    {

        return _inner != null ? _inner.GetHashCode() : 0;

    }

}

public class SafeListenerList : System.Collections.ArrayList

{

    public override int Add(object value)

    {

        if (value is IProgressListener && !(value is SafeProgressListener))

        {

            value = new SafeProgressListener((IProgressListener)value);

        }

        return base.Add(value);

    }

    public override void Remove(object obj)

    {

        for (int i = Count - 1; i >= 0; i--)

        {

            object item = this[i];

            if (object.ReferenceEquals(item, obj) || item.Equals(obj))

            {

                RemoveAt(i);

            }

        }

    }

}

