using System.Text;

namespace StarfieldVT.Core.Tests;

public class LoadOrderFallbackTests
{
    [Fact]
    public void OrderAfterMasters_PutsMastersFirstAndSkipsListedAndMissing()
    {
        var listed = new[] { "Starfield.esm", "ShatteredSpace.esm" };
        var candidates = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["starfield.esm"] = [],
            ["AstridCrew - Patch.esm"] = ["Starfield.esm", "AstridCrew.esm"],
            ["AstridCrew.esm"] = ["Starfield.esm"],
            ["Tweak.esp"] = ["Starfield.esm"],
            ["Light.esl"] = ["Starfield.esm"],
            ["Orphan.esm"] = ["Starfield.esm", "Missing.esm"],
        };

        var ordered = LoadOrderFallback.OrderAfterMasters(listed, candidates, out var missing);

        Assert.Equal(["AstridCrew.esm", "AstridCrew - Patch.esm", "Light.esl", "Tweak.esp"], ordered);
        Assert.Equal([("Orphan.esm", "Missing.esm")], missing);
    }

    [Fact]
    public void ReadMasters_ReadsMastSubrecords()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "Test.esm");
        File.WriteAllBytes(path, BuildHeader(("HEDR", new byte[12]), ("MAST", Encoding.ASCII.GetBytes("Starfield.esm\0")),
            ("DATA", new byte[8]), ("MAST", Encoding.ASCII.GetBytes("AstridCrew.esm\0")), ("SNAM", Encoding.ASCII.GetBytes("x\0"))));

        Assert.Equal(["Starfield.esm", "AstridCrew.esm"], LoadOrderFallback.ReadMasters(path));
    }

    [Fact]
    public void ReadMasters_RejectsNonPluginFiles()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "Fake.esm");
        File.WriteAllText(path, "not a plugin at all");

        Assert.Throws<InvalidDataException>(() => LoadOrderFallback.ReadMasters(path));
    }

    [Fact]
    public void FindUnlistedPlugins_SkipsUnreadablePlugins()
    {
        using var dir = new TempDir();
        File.WriteAllBytes(Path.Combine(dir.Path, "Good.esm"), BuildHeader());
        File.WriteAllText(Path.Combine(dir.Path, "Bad.esm"), "garbage");

        Assert.Equal(["Good.esm"], LoadOrderFallback.FindUnlistedPlugins(dir.Path, []));
    }

    private static byte[] BuildHeader(params (string Type, byte[] Data)[] subrecords)
    {
        var body = new MemoryStream();
        foreach (var (type, data) in subrecords)
        {
            body.Write(Encoding.ASCII.GetBytes(type));
            body.Write(BitConverter.GetBytes((ushort)data.Length));
            body.Write(data);
        }

        var file = new MemoryStream();
        file.Write(Encoding.ASCII.GetBytes("TES4"));
        file.Write(BitConverter.GetBytes((uint)body.Length));
        file.Write(new byte[16]);
        body.WriteTo(file);
        return file.ToArray();
    }
}
