using System;
using System.Windows;
using System.Windows.Threading;

namespace HVACLoadTerminals.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppLogger.Info("=== App started ===");
            AppLogger.Info("Version: " + typeof(App).Assembly.GetName().Version +
                           " | " + typeof(App).Assembly.Location);

            DispatcherUnhandledException += (_, args) =>
            {
                AppLogger.Error("DispatcherUnhandledException", args.Exception);
                MessageBox.Show(
                    "Непредвиденная ошибка:\n" + args.Exception.Message +
                    "\n\nЛог: " + AppLogger.LogDirectory,
                    "HVAC Terminals", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // не роняем приложение
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                AppLogger.Error("AppDomain.UnhandledException",
                    args.ExceptionObject as Exception);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLogger.Error("UnobservedTaskException", args.Exception);
                args.SetObserved();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.Info("=== App exit, code=" + e.ApplicationExitCode + " ===");
            base.OnExit(e);
        }
    }
}
