using System.ComponentModel;
using System.Windows;
using HVACLoadTerminals.App.ViewModels;

namespace HVACLoadTerminals.App
{
    /// <summary>Модальный CRUD-редактор офлайн-каталога приборов (карточка U2.2).</summary>
    public partial class CatalogEditorWindow : Window
    {
        public CatalogEditorWindow(CatalogEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is CatalogEditorViewModel vm && vm.IsDirty)
            {
                var result = MessageBox.Show(this,
                    "Изменения каталога не сохранены. Сохранить перед закрытием?",
                    "Каталог приборов", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        return;
                    case MessageBoxResult.Yes when vm.SaveCommand.CanExecute(null):
                        vm.SaveCommand.Execute(null);
                        break;
                }
            }
            base.OnClosing(e);
        }
    }
}
