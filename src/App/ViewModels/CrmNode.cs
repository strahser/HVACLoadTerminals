using System.Collections.ObjectModel;

namespace HVACLoadTerminals.App.ViewModels
{
    /// <summary>
    /// M1.2: узел дерева CRM «Системы → Уровни → Помещения». Kind:
    /// System / Level / Room. Содержит счётчик приборов и ключи фильтрации.
    /// </summary>
    public class CrmNode
    {
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";

        /// <summary>Имя системы (для System/Level/Room-узлов, если применимо).</summary>
        public string? SystemName { get; set; }

        /// <summary>Уровень (для Level/Room).</summary>
        public string? LevelName { get; set; }

        /// <summary>S_ID комнаты (для Room).</summary>
        public string? RoomId { get; set; }

        /// <summary>Число приборов в поддереве.</summary>
        public int DeviceCount { get; set; }

        public string CountText => DeviceCount > 0 ? $" · {DeviceCount} шт" : "";

        public ObservableCollection<CrmNode> Children { get; } = new();
    }
}
