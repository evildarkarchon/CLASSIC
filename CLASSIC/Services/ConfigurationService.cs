using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using YamlDotNet.RepresentationModel;
        using YamlDotNet.Serialization;
        using YamlDotNet.Serialization.NamingConventions;
        
        namespace CLASSIC.Services
        {
            public class ConfigurationService
            {
                private readonly Dictionary<string, Dictionary<string, object>> _yamlCache = new();
                private readonly Dictionary<string, DateTime> _fileModTimes = new();
                private readonly Logger _logger;
        
                public GameId GameId { get; private set; } = GameId.Fallout4;
                public string VRSuffix { get; private set; } = string.Empty;
        
                public ConfigurationService(Logger logger)
                {
                    _logger = logger;
        
                    // Initialize settings
                    EnsureSettingsExist();
        
                    // Set VR mode based on settings
                    var vrMode = GetSetting<bool>(YamlStore.Settings, "CLASSIC_Settings.VR Mode");
                    VRSuffix = vrMode ? "VR" : string.Empty;
                }
        
                private void EnsureSettingsExist()
                {
                    var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");
        
                    if (!File.Exists(settingsPath))
                    {
                        var defaultSettings = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_settings");
        
                        if (string.IsNullOrEmpty(defaultSettings))
                            throw new InvalidOperationException("Invalid Default Settings in 'CLASSIC Main.yaml'");
        
                        File.WriteAllText(settingsPath, defaultSettings);
                    }
                }
        
                public void GenerateRequiredFiles()
                {
                    GenerateIgnoreFile();
                    GenerateLocalYamlFile();
                }
        
                private void GenerateIgnoreFile()
                {
                    var ignorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Ignore.yaml");
        
                    if (!File.Exists(ignorePath))
                    {
                        var defaultIgnoreFile = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_ignorefile");
        
                        if (string.IsNullOrEmpty(defaultIgnoreFile))
                            throw new InvalidOperationException("Invalid Default Ignore file in 'CLASSIC Main.yaml'");
        
                        File.WriteAllText(ignorePath, defaultIgnoreFile);
                    }
                }
        
                private void GenerateLocalYamlFile()
                {
                    var localPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        $"CLASSIC Data/CLASSIC {GameId}{VRSuffix} Local.yaml");
        
                    if (!File.Exists(localPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath));
        
                        var defaultLocalYaml = GetSetting<string>(YamlStore.Main, "CLASSIC_Info.default_localyaml");
        
                        if (string.IsNullOrEmpty(defaultLocalYaml))
                            throw new InvalidOperationException("Invalid Default Local YAML in 'CLASSIC Main.yaml'");
        
                        File.WriteAllText(localPath, defaultLocalYaml);
                    }
                }
        
                public T GetSetting<T>(YamlStore store, string keyPath, T defaultValue = default)
                {
                    try
                    {
                        var yamlPath = GetYamlPath(store);
                        var data = LoadYaml(yamlPath);
        
                        var keys = keyPath.Split('.');
                        object currentValue = data;
        
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
        
                        if (currentValue is T typedValue)
                        {
                            return typedValue;
                        }
        
                        try
                        {
                            // Try conversion for common types
                            if (typeof(T) == typeof(bool) && currentValue != null)
                                return (T)(object)Convert.ToBoolean(currentValue);
        
                            if (typeof(T) == typeof(int) && currentValue != null)
                                return (T)(object)Convert.ToInt32(currentValue);
        
                            if (typeof(T) == typeof(string) && currentValue != null)
                                return (T)(object)currentValue.ToString();
        
                            if (typeof(T) == typeof(DirectoryInfo) && currentValue is string path)
                                return (T)(object)new DirectoryInfo(path);
        
                            if (typeof(T) == typeof(FileInfo) && currentValue is string filePath)
                                return (T)(object)new FileInfo(filePath);
                        }
                        catch
                        {
                            _logger.Error($"Error converting value for path {keyPath}");
                            return defaultValue;
                        }
        
                        return defaultValue;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error getting setting {keyPath}: {ex.Message}");
                        return defaultValue;
                    }
                }
        
                public void SetSetting<T>(YamlStore store, string keyPath, T value)
                {
                    try
                    {
                        var yamlPath = GetYamlPath(store);
                        var data = LoadYaml(yamlPath);
        
                        var keys = keyPath.Split('.');
                        Dictionary<string, object> currentDict = data;
        
                        // Navigate to final container
                        for (int i = 0; i < keys.Length - 1; i++)
                        {
                            var key = keys[i];
        
                            if (!currentDict.TryGetValue(key, out var nextObj) || !(nextObj is Dictionary<string, object>))
                            {
                                var newDict = new Dictionary<string, object>();
                                currentDict[key] = newDict;
                                currentDict = newDict;
                            }
                            else
                            {
                                currentDict = (Dictionary<string, object>)nextObj;
                            }
                        }
        
                        // Set value
                        currentDict[keys.Last()] = value;
        
                        // Save YAML
                        SaveYaml(yamlPath, data);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error setting {keyPath}: {ex.Message}");
                    }
                }
        
                private string GetYamlPath(YamlStore store)
                {
                    return store switch
                    {
                        YamlStore.Main => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data/databases/CLASSIC Main.yaml"),
                        YamlStore.Settings => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml"),
                        YamlStore.Ignore => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Ignore.yaml"),
                        YamlStore.Game => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"CLASSIC Data/databases/CLASSIC {GameId}.yaml"),
                        YamlStore.GameLocal => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"CLASSIC Data/CLASSIC {GameId}{VRSuffix} Local.yaml"),
                        YamlStore.Test => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests/test_settings.yaml"),
                        _ => throw new NotImplementedException($"YAML store {store} not implemented")
                    };
                }
        
                private Dictionary<string, object> LoadYaml(string yamlPath)
                {
                    if (!File.Exists(yamlPath))
                        return new Dictionary<string, object>();
        
                    var lastWriteTime = File.GetLastWriteTime(yamlPath);
        
                    // Check if file has been modified since last load
                    if (_yamlCache.TryGetValue(yamlPath, out var cachedYaml) && 
                        _fileModTimes.TryGetValue(yamlPath, out var cachedTime) &&
                        cachedTime == lastWriteTime)
                    {
                        return cachedYaml;
                    }
        
                    // Load and deserialize YAML
                    using var reader = new StreamReader(yamlPath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
        
                    var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(reader);
        
                    // Update cache
                    _yamlCache[yamlPath] = yamlObject;
                    _fileModTimes[yamlPath] = lastWriteTime;
        
                    return yamlObject;
                }
        
                private void SaveYaml(string yamlPath, Dictionary<string, object> data)
                {
                    var serializer = new SerializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
        
                    using var writer = new StreamWriter(yamlPath);
                    serializer.Serialize(writer, data);
        
                    // Update cache
                    _yamlCache[yamlPath] = data;
                    _fileModTimes[yamlPath] = File.GetLastWriteTime(yamlPath);
                }
            }
        }