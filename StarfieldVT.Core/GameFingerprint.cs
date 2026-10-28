using System.Security.Cryptography;
using System.Text;

namespace StarfieldVT.Core;

/// <summary>
/// A hash of the things the voice line cache depends on: which data folder is used, the plugins and
/// archives in it (name, size, modified time) and the load order in Plugins.txt. A game update, a
/// new mod or a load order change all produce a different fingerprint. With all mod voices included,
/// the loose voice files and the option itself count too.
/// </summary>
public static class GameFingerprint
{
    private static readonly string[] Extensions = [".esm", ".esp", ".esl", ".ba2"];

    public static string PluginsTxtPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starfield", "Plugins.txt");

    public static string Compute(string dataFolderPath, bool includeAllModVoices = false) =>
        Compute(dataFolderPath, PluginsTxtPath, includeAllModVoices);

    public static string Compute(string dataFolderPath, string pluginsTxtPath, bool includeAllModVoices = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Path.GetFullPath(dataFolderPath).ToLowerInvariant());

        if (Directory.Exists(dataFolderPath))
        {
            var files = Directory.EnumerateFiles(dataFolderPath)
                .Where(file => Extensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Select(file => new FileInfo(file))
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var info in files)
            {
                sb.Append(info.Name.ToLowerInvariant()).Append('|')
                    .Append(info.Length).Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks).AppendLine();
            }
        }

        if (includeAllModVoices)
        {
            sb.AppendLine("include all mod voices");
            foreach (var file in VoiceArchiveManager.GetLooseVoiceFiles(dataFolderPath))
            {
                var info = new FileInfo(Path.Combine(dataFolderPath, file.Path));
                sb.Append(file.Path.ToLowerInvariant()).Append('|')
                    .Append(info.Length).Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks).AppendLine();
            }
        }

        if (File.Exists(pluginsTxtPath))
        {
            sb.AppendLine(File.ReadAllText(pluginsTxtPath));
        }
        else
        {
            // without a Plugins.txt every plugin in Data is read (LoadOrderFallback)
            sb.AppendLine("no Plugins.txt: all Data plugins");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
