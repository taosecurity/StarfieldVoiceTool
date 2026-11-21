using StarfieldVT.Core.Models;

namespace StarfieldVT.Core.Export
{
    /// <summary>The help text for a --voice-type that isn't found: near names, or the plugins that do have it.</summary>
    public static class VoiceTypeHint
    {
        private const int MaxNames = 20;

        /// <param name="tree">The whole voice line tree (every plugin).</param>
        /// <param name="plugin">The plugin the user picked with --plugin, or null.</param>
        /// <param name="voiceType">The voice type the user asked for.</param>
        public static string For(IEnumerable<Master> tree, string? plugin, string voiceType)
        {
            var all = tree.SelectMany(m => m.VoiceTypes).ToList();

            // the voice type exists, only in other plugins (e.g. a companion mod's own plugin)
            if (plugin != null)
            {
                var elsewhere = all
                    .Where(vt => vt.EditorId.Equals(voiceType, StringComparison.OrdinalIgnoreCase)
                                 && !vt.FromMaster.Equals(plugin, StringComparison.OrdinalIgnoreCase))
                    .Select(vt => vt.FromMaster)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxNames)
                    .ToList();
                if (elsewhere.Count > 0)
                {
                    return $"It has voice lines in: {string.Join(", ", elsewhere)}. Use --plugin with one of those, or leave --plugin out.";
                }
            }

            var inScope = plugin == null
                ? all
                : all.Where(vt => vt.FromMaster.Equals(plugin, StringComparison.OrdinalIgnoreCase)).ToList();
            var near = inScope.Select(vt => vt.EditorId)
                .Where(id => id.Contains(voiceType, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxNames)
                .ToList();
            return near.Count > 0
                ? "Did you mean: " + string.Join(", ", near)
                : "Run with --list and no --voice-type to see the voice lines.";
        }
    }
}
