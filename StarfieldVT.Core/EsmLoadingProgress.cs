namespace StarfieldVT.Core
{
    public enum LoadState
    {
        Loading,
        Completed,
        Failed
    }

    public class EsmLoadingProgress
    {
        public LoadState State { get; init; } = LoadState.Loading;
        public string EsmName { get; init; } = "";
        public int PluginNumber { get; init; }
        public int PluginCount { get; init; }

        /// <summary>Number of quests parsed so far in the current plugin.</summary>
        public int Num { get; init; }

        /// <summary>Total quests in the current plugin, or 0 when the amount of work is unknown.</summary>
        public int Total { get; init; }

        /// <summary>Optional status text that replaces the default per-quest message.</summary>
        public string? Message { get; init; }
    }
}
