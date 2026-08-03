using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Infrastructure.Visualization;
using HVACLoadTerminals.Revit.Logging;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVACLoadTerminals.Revit.Visualization
{
    /// <summary>
    /// In-process WebView2 preview window used by the Revit add-in. Loads the
    /// HTML scene produced by <see cref="HtmlSceneExporter"/> and bridges it to
    /// Revit over the WebView2 postMessage protocol:
    ///   Host -> Page : { type: "scene", payload: &lt;scene json&gt; }
    ///   Page -> Host : { type: "apply" | "cancel" | "recompute", options? }
    /// </summary>
    public partial class WebView2PreviewWindow : Window
    {
        private readonly string _title;
        private string _sceneJson;
        private readonly Func<string> _recomputeSceneJson;
        private bool _applied;

        /// <summary>True when the user confirmed placement from within the page.</summary>
        public bool IsApplied => _applied;

        /// <summary>Minimal DTO for WebView2 host messages coming from the page.</summary>
        private class WebMessage
        {
            public string? Type { get; set; }
            public JObject? Options { get; set; }
        }

        public WebView2PreviewWindow(string title, string sceneJson, Func<string> recomputeSceneJson)
        {
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _sceneJson = sceneJson ?? throw new ArgumentNullException(nameof(sceneJson));
            _recomputeSceneJson = recomputeSceneJson ?? throw new ArgumentNullException(nameof(recomputeSceneJson));

            InitializeComponent();

            Title = _title;
            StatusText.Text = "WebView2: initializing...";

            Closed += OnWindowClosed;
            _ = InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HVACLoadTerminals", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                var htmlDir = Path.Combine(Path.GetTempPath(), "HVACLoadTerminalsPreview");
                var htmlPath = HtmlSceneExporter.SaveToFile(htmlDir, _title, _sceneJson);
                WebView.Source = new Uri(htmlPath);

                StatusText.Text = "WebView2: ready — scene loaded, messages active";
            }
            catch (Exception ex)
            {
                StatusText.Text = "WebView2 error: " + ex.Message;
                HvacLogger.LogException("WebView2 init", ex);
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (!e.IsSuccess || WebView.CoreWebView2 == null) return;
                SendScene();
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 navigation completed", ex);
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw;
            try
            {
                raw = e.TryGetWebMessageAsString();
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 message read", ex);
                return;
            }

            try
            {
                var msg = JsonConvert.DeserializeObject<WebMessage>(raw);
                if (msg == null || string.IsNullOrEmpty(msg.Type)) return;

                switch (msg.Type)
                {
                    case "apply":
                        Dispatcher.Invoke(() =>
                        {
                            _applied = true;
                            DialogResult = true;
                            Close();
                        });
                        break;

                    case "cancel":
                        Dispatcher.Invoke(() =>
                        {
                            DialogResult = false;
                            Close();
                        });
                        break;

                    case "recompute":
                        Dispatcher.Invoke(() => HandleRecompute());
                        break;
                }
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 message", ex);
            }
        }

        private void HandleRecompute()
        {
            try
            {
                var newJson = _recomputeSceneJson();
                if (string.IsNullOrWhiteSpace(newJson))
                {
                    StatusText.Text = "Recompute returned no scene";
                    return;
                }

                _sceneJson = newJson;
                SendScene();
                StatusText.Text = "Recomputed";
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("Recompute error", ex);
                TaskDialog.Show("Recompute error", ex.Message);
            }
        }

        private void SendScene()
        {
            if (WebView.CoreWebView2 == null) return;
            var message = JsonConvert.SerializeObject(new { type = "scene", payload = JObject.Parse(_sceneJson) });
            WebView.CoreWebView2.PostWebMessageAsString(message);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.PostWebMessageAsString(
                        JsonConvert.SerializeObject(new { type = "apply" }));
                    return;
                }
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 apply post", ex);
            }

            // Fallback when WebView2 is not available.
            _applied = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.PostWebMessageAsString(
                        JsonConvert.SerializeObject(new { type = "cancel" }));
                    return;
                }
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 cancel post", ex);
            }

            DialogResult = false;
            Close();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            try
            {
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                    WebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                }
                WebView.Dispose();
            }
            catch (Exception ex)
            {
                HvacLogger.LogException("WebView2 dispose", ex);
            }
        }
    }
}
