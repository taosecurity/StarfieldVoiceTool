using System.Text;

using NVorbis;

namespace StarfieldVT.Core.Audio;

/// <summary>
/// Decodes Ogg Vorbis to a 16-bit PCM WAV file using NVorbis (managed code, no external tools).
/// </summary>
/// <remarks>
/// ww2ogg's output has no valid granule positions (the total length reads as 0), which made
/// NVorbis 0.10.x loop forever in ReadSamples (NVorbis issues #40/#76, fixed in 1.0).
/// </remarks>
public static class OggToWav
{
    // voice lines are seconds long; anything past this means the decoder is looping
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    public static void Convert(string oggPath, string wavPath)
    {
        using var vorbis = new VorbisReader(oggPath);
        using var output = File.Create(wavPath);
        Write(vorbis.Channels, vorbis.SampleRate, buffer => vorbis.ReadSamples(buffer, 0, buffer.Length), output);
    }

    /// <summary>
    /// Write a 16-bit PCM WAV from interleaved float samples in [-1, 1]. <paramref name="readSamples"/>
    /// fills the buffer and returns how many samples it wrote, or 0 at the end. The total length isn't
    /// needed up front: the header sizes are patched once all samples are written.
    /// </summary>
    public static void Write(int channels, int sampleRate, Func<float[], int> readSamples, Stream output)
    {
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

        const short bitsPerSample = 16;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        var start = output.Position;
        writer.Write("RIFF"u8);
        writer.Write(0); // RIFF size, patched below
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(0); // data size, patched below

        var buffer = new float[sampleRate * channels]; // about one second per read
        var maxSamples = (long)(MaxDuration.TotalSeconds * sampleRate * channels);
        long dataBytes = 0;
        long totalSamples = 0;
        int read;
        while ((read = readSamples(buffer)) > 0)
        {
            totalSamples += read;
            if (totalSamples > maxSamples)
            {
                throw new InvalidDataException($"Decoded audio exceeds {MaxDuration.TotalMinutes} minutes; the stream is likely corrupt.");
            }

            for (var i = 0; i < read; i++)
            {
                writer.Write((short)Math.Round(Math.Clamp(buffer[i], -1f, 1f) * short.MaxValue));
            }

            dataBytes += read * 2L;
        }

        if (dataBytes > uint.MaxValue - 36)
        {
            throw new InvalidDataException("Audio is too long for a WAV file.");
        }

        writer.Flush();
        var end = output.Position;
        output.Position = start + 4;
        writer.Write((uint)(36 + dataBytes));
        output.Position = start + 40;
        writer.Write((uint)dataBytes);
        writer.Flush();
        output.Position = end;
    }
}
