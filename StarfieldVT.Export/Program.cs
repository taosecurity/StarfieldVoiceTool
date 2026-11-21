using System.Globalization;
using System.Runtime.InteropServices;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Export;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Models;
using StarfieldVT.Core.Sounds;
using StarfieldVT.UI.Audio;

namespace StarfieldVT.ExportTool;

/// <summary>
/// starfieldvt-export: the bulk export of the window's two tabs, from the command line. It reads the
/// game's files only and never changes them. Exit codes are in <see cref="ExportOptions"/>.
/// </summary>
public static class Program
{
    /// <summary>How many files --list prints before "... and N more".</summary>
    private const int ListRows = 20;

    public static int Main(string[] args)
    {
        var options = ExportOptions.Parse(args);
        if (options.Help)
        {
            Console.WriteLine(ExportOptions.Usage);
            return ExportOptions.ExitOk;
        }

        if (options.Errors.Count > 0)
        {
            foreach (var error in options.Errors) Console.Error.WriteLine($"starfieldvt-export: {error}");
            Console.Error.WriteLine("Run starfieldvt-export --help for the options.");
            return options.ExitCode;
        }

        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // the first Ctrl+C stops after the current file and still writes list.csv; a second one ends at once
            if (cancel.IsCancellationRequested) return;
            e.Cancel = true;
            Console.Error.WriteLine();
            Console.Error.WriteLine("Stopping after the current file (press Ctrl+C again to quit at once)...");
            cancel.Cancel();
        };

        StartLogging(options);
        try
        {
            return Run(options, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Cancelled with Ctrl+C before the export started; nothing was written");
            return ExportOptions.ExitCancelled;
        }
        catch (ExportStoppedException ex)
        {
            Log.Error("{Message}", ex.Message);
            return ExportOptions.ExitFailed;
        }
        catch (Exception ex) when (VoiceIndexPlan.IsIndexLocked(ex))
        {
            Log.Error("{Message}", VoiceIndexPlan.IndexLockedMessage);
            Log.Debug(ex, "Details");
            return ExportOptions.ExitFailed;
        }
        catch (IOException ex)
        {
            // a folder or file problem: the message names it and the fix; the stack only at --verbose
            Log.Error("The export stopped: {Message}", ex.Message);
            Log.Debug(ex, "Details");
            return ExportOptions.ExitFailed;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "The export stopped: {Message}", ex.Message);
            return ExportOptions.ExitFailed;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void StartLogging(ExportOptions options)
    {
        var config = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}{Exception}", formatProvider: CultureInfo.InvariantCulture);
        config = options.Verbose ? config.MinimumLevel.Debug() : config.MinimumLevel.Information();
        var logFile = options.LogFile;
        if (logFile != null)
        {
            var dir = Path.GetDirectoryName(logFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            config = config.WriteTo.File(logFile, formatProvider: CultureInfo.InvariantCulture);
        }
        else
        {
            config = config.WriteTo.File(
                Path.Combine(AppDataFolder.GetLogDir(), "starfieldvt-export.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 524288000,
                formatProvider: CultureInfo.InvariantCulture);
        }

        Log.Logger = config.CreateLogger();
        Log.Debug("starfieldvt-export {Version} on {OS} ({Arch})",
            typeof(Program).Assembly.GetName().Version, RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture);
        Log.Debug("Log level {Level}, log file {LogFile}",
            options.Verbose ? "Debug (--verbose)" : "Information",
            logFile ?? Path.Combine(AppDataFolder.GetLogDir(), "starfieldvt-export<date>.log"));

        // ww2ogg reports conversion problems through its own events; its trace goes below --verbose
        // (and its own console printing is turned off)
        AudioConverter.RouteConverterLog();
    }

    private static int Run(ExportOptions options, CancellationToken cancel)
    {
        var dataPath = FindGame(options);
        if (dataPath == null) return ExportOptions.ExitNoGame;
        Log.Debug("Starfield Data folder {DataPath}", dataPath);

        return options.Kind == ExportKind.Sounds
            ? Export(options, SoundItems(options, dataPath, out var findSound), ExportLayout.SoundColumns,
                (sound, path) => AudioConverter.ConvertWemToWav(findSound(sound), path), cancel)
            : Export(options, VoiceItems(options, dataPath, cancel, out var findVoice), ExportLayout.VoiceColumns,
                (line, path) => AudioConverter.ConvertWemToWav(findVoice(line), path), cancel);
    }

    /// <summary>--game, else the path the window saved, else the Steam lookup the window uses. Never saves a setting.</summary>
    private static string? FindGame(ExportOptions options)
    {
        if (options.GameFolder != null)
        {
            var data = SettingsManager.NormalizeDataFolder(options.GameFolder);
            if (data != null) return Path.GetFullPath(data);
            Log.Error("No Starfield.esm in {Folder} or its Data folder. Give --game the game's Data folder, e.g. " +
                      "--game \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Starfield\\Data\"", options.GameFolder);
            return null;
        }

        var settings = SettingsManager.Instance;
        if (settings.HasValidGamePath()) return settings.StarfieldDataPath!;

        var auto = settings.TryAutoDetectGamePath();
        if (auto != null && SettingsManager.IsDataFolder(auto)) return auto;

        Log.Error("Starfield's Data folder wasn't found (no saved game path in the window and no Steam or Xbox install found). " +
                  "Give it with --game \"<Starfield folder>\\Data\", or set it once in the window with File > Set Game Path.");
        return null;
    }

    private static List<ExportItem<SoundEffect>>? SoundItems(ExportOptions options, string dataPath,
        out Func<SoundEffect, byte[]> find)
    {
        Log.Information("Reading sound archives in {DataPath}...", dataPath);
        var index = SoundIndex.Build(dataPath);
        find = sound => index.Files.TryGetValue(sound.Id, out var file)
            ? file.GetBytes()
            : throw new FileNotFoundException($"Sound {sound.Id} is not in the sound archives any more.");

        IEnumerable<SoundEffect> sounds = index.Sounds;
        if (options.Group != null)
        {
            var groups = SoundCatalog.Groups(index.Sounds);
            var group = groups.FirstOrDefault(g =>
                string.Equals(g.Group, options.Group, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(g.ModSource, options.Group, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                Log.Error("No sound group or mod archive is called \"{Group}\". The groups are: {Groups}. Mod archives: {Mods}",
                    options.Group,
                    string.Join(", ", groups.Where(g => g.Group != null).Select(g => g.Group)),
                    string.Join(", ", groups.Where(g => g.ModSource != null).Select(g => g.ModSource).DefaultIfEmpty("none")));
                return null;
            }

            sounds = sounds.Where(group.Contains);
        }

        var terms = SoundCatalog.Terms(options.Search);
        return ExportLayout.ForSounds(sounds.Where(s => SoundCatalog.Matches(s, terms)), !options.Flat);
    }

    private static List<ExportItem<VoiceLine>>? VoiceItems(ExportOptions options, string dataPath, CancellationToken cancel,
        out Func<VoiceLine, byte[]> find)
    {
        var includeAllModVoices = SettingsManager.Instance.IncludeAllModVoices;
        var archives = VoiceArchiveManager.Shared(dataPath, includeAllModVoices);
        find = line => (archives.FindFile(line.Filename)
                        ?? throw new FileNotFoundException(
                            $"Could not find {line.Filename} in the voice archives for {line.ModName}")).GetBytes();

        var tree = LoadVoiceTree(dataPath, includeAllModVoices, !string.IsNullOrWhiteSpace(options.Search), cancel);
        var voiceTypes = tree.SelectMany(m => m.VoiceTypes).ToList();

        string? plugin = null;
        if (options.Plugin != null)
        {
            plugin = tree.FirstOrDefault(m => string.Equals(m.Filename, options.Plugin, StringComparison.OrdinalIgnoreCase))?.Filename;
            if (plugin == null)
            {
                Log.Error("No plugin with voice lines is called \"{Plugin}\". Plugins with voice lines: {Plugins}",
                    options.Plugin, string.Join(", ", tree.Select(m => m.Filename)));
                return null;
            }

            voiceTypes = voiceTypes.Where(vt => vt.FromMaster.Equals(plugin, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        string? voiceType = null;
        if (options.VoiceType != null)
        {
            voiceType = voiceTypes.FirstOrDefault(vt => vt.EditorId.Equals(options.VoiceType, StringComparison.OrdinalIgnoreCase))?.EditorId;
            if (voiceType == null)
            {
                Log.Error("No voice type \"{VoiceType}\"{InPlugin}. {Hint}", options.VoiceType,
                    plugin == null ? "" : $" in {plugin}",
                    VoiceTypeHint.For(tree, plugin, options.VoiceType));
                return null;
            }

            voiceTypes = voiceTypes.Where(vt => vt.EditorId.Equals(voiceType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        IEnumerable<VoiceLine> lines;
        if (string.IsNullOrWhiteSpace(options.Search))
        {
            lines = voiceTypes.SelectMany(vt => vt.VoiceLines);
        }
        else
        {
            // the same full-text search as the Voice Lines search box
            lines = LuceneManager.Instance().FreeTextSearch(options.Search, plugin, voiceType);
        }

        return ExportLayout.ForVoices(lines, !options.Flat);
    }

    /// <summary>
    /// The voice cache when it matches the game, else the same build the window does (about 30 seconds;
    /// it saves the cache and the search index too). The search index is shared with the window, so a
    /// run that needs it stops when the window is open, and an empty index is filled from the cache.
    /// </summary>
    private static List<Master> LoadVoiceTree(string dataPath, bool includeAllModVoices, bool search, CancellationToken cancel)
    {
        var fingerprint = GameFingerprint.Compute(dataPath, includeAllModVoices);
        var cached = new VoiceLineTreeCacheManager().TryToLoadCache(fingerprint);
        var windowRunning = WindowRunning();
        var action = VoiceIndexPlan.Choose(cached != null, search, windowRunning, () => LuceneManager.Instance().DocumentCount);
        Log.Debug("Voice cache {Cache}, --search {Search}, window open {Window}: {Action}",
            cached != null ? "current" : "missing or out of date", search, windowRunning, action);

        switch (action)
        {
            case VoiceIndexAction.StopWindowOpen:
                throw new ExportStoppedException(VoiceIndexPlan.WindowOpenMessage);
            case VoiceIndexAction.Rebuild:
                Log.Information("Reading the plugins' voice lines (about 30 seconds; the window does the same on its first start)...");
                LuceneManager.Instance().DeleteIndex();
                return new DialogueTreeBuilder().BuildTree(dataPath, fingerprint, includeAllModVoices, new LogProgress(), cancel);
            case VoiceIndexAction.Refill:
                Log.Warning("The voice line search index is empty; filling it from the cache before searching");
                var index = LuceneManager.Instance();
                foreach (var line in cached!.SelectMany(m => m.VoiceTypes).SelectMany(vt => vt.VoiceLines))
                {
                    index.AddDocumentWithoutCommitting(line);
                }

                index.CommitWrites();
                break;
        }

        Log.Information("Voice lines loaded from the cache ({Plugins} plugins)", cached!.Count);
        return cached;
    }

    private static bool WindowRunning()
    {
        System.Diagnostics.Process[] processes;
        int mySession;
        try
        {
            using var me = System.Diagnostics.Process.GetCurrentProcess();
            mySession = me.SessionId;
            processes = System.Diagnostics.Process.GetProcessesByName(VoiceIndexPlan.WindowProcessName);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not list processes; assuming the window is closed");
            return false;
        }

        var myAccount = AccountOf(Environment.ProcessId);
        var windows = new List<VoiceIndexPlan.WindowProcess>();
        try
        {
            foreach (var p in processes)
            {
                bool? sameSession = null;
                try
                {
                    sameSession = p.SessionId == mySession;
                }
                catch (Exception)
                {
                    // the process is ending, or its session can't be read (any reason: the session is then unknown)
                }

                var owner = AccountOf(p.Id);
                bool? sameAccount = owner != null && myAccount != null ? owner == myAccount : null;
                windows.Add(new VoiceIndexPlan.WindowProcess(sameAccount, sameSession));
                Log.Debug("StarfieldVT process {Id}: this account {Account}, this session {Session}",
                    p.Id, sameAccount?.ToString() ?? "unknown", sameSession?.ToString() ?? "unknown");
            }
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }

        return VoiceIndexPlan.AnyOfMine(windows);
    }

    /// <summary>The account a process runs under (a Windows SID, or a Linux uid), or null when it can't be read.</summary>
    private static string? AccountOf(int pid)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return WindowsAccountOf(pid);
            if (OperatingSystem.IsLinux())
            {
                var uid = File.ReadLines($"/proc/{pid}/status").FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal));
                return uid?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read the account of process {Id}", pid);
        }

        return null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? WindowsAccountOf(int pid)
    {
        const uint ProcessQueryLimitedInformation = 0x1000, TokenQuery = 0x0008;
        var process = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
        if (process == IntPtr.Zero) return null; // usually another account's process
        try
        {
            if (!OpenProcessToken(process, TokenQuery, out var token)) return null;
            try
            {
                using var identity = new System.Security.Principal.WindowsIdentity(token);
                return identity.User?.Value;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>The export can't run; the message says why and what to do (exit code 4, no stack trace).</summary>
    private sealed class ExportStoppedException(string message) : Exception(message);

    private static int Export<T>(ExportOptions options, List<ExportItem<T>>? items, IReadOnlyList<string> columns,
        Action<T, string> convert, CancellationToken cancel)
    {
        if (items == null) return ExportOptions.ExitBadOptions;
        var what = options.Kind == ExportKind.Sounds ? "sounds" : "voice lines";

        if (options.List)
        {
            // show the names a run into this folder would use (another item's file there means a hashed name)
            var planned = options.OutFolder != null
                ? BulkExporter.PlannedPaths(items, options.OutFolder, options.NoCsv ? null : columns)
                : items.Select(i => new PlannedPath<T>(i, null)).ToList();
            Console.WriteLine($"{items.Count:N0} {what} would be exported ({(options.Flat ? "flat" : "game folders")}, list.csv {(options.NoCsv ? "off" : "on")}):");
            foreach (var p in planned.Take(ListRows))
            {
                Console.WriteLine(p.Problem == null
                    ? $"  {p.Item.RelativePath}  ({p.Item.Description})"
                    : $"  {p.Item.RelativePath}  ({p.Item.Description}) WILL FAIL: {p.Problem}");
            }

            if (items.Count > ListRows) Console.WriteLine($"  ... and {items.Count - ListRows:N0} more");
            var failing = planned.Count(p => p.Problem != null);
            if (failing > 0) Console.WriteLine($"{failing:N0} of them would fail in this folder; export into a new folder.");
            return ExportOptions.ExitOk;
        }

        if (items.Count == 0)
        {
            Log.Warning("No {What} match these options, so nothing was exported", what);
            return ExportOptions.ExitOk;
        }

        var bulk = new BulkExportOptions(options.NoCsv ? null : columns, options.Flat ? "flat" : "game folders");
        var summary = BulkExporter.Run(items, options.OutFolder!, bulk, convert, new ConsoleProgress(), cancel);
        if (!Console.IsErrorRedirected) Console.Error.Write("\r" + new string(' ', 60) + "\r");
        Console.WriteLine(summary.ToString());
        if (summary.CsvPath != null) Console.WriteLine($"List of files: {summary.CsvPath}");

        return summary.Cancelled ? ExportOptions.ExitCancelled
            : summary.Failed > 0 ? ExportOptions.ExitSomeFailed
            : ExportOptions.ExitOk;
    }

    /// <summary>Shows "Exporting 1,204 of 35,491..." on the console at most four times a second (not when redirected).</summary>
    private sealed class ConsoleProgress : IProgress<ExportProgress>
    {
        private long _last;

        public void Report(ExportProgress value)
        {
            if (Console.IsErrorRedirected) return;
            var now = Environment.TickCount64;
            if (value.Done < value.Total && now - _last < 250) return;
            _last = now;
            Console.Error.Write($"\rExporting {value.Done:N0} of {value.Total:N0}...");
        }
    }

    /// <summary>Logs the voice line build one plugin at a time (Debug for each plugin).</summary>
    private sealed class LogProgress : IProgress<EsmLoadingProgress>
    {
        private string _lastEsm = "";

        public void Report(EsmLoadingProgress value)
        {
            if (value.Message != null)
            {
                Log.Debug("{Message}", value.Message);
            }
            else if (value.EsmName != _lastEsm)
            {
                _lastEsm = value.EsmName;
                Log.Debug("Reading {Plugin} (plugin {Number} of {Count})", value.EsmName, value.PluginNumber, value.PluginCount);
            }
        }
    }
}
