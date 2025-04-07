using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CLASSIC.Models;

namespace CLASSIC.Services;

/// <summary>
/// Provides access to application configuration stored in YAML files.
/// </summary>
public class ConfigurationService
{
    private readonly Dictionary<string, Dictionary<string, object?>> _yamlCache = new();
    private readonly Dictionary<string, DateTime> _fileModTimes = new();
    private readonly HashSet<YamlStore> _staticStores = [YamlStore.Main, YamlStore.Game];
    private readonly LoggingService _logger;
    private readonly GameVariables _gameVars;

    /// <summary>
    /// Initializes a new instance of the ConfigurationService class.
    /// </summary>
    /// <param name="logger">The logging service.</param>
    /// <param name="gameVars">The game variables.</param>
    public ConfigurationService(LoggingService logger, GameVariables gameVars)
    {
        _logger = logger;
        _gameVars = gameVars;

        // Verify required base directories exist
        VerifyRequiredDirectories();

        // Ensure settings file exists
        EnsureSettingsExist();

        // Set VR mode based on settings
        var vrMode = GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.VR Mode");
        _gameVars.Vr = vrMode ? "VR" : string.Empty;

        // Pre-load static YAML files
        foreach (var path in _staticStores.Select(GetYamlPath))
        {
            LoadYaml(path);
        }
    }

    /// <summary>
    /// Verifies that required directories exist, throwing an exception if they don't.
    /// </summary>
    private void VerifyRequiredDirectories()
    {
        var classicDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data");
        var databasesDir = Path.Combine(classicDataDir, "databases");

        if (!Directory.Exists(classicDataDir))
        {
            throw new DirectoryNotFoundException(
                "CLASSIC Data directory not found. Please ensure the application is properly installed.");
        }

        if (!Directory.Exists(databasesDir))
        {
            throw new DirectoryNotFoundException(
                "CLASSIC Data/databases directory not found. Please ensure the application is properly installed.");
        }
    }

    /// <summary>
    /// Ensures that the settings file exists, creating it from defaults if needed.
    /// </summary>
    private void EnsureSettingsExist()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");

        if (File.Exists(settingsPath)) return;
        _logger.Info("Settings file not found, creating from defaults");
        var defaultSettings = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_settings");

        if (string.IsNullOrEmpty(defaultSettings))
        {
            _logger.Error("Invalid Default Settings in 'CLASSIC Main.yaml'");
            throw new InvalidOperationException("Invalid Default Settings in 'CLASSIC Main.yaml'");
        }

        File.WriteAllText(settingsPath, defaultSettings);
    }

    /// <summary>
    /// Generates required files if they don't exist.
    /// </summary>
    public void GenerateRequiredFiles()
    {
        _logger.Debug("Generating required configuration files");
        GenerateIgnoreFile();
        GenerateLocalYamlFile();
    }

    /// <summary>
    /// Generates the ignore file if it doesn't exist.
    /// </summary>
    private void GenerateIgnoreFile()
    {
        var ignorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Ignore.yaml");

        if (File.Exists(ignorePath)) return;
        _logger.Info("Ignore file not found, creating from defaults");
        var defaultIgnoreFile = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_ignorefile");

        if (string.IsNullOrEmpty(defaultIgnoreFile))
        {
            _logger.Error("Invalid Default Ignore file in 'CLASSIC Main.yaml'");
            throw new InvalidOperationException("Invalid Default Ignore file in 'CLASSIC Main.yaml'");
        }

        File.WriteAllText(ignorePath, defaultIgnoreFile);
    }

    /// <summary>
    /// Generates the local YAML file if it doesn't exist.
    /// </summary>
    private void GenerateLocalYamlFile()
    {
        var localPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"CLASSIC Data/CLASSIC {_gameVars.Game}{_gameVars.Vr} Local.yaml");

        if (File.Exists(localPath)) return;
        _logger.Info("Local YAML file not found, creating from defaults");
        var dirPath = Path.GetDirectoryName(localPath);

        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            // Only create parent directory for the Local YAML file if needed
            Directory.CreateDirectory(dirPath);
        }

        var defaultLocalYaml = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_localyaml");

        if (string.IsNullOrEmpty(defaultLocalYaml))
        {
            _logger.Error("Invalid Default Local YAML in 'CLASSIC Main.yaml'");
            throw new InvalidOperationException("Invalid Default Local YAML in 'CLASSIC Main.yaml'");
        }

        File.WriteAllText(localPath, defaultLocalYaml);
    }

    /// <summary>
    /// Gets a setting value from the specified YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="store">The YAML store.</param>
    /// <param name="keyPath">The dot-separated path to the setting.</param>
    /// <param name="defaultValue">The default value to return if the setting is not found.</param>
    /// <returns>The setting value, or the default value if not found.</returns>
    public T? GetSetting<T>(YamlStore store, string keyPath, T? defaultValue = default)
    {
        try
        {
            var yamlPath = GetYamlPath(store);
            var data = LoadYaml(yamlPath);

            var keys = keyPath.Split('.');
            object currentValue = data;

            // Traverse the YAML structure
            foreach (var key in keys)
            {
                if (currentValue is Dictionary<string, object> dict && dict.TryGetValue(key, out var value))
                {
                    currentValue = value;
                }
                else
                {
                    return defaultValue;
                }
            }

            // Try to convert to the requested type
            if (currentValue is T typedValue)
            {
                return typedValue;
            }

            try
            {
                // Type conversion for common types
                if (typeof(T) == typeof(bool))
                {
                    if (currentValue is string strValue && bool.TryParse(strValue, out var boolResult))
                        return (T)(object)boolResult;
                    return (T)(object)Convert.ToBoolean(currentValue);
                }

                if (typeof(T) == typeof(int))
                {
                    if (currentValue is string strValue && int.TryParse(strValue, out var intResult))
                        return (T)(object)intResult;
                    return (T)(object)Convert.ToInt32(currentValue);
                }

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)currentValue.ToString()!;
                }

                if (typeof(T) == typeof(DirectoryInfo) && currentValue is string path)
                {
                    return (T)(object)new DirectoryInfo(path);
                }

                if (typeof(T) == typeof(FileInfo) && currentValue is string filePath)
                {
                    return (T)(object)new FileInfo(filePath);
                }

                if (typeof(T) == typeof(string[]) && currentValue is List<object> list)
                {
                    return (T)(object)list.Select(x => x.ToString()).ToArray();
                }

                if (typeof(T) == typeof(Dictionary<string, string>) &&
                    currentValue is Dictionary<string, object> objDict)
                {
                    return (T)(object)objDict.ToDictionary(kv => kv.Key, kv => kv.Value.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error converting value for path {keyPath}: {ex.Message}");
                return defaultValue;
            }

            _logger.Warning($"Could not convert setting at {keyPath} to type {typeof(T).Name}");
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting setting {keyPath}: {ex.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets a setting value in the specified YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="store">The YAML store.</param>
    /// <param name="keyPath">The dot-separated path to the setting.</param>
    /// <param name="value">The value to set.</param>
    public void SetSetting<T>(YamlStore store, string keyPath, T? value)
    {
        try
        {
            _logger.Debug($"Setting {keyPath} to {value}");

            var yamlPath = GetYamlPath(store);
            var data = LoadYaml(yamlPath);

            var keys = keyPath.Split('.');
            var currentDict = data;

            // Navigate to the final container
            for (var i = 0; i < keys.Length - 1; i++)
            {
                var key = keys[i];

                if (!currentDict.TryGetValue(key, out var nextObj) || nextObj is not Dictionary<string, object?> obj)
                {
                    var newDict = new Dictionary<string, object?>();
                    currentDict[key] = newDict;
                    currentDict = newDict;
                }
                else
                {
                    currentDict = obj;
                }
            }

            // Set the value
            currentDict[keys[^1]] = value;

            // Save the YAML file
            SaveYaml(yamlPath, data);

            // If this is a settings change that affects VR mode, update the GameVariables
            if (store == YamlStore.Settings && keyPath == "CLASSIC_Settings.VR Mode" && value is bool vrMode)
            {
                _gameVars.Vr = vrMode ? "VR" : string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error setting {keyPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the file path for the specified YAML store.
    /// </summary>
    /// <param name="store">The YAML store.</param>
    /// <returns>The file path.</returns>
    private string GetYamlPath(YamlStore store)
    {
        return store switch
        {
            YamlStore.Main => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data", "databases",
                "CLASSIC Main.yaml"),
            YamlStore.Settings => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml"),
            YamlStore.Ignore => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Ignore.yaml"),
            YamlStore.Game => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data", "databases",
                $"CLASSIC {_gameVars.Game}.yaml"),
            YamlStore.GameLocal => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data",
                $"CLASSIC {_gameVars.Game}{_gameVars.Vr} Local.yaml"),
            YamlStore.Test => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests", "test_settings.yaml"),
            _ => throw new ArgumentOutOfRangeException(nameof(store), $"Unsupported YAML store: {store}")
        };
    }

    /// <summary>
    /// Loads a YAML file with caching based on file modification time.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <returns>The parsed YAML data.</returns>
    private Dictionary<string, object?> LoadYaml(string yamlPath)
    {
        // For settings and ignore files, we'll create them
        if (yamlPath.EndsWith("CLASSIC Settings.yaml") || yamlPath.EndsWith("CLASSIC Ignore.yaml"))
        {
            return new Dictionary<string, object?>();
        }

        // For GameLocal, we'll create it in GenerateLocalYamlFile
        if (yamlPath.Contains($"CLASSIC {_gameVars.Game}{_gameVars.Vr} Local.yaml"))
        {
            return new Dictionary<string, object?>();
        }

        // Check if this is a static store
        var isStatic = _staticStores.Any(store => GetYamlPath(store) == yamlPath);

        // For static stores, just load once
        if (!isStatic || !_yamlCache.TryGetValue(yamlPath, out var yaml))
        {
            if (!isStatic)
            {
                var lastWriteTime = File.GetLastWriteTime(yamlPath);

                if (_yamlCache.TryGetValue(yamlPath, out var value) &&
                    _fileModTimes.ContainsKey(yamlPath) &&
                    _fileModTimes[yamlPath] == lastWriteTime)
                {
                    return value;
                }

                _fileModTimes[yamlPath] = lastWriteTime;
            }

            _logger.Debug($"Loading YAML file: {yamlPath}");

            try
            {
                // Read and deserialize the YAML file
                var yamlContent = File.ReadAllText(yamlPath);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var yamlObject =
                    deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                // Update the cache
                _yamlCache[yamlPath] = yamlObject!;

                return _yamlCache[yamlPath];
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading YAML file {yamlPath}: {ex.Message}");
                throw new InvalidOperationException($"Error parsing YAML file {yamlPath}: {ex.Message}", ex);
            }
        }

        // For dynamic stores, check if file has been modified
        return yaml;
    }

    /// <summary>
    /// Saves a YAML file.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <param name="data">The YAML data to save.</param>
    private void SaveYaml(string yamlPath, Dictionary<string, object?> data)
    {
        _logger.Debug($"Saving YAML file: {yamlPath}");

        try
        {
            var serializer = new SerializerBuilder()
                .Build();

            var yamlContent = serializer.Serialize(data);

            File.WriteAllText(yamlPath, yamlContent);

            // Update the cache
            _yamlCache[yamlPath] = data;
            _fileModTimes[yamlPath] = File.GetLastWriteTime(yamlPath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error saving YAML file {yamlPath}: {ex.Message}");
            throw new InvalidOperationException($"Error saving YAML file {yamlPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the SHA256 hash of a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The hash as a hex string.</returns>
    public string CalculateFileHash(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning($"File not found for hashing: {filePath}");
            return string.Empty;
        }

        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error calculating file hash: {ex.Message}");
            return string.Empty;
        }
    }
}