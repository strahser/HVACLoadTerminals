using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HVACLoadTerminals.Core.Models.Snapshot;
using HVACLoadTerminals.Core.Services;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>
    /// M2.3/M1.1b: панель свойств помещения (ветка «Помещение» дерева), общая для
    /// обоих хостов. Нагрузки редактируются прямо в RoomRow — её PropertyChanged
    /// уже подключён к живому пересчёту presenter'а; проёмы/температура — из снимка.
    /// </summary>
    public class RoomPropertiesViewModel : INotifyPropertyChanged
    {
        private readonly CrmViewModel _owner;

        public RoomPropertiesViewModel(CrmViewModel owner) =>
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        private SnapshotWorkspacePresenter Workspace => _owner.Workspace;

        private RoomRow? _room;
        /// <summary>Редактируемая строка помещения (биндится напрямую).</summary>
        public RoomRow? Room
        {
            get => _room;
            private set { _room = value; OnPropertyChanged(nameof(Room)); }
        }

        private string _temperatureText = "—";
        public string TemperatureText
        {
            get => _temperatureText;
            set { _temperatureText = value; OnPropertyChanged(nameof(TemperatureText)); }
        }

        private string _openingsText = "";
        public string OpeningsText
        {
            get => _openingsText;
            set { _openingsText = value; OnPropertyChanged(nameof(OpeningsText)); }
        }

        private string _windowForecastText = "";
        public string WindowForecastText
        {
            get => _windowForecastText;
            set { _windowForecastText = value; OnPropertyChanged(nameof(WindowForecastText)); }
        }

        /// <summary>Обновить под выбранный узел дерева / последний расчёт.</summary>
        public void Refresh()
        {
            var node = _owner.SelectedNode;
            string? roomId = node?.Kind == "Room" ? node.RoomId : null;
            Room = roomId == null
                ? null
                : Workspace.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (Room == null)
            {
                TemperatureText = "—";
                OpeningsText = "";
                WindowForecastText = "";
                return;
            }

            var snap = Workspace.FindSnapshotRoom(Room.RoomId);
            TemperatureText = snap is { Temperature: > 0 }
                ? $"{snap.Temperature:F1} °C" : "—";

            var openings = Workspace.GetRoomOpenings(Room.RoomId);
            OpeningsText = openings.Count == 0
                ? "нет проёмов в снимке"
                : string.Join("\n", openings.Select(o =>
                {
                    double wMm = LengthUnitConverter.UnitsToMm(o.Width);
                    double hMm = LengthUnitConverter.UnitsToMm(o.Height);
                    string ext = o.IsExternal ? "" : " (внутр.)";
                    return $"{o.EnclosureType} {o.FamilySymbolName}: {wMm:F0}×{hMm:F0}{ext}";
                }));

            // Прогноз длины отопительных приборов: Σ ширина светопрозрачных × 0.6.
            double windowsWidthMm = openings
                .Where(o => o.IsExternal &&
                            (IsWindowLike(o.EnclosureType)))
                .Sum(o => LengthUnitConverter.UnitsToMm(o.Width));
            WindowForecastText = windowsWidthMm > 0
                ? $"Σ ширина окон {windowsWidthMm:F0} мм → длина приборов ≥ " +
                  $"{windowsWidthMm * Workspace.MinWindowLengthRatio:F0} мм"
                : "";

            static bool IsWindowLike(string enclosureType) =>
                enclosureType.Equals("Окно", StringComparison.OrdinalIgnoreCase) ||
                enclosureType.Equals("Витраж", StringComparison.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
