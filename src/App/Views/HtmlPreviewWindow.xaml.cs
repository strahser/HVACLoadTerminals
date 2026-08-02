using System;
using System.Windows;
using HVACLoadTerminals.Infrastructure.Visualization;

namespace HVACLoadTerminals.App.Views
{
    public partial class HtmlPreviewWindow : Window
    {
        private readonly IHtmlPreviewHost _host;
        private readonly Action? _recompute;

        public HtmlPreviewWindow(IHtmlPreviewHost host, Action? recompute = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _recompute = recompute;

            InitializeComponent();

            StatusText.Text = host.IsRunning ? host.BaseUrl : "Server not running";

            if (host.IsRunning && !string.IsNullOrEmpty(host.BaseUrl))
            {
                PreviewBrowser.Navigate(new Uri(host.BaseUrl));
            }

            Closed += (s, e) =>
            {
                if (host.IsRunning) host.Stop();
                host.Dispose();
            };
        }

        private void Recompute_Click(object sender, RoutedEventArgs e)
        {
            _recompute?.Invoke();
            if (_host.IsRunning && !string.IsNullOrEmpty(_host.BaseUrl))
            {
                PreviewBrowser.Refresh();
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _host.Apply();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _host.Cancel();
            DialogResult = false;
            Close();
        }
    }
}
