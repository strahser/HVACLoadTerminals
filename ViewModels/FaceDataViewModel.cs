using HVACLoadTerminals.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVACLoadTerminals.ViewModels
{
    class FaceDataViewModel : ReactiveObject
    {
        public List<FaceData> faceDataList { get; set; }
        public FaceDataViewModel(List<FaceData> _faceDataList){

            faceDataList = _faceDataList;
        }
    }
}
