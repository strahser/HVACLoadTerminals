using System;
using HVACLoadTerminals.ModelsStatic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace HVACLoadTerminals.NormativeHeatResistance;

[TestClass]
public class NormativeValueCalculatorTests
{
    [TestMethod]
    public void GetNormativeCalculator_Coefficients_Living_Wall()
    {
        // Arrange
        string category = BuildingCategory.Living.Value;
        string structure = EnclosureTypeOptions.Wall;
        double gsop = 5000;

        // Act
        Func<double, double> calculator = NormativeValueCalculator.GetNormativeCalculator(category, structure);
        double result = calculator(gsop);

        // Assert
        double expected = StaticCoefficientValues.Coefficients[(category, structure)].A * Math.Min(gsop, NormativeValueCalculator.GetMaxGSOP(structure)) + StaticCoefficientValues.Coefficients[(category, structure)].B;
        Assert.AreEqual(expected, result, 0.00001); // Добавлена дельта для сравнения double
    }

    [TestMethod]
    public void GetNormativeCalculator_TableValues_Living_Window()
    {
        // Arrange
        string category = BuildingCategory.Living.Value;
        string structure = EnclosureTypeOptions.Window;
        double gsop = 5000;

        // Act
        Func<double, double> calculator = NormativeValueCalculator.GetNormativeCalculator(category, structure);
        double result = calculator(gsop);

        // Assert
        // Линейная интерполяция между 4000 и 6000: (0.63 + 0.73) / 2 = 0.68
        double expected = 0.68;
        Assert.AreEqual(expected, result, 0.01); // Допуск 0.01 из-за интерполяции
    }

    [TestMethod]
    public void GetNormativeCalculator_DefaultValue_UnknownCategory()
    {
        // Arrange
        string category = "Unknown";
        string structure = EnclosureTypeOptions.Wall;
        double gsop = 1000;

        // Act
        Func<double, double> calculator = NormativeValueCalculator.GetNormativeCalculator(category, structure);
        double result = calculator(gsop);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetNormativeCalculator_Door_CalculatesCorrectly()
    {
        // Arrange
        string category = BuildingCategory.Living.Value;
        string structure = EnclosureTypeOptions.Door; // Door рассчитывается на основе Wall
        double gsop = 5000;

        // Act
        Func<double, double> calculator = NormativeValueCalculator.GetNormativeCalculator(category, structure);
        double result = calculator(gsop);

        // Assert
        double expectedWallValue = StaticCoefficientValues.Coefficients[(BuildingCategory.Living.Value, EnclosureTypeOptions.Wall)].A * Math.Min(gsop, NormativeValueCalculator.GetMaxGSOP(EnclosureTypeOptions.Wall)) + StaticCoefficientValues.Coefficients[(BuildingCategory.Living.Value, EnclosureTypeOptions.Wall)].B;
        double expected = expectedWallValue * 0.6;
        Assert.AreEqual(expected, result, 0.00001);
    }

    [TestMethod]
    public void GetNormativeCalculator_Window_Curtain_Same()
    {
        // Arrange
        string category = BuildingCategory.Living.Value;
        string structureWindow = EnclosureTypeOptions.Window;
        string structureCurtain = EnclosureTypeOptions.Curtain;
        double gsop = 5000;

        // Act
        Func<double, double> calculatorWindow = NormativeValueCalculator.GetNormativeCalculator(category, structureWindow);
        Func<double, double> calculatorCurtain = NormativeValueCalculator.GetNormativeCalculator(category, structureCurtain);
        double resultWindow = calculatorWindow(gsop);
        double resultCurtain = calculatorCurtain(gsop);


        // Assert
        Assert.AreEqual(resultWindow, resultCurtain, 0.00001);
    }

    [TestMethod]
    public void GetNormativeCalculator_TableValues_Industrial_Roof()
    {
        // Arrange
        string category = BuildingCategory.Industrial.Value;
        string structure = EnclosureTypeOptions.Roof;
        double gsop = 5000;

        // Act
        Func<double, double> calculator = NormativeValueCalculator.GetNormativeCalculator(category, structure);
        double result = calculator(gsop);

        // Assert
        // Линейная интерполяция между 4000 и 6000: (2.8 + 3.4) / 2 = 3.1
        double expected = 3.1;
        Assert.AreEqual(expected, result, 0.01); // Допуск 0.01 из-за интерполяции
    }


}