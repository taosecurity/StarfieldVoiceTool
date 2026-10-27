using System.Runtime.InteropServices;

using LibVLCSharp.Shared;

using Serilog;

namespace StarfieldVT.UI.Audio;

public class AudioOutputManager
{
    private readonly object _lock = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;

    private AudioOutputManager()
    {
        if (OperatingSystem.IsLinux())
        {
            UseSystemLibVlcOnLinux();
        }

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
        }
        catch (Exception e) when (OperatingSystem.IsLinux() && e is DllNotFoundException or VLCException)
        {
            throw new InvalidOperationException(
                "Playback needs VLC. On Debian or Ubuntu: sudo apt install libvlc5 vlc-plugin-base", e);
        }
    }

    /// <summary>
    /// LibVLCSharp imports "libvlc", which resolves to libvlc.so, but distributions only ship that
    /// unversioned name in their -dev packages; the runtime package (Debian/Ubuntu libvlc5) provides
    /// libvlc.so.5. Load that instead, falling back to the default lookup.
    /// </summary>
    private static void UseSystemLibVlcOnLinux()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, _, _) =>
                name == "libvlc" && NativeLibrary.TryLoad("libvlc.so.5", out var handle) ? handle : IntPtr.Zero);
        }
        catch (InvalidOperationException e)
        {
            // a resolver is already registered for LibVLCSharp; keep it
            Log.Debug(e, "LibVLCSharp already has a native library resolver");
        }
    }

    private static readonly Lazy<AudioOutputManager> LazyInstance = new(() => new AudioOutputManager());

    public static AudioOutputManager Instance => LazyInstance.Value;

    public void PlaySound(string soundPath)
    {
        lock (_lock)
        {
            ReleasePlayer();

            // A fresh LibVLC instance per sound (about 3 ms). One long-lived instance keeps its audio
            // output after the first stream, and on WSLg's PulseAudio every later stream through that
            // kept output ended at once without sound: measured 1 of 12 short lines played with one
            // instance, 8 of 8 with an instance per line.
            //
            // The player is kept until the next sound starts or the app exits. It must not be disposed
            // on EndReached: that fires when VLC has finished decoding, while the audio output may
            // still be playing buffered audio (high-latency outputs like WSLg or Bluetooth).
            _libVlc = new LibVLC("--no-video");
            _mediaPlayer = new MediaPlayer(_libVlc);

            using var media = new Media(_libVlc, soundPath, FromType.FromPath);
            _mediaPlayer.Play(media);
        }
    }

    private void ReleasePlayer()
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _libVlc?.Dispose();
        _libVlc = null;
    }

    public void StopSound()
    {
        lock (_lock)
        {
            // not just when IsPlaying: after decoding ends, buffered audio may still be audible
            _mediaPlayer?.Stop();
        }
    }
}
