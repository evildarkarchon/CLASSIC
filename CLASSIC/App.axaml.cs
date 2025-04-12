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
using NLog;

namespace CLASSIC;

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
        services.AddSingleton<LoggingService>();
        services.AddSingleton<GameVariables>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<GamePathService>();
        services.AddSingleton<GameIntegrityService>();
        services.AddSingleton<AudioService>();

        // Register view models
        services.AddTransient<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Initialize logging
        var logger = _serviceProvider.GetRequiredService<LoggingService>();
        logger.Info("Application starting");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };

            // Handle application exit
            desktop.Exit += (_, _) =>
            {
                logger.Info("Application exiting");
                LogManager.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}