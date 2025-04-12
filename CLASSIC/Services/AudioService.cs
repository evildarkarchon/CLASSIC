using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using ReactiveUI;

namespace CLASSIC.Services;

/// <summary>
/// Service for playing audio notifications in the application using NAudio.
/// </summary>
public class AudioService : ReactiveObject, IDisposable
{
    private readonly ConfigurationService _config;
    private readonly LoggingService _logger;
    private bool _audioEnabled;
    private readonly Dictionary<string, CachedSound> _cachedSounds = new();
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets whether audio notifications are enabled.
    /// </summary>
    public bool AudioEnabled
    {
        get => _audioEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _audioEnabled, value);
            _config.SetSetting(YamlStore.Settings, "CLASSIC_Settings.Audio Notifications", value);
        }
    }

    /// <summary>
    /// Initializes a new instance of the AudioService class.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="logger">The logging service.</param>
    public AudioService(ConfigurationService config, LoggingService logger)
    {
        _config = config;
        _logger = logger;

        // Load audio settings from configuration
        _audioEnabled = _config.GetSetting(YamlStore.Settings, "CLASSIC_Settings.Audio Notifications", true);

        // Pre-cache standard sounds
        PreloadSounds();

        _logger.Debug("Audio service initialized");
    }

    private void PreloadSounds()
    {
        try
        {
            var errorSoundPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "CLASSIC Data/sounds/classic_error.wav");

            var notifySoundPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "CLASSIC Data/sounds/classic_notify.wav");

            if (File.Exists(errorSoundPath))
            {
                _cachedSounds["error"] = new CachedSound(errorSoundPath);
                _logger.Debug("Error sound loaded");
            }
            else
            {
                _logger.Warning($"Error sound file not found: {errorSoundPath}");
            }

            if (File.Exists(notifySoundPath))
            {
                _cachedSounds["notify"] = new CachedSound(notifySoundPath);
                _logger.Debug("Notification sound loaded");
            }
            else
            {
                _logger.Warning($"Notification sound file not found: {notifySoundPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error preloading sounds: {ex.Message}");
        }
    }

    /// <summary>
    /// Plays the error sound if audio is enabled and the sound file is available.
    /// </summary>
    public void PlayErrorSound()
    {
        if (_audioEnabled && _cachedSounds.TryGetValue("error", out var sound))
        {
            PlaySound(sound, 0.5f);
        }
    }

    /// <summary>
    /// Plays the notification sound if audio is enabled and the sound file is available.
    /// </summary>
    public void PlayNotifySound()
    {
        if (_audioEnabled && _cachedSounds.TryGetValue("notify", out var sound))
        {
            PlaySound(sound, 0.5f);
        }
    }

    /// <summary>
    /// Plays a custom sound file at the specified volume if audio is enabled.
    /// </summary>
    /// <param name="soundPath">The path to the sound file.</param>
    /// <param name="volume">The volume level (0.0 to 1.0).</param>
    public void PlayCustomSound(string soundPath, float volume = 1.0f)
    {
        if (!_audioEnabled || !File.Exists(soundPath))
            return;

        try
        {
            // For one-off sounds, don't cache them
            var sound = new CachedSound(soundPath);
            PlaySound(sound, volume);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error playing custom sound: {ex.Message}");
        }
    }

    private void PlaySound(CachedSound sound, float volume)
    {
        try
        {
            var player = new CachedSoundSampleProvider(sound)
            {
                Volume = volume
            };

            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(player);
            outputDevice.Play();

            // Since WaveOutEvent is IDisposable, we need to keep it around until playback finishes
            var localDevice = outputDevice;
            outputDevice.PlaybackStopped += (sender, args) => { localDevice.Dispose(); };
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during sound playback: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            foreach (var sound in _cachedSounds.Values)
            {
                sound.Dispose();
            }

            _cachedSounds.Clear();
        }

        _isDisposed = true;
    }
}

/// <summary>
/// Represents a preloaded sound in memory for efficient playback.
/// </summary>
public class CachedSound : IDisposable
{
    public float[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }
    private bool _disposed;

    public CachedSound(string audioFileName)
    {
        using var audioFileReader = new AudioFileReader(audioFileName);
        WaveFormat = audioFileReader.WaveFormat;
        var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
        var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
        int samplesRead;
        while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            wholeFile.AddRange(readBuffer.Take(samplesRead));
        }

        AudioData = wholeFile.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        // Release the audio data
        AudioData = null!;
        _disposed = true;
    }
}

/// <summary>
/// Provides a sample provider for cached sounds.
/// </summary>
public class CachedSoundSampleProvider(CachedSound cachedSound) : ISampleProvider
{
    private int _position;
    private float _volume = 1.0f;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public WaveFormat WaveFormat => cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = cachedSound.AudioData.Length - _position;
        var samplesToCopy = Math.Min(availableSamples, count);

        if (samplesToCopy > 0)
        {
            // Copy and apply volume
            if (Math.Abs(_volume - 1.0f) < 0.001f)
            {
                // No volume adjustment needed
                Array.Copy(cachedSound.AudioData, _position, buffer, offset, samplesToCopy);
            }
            else
            {
                // Apply volume adjustment
                for (var i = 0; i < samplesToCopy; i++)
                {
                    buffer[offset + i] = cachedSound.AudioData[_position + i] * _volume;
                }
            }

            _position += samplesToCopy;
        }

        return samplesToCopy;
    }
}