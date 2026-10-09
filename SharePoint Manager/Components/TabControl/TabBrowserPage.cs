using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Keutmann.SharePointManager.Library;
#if SPSE
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
#endif

namespace Keutmann.SharePointManager.Components
{
    /// <summary>
    /// The embedded browser tab.
    ///
    /// On the SE build this hosts Microsoft Edge (WebView2). The legacy WinForms
    /// WebBrowser control runs in IE7 document mode unless the process is registered
    /// under FEATURE_BROWSER_EMULATION, which cannot render a modern SharePoint page -
    /// scripts fail on ES6 constructs such as Map being undefined. WebView2 is Chromium
    /// based and needs no registry opt-in.
    ///
    /// The pre-SE configurations still target .NET Framework 4.0 and cannot reference
    /// the net462 WebView2 assemblies, so they keep the old control.
    /// </summary>
    public class TabBrowserPage : TabPage
    {
#if SPSE
        private readonly WebView2 _browser;
        private Uri _pendingUrl;
        private bool _ready;

        public TabBrowserPage()
            : base()
        {
            this.Controls.Clear();
            this.Name = "Browser";
            this.Text = SPMLocalization.GetString("Browser_Text");
            this.UseVisualStyleBackColor = true;

            _browser = new WebView2();
            _browser.Dock = DockStyle.Fill;
            _browser.CoreWebView2InitializationCompleted += Browser_InitializationCompleted;
            this.Controls.Add(_browser);

            InitializeBrowserAsync();
        }

        /// <summary>
        /// WebView2 initializes asynchronously. Navigation requested before it is ready is
        /// held in _pendingUrl and applied once initialization completes.
        /// </summary>
        private async void InitializeBrowserAsync()
        {
            try
            {
                // An explicit user data folder is required: the default sits beside the
                // executable, which is not writable when the tool is run from Program Files.
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SharePointManager", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _browser.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                ShowBrowserUnavailable(ex);
            }
        }

        private void Browser_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                ShowBrowserUnavailable(e.InitializationException);
                return;
            }

            _ready = true;

            if (_pendingUrl != null)
            {
                _browser.Source = _pendingUrl;
                _pendingUrl = null;
            }
        }

        /// <summary>
        /// Replaces the control with an explanation rather than letting a missing runtime
        /// take down the tab.
        /// </summary>
        private void ShowBrowserUnavailable(Exception ex)
        {
            Trace.WriteLine("WebView2 initialization failed: " + ex);

            this.Controls.Remove(_browser);

            var message = new Label();
            message.Dock = DockStyle.Fill;
            message.Padding = new Padding(12);
            message.Text = "The embedded browser could not be started." + Environment.NewLine + Environment.NewLine +
                           "SharePoint Manager renders pages with the Microsoft Edge WebView2 runtime. " +
                           "Install the Evergreen WebView2 Runtime and restart the application." +
                           Environment.NewLine + Environment.NewLine +
                           (ex != null ? ex.Message : String.Empty);
            this.Controls.Add(message);
        }

        public string Url
        {
            get
            {
                if (_browser == null || _browser.Source == null) return string.Empty;
                return _browser.Source.ToString();
            }
            set
            {
                var uri = new Uri(value);

                if (_ready)
                {
                    _browser.Source = uri;
                }
                else
                {
                    _pendingUrl = uri;
                }

                Trace.WriteLine("Browser updated with url: " + value);
            }
        }
#else
        private WebBrowser _browser = null;

        public WebBrowser Browser
        {
            get
            {
                if (_browser == null)
                {
                    _browser = new WebBrowser();
                    _browser.Dock = DockStyle.Fill;
                    _browser.AllowNavigation = true;

                    // Without this, every script failure on a modern SharePoint page raises
                    // a modal IE script-error dialog over the application.
                    _browser.ScriptErrorsSuppressed = true;
                }
                return _browser;
            }
        }

        public TabBrowserPage()
            : base()
        {
            this.Controls.Clear();
            this.Controls.Add(Browser);
            this.Name = "Browser";
            this.Text = SPMLocalization.GetString("Browser_Text");
            this.UseVisualStyleBackColor = true;
        }

        public string Url
        {
            get
            {
                return Browser.Url + string.Empty;
            }
            set
            {
                Browser.Url = new Uri(value);
                Trace.WriteLine("Browser updated with url: " + value);
            }
        }
#endif

        public TabBrowserPage(string titel, string url)
            : this()
        {
            this.Text = titel;
            this.Url = url;
        }
    }
}
