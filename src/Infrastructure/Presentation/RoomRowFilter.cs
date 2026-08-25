using System;
using HVACLoadTerminals.Core.Models;

namespace HVACLoadTerminals.Infrastructure.Presentation
{
    /// <summary>UX-серия: чистый предикат видимости строки помещения —
    /// уровень + поиск по номеру/названию (подстроки через пробел) + режим
    /// фильтра по назначенным системам. Таблица и массовые операции
    /// «по видимым» используют один и тот же предикат.</summary>
    public static class RoomRowFilter
    {
        public const string All = "Все помещения";
        public const string WithoutSystems = "Без назначенной системы";
        public const string WithSystems = "Есть назначения";
        public const string NoSupply = "Нет притока";
        public const string NoExhaust = "Нет вытяжки";

        public static readonly string[] Modes =
            { All, WithoutSystems, WithSystems, NoSupply, NoExhaust };

        public static bool IsVisible(
            RoomRow row, string? level, string? searchQuery, string? mode)
        {
            if (row == null)
                return false;
            if (!string.IsNullOrEmpty(level) && row.LevelName != level)
                return false;

            var query = (searchQuery ?? "").Trim();
            if (query.Length > 0)
            {
                // Каждый токен (через пробел) должен встретиться в номере ИЛИ названии.
                foreach (var token in query.Split(
                    (char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Matches(row.Number, token) && !Matches(row.Name, token))
                        return false;
                }
            }

            switch (mode ?? All)
            {
                case WithoutSystems: return !HasIncludedSystem(row, null);
                case WithSystems: return HasIncludedSystem(row, null);
                case NoSupply: return !HasIncludedSystem(row, HVACSystemType.Supply);
                case NoExhaust: return !HasIncludedSystem(row, HVACSystemType.Exhaust);
                default: return true;
            }
        }

        private static bool Matches(string? source, string token) =>
            (source ?? "").IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Назначенной считается включённая строка системы комнаты
        /// (тип учитывается, когда задан).</summary>
        private static bool HasIncludedSystem(RoomRow row, HVACSystemType? type)
        {
            var systems = row.Systems;
            if (systems == null)
                return false;
            foreach (var s in systems)
            {
                if (s.IsIncluded && (type == null || s.Type == type))
                    return true;
            }
            return false;
        }
    }
}
