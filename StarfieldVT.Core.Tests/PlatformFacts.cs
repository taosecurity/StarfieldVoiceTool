namespace StarfieldVT.Core.Tests;

/// <summary>A test that runs only off Windows (Linux, macOS); the runner reports it as skipped on Windows.</summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Off-Windows only: checks how paths without drive letters are read.";
        }
    }
}
