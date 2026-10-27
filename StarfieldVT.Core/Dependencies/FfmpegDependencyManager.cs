using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using FFMpegCore;

using Serilog;

using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.Core.Dependencies;

public class FfmpegDependencyManager
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string FfmpegBinaryName => IsWindows ? "ffmpeg.exe" : "ffmpeg";

    private static string FfmpegDownloadUrl =>
        IsWindows
            ? "https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v5.1/ffmpeg-5.1-win-64.zip"
            : "https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v5.1/ffmpeg-5.1-linux-64.zip";

    // SHA-256 of the release assets above (published 2023-12-28, sizes match the GitHub release metadata).
    // A download that doesn't match is rejected rather than run.
    private static string FfmpegZipSha256 =>
        IsWindows
            ? "332CC44F49131BEB35955BF198AF43345AC20652F352CCA62C3A03F4085D6BCD"
            : "95006E86821AFD6559D9D3DFFBC7C9B8427EB746EA2001EF619F23539721423A";

    public bool FfmpegOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return false;

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, FfmpegBinaryName);
            if (File.Exists(fullPath))
            {
                Log.Information("Path to ffmpeg found at {Path}", fullPath);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Make sure FFMpegCore can find ffmpeg: use the one on PATH, one next to the app (older versions
    /// downloaded it there), or download a verified copy into %LOCALAPPDATA%\StarfieldVT\ffmpeg.
    /// </summary>
    public async Task EnsureFfmpegAsync(CancellationToken cancellationToken = default)
    {
        Log.Information("Checking for {FfmpegBinary} in PATH", FfmpegBinaryName);
        if (FfmpegOnPath())
        {
            return;
        }

        foreach (var dir in new[] { AppContext.BaseDirectory, AppDataFolder.GetFfmpegDir() })
        {
            if (File.Exists(Path.Combine(dir, FfmpegBinaryName)))
            {
                Log.Information("Using ffmpeg in {Dir}", dir);
                UseFfmpegFolder(dir);
                return;
            }
        }

        await DownloadFfmpeg(cancellationToken);
        UseFfmpegFolder(AppDataFolder.GetFfmpegDir());
    }

    private static void UseFfmpegFolder(string dir) => GlobalFFOptions.Configure(options => options.BinaryFolder = dir);

    private static async Task DownloadFfmpeg(CancellationToken cancellationToken)
    {
        var installDir = AppDataFolder.GetFfmpegDir();
        var tempDir = AppDataFolder.GetTempDir();
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, $"ffmpeg-{Guid.NewGuid():N}.zip");

        Log.Information("{FfmpegBinary} not found, downloading from {Url}", FfmpegBinaryName, FfmpegDownloadUrl);
        try
        {
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            await using (var stream = await httpClient.GetStreamAsync(FfmpegDownloadUrl, cancellationToken))
            await using (var fileWriter = File.Create(zipPath))
            {
                await stream.CopyToAsync(fileWriter, cancellationToken);
            }

            string actualHash;
            await using (var zipStream = File.OpenRead(zipPath))
            {
                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(zipStream, cancellationToken));
            }

            if (!actualHash.Equals(FfmpegZipSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Downloaded ffmpeg failed verification (SHA-256 {actualHash}, expected {FfmpegZipSha256}).");
            }

            // extract only the ffmpeg binary, never anything else the zip might contain
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals(FfmpegBinaryName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException($"The ffmpeg download did not contain {FfmpegBinaryName}.");

            var ffmpegPath = Path.Combine(installDir, FfmpegBinaryName);
            var partialPath = ffmpegPath + ".partial";
            entry.ExtractToFile(partialPath, overwrite: true);
            File.Move(partialPath, ffmpegPath, overwrite: true);

            if (!OperatingSystem.IsWindows())
            {
                Log.Information("Setting executable permission on {Path}", ffmpegPath);
                File.SetUnixFileMode(ffmpegPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            Log.Information("Installed verified ffmpeg at {Path}", ffmpegPath);
        }
        finally
        {
            try
            {
                File.Delete(zipPath);
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "Could not delete {ZipPath}", zipPath);
            }
        }
    }
}
