using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;



namespace HVACLoadTerminals.Models
{
public class SpaceModel
{
    Space SpaceData { get; set; }
    public string S_ID { get; set; }
    public string S_Number { get; set; }
    public string S_Name { get; set; }
    public double S_height { get; set; }
    public double S_area { get; set; }
    public double S_Volume { get; set; }
    public string S_level { get; set; }
    public SpaceBoundaryModel geometry_data { get; set; }


        // Конструктор для инициализации объекта из данных _Space
        public SpaceModel(Space space)
    {
        SpaceData = space;
        S_ID = space.Id.ToString();
        S_Number = space.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
        S_Name = space.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
        S_height = ParameterDisplayConvertor.Meters(space, BuiltInParameter.ROOM_HEIGHT);
        S_area = ParameterDisplayConvertor.SquareMeters(space, BuiltInParameter.ROOM_AREA);
        S_Volume = ParameterDisplayConvertor.CubicMeters(space, BuiltInParameter.ROOM_VOLUME);
        S_level = space.get_Parameter(BuiltInParameter.LEVEL_NAME).AsString();
        geometry_data = GetGeometryData();
}

        public SpaceBoundaryModel GetGeometryData() 
        {

                SpaceBoundary spaceBoundary = new SpaceBoundary(SpaceData);
                SpaceBoundaryModel spaceData = spaceBoundary.GetSpaceBoundaryModel(spaceBoundary.cleanCurves);
            return spaceData;

        }
    }
        
}
    

