using System.Windows;

namespace HVACLoadTerminals.App
{
    public partial class CalculationDetailWindow : Window
    {
        public CalculationDetailWindow(
            string systemName, string deviceInfo, string rule,
            int count, double flowPerDevice, double kef,
            string steps)
        {
            InitializeComponent();
            TitleText.Text = $"Детали расчёта — {systemName}";
            DeviceText.Text = deviceInfo;
            RuleText.Text = rule;
            CountText.Text = $"{count} шт";
            FlowPerDevText.Text = flowPerDevice > 0 ? $"{flowPerDevice:F0} м³/ч" : "—";
            KefText.Text = kef > 0 ? $"{kef:F2}" : "—";
            StepsText.Text = steps;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
