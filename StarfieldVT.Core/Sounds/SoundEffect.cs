namespace StarfieldVT.Core.Sounds;

/// <summary>One Wwise sound (a .wem under sound/soundbanks), as the game would play it.</summary>
public sealed class SoundEffect
{
    public uint Id { get; set; }

    /// <summary>The source file name without ".wav", or the ID when no bank names the sound.</summary>
    public string Name { get; set; } = "";

    /// <summary>Wwise's ShortName, e.g. "WPN\Hand\Microgun\WPN_Hand_AlliedMicrogun_SpinLoop.wav"; null if unnamed.</summary>
    public string? ShortName { get; set; }

    /// <summary>First folder of the ShortName (AMB, WPN, MUS, ...), or "Other".</summary>
    public string Group { get; set; } = "";

    /// <summary>The ShortName's folder below the group, e.g. "Hand\Microgun"; empty if none.</summary>
    public string Folder { get; set; } = "";

    /// <summary>The bank that names the sound, e.g. "Starfield_WPN" or a mod's bank.</summary>
    public string? Bank { get; set; }

    /// <summary>The archive file name (e.g. "Starfield - WwiseSoundsPatch.ba2") or "Data folder" for a loose file.</summary>
    public string Source { get; set; } = "";

    /// <summary>The file's path inside the source, e.g. "sound/soundbanks/80667371.wem".</summary>
    public string FilePath { get; set; } = "";

    /// <summary>True unless the file comes from a base game archive ("Starfield - *.ba2").</summary>
    public bool FromMod { get; set; }

    /// <summary>Earlier sources that hold the same sound ID, which this one replaces.</summary>
    public List<string> Replaces { get; set; } = [];

    /// <summary>Wwise events that can play this sound, e.g. "MUS_AstralLounge".</summary>
    public List<string> Events { get; set; } = [];

    /// <summary>The events as one line for the table.</summary>
    public string EventsText => string.Join(", ", Events);

    /// <summary>For the table: what this sound replaces, e.g. "replaces Starfield - WwiseSounds01.ba2".</summary>
    public string ReplacesText => Replaces.Count == 0 ? "" : "replaces " + string.Join(", ", Replaces);

    public override string ToString() => $"{Id} {Name} ({Source})";
}
