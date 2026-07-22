using System;
using HVACLoadTerminals.Core.Interfaces;
using HVACLoadTerminals.Core.Services;
using HVACLoadTerminals.Infrastructure.Data;
using HVACLoadTerminals.Infrastructure.Services;
using HVACLoadTerminals.Infrastructure.Visualization;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public static class AppHost
    {
        private static readonly Lazy<IServiceProvider> _provider = new Lazy<IServiceProvider>(() =>
        {
            var sc = new ServiceCollection();

            sc.AddSingleton<ITerminalPlacementService, TerminalPlacementService>();
            sc.AddSingleton<IPolygonVisualizer, OxyPlotVisualizer>();
            sc.AddSingleton<PolygonOffsetService>();
            sc.AddSingleton<TerminalSelectionService>();

            sc.AddSingleton<DemoRoomDataService>();
            sc.AddSingleton<JsonRoomDataStore>(_ =>
                new JsonRoomDataStore(AppDomain.CurrentDomain.BaseDirectory + "\\room_data.json"));

            sc.AddTransient<ViewModels.MainViewModel>();

            return sc.BuildServiceProvider();
        });

        public static IServiceProvider Services => _provider.Value;
    }
}
