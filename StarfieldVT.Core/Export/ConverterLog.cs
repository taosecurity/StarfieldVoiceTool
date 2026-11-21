using Serilog;

namespace StarfieldVT.Core.Export;

/// <summary>
/// Where ww2ogg's (BnkExtractor's) log events go. Its own trace is about 100 lines per sound
/// (codebook details), so it goes to Serilog's Verbose level, below --verbose (Debug): a bulk
/// export at Debug then logs one line per file, not millions. Warnings and errors still show.
/// The window and starfieldvt-export both hook BnkExtractor.Logger's events to these methods.
/// </summary>
public static class ConverterLog
{
    public static void Trace(string message) => Log.Verbose("ww2ogg: {Message}", message);

    public static void Warning(string message) => Log.Warning("ww2ogg: {Message}", message);

    public static void Error(string message) => Log.Error("ww2ogg: {Message}", message);

    public static void Fatal(string message) => Log.Fatal("ww2ogg: {Message}", message);
}
