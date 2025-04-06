using Avalonia;
using Avalonia.ReactiveUI;
using System;
using CLASSIC.Services;
using NLog;

namespace CLASSIC;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Configure NLog first
            var logConfig = new NLog.Config.LoggingConfiguration();
            var logFile = new NLog.Targets.FileTarget("logfile") 
            { 
                FileName = "${basedir}/CLASSIC Journal.log",
                Layout = "${longdate} | ${level:uppercase=true} | ${message} ${exception:format=toString}"
            };
            
            // Apply rules
            logConfig.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
            LogManager.Configuration = logConfig;
            
            // Start the application
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Log unhandled exceptions
            LogManager.GetCurrentClassLogger().Fatal(ex, "Application crashed with unhandled exception");
            throw;
        }
        finally
        {
            // Ensure proper shutdown of NLog
            LogManager.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}