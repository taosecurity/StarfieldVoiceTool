using System.IO;

using BnkExtractor;

using Serilog;

using StarfieldVT.Core.Audio;
using StarfieldVT.Core.Export;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Sounds;

namespace StarfieldVT.UI.Audio;

public static class AudioConverter
{
    /// <summary>
    /// Send ww2ogg's (BnkExtractor's) log events to Serilog through ConverterLog. Its own default
    /// handlers print every trace line to the console (about 100 per sound), so they are removed
    /// first; the window has no console, but starfieldvt-export does. Call once at startup.
    /// </summary>
    public static void RouteConverterLog()
    {
        BnkExtractor.Logger.ClearLoggingEvents();
        BnkExtractor.Logger.Info += ConverterLog.Trace;
        BnkExtractor.Logger.Debug += ConverterLog.Trace;
        BnkExtractor.Logger.Verbose += ConverterLog.Trace;
        BnkExtractor.Logger.Warning += ConverterLog.Warning;
        BnkExtractor.Logger.Error += ConverterLog.Error;
        BnkExtractor.Logger.Fatal += ConverterLog.Fatal;
    }

    /// <summary>
    /// Convert Wwise .wem bytes to a .wav at <paramref name="wavPath"/>: ww2ogg turns the .wem into
    /// Ogg Vorbis, then NVorbis decodes it to PCM. The intermediate .wem and .ogg files are written
    /// to the app's temp folder and always cleaned up.
    /// </summary>
    public static void ConvertWemToWav(byte[] wemBytes, string wavPath)
    {
        var tag = WemFormat.ReadFormatTag(wemBytes);
        // Verbose, not Debug: a bulk export at --verbose logs one line per file (BulkExporter's)
        Log.Verbose("Converting {Codec} .wem ({Bytes} bytes) to {WavPath}", WemFormat.Describe(tag), wemBytes.Length, wavPath);
        if (tag == WemFormat.WwisePcm)
        {
            ConvertPcm(wemBytes, wavPath);
            return;
        }

        if (tag != WemFormat.WwiseVorbis)
        {
            throw new InvalidDataException(
                $"This sound couldn't be converted: {WemFormat.Describe(tag)}. Please report it with the log (Help → Open Log Folder).");
        }

        var tempDir = AppDataFolder.GetTempDir();
        Directory.CreateDirectory(tempDir);
        var tempWemPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.wem");
        var tempOggPath = Path.ChangeExtension(tempWemPath, "ogg");

        try
        {
            File.WriteAllBytes(tempWemPath, wemBytes);
            Extractor.ConvertWem(tempWemPath);

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

            Log.Verbose("Writing converted wem file to wav file at {WavPath}", wavPath);
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

    /// <summary>Wwise PCM needs no decoding: rewrite the header and copy the samples.</summary>
    private static void ConvertPcm(byte[] wemBytes, string wavPath)
    {
        try
        {
            var directoryName = Path.GetDirectoryName(Path.GetFullPath(wavPath));
            if (directoryName != null) Directory.CreateDirectory(directoryName);

            using (var output = File.Create(wavPath))
            {
                WwisePcm.ToWav(wemBytes, output);
            }

            Log.Verbose("Wrote PCM sound to wav file at {WavPath}", wavPath);
        }
        catch (Exception e)
        {
            Log.Error(e, "There was an error converting the PCM wem file to {WavPath}", wavPath);
            TryDelete(wavPath);
            throw;
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
