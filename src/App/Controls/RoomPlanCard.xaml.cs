using System.Windows;
using System.Windows.Controls;
using HVACLoadTerminals.Infrastructure.Presentation;

namespace HVACLoadTerminals.App.Controls
{
    public partial class RoomPlanCard : UserControl
    {
        private RoomRow? _room;

        public RoomPlanCard()
        {
            InitializeComponent();
        }

        public void SetRoom(RoomRow? room)
        {
            _room = room;
            if (room == null)
            {
                TitleText.Text = "";
                SubtitleText.Text = "";
                SystemsText.Text = "";
                FlowText.Text = "";
                return;
            }
            TitleText.Text = $"{room.Number}. {room.Name}";
            SubtitleText.Text = $"Уровень: {room.LevelName} · S={room.Area:F1} м² · {(room.IsCorner ? "угл." : "")} · {room.Purpose}";
            SystemsText.Text = $"Системы: {room.SystemsSummary}";
            double flow = 0;
            foreach (var s in room.Systems)
                if (s.IsIncluded) flow += s.FlowM3h;
            FlowText.Text = flow > 0 ? $"Σ расход: {flow:F0} м³/ч · Q={room.HeatingW:F0} Вт" : $"Q={room.HeatingW:F0} Вт";
        }

        public event RoutedEventHandler? SystemsRequested;
        public event RoutedEventHandler? WizardRequested;
        public event RoutedEventHandler? CurvesRequested;

        private void Systems_Click(object sender, RoutedEventArgs e) => SystemsRequested?.Invoke(this, e);
        private void Wizard_Click(object sender, RoutedEventArgs e) => WizardRequested?.Invoke(this, e);
        private void Curves_Click(object sender, RoutedEventArgs e) => CurvesRequested?.Invoke(this, e);
    }
}
