using System.Reflection;
using System.Runtime.InteropServices;

using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.Core;

/// <summary>Text and links for the Help menu and the About window.</summary>
public static class AboutInfo
{
    public const string ProjectUrl = "https://github.com/taosecurity/StarfieldVoiceTool";
    public const string IssuesUrl = ProjectUrl + "/issues";
    public const string LatestReleaseUrl = ProjectUrl + "/releases/latest";
    public const string OriginalUrl = "https://github.com/yak3d/StarfieldVoiceTool";

    public const string Description =
        "Browse, search, play and export Starfield's voiced lines, including your mods'.";

    public const string Maintainer =
        "A fork of yak3d's Starfield Voice Tool, maintained by Richard Bejtlich.";

    public const string Credits =
        "Thanks to yak3d (the original tool), ds5678 (BnkExtractor), Mutagen, NVorbis and VLC.";

    public const string License =
        "Source code: MIT. This build: GPL-3.0, because it includes Mutagen. " +
        "Third-party notices lists every bundled component and its license.";

    /// <summary>The app version as people see it, e.g. "0.8.1".</summary>
    public static string Version(Assembly assembly)
    {
        var v = assembly.GetName().Version;
        return v == null ? "unknown" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>The THIRD-PARTY-NOTICES.md shipped next to the program, if it's there.</summary>
    public static string NoticesPath => Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");

    /// <summary>The block "Copy details" puts on the clipboard, for bug reports.</summary>
    public static string Details(string version, string? dataPath, bool includeAllModVoices) =>
        string.Join(Environment.NewLine,
            $"Starfield Voice Tool {version}",
            $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})",
            $"Data folder: {dataPath ?? "(not set)"}",
            $"Include voices from all mod archives: {(includeAllModVoices ? "on" : "off")}",
            $"Settings and logs: {AppDataFolder.GetAppDataFolder()}",
            $"Log folder: {AppDataFolder.GetLogDir()}");
}
