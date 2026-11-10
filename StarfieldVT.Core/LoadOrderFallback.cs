using System.Text;

using Serilog;

namespace StarfieldVT.Core;

/// <summary>
/// Without a Plugins.txt (e.g. mods copied into Data by hand, no mod manager), Mutagen only sees the
/// game's built-in plugins. This adds the other plugins in the Data folder so their lines are listed.
/// </summary>
public static class LoadOrderFallback
{
    private static readonly string[] PluginExtensionOrder = [".esm", ".esl", ".esp"];

    /// <summary>
    /// The plugins in <paramref name="dataFolderPath"/> that aren't in <paramref name="listed"/>, ordered so
    /// each comes after its masters. Plugins with a master that is missing are left out and logged.
    /// </summary>
    public static List<string> FindUnlistedPlugins(string dataFolderPath, IEnumerable<string> listed) =>
        FindUnlistedPlugins(ReadDataFolderPlugins(dataFolderPath), listed);

    public static List<string> FindUnlistedPlugins(IReadOnlyDictionary<string, IReadOnlyList<string>> onDisk,
        IEnumerable<string> listed)
    {
        var ordered = OrderAfterMasters(listed.ToList(), onDisk, out var missingMasters);
        foreach (var (plugin, missing) in missingMasters)
        {
            Log.Warning("Skipping {Plugin}: master {Master} is not in the Data folder", plugin, missing);
        }

        return ordered;
    }

    /// <summary>Every plugin in the Data folder with its masters. Unreadable plugins are logged and left out.</summary>
    public static Dictionary<string, IReadOnlyList<string>> ReadDataFolderPlugins(string dataFolderPath)
    {
        var plugins = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(dataFolderPath))
        {
            var name = Path.GetFileName(file);
            if (!VoicePaths.IsPluginName(name)) continue;

            try
            {
                plugins[name] = ReadMasters(file);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read the plugin header of {Plugin}, skipping it", name);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Plugins.txt is shared by every Starfield install on the PC, so it can list plugins that aren't in
    /// this Data folder (another install, or a mod manager staging folder). Mutagen can't read any plugin
    /// then, so keep only the enabled plugins that are on disk, in load order, and whose masters are all
    /// kept before them. The ones left out are returned with the reason.
    /// </summary>
    public static List<string> KeepLoadable(IReadOnlyList<string> enabledInOrder,
        IReadOnlyDictionary<string, IReadOnlyList<string>> onDisk,
        out List<string> notOnDisk, out List<(string Plugin, string Master)> missingMasters)
    {
        var kept = new List<string>();
        var keptSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        notOnDisk = new List<string>();
        missingMasters = new List<(string, string)>();
        foreach (var name in enabledInOrder)
        {
            if (!onDisk.TryGetValue(name, out var masters))
            {
                notOnDisk.Add(name);
                continue;
            }

            var missing = masters.FirstOrDefault(master => !keptSet.Contains(master));
            if (missing != null)
            {
                missingMasters.Add((name, missing));
                continue;
            }

            kept.Add(name);
            keptSet.Add(name);
        }

        return kept;
    }

    /// <summary>
    /// Order the candidates not already listed so masters come first: .esm, then .esl, then .esp, by name,
    /// each placed once all its masters are loaded. Candidates whose masters never load are returned in
    /// <paramref name="missingMasters"/> with the first master that is missing.
    /// </summary>
    public static List<string> OrderAfterMasters(IReadOnlyList<string> listed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> candidates, out List<(string Plugin, string Master)> missingMasters)
    {
        var loaded = new HashSet<string>(listed, StringComparer.OrdinalIgnoreCase);
        var pending = candidates.Keys
            .Where(name => !loaded.Contains(name))
            .OrderBy(name => Array.FindIndex(PluginExtensionOrder,
                ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // always take the first ready plugin, so an .esm waiting on its master still loads before any .esp
        var ordered = new List<string>();
        while (pending.FirstOrDefault(name => candidates[name].All(loaded.Contains)) is { } next)
        {
            ordered.Add(next);
            loaded.Add(next);
            pending.Remove(next);
        }

        missingMasters = pending
            .Select(name => (name, candidates[name].FirstOrDefault(master => !loaded.Contains(master)) ?? "?"))
            .ToList();
        return ordered;
    }

    /// <summary>The MAST entries of a plugin's TES4 header record.</summary>
    public static List<string> ReadMasters(string pluginPath)
    {
        using var reader = new BinaryReader(File.OpenRead(pluginPath));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TES4")
            throw new InvalidDataException("Not a plugin file (no TES4 header)");

        var dataSize = reader.ReadUInt32();
        reader.ReadBytes(16); // flags, form id, version control, form version, unknown
        var data = reader.ReadBytes((int)dataSize);

        var masters = new List<string>();
        var position = 0;
        uint? extendedSize = null;
        while (position + 6 <= data.Length)
        {
            var type = Encoding.ASCII.GetString(data, position, 4);
            var size = extendedSize ?? BitConverter.ToUInt16(data, position + 4);
            extendedSize = null;
            position += 6;
            if (position + size > data.Length) break;

            if (type == "XXXX")
            {
                // the next subrecord is longer than 64 KB; this one holds its real size
                extendedSize = BitConverter.ToUInt32(data, position);
            }
            else if (type == "MAST")
            {
                masters.Add(Encoding.Latin1.GetString(data, position, (int)size).TrimEnd('\0'));
            }

            position += (int)size;
        }

        return masters;
    }
}
