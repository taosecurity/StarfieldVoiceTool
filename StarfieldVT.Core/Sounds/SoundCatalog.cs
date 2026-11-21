using System.Globalization;

namespace StarfieldVT.Core.Sounds;

/// <summary>An entry in the Sound Effects list on the left: all sounds, one group, or one mod's sounds.</summary>
public sealed record SoundGroup(string Label, string? Group, string? ModSource, int Count)
{
    public bool Contains(SoundEffect sound) =>
        (Group == null || (!sound.FromMod && sound.Group.Equals(Group, StringComparison.OrdinalIgnoreCase))) &&
        (ModSource == null || sound.Source.Equals(ModSource, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Label;
}

/// <summary>Grouping, searching and file names for the Sound Effects view.</summary>
public static class SoundCatalog
{
    public const string AllLabel = "All sounds";

    /// <summary>
    /// "All sounds", then the base game's groups (AMB, WPN, ...) by name, then one entry per mod
    /// source, so a mod's sounds are easy to find whatever folder its author used.
    /// </summary>
    public static List<SoundGroup> Groups(IReadOnlyCollection<SoundEffect> sounds)
    {
        var groups = new List<SoundGroup> { new(AllLabel, null, null, sounds.Count) };
        groups.AddRange(sounds.Where(s => !s.FromMod)
            .GroupBy(s => s.Group, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SoundGroup($"{g.Key} ({g.Count():N0})", g.Key, null, g.Count())));
        groups.AddRange(sounds.Where(s => s.FromMod)
            .GroupBy(s => s.Source, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SoundGroup($"Mod: {g.Key} ({g.Count():N0})", null, g.Key, g.Count())));
        return groups;
    }

    /// <summary>
    /// The group a sound is listed under on the left, without its count: the Wwise group (AMB, WPN, ...)
    /// for a base game sound, "Mod: &lt;archive&gt;" for a DLC, Creation or mod sound. list.csv's Group column uses it.
    /// </summary>
    public static string GroupLabel(SoundEffect sound) => sound.FromMod ? $"Mod: {sound.Source}" : sound.Group;

    /// <summary>
    /// The entry of <paramref name="groups"/> that a --group value names: a base game group (WPN), or a mod
    /// archive by its name, with or without the "Mod: " that list.csv's Group column puts in front. Null if none.
    /// </summary>
    public static SoundGroup? FindGroup(IEnumerable<SoundGroup> groups, string name)
    {
        const string modPrefix = "Mod: ";
        var archive = name.StartsWith(modPrefix, StringComparison.OrdinalIgnoreCase) ? name[modPrefix.Length..].Trim() : name;
        return groups.FirstOrDefault(g =>
            string.Equals(g.Group, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g.ModSource, archive, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Split a search into words; a sound must match every word.</summary>
    public static string[] Terms(string? search) =>
        (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// A word matches the sound's ID exactly, or appears in its name, folder, group, bank, source or
    /// any of its event names (case-insensitive).
    /// </summary>
    public static bool Matches(SoundEffect sound, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (uint.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id == sound.Id) continue;
            if (Has(sound.Name, term) || Has(sound.Folder, term) || Has(sound.Group, term) || Has(sound.Bank, term) ||
                Has(sound.Source, term) || sound.Events.Any(e => Has(e, term)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool Has(string? text, string term) =>
        text != null && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>"&lt;Name&gt;_&lt;Id&gt;.wav", with characters Windows doesn't allow in file names replaced.</summary>
    public static string ExportFileName(SoundEffect sound)
    {
        var name = Export.ExportLayout.CleanName(sound.Name);
        var id = sound.Id.ToString(CultureInfo.InvariantCulture);
        return name.Length == 0 || name == id ? $"{id}.wav" : $"{name}_{id}.wav";
    }
}
