using System;
using System.Windows.Input;
using HVACLoadTerminals.App.Views;
using HVACLoadTerminals.Infrastructure.Visualization;

namespace HVACLoadTerminals.App.Commands
{
    public class OpenHtmlPreviewCommand : ICommand
    {
        private readonly Func<string>? _getSceneJson;

        public OpenHtmlPreviewCommand(Func<string>? getSceneJson = null)
        {
            _getSceneJson = getSceneJson;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            string sceneJson;
            if (parameter is string json && !string.IsNullOrWhiteSpace(json))
            {
                sceneJson = json;
            }
            else if (_getSceneJson != null)
            {
                sceneJson = _getSceneJson();
            }
            else
            {
                sceneJson = "{\"Title\":\"\",\"Rooms\":[]}";
            }

            var server = new HtmlPreviewServer("HVAC Load Terminals", sceneJson, () => sceneJson);
            server.Start();

            var window = new HtmlPreviewWindow(server);
            window.ShowDialog();
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
