using StarfieldVT.Core.Models;

namespace StarfieldVT.Core;

/// <summary>
/// The lines the table is showing, so a dialogue search only finds lines among them. The search
/// index is scoped by plugin and voice type, but the table can also be narrowed by the voice type
/// filter box (the tree hands over a filtered copy of the plugin), so the search results are
/// checked against this set: plugin AND voice type filter AND dialogue text.
/// </summary>
public sealed class SearchScope
{
    private readonly HashSet<(string Filename, string VoiceType)>? _keys;

    private SearchScope(HashSet<(string, string)>? keys) => _keys = keys;

    /// <summary>No restriction: the table is empty, so a search covers every plugin.</summary>
    public static SearchScope Everything { get; } = new(null);

    public static SearchScope Of(IEnumerable<VoiceLine> shown)
    {
        var keys = new HashSet<(string, string)>(KeyComparer.Instance);
        foreach (var line in shown) keys.Add(Key(line));
        return new SearchScope(keys);
    }

    public bool IsEverything => _keys == null;

    public IEnumerable<VoiceLine> Restrict(IEnumerable<VoiceLine> results) =>
        _keys == null ? results : results.Where(line => _keys.Contains(Key(line)));

    private static (string, string) Key(VoiceLine line) => (line.Filename, line.VoiceType);

    private sealed class KeyComparer : IEqualityComparer<(string, string)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((string, string) a, (string, string) b) =>
            StringComparer.OrdinalIgnoreCase.Equals(a.Item1, b.Item1) &&
            StringComparer.OrdinalIgnoreCase.Equals(a.Item2, b.Item2);

        public int GetHashCode((string, string) k) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(k.Item1),
            StringComparer.OrdinalIgnoreCase.GetHashCode(k.Item2));
    }
}
