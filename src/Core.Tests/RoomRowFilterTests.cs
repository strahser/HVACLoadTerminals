using System.Collections.Generic;
using HVACLoadTerminals.Core.Models;
using HVACLoadTerminals.Infrastructure.Presentation;
using Xunit;

namespace HVACLoadTerminals.Core.Tests
{
    /// <summary>UX-серия: чистый предикат видимости строки помещения —
    /// уровень + поиск по номеру/названию + фильтр по назначенным системам.</summary>
    public class RoomRowFilterTests
    {
        private static RoomRow Row(
            string number = "101", string name = "Кабинет",
            string level = "Уровень 1",
            IEnumerable<SystemRow>? systems = null)
        {
            return new RoomRow
            {
                RoomId = number,
                Number = number,
                Name = name,
                LevelName = level,
                Systems = systems != null ? new List<SystemRow>(systems) : new List<SystemRow>()
            };
        }

        [Fact]
        public void LevelFilter_SelectsOnlyRequestedLevel()
        {
            var row = Row(level: "Уровень 2");
            Assert.True(RoomRowFilter.IsVisible(row, "Уровень 2", "", RoomRowFilter.All));
            Assert.False(RoomRowFilter.IsVisible(row, "Уровень 1", "", RoomRowFilter.All));
        }

        [Fact]
        public void EmptyLevel_MatchesAnyRow()
        {
            var row = Row(level: "Чердак");
            Assert.True(RoomRowFilter.IsVisible(row, "", null, null));
        }

        [Theory]
        [InlineData("101")]
        [InlineData("кабин")]
        [InlineData("01 Каб")]
        [InlineData("  101  ")]
        public void Search_MatchesNumberOrName_CaseInsensitive(string query)
        {
            var row = Row(number: "101", name: "Кабинет переговоров");
            Assert.True(RoomRowFilter.IsVisible(row, "", query, RoomRowFilter.All));
        }

        [Fact]
        public void Search_AllTokensMustMatch()
        {
            var row = Row(number: "101", name: "Кабинет");
            // «102 кабин»: токен 102 не встречается — мимо.
            Assert.False(RoomRowFilter.IsVisible(row, "", "102 кабин", RoomRowFilter.All));
            Assert.True(RoomRowFilter.IsVisible(row, "", "101 кабин", RoomRowFilter.All));
        }

        [Fact]
        public void Search_Miss_ReturnsFalse()
        {
            var row = Row(number: "101", name: "Кабинет");
            Assert.False(RoomRowFilter.IsVisible(row, "", "серверная", RoomRowFilter.All));
        }

        private static readonly SystemRow SupplyP1 = new()
        {
            Name = "П1", Type = HVACSystemType.Supply, FlowM3h = 60
        };
        private static readonly SystemRow ExhaustV1 = new()
        {
            Name = "В1", Type = HVACSystemType.Exhaust, FlowM3h = 80
        };

        [Fact]
        public void WithoutSystems_EmptyList_Visible()
        {
            var row = Row();
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.WithoutSystems));
            Assert.False(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.WithSystems));
        }

        [Fact]
        public void WithSystems_IncludedSystem_Visible()
        {
            var row = Row(systems: new[] { SupplyP1 });
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.WithSystems));
            Assert.False(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.WithoutSystems));
        }

        [Fact]
        public void ExcludedSystem_DoesNotCountAsAssigned()
        {
            var excluded = new SystemRow
            {
                Name = "П9", Type = HVACSystemType.Supply, IsIncluded = false
            };
            var row = Row(systems: new[] { excluded });
            // Исключённая система назначением не считается: комната «чистая».
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.WithoutSystems));
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.NoSupply));
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.NoExhaust));
        }

        [Fact]
        public void NoSupply_NoExhaust_ByIncludedSystemType()
        {
            var row = Row(systems: new[] { SupplyP1 });
            Assert.True(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.NoExhaust));
            Assert.False(RoomRowFilter.IsVisible(row, "", "", RoomRowFilter.NoSupply));

            var both = Row(systems: new[] { SupplyP1, ExhaustV1 });
            Assert.False(RoomRowFilter.IsVisible(both, "", "", RoomRowFilter.NoSupply));
            Assert.False(RoomRowFilter.IsVisible(both, "", "", RoomRowFilter.NoExhaust));
        }

        [Fact]
        public void Combined_LevelAndSearchAndMode()
        {
            var cleanRoom = Row(number: "205", name: "Серверная", level: "Уровень 2");
            var assignedRoom = Row(number: "206", name: "Кабинет", level: "Уровень 2",
                systems: new[] { SupplyP1 });

            const string mode = RoomRowFilter.WithoutSystems;
            Assert.True(RoomRowFilter.IsVisible(cleanRoom, "Уровень 2", "205 сер", mode));
            Assert.False(RoomRowFilter.IsVisible(assignedRoom, "Уровень 2", "206", mode));
            // На другом уровне даже чистая комната не видна.
            Assert.False(RoomRowFilter.IsVisible(cleanRoom, "Уровень 1", "205", mode));
        }
    }
}
