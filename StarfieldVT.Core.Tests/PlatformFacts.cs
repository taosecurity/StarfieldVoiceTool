namespace StarfieldVT.Core.Tests;

/// <summary>A test that runs only on Linux and macOS; the runner reports it as skipped on Windows.</summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Unix-only: checks case-sensitive or non-drive path behavior.";
        }
    }
}

/// <summary>A test that runs only on Windows; the runner reports it as skipped elsewhere.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only: checks drive and case-insensitive path behavior.";
        }
    }
}
