using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Lets Tab and Shift+Tab leave an embedded web page. Help's support page embeds the Zoho SalesIQ chat in an
    /// iframe that keeps focus to itself, so a keyboard user could never Tab back to the app (found by the E2E
    /// keyboard check). A small script, run in the page and in every iframe, tells the app when Tab would go past
    /// the frame's last focusable element (or Shift+Tab before its first), or when focus sits on something that
    /// isn't a Tab stop; the app then moves WPF focus on from the WebView2.
    /// </summary>
    internal static class WebViewTabOut
    {
        private const string Script = @"(function () {
  if (window.__fbTabOut) return; window.__fbTabOut = true;
  var sel = 'a[href],button:not([disabled]),input:not([disabled]):not([type=hidden]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex=""-1""]),[contenteditable=""true""]';
  window.addEventListener('keydown', function (e) {
    if (e.key !== 'Tab' || e.ctrlKey || e.altKey || e.metaKey) return;
    if (!window.chrome || !window.chrome.webview) return;
    var all = Array.prototype.filter.call(document.querySelectorAll(sel), function (el) {
      return el.offsetParent !== null && el.getAttribute('aria-hidden') !== 'true';
    });
    var active = document.activeElement;
    if (active && active.tagName === 'IFRAME') return; // that frame's own script answers
    var index = all.indexOf(active);
    var out = index < 0 || (!e.shiftKey && index === all.length - 1) || (e.shiftKey && index === 0);
    if (!out) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    window.chrome.webview.postMessage({ fbTabOut: e.shiftKey ? 'previous' : 'next' });
  }, true);
})();";

        /// <summary>Call once the WebView2's CoreWebView2 exists (its CoreWebView2InitializationCompleted).</summary>
        internal static void Attach(WebView2 webView)
        {
            try
            {
                var core = webView?.CoreWebView2;
                if (core == null) return;
                _ = core.AddScriptToExecuteOnDocumentCreatedAsync(Script);
                webView.WebMessageReceived += (s, e) => OnMessage(webView, e.WebMessageAsJson);
                // Iframes (the chat widget) get the script too, and their messages come through the frame.
                core.FrameCreated += (s, e) =>
                {
                    CoreWebView2Frame frame = e.Frame;
                    frame.WebMessageReceived += (fs, fe) => OnMessage(webView, fe.WebMessageAsJson);
                    frame.NavigationCompleted += async (fs, fe) =>
                    {
                        try { await frame.ExecuteScriptAsync(Script); }
                        catch (Exception ex) { LogWriter.LogWrite("WebViewTabOut frame script ex - " + ex.Message); }
                    };
                };
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTabOut.Attach ex - " + ex.Message);
            }
        }

        private static void OnMessage(WebView2 webView, string json)
        {
            try
            {
                if (json == null || json.IndexOf("fbTabOut", StringComparison.Ordinal) < 0) return;
                var direction = json.IndexOf("previous", StringComparison.Ordinal) >= 0
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next;
                webView.Dispatcher.BeginInvoke((Action)(() => webView.MoveFocus(new TraversalRequest(direction))));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WebViewTabOut message ex - " + ex.Message);
            }
        }
    }
}
