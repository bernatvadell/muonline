// File: SoundController.cs
using Microsoft.Xna.Framework.Audio;
using NLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;

namespace Client.Main.Controllers
{
    public class SoundController : IDisposable
    {
        public static SoundController Instance { get; private set; } = new SoundController();

        private Dictionary<string, SoundEffect> _soundEffectCache = new Dictionary<string, SoundEffect>();
        private SoundEffectInstance _activeBackgroundMusicInstance;
        private string _currentBackgroundMusicPath;
        private SoundEffectInstance _activeAmbientSoundInstance;
        private string _currentAmbientSoundPath;
        private HashSet<string> _failedPaths = new HashSet<string>();
        private readonly Dictionary<string, Task<SoundEffect>> _soundEffectLoadTasks = new Dictionary<string, Task<SoundEffect>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _soundCacheLock = new object();
        private int _backgroundMusicRequestId;

        private sealed class ManagedLoopData
        {
            public SoundEffectInstance Instance;
            public float BaseVolume;
        }

        private readonly Dictionary<string, ManagedLoopData> _managedLoopingInstances = new Dictionary<string, ManagedLoopData>();
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<SoundController>();

        private SoundController()
        {
            SoundEffect.MasterVolume = MathHelper.Clamp(Constants.SOUND_EFFECTS_VOLUME / 100f, 0f, 1f);
        }

        public void StopBackgroundMusic()
        {
            Interlocked.Increment(ref _backgroundMusicRequestId);
            StopBackgroundMusicInstance();
        }

        private void StopBackgroundMusicInstance()
        {
            _activeBackgroundMusicInstance?.Stop(true);
            _activeBackgroundMusicInstance?.Dispose();
            _activeBackgroundMusicInstance = null;
            _currentBackgroundMusicPath = null;
        }

        public void PlayBackgroundMusic(string relativePath)
        {
            if (!Constants.BACKGROUND_MUSIC || string.IsNullOrEmpty(relativePath))
            {
                StopBackgroundMusic();
                return;
            }

            if (_currentBackgroundMusicPath == relativePath &&
                _activeBackgroundMusicInstance != null &&
                !_activeBackgroundMusicInstance.IsDisposed &&
                _activeBackgroundMusicInstance.State == SoundState.Playing)
            {
                return;
            }

            var requestId = Interlocked.Increment(ref _backgroundMusicRequestId);
            StopBackgroundMusicInstance();

            _ = PlayBackgroundMusicAsync(relativePath, requestId);
        }

        public void PreloadBackgroundMusic(string relativePath)
        {
            if ((!Constants.BACKGROUND_MUSIC && !Constants.SOUND_EFFECTS) || string.IsNullOrEmpty(relativePath))
                return;

            _ = LoadSoundEffectDataAsync(Path.Combine(Constants.DataPath, relativePath));
        }

        public void StopAmbientSound()
        {
            _activeAmbientSoundInstance?.Stop(true);
            _activeAmbientSoundInstance?.Dispose();
            _activeAmbientSoundInstance = null;
            _currentAmbientSoundPath = null;
        }

        public void PlayAmbientSound(string relativePath)
        {
            if (!Constants.SOUND_EFFECTS || string.IsNullOrEmpty(relativePath))
            {
                StopAmbientSound();
                return;
            }

            if (_currentAmbientSoundPath == relativePath &&
                _activeAmbientSoundInstance != null &&
                !_activeAmbientSoundInstance.IsDisposed &&
                _activeAmbientSoundInstance.State == SoundState.Playing)
            {
                return;
            }

            StopAmbientSound();

            SoundEffect ambientEffect = LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
            if (ambientEffect != null)
            {
                _activeAmbientSoundInstance = ambientEffect.CreateInstance();
                _activeAmbientSoundInstance.IsLooped = true;
                _activeAmbientSoundInstance.Volume = 1f; // Lower volume for ambient sounds
                try
                {
                    _activeAmbientSoundInstance.Play();
                    _currentAmbientSoundPath = relativePath;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"[PlayAmbientSound] Error playing sound '{relativePath}': {ex.Message}");
                    _activeAmbientSoundInstance.Dispose();
                    _activeAmbientSoundInstance = null;
                    _currentAmbientSoundPath = null;
                }
            }
            else
            {
                _logger?.LogDebug($"[PlayAmbientSound] Failed to load SoundEffect for: {relativePath}");
            }
        }

        /// <summary>
        /// Preloads a sound effect into the cache to avoid loading delays during playback.
        /// This is a new, more generic method name.
        /// </summary>
        public void PreloadSound(string relativePath)
        {
            if ((!Constants.BACKGROUND_MUSIC && !Constants.SOUND_EFFECTS) || string.IsNullOrEmpty(relativePath))
                return;

            LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
        }

        /// <summary>
        /// Plays a sound effect with volume attenuation based on distance.
        /// Can be looped if 'loop' parameter is true.
        /// </summary>
        public void PlayBufferWithAttenuation(string relativePath, Vector3 sourcePosition, Vector3 listenerPosition, float maxDistance = 1000f, bool loop = false)
        {
            if (!Constants.SOUND_EFFECTS || string.IsNullOrEmpty(relativePath)) return;

            string fullPath = Path.Combine(Constants.DataPath, relativePath);
            float distance = Vector3.Distance(sourcePosition, listenerPosition);
            float volume = 1.0f - (distance / maxDistance);
            volume = MathHelper.Clamp(volume, 0f, 1f);

            if (loop)
            {
                string instanceKey = fullPath.ToLowerInvariant();
                if (!_managedLoopingInstances.TryGetValue(instanceKey, out var managed) || managed.Instance == null || managed.Instance.IsDisposed)
                {
                    SoundEffect sfxData = LoadSoundEffectData(fullPath);
                    if (sfxData == null || sfxData.IsDisposed)
                    {
                        _logger?.LogDebug($"[ManagedLoop] Failed to load SoundEffect data for: {relativePath}");
                        return;
                    }

                    var newInstance = sfxData.CreateInstance();
                    newInstance.IsLooped = true;
                    managed = new ManagedLoopData
                    {
                        Instance = newInstance,
                        BaseVolume = 0f
                    };
                    _managedLoopingInstances[instanceKey] = managed;
                }

                var instance = managed.Instance;
                float fxFactor = Constants.SOUND_EFFECTS_VOLUME / 100f;
                managed.BaseVolume = MathHelper.Clamp(volume, 0f, 1f);
                UpdateManagedLoopVolume(managed);

                if (managed.BaseVolume > 0.01f)
                {
                    if (instance.State != SoundState.Playing)
                    {
                        try
                        {
                            instance.Play();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug($"[ManagedLoop] Error playing instance of '{relativePath}': {ex.Message}");
                            instance.Dispose();
                            _managedLoopingInstances.Remove(instanceKey);
                        }
                    }
                    else
                    {
                        UpdateManagedLoopVolume(managed);
                    }
                }
                else if (instance.State == SoundState.Playing)
                {
                    instance.Pause();
                }
            }
            else
            {
                SoundEffect sfx = LoadSoundEffectData(fullPath); // LoadSoundEffectData używa cache
                if (sfx == null || sfx.IsDisposed)
                {
                    return;
                }

                float fxFactor = Constants.SOUND_EFFECTS_VOLUME / 100f;
                if (volume > 0.01f && fxFactor > 0f)
                {
                    try
                    {
                        var scaled = MathHelper.Clamp(volume * fxFactor, 0f, 1f);
                        sfx.Play(scaled, 0.0f, 0.0f);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug($"[PlayEffectWithAttenuation] Error playing sound '{relativePath}': {ex.Message}");
                    }
                }
            }
        }

        public void PlayBuffer(string relativePath)
        {
            if (!Constants.SOUND_EFFECTS) return;

            SoundEffect sfx = LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
            if (sfx == null || sfx.IsDisposed)
                return;

            try
            {
                var volume = MathHelper.Clamp(Constants.SOUND_EFFECTS_VOLUME / 100f, 0f, 1f);
                if (volume <= 0f)
                {
                    return;
                }
                sfx.Play(volume, 0.0f, 0.0f);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[PlayEffect] Error playing sound '{relativePath}': {ex.Message}");
            }
        }

        public void ApplyBackgroundMusicVolume()
        {
            if (_activeBackgroundMusicInstance != null && !_activeBackgroundMusicInstance.IsDisposed)
            {
                var rawVolume = Constants.BACKGROUND_MUSIC_VOLUME / 100f;
                if (rawVolume <= 0f)
                {
                    StopBackgroundMusic();
                    return;
                }

                var volume = MathHelper.Clamp(rawVolume, 0f, 1f);

                if (float.IsNaN(volume) || float.IsInfinity(volume))
                {
                    volume = 1f;
                }

                volume = Math.Max(0f, Math.Min(1f, volume));

                _activeBackgroundMusicInstance.Volume = volume;
            }
        }

        public void ApplySoundEffectsVolume()
        {
            float master = Constants.SOUND_EFFECTS ? MathHelper.Clamp(Constants.SOUND_EFFECTS_VOLUME / 100f, 0f, 1f) : 0f;
            SoundEffect.MasterVolume = master;
            foreach (var entry in _managedLoopingInstances.Values)
            {
                UpdateManagedLoopVolume(entry);
            }
        }

        private void UpdateManagedLoopVolume(ManagedLoopData managed)
        {
            if (managed?.Instance == null || managed.Instance.IsDisposed)
            {
                return;
            }

            if (!Constants.SOUND_EFFECTS)
            {
                managed.Instance.Volume = 0f;
                if (managed.Instance.State == SoundState.Playing)
                {
                    managed.Instance.Pause();
                }
                return;
            }

            float factor = Constants.SOUND_EFFECTS_VOLUME / 100f;
            var calculatedVolume = managed.BaseVolume * factor;
            managed.Instance.Volume = MathHelper.Clamp(calculatedVolume, 0f, 1f);
            if (managed.Instance.Volume <= 0f)
            {
                if (managed.Instance.State == SoundState.Playing)
                {
                    managed.Instance.Pause();
                }
            }
            else if (managed.Instance.State != SoundState.Playing)
            {
                try
                {
                    managed.Instance.Play();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"[ManagedLoop] Error resuming loop instance: {ex.Message}");
                }
            }
        }

        private SoundEffect LoadSoundEffectData(string fullPath)
        {
            string cacheKey = fullPath.ToLowerInvariant();
            var cached = GetCachedSoundEffect(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            if (IsFailedPath(cacheKey)) return null;

            if (!File.Exists(fullPath))
            {
                _logger?.LogDebug($"[LoadSoundEffectData] File not found: {fullPath}");
                MarkFailedPath(cacheKey);
                return null;
            }

            string extension = Path.GetExtension(fullPath)?.ToLowerInvariant();
            try
            {
                SoundEffect loadedSfx = null;
                if (extension == ".wav")
                {
                    loadedSfx = SoundEffect.FromFile(fullPath);
                }
                else if (extension == ".mp3")
                {
                    byte[] pcmData = LoadMp3PcmData(fullPath, out int sampleRate, out int channels);
                    if (pcmData != null && sampleRate > 0 && channels > 0 && pcmData.Length > 0)
                    {
                        loadedSfx = new SoundEffect(pcmData, sampleRate, (AudioChannels)channels);
                    }
                    else
                    {
                        _logger?.LogDebug($"[LoadSoundEffectData] Failed to load PCM data from MP3: {fullPath}");
                        MarkFailedPath(cacheKey);
                    }
                }
                else
                {
                    _logger?.LogDebug($"[LoadSoundEffectData] Unsupported audio file extension: {extension} for path {fullPath}");
                    MarkFailedPath(cacheKey);
                    return null;
                }

                if (loadedSfx != null)
                {
                    CacheSoundEffect(cacheKey, loadedSfx);
                }
                return loadedSfx;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[LoadSoundEffectData] Error loading sound data from '{fullPath}': {ex.Message}");
                MarkFailedPath(cacheKey);
            }
            return null;
        }

        private byte[] LoadMp3PcmData(string filePath, out int sampleRate, out int channels)
        {
            sampleRate = 0;
            channels = 0;
            try
            {
                using (var fs = File.OpenRead(filePath))
                using (var mpegFile = new MpegFile(fs))
                {
                    if (mpegFile.SampleRate == 0 || mpegFile.Channels == 0 || mpegFile.Length == 0)
                    {
                        _logger?.LogDebug($"[LoadMp3PcmData] Invalid MP3 header or empty file: {filePath}. SampleRate: {mpegFile.SampleRate}, Channels: {mpegFile.Channels}, Length: {mpegFile.Length}");
                        return null;
                    }
                    sampleRate = mpegFile.SampleRate;
                    channels = mpegFile.Channels;

                    if (mpegFile.Length <= 0)
                    {
                        _logger?.LogDebug($"[LoadMp3PcmData] MP3 file has zero length (no samples): {filePath}");
                        return null;
                    }

                    List<byte> pcmList = new List<byte>();
                    float[] floatBuffer = new float[1152 * channels * 2];

                    int samplesReadFromFrame;
                    while ((samplesReadFromFrame = mpegFile.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
                    {
                        for (int i = 0; i < samplesReadFromFrame; i++)
                        {
                            short s = (short)(MathHelper.Clamp(floatBuffer[i], -1.0f, 1.0f) * short.MaxValue);
                            pcmList.Add((byte)(s & 0xFF));
                            pcmList.Add((byte)((s >> 8) & 0xFF));
                        }
                    }

                    if (pcmList.Count == 0 && mpegFile.Length > 0)
                    {
                        _logger?.LogDebug($"[LoadMp3PcmData] No PCM data generated from MP3 despite non-zero length: {filePath}. Total Samples in file: {mpegFile.Length}");
                        return null;
                    }
                    else if (pcmList.Count == 0)
                    {
                        _logger?.LogDebug($"[LoadMp3PcmData] No PCM data generated from MP3: {filePath}");
                        return null;
                    }
                    return pcmList.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[LoadMp3PcmData] Error loading MP3 file '{filePath}': {ex.Message}");
                return null;
            }
        }

        public void ClearSoundCaches()
        {
            StopBackgroundMusic();

            List<SoundEffect> cached;
            lock (_soundCacheLock)
            {
                cached = new List<SoundEffect>(_soundEffectCache.Values);
                _soundEffectCache.Clear();
                _soundEffectLoadTasks.Clear();
                _failedPaths.Clear();
            }

            foreach (var sfx in cached)
            {
                sfx?.Dispose();
            }

            foreach (var instanceEntry in _managedLoopingInstances)
            {
                var managed = instanceEntry.Value;
                if (managed?.Instance != null)
                {
                    managed.Instance.Stop(true);
                    managed.Instance.Dispose();
                    managed.Instance = null;
                }
            }
            _managedLoopingInstances.Clear();

            _logger?.LogDebug("SoundController: SoundEffect cache and managed looping instances cleared.");
        }

        public void Dispose()
        {
            ClearSoundCaches();
        }

        private async Task PlayBackgroundMusicAsync(string relativePath, int requestId)
        {
            SoundEffect musicEffect = null;
            var fullPath = Path.Combine(Constants.DataPath, relativePath);
            try
            {
                musicEffect = await LoadSoundEffectDataAsync(fullPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[PlayBackgroundMusic] Error loading sound '{relativePath}': {ex.Message}");
            }

            if (musicEffect == null)
            {
                _logger?.LogDebug($"[PlayBackgroundMusic] Failed to load SoundEffect for: {relativePath}");
                return;
            }

            MuGame.ScheduleOnMainThread(() =>
            {
                if (requestId != _backgroundMusicRequestId || !Constants.BACKGROUND_MUSIC)
                {
                    return;
                }

                _activeBackgroundMusicInstance?.Stop(true);
                _activeBackgroundMusicInstance?.Dispose();
                _activeBackgroundMusicInstance = musicEffect.CreateInstance();
                _activeBackgroundMusicInstance.IsLooped = true;
                try
                {
                    ApplyBackgroundMusicVolume();
                    _activeBackgroundMusicInstance.Play();
                    _currentBackgroundMusicPath = relativePath;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"[PlayBackgroundMusic] Error playing sound '{relativePath}': {ex.Message}");
                    StopBackgroundMusicInstance();
                }
            });
        }

        private Task<SoundEffect> LoadSoundEffectDataAsync(string fullPath)
        {
            string cacheKey = fullPath.ToLowerInvariant();

            var cached = GetCachedSoundEffect(cacheKey);
            if (cached != null)
            {
                return Task.FromResult(cached);
            }

            if (IsFailedPath(cacheKey))
            {
                return Task.FromResult<SoundEffect>(null);
            }

            lock (_soundCacheLock)
            {
                if (_soundEffectLoadTasks.TryGetValue(cacheKey, out var existingTask))
                {
                    return existingTask;
                }

                var task = Task.Run(() =>
                {
                    try
                    {
                        return LoadSoundEffectData(fullPath);
                    }
                    finally
                    {
                        lock (_soundCacheLock)
                        {
                            _soundEffectLoadTasks.Remove(cacheKey);
                        }
                    }
                });

                _soundEffectLoadTasks[cacheKey] = task;
                return task;
            }
        }

        private SoundEffect GetCachedSoundEffect(string cacheKey)
        {
            lock (_soundCacheLock)
            {
                if (_soundEffectCache.TryGetValue(cacheKey, out var sfx))
                {
                    if (sfx != null && !sfx.IsDisposed)
                    {
                        return sfx;
                    }
                    _soundEffectCache.Remove(cacheKey);
                }
            }

            return null;
        }

        private void CacheSoundEffect(string cacheKey, SoundEffect effect)
        {
            lock (_soundCacheLock)
            {
                _soundEffectCache[cacheKey] = effect;
            }
        }

        private bool IsFailedPath(string cacheKey)
        {
            lock (_soundCacheLock)
            {
                return _failedPaths.Contains(cacheKey);
            }
        }

        private void MarkFailedPath(string cacheKey)
        {
            lock (_soundCacheLock)
            {
                _failedPaths.Add(cacheKey);
            }
        }
    }
}
