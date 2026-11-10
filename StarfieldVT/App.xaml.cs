using System.IO;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.UI.Audio;

namespace StarfieldVT
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var args = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Args ?? Array.Empty<string>();
            var options = StartupOptions.Parse(args);

            var config = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console();
            config = options.Verbose ? config.MinimumLevel.Debug() : config.MinimumLevel.Information();
            if (options.LogFile != null)
            {
                var dir = Path.GetDirectoryName(options.LogFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                config = config.WriteTo.File(options.LogFile);
            }
            else
            {
                config = config.WriteTo.File(
                    Path.Combine(AppDataFolder.GetLogDir(), "starfieldvt.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 524288000);
            }
            Log.Logger = config.CreateLogger();

            Log.Information("Starfield Voice Tool {Version} starting on {OS} ({Arch})",
                typeof(App).Assembly.GetName().Version,
                System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
            Log.Information("Log level {Level}, log file {LogFile}",
                options.Verbose ? "Debug (--verbose)" : "Information",
                options.LogFile ?? Path.Combine(AppDataFolder.GetLogDir(), "starfieldvt<date>.log"));
            foreach (var error in options.Errors) Log.Warning("Command line: {Problem}", error);
            if (options.Unknown.Count > 0)
            {
                Log.Warning("Ignoring unknown command-line options {Options}; the known ones are --verbose and --log <file>",
                    options.Unknown);
            }

            // ww2ogg (BnkExtractor) reports conversion problems through its own events, which by
            // default only reach a console the Windows build doesn't have
            BnkExtractor.Logger.Warning += m => Log.Warning("ww2ogg: {Message}", m);
            BnkExtractor.Logger.Error += m => Log.Error("ww2ogg: {Message}", m);
            BnkExtractor.Logger.Fatal += m => Log.Fatal("ww2ogg: {Message}", m);
            BnkExtractor.Logger.Verbose += m => Log.Debug("ww2ogg: {Message}", m);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled error, the tool will close");
                Log.CloseAndFlush();
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
                Log.Error(e.Exception, "Unobserved background task error");
            AudioConverter.CleanTempDir();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
