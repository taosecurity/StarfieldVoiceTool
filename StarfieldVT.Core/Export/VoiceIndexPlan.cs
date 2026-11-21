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

    /// <summary>The message when the search index is locked although no window was found (another export, or a window started as "dotnet StarfieldVT.dll").</summary>
    public const string IndexLockedMessage =
        "The voice line search index is locked by another program: a Starfield Voice Tool window, or another " +
        "starfieldvt-export run that reads the plugins or uses --search. Close the window or wait for the other " +
        "run to finish, then run the command again.";

    /// <summary>
    /// A window counts only in this Windows session: each account has its own search index under its
    /// own %LOCALAPPDATA%, so another user's open window locks nothing here.
    /// </summary>
    public static bool AnyInSession(IEnumerable<int> windowSessionIds, int mySessionId) =>
        windowSessionIds.Any(id => id == mySessionId);

    /// <summary>The exception (or one inside it) is Lucene's "the index is locked" error.</summary>
    public static bool IsIndexLocked(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
        {
            if (ex is Lucene.Net.Store.LockObtainFailedException) return true;
        }

        return false;
    }

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
