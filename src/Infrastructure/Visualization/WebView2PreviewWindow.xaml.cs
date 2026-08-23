using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using HVACLoadTerminals.Infrastructure.Visualization;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>
    /// Common in-process WebView2 preview window (generalized from the Revit
    /// add-in host; used by the standalone App and the Revit stand). Loads the
    /// HTML scene produced by <see cref="HtmlSceneExporter"/> (file://, fully
    /// offline) and bridges it over the WebView2 postMessage protocol:
    ///   Host -> Page : { type: "scene", payload: &lt;scene json&gt; }
    ///   Page -> Host : { type: "apply" | "cancel" | "recompute", options? }
    /// All errors are surfaced as text in the status bar — the window never
    /// crashes the host application.
    /// </summary>
    public partial class WebView2PreviewWindow : Window
    {
        /// <summary>Optional host log sink (App/Revit wire their loggers here).</summary>
        public static Action<string>? LogSink;

        private readonly string _title;
        private readonly bool _isModal;
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

        public WebView2PreviewWindow(string title, string sceneJson, Func<string> recomputeSceneJson,
            bool modal = true)
        {
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _sceneJson = sceneJson ?? throw new ArgumentNullException(nameof(sceneJson));
            _recomputeSceneJson = recomputeSceneJson ?? throw new ArgumentNullException(nameof(recomputeSceneJson));
            _isModal = modal;

            InitializeComponent();

            Title = string.IsNullOrWhiteSpace(_title) ? "HTML Preview" : _title;
            StatusText.Text = "WebView2: инициализация...";

            Closed += OnWindowClosed;
            _ = InitializeWebViewAsync();
        }

        private static class NativeLoader
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string fileName);
        }

        /// <summary>
        /// NuGet кладёт в корень вывода win-x86 WebView2Loader.dll, поэтому в
        /// 64-битном процессе инициализация падала с HRESULT 0x8007000B.
        /// Предзагружаем загрузчик нужной разрядности из runtimes\ — тогда
        /// последующий P/Invoke "WebView2Loader.dll" подхватит уже загруженный
        /// модуль независимо от того, что лежит рядом с exe.
        /// </summary>
        private static void TryPreloadNativeLoader()
        {
            try
            {
                var baseDir = Path.GetDirectoryName(typeof(WebView2PreviewWindow).Assembly.Location);
                if (string.IsNullOrEmpty(baseDir)) return;

                var arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                var candidate = Path.Combine(baseDir, "runtimes", arch, "native", "WebView2Loader.dll");
                if (File.Exists(candidate))
                    NativeLoader.LoadLibrary(candidate);
            }
            catch (Exception ex)
            {
                LogSink?.Invoke("WebView2 loader preload: " + ex.Message);
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                TryPreloadNativeLoader();

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

                StatusText.Text = "WebView2: готово — сцена загружена, мост сообщений активен";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка WebView2: " + ex.Message;
                LogSink?.Invoke("WebView2 init: " + ex.Message);
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
                LogSink?.Invoke("WebView2 navigation completed: " + ex.Message);
            }
        }

        /// <summary>Closes the window, setting DialogResult only for modal show.</summary>
        private void CloseWithResult(bool result)
        {
            _applied = result;
            if (_isModal)
            {
                try { DialogResult = result; }
                catch (Exception ex) { LogSink?.Invoke("WebView2 DialogResult: " + ex.Message); }
            }
            Close();
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
                LogSink?.Invoke("WebView2 message read: " + ex.Message);
                return;
            }

            try
            {
                var msg = JsonConvert.DeserializeObject<WebMessage>(raw);
                if (msg == null || string.IsNullOrEmpty(msg.Type)) return;

                switch (msg.Type)
                {
                    case "apply":
                        Dispatcher.Invoke(() => CloseWithResult(true));
                        break;

                    case "cancel":
                        Dispatcher.Invoke(() => CloseWithResult(false));
                        break;

                    case "recompute":
                        Dispatcher.Invoke(() => HandleRecompute());
                        break;
                }
            }
            catch (Exception ex)
            {
                LogSink?.Invoke("WebView2 message: " + ex.Message);
            }
        }

        private void HandleRecompute()
        {
            try
            {
                var newJson = _recomputeSceneJson();
                if (string.IsNullOrWhiteSpace(newJson))
                {
                    StatusText.Text = "Пересчёт не вернул сцену";
                    return;
                }

                _sceneJson = newJson;
                SendScene();
                StatusText.Text = "Пересчитано";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка пересчёта: " + ex.Message;
                LogSink?.Invoke("Recompute error: " + ex.Message);
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
                LogSink?.Invoke("WebView2 apply post: " + ex.Message);
            }

            // Fallback when WebView2 is not available.
            CloseWithResult(true);
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
                    LogSink?.Invoke("WebView2 cancel post: " + ex.Message);
                }

            CloseWithResult(false);
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
                LogSink?.Invoke("WebView2 dispose: " + ex.Message);
            }
        }
    }
}
