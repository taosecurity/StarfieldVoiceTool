using System.Buffers.Binary;

using Mutagen.Bethesda.Archives;

using Noggog;

namespace StarfieldVT.Core.Sounds;

/// <summary>One sound stored inside a .bnk file: its Wwise ID and where its bytes are in the bank.</summary>
public sealed record BnkMediaEntry(uint Id, int Offset, int Length);

/// <summary>
/// Reads the list of sounds embedded in a Wwise sound bank. A bank is a series of sections, each a
/// 4-byte tag and a 32-bit little-endian size. DIDX lists the embedded files (12 bytes each: ID,
/// offset into DATA, size) and DATA holds them back to back.
/// </summary>
public static class BnkMedia
{
    public static List<BnkMediaEntry> Read(ReadOnlySpan<byte> bank)
    {
        (int Offset, int Length)? didx = null;
        (int Offset, int Length)? data = null;

        var pos = 0;
        while (pos + 8 <= bank.Length)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bank.Slice(pos + 4, 4));
            var start = pos + 8;
            if (size > (uint)(bank.Length - start)) break; // truncated: use what was found so far

            var tag = bank.Slice(pos, 4);
            if (tag.SequenceEqual("DIDX"u8)) didx = (start, (int)size);
            else if (tag.SequenceEqual("DATA"u8)) data = (start, (int)size);
            pos = start + (int)size;
        }

        var entries = new List<BnkMediaEntry>();
        if (didx == null || data == null) return entries;

        var (dataStart, dataLength) = data.Value;
        for (var i = 0; i + 12 <= didx.Value.Length; i += 12)
        {
            var entry = bank.Slice(didx.Value.Offset + i, 12);
            var id = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            if (length == 0 || offset > (uint)dataLength || length > (uint)dataLength - offset) continue;
            entries.Add(new BnkMediaEntry(id, dataStart + (int)offset, (int)length));
        }

        return entries;
    }
}

/// <summary>A sound inside a .bnk, read from its bank when played or exported.</summary>
public sealed class EmbeddedSoundFile : IArchiveFile
{
    private readonly IArchiveFile _bank;
    private readonly BnkMediaEntry _entry;

    public EmbeddedSoundFile(IArchiveFile bank, BnkMediaEntry entry)
    {
        _bank = bank;
        _entry = entry;
    }

    /// <summary>The bank's path, e.g. "sound/soundbanks/2286754810.bnk".</summary>
    public string Path => _bank.Path;
    public uint Size => (uint)_entry.Length;

    public byte[] GetBytes()
    {
        var bank = _bank.GetBytes();
        if (_entry.Offset + _entry.Length > bank.Length)
            throw new InvalidDataException($"Sound {_entry.Id} lies outside its bank {_bank.Path}; the bank may have changed.");
        return bank.AsSpan(_entry.Offset, _entry.Length).ToArray();
    }

    public ReadOnlySpan<byte> GetSpan() => GetBytes();
    public ReadOnlyMemorySlice<byte> GetMemorySlice() => new(GetBytes());
    public Stream AsStream() => new MemoryStream(GetBytes(), false);
}
