namespace StarfieldVT.Core.Export;

/// <summary>What the command-line exporter exports.</summary>
public enum ExportKind
{
    Sounds,
    Voices,
}

/// <summary>
/// The command line of starfieldvt-export:
/// <c>starfieldvt-export sounds|voices --out &lt;folder&gt; [filters] [--flat] [--no-csv] [--list] [--game &lt;Data folder&gt;] [--verbose] [--log &lt;file&gt;]</c>.
/// Problems go to <see cref="Errors"/>, and any error means exit code 2.
/// </summary>
public sealed class ExportOptions
{
    /// <summary>Every file was written (or kept), or --list / --help finished.</summary>
    public const int ExitOk = 0;

    /// <summary>Some files failed; the rest were written.</summary>
    public const int ExitSomeFailed = 1;

    /// <summary>The command line was wrong.</summary>
    public const int ExitBadOptions = 2;

    /// <summary>No Starfield Data folder was found.</summary>
    public const int ExitNoGame = 3;

    /// <summary>Stopped with Ctrl+C; list.csv still lists what was done.</summary>
    public const int ExitCancelled = 130;

    public ExportKind? Kind { get; private set; }

    /// <summary>The export folder, as a full path.</summary>
    public string? OutFolder { get; private set; }

    /// <summary>Sounds only: a group (AMB, WPN, ...) or a mod's source archive.</summary>
    public string? Group { get; private set; }

    /// <summary>Voices only: a plugin file name such as Starfield.esm.</summary>
    public string? Plugin { get; private set; }

    /// <summary>Voices only: a voice type editor ID such as NPCFAstrid.</summary>
    public string? VoiceType { get; private set; }

    /// <summary>The same search the tab's search box does.</summary>
    public string? Search { get; private set; }

    /// <summary>Put every file in one folder instead of the game's folders.</summary>
    public bool Flat { get; private set; }

    /// <summary>Don't write list.csv.</summary>
    public bool NoCsv { get; private set; }

    /// <summary>A dry run: print the count and the first rows, write nothing.</summary>
    public bool List { get; private set; }

    /// <summary>The Starfield Data folder (or the install folder above it), as typed; null means the saved path or the Steam lookup.</summary>
    public string? GameFolder { get; private set; }

    public bool Verbose { get; private set; }

    /// <summary>The log file, as a full path; null means the daily file in the logs folder.</summary>
    public string? LogFile { get; private set; }

    public bool Help { get; private set; }

    public List<string> Errors { get; } = [];

    /// <summary>2 when the command line had a problem, otherwise 0.</summary>
    public int ExitCode => Errors.Count > 0 ? ExitBadOptions : ExitOk;

    public const string Usage =
        """
        starfieldvt-export: export Starfield sound effects or voice lines to WAV files.

        Usage:
          starfieldvt-export sounds --out <folder> [--group WPN] [--search "lightsaber"] [options]
          starfieldvt-export voices --out <folder> [--plugin Starfield.esm] [--voice-type NPCFAstrid] [--search "good to see you"] [options]

        Options:
          --out <folder>        Where the WAV files go (created if missing). Not needed with --list.
          --group <name>        Sounds: one group (AMB, WPN, MUS, ...) or a mod's archive name.
          --plugin <file>       Voices: one plugin, e.g. Starfield.esm.
          --voice-type <id>     Voices: one voice type, e.g. NPCFAstrid.
          --search <text>       The same search as the tab's search box.
          --flat                One folder for everything instead of the game's folders.
          --no-csv              Don't write list.csv.
          --list                Dry run: print the count and the first files, write nothing.
          --game <folder>       The Starfield Data folder (default: the path the window saved, then the Steam lookup).
          --verbose, -v         Log every file (Debug level).
          --log <file>          Write the log to this file instead of the logs folder.
          --help, -h            Show this help.

        Exit codes: 0 all written, 1 some failed, 2 bad options, 3 game Data folder not found, 130 cancelled with Ctrl+C.
        """;

    public static ExportOptions Parse(IReadOnlyList<string> args)
    {
        var o = new ExportOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string name = arg, inline = "";
            var hasInline = false;
            var eq = arg.IndexOf('=');
            if (arg.StartsWith("--") && eq > 2)
            {
                name = arg[..eq];
                inline = arg[(eq + 1)..];
                hasInline = true;
            }

            string? Value(string example)
            {
                if (hasInline)
                {
                    if (inline.Length > 0) return inline;
                    o.Errors.Add($"{name}= needs a value, e.g. {name} {example}.");
                    return null;
                }

                if (i + 1 < args.Count && !args[i + 1].StartsWith("--"))
                {
                    return args[++i];
                }

                o.Errors.Add($"{name} needs a value, e.g. {name} {example}.");
                return null;
            }

            void Flag(Action set)
            {
                if (hasInline) o.Errors.Add($"{name} takes no value; use {name} alone.");
                else set();
            }

            switch (name.ToLowerInvariant())
            {
                case "sounds" when o.Kind == null && i == 0:
                    o.Kind = ExportKind.Sounds;
                    break;
                case "voices" when o.Kind == null && i == 0:
                    o.Kind = ExportKind.Voices;
                    break;
                case "--out":
                    if (Value("\"D:\\Exports\\sounds\"") is { } outFolder) o.OutFolder = FullPath(o, name, outFolder);
                    break;
                case "--group":
                    o.Group = Value("WPN");
                    break;
                case "--plugin":
                    o.Plugin = Value("Starfield.esm");
                    break;
                case "--voice-type":
                    o.VoiceType = Value("NPCFAstrid");
                    break;
                case "--search":
                    o.Search = Value("\"lightsaber\"");
                    break;
                case "--game":
                    o.GameFolder = Value("\"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Starfield\\Data\"");
                    break;
                case "--log":
                    if (Value("C:\\temp\\starfieldvt-export.log") is { } log) o.LogFile = FullPath(o, name, log);
                    break;
                case "--flat":
                    Flag(() => o.Flat = true);
                    break;
                case "--no-csv":
                    Flag(() => o.NoCsv = true);
                    break;
                case "--list":
                    Flag(() => o.List = true);
                    break;
                case "--verbose":
                case "-v":
                    Flag(() => o.Verbose = true);
                    break;
                case "--help":
                case "-h":
                case "/?":
                    Flag(() => o.Help = true);
                    break;
                default:
                    o.Errors.Add(name is "sounds" or "voices"
                        ? $"\"{arg}\" must come first, and only once."
                        : $"Unknown option \"{arg}\". Run starfieldvt-export --help for the list.");
                    break;
            }
        }

        if (o.Help) return o;

        if (o.Kind == null)
        {
            o.Errors.Insert(0, "Say what to export first: starfieldvt-export sounds ... or starfieldvt-export voices ...");
            return o;
        }

        if (o.OutFolder == null && !o.List)
        {
            o.Errors.Add("--out is needed, e.g. --out \"D:\\Exports\\sounds\" (or use --list for a dry run).");
        }

        if (o.Kind == ExportKind.Sounds)
        {
            if (o.Plugin != null) o.Errors.Add("--plugin is for voices; for sounds use --group or --search.");
            if (o.VoiceType != null) o.Errors.Add("--voice-type is for voices; for sounds use --group or --search.");
        }
        else if (o.Group != null)
        {
            o.Errors.Add("--group is for sounds; for voices use --plugin, --voice-type or --search.");
        }

        return o;
    }

    private static string? FullPath(ExportOptions o, string name, string path)
    {
        try
        {
            return Path.GetFullPath(SettingsManager.CleanUpTypedPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            o.Errors.Add($"{name} \"{path}\" is not a usable path: {ex.Message}");
            return null;
        }
    }
}
