// Services/GamePathService.cs
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CLASSIC.Models;

namespace CLASSIC.Services
{
    public class GamePathService
    {
        private readonly ConfigurationService _config;
        private readonly Logger _logger;
        private readonly GameVariables _gameVars;
        
        public GamePathService(ConfigurationService config, Logger logger, GameVariables gameVars)
        {
            _config = config;
            _logger = logger;
            _gameVars = gameVars;
        }
        
        public void FindGamePath()
        {
            _logger.Debug("Initiated game path check");
            string path = null;
            
            // Try registry lookup
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{_gameVars.GameNameWithVR}");
                    
                    if (key != null)
                    {
                        path = key.GetValue("installed path") as string;
                    }
                    else
                    {
                        // Try GOG path for Fallout 4
                        using var keyGog = Registry.LocalMachine.OpenSubKey(
                            @"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
                        if (keyGog != null)
                        {
                            path = keyGog.GetValue("path") as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Registry lookup failed: {ex.Message}");
            }
            
            // Validate the found path
            if (!string.IsNullOrEmpty(path))
            {
                var exePath = Path.Combine(path, $"{_gameVars.GameNameWithVR}.exe");
                
                if (File.Exists(exePath))
                {
                    _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Root_Folder_Game", path);
                    _logger.Info($"Game path found in registry: {path}");
                    return;
                }
            }
            
            // If registry lookup fails, try XSE log file
            FindGamePathFromXSELog();
        }
        
        private void FindGamePathFromXSELog()
        {
            var xseFile = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_File_XSE");
            var xseAcronym = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_Acronym");
            var xseAcronymBase = _config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(xseFile) || !File.Exists(xseFile))
            {
                _logger.Warning($"The {xseAcronym?.ToLower()}.log file is missing from your game documents folder!");
                return;
            }
            
            try
            {
                foreach (var line in File.ReadAllLines(xseFile))
                {
                    if (line.StartsWith("plugin directory"))
                    {
                        var gamePath = line.Split('=', 2)[1].Trim()
                            .Replace($"\\Data\\{xseAcronymBase}\\Plugins", "");
                        
                        var exePath = Path.Combine(gamePath, $"{_gameVars.GameNameWithVR}.exe");
                        
                        if (File.Exists(exePath))
                        {
                            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Root_Folder_Game", gamePath);
                            _logger.Info($"Game path found in XSE log: {gamePath}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to read XSE log: {ex.Message}");
            }
        }
        
        public void FindDocsPath()
        {
            _logger.Debug("Initiated docs path check");
            string docsPath = null;
            
            // Try to get Documents path from registry
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders");
                    
                    if (key != null)
                    {
                        docsPath = key.GetValue("Personal") as string;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to get Documents path from registry: {ex.Message}");
            }
            
            // Fallback to user profile
            if (string.IsNullOrEmpty(docsPath))
            {
                docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
            }
            
            // Construct the game's document path
            var docsName = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.Main_Docs_Name");
            
            if (!string.IsNullOrEmpty(docsName))
            {
                var gameDocsPath = Path.Combine(docsPath, "My Games", docsName);
                _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Root_Folder_Docs", gameDocsPath);
                _logger.Info($"Game docs path set to: {gameDocsPath}");
            }
        }
        
        public void GenerateGamePaths()
        {
            _logger.Debug("Initiated game path generation");
            
            var gamePath = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Root_Folder_Game");
            var xseAcronymBase = _config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(xseAcronymBase))
            {
                _logger.Error("Game path or XSE acronym is missing");
                return;
            }
            
            // Set data folder paths
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_Folder_Data", 
                Path.Combine(gamePath, "Data"));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_Folder_Scripts", 
                Path.Combine(gamePath, "Data", "Scripts"));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_Folder_Plugins", 
                Path.Combine(gamePath, "Data", xseAcronymBase, "Plugins"));
                
            // Set executable and INI paths
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_SteamINI", 
                Path.Combine(gamePath, "steam_api.ini"));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_EXE", 
                Path.Combine(gamePath, $"{_gameVars.GameNameWithVR}.exe"));
                
            // Set address library paths based on game version
            SetAddressLibraryPaths();
        }
        
        private void SetAddressLibraryPaths()
        {
            var exePath = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_EXE");
            var pluginsPath = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_Folder_Plugins");
            
            if (string.IsNullOrEmpty(exePath) || string.IsNullOrEmpty(pluginsPath))
                return;
                
            var gameVersion = GetGameVersionFromFile(exePath);
            var xseAcronymBase = _config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (_gameVars.Game == GameId.Fallout4)
            {
                if (string.IsNullOrEmpty(_gameVars.VR))
                {
                    // Regular Fallout 4
                    if (gameVersion == Constants.OGVersion || gameVersion == Constants.NullVersion)
                    {
                        _config.SetSetting(YamlStore.GameLocal, "Game_Info.Game_File_AddressLib", 
                            Path.Combine(pluginsPath, "version-1-10-163-0.bin"));
                    }
                    else if (gameVersion == Constants.NGVersion)
                    {
                        _config.SetSetting(YamlStore.GameLocal, "Game_Info.Game_File_AddressLib", 
                            Path.Combine(pluginsPath, "version-1-10-984-0.bin"));
                    }
                }
                else
                {
                    // Fallout 4 VR
                    _config.SetSetting(YamlStore.GameLocal, "GameVR_Info.Game_File_AddressLib", 
                        Path.Combine(pluginsPath, "version-1-2-72-0.csv"));
                }
            }
        }
        
        private string GetGameVersionFromFile(string exePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                    return $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}";
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get game version: {ex.Message}");
            }
            
            return Constants.NullVersion;
        }
        
        public void GenerateDocsPaths()
        {
            _logger.Debug("Initiated docs path generation");
            
            var docsPath = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Root_Folder_Docs");
            var xseAcronym = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_Acronym");
            var xseAcronymBase = _config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(docsPath) || string.IsNullOrEmpty(xseAcronym) || string.IsNullOrEmpty(xseAcronymBase))
            {
                _logger.Error("Docs path or XSE acronym is missing");
                return;
            }
            
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_Folder_XSE", 
                Path.Combine(docsPath, xseAcronymBase));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_File_PapyrusLog", 
                Path.Combine(docsPath, "Logs", "Script", "Papyrus.0.log"));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_File_WryeBashPC", 
                Path.Combine(docsPath, "ModChecker.html"));
                
            _config.SetSetting(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_File_XSE", 
                Path.Combine(docsPath, xseAcronymBase, $"{xseAcronym.ToLower()}.log"));
        }
    }
}