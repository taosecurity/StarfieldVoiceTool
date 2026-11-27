using System.IO;

namespace StarfieldVT.Core.Filesystem
{
    public static class AppDataFolder
    {
        private static readonly string _appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string GetAppDataFolder() => Path.Combine(_appDataPath, "StarfieldVT");
        public static string GetLogDir() => Path.Combine(GetAppDataFolder(), "logs");

        /// <summary>
        /// The folder for temporary conversion files. On Windows it is under the per-user %TEMP%.
        /// Elsewhere the shared /tmp is not used: the folder is under the user's own app data folder
        /// and is created so that only this user can use it (mode 0700).
        /// </summary>
        public static string GetTempDir() => GetTempDir(OperatingSystem.IsWindows(), GetAppDataFolder());

        /// <summary>Testable form of <see cref="GetTempDir()"/>.</summary>
        public static string GetTempDir(bool windows, string appDataFolder)
        {
            if (windows)
                return Path.Combine(Path.GetTempPath(), "StarfieldVT");

            var dir = Path.Combine(appDataFolder, "temp");
            if (!Directory.Exists(dir))
            {
                if (OperatingSystem.IsWindows())
                    Directory.CreateDirectory(dir);
                else
                    Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            return dir;
        }
    }
}
