using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controllers
{
    public class SoundController
    {
        public static SoundController Instance { get; private set; } = new SoundController();

        private Dictionary<string, SoundEffectInstance> _songs = [];
        private SoundEffectInstance _activeGlobalSoundEffect;

        public void PlayBackgroundMusic(string path)
        {
            var fullPath = Path.Combine(Constants.DataPath, path);

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"File not found: {fullPath}");
                return;
            }

            if (!_songs.TryGetValue(path, out var music))
            {
                music = LoadMp3FromFile(fullPath);
                music.IsLooped = true;
                _songs.Add(path, music);
            }

            _activeGlobalSoundEffect?.Stop();
            _activeGlobalSoundEffect = music;

            music.Play();
        }


        private SoundEffectInstance LoadMp3FromFile(string filePath)
        {
            using (var mp3Reader = new Mp3FileReader(filePath))
            using (var waveStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader))
            using (var memoryStream = new MemoryStream())
            {
                waveStream.CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();

                var soundEffect = new SoundEffect(bytes, waveStream.WaveFormat.SampleRate, (AudioChannels)waveStream.WaveFormat.Channels);
                return soundEffect.CreateInstance();
            }
        }
    }
}
