using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>
    /// Local HTTP preview server bridging the HTML scene document and the hosting
    /// application (WPF window, Revit dialog). Routes:
    ///   GET  /               - full HTML document (HtmlSceneExporter.BuildHtml)
    ///   GET  /api/scene      - current scene JSON
    ///   POST /api/recompute  - recompute scene from changed UI options, returns new JSON
    /// The port is auto-selected on a free loopback port.
    /// </summary>
    public sealed class HtmlPreviewServer : IHtmlPreviewHost
    {
        private readonly object _sync = new object();
        private readonly string _title;
        private readonly Func<string> _recomputeSceneJson;

        private HttpListener? _listener;
        private int _port;
        private string _sceneJson;

        /// <summary>
        /// Creates the server. <paramref name="recomputeSceneJson"/> is a callback
        /// returning the NEW scene JSON whenever the browser POSTs /api/recompute
        /// (UI options changed).
        /// </summary>
        public HtmlPreviewServer(string title, string initialSceneJson, Func<string> recomputeSceneJson)
        {
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _sceneJson = string.IsNullOrWhiteSpace(initialSceneJson)
                ? "{\"Title\":\"\",\"Rooms\":[]}"
                : initialSceneJson;
            _recomputeSceneJson = recomputeSceneJson ?? throw new ArgumentNullException(nameof(recomputeSceneJson));
        }

        public string BaseUrl => _listener == null ? string.Empty : "http://127.0.0.1:" + _port + "/";

        public bool IsRunning => _listener != null && _listener.IsListening;

        public void Start()
        {
            if (IsRunning)
                return;

            int port = FindFreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            listener.Start();

            _port = port;
            _listener = listener;

            // Fire-and-forget accept loop; each context is handled on a pool thread.
            // The loop exits when the listener is stopped or closed.
            _ = Task.Run(() => AcceptLoop(listener));
        }

        public void Stop()
        {
            var listener = _listener;
            _listener = null;

            if (listener == null)
                return;

            try { listener.Stop(); }
            catch (HttpListenerException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            try { listener.Close(); }
            catch (HttpListenerException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public void Dispose() => Stop();

        public void RecomputeScene(string sceneJson)
        {
            lock (_sync)
            {
                _sceneJson = sceneJson;
            }
        }

        /// <summary>No-op for the local server; reserved for window/dialog hosts.</summary>
        public void Apply() { }

        /// <summary>No-op for the local server; reserved for window/dialog hosts.</summary>
        public void Cancel() { }

        private async Task AcceptLoop(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break; // listener stopped or closed
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => HandleContext(context));
            }
        }

        private void HandleContext(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                string path = request.Url?.AbsolutePath ?? "/";
                byte[] body;
                string contentType;
                int statusCode;

                if (request.HttpMethod == "GET" && path == "/")
                {
                    string html;
                    lock (_sync)
                    {
                        html = HtmlSceneExporter.BuildHtml(_title, _sceneJson);
                    }
                    body = Encoding.UTF8.GetBytes(html);
                    contentType = "text/html; charset=utf-8";
                    statusCode = 200;
                }
                else if (request.HttpMethod == "GET" && path == "/api/scene")
                {
                    string json;
                    lock (_sync)
                    {
                        json = _sceneJson;
                    }
                    body = Encoding.UTF8.GetBytes(json);
                    contentType = "application/json; charset=utf-8";
                    statusCode = 200;
                }
                else if (request.HttpMethod == "POST" && path == "/api/recompute")
                {
                    string json;
                    lock (_sync)
                    {
                        json = _recomputeSceneJson();
                        _sceneJson = json;
                    }
                    body = Encoding.UTF8.GetBytes(json);
                    contentType = "application/json; charset=utf-8";
                    statusCode = 200;
                }
                else
                {
                    body = Encoding.UTF8.GetBytes("Not Found");
                    contentType = "text/plain; charset=utf-8";
                    statusCode = 404;
                }

                response.StatusCode = statusCode;
                response.ContentType = contentType;
                response.ContentLength64 = body.Length;
                response.OutputStream.Write(body, 0, body.Length);
                response.OutputStream.Close();
            }
            catch (HttpListenerException)
            {
                // Listener closed while a response was in flight (expected during Stop()).
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
                // Client disconnected before the response was fully written.
            }
        }

        private static int FindFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                probe.Start();
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }
    }
}
