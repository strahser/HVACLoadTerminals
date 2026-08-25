# HVAC Load Terminals

Универсальная расстановка приборов вентиляции и отопления — приточные/вытяжные
диффузоры и решётки, фанкойлы, отопительные приборы (радиаторы/конвекторы) —
по нагрузке (расход воздуха, мощность) с офлайн-стендом WPF, интерактивным
HTML-превью (WebView2) и записью в модель Revit 2024.

Данные помещений берутся из **снимка HeatLossRevit2** (`snapshots_raw\*.json`) —
работа без открытой модели Revit; нагрузки автогенерируются (100 Вт/м²,
кратности по назначению помещения).

## Архитектура

Чистая архитектура: ядро не зависит от Revit/WPF; App и Revit — два хоста
над одним ядром и одним presenter'ом.

```
Core  ←  Infrastructure  ←  App (WPF) / Revit 2024 add-in
                ↑
          Core.Tests (xUnit)
```

| Слой | Проект | Назначение |
|------|--------|------------|
| **Core** | `src\Core` | Доменная логика (.NET Framework 4.8, без Revit/WPF): геометрия Clipper2, расчёт количества, подбор типоразмера, размещение трёх классов приборов, автогенерация нагрузок |
| **Infrastructure** | `src\Infrastructure` | Загрузчик снимка, офлайн JSON-каталог приборов (CRUD), presenter рабочего места (`SnapshotWorkspacePresenter`), HTML-экспортёр + WebView2-хост, OxyPlot |
| **App** | `src\App` | Автономный WPF-стенд «Снимок помещений» (без Revit) |
| **Revit** | `src\Revit` | Add-in Revit 2024: команды ленты, стенд расстановки, запись FamilyInstance |
| **Tests** | `src\Core.Tests` | xUnit-тесты ядра и presenter'а |

## Рабочий стенд «Снимок помещений» (App)

1. **① Открыть снимок…** — JSON из `%AppData%\HeatLossRevit2\data\snapshots_raw\`;
   нагрузки подставляются автоматически.
2. Таблица помещений: фильтр по уровню, чекбокс «Включено» (групповые операции
   «Включить уровень» / «Только видимые»), inline-правка Q и расходов,
   «Живой пересчёт» с debounce ~300 мс.
3. Правила расстановки:
   - режимы количества: `Auto` / `ByArea` / `ByFlow` / `Fixed`;
   - паттерны массовой расстановки (`WallPattern`): `CeilingGrid` (сетка по потолку),
     `LongSide`, `ShortSide`, `Explicit`; дефолт владельца — приток вдоль длинной
     стороны, вытяжка вдоль короткой;
   - правило одиночного прибора (`SingleRule`): `Center` / `Corner`;
   - доля длины приборов от ширины окна (по умолчанию 0.6), скорость решётки v;
4. **▶ РАССЧИТАТЬ** — отопление под каждым окном, приток/вытяжка по паттернам;
   план OxyPlot с подсветкой выбранной стороны и k_ef цветом; координаты в мм.
5. **🌐 HTML** — интерактивное превью в общем WebView2-хосте (офлайн `file://`,
   мост postMessage, Recompute); фолбэк — локальный HTTP-сервер + браузер.
6. **💾/📂 Проект** (`*.hvacproj.json`) — round-trip состояния вместе с флагами комнат.
7. **🗂 Каталог приборов…** — офлайн CRUD-редактор типоразмеров
   (`%AppData%\HVACLoadTerminals\catalog.json`; seed при первом запуске —
   `CatalogFactory`, 14 типоразмеров).

## CRM-каркас (оба хоста: App и стенд Revit)

Единое ядро `CrmViewModel` (Infrastructure): слева дерево
**«Системы → Уровни → Помещения»**, справа панель свойств, снизу таблицы;
выбор узла фильтрует таблицу приборов и план.

Панель свойств **системы**:
- закрепление типоразмера из каталога (или автоподбор), паспорт прибора;
- правило количества `Auto / ByArea / ByFlow / Fixed` + N — пер-системно,
  без оверрайда работают значения тулбара;
- **N-калькулятор**: «N = ⌈Q 1200 / 500 м³/ч⌉ = 3» — почему такое количество;
- паттерны расстановки и правило одиночного прибора;
- отступы: от стен (buffer(-x) зоны) и заглубление от потолка + мини-схема
  офсет-полигона;
- переименование системы во всех комнатах с валидацией;
- сводка: комнат, приборов, Σрасход, средний k_ef.

Панель свойств **помещения**: Q/приток/вытяжка/назначение (живой пересчёт),
температура, проёмы из снимка, прогноз длины отопительных приборов под окнами.

Массовые операции (Detail-режим прототипа): мультиселект помещений →
**«⚙ К выбранным…»** — применить типоразмер/правило/паттерн/отступы к выбранным
комнатам по чекбоксам полей.

Визуализация и выгрузки:
- вкладка **3D** (three.js в WebView2): плиты этажей, приборы на высоте установки,
  селектор уровня, изоляция систем чекбоксами;
- план OxyPlot: цвета «По k_ef / По системам», подписи комнат;
- **📄 Отчёт** — HTML уровня (схема + сводка систем + таблица приборов);
- **⬇ Excel** (level_values + Приборы), **⬇ Задание JSON**, **🌐 HTML**.

## Revit 2024 add-in

Вкладка **HVAC Terminals**, панель **Placement**:

| Кнопка | Команда | Назначение |
|--------|---------|------------|
| Place\nTerminals | `PlaceTerminalsCommand` | WPF-окно подбора по MEP Spaces (автономный режим) |
| Review\nPlacement | `ReviewPlacementCommand` | Модельные линии контуров Space на плане |
| Export\nRooms | `ExportRoomDataCommand` | Экспорт геометрии/систем Space в JSON |
| Mass\nPlacement | `RevitHtmlPlacementCommand` | Массовая расстановка всех Spaces: HTML+Revit превью, Place/Cancel (rollback) |
| Individual\nPlacement | `RevitIndividualPlacementCommand` | Только выделенные Spaces |
| По снимку\nпомещений | `ImportSnapshotPlacementCommand` | Снимок → расчёт ядром → запись трёх классов приборов; идемпотентность маркером `HLT\|<DocumentTitle>\|<roomId>\|<systemName>` в «Комментариях» (Пропустить/Заменить/Всё) |
| Стенд\nрасстановки | `SnapshotStandCommand` | Modeless-окно стенда (паритет с App); запись через ExternalEvent без блокировки Revit |
| Run\nTests | `RevitTestRunnerCommand` | In-process тесты Revit API, JSON-отчёт в `%LocalAppData%\HVACLoadTerminals\TestResults\` |

## Ключевые сервисы ядра

| Сервис | Назначение |
|--------|------------|
| `SnapshotPlacementEngine` | Конвейер «снимок → нагрузки → размещение» для App и Revit |
| `LoadsEstimatorService` | Автогенерация нагрузок: Q = S×100 Вт/м² (угловые ×1.1), вентиляция по назначению/кратностям |
| `HeatingPlacementService` | Отопительный прибор под каждым окном; суммарная длина ≥60 % ширины окна; fallback — длиннейшая наружная стена |
| `CeilingPlacementService` | Потолочная сетка по площади обслуживания (Clipper2 offset), паттерны LongSide/ShortSide, min distance, SingleRule |
| `TerminalPlacementService` | Настенное размещение вдоль рёбер (SidePreference, CoordinateSystem, SpacingMm) |
| `QuantityCalculator` | Режимы ByCalculation/ByCount/ByStep/ByArea/ByLength/Auto |
| `TerminalSelectionService` | Подбор типоразмера: мин. количество → мин. запас; коэффициент загрузки k_ef (<0.6 недогруз … >0.9 перегруз) |
| `GrilleSizingService` | Геометрия решёток из эквивалентного диаметра: H = max(D−200; √(A/3); 100 мм), аспект ≤3, разбивка на N штук |
| `RoomGeometryAnalyzer` / `PolygonOffsetService` | Классификация рёбер, внутренний офсет полигона (Clipper2) |

## Сборка

Требования: .NET Framework 4.8, MSBuild (VS 2022), для Revit-проекта —
Revit 2024 (`RevitAPI.dll` из `C:\Program Files\Autodesk\Revit 2024`).
NuGet: Clipper2, xUnit, Newtonsoft.Json, OxyPlot.Wpf, Microsoft.Web.WebView2.

```bat
:: Полное решение (4 проекта, включая Revit)
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ^
  HVACLoadTerminals.sln /t:Build /p:Configuration=Debug /v:m /nologo

:: Быстрая проверка без Revit SDK: ядро + тесты
dotnet build src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v q
dotnet test  src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v q
```

## Установка в Revit

1. Собрать `HVACLoadTerminals.Revit.dll`.
2. Скопировать `src\Revit\HVACLoadTerminals.addin` в `%APPDATA%\Autodesk\Revit\Addins\2024\`
   (при необходимости поправить путь к DLL внутри).
3. Запустить Revit 2024 — вкладка «HVAC Terminals».

## Лицензии

Код проекта — см. [LICENSE.txt](LICENSE.txt). Внешние библиотеки: Clipper2 (Apache-2.0),
Newtonsoft.Json (MIT), OxyPlot (MIT), xUnit (Apache-2.0), Microsoft.Web.WebView2 (MIT),
Revit API (Autodesk).
