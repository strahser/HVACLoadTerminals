using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Presentation;
using HVACLoadTerminals.Infrastructure.Visualization;
using HVACLoadTerminals.Revit.Logging;

namespace HVACLoadTerminals.Revit.UI
{
    /// <summary>
    /// Modeless snapshot placement window over the shared presenter (plan C3.3).
    /// Shown with Show() — Revit stays responsive; model writes go through the
    /// ExternalEvent handler only.
    /// </summary>
    public partial class SnapshotPlacementWindow : Window
    {
        private readonly SnapshotWorkspacePresenter _presenter;
        private readonly PlaceDevicesExternalEventHandler? _handler;
        private readonly ExternalEvent? _externalEvent;

        /// <param name="handler">Обработчик записи в модель. Null → окно работает
        /// без записи в модель (дизайн-ревью/скриншоты вне API-контекста Revit;
        /// ExternalEvent.Create допустим только внутри Revit).</param>
        public SnapshotPlacementWindow(
            SnapshotWorkspacePresenter presenter,
            PlaceDevicesExternalEventHandler? handler)
        {
            InitializeComponent();
            _presenter = presenter;
            _handler = handler;

            DataContext = _presenter;
            _presenter.StateChanged += OnStateChanged;

            if (handler != null)
            {
                _externalEvent = ExternalEvent.Create(handler); // valid: called in API context
                handler.Completed += status => StatusText.Text = status;
            }

            // U3.1: валидация числовых полей и прочие предупреждения — в статус-строку.
            _presenter.ErrorSink = msg => StatusText.Text = msg;

            Closed += (_, _) => _presenter.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(WorkspaceState state)
        {
            RoomsGrid.ItemsSource = state.Rooms;
            PlacementsGrid.ItemsSource = state.IsCalculation
                ? state.Placements
                : PlacementsGrid.ItemsSource;
            StatusText.Text = state.Status;
        }

        private void OpenSnapshot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите снимок помещений HeatLossRevit2",
                Filter = "Снимки (*.json)|*.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                _presenter.LoadSnapshot(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка чтения снимка: " + ex.Message;
            }
        }

        private void EditSystems_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RoomRow row)
                new SystemEditorWindow(row) { Owner = this }.ShowDialog();
        }

        private void RegenLoads_Click(object sender, RoutedEventArgs e)        {
            try
            {
                _presenter.RegenerateLoads();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.Message;
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e) =>
            _presenter.Calculate();

        // ------------------------------------------------------------------
        // U3.1: паритет тулбара с App — назначение, проект, HTML
        // ------------------------------------------------------------------

        private void ApplyPurpose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // На стенде нет фильтра уровня — назначение ко всем помещениям
                // (как «Все уровни» в App).
                _presenter.ApplyPurpose(_ => true,
                    PurposeBox?.Text?.Trim() is { Length: > 0 } purpose ? purpose : "");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка назначения: " + ex.Message;
            }
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (_presenter.Rooms.Count == 0)
            {
                StatusText.Text = "Нет проекта для сохранения";
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json"
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                _presenter.SaveProject(dlg.FileName);
                StatusText.Text = $"Проект сохранён: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка сохранения: " + ex.Message;
            }
        }

        private void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Проект размещения (*.hvacproj.json)|*.hvacproj.json|Все файлы|*.*"
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                _presenter.LoadProject(dlg.FileName); // raises StateChanged
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки проекта: " + ex.Message;
            }
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_presenter.CurrentSnapshot == null || _presenter.LastRawPlacements.Count == 0)
            {
                StatusText.Text = "Рассчитайте размещение перед экспортом HTML";
                return;
            }

            try
            {
                string title = "Расстановка по снимку — стенд";
                string BuildSceneJson() => PlacementSceneSerializer.ToJson(
                    _presenter.BuildPlacementResults(), title);

                WebView2PreviewWindow.LogSink ??= msg =>
                    HvacLogger.Warn("[WebView2Preview] " + msg);

                try
                {
                    // Общий WebView2-хост из Infrastructure (как в RevitHtmlPlacementCommand),
                    // немодальный: можно править опции стенда и жать «Пересчитать».
                    string initialJson = BuildSceneJson();
                    var wv2 = new WebView2PreviewWindow(
                        title, initialJson,
                        recomputeSceneJson: () =>
                        {
                            _presenter.Calculate();
                            return BuildSceneJson();
                        },
                        modal: false);
                    wv2.Show();
                    StatusText.Text = "HTML-превью открыт";
                }
                catch (Exception wv2Ex)
                {
                    // Fallback U1.3: файл на диск + системный браузер.
                    string htmlPath = HtmlSceneExporter.SaveToFile(
                        Path.Combine(Path.GetTempPath(), "HVACLoadTerminalsPreview"),
                        title, BuildSceneJson());
                    StatusText.Text = $"WebView2 недоступен ({wv2Ex.Message}) — открыта копия в браузере";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(htmlPath)
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка экспорта HTML: " + ex.Message;
            }
        }

        private void Place_Click(object sender, RoutedEventArgs e)
        {
            var raw = _presenter.LastRawPlacements;
            if (_presenter.CurrentSnapshot == null || raw.Count == 0)
            {
                StatusText.Text = "Откройте снимок и рассчитайте размещение";
                return;
            }
            if (_handler == null || _externalEvent == null)
            {
                StatusText.Text =
                    "Запись в модель недоступна: окно открыто без Revit API";
                return;
            }

            _handler.SetPending(new PlaceRequest
            {
                Placements = raw.ToList(),
                RoomLevels = _presenter.CurrentSnapshot.Rooms
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().LevelName ?? ""),
                DocumentTitle = !string.IsNullOrEmpty(_presenter.CurrentSnapshot.Metadata?.DocumentTitle)
                    ? Path.GetFileNameWithoutExtension(_presenter.CurrentSnapshot.Metadata.DocumentTitle)
                    : "Snapshot"
            });
            _externalEvent.Raise();
        }
    }
}
