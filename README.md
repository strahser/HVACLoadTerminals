# HVAC Load Terminals

Подбор и расстановка воздухораспределительных устройств (ВРУ) в помещениях по известной нагрузке (расход воздуха, приток, вытяжка, холодильная нагрузка).

## Архитектура

```
src/
├── Core/                  # Чистая доменная логика
├── Infrastructure/        # Реализации (SQLite, OxyPlot, JSON)
├── App/                   # Desktop WPF приложение (автономный просмотр)
└── Revit/                 # Revit add-in
```

### Core (HVACLoadTerminals.Core)

**Модели:**
- `Point2D`, `Polygon2D` — геометрические примитивы
- `RoomPolygon` — помещение с полигоном границ и системами
- `HVACSystem`, `HVACSystemType` — приточная/вытяжная/фанкойл система
- `TerminalDevice` — оборудование из каталога (расход, тип, семейство)
- `DevicePlacement` — размещённый прибор с координатами
- `PlacementResult` — результат расчёта расстановки

**Интерфейсы:**
- `IRoomGeometryProvider` — получение полигонов помещений из Revit
- `IRoomSystemProvider` — получение/назначение систем для помещения
- `ITerminalCatalogRepository` — каталог оборудования (SQLite)
- `ITerminalPlacementService` — сервис расчёта расстановки
- `IPolygonVisualizer` — визуализация (OxyPlot / Revit)
- `IDevicePlacer` — физическая установка приборов

**Сервисы:**
- `PolygonOffsetService` — смещение полигона внутрь
- `TerminalSelectionService` — подбор оптимального количества приборов
- `TerminalPlacementService` — расчёт расстановки вдоль смещённого контура

### Infrastructure (HVACLoadTerminals.Infrastructure)

- `SQLiteTerminalCatalogRepository` — чтение каталога из SQLite
- `JsonRoomDataStore` — сохранение/загрузка данных помещений в JSON
- `OxyPlotVisualizer` — визуализация полигонов через OxyPlot.Wpf
- `DemoRoomDataService` — демо-данные для тестирования

### App (HVACLoadTerminals.App)

Desktop WPF-приложение для тестирования алгоритмов без Revit. Использует OxyPlot для отображения:
- Полигонов помещений
- Смещённых контуров
- Размещённых приборов

**DI:** Microsoft.Extensions.DependencyInjection

### Revit (HVACLoadTerminals.Revit)

**Команды:**
- `PlaceTerminalsCommand` — открывает WPF-окно с OxyPlot для подбора
- `ReviewPlacementCommand` — рисует модельные линии на плане этажа (выберите Space)
- `ExportRoomDataCommand` — экспорт геометрии и систем Space в JSON

**Сервисы:**
- `RevitRoomGeometryProvider` — извлекает полигон нижней грани Space
- `RevitRoomSystemProvider` — читает параметры притока/вытяжки Space
- `RevitDevicePlacer` — размещает FamilyInstance в Revit

## Установка Revit add-in

1. Собрать `HVACLoadTerminals.Revit.dll`
2. Скопировать `src\Revit\HVACLoadTerminals.addin` в:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2024\
   ```
3. Отредактировать путь к сборке в `.addin` файле

## Desktop App (без Revit)

Запуск: `src\App\bin\Debug\net48\HVACLoadTerminals.App.exe`

## Принцип работы

1. Загрузка полигонов помещений (из Revit Space или демо-данных)
2. Для каждого помещения и системы подбирается оптимальное ВРУ:
   - Расчёт количества приборов: `ceil(ТребуемыйРасход / МаксРасходПрибора)`
   - Выбор прибора с минимальным количеством
3. Смещение полигона внутрь на заданное расстояние (500 мм)
4. Равномерное распределение точек установки вдоль смещённого контура
5. Визуализация результата
