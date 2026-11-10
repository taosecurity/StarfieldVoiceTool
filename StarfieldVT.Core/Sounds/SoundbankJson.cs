using System.Globalization;
using System.Text.Json;

namespace StarfieldVT.Core.Sounds;

/// <summary>A sound file named in a Wwise bank's JSON. ShortName is the source file, e.g. "WPN\Hand\X.wav".</summary>
public sealed record SoundFileInfo(uint Id, string? ShortName, bool InMemory);

/// <summary>A Wwise event and the streamed sounds it can play.</summary>
public sealed record SoundEventInfo(uint Id, string Name, IReadOnlyList<uint> SoundIds);

public sealed record SoundBankInfo(uint Id, string Name, IReadOnlyList<SoundEventInfo> Events, IReadOnlyList<SoundFileInfo> Files);

/// <summary>
/// What we read from a Wwise "SoundbanksInfo" JSON file. The game ships one per bank
/// (sound/soundbanks/&lt;bankId&gt;.json, also in mods) and a combined soundbanksinfo.json, whose
/// top-level StreamedFiles lists every streamed sound.
/// </summary>
public sealed record SoundbankJsonFile(IReadOnlyList<SoundBankInfo> Banks, IReadOnlyList<SoundFileInfo> StreamedFiles)
{
    public static SoundbankJsonFile Parse(ReadOnlyMemory<byte> json)
    {
        var span = json.Span;
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) json = json[3..];

        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        if (!doc.RootElement.TryGetProperty("SoundBanksInfo", out var info) || info.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Not a Wwise soundbank JSON file (no SoundBanksInfo object).");

        var banks = new List<SoundBankInfo>();
        foreach (var bank in Array(info, "SoundBanks"))
        {
            if (!TryId(bank, out var bankId)) continue;
            var name = Text(bank, "ShortName") ?? bankId.ToString(CultureInfo.InvariantCulture);

            var files = new List<SoundFileInfo>();
            files.AddRange(Files(bank, "ReferencedStreamedFiles", inMemory: false));
            files.AddRange(Files(bank, "IncludedMemoryFiles", inMemory: true));

            var events = new List<SoundEventInfo>();
            foreach (var ev in Array(bank, "IncludedEvents"))
            {
                if (!TryId(ev, out var evId)) continue;
                var evFiles = Files(ev, "ReferencedStreamedFiles", inMemory: false).ToList();
                // events name their sounds too; keep those names in case the bank list has only IDs
                files.AddRange(evFiles.Where(f => f.ShortName != null));
                events.Add(new SoundEventInfo(evId, Text(ev, "Name") ?? evId.ToString(CultureInfo.InvariantCulture),
                    evFiles.Select(f => f.Id).Distinct().ToList()));
            }

            banks.Add(new SoundBankInfo(bankId, name, events, files));
        }

        var streamed = Files(info, "StreamedFiles", inMemory: false).ToList();
        return new SoundbankJsonFile(banks, streamed);
    }

    private static IEnumerable<SoundFileInfo> Files(JsonElement parent, string property, bool inMemory)
    {
        foreach (var item in Array(parent, property))
        {
            if (TryId(item, out var id)) yield return new SoundFileInfo(id, Text(item, "ShortName"), inMemory);
        }
    }

    private static IEnumerable<JsonElement> Array(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static string? Text(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Wwise writes IDs as strings ("Id": "29651"); accept numbers too.</summary>
    private static bool TryId(JsonElement element, out uint id)
    {
        id = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("Id", out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.String => uint.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out id),
            JsonValueKind.Number => value.TryGetUInt32(out id),
            _ => false,
        };
    }
}
