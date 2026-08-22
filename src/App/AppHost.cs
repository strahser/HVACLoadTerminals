using System;
using HVACLoadTerminals.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HVACLoadTerminals.App
{
    public static class AppHost
    {
        private static readonly Lazy<IServiceProvider> _provider = new Lazy<IServiceProvider>(() =>
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<MainViewModel>();
            return sc.BuildServiceProvider();
        });

        public static IServiceProvider Services => _provider.Value;
    }
}
