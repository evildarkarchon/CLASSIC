// App.axaml.cs
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CLASSIC.Models;
using CLASSIC.Services;
using CLASSIC.ViewModels;
using CLASSIC.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CLASSIC
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure services
            var services = new ServiceCollection();
            
            // Register services
            services.AddSingleton<Logger>();
            services.AddSingleton<GameVariables>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<GamePathService>();
            services.AddSingleton<GameIntegrityService>();
            
            // Register view models
            services.AddTransient<MainViewModel>();
            
            _serviceProvider = services.BuildServiceProvider();
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}