// ViewModels/MainViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using CLASSIC.Models;
using CLASSIC.Services;
using ReactiveUI;
using Avalonia.Platform.Storage;

namespace CLASSIC.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public BackupOptions BackupOptions { get; set; } = new BackupOptions();
        private readonly ConfigurationService _config;
        private readonly Logger _logger;
        private readonly GamePathService _pathService;
        private readonly GameIntegrityService _integrityService;
        private readonly GameVariables _gameVars;
        
        private string _outputText = string.Empty;
        private string? _customScanPath = string.Empty;
        private string? _modsFolderPath = string.Empty;
        private bool _isBusy;
        
        public string OutputText 
        { 
            get => _outputText;
            private set => this.RaiseAndSetIfChanged(ref _outputText, value);
        }
        
        public string? CustomScanPath
        {
            get => _customScanPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _customScanPath, value);
                _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.SCAN Custom Path", value);
            }
        }
        
        public string? ModsFolderPath
        {
            get => _modsFolderPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _modsFolderPath, value);
                _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.MODS Folder Path", value);
            }
        }
        
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }
        
        // Settings properties
        public bool FCXMode
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.FCX Mode");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.FCX Mode", value);
        }
        
        public bool SimplifyLogs
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.Simplify Logs");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Simplify Logs", value);
        }
        
        public bool UpdateCheck
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.Update Check");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Update Check", value);
        }
        
        public bool VRMode
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.VR Mode");
            set 
            { 
                _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.VR Mode", value);
                _gameVars.Vr = value ? "VR" : string.Empty;
            }
        }
        
        public bool ShowFormIDValues
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.Show FormID Values");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Show FormID Values", value);
        }
        
        public bool MoveUnsolvedLogs
        {
            get => _config.GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.Move Unsolved Logs");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Move Unsolved Logs", value);
        }
        
        public bool AudioNotifications
        {
            get => _config.GetSetting(YamlStore.Settings, "CLASSIC_Settings.Audio Notifications", true);
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Audio Notifications", value);
        }
        
        public string? UpdateSource
        {
            get => _config.GetSetting<string>(YamlStore.Settings, "CLASSIC_Settings.Update Source", "Both");
            set => _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Update Source", value);
        }
        
        public ObservableCollection<string> UpdateSourceOptions { get; } = ["Nexus", "GitHub", "Both"];
        
        // Commands
        public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
        public ReactiveCommand<Unit, Unit> ScanGameFilesCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectCustomScanFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectModsFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSettingsFileCommand { get; }
        public ReactiveCommand<Unit, Unit> CheckUpdatesCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowHelpCommand { get; }
        
        // Article link commands
        public ReactiveCommand<string, Unit> OpenUrlCommand { get; }
        
        // Backup tab commands
        public ReactiveCommand<BackupOptions, Unit> BackupCommand { get; }
        public ReactiveCommand<BackupOptions, Unit> RestoreCommand { get; }
        public ReactiveCommand<BackupOptions, Unit> RemoveCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenBackupFolderCommand { get; }
        
        public MainViewModel(
            ConfigurationService config,
            Logger logger,
            GamePathService pathService,
            GameIntegrityService integrityService,
            GameVariables gameVars)
        {
            _config = config;
            _logger = logger;
            _pathService = pathService;
            _integrityService = integrityService;
            _gameVars = gameVars;
            
            // Initialize folder paths
            var scanPath = _config.GetSetting<string>(YamlStore.Settings, "CLASSIC_Settings.SCAN Custom Path");
            if (!string.IsNullOrEmpty(scanPath))
                _customScanPath = scanPath;
                
            var modsPath = _config.GetSetting<string>(YamlStore.Settings, "CLASSIC_Settings.MODS Folder Path");
            if (!string.IsNullOrEmpty(modsPath))
                _modsFolderPath = modsPath;
            
            // Initialize commands
            ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogs);
            ScanGameFilesCommand = ReactiveCommand.CreateFromTask(ScanGameFiles);
            SelectCustomScanFolderCommand = ReactiveCommand.CreateFromTask(SelectCustomScanFolder);
            SelectModsFolderCommand = ReactiveCommand.CreateFromTask(SelectModsFolder);
            OpenSettingsFileCommand = ReactiveCommand.Create(OpenSettingsFile);
            CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckUpdates);
            ShowAboutCommand = ReactiveCommand.Create(ShowAbout);
            ShowHelpCommand = ReactiveCommand.Create(ShowHelp);
            
            OpenUrlCommand = ReactiveCommand.Create<string>(OpenUrl);
            
            BackupCommand = ReactiveCommand.CreateFromTask<BackupOptions>(
                options => ManageFiles(options, BackupOperation.Backup));
            
            RestoreCommand = ReactiveCommand.CreateFromTask<BackupOptions>(
                options => ManageFiles(options, BackupOperation.Restore));
                
            RemoveCommand = ReactiveCommand.CreateFromTask<BackupOptions>(
                options => ManageFiles(options, BackupOperation.Remove));
                
            OpenBackupFolderCommand = ReactiveCommand.Create(OpenBackupFolder);
            
            // Initialize application
            InitializeAsync();
        }
        
        private async void InitializeAsync()
        {
            try
            {
                // Generate required files and settings
                _config.GenerateRequiredFiles();
                
                OutputText = $"Hello World! | Crash Log Auto Scanner & Setup Integrity Checker | " +
                            $"{_config.GetSetting<string>(YamlStore.Main, "CLASSIC_Info.version")} | " +
                            $"{_config.GetSetting<string>(YamlStore.Game, "Game_Info.Main_Root_Name")}\n\n" +
                            "REMINDER: COMPATIBLE CRASH LOGS MUST START WITH 'crash-' AND MUST HAVE .log EXTENSION\n\n" +
                            "❓ PLEASE WAIT WHILE CLASSIC CHECKS YOUR SETTINGS AND GAME SETUP...";
                
                await Task.Run(() => {
                    _logger.Debug($"> > > STARTED {_config.GetSetting<string>(YamlStore.Main, "CLASSIC_Info.version")}");
                    
                    var gamePath = _config.GetSetting<string>(
                        YamlStore.GameLocal, $"Game{_gameVars.Vr}_Info.Root_Folder_Game");
                    
                    if (string.IsNullOrEmpty(gamePath))
                    {
                        // Find and generate required paths
                        _pathService.FindDocsPath();
                        _pathService.GenerateDocsPaths();
                        _pathService.FindGamePath();
                        _pathService.GenerateGamePaths();
                    }
                    else
                    {
                        // Perform backup
                        BackupMainFiles();
                    }
                });
                
                // Check for updates
                if (UpdateCheck)
                {
                    await CheckUpdates();
                }
                
                OutputText += "\n\n✔️ ALL CLASSIC AND GAME SETTINGS CHECKS HAVE BEEN PERFORMED!\n" +
                              "    YOU CAN NOW SCAN YOUR CRASH LOGS, GAME AND/OR MOD FILES\n";
            }
            catch (Exception ex)
            {
                _logger.Error($"Initialization error: {ex.Message}");
                OutputText += $"\n\nError during initialization: {ex.Message}";
            }
        }
        
        private void BackupMainFiles()
        {
            // Implementation similar to Python's main_files_backup method
            // This would create backups of important game files
        }
        
        private async Task ScanCrashLogs()
        {
            try
            {
                IsBusy = true;
                
                OutputText += "\n\nScanning crash logs, please wait...\n";
                
                await Task.Run(() => {
                    // Implementation of crash log scanning would go here
                    // Will involve methods from ScanLogs.cs
                });
                
                OutputText += "\nScan complete!\n";
                
                // Play notification sound if enabled
                if (AudioNotifications)
                {
                    // Play sound
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error scanning crash logs: {ex.Message}");
                OutputText += $"\nError during scan: {ex.Message}";
                
                // Play error sound if enabled
                if (AudioNotifications)
                {
                    // Play error sound
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private async Task ScanGameFiles()
        {
            try
            {
                IsBusy = true;
                
                OutputText += "\n\nScanning game files, please wait...\n";
                
                await Task.Run(() => {
                    var result = _integrityService.CheckGameIntegrity();
                    result += _integrityService.CheckXseIntegrity();
                    result += _integrityService.CheckXseHashes();
                    
                    // More scan methods would be implemented here
                    
                    OutputText += "\n" + result;
                });
                
                OutputText += "\nScan complete!\n";
                
                // Play notification sound if enabled
                if (AudioNotifications)
                {
                    // Play sound
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error scanning game files: {ex.Message}");
                OutputText += $"\nError during scan: {ex.Message}";
                
                // Play error sound if enabled
                if (AudioNotifications)
                {
                    // Play error sound
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private async Task SelectCustomScanFolder()
        {
            // Get the current top-level window
            var topLevel = TopLevel.GetTopLevel(Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? 
                desktop.MainWindow : null);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Custom Scan Folder",
                    AllowMultiple = false
                });
                
                if (folders.Count > 0)
                {
                    CustomScanPath = folders[0].Path.LocalPath;
                }
            }
        }
        
        private async Task SelectModsFolder()
        {
            // Get the current top-level window
            var topLevel = TopLevel.GetTopLevel(Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ?
                desktop.MainWindow : null);
        
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Staging Mods Folder",
                    AllowMultiple = false
                });
        
                if (folders.Count > 0)
                {
                    ModsFolderPath = folders[0].Path.LocalPath;
                }
            }
        }
        
        private void OpenSettingsFile()
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");
            
            if (File.Exists(settingsPath))
            {
                OpenFile(settingsPath);
            }
        }
        
        private async Task CheckUpdates()
        {
            // Implementation of update checking
            // This would connect to GitHub or Nexus based on settings
        }
        
        private void ShowAbout()
        {
            // Implementation to show About dialog 
        }
        
        private void ShowHelp()
        {
            // Implementation to show Help dialog with text from YAML settings
        }
        
        private void OpenUrl(string url)
        {
            // Implementation to open URL in browser
        }
        
        private async Task ManageFiles(BackupOptions options, BackupOperation operation)
        {
            try
            {
                IsBusy = true;
                
                string operationName = operation.ToString().ToUpper();
                OutputText += $"\n\n{operationName} {options.Type} files, please wait...\n";
                
                await Task.Run(() => {
                    // Implementation of file backup/restore/remove
                });
                
                OutputText += $"\n{operationName} operation complete!\n";
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during {operation} operation: {ex.Message}");
                OutputText += $"\nError: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private void OpenBackupFolder()
        {
            var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Backup", "Game Files");
            
            if (Directory.Exists(backupPath))
            {
                OpenFile(backupPath);
            }
            else
            {
                Directory.CreateDirectory(backupPath);
                OpenFile(backupPath);
            }
        }
        
        private void OpenFile(string path)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = true;
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error opening file {path}: {ex.Message}");
                OutputText += $"\nError opening file: {ex.Message}";
            }
        }
    }
    
    public class BackupOptions
    {
        public string Type { get; set; } = string.Empty;
    }
}