using System.IO;

using BnkExtractor;

using Serilog;

using StarfieldVT.Core.Audio;
using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.UI.Audio;

public static class AudioConverter
{
    /// <summary>
    /// Convert Wwise .wem bytes to a .wav at <paramref name="wavPath"/>: ww2ogg turns the .wem into
    /// Ogg Vorbis, then NVorbis decodes it to PCM. The intermediate .wem and .ogg files are written
    /// to the app's temp folder and always cleaned up.
    /// </summary>
    public static void ConvertWemToWav(byte[] wemBytes, string wavPath)
    {
        var tempDir = AppDataFolder.GetTempDir();
        Directory.CreateDirectory(tempDir);
        var tempWemPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.wem");
        var tempOggPath = Path.ChangeExtension(tempWemPath, "ogg");

        try
        {
            File.WriteAllBytes(tempWemPath, wemBytes);
            Extractor.ConvertWem(tempWemPath);
            // BnkExtractor's ww2ogg port opens the .wem and never closes it; run finalizers so the
            // handle is released and the temp file can be deleted now rather than at the next start
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // ww2ogg logs parse errors instead of throwing, and then writes no .ogg
            if (!File.Exists(tempOggPath))
            {
                throw new InvalidDataException("This voice file couldn't be converted (unsupported or corrupt .wem).");
            }

            var directoryName = Path.GetDirectoryName(Path.GetFullPath(wavPath));
            if (directoryName != null)
            {
                Directory.CreateDirectory(directoryName);
            }

            Log.Information("Writing converted wem file to wav file at {WavPath}", wavPath);
            OggToWav.Convert(tempOggPath, wavPath);
        }
        catch (Exception e)
        {
            Log.Error(e, "There was an error converting the wem file to {WavPath}", wavPath);
            TryDelete(wavPath);
            throw;
        }
        finally
        {
            TryDelete(tempWemPath);
            TryDelete(tempOggPath);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Debug(e, "Could not delete temp file {Path}", path);
        }
    }

    /// <summary>Remove leftovers from previous sessions (e.g. playback files from a crash).</summary>
    public static void CleanTempDir()
    {
        var tempDir = AppDataFolder.GetTempDir();
        if (!Directory.Exists(tempDir)) return;

        foreach (var file in Directory.EnumerateFiles(tempDir))
        {
            TryDelete(file);
        }
    }
}
