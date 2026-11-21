namespace StarfieldVT.Core.Export;

/// <summary>What starfieldvt-export voices does with the voice line search index it shares with the window.</summary>
public enum VoiceIndexAction
{
    /// <summary>Use the cached tree; the index isn't needed (no --search).</summary>
    None,

    /// <summary>Use the cached tree and search the index as it is.</summary>
    Search,

    /// <summary>The cache is current but the index is empty: fill it from the cached tree, then search.</summary>
    Refill,

    /// <summary>The cache doesn't match the game: read the plugins again, which rebuilds the cache and the index.</summary>
    Rebuild,

    /// <summary>The index is needed but the window is open and holds it: stop and ask the user to close the window.</summary>
    StopWindowOpen,
}

/// <summary>Chooses a <see cref="VoiceIndexAction"/>.</summary>
public static class VoiceIndexPlan
{
    /// <summary>The window's process name (StarfieldVT.exe, or StarfieldVT on Linux).</summary>
    public const string WindowProcessName = "StarfieldVT";

    /// <summary>The message for <see cref="VoiceIndexAction.StopWindowOpen"/>.</summary>
    public const string WindowOpenMessage =
        "The Starfield Voice Tool window is open, and it holds the voice line search index that this run needs " +
        "(to read the plugins again, or for --search). Close the window and run the command again. " +
        "A voices export without --search works with the window open once the window has loaded its lines.";

    /// <param name="cacheCurrent">The voice cache matches the game's files.</param>
    /// <param name="search">--search was given.</param>
    /// <param name="windowRunning">A StarfieldVT window process is running.</param>
    /// <param name="indexDocuments">How many lines the index holds; only asked when the index is used and the window is closed.</param>
    public static VoiceIndexAction Choose(bool cacheCurrent, bool search, bool windowRunning, Func<int> indexDocuments)
    {
        if (cacheCurrent && !search) return VoiceIndexAction.None;
        if (windowRunning) return VoiceIndexAction.StopWindowOpen;
        if (!cacheCurrent) return VoiceIndexAction.Rebuild;
        return indexDocuments() == 0 ? VoiceIndexAction.Refill : VoiceIndexAction.Search;
    }
}
