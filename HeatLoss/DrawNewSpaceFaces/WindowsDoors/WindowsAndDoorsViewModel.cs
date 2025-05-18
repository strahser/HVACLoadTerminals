using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.WindowsDoors
{
    public class WindowsAndDoorsViewModel : ViewModelBase
    {
        // поля
        private Document _selectedRoomDocument;
        private ObservableCollection<Document> _linkedDocuments = [];
        private readonly OpensHandler _opens; 
        
        //комманды
        public ICommand DrawWindowsCommand { get; }
        public ICommand DrawDoorsCommand { get; }

        public WindowsAndDoorsViewModel(Document hvacDocument)
        {
            LinkedDocuments = new ObservableCollection<Document>(GetLinkedDocuments(hvacDocument));
            
            //передаем первую ссылку по умолчанию
            if (LinkedDocuments.Any()) { SelectedRoomDocument = LinkedDocuments.First();}
            DrawWindowsCommand = new RelayCommand(DrawWindows);
            DrawDoorsCommand = new RelayCommand(DrawDoors);
            _opens = new OpensHandler(hvacDocument, SelectedRoomDocument);
        }
        
        
        public ObservableCollection<Document> LinkedDocuments 
        { get => _linkedDocuments; set=>SetField(ref _linkedDocuments, value);}

        public Document SelectedRoomDocument
        { get => _selectedRoomDocument; set=>SetField(ref _selectedRoomDocument, value);}
        
        private List<Document> GetLinkedDocuments(Document hvacDocument)
        {
            IList<RevitLinkInstance> linkedInstances = CollectorQuery.GetLinkedDocument(hvacDocument);
            return linkedInstances.Select(instance => instance.GetLinkDocument()).Where(doc => doc != null).ToList();
        }
        
        private void DrawWindows(object parameter) { _opens.DrawWindows();}

        private void DrawDoors(object parameter){ _opens.DrawDoors();}
    }
}