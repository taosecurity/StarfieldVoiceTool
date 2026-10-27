using System.IO;

using Mutagen.Bethesda.Archives;

using Serilog;

using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.UI.Audio
{
    public class VoiceManager
    {
        private VoiceManager() { }
        private static readonly Lazy<VoiceManager> lazy = new Lazy<VoiceManager>(() => new VoiceManager());
        public static VoiceManager Instance => lazy.Value;

        private readonly object _lock = new();
        private string? _currentWavPath;

        /// <summary>
        /// Convert and play a voice file. Slow (decoding + ffmpeg), so call it off the UI thread.
        /// Throws if the file can't be converted.
        /// </summary>
        public void PlayVoiceLine(IArchiveFile voiceFile)
        {
            Log.Information("Playing voice file {Path}", voiceFile.Path);

            var wavPath = Path.Combine(AppDataFolder.GetTempDir(), $"playback-{Guid.NewGuid():N}.wav");
            AudioConverter.ConvertWemToWav(voiceFile.GetBytes(), wavPath);

            string? previousWavPath;
            lock (_lock)
            {
                previousWavPath = _currentWavPath;
                _currentWavPath = wavPath;
            }

            // stops (and releases) the previous sound, so its file can be removed afterwards
            AudioOutputManager.Instance.PlaySound(wavPath);
            if (previousWavPath != null)
            {
                AudioConverter.TryDelete(previousWavPath);
            }
        }
    }
}
