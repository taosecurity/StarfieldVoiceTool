using Serilog;
using Serilog.Core;
using Serilog.Events;

using StarfieldVT.Core.Export;

namespace StarfieldVT.Core.Tests;

/// <summary>Swaps the global Serilog logger, so these tests don't run alongside others.</summary>
[CollectionDefinition("Global logger", DisableParallelization = true)]
public class GlobalLoggerCollection;

/// <summary>
/// PLAN section 4: at --verbose (Debug) a bulk export logs one line per file. ww2ogg's own trace
/// (about 100 lines per sound in the real 35,491-sound run, 2026-11-21) goes below that level.
/// </summary>
[Collection("Global logger")]
public class ExportLoggingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svt-export-log-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _previous = Log.Logger;
    private readonly List<LogEvent> _events = [];

    public ExportLoggingTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(new ListSink(_events)).CreateLogger();
    }

    public void Dispose()
    {
        Log.Logger = _previous;
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            lock (events) events.Add(logEvent);
        }
    }

    // converts like AudioConverter does: ww2ogg trace and the converter's own steps, then the file
    private static void ChattyConvert(string text, string path)
    {
        Log.Verbose("Converting {Codec} .wem ({Bytes} bytes) to {WavPath}", "Wwise Vorbis", 10, path);
        for (var i = 0; i < 100; i++) ConverterLog.Trace($"Codebook with 1 dimensions {i}");
        File.WriteAllText(path, text);
    }

    [Fact]
    public void VerboseLogsOneLinePerFile()
    {
        var items = Enumerable.Range(1, 5)
            .Select(i => new ExportItem<string>($"f{i}.wav", [$"{i}"], $"data {i}", $"item {i}"))
            .ToList();
        var summary = BulkExporter.Run(items, _dir, new BulkExportOptions(["File", "N"]), ChattyConvert);

        Assert.Equal(5, summary.Written);
        var debug = _events.Where(e => e.Level == LogEventLevel.Debug).ToList();
        Assert.Equal(5, debug.Count);
        Assert.All(debug, e => Assert.StartsWith("Wrote ", e.RenderMessage()));
        Assert.DoesNotContain(_events, e => e.RenderMessage().Contains("ww2ogg"));
    }

    [Fact]
    public void ConverterProblemsStillShow()
    {
        ConverterLog.Warning("bad packet");
        ConverterLog.Error("no codebook");
        ConverterLog.Fatal("gave up");
        ConverterLog.Trace("Total bits read = 280");

        Assert.Equal(
            ["Warning ww2ogg: \"bad packet\"", "Error ww2ogg: \"no codebook\"", "Fatal ww2ogg: \"gave up\""],
            _events.Select(e => $"{e.Level} {e.RenderMessage()}"));
    }
}
