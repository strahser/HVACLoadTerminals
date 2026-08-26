using System;
using System.IO;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Data
{
    /// <summary>
    /// JSON-хранилище UiSettings: %AppData%\HVACLoadTerminals\ui-settings.json
    /// (рядом с catalog.json), атомарная запись через tmp+Replace,
    /// реконсиляция при чтении (устаревшие ключи отбрасываются).
    /// Аналогия: JsonCatalogRepository + трёхслойный персист из референса
    /// (мгновенная локальная запись + реконсиляция с реестром).
    /// </summary>
    public class JsonUiSettingsStore
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly string _path;

        public string FilePath => _path;

        public JsonUiSettingsStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу настроек не задан", nameof(path));
            _path = path;
        }

        public static string ResolveDefaultPath()
        {
            string env = Environment.GetEnvironmentVariable("HVACLOAD_UI_SETTINGS");
            if (!string.IsNullOrWhiteSpace(env))
                return env;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HVACLoadTerminals", "ui-settings.json");
        }

        /// <summary>Загрузить с реконсиляцией; при отсутствии/битом файле — дефолты.</summary>
        public UiSettings Load()
        {
            if (!File.Exists(_path))
                return CreateDefault();

            string json;
            try
            {
                json = File.ReadAllText(_path);
            }
            catch
            {
                return CreateDefault();
            }

            UiSettings? loaded;
            try
            {
                loaded = JsonConvert.DeserializeObject<UiSettings>(json, JsonSettings);
            }
            catch (JsonException)
            {
                // Битый JSON — бэкап и дефолты (рабочий файл не перезаписываем до первого Save).
                TryBackupCorrupted();
                return CreateDefault();
            }
            catch
            {
                return CreateDefault();
            }

            if (loaded == null)
                return CreateDefault();

            // Версия новее текущей — не ломаем UI, берём дефолты (forward compat).
            if (loaded.Version > UiSettings.CurrentVersion)
                return CreateDefault();

            loaded.Reconcile();
            return loaded;
        }

        public void Save(UiSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Version = UiSettings.CurrentVersion;
            settings.Reconcile();

            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tmp = _path + ".tmp";
            string json = JsonConvert.SerializeObject(settings, JsonSettings) + Environment.NewLine;
            File.WriteAllText(tmp, json);
            if (File.Exists(_path))
                File.Replace(tmp, _path, null);
            else
                File.Move(tmp, _path);
        }

        private static UiSettings CreateDefault()
        {
            var s = new UiSettings();
            s.Reconcile();
            return s;
        }

        private void TryBackupCorrupted()
        {
            try
            {
                string backup = _path + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json";
                if (File.Exists(_path))
                    File.Copy(_path, backup, overwrite: false);
            }
            catch { /* best effort */ }
        }
    }
}
