using System;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls;

public class EnclosureCacheManager
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WallEnclosureCache.json");

    private readonly ILogger _logger;

    public EnclosureCacheManager(ILogger logger)
    {
        _logger = logger;
    }

    public Dictionary<string, List<int>> LoadCache()
    {
        if (!File.Exists(CachePath))
        {
            _logger.Log("Файл кэша не найден");
            return new Dictionary<string, List<int>>();
        }

        try
        {
            var json = File.ReadAllText(CachePath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(json) 
                   ?? new Dictionary<string, List<int>>();
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка чтения кэша: {ex.Message}");
            return new Dictionary<string, List<int>>();
        }
    }

    public void SaveCache(Dictionary<string, List<int>> cache)
    {
        try
        {
            File.WriteAllText(CachePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
            _logger.Log($"Кэш успешно сохранен: {CachePath}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка сохранения кэша: {ex.Message}", LogLevel.Error);
        }
    }
}
