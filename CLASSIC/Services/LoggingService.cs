// Services/LoggingService.cs

using System;
using NLog;
using ReactiveUI;

namespace CLASSIC.Services;

public class LoggingService : ReactiveObject
{
    private readonly Logger _logger;
    private string _lastMessage = string.Empty;

    public string LastMessage
    {
        get => _lastMessage;
        private set => this.RaiseAndSetIfChanged(ref _lastMessage, value);
    }

    public LoggingService()
    {
        _logger = LogManager.GetCurrentClassLogger();

        // Check if log file exists and is older than 7 days
        var logFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Journal.log");
        if (System.IO.File.Exists(logFilePath))
        {
            var fileInfo = new System.IO.FileInfo(logFilePath);
            if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > 7)
            {
                try
                {
                    System.IO.File.Delete(logFilePath);
                    Debug("Log file was deleted and regenerated due to being older than 7 days.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "An error occurred while deleting the log file");
                }
            }
        }

        Debug("Logging service initialized");
    }

    public void Debug(string message)
    {
        LogMessage(LogLevel.Debug, message);
    }

    public void Info(string message)
    {
        LogMessage(LogLevel.Info, message);
    }

    public void Warning(string message)
    {
        LogMessage(LogLevel.Warn, message);
    }

    public void Error(string message)
    {
        LogMessage(LogLevel.Error, message);
    }

    public void Error(Exception ex, string message)
    {
        _logger.Error(ex, message);
        LastMessage = message;
    }

    private void LogMessage(LogLevel level, string message)
    {
        LastMessage = message;
        _logger.Log(level, message);
    }
}