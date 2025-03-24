// Services/GamePathService.cs
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CLASSIC.Models;

namespace CLASSIC.Services
{
    public class GamePathService(ConfigurationService config, Logger logger, GameVariables gameVars)
    {
        public void FindGamePath()
        {
            logger.Debug("Initiated game path check");
            string? path = null;
            
            // Try registry lookup
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{gameVars.GameNameWithVr}");
                    
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
                logger.Warning($"Registry lookup failed: {ex.Message}");
            }
            
            // Validate the found path
            if (!string.IsNullOrEmpty(path))
            {
                var exePath = Path.Combine(path, $"{gameVars.GameNameWithVr}.exe");
                
                if (File.Exists(exePath))
                {
                    config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Root_Folder_Game", path);
                    logger.Info($"Game path found in registry: {path}");
                    return;
                }
            }
            
            // If registry lookup fails, try XSE log file
            FindGamePathFromXseLog();
        }
        
        private void FindGamePathFromXseLog()
        {
            var xseFile = config.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Docs_File_XSE");
            var xseAcronym = config.GetSetting<string>(YamlStore.Game, $"Game{gameVars.Vr}_Info.XSE_Acronym");
            var xseAcronymBase = config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(xseFile) || !File.Exists(xseFile))
            {
                logger.Warning($"The {xseAcronym?.ToLower()}.log file is missing from your game documents folder!");
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
                        
                        var exePath = Path.Combine(gamePath, $"{gameVars.GameNameWithVr}.exe");
                        
                        if (File.Exists(exePath))
                        {
                            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Root_Folder_Game", gamePath);
                            logger.Info($"Game path found in XSE log: {gamePath}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to read XSE log: {ex.Message}");
            }
        }
        
        public void FindDocsPath()
        {
            logger.Debug("Initiated docs path check");
            string? docsPath = null;
            
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
                logger.Warning($"Failed to get Documents path from registry: {ex.Message}");
            }
            
            // Fallback to user profile
            if (string.IsNullOrEmpty(docsPath))
            {
                docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
            }
            
            // Construct the game's document path
            var docsName = config.GetSetting<string>(YamlStore.Game, $"Game{gameVars.Vr}_Info.Main_Docs_Name");
            
            if (!string.IsNullOrEmpty(docsName))
            {
                var gameDocsPath = Path.Combine(docsPath, "My Games", docsName);
                config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Root_Folder_Docs", gameDocsPath);
                logger.Info($"Game docs path set to: {gameDocsPath}");
            }
        }
        
        public void GenerateGamePaths()
        {
            logger.Debug("Initiated game path generation");
            
            var gamePath = config.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Root_Folder_Game");
            var xseAcronymBase = config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(xseAcronymBase))
            {
                logger.Error("Game path or XSE acronym is missing");
                return;
            }
            
            // Set data folder paths
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_Folder_Data", 
                Path.Combine(gamePath, "Data"));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_Folder_Scripts", 
                Path.Combine(gamePath, "Data", "Scripts"));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_Folder_Plugins", 
                Path.Combine(gamePath, "Data", xseAcronymBase, "Plugins"));
                
            // Set executable and INI paths
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_File_SteamINI", 
                Path.Combine(gamePath, "steam_api.ini"));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_File_EXE", 
                Path.Combine(gamePath, $"{gameVars.GameNameWithVr}.exe"));
                
            // Set address library paths based on game version
            SetAddressLibraryPaths();
        }
        
        private void SetAddressLibraryPaths()
        {
            var exePath = config.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_File_EXE");
            var pluginsPath = config.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Game_Folder_Plugins");
            
            if (string.IsNullOrEmpty(exePath) || string.IsNullOrEmpty(pluginsPath))
                return;
                
            var gameVersion = GetGameVersionFromFile(exePath);
            var xseAcronymBase = config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (gameVars.Game == GameId.Fallout4)
            {
                if (string.IsNullOrEmpty(gameVars.Vr))
                {
                    // Regular Fallout 4
                    if (gameVersion == Constants.OgVersion || gameVersion == Constants.NullVersion)
                    {
                        config.SetSetting(YamlStore.GameLocal, "Game_Info.Game_File_AddressLib", 
                            Path.Combine(pluginsPath, "version-1-10-163-0.bin"));
                    }
                    else if (gameVersion == Constants.NgVersion)
                    {
                        config.SetSetting(YamlStore.GameLocal, "Game_Info.Game_File_AddressLib", 
                            Path.Combine(pluginsPath, "version-1-10-984-0.bin"));
                    }
                }
                else
                {
                    // Fallout 4 VR
                    config.SetSetting(YamlStore.GameLocal, "GameVR_Info.Game_File_AddressLib", 
                        Path.Combine(pluginsPath, "version-1-2-72-0.csv"));
                }
            }
        }
        
        private string GetGameVersionFromFile(string? exePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (exePath != null)
                    {
                        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                        return $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to get game version: {ex.Message}");
            }
            
            return Constants.NullVersion;
        }
        
        public void GenerateDocsPaths()
        {
            logger.Debug("Initiated docs path generation");
            
            var docsPath = config.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Root_Folder_Docs");
            var xseAcronym = config.GetSetting<string>(YamlStore.Game, $"Game{gameVars.Vr}_Info.XSE_Acronym");
            var xseAcronymBase = config.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym");
            
            if (string.IsNullOrEmpty(docsPath) || string.IsNullOrEmpty(xseAcronym) || string.IsNullOrEmpty(xseAcronymBase))
            {
                logger.Error("Docs path or XSE acronym is missing");
                return;
            }
            
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Docs_Folder_XSE", 
                Path.Combine(docsPath, xseAcronymBase));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Docs_File_PapyrusLog", 
                Path.Combine(docsPath, "Logs", "Script", "Papyrus.0.log"));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Docs_File_WryeBashPC", 
                Path.Combine(docsPath, "ModChecker.html"));
                
            config.SetSetting(YamlStore.GameLocal, $"Game{gameVars.Vr}_Info.Docs_File_XSE", 
                Path.Combine(docsPath, xseAcronymBase, $"{xseAcronym.ToLower()}.log"));
        }
    }
}