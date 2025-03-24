// Services/GameIntegrityService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CLASSIC.Models;

namespace CLASSIC.Services
{
    public class GameIntegrityService
    {
        private readonly ConfigurationService _config;
        private readonly Logger _logger;
        private readonly GameVariables _gameVars;
        
        public GameIntegrityService(ConfigurationService config, Logger logger, GameVariables gameVars)
        {
            _config = config;
            _logger = logger;
            _gameVars = gameVars;
        }
        
        public string CheckGameIntegrity()
        {
            _logger.Debug("Initiated game integrity check");
            var messageBuilder = new StringBuilder();
            
            var steamIniLocal = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_SteamINI");
            var exeHashOld = _config.GetSetting<string>(YamlStore.Game, "Game_Info.EXE_HashedOLD");
            var exeHashNew = _config.GetSetting<string>(YamlStore.Game, "Game_Info.EXE_HashedNEW");
            var gameExeLocal = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_EXE");
            var rootName = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.Main_Root_Name");
            
            if (string.IsNullOrEmpty(exeHashOld) || string.IsNullOrEmpty(rootName))
            {
                throw new InvalidOperationException("Missing required configuration values");
            }
            
            if (!string.IsNullOrEmpty(gameExeLocal) && File.Exists(gameExeLocal))
            {
                var exeHashLocal = CalculateFileHash(gameExeLocal);
                
                // Check if executable hash matches known values
                if ((exeHashLocal == exeHashOld || exeHashLocal == exeHashNew) && 
                    (string.IsNullOrEmpty(steamIniLocal) || !File.Exists(steamIniLocal)))
                {
                    messageBuilder.AppendLine($"✔️ You have the latest version of {rootName}!");
                    messageBuilder.AppendLine("-----");
                }
                else if (!string.IsNullOrEmpty(steamIniLocal) && File.Exists(steamIniLocal))
                {
                    messageBuilder.AppendLine($"\U0001F480 CAUTION : YOUR {rootName} GAME / EXE VERSION IS OUT OF DATE");
                    messageBuilder.AppendLine("-----");
                }
                else
                {
                    messageBuilder.AppendLine($"❌ CAUTION : YOUR {rootName} GAME / EXE VERSION IS OUT OF DATE");
                    messageBuilder.AppendLine("-----");
                }
                
                // Check if game is installed in Program Files
                if (!gameExeLocal.Contains("Program Files"))
                {
                    messageBuilder.AppendLine($"✔️ Your {rootName} game files are installed outside of the Program Files folder!");
                    messageBuilder.AppendLine("-----");
                }
                else
                {
                    var rootWarn = _config.GetSetting<string>(YamlStore.Main, "Warnings_GAME.warn_root_path");
                    if (!string.IsNullOrEmpty(rootWarn))
                    {
                        messageBuilder.AppendLine(rootWarn);
                    }
                }
            }
            
            return messageBuilder.ToString();
        }
        
        public string CheckXSEIntegrity()
        {
            _logger.Debug("Initiated XSE integrity check");
            var messageBuilder = new StringBuilder();
            var failedList = new StringBuilder();
            
            var catchErrors = _config.GetSetting<string[]>(YamlStore.Main, "catch_log_errors") ?? Array.Empty<string>();
            var xseAcronym = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_Acronym");
            var xseLogFile = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Docs_File_XSE");
            var xseFullName = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_FullName");
            var xseVerLatest = _config.GetSetting<string>(YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_Ver_Latest");
            var adlibFileStr = _config.GetSetting<string>(YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_File_AddressLib");
            
            if (string.IsNullOrEmpty(xseAcronym) || string.IsNullOrEmpty(xseFullName) || string.IsNullOrEmpty(xseVerLatest))
            {
                throw new InvalidOperationException("Missing required XSE configuration values");
            }
            
            // Check Address Library
            if (!string.IsNullOrEmpty(adlibFileStr) && File.Exists(adlibFileStr))
            {
                messageBuilder.AppendLine("✔️ REQUIRED: *Address Library* for Script Extender is installed!");
                messageBuilder.AppendLine("-----");
            }
            else if (!string.IsNullOrEmpty(adlibFileStr))
            {
                var warnAdlib = _config.GetSetting<string>(YamlStore.Game, "Warnings_MODS.Warn_ADLIB_Missing");
                if (!string.IsNullOrEmpty(warnAdlib))
                {
                    messageBuilder.AppendLine(warnAdlib);
                }
            }
            else
            {
                messageBuilder.AppendLine($"❌ Value for Address Library is invalid or missing from CLASSIC {_gameVars.Game} Local.yaml!");
                messageBuilder.AppendLine("-----");
            }
            
            // Check XSE log file
            if (!string.IsNullOrEmpty(xseLogFile) && File.Exists(xseLogFile))
            {
                messageBuilder.AppendLine($"✔️ REQUIRED: *{xseFullName}* is installed!");
                messageBuilder.AppendLine("-----");
                
                var xseData = File.ReadAllLines(xseLogFile);
                
                if (xseData.Length > 0 && xseData[0].Contains(xseVerLatest))
                {
                    messageBuilder.AppendLine($"✔️ You have the latest version of *{xseFullName}*!");
                    messageBuilder.AppendLine("-----");
                }
                else
                {
                    var warnOutdated = _config.GetSetting<string>(YamlStore.Game, "Warnings_XSE.Warn_Outdated");
                    if (!string.IsNullOrEmpty(warnOutdated))
                    {
                        messageBuilder.AppendLine(warnOutdated);
                    }
                }
                
                // Check for errors in log
                foreach (var line in xseData)
                {
                    foreach (var errorType in catchErrors)
                    {
                        if (line.ToLower().Contains(errorType.ToLower()))
                        {
                            failedList.AppendLine($"ERROR > {line.Trim()}");
                        }
                    }
                }
                
                if (failedList.Length > 0)
                {
                    messageBuilder.AppendLine($"#❌ CAUTION : {xseAcronym}.log REPORTS THE FOLLOWING ERRORS #");
                    messageBuilder.Append(failedList);
                    messageBuilder.AppendLine("-----");
                }
            }
            else
            {
                messageBuilder.AppendLine($"❌ CAUTION : *{xseAcronym.ToLower()}.log* FILE IS MISSING FROM YOUR DOCUMENTS FOLDER!");
                messageBuilder.AppendLine($"   You need to run the game at least once with {xseAcronym.ToLower()}_loader.exe");
                messageBuilder.AppendLine("    After that, try running CLASSIC again!");
                messageBuilder.AppendLine("-----");
            }
            
            return messageBuilder.ToString();
        }
        
        public string CheckXSEHashes()
        {
            _logger.Debug("Initiated XSE file hash check");
            var messageBuilder = new StringBuilder();
            bool xseScriptMissing = false;
            bool xseScriptMismatch = false;
            
            var xseHashedScripts = _config.GetSetting<Dictionary<string, string>>(
                YamlStore.Game, $"Game{_gameVars.VR}_Info.XSE_HashedScripts") ?? 
                new Dictionary<string, string>();
                
            var gameFolderScripts = _config.GetSetting<string>(
                YamlStore.GameLocal, $"Game{_gameVars.VR}_Info.Game_Folder_Scripts");
                
            if (string.IsNullOrEmpty(gameFolderScripts))
            {
                throw new InvalidOperationException("Missing game folder scripts path");
            }
            
            var xseHashedScriptsLocal = new Dictionary<string, string>();
            
            foreach (var key in xseHashedScripts.Keys)
            {
                var scriptPath = Path.Combine(gameFolderScripts, key);
                
                if (File.Exists(scriptPath))
                {
                    var fileHash = CalculateFileHash(scriptPath);
                    xseHashedScriptsLocal[key] = fileHash;
                }
                else
                {
                    xseHashedScriptsLocal[key] = null;
                }
            }
            
            // Compare hashes
            foreach (var key in xseHashedScripts.Keys)
            {
                if (xseHashedScriptsLocal.TryGetValue(key, out var localHash))
                {
                    var knownHash = xseHashedScripts[key];
                    
                    if (knownHash == localHash)
                    {
                        // Hash matches, file is good
                    }
                    else if (localHash == null) // File is missing
                    {
                        messageBuilder.AppendLine($"❌ CAUTION : {key} Script Extender file is missing from your game Scripts folder!");
                        messageBuilder.AppendLine("-----");
                        xseScriptMissing = true;
                    }
                    else // Hash mismatch
                    {
                        messageBuilder.AppendLine($"[!] CAUTION : {key} Script Extender file is outdated or overridden by another mod!");
                        messageBuilder.AppendLine("-----");
                        xseScriptMismatch = true;
                    }
                }
            }
            
            // Add warnings if missing or mismatched files found
            if (xseScriptMissing)
            {
                var warnMissing = _config.GetSetting<string>(YamlStore.Game, "Warnings_XSE.Warn_Missing");
                if (!string.IsNullOrEmpty(warnMissing))
                {
                    messageBuilder.AppendLine(warnMissing);
                }
            }
            
            if (xseScriptMismatch)
            {
                var warnMismatch = _config.GetSetting<string>(YamlStore.Game, "Warnings_XSE.Warn_Mismatch");
                if (!string.IsNullOrEmpty(warnMismatch))
                {
                    messageBuilder.AppendLine(warnMismatch);
                }
            }
            
            if (!xseScriptMissing && !xseScriptMismatch)
            {
                messageBuilder.AppendLine("✔️ All Script Extender files have been found and accounted for!");
                messageBuilder.AppendLine("-----");
            }
            
            return messageBuilder.ToString();
        }
        
        private string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}