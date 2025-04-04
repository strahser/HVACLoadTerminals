using System;
using System.Collections.Generic;
using HVACLoadTerminals.ModelsStatic;
using HVACLoadTerminals.ProjectSettings;

namespace HVACLoadTerminals.NormativeHeatResistance;

public static class StaticCoefficientValues
{
    public static readonly Dictionary<(string CategoryValue, string StructureType), (double A, double B)> Coefficients = new()
    {
        // Жилые здания (1.1)
        { (BuildingCategory.Living.Value, EnclosureTypeOptions.Wall), (0.00035, 1.4) },
        { (BuildingCategory.Living.Value, EnclosureTypeOptions.Roof), (0.0005, 2.2) },
        { (BuildingCategory.Living.Value, EnclosureTypeOptions.Floor), (0.00045, 1.9) },
        { (BuildingCategory.Living.Value, EnclosureTypeOptions.Skylight), (0.000025, 0.25) },

        // Образовательные (1.2)
        { (BuildingCategory.Schools.Value, EnclosureTypeOptions.Wall), (0.00035, 1.4) },
        { (BuildingCategory.Schools.Value, EnclosureTypeOptions.Roof), (0.0005, 2.2) },
        { (BuildingCategory.Schools.Value, EnclosureTypeOptions.Floor), (0.00045, 1.9) },
        { (BuildingCategory.Schools.Value, EnclosureTypeOptions.Skylight), (0.000025, 0.25) },

        // Общественные (2)
        { (BuildingCategory.Public.Value, EnclosureTypeOptions.Wall), (0.0003, 1.2) },
        { (BuildingCategory.Public.Value, EnclosureTypeOptions.Skylight), (0.000025, 0.25) },

        // Производственные (3)
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Wall), (0.0002, 1.0) },
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Roof), (0.0005, 2.2) }, 
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Floor), (0.00045, 1.9) }, 
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Window), (0.000025, 0.2) },
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Skylight), (0.000025, 0.15) },
    };

    public static readonly Dictionary<(string CategoryValue, string StructureType), 
                                        (double[] GSOP, double[] R0)> TableValues = new()
    {
        // Окна для всех категорий
        { (BuildingCategory.Living.Value, EnclosureTypeOptions.Window), 
            (new[] { 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
             new[] { 0.49, 0.63, 0.73, 0.75, 0.77, 0.8 }) },
        
        { (BuildingCategory.Schools.Value, EnclosureTypeOptions.Window), 
            (new[] { 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
             new[] { 0.3, 0.45, 0.6, 0.7, 0.75, 0.8 }) },
        
        { (BuildingCategory.Public.Value, EnclosureTypeOptions.Window), 
            (new[] { 1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
             new[] { 0.49, 0.49, 0.63, 0.73, 0.75, 0.77, 0.8 }) },
        
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Window), 
            (new[] { 1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
             new[] { 0.23, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5 }) },
        
        // Кровля и пол для общественных (из таблицы)
        { (BuildingCategory.Public.Value, EnclosureTypeOptions.Roof), 
            (new[] { 1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
                new[] { 1.5, 2.0, 2.8, 3.4, 3.9, 4.4, 4.8 }) },
        { (BuildingCategory.Public.Value, EnclosureTypeOptions.Floor), 
            ([1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0
                ], 
                new[] { 1.2, 1.6, 2.2, 2.7, 3.1, 3.5, 3.8 }) },
        
        // Кровля и пол для Производственных (из таблицы)
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Roof), 
            (new[] { 1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0 }, 
            new[] { 1.5, 2.0, 2.8, 3.4, 3.9, 4.4, 4.8 }) },
        { (BuildingCategory.Industrial.Value, EnclosureTypeOptions.Floor), 
            ([1000.0, 2000.0, 4000.0, 6000.0, 8000.0, 10000.0, 12000.0
            ], 
            new[] { 1.2, 1.6, 2.2, 2.7, 3.1, 3.5, 3.8 }) }


    };
}
    
