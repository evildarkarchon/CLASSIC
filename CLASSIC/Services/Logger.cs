// Services/Logger.cs
using System;
using System.IO;
using ReactiveUI;

namespace CLASSIC.Services
{
    public class Logger : ReactiveObject
    {
        private readonly string _logPath;
        private string _lastMessage = string.Empty;
        
        public string LastMessage 
        { 
            get => _lastMessage;
            private set => this.RaiseAndSetIfChanged(ref _lastMessage, value);
        }
        
        public Logger()
        {
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Journal.log");
            
            // Check if log file exists and is older than 7 days
            if (File.Exists(_logPath))
            {
                var fileInfo = new FileInfo(_logPath);
                if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > 7)
                {
                    try
                    {
                        File.Delete(_logPath);
                        Debug("Log file was deleted and regenerated due to being older than 7 days.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred while deleting {_logPath}: {ex.Message}");
                    }
                }
            }
        }
        
        public void Debug(string message)
        {
            LogMessage("DEBUG", message);
        }
        
        public void Info(string message)
        {
            LogMessage("INFO", message);
        }
        
        public void Warning(string message)
        {
            LogMessage("WARNING", message);
        }
        
        public void Error(string message)
        {
            LogMessage("ERROR", message);
        }
        
        private void LogMessage(string level, string message)
        {
            LastMessage = message;
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {level} | {message}";
            
            try
            {
                File.AppendAllText(_logPath, logEntry + Environment.NewLine);
                Console.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine(logEntry);
            }
        }
    }
}