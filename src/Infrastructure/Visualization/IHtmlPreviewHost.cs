using System;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>
    /// Abstraction over the HTML preview surface that hosts the local scene server.
    /// Implementations: <see cref="HtmlPreviewServer"/> (local HTTP bridge used by
    /// the WPF window and the Revit dialog), or an in-process WebView2 host.
    /// </summary>
    public interface IHtmlPreviewHost : IDisposable
    {
        /// <summary>Starts the host (binds the port / opens the window).</summary>
        void Start();

        /// <summary>Stops the host and releases all resources.</summary>
        void Stop();

        /// <summary>True while the host is running and able to serve requests.</summary>
        bool IsRunning { get; }

        /// <summary>Root URL of the preview surface (e.g. http://127.0.0.1:PORT/).</summary>
        string BaseUrl { get; }

        /// <summary>Replaces the scene JSON served to the client.</summary>
        void RecomputeScene(string sceneJson);

        /// <summary>Applies the currently staged options (host-specific hook).</summary>
        void Apply();

        /// <summary>Cancels the current interaction (host-specific hook).</summary>
        void Cancel();
    }
}
