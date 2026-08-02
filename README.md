# HVAC Load Terminals

Подбор и расстановка воздухораспределительных устройств (ВРУ) в помещениях по известной нагрузке (расход воздуха, приток, вытяжка, холодильная нагрузка) с превью в HTML и в Revit с отменой транзакции.

## Архитектура

Проект построен по принципу чистой архитектуры: ядро (Core) не зависит от Revit/WPF, инфраструктура реализует интерфейсы, Revit и App — адаптеры.

```
Core ← Infrastructure ← App / Revit
```

### Слои

| Слой | Проект | Назначение |
|------|--------|------------|
| **Core** | HVACLoadTerminals.Core | Чистая доменная логика (C#, .NET 4.8). Модели, сервисы, интерфейсы. Нет зависимостей от Revit/WPF |
| **Infrastructure** | HVACLoadTerminals.Infrastructure | Реализации: сериализация сцены, HTML-экспортёр, локальный HTTP-сервер, визуализация OxyPlot, хранилище JSON/SQLite |
| **App** | HVACLoadTerminals.App | Desktop WPF-приложение для автономного тестирования без Revit |
| **Revit** | HVACLoadTerminals.Revit | Revit 2024 add-in: команды, сервисы Revit API, тест-раннер |

### Зависимости

```
Core (чистая C#)
  ↑
Infrastructure (реализации)
  ↑        ↑
App (WPF)  Revit (API)
```

## Возможности

### Режимы количества

| Режим | Описание | Параметры |
|-------|----------|-----------|
| `ByCalculation` | Автоматический расчёт: `ceil(нагрузка / мощность прибора)` | — |
| `ByCount` | Заданное точное количество | `FixedCount` |
| `ByStep` | Начиная от минимума с шагом | `StepCount`, `MaxCount` |

### Параметры размещения

| Параметр | Тип | Описание |
|----------|-----|----------|
| `WallOffsetMm` | double | Расстояние от стены до центра прибора (мм, по умолчанию 500) |
| `SidePreference` | enum | Сторона размещения: `Any`, `LongSide`, `ShortSide` |
| `CoordinateSystem` | enum | Условная система координат: `Auto`, `Bottom`, `Right`, `Top`, `Left` |
| `SpacingMm` | double | Расстояние между приборами (0 = авто, равномерно) |
| `StartOffsetMm` | double | Отступ от краёв стены (мм) |

### Геометрические операции (Clipper2)

- Смещение полигона внутрь (OffsetInward) с точностью Clipper2
- Объединение, разность, очистка полигонов
- Классификация рёбер: длина, ориентация, внутренние нормали
- Выбор.primary стены по предпочтению (длинная/короткая)

### Визуализация

- **HTML-превью**: интерактивный 2D Canvas + опциональный Three.js 3D (CDN)
- **WPF OxyPlot**: полигоны, смещённые контуры, точки размещения
- **Revit-превью**: маркеры + модальный диалог Place/Cancel (rollback транзакции)

### Автосбор семейств из Revit

- Категории: `OST_DuctTerminal` (воздухораспределители), `OST_MechanicalEquipment` (фанкойлы)
- Параметры: Air Flow / Cooling Capacity / Width / Height (RU + EN имена)
- Классификация по типу системы (Supply/Exhaust/FanCoil/Cooling)

### Массовая и индивидуальная расстановка

- **Mass Placement**: все MEP Spaces в модели, автоматически
- **Individual Placement**: только выделенные помещения

## Возможности (детально)

### Выбор стороны (SidePreference)

| Значение | Поведение |
|----------|-----------|
| `Any` | Выбирается первое доступное ребро |
| `LongSide` | Приоритет длинным рёбрам (top/bottom) |
| `ShortSide` | Приоритет коротким рёбрам (left/right) |

### Условная система координат (CoordinateSystem)

| Значение | Поведение |
|----------|-----------|
| `Auto` | Автоматический выбор по нормали к primary ребру |
| `Bottom` | Приборы вдоль нижней стены |
| `Right` | Приборы вдоль правой стены |
| `Top` | Приборы вдоль верхней стены |
| `Left` | Приборы вдоль левой стены |

### Вращение приборов

Прибор поворачивается так, чтобы его передняя часть (front face) была направлена внутрь помещения. Угол вычисляется через `Math.Atan2(normal.Y, normal.X)`.

### Цвета систем

| Система | Цвет |
|---------|------|
| Supply (приток) | Красный |
| Exhaust (вытяжка) | Зелёный |
| FanCoil (фанкойл) | Оранжевый |
| Cooling (охлаждение) | Синий |

## Сборка

### Требования

- Visual Studio 2022 (Community/Professional) с MSBuild 17
- .NET Framework 4.8 SDK (SDK-style проекты)
- NuGet-кэш (офлайн): Clipper2 2.0.0, xUnit 2.5.3, Newtonsoft.Json 13.0.3, OxyPlot.Wpf 2.1.2

### Команды сборки

```bash
# Сборка всего решения (Debug)
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  HVACLoadTerminals.sln /t:Build /p:Configuration=Debug /v:m /nologo

# Сборка только Core
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  src\Core\HVACLoadTerminals.Core.csproj /t:Build /p:Configuration=Debug /v:m /nologo

# Сборка только Revit
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  src\Revit\HVACLoadTerminals.Revit.csproj /t:Build /p:Configuration=Debug /v:m /nologo

# Сборка только App
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  src\App\HVACLoadTerminals.App.csproj /t:Build /p:Configuration=Debug /v:m /nologo

# Запуск тестов Core (xUnit)
"C:\Program Files\dotnet\dotnet.exe" test src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v q
```

### Структура вывода

```
src/Core/bin/Debug/net48/HVACLoadTerminals.Core.dll
src/Infrastructure/bin/Debug/net48/HVACLoadTerminals.Infrastructure.dll
src/App/bin/Debug/net48/HVACLoadTerminals.App.exe
src/Revit/bin/Debug/net48/HVACLoadTerminals.Revit.dll
```

## Установка в Revit 2024

### Содержимое addin-файла

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>HVAC Load Terminals</Name>
    <Assembly>HVACLoadTerminals.Revit.dll</Assembly>
    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>
    <FullClassName>HVACLoadTerminals.Revit.Application</FullClassName>
    <VendorId>HVACTerminals</VendorId>
    <VendorDescription>HVAC Terminals Project</VendorDescription>
  </AddIn>
</RevitAddIns>
```

### Установка

1. Собрать `HVACLoadTerminals.Revit.dll` (см. раздел "Сборка")
2. Скопировать `src\Revit\HVACLoadTerminals.addin` в:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2024\
   ```
3. При необходимости отредактировать путь к DLL в `.addin` файле
4. Запустить Revit 2024 — вкладка "HVAC Terminals" появится на ленте

### Вкладка "HVAC Terminals"

| Кнопка | Команда | Описание |
|--------|---------|----------|
| **Place Terminals** | `PlaceTerminalsCommand` | Открывает WPF-окно с OxyPlot для подбора (автономный режим) |
| **Review Placement** | `ReviewPlacementCommand` | Рисует модельные линии на плане этажа (выберите Space) |
| **Export Rooms** | `ExportRoomDataCommand` | Экспорт геометрии и систем Space в JSON |
| **Mass Placement** | `RevitHtmlPlacementCommand` | Массовая расстановка всех Spaces: HTML-превью + Revit-превью + Place/Cancel |
| **Individual Placement** | `RevitIndividualPlacementCommand` | Расстановка только выделенных Spaces |
| **Run Tests** | `RevitTestRunnerCommand` | Запуск автотестов в Revit, JSON-отчёт |

## Тесты

### Core.Tests (xUnit, 33 теста)

| Файл | Тесты | Описание |
|------|-------|----------|
| `GeometryTests.cs` | Геометрические операции | Clipper2: offset, union, difference, clean polygon |
| `QuantityCalculatorTests.cs` | Расчёт количества | ByCalculation, ByCount, ByStep, edge cases |
| `RoomGeometryAnalyzerTests.cs` | Анализ рёбер | Классификация, выбор primary ребра, координатная система |
| `PlacementServiceTests.cs` | Интеграционные тесты | Расстановка: количество, позиции, смещение, вращение, фильтрация |

```bash
# Запуск всех тестов
"C:\Program Files\dotnet\dotnet.exe" test src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v q

# Ожидаемый результат: 33 passed, 0 failed
```

### Revit-тесты (in-process, через RunTests)

| Фикстура | Тесты | Описание |
|-----------|-------|----------|
| `SpaceExtractionFixture` | Rooms_AreExtracted, Polygon_IsValid | Извлечение комнат из модели |
| `FamilyCatalogFixture` | Families_AreCollected, FlowParam_Mapped, SystemType_Classified | Автосбор каталога семейств |
| `PlacementFixture` | Quantity_ByCalculation, Positions_InsidePolygon, Offset_500mm, Rotation_MatchesNormal | Расстановка (чистый C# + Revit) |
| `PreviewRollbackFixture` | Preview_RequiresStartedTransaction, Preview_NullUIDoc_Throws | Превью и откат транзакции |

Запуск: кнопка **Run Tests** на вкладке "HVAC Terminals".
Отчёт: `%LocalAppData%\HVACLoadTerminals\TestResults\revit-tests-<timestamp>.json`

## Использование

### Типовой сценарий (Revit)

1. Открыть модель Revit 2024 с MEP Spaces
2. (Опционально) **Run Tests** — проверить работоспособность
3. **Mass Placement** — расстановка по всем Spaces:
   - Автосбор семейств из модели
   - Расчёт количества и позиций
   - HTML-превью в браузере (Canvas2D + Three.js)
   - Revit-превью с маркерами
   - **Place** =.commit, **Cancel** =rollback (ничего не остаётся)
4. Или **Individual Placement** — только для выделенных Spaces

### Типовой сценарий (Desktop App)

1. Запустить `src\App\bin\Debug\net48\HVACLoadTerminals.App.exe`
2. Загружаются демо-данные (комнаты + каталог приборов)
3. Выбрать комнату в списке
4. **Calculate Placement** — расчёт + визуализация на OxyPlot
5. **Show All Rooms** — показать все комнаты
6. **Export/Import to JSON** — сохранение/загрузка данных

### Структура каталога

```
HVACLoadTerminals/
├── src/
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── Point2D.cs                    # Геометрическая точка
│   │   │   ├── Polygon2D.cs                  # Полигон (с ContainsPoint, GetMinDistanceToEdge)
│   │   │   ├── RoomPolygon.cs                # Помещение с полигоном и системами
│   │   │   ├── HVACSystem.cs                 # Система (тип, расход, нагрузка)
│   │   │   ├── HVACSystemType.cs             # Enum: Supply/Exhaust/FanCoil/Cooling
│   │   │   ├── TerminalDevice.cs              # Прибор из каталога
│   │   │   ├── DevicePlacement.cs            # Размещённый прибор (координаты, вращение)
│   │   │   ├── PlacementResult.cs            # Результат расчёта
│   │   │   ├── PlacementOptions.cs           # Параметры размещения
│   │   │   ├── PlacementMode.cs              # Enum: ByCalculation/ByCount/ByStep
│   │   │   ├── PlacementSide.cs              # Enum: Any/LongSide/ShortSide
│   │   │   ├── CoordinateSystem.cs           # Enum: Auto/Bottom/Right/Top/Left
│   │   │   ├── RoomPlacementRequest.cs       # Запрос на расстановку
│   │   │   └── RoomPlacementConfig.cs        # Конфигурация для помещения
│   │   ├── Services/
│   │   │   ├── ClipperGeometryService.cs     # Обёртка Clipper2 (offset, union, difference)
│   │   │   ├── PolygonOffsetService.cs       # Смещение полигона внутрь (Clipper2)
│   │   │   ├── RoomGeometryAnalyzer.cs       # Классификация рёбер, выбор primary
│   │   │   ├── QuantityCalculator.cs         # Расчёт количества (3 режима)
│   │   │   ├── TerminalSelectionService.cs   # Подбор оптимального прибора
│   │   │   ├── TerminalPlacementService.cs   # Оркестрация расстановки
│   │   │   └── LengthUnitConverter.cs        # Конвертация мм ↔ единицы Revit
│   │   └── Interfaces/
│   │       ├── IRoomGeometryProvider.cs      # Получение полигонов помещений
│   │       ├── ITerminalCatalogRepository.cs # Каталог приборов
│   │       ├── ITerminalPlacementService.cs  # Сервис расстановки
│   │       ├── IPolygonVisualizer.cs         # Визуализация
│   │       └── IDevicePlacer.cs              # Физическая установка
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── JsonRoomDataStore.cs          # Хранилище JSON
│   │   │   └── SQLiteTerminalCatalogRepository.cs  # Каталог SQLite
│   │   ├── Services/
│   │   │   └── DemoRoomDataService.cs        # Демо-данные
│   │   └── Visualization/
│   │       ├── PlacementSceneSerializer.cs   # Сериализация сцены в JSON
│   │       ├── HtmlSceneExporter.cs          # Экспорт в HTML5 (Canvas2D + Three.js)
│   │       ├── HtmlPreviewServer.cs          # Локальный HTTP-сервер (preview bridge)
│   │       ├── IHtmlPreviewHost.cs           # Интерфейс хоста превью
│   │       └── OxyPlotVisualizer.cs          # Визуализация OxyPlot.Wpf
│   ├── App/
│   │   ├── MainWindow.xaml                   # WPF окно (OxyPlot + панель)
│   │   ├── MainWindow.xaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs              # ViewModel (комнаты, расстановка)
│   │   │   └── RelayCommand.cs               # Команда WPF
│   │   ├── Views/
│   │   │   ├── HtmlPreviewWindow.xaml         # WPF окно HTML-превью
│   │   │   └── HtmlPreviewWindow.xaml.cs
│   │   └── Commands/
│   │       └── OpenHtmlPreviewCommand.cs      # Команда открытия HTML-превью
│   └── Revit/
│       ├── Application.cs                    # IExternalApplication (лента, кнопки)
│       ├── HVACLoadTerminals.addin            # Revit addin-манифест
│       ├── Commands/
│       │   ├── PlaceTerminalsCommand.cs       # WPF-окно (автономный)
│       │   ├── ReviewPlacementCommand.cs      # Модельные линии
│       │   ├── ExportRoomDataCommand.cs       # Экспорт JSON
│       │   ├── RevitHtmlPlacementCommand.cs   # Mass Placement (HTML + Revit)
│       │   ├── RevitIndividualPlacementCommand.cs  # Individual Placement
│       │   └── RevitTestRunnerCommand.cs      # Запуск автотестов
│       ├── Services/
│       │   ├── RevitRoomGeometryProvider.cs   # Извлечение геометрии Space
│       │   ├── RevitFamilyCatalogProvider.cs  # Автосбор семейств
│       │   ├── RevitDevicePlacer.cs           # Физическая установка FamilyInstance
│       │   └── RevitPlacementPreviewService.cs # Превью + Place/Cancel
│       └── Testing/
│           ├── RevitTestAttribute.cs          # Атрибут [RevitTest]
│           ├── RevitTestFixtureAttribute.cs   # Атрибут [RevitTestFixture]
│           ├── RevitTestRunner.cs             # Discovery + execution + JSON report
│           ├── Assert.cs                      # Минимальные assertion-хелперы
│           ├── TestAssertFailedException.cs   # Исключение при ошибке assert
│           ├── TestDocumentContext.cs          # Static holder для Document
│           ├── RunnerSmokeFixture.cs          # Smoke-тест (2 метода)
│           └── RevitIntegrationFixtures.cs    # 4 фикстуры, 13 тестов
└── HVACLoadTerminals.sln                      # Решение (4 проекта)
```

## Технические детали

### Формат JSON-сцены

```json
{
  "Title": "Terminal Placement",
  "Rooms": [
    {
      "Id": "12345",
      "Name": "Room 101",
      "Boundary": [[0,0],[12,0],[12,-8],[0,-8]],
      "Systems": [
        { "Name": "Supply", "Type": "Supply", "FlowRate": 1200 }
      ],
      "Placements": [
        {
          "DeviceId": "D1",
          "FamilyName": "Diffuser",
          "Position": [2.5, -4.0],
          "Rotation": 1.57,
          "SystemName": "Supply",
          "EdgeIndex": 0,
          "Side": "Bottom"
        }
      ]
    }
  ]
}
```

### Единицы измерения

| Параметр | Единицы в Revit API | Единицы в HTML/JSON |
|----------|---------------------|---------------------|
| Координаты | Футы (internal) | Футы (feet) |
| Расход воздуха | куб.м/час (converted) | куб.м/час |
| Холодильная нагрузка | Ватты (converted) | Ватты |
| Смещение от стены | мм (параметр) → футы (расчёт) | мм (параметр) |
| Размеры приборов | мм (converted) | мм |

### Цвета систем (HTML-превью)

| Тип системы | HEX цвет | Описание |
|-------------|----------|----------|
| Supply | `#E74C3C` | Красный (приток) |
| Exhaust | `#2ECC71` | Зелёный (вытяжка) |
| FanCoil | `#F39C12` | Оранжевый (фанкойл) |
| Cooling | `#3498DB` | Синий (охлаждение) |

## Лицензии и зависимости

### Внешние библиотеки

| Пакет | Версия | Назначение |
|-------|--------|------------|
| Clipper2 | 2.0.0 | Геометрические операции (offset, union, difference) |
| xUnit | 2.5.3 | Unit-тестирование (Core.Tests) |
| Newtonsoft.Json | 13.0.3 | Сериализация JSON |
| OxyPlot.Wpf | 2.1.2 | Визуализация в WPF |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | DI в WPF-приложении |
| RevitAPI | 2024 | Revit API (только для Revit-проекта) |

### Revit API

Проект использует Revit 2024 API (RevitAPI.dll, RevitAPIUI.dll). Доступ к API осуществляется через:
- `Autodesk.Revit.DB` — геометрия, элементы, транзакции
- `Autodesk.Revit.UI` — интерфейс, команды, диалоги
- `Autodesk.Revit.DB.Mechanical` — MEP Spaces
