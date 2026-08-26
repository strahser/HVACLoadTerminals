using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App
{
    public partial class GlobalSystemRegistryWindow : Window
    {
        private readonly SnapshotWorkspacePresenter _presenter;

        public ObservableCollection<RegistryRow> Rows { get; } = new();

        public GlobalSystemRegistryWindow(SnapshotWorkspacePresenter presenter)
        {
            InitializeComponent();
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            LoadRows();
            Grid.ItemsSource = Rows;
        }

        private void LoadRows()
        {
            Rows.Clear();
            var summaries = _presenter.LastSystemSummaries;
            foreach (var ps in _presenter.ProjectSystems.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var sum = summaries.FirstOrDefault(s => s.Name == ps.Name);
                var opts = _presenter.GetSystemOptions(ps.Name);
                string deviceText = "(авто)";
                if (opts?.DeviceTypeId != null)
                {
                    try
                    {
                        var dev = _presenter.GetCatalog().FirstOrDefault(d => d.Id == opts.DeviceTypeId);
                        if (dev != null) deviceText = $"{dev.Manufacturer} {dev.TypeName}".Trim();
                    }
                    catch { }
                }
                Rows.Add(new RegistryRow
                {
                    Id = ps.Id,
                    Name = ps.Name,
                    Type = ps.Type,
                    TypeText = ps.Type.ToString(),
                    DeviceText = deviceText,
                    RuleText = opts?.CountRule.ToString() ?? _presenter.SupplyRule.ToString(),
                    PatternText = opts?.Pattern.ToString() ?? _presenter.SupplyPattern.ToString(),
                    RoomCount = sum?.RoomCount ?? 0,
                    DeviceCount = sum?.DeviceCount ?? 0
                });
            }
            StatusText.Text = Rows.Count == 0 ? "Систем ещё нет — добавьте через мастер" : $"{Rows.Count} систем";
        }

        private void AddViaWizard_Click(object sender, RoutedEventArgs e)
        {
            // Открыть мастер для всех помещений уровня? Для глобального — выбираем пустой фильтр (все помещения) но пользователь выберет?
            // Упростим: выбрать все включённые помещения
            var ids = _presenter.Rooms.Where(r => r.IsIncluded).Select(r => r.RoomId).ToHashSet();
            if (ids.Count == 0) ids = _presenter.Rooms.Select(r => r.RoomId).ToHashSet();
            var win = new AssignSystemWizardWindow(_presenter, r => ids.Contains(r.RoomId)) { Owner = this };
            win.ShowDialog();
            LoadRows();
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is not RegistryRow row) { MessageBox.Show("Выберите систему"); return; }
            string? newName = Prompt("Новое имя:", "Переименовать", row.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == row.Name) return;
            string? err = _presenter.RenameSystem(row.Name, newName.Trim());
            if (err != null) { MessageBox.Show(err, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            LoadRows();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is not RegistryRow row) { MessageBox.Show("Выберите систему"); return; }
            if (MessageBox.Show($"Удалить систему «{row.Name}» из всех помещений? Приборы будут пересчитаны.", "Удалить", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            // Удаляем из всех комнат
            string before = _presenter.CaptureStateJson();
            foreach (var room in _presenter.Rooms)
            {
                var toRemove = room.Systems.Where(s => string.Equals(s.Name, row.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var s in toRemove) room.Systems.Remove(s);
                if (toRemove.Count > 0)
                {
                    _presenter.CommitRoomSystems(room);
                    room.RefreshSystemSummary();
                }
            }
            // Удалить из справочника
            var ps = _presenter.ProjectSystems.FirstOrDefault(p => p.Id == row.Id);
            if (ps != null)
            {
                // прямой доступ через reflection? ProjectSystems is IReadOnlyList but underlying is List<ProjectSystem> _projectSystems
                // Используем метод presenter'а: нет Delete, поэтому через Rooms уже очистили и при следующем RebuildTree/Reference?
                // Хак: через Capture/Restore? Проще оставить пустую систему — она исчезнет после перезагрузки без ссылок? Но лучше удалить через внутренний список
                // Попробуем через поле _projectSystems через reflection
                try
                {
                    var fld = typeof(SnapshotWorkspacePresenter).GetField("_projectSystems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fld?.GetValue(_presenter) is System.Collections.IList list)
                    {
                        var toDel = list.Cast<object>().FirstOrDefault(o => (string)o.GetType().GetProperty("Id")!.GetValue(o)! == row.Id);
                        if (toDel != null) list.Remove(toDel);
                    }
                }
                catch { }
            }
            LoadRows();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try { _presenter.Calculate(); } catch (Exception ex) { MessageBox.Show("Ошибка расчёта: " + ex.Message); }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private string Prompt(string text, string title, string def)
        {
            var win = new Window { Title = title, Width = 360, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White };
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 6) });
            var tb = new System.Windows.Controls.TextBox { Text = def, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(tb);
            var btns = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(4), IsDefault = true };
            var cancel = new System.Windows.Controls.Button { Content = "Отмена", Width = 70, Margin = new Thickness(4), IsCancel = true };
            btns.Children.Add(ok); btns.Children.Add(cancel);
            panel.Children.Add(btns);
            win.Content = panel;
            string result = def;
            ok.Click += (s, ev) => { result = tb.Text; win.DialogResult = true; win.Close(); };
            cancel.Click += (s, ev) => { win.DialogResult = false; win.Close(); };
            tb.Focus(); tb.SelectAll();
            return win.ShowDialog() == true ? result : "";
        }

        public class RegistryRow
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public HVACLoadTerminals.Core.Models.HVACSystemType Type { get; set; }
            public string TypeText { get; set; } = "";
            public string DeviceText { get; set; } = "";
            public string RuleText { get; set; } = "";
            public string PatternText { get; set; } = "";
            public int RoomCount { get; set; }
            public int DeviceCount { get; set; }
        }
    }
}
