# ОТЧЁТ: U2.2 — Офлайн-каталог приборов: файл + CRUD-редактор

- **Дата**: 2026-08-23 (попытка 2 план-раннера; финальная верификация критериев приёмки)
- **Статус**: выполнено (код закрыт коммитом `761bbaa`, вердикт контролёра PASS `dd5a4f2`)
- **Карточка**: `U2.2` (план `2026-08-23_ui-usability-critique-fixes.md`)

## Что было не так

`SnapshotWorkspacePresenter.Calculate()` захардкоживал `CatalogFactory.CreateDemo()`
на месте вызова — правка характеристик приборов (расход, мощность, S обслуживания,
габариты, типоразмеры) была невозможна ни офлайн, ни без пересборки. Требование
владельца «менять характеристики приборов офлайн» нарушалось.

Прошлая попытка прервалась сбоем раннера; артефакт кода был уже закоммичен
(`761bbaa`), вердикт — PASS (`dd5a4f2`). Настоящая сессия — независимая
верификация критериев приёмки и отчёт по шаблону.

## Решения (grill)

- **U2.2/Q1 (план, «где жить каталогу»)**: глобальный каталог
  `%AppData%\HVACLoadTerminals\catalog.json` + env `HVACLOAD_CATALOG` +
  `JsonCatalogRepository.DefaultPathOverride` (опция «рядом с проектом»).
  Копирование в `.hvacproj` НЕ делается — проект хранит путь/версию
  (`ProjectDto.CatalogPath/CatalogVersion`). Вопрос остался за владельцем;
  текущее поведение — глобальный каталог.
- ASSUMPTION: правка каталога не требует подтверждения владельца при сохранении —
  валидация + `IsDirty`-диалог перед закрытием достаточны.
- ASSUMPTION: битый JSON не блокирует расчёт — warning в ErrorSink + переход на
  встроенный каталог; рабочий файл не теряется.

## Что сделано (пути файлов)

### Хранилище — `src/Infrastructure/Data/JsonCatalogRepository.cs` (новое)

`JsonCatalogRepository : ITerminalCatalogRepository`:

- `ResolveDefaultPath()` — `%AppData%\HVACLoadTerminals\catalog.json`, env
  `HVACLOAD_CATALOG` или `DefaultPathOverride`;
- `EnsureSeeded()` — первый запуск: демо-каталог `CatalogFactory.CreateDemo()`
  (14 типоразмеров) как seed; существующий файл НЕ перезаписывается;
- `LoadDocument()/GetAllDevices()/GetDevicesBySystemType()/GetDeviceById()` — чтение
  (`StringEnumConverter` — файл правится руками без пересборки);
- `SaveAll()` — валидация (`Validate`: Id заполнен/уникален, семейство/типоразмер,
  расход ≥ 0 и > 0 у воздушных систем, мощности/площадь/габариты ≥ 0) + атомарное
  сохранение через tmp + `File.Replace` — сбой не теряет рабочий файл;
- `LoadDocument()` на битом JSON — `InvalidDataException` с именем файла
  («Файл каталога приборов повреждён: …»), файл не трогается.

### Расчёт — `src/Infrastructure/Presentation/SnapshotWorkspacePresenter.cs`

- `CatalogRepository` (свойство, инжектируется извне) + `UseJsonCatalog(path)`;
  захардкоженный `CreateDemo()` из `Calculate()` убран;
- `ResolveCatalog()` — внешний каталог приоритетен; сбой чтения → `FallbackCatalog()`
  = встроенный демо-каталог + сообщение в ErrorSink;
- проект round-trip: `SaveProject/LoadProject` хранят путь и версию каталога
  (`ProjectDto.CatalogPath/CatalogVersion`), при загрузке каталог подхватывается.

### UI-редактор (App)

- `src/App/CatalogEditorWindow.xaml(.cs)` + `src/App/ViewModels/CatalogEditorViewModel.cs` —
  модальный CRUD: DataGrid по полям типоразмера, фильтр «Класс»
  (Все/Приток/Вытяжка/Отопление/Фанкойлы/Охлаждение), Добавить/Удалить/Демо/
  Сохранить/Закрыть, валидация с перечнем ошибок и запретом сохранения,
  статус «Каталог валиден: N типоразмеров · версия V»;
- `src/App/MainWindow.xaml` + `MainViewModel.cs` — кнопка тулбара «🗂 Каталог приборов…»
  (`EditCatalogCommand`), после сохранения — пересчёт на новых характеристиках.

### Тесты — `src/Core.Tests/OfflineCatalogTests.cs` (8 новых)

`RoundTrip_Save_Load_Preserves_All_Fields`, `EnsureSeeded_Writes_Demo_Catalog_And_Does_Not_Overwrite_Existing`,
`Load_Broken_Json_Throws_Clear_Error_And_File_Stays_Intact`, `Save_Rejects_Invalid_Devices_And_Preserves_Previous_File`,
`Save_Rejects_Empty_Catalog`, `Calculate_Uses_External_Catalog_From_Repository`,
`Calculate_Falls_Back_To_Demo_Catalog_When_File_Broken`, `Project_RoundTrip_Preserves_Catalog_Path_And_Version`.

## Доказательства (критерии приёмки — все выполнены)

Верификация выполнена в чистом git-worktree на коммите `761bbaa` (состояние кода
именно карточки U2.2, без посторонних правок U3.1):

| # | Критерий | Результат |
|---|----------|-----------|
| 1 | round-trip тест каталога (save/load ==) | ✅ `RoundTrip_Save_Load_Preserves_All_Fields` (OfflineCatalogTests.cs:43) — все поля типоразмера сохраняются, `Version == CurrentVersion (1)` |
| 2 | юнит-тест «Calculate использует внешний каталог» | ✅ `Calculate_Uses_External_Catalog_From_Repository` (OfflineCatalogTests.cs:162): приток SUP-TINY (ceil(500/250)=2 прибора) и отопление HT-CUSTOM берутся из внешнего JSON; + fallback-тест битого файла `Calculate_Falls_Back_To_Demo_Catalog_When_File_Broken` |
| 3 | сборка+тесты зелёные | ✅ worktree@761bbaa: `dotnet build` → Ошибок: 0, Предупреждений: 0; `dotnet test` → **98/98** (база 74/74; +24 за карточки U1.x–U2.2, из них 8 — U2.2). Ветка на момент отчёта — 108/108 (U3.1 поверх) |
| 4 | скриншот редактора | ✅ `Tasks\Отчёты\U2.2_артефакты\U2.2_catalog_editor.png` (65912 байт) — окно с 14 типоразмерами, фильтром класса и статусом валидации |

Дополнительно: grep по presenter — внешний каталог читается через
`CatalogRepository` (`ResolveCatalog()`, SnapshotWorkspacePresenter.cs:467),
`CreateDemo()` остался только seed/fallback-ом (строки 471/494); захардкоженного
вызова внутри `Calculate()` нет.

### Финальная верификация (повторный запуск, текущая ветка `230002f`)

| Проверка | Команда | Результат |
|----------|---------|-----------|
| Сборка | `dotnet build src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --nologo -v q` | EXIT 0, Ошибок: 0, Предупреждений: 0 |
| Тесты | `dotnet test src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj --no-build -v q` | не пройдено 0, пройдено **108**, пропущено 0, всего 108 |
| round-trip (кр.1) | OfflineCatalogTests.cs:43 | `RoundTrip_Save_Load_Preserves_All_Fields` |
| внешний каталог (кр.2) | OfflineCatalogTests.cs:162 | `Calculate_Uses_External_Catalog_From_Repository` + fallback `Calculate_Falls_Back_To_Demo_Catalog_When_File_Broken` |
| битый JSON (кр.4) | OfflineCatalogTests.cs:101 | `Load_Broken_Json_Throws_Clear_Error_And_File_Stays_Intact` |
| скриншот (кр.4) | `Tasks\Отчёты\U2.2_артефакты\U2.2_catalog_editor.png` | 65 912 байт — окно с 14 типоразмерами, фильтром класса, статусом валидации |

## ASSUMPTION

- Правка каталога не требует подтверждения владельца при сохранении — валидация +
  `IsDirty`-диалог достаточны.
- Битый JSON не блокирует расчёт: warning в ErrorSink + встроенный каталог; рабочий
  файл не теряется (проверено тестом `Load_Broken_Json_…`).

## Открытые вопросы

- Каталог глобальный (`%AppData%`); вариант «глобальный + копия в проект .hvacproj»
  остался за владельцем (вопрос grill плана №2).
- На момент верификации в ветке уже поверх U2.2 закрыта карточка U3.1 (108/108) —
  данные U2.2 не затронуты (worktree-проверка на `761bbaa`).

## Как проверить

1. App → «🗂 Каталог приборов…»: таблица 14 типоразмеров, фильтр класса, правка полей.
2. Изменить MaxFlowRate диффузора → Сохранить → «▶ РАССЧИТАТЬ»: количество по ByFlow меняется.
3. Ввести отрицательный расход → Сохранение заблокировано, перечень ошибок показан.
4. Испортить `%AppData%\HVACLoadTerminals\catalog.json` → расчёт не падает,
   в статусе warning «Используется встроенный каталог приборов».

## Повторная верификация (попытка 2 план-раннера, 2026-08-23 ~19:20)

Повторный независимый прогон на текущем HEAD ветки (`230002f`, поверх U3.1):

- `dotnet build src\Core.Tests\HVACLoadTerminals.Core.Tests.csproj` → Ошибок: 0,
  Предупреждений: 2 (MSB3026 — ретраи копирования из-за файловой блокировки
  зависшим testhost, не ошибки кода);
- `dotnet test` (--no-build) → **108/108** (0 failed / 0 skipped);
- критерии 1, 2, 4 подтверждены кодами выше (тесты `RoundTrip_…`,
  `Calculate_Uses_External_Catalog_…`, скриншот `U2.2_catalog_editor.png`,
  65912 байт) — без изменений кода.

Изменений в коде не потребовалось — карточка уже закрыта коммитами `761bbaa`
(код) и `dd5a4f2` (вердикт PASS).

## Коммит

Коммит `plan/U2.2`: код U2.2 — `761bbaa`, вердикт — `dd5a4f2`, настоящий отчёт —
в текущем коммите (только файл отчёта, без посторонних правок рабочего дерева).