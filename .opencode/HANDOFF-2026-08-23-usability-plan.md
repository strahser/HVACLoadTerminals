# HANDOFF — HVACLoadTerminals, план ui-usability-critique-fixes (23.08.2026, конец сессии)

## Репозитории и коммиты

- `D:\Projects\HVACLoadTerminals` — ветка `refactoring/solid-clean-architecture`,
  HEAD `cffde79`, запушен (origin синхронизирован).
- `D:\Projects\ProjectsPalns` — план
  `HVACLoadTerminals/_current/2026-08-23_ui-usability-critique-fixes.md`,
  коммит `606d934`, запушен.
- Прогресс плана: **6 из 7 карточек выполнено** — U1.1 ✅ U1.2 ✅ U1.3 ✅
  U2.1 ✅ U2.2 ✅ U3.1 ✅. Осталась **U3.2** (гигиена репозитория).

## Контекст

План «исправления по критике UI» исполнялся план-раннером dev-pipeline v2
(`python -X utf8 -m agents.plan_runner --project hvacloadterminals`).
Из-за инфраструктурных сбоев карточки U1.3, U2.2, U3.1 закрывались вручную:
работа субагентов сохранялась, отчёт дописывался под шаблон контролёра
(секции «Что сделано» / «Доказательства» / «Открытые вопросы»), затем
`python -X utf8 -m pipeline.cli verify hvacloadterminals <CARD>` → PASS →
`pipeline.plans.set_card_status` → коммит/пуш обоих репозиториев.
Базлайн тестов вырос 74 → 108/108, сборка sln EXITCODE=0.

## ГОТОВО

- U1.1 фильтр уровней, U1.2 выбор комнат, U1.3 WebView2-превью,
  U2.1 паттерны WallPattern/SingleRule, U2.2 офлайн-каталог с CRUD,
  U3.1 паритет хостов (мм, k_ef цветом, debounce, валидация) — всё PASS,
  статусы `done` в файле плана.
- Чекпоинт этапа U1 владельцем формально не одобрен (работа шла по команде
  «продолжи»); этап U2 закрывается карточкой U3.2-зависимостей не имеет.

## ЗАДАЧА (продолжение)

Закрыть **U3.2 — гигиена репозитория** (последняя карточка плана):
1. Запустить конвейер: `D:\Projects\dev-pipeline\run_hvac_runner.cmd`
   (окно свернётся, лог — `Tasks\Konveyer_console.log`, состояние —
   `Tasks\Конвейер\runner_state.json`); раннер сам возьмёт U3.2.
   Либо исполнить карточку вручную по процедуре выше (одна карточка = один
   коммит `plan/U3.2: …`, отчёт в `Tasks\Отчёты\`, verify → PASS →
   `set_card_status` → пуш обоих репо).
2. DoD карточки: sln EXITCODE=0; Core.Tests ≥108; `git ls-files` без *.exe;
   README соответствует коду; легаси первого поколения удалено.
3. После U3.2: перенести план в `_done/`, обновить ревью-блок (✅) и индекс
   README ProjectsPalns, коммит `plans: HVACLoadTerminals: …`, пуш.

## Цикл работы

Карточка → субагент/ручное исполнение → отчёт с секциями шаблона →
`verify` PASS → `set_card_status done` → коммит `plan/<CARD>: …` →
пуш HVACLoadTerminals и ProjectsPalns.

## Грабли (все гвозди программы 23.08.2027 забиты, но помни о них)

1. **runner.lock**: после насильственного убийства раннера файл
   `Tasks\Конвейер\runner.lock` остаётся — новый раннер откажется стартовать
   («на проекте уже работает конвейер»). Лок удалить руками (pid мёртв).
2. **Таймаут сессии**: `run_hvac_runner.cmd` ставит `SUBAGENT_TIMEOUT_SEC=3600`
   (agent_manager.py теперь читает env; дефолт 1800 мал для тяжёлых карточек).
   Убитый по таймауту opencode оставляет WIP в дереве — не сбрасывать, следующие
   попытки/ручное закрытие продолжают с него.
3. **Разрешения opencode**: `opencode.json` в корне проекта даёт headless-агентам
   external_directory (snapshots_raw HeatLossRevit2 в %AppData%, .nuget,
   .NET Framework, Temp). Новая потребность в чужих каталогах = дополнить список,
   иначе попытка сгорит на auto-reject.
4. **Отчёт по точному пути**: субагент обязан писать отчёт в путь из промпта
   (с ЧЧММСС); фолбэк на свежий отчёт карточки (<12 ч) в plan_runner есть.
5. **opencode run через cmd** падал на лимите 8191 символов — session_worker
   теперь запускает настоящий `...\npm\node_modules\opencode-ai\bin\opencode.exe`
   напрямую (лимит 32767).
6. **Фигурные скобки в хвостах ошибок** субагентов роняли раннер в format_map —
   в agent_manager._build_subprompt есть try/except fallback (не удалять).
7. **Секция отчёта**: контролёр ищет литеральные «Доказательства» и
   «Открытые вопросы» (алиасы см. pipeline/cli.py SECTION_ALIASES) — без них
   вердикт PARTIAL = ретрай.
8. Параллельные сессии других проектов пишут в общие логи/панель — состояние
   конвейера читать только из `runner_state.json` своего проекта.
