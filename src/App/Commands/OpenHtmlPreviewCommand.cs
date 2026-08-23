using System;
using System.Diagnostics;
using System.Windows.Input;
using HVACLoadTerminals.App;
using HVACLoadTerminals.Infrastructure.Visualization;

namespace HVACLoadTerminals.App.Commands
{
    /// <summary>
    /// Opens the interactive HTML preview of the placement scene. Primary path:
    /// the common in-process WebView2 host (<see cref="WebView2PreviewWindow"/>,
    /// offline file:// scene + postMessage bridge). Fallback when WebView2 is
    /// unavailable: local <see cref="HtmlPreviewServer"/> + system browser.
    /// All failures are reported as text (report sink / exception for the
    /// caller's status bar) instead of crashing.
    /// </summary>
    public class OpenHtmlPreviewCommand : ICommand
    {
        private const string DefaultTitle = "HVAC Load Terminals";

        private static HtmlPreviewServer? _fallbackServer;

        private readonly Func<string>? _getSceneJson;
        private readonly Action<string>? _report;
        private readonly string _title;
        private readonly bool _modal;

        /// <param name="getSceneJson">Scene factory: builds the initial JSON and re-runs on page Recompute.</param>
        /// <param name="report">Optional sink for non-fatal messages (status bar).</param>
        /// <param name="title">Window/document title.</param>
        /// <param name="modal">Modal window (Revit stand) or modeless so the user can
        /// change options in the main window and hit page Recompute (App).</param>
        public OpenHtmlPreviewCommand(Func<string>? getSceneJson = null, Action<string>? report = null,
            string? title = null, bool modal = true)
        {
            _getSceneJson = getSceneJson;
            _report = report;
            _title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title!;
            _modal = modal;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            WebView2PreviewWindow.LogSink ??= msg => AppLogger.Warn("[WebView2Preview] " + msg);

            string sceneJson = ResolveSceneJson(parameter);

            try
            {
                var window = new WebView2PreviewWindow(
                    _title, sceneJson, _getSceneJson ?? (() => sceneJson), _modal);
                if (_modal)
                    window.ShowDialog();
                else
                    window.Show();
                return;
            }
            catch (Exception ex)
            {
                Report("WebView2 недоступен (" + ex.Message + ") — открываю системный браузер");
            }

            StartServerAndOpenBrowser(sceneJson);
        }

        private string ResolveSceneJson(object? parameter)
        {
            if (parameter is string json && !string.IsNullOrWhiteSpace(json))
                return json;
            if (_getSceneJson != null)
                return _getSceneJson();
            return "{\"Title\":\"\",\"Rooms\":[]}";
        }

        private void StartServerAndOpenBrowser(string sceneJson)
        {
            if (_fallbackServer == null)
                _fallbackServer = new HtmlPreviewServer(_title, sceneJson,
                    _getSceneJson ?? (() => sceneJson));
            else
                _fallbackServer.RecomputeScene(sceneJson);

            try
            {
                if (!_fallbackServer.IsRunning)
                    _fallbackServer.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "HTML-превью недоступно: не удалось запустить локальный сервер — " + ex.Message, ex);
            }

            Report("Превью в системном браузере: " + _fallbackServer.BaseUrl);

            try
            {
                Process.Start(new ProcessStartInfo(_fallbackServer.BaseUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Не удалось открыть браузер: " + ex.Message +
                    "\nОткройте вручную: " + _fallbackServer.BaseUrl, ex);
            }
        }

        private void Report(string message) => _report?.Invoke(message);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
