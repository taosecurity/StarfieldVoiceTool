using System.Security.Cryptography;
using System.Text;

namespace StarfieldVT.Core;

/// <summary>
/// A hash of the things the voice line cache depends on: which data folder is used, the plugins and
/// archives in it (name, size, modified time) and the load order in Plugins.txt. A game update, a
/// new mod or a load order change all produce a different fingerprint.
/// </summary>
public static class GameFingerprint
{
    private static readonly string[] Extensions = [".esm", ".esp", ".esl", ".ba2"];

    public static string PluginsTxtPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starfield", "Plugins.txt");

    public static string Compute(string dataFolderPath) => Compute(dataFolderPath, PluginsTxtPath);

    public static string Compute(string dataFolderPath, string pluginsTxtPath)
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

        if (File.Exists(pluginsTxtPath))
        {
            sb.AppendLine(File.ReadAllText(pluginsTxtPath));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
