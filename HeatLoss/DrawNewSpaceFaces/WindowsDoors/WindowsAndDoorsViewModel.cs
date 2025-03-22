using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HVACLoadTerminals.Utils;
using HVACLoadTerminals.Utils.HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
{
    public class WindowsAndDoorsViewModel : ViewModelBase
    {
        private readonly Document _hvacDocument;
        
        private Document _selectedRoomDocument;
        
        private ObservableCollection<Document> _linkedDocuments = new ObservableCollection<Document>();

        public WindowsAndDoorsViewModel(Document hvacDocument)
        {
            _hvacDocument = hvacDocument;
            LinkedDocuments = new ObservableCollection<Document>(GetLinkedDocuments(hvacDocument));
            if (LinkedDocuments.Any())
            {
                SelectedRoomDocument = LinkedDocuments.First();
            }

            DrawWindowsCommand = new RelayCommand(DrawWindows);
            DrawDoorsCommand = new RelayCommand(DrawDoors);
        }
        public ICommand DrawWindowsCommand { get; }
        
        public ICommand DrawDoorsCommand { get; }

        public ObservableCollection<Document> LinkedDocuments
        {
            get { return _linkedDocuments; }
            set
            {
                _linkedDocuments = value;
                OnPropertyChanged(nameof(LinkedDocuments));
            }
        }

        public Document SelectedRoomDocument
        {
            get { return _selectedRoomDocument; }
            set
            {
                _selectedRoomDocument = value;
                OnPropertyChanged(nameof(SelectedRoomDocument));
            }
        }

        private List<Document> GetLinkedDocuments(Document hvacDocument)
        {
            IList<RevitLinkInstance> linkedInstances = CollectorQuery.GetLinkedDocument(hvacDocument);
            return linkedInstances.Select(instance => instance.GetLinkDocument()).Where(doc => doc != null).ToList();
        }

        private void DrawWindows(object parameter)
        {
            DrawElements(true, false);
        }

        private void DrawDoors(object parameter)
        {
            DrawElements(false, true);
        }

        private void DrawElements(bool drawWindows, bool drawDoors)
        {
            if (SelectedRoomDocument == null)
            {
                TaskDialog.Show("Ошибка", "Пожалуйста, выберите связанный документ.");
                return;
            }

            var walls = CollectorQuery.GetAllWalls(_hvacDocument);
            var opens = new OpensHandler(_hvacDocument, SelectedRoomDocument);

            if (drawWindows)
            {
                opens.DrawWindows(walls);
            }

            if (drawDoors)
            {
                opens.DrawDoors(walls);
            }
        }
    }
}