using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HVACLoadTerminals.Infrastructure.Data
{
    /// <summary>JSON-документ каталога приборов (U2.2).</summary>
    public class CatalogDocument
    {
        public int Version { get; set; } = JsonCatalogRepository.CurrentVersion;
        public List<TerminalDevice> Devices { get; set; } = new List<TerminalDevice>();
    }

    /// <summary>
    /// Офлайн-каталог приборов в JSON (карточка U2.2): чтение/запись без Revit и
    /// без пересборки. Дефолтный путь — <c>%AppData%\HVACLoadTerminals\catalog.json</c>;
    /// переопределяется переменной окружения <c>HVACLOAD_CATALOG</c> или свойством
    /// <see cref="DefaultPathOverride"/> («рядом с проектом» — опция карточки).
    /// Первый запуск: файл отсутствует → seed из <see cref="CatalogFactory.CreateDemo"/>.
    /// </summary>
    public class JsonCatalogRepository : ITerminalCatalogRepository
    {
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            // Читаемые имена систем ("Supply"), файл правится руками без пересборки.
            Converters = { new StringEnumConverter() }
        };

        private readonly string _path;

        /// <summary>Версия последнего загруженного/сохранённого документа.</summary>
        public int Version { get; private set; }

        /// <summary>Путь файла каталога, с которым работает репозиторий.</summary>
        public string FilePath => _path;

        /// <summary>Приоритетная альтернатива дефолтному пути (опция «рядом с проектом»).</summary>
        public static string? DefaultPathOverride { get; set; }

        public JsonCatalogRepository(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к каталогу не задан", nameof(path));
            _path = path;
            Version = CurrentVersion;
        }

        /// <summary>
        /// Путь по умолчанию: <c>%AppData%\HVACLoadTerminals\catalog.json</c>,
        /// либо переменная окружения <c>HVACLOAD_CATALOG</c>, либо
        /// <see cref="DefaultPathOverride"/>.
        /// </summary>
        public static string ResolveDefaultPath()
        {
            if (!string.IsNullOrWhiteSpace(DefaultPathOverride))
                return DefaultPathOverride!;
            string env = Environment.GetEnvironmentVariable("HVACLOAD_CATALOG");
            if (!string.IsNullOrWhiteSpace(env))
                return env;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HVACLoadTerminals", "catalog.json");
        }

        /// <summary>Seed при первом запуске: нет файла → записать демо-каталог.</summary>
        public void EnsureSeeded()
        {
            if (File.Exists(_path))
                return;
            SaveAll(CatalogFactory.CreateDemo());
        }

        /// <summary>Полный документ: версия + приборы. Бросает внятную ошибку на битый JSON.</summary>
        public CatalogDocument LoadDocument()
        {
            if (!File.Exists(_path))
                throw new FileNotFoundException($"Файл каталога приборов не найден: {_path}", _path);

            string json;
            try
            {
                json = File.ReadAllText(_path);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Не удалось прочитать каталог приборов «{_path}»: {ex.Message}", ex);
            }

            CatalogDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CatalogDocument>(json, JsonSettings)
                    ?? throw new InvalidDataException(
                        $"Файл каталога приборов пуст или повреждён: {_path}");
            }
            catch (JsonException ex)
            {
                // Битый JSON — внятная ошибка; рабочий файл не трогаем.
                throw new InvalidDataException(
                    $"Файл каталога приборов повреждён: {_path}\n{ex.Message}", ex);
            }

            var errors = Validate(document.Devices);
            if (errors.Count > 0)
                throw new InvalidDataException(
                    $"Каталог приборов «{_path}» содержит некорректные записи:\n- " +
                    string.Join("\n- ", errors));

            Version = document.Version;
            return document;
        }

        public IReadOnlyList<TerminalDevice> GetAllDevices() => LoadDocument().Devices;

        public IReadOnlyList<TerminalDevice> GetDevicesBySystemType(HVACSystemType systemType) =>
            GetAllDevices().Where(d => d.SystemType == systemType).ToList();

        public TerminalDevice? GetDeviceById(string id) =>
            GetAllDevices().FirstOrDefault(d =>
                string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Валидация и атомарное сохранение: сначала tmp-файл, затем замена — сбой
        /// посреди записи не уничтожает рабочий каталог.
        /// </summary>
        public void SaveAll(IEnumerable<TerminalDevice> devices)
        {
            var list = devices?.ToList() ?? throw new ArgumentNullException(nameof(devices));
            var errors = Validate(list);
            if (errors.Count > 0)
                throw new InvalidDataException(
                    "Каталог не сохранён — исправьте ошибки:\n- " + string.Join("\n- ", errors));

            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var document = new CatalogDocument { Version = CurrentVersion, Devices = list };
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(document, JsonSettings) +
                                   Environment.NewLine);
            if (File.Exists(_path))
                File.Replace(tmp, _path, destinationBackupFileName: null);
            else
                File.Move(tmp, _path);

            Version = CurrentVersion;
        }

        /// <summary>Правила валидации (расход &gt; 0 у воздушных систем и т.п.).</summary>
        public static IReadOnlyList<string> Validate(IReadOnlyList<TerminalDevice> devices)
        {
            var errors = new List<string>();
            if (devices.Count == 0)
                errors.Add("каталог пуст — добавьте хотя бы один прибор");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                string label = $"[{i + 1}]";
                if (string.IsNullOrWhiteSpace(d.Id))
                {
                    errors.Add($"{label} не заполнен идентификатор (Id)");
                }
                else if (!ids.Add(d.Id))
                {
                    errors.Add($"{label} дубликат Id «{d.Id}»");
                }
                if (string.IsNullOrWhiteSpace(d.FamilyName))
                    errors.Add($"{label} {d.Id}: не заполнено семейство");
                if (string.IsNullOrWhiteSpace(d.TypeName))
                    errors.Add($"{label} {d.Id}: не заполнен типоразмер");

                if (d.MaxFlowRate < 0)
                    errors.Add($"{label} {d.Id}: расход не может быть отрицательным");
                if (d.SystemType != HVACSystemType.Heating && d.MaxFlowRate <= 0)
                    errors.Add(
                        $"{label} {d.Id}: для системы {d.SystemType} расход должен быть > 0");

                if (d.CoolingCapacityW < 0 || d.HeatingCapacityW < 0 ||
                    d.ServiceAreaM2 < 0 || d.WidthMm < 0 || d.HeightMm < 0)
                    errors.Add($"{label} {d.Id}: мощности, площадь и габариты должны быть ≥ 0");
            }

            return errors;
        }
    }
}
