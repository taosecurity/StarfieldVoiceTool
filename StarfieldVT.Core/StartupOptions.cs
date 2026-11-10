namespace StarfieldVT.Core;

/// <summary>
/// Command-line options for troubleshooting. Anything else on the command line is left for the
/// UI framework and reported in <see cref="Unknown"/> so it can be logged.
/// </summary>
public sealed class StartupOptions
{
    /// <summary>Log at Debug level: every quest and dialogue parsed, every search, every playback step.</summary>
    public bool Verbose { get; private set; }

    /// <summary>Write the log to this file instead of the daily file in the logs folder.</summary>
    public string? LogFile { get; private set; }

    /// <summary>Problems found while parsing, e.g. "--log needs a file name".</summary>
    public List<string> Errors { get; } = new();

    public List<string> Unknown { get; } = new();

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var options = new StartupOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;
                case "--log":
                    if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                    {
                        options.LogFile = Path.GetFullPath(args[++i]);
                    }
                    else
                    {
                        options.Errors.Add("--log needs a file name, e.g. --log C:\\temp\\starfieldvt.log; using the default log file.");
                    }
                    break;
                default:
                    if (arg.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = arg["--log=".Length..];
                        if (value.Length > 0) options.LogFile = Path.GetFullPath(value);
                        else options.Errors.Add("--log= needs a file name; using the default log file.");
                    }
                    else
                    {
                        options.Unknown.Add(arg);
                    }
                    break;
            }
        }

        return options;
    }
}
