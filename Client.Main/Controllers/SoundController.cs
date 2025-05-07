﻿// File: SoundController.cs
using Microsoft.Xna.Framework.Audio;
using NLayer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;

namespace Client.Main.Controllers
{
    public class SoundController : IDisposable
    {
        public static SoundController Instance { get; private set; } = new SoundController();

        // Cache for loaded SoundEffects (raw audio data)
        private Dictionary<string, SoundEffect> _soundEffectCache = new Dictionary<string, SoundEffect>();

        // For background music which is looped and only one can play at a time
        private SoundEffectInstance _activeBackgroundMusicInstance;
        private string _currentBackgroundMusicPath;

        private HashSet<string> _failedPaths = new HashSet<string>();

        public void StopBackgroundMusic()
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

            StopBackgroundMusic();

            SoundEffect musicEffect = LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
            if (musicEffect != null)
            {
                _activeBackgroundMusicInstance = musicEffect.CreateInstance();
                _activeBackgroundMusicInstance.IsLooped = true;
                try
                {
                    _activeBackgroundMusicInstance.Play();
                    _currentBackgroundMusicPath = relativePath;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlayBackgroundMusic] Error playing sound '{relativePath}': {ex.Message}");
                    _activeBackgroundMusicInstance.Dispose();
                    _activeBackgroundMusicInstance = null;
                    _currentBackgroundMusicPath = null;
                }
            }
            else
            {
                Debug.WriteLine($"[PlayBackgroundMusic] Failed to load SoundEffect for: {relativePath}");
            }
        }

        public void PreloadBackgroundMusic(string relativePath)
        {
            if (!Constants.BACKGROUND_MUSIC || string.IsNullOrEmpty(relativePath))
                return;

            LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
        }

        /// <summary>
        /// Plays a one-shot sound effect with volume attenuation based on distance.
        /// Uses SoundEffect.Play() for fire-and-forget behavior.
        /// </summary>
        public void PlayBufferWithAttenuation(string relativePath, Vector3 sourcePosition, Vector3 listenerPosition, float maxDistance = 1000f)
        {
            if (!Constants.SOUND_EFFECTS) return;

            SoundEffect sfx = LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
            if (sfx == null || sfx.IsDisposed)
            {
                return;
            }

            float distance = Vector3.Distance(sourcePosition, listenerPosition);
            float volume = 1.0f - (distance / maxDistance);
            volume = MathHelper.Clamp(volume, 0f, 1f);

            if (volume > 0.01f)
            {
                try
                {
                    sfx.Play(volume, 0.0f, 0.0f); // pitch = 0.0f (normal), pan = 0.0f (center)
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlayEffectWithAttenuation] Error playing sound '{relativePath}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Plays a one-shot sound effect at full volume.
        /// Uses SoundEffect.Play() for fire-and-forget behavior.
        /// </summary>
        public void PlayBuffer(string relativePath)
        {
            if (!Constants.SOUND_EFFECTS) return;

            SoundEffect sfx = LoadSoundEffectData(Path.Combine(Constants.DataPath, relativePath));
            if (sfx == null || sfx.IsDisposed)
                return;

            try
            {
                sfx.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlayEffect] Error playing sound '{relativePath}': {ex.Message}");
            }
        }

        /*
        GetOrCreateSoundInstance is no longer needed for effects if using SoundEffect.Play().
        Leaving it commented in case you want to return to instance-based sound management.
        If not, this can be removed or modified to return just the SoundEffect.
        */

        private SoundEffect LoadSoundEffectData(string fullPath)
        {
            string cacheKey = fullPath.ToLowerInvariant();
            if (_soundEffectCache.TryGetValue(cacheKey, out SoundEffect sfx))
            {
                if (sfx != null && !sfx.IsDisposed)
                    return sfx;
                else
                    _soundEffectCache.Remove(cacheKey);
            }

            if (_failedPaths.Contains(cacheKey)) return null;

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"[LoadSoundEffectData] File not found: {fullPath}");
                _failedPaths.Add(cacheKey);
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
                        Debug.WriteLine($"[LoadSoundEffectData] Failed to load PCM data from MP3: {fullPath}");
                        _failedPaths.Add(cacheKey);
                    }
                }
                else
                {
                    Debug.WriteLine($"[LoadSoundEffectData] Unsupported audio file extension: {extension} for path {fullPath}");
                    _failedPaths.Add(cacheKey);
                    return null;
                }

                if (loadedSfx != null)
                {
                    _soundEffectCache[cacheKey] = loadedSfx;
                }
                return loadedSfx;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadSoundEffectData] Error loading sound data from '{fullPath}': {ex.Message}");
                _failedPaths.Add(cacheKey);
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
                        Debug.WriteLine($"[LoadMp3PcmData] Invalid MP3 header or empty file: {filePath}. SampleRate: {mpegFile.SampleRate}, Channels: {mpegFile.Channels}, Length: {mpegFile.Length}");
                        return null;
                    }
                    sampleRate = mpegFile.SampleRate;
                    channels = mpegFile.Channels;

                    if (mpegFile.Length <= 0)
                    {
                        Debug.WriteLine($"[LoadMp3PcmData] MP3 file has zero length (no samples): {filePath}");
                        return null;
                    }

                    List<byte> pcmList = new List<byte>();
                    float[] floatBuffer = new float[1152 * channels * 2]; // Slightly larger buffer for safety

                    int samplesReadFromFrame;
                    while ((samplesReadFromFrame = mpegFile.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
                    {
                        for (int i = 0; i < samplesReadFromFrame; i++)
                        {
                            short s = (short)(Math.Clamp(floatBuffer[i], -1.0f, 1.0f) * short.MaxValue);
                            pcmList.Add((byte)(s & 0xFF));
                            pcmList.Add((byte)((s >> 8) & 0xFF));
                        }
                    }

                    if (pcmList.Count == 0)
                    {
                        Debug.WriteLine($"[LoadMp3PcmData] No PCM data generated from MP3: {filePath}");
                        return null;
                    }

                    return pcmList.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadMp3PcmData] Error loading MP3 file '{filePath}': {ex.Message}");
                return null;
            }
        }

        public void ClearSoundCaches()
        {
            StopBackgroundMusic();

            /*
            _soundInstances is no longer used for effects in this approach.
            If you still use it for other cases, you can keep the cleanup.
            Commented out for now since effects use SoundEffect.Play().
            */

            foreach (var sfx in _soundEffectCache.Values)
            {
                sfx?.Dispose();
            }
            _soundEffectCache.Clear();

            _failedPaths.Clear();
            Debug.WriteLine("SoundController: SoundEffect cache cleared.");
        }

        public void Dispose()
        {
            ClearSoundCaches();
        }
    }
}
