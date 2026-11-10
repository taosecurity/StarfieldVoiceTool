using System.Buffers.Binary;
using System.Text;

namespace StarfieldVT.Core.Sounds;

/// <summary>
/// Converts a Wwise PCM .wem (fmt tag 0xFFFE) to a standard WAV. The samples are already plain
/// interleaved 16-bit PCM, so only the header changes: the data chunk is copied as it is.
/// </summary>
/// <remarks>
/// The Wwise fmt chunk is 24 bytes: the usual 16, then cbSize (2), 2 unused bytes and a 32-bit
/// channel configuration (bits 0-7 channel count, 8-11 config type, 12-31 speaker mask).
/// </remarks>
public static class WwisePcm
{
    public static bool IsWwisePcm(ReadOnlySpan<byte> wem) => WemFormat.ReadFormatTag(wem) == WemFormat.WwisePcm;

    public static void ToWav(ReadOnlySpan<byte> wem, Stream output)
    {
        var tag = WemFormat.ReadFormatTag(wem);
        if (tag != WemFormat.WwisePcm)
            throw new InvalidDataException($"Not a Wwise PCM sound ({WemFormat.Describe(tag)}).");

        var fmt = RiffChunks.Find(wem, "fmt "u8)!.Value;
        if (fmt.Length < 16) throw new InvalidDataException("The sound's fmt chunk is too short.");
        var f = wem.Slice(fmt.Offset, fmt.Length);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(f[2..]);
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(f[4..]);
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(f[14..]);
        if (channels == 0 || sampleRate <= 0)
            throw new InvalidDataException($"The sound's header is invalid ({channels} channels, {sampleRate} Hz).");
        if (bitsPerSample != 16)
            throw new InvalidDataException($"Only 16-bit Wwise PCM is supported; this sound has {bitsPerSample} bits per sample.");

        uint speakerMask = 0;
        if (fmt.Length >= 24) speakerMask = BinaryPrimitives.ReadUInt32LittleEndian(f[20..]) >> 12;

        var data = RiffChunks.Find(wem, "data"u8)
                   ?? throw new InvalidDataException("The sound has no data chunk.");
        var blockAlign = (ushort)(channels * 2);
        var dataLength = data.Length - data.Length % blockAlign; // whole sample frames only

        WriteHeader(output, channels, sampleRate, blockAlign, dataLength, speakerMask);
        output.Write(wem.Slice(data.Offset, dataLength));
    }

    /// <summary>
    /// Mono and stereo get a plain PCM header (format 1), which every player reads. More channels get
    /// WAVE_FORMAT_EXTENSIBLE with the speaker mask, so players know which channel is which.
    /// </summary>
    private static void WriteHeader(Stream output, ushort channels, int sampleRate, ushort blockAlign, int dataLength, uint speakerMask)
    {
        var extensible = channels > 2;
        var fmtSize = extensible ? 40 : 16;
        using var w = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8);
        w.Write((uint)(4 + 8 + fmtSize + 8 + dataLength));
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(fmtSize);
        w.Write(extensible ? (ushort)0xFFFE : (ushort)1);
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * blockAlign);
        w.Write(blockAlign);
        w.Write((ushort)16);
        if (extensible)
        {
            w.Write((ushort)22); // cbSize
            w.Write((ushort)16); // valid bits per sample
            w.Write(speakerMask);
            // KSDATAFORMAT_SUBTYPE_PCM {00000001-0000-0010-8000-00AA00389B71}
            w.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray());
        }

        w.Write("data"u8);
        w.Write(dataLength);
    }
}
