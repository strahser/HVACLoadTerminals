using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;


namespace HVACLoadTerminals.DrawNewSpaceFaces.Walls
{
    public partial class WallOrientationWindow : Window
    {
        public string SelectedDirection { get; private set; } = "up"; // Значение по умолчанию
        public Document SelectedRoomDocument { get; private set; }

        private ObservableCollection<Document> LinkedDocuments { get; set; } = new ObservableCollection<Document>();


        public WallOrientationWindow(Document hvacDocument)
        {
            InitializeComponent();

            // Заполняем список связанных документов
            IList<RevitLinkInstance> linkedInstances = CollectorQuery.GetLinkedDocument(hvacDocument);
            foreach (var instance in linkedInstances)
            { Document linkedDoc = instance.GetLinkDocument();
                if (linkedDoc != null)
                {
                    LinkedDocuments.Add(linkedDoc);
                }
            }

            // Устанавливаем источник данных для ComboBox
            DocumentComboBox.ItemsSource = LinkedDocuments;

            // Если есть связанные документы, выбираем первый по умолчанию
            if (LinkedDocuments.Count > 0)
            {
                DocumentComboBox.SelectedIndex = 0;
                SelectedRoomDocument = LinkedDocuments[0]; // Устанавливаем значение по умолчанию
            }

            // Устанавливаем значение по умолчанию для кнопки "Up"
            UpButton.IsChecked = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (UpButton.IsChecked == true)
            {
                SelectedDirection = "up";
            }
            else if (DownButton.IsChecked == true)
            {
                SelectedDirection = "down";
            }
            else if (LeftButton.IsChecked == true)
            {
                SelectedDirection = "left";
            }
            else if (RightButton.IsChecked == true)
            {
                SelectedDirection = "right";
            }

            // Получаем выбранный документ из ComboBox
            SelectedRoomDocument = (Document)DocumentComboBox.SelectedItem;


            // Закрываем окно и возвращаем true, чтобы указать, что пользователь нажал "ОК"
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Закрываем окно и возвращаем false, чтобы указать, что пользователь нажал "Отмена"
            DialogResult = false;
            Close();
        }

        private void DocumentComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Обновляем SelectedRoomDocument при изменении выбора в ComboBox
             SelectedRoomDocument = (Document)DocumentComboBox.SelectedItem;
        }
    }

}
