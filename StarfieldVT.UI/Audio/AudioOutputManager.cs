using LibVLCSharp.Shared;

using Serilog;

namespace StarfieldVT.UI.Audio;

public class AudioOutputManager
{
    private readonly LibVLC _libVlc;
    private readonly object _lock = new();
    private MediaPlayer? _mediaPlayer;

    private AudioOutputManager()
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--no-video");
    }

    private static readonly Lazy<AudioOutputManager> LazyInstance = new(() => new AudioOutputManager());

    public static AudioOutputManager Instance => LazyInstance.Value;

    public void PlaySound(string soundPath)
    {
        lock (_lock)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            var player = new MediaPlayer(_libVlc);
            player.EndReached += (_, _) =>
            {
                Log.Debug("Playback ended, disposing media player");
                // Must be dispatched - cannot dispose from event handler thread.
                // Only dispose this player if it is still the current one; if a newer sound has started,
                // PlaySound already disposed it and the field now holds the new player.
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    lock (_lock)
                    {
                        if (!ReferenceEquals(_mediaPlayer, player)) return;
                        _mediaPlayer = null;
                    }

                    player.Dispose();
                });
            };
            _mediaPlayer = player;

            using var media = new Media(_libVlc, soundPath, FromType.FromPath);
            player.Play(media);
        }
    }

    public void StopSound()
    {
        lock (_lock)
        {
            if (_mediaPlayer is { IsPlaying: true })
            {
                _mediaPlayer.Stop();
            }
        }
    }
}
