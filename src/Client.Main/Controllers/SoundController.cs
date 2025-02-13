using Microsoft.Xna.Framework.Audio;
using NLayer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Client.Main.Controllers
{
    public class SoundController
    {
        public static SoundController Instance { get; private set; } = new SoundController();

        private Dictionary<string, SoundEffectInstance> _songs = new Dictionary<string, SoundEffectInstance>();
        private SoundEffectInstance _activeGlobalSoundEffect;
        private HashSet<string> _failedPaths = new HashSet<string>();

        public void StopBackgroundMusic()
        {
            _activeGlobalSoundEffect?.Stop();
            _activeGlobalSoundEffect = null;
        }

        public void PlayBackgroundMusic(string path)
        {
            if (!Constants.BACKGROUND_MUSIC)
                return;

            if (string.IsNullOrEmpty(path))
            {
                StopBackgroundMusic();
                return;
            }

            var fullPath = Path.Combine(Constants.DataPath, path);

            if (!File.Exists(fullPath))
            {
                StopBackgroundMusic();
                Debug.WriteLine($"File not found: {fullPath}");
                return;
            }

            if (_failedPaths.Contains(fullPath))
                return;

            if (!_songs.TryGetValue(path, out var music))
            {
                music = LoadSoundFromFile(fullPath);
                if (music == null)
                {
                    _failedPaths.Add(fullPath);
                    return;
                }
                music.IsLooped = true;
                _songs.Add(path, music);
            }

            StopBackgroundMusic();
            _activeGlobalSoundEffect = music;
            music.Play();
        }
        
        /// <summary>
        /// Plays a sound while adjusting the volume based on the distance
        /// between the sound source and the listener.
        /// </summary>
        /// <param name="path">Path to the sound file.</param>
        /// <param name="sourcePosition">Position of the sound source (e.g., an object in the game).</param>
        /// <param name="listenerPosition">Position of the listener (e.g., the camera).</param>
        public void PlayBufferWithAttenuation(string path, Microsoft.Xna.Framework.Vector3 sourcePosition, Microsoft.Xna.Framework.Vector3 listenerPosition)
        {
            if (!Constants.SOUND_EFFECTS)
                return;

            if (!_songs.TryGetValue(path, out var soundEffectInstance))
            {
                var fullPath = Path.Combine(Constants.DataPath, path);
                if (!File.Exists(fullPath))
                {
                    Debug.WriteLine($"File not found: {fullPath}");
                    return;
                }

                try
                {
                    var soundEffect = LoadSoundFromFile(fullPath);
                    if (soundEffect == null)
                        return;

                    soundEffectInstance = soundEffect;
                    _songs.Add(path, soundEffectInstance);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading sound effect from file: {ex.Message}");
                    return;
                }
            }

            float distance = Microsoft.Xna.Framework.Vector3.Distance(sourcePosition, listenerPosition);

            float maxDistance = 1000f;

            float volume = 1.0f - (distance / maxDistance);
            volume = Microsoft.Xna.Framework.MathHelper.Clamp(volume, 0f, 1f);

            soundEffectInstance.Volume = volume;
            soundEffectInstance.Play();
        }

        public void PlayBuffer(string path)
        {
            if (!Constants.SOUND_EFFECTS)
                return;

            if (!_songs.TryGetValue(path, out var soundEffectInstance))
            {
                var fullPath = Path.Combine(Constants.DataPath, path);
                if (!File.Exists(fullPath))
                {
                    Debug.WriteLine($"File not found: {fullPath}");
                    return;
                }

                try
                {
                    var soundEffect = LoadSoundFromFile(fullPath);
                    if (soundEffect == null)
                        return;

                    soundEffectInstance = soundEffect;
                    _songs.Add(path, soundEffectInstance);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading sound effect from file: {ex.Message}");
                    return;
                }
            }

            soundEffectInstance.Play();
        }

        /// <summary>
        /// Loads an audio file (MP3 or WAV) based on its extension.
        /// For WAV, it uses SoundEffect.FromFile, and for MP3, the LoadMp3FromFile method.
        /// </summary>
        /// <param name="filePath">Path to the audio file</param>
        /// <returns>SoundEffectInstance or null if an error occurred</returns>
        private SoundEffectInstance LoadSoundFromFile(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (extension == ".wav")
            {
                try
                {
                    var soundEffect = SoundEffect.FromFile(filePath);
                    return soundEffect.CreateInstance();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading WAV file: {ex.Message}");
                    return null;
                }
            }
            else if (extension == ".mp3")
            {
                return LoadMp3FromFile(filePath);
            }
            else
            {
                Debug.WriteLine($"Unsupported audio file extension: {extension}");
                return null;
            }
        }

        /// <summary>
        /// Loads an MP3 file, decodes it using NLayer, and converts float samples to 16-bit PCM.
        /// Then creates a SoundEffectInstance.
        /// </summary>
        /// <param name="filePath">Path to the MP3 file</param>
        /// <returns>SoundEffectInstance or null if an error occurred</returns>
        private SoundEffectInstance LoadMp3FromFile(string filePath)
        {
            try
            {
                int sampleRate, channels;
                byte[] pcmData;

                using (var fs = File.OpenRead(filePath))
                {
                    using (var mpegFile = new MpegFile(fs))
                    {
                        sampleRate = mpegFile.SampleRate;
                        channels = mpegFile.Channels;

                        // Determine the number of samples to read.
                        // MpegFile.Length returns the number of samples (for each channel).
                        int totalSamples = (int)mpegFile.Length;
                        float[] floatBuffer = new float[totalSamples];
                        int samplesRead = mpegFile.ReadSamples(floatBuffer, 0, totalSamples);

                        // Convert float samples to 16-bit PCM (2 bytes per sample)
                        pcmData = new byte[samplesRead * 2];
                        for (int i = 0; i < samplesRead; i++)
                        {
                            // Clamp the value to avoid exceeding the range
                            float sample = floatBuffer[i];
                            if (sample > 1.0f) sample = 1.0f;
                            if (sample < -1.0f) sample = -1.0f;
                            // Scale to the short range
                            short s = (short)(sample * short.MaxValue);
                            // Write in Little Endian format
                            pcmData[i * 2] = (byte)(s & 0xFF);
                            pcmData[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                        }
                    }
                }

                // Create a SoundEffect object. The constructor expects 16-bit PCM data.
                var soundEffect = new SoundEffect(pcmData, sampleRate, (AudioChannels)channels);
                return soundEffect.CreateInstance();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading MP3 file: {ex.Message}");
                return null;
            }
        }
    }
}
