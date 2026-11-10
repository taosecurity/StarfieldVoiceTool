using System.Buffers.Binary;

namespace StarfieldVT.Core.Sounds;

/// <summary>
/// Reads the codec of a Wwise .wem file from its RIFF "fmt " chunk. Starfield's .wem files use two:
/// Wwise PCM (tag 0xFFFE, plain 16-bit samples) and Wwise Vorbis (tag 0xFFFF, which ww2ogg converts).
/// </summary>
public static class WemFormat
{
    public const ushort WwisePcm = 0xFFFE;
    public const ushort WwiseVorbis = 0xFFFF;

    /// <summary>The fmt tag, or null when the bytes are not a RIFF file with a fmt chunk.</summary>
    public static ushort? ReadFormatTag(ReadOnlySpan<byte> wem)
    {
        var fmt = RiffChunks.Find(wem, "fmt "u8);
        if (fmt == null || fmt.Value.Length < 2) return null;
        return BinaryPrimitives.ReadUInt16LittleEndian(wem.Slice(fmt.Value.Offset, 2));
    }

    public static string Describe(ushort? tag) => tag switch
    {
        WwisePcm => "Wwise PCM",
        WwiseVorbis => "Wwise Vorbis",
        null => "not a RIFF/WAVE file",
        _ => $"unsupported codec 0x{tag.Value:X4}",
    };
}

/// <summary>Finds chunks in a little-endian RIFF/WAVE file.</summary>
internal static class RiffChunks
{
    /// <summary>Offset and length of a chunk's data (clipped to the file), or null if it isn't there.</summary>
    public static (int Offset, int Length)? Find(ReadOnlySpan<byte> riff, ReadOnlySpan<byte> id)
    {
        if (riff.Length < 12 || !riff[..4].SequenceEqual("RIFF"u8) || !riff.Slice(8, 4).SequenceEqual("WAVE"u8))
            return null;

        var pos = 12;
        while (pos + 8 <= riff.Length)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(riff.Slice(pos + 4, 4));
            var start = pos + 8;
            var length = (int)Math.Min(size, (uint)(riff.Length - start));
            if (riff.Slice(pos, 4).SequenceEqual(id)) return (start, length);

            var next = (long)start + size + (size & 1);
            if (next > riff.Length) break;
            pos = (int)next;
        }

        return null;
    }
}
