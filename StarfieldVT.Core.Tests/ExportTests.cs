using System.Text;

using StarfieldVT.Core.Export;
using StarfieldVT.Core.Models;
using StarfieldVT.Core.Sounds;

namespace StarfieldVT.Core.Tests;

/// <summary>Bulk export: layout, list.csv and the exporter loop, with synthetic data only.</summary>
public class ExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svt-export-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static SoundEffect Sound(uint id, string name, string group = "WPN", string folder = "", string? shortName = "x",
        string source = "Starfield - WwiseSounds01.ba2") => new()
    {
        Id = id, Name = name, Group = group, Folder = folder, ShortName = shortName, Source = source,
    };

    private static VoiceLine Voice(string file, string plugin = "Starfield.esm", string voiceType = "NPCFAstrid", string dialogue = "Hello") =>
        new(file, dialogue, plugin, voiceType);

    private static string P(params string[] parts) => Path.Combine(parts);

    // ---------- Layout ----------

    [Fact]
    public void SoundsKeepTheGameFolders()
    {
        var items = ExportLayout.ForSounds([Sound(12, "WPN_Hand_Microgun_Spin", "WPN", "Hand\\Microgun")], keepFolders: true);
        Assert.Equal(P("WPN", "Hand", "Microgun", "WPN_Hand_Microgun_Spin_12.wav"), items[0].RelativePath);
    }

    [Fact]
    public void SoundsWithNoFolderSitInTheirGroup()
    {
        var items = ExportLayout.ForSounds([Sound(3, "AMB_Wind", "AMB", "")], keepFolders: true);
        Assert.Equal(P("AMB", "AMB_Wind_3.wav"), items[0].RelativePath);
    }

    [Fact]
    public void UnnamedSoundsGoUnderUnnamed()
    {
        var items = ExportLayout.ForSounds([Sound(80667371, "80667371", "Other", "", shortName: null)], keepFolders: true);
        Assert.Equal(P(ExportLayout.UnnamedFolder, "80667371.wav"), items[0].RelativePath);
    }

    [Fact]
    public void FlatModeUsesOneFolder()
    {
        var items = ExportLayout.ForSounds([Sound(12, "A", "WPN", "Hand\\Microgun"), Sound(13, "B", shortName: null)], keepFolders: false);
        Assert.Equal(["A_12.wav", "B_13.wav"], items.Select(i => i.RelativePath));

        var voices = ExportLayout.ForVoices([Voice("sound\\voice\\starfield.esm\\npcfastrid\\0001a2b3.wem")], keepFolders: false);
        Assert.Equal("NPCFAstrid_0001a2b3.wav", voices[0].RelativePath);
    }

    [Fact]
    public void VoicesUsePluginAndVoiceTypeFolders()
    {
        var items = ExportLayout.ForVoices([Voice("sound/voice/starfield.esm/npcfastrid/0001a2b3.wem")], keepFolders: true);
        Assert.Equal(P("Starfield.esm", "NPCFAstrid", "NPCFAstrid_0001a2b3.wav"), items[0].RelativePath);
        Assert.Equal(["Starfield.esm", "NPCFAstrid", "sound/voice/starfield.esm/npcfastrid/0001a2b3.wem", "Hello"], items[0].CsvRow);
    }

    [Fact]
    public void NameClashesGetNumbers()
    {
        // same name and ID from two sources, and a case-only difference, as Windows sees it
        var items = ExportLayout.ForSounds(
            [Sound(5, "Boom"), Sound(5, "Boom", source: "Mod.ba2"), Sound(5, "BOOM", source: "Other.ba2")], keepFolders: false);
        Assert.Equal(["Boom_5.wav", "Boom_5_2.wav", "BOOM_5_3.wav"], items.Select(i => i.RelativePath));

        var voices = ExportLayout.ForVoices([Voice("a/x.wem"), Voice("b/x.wem")], keepFolders: true);
        Assert.Equal(P("Starfield.esm", "NPCFAstrid", "NPCFAstrid_x_2.wav"), voices[1].RelativePath);
    }

    [Theory]
    [InlineData("..\\..\\x")]
    [InlineData("../../x")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("C:")]
    [InlineData("\\\\server\\share")]
    [InlineData("..")]
    [InlineData("a<b>c|d?e*f\"g")]
    public void HostileNamesStayInsideTheFolder(string hostile)
    {
        var root = Path.Combine(_dir, "out");
        var sounds = ExportLayout.ForSounds([Sound(1, hostile, hostile, hostile)], keepFolders: true);
        var voices = ExportLayout.ForVoices([new VoiceLine(hostile + ".wem", "", hostile, hostile)], keepFolders: true);
        foreach (var path in sounds.Select(i => i.RelativePath).Concat(voices.Select(i => i.RelativePath)))
        {
            Assert.False(Path.IsPathRooted(path), path);
            Assert.DoesNotContain(path.Split(Path.DirectorySeparatorChar), p => p is "" or "." or ".." || p.Contains(':'));
            var full = ExportLayout.ResolveInside(root, path);
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, full);
        }
    }

    [Fact]
    public void ReservedDeviceNamesAreRenamed()
    {
        Assert.Equal("_CON", ExportLayout.SafePart("CON"));
        Assert.Equal("_nul.wav", ExportLayout.SafePart("nul.wav"));
        Assert.Equal("_COM1", ExportLayout.SafePart("COM1"));
        Assert.Equal("CONSOLE", ExportLayout.SafePart("CONSOLE"));
        Assert.Equal("_", ExportLayout.SafePart(" . "));

        var voices = ExportLayout.ForVoices([new VoiceLine("x.wem", "", "AUX", "PRN")], keepFolders: true);
        Assert.Equal(P("_AUX", "_PRN", "PRN_x.wav"), voices[0].RelativePath);
    }

    [Fact]
    public void ResolveInsideRefusesEscapes()
    {
        var root = Path.Combine(_dir, "out");
        Assert.Throws<InvalidOperationException>(() => ExportLayout.ResolveInside(root, P("..", "evil.wav")));
        Assert.Throws<InvalidOperationException>(() => ExportLayout.ResolveInside(root, Path.GetFullPath(P(_dir, "evil.wav"))));
        Assert.Throws<InvalidOperationException>(() => ExportLayout.ResolveInside(root, P("..", "out2", "evil.wav")));
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "a", "b.wav"), ExportLayout.ResolveInside(root, P("a", "b.wav")));
    }

    [Fact]
    public void ExportFileNameUsesTheSharedCleaning()
    {
        Assert.Equal("a_b_c__5.wav", SoundCatalog.ExportFileName(Sound(5, "a:b/c?")));
        Assert.Equal(ExportLayout.CleanName("a:b/c?") + "_5.wav", SoundCatalog.ExportFileName(Sound(5, "a:b/c?")));
    }

    [Fact]
    public void SoundCsvRowHasEveryColumn()
    {
        var sound = Sound(7, "MUS_A", "MUS", "Lounge");
        sound.Events = ["MUS_X", "MUS_Y"];
        sound.Bank = "Starfield_MUS";
        sound.Replaces = ["Starfield - WwiseSounds01.ba2"];
        sound.Source = "Mod.ba2";
        var item = ExportLayout.ForSounds([sound], keepFolders: true)[0];
        Assert.Equal(ExportLayout.SoundColumns.Count, item.CsvRow.Count + 1);
        Assert.Equal(["7", "MUS_A", "MUS", "Lounge", "MUS_X; MUS_Y", "Starfield_MUS", "Mod.ba2", "Starfield - WwiseSounds01.ba2"], item.CsvRow);
    }

    // ---------- CSV ----------

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("cr\rhere", "\"cr\rhere\"")]
    [InlineData("", "")]
    public void CsvQuoting(string cell, string expected) => Assert.Equal(expected, ExportCsv.Escape(cell));

    [Fact]
    public void CsvHasBomAndCrlf()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "list.csv");
        ExportCsv.Write(path, ["File", "Dialogue"], [["a.wav", "Hi, there"], ["b.wav", "é"]]);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("File,Dialogue\r\na.wav,\"Hi, there\"\r\nb.wav,é\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }

    // ---------- Exporter ----------

    private static void FakeConvert(SoundEffect sound, string path)
    {
        if (sound.Name == "bad") throw new InvalidDataException("corrupt .wem");
        File.WriteAllText(path, "RIFF" + sound.Id);
    }

    private sealed class SyncProgress : IProgress<ExportProgress>
    {
        public List<ExportProgress> Reports { get; } = [];
        public void Report(ExportProgress value) => Reports.Add(value);
    }

    private static readonly BulkExportOptions WithCsv = new(ExportLayout.SoundColumns);

    [Fact]
    public void ExporterWritesFilesAndCsv()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A", "WPN", "Hand"), Sound(2, "B", "AMB")], keepFolders: true);
        var progress = new SyncProgress();
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert, progress);

        Assert.Equal((2, 2, 0, 0, false), (summary.Total, summary.Written, summary.Kept, summary.Failed, summary.Cancelled));
        Assert.Equal("RIFF1", File.ReadAllText(Path.Combine(_dir, "WPN", "Hand", "A_1.wav")));
        Assert.True(File.Exists(Path.Combine(_dir, "AMB", "B_2.wav")));
        Assert.Equal([1, 2], progress.Reports.Select(r => r.Done));
        Assert.All(progress.Reports, r => Assert.Equal(2, r.Total));

        var lines = File.ReadAllLines(summary.CsvPath!);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("File,ID,Name,Group", lines[0].TrimStart('\uFEFF'));
        Assert.StartsWith(P("WPN", "Hand", "A_1.wav") + ",1,A,WPN,Hand", lines[1]);
        Assert.Empty(Directory.GetFiles(_dir, "*" + BulkExporter.PartialSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public void ExporterKeepsExistingFiles()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A"), Sound(2, "B")], keepFolders: false);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "A_1.wav"), "old");

        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);

        Assert.Equal((1, 1), (summary.Written, summary.Kept));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_dir, "A_1.wav")));
        Assert.Equal(3, File.ReadAllLines(summary.CsvPath!).Length); // kept files are listed too
    }

    [Fact]
    public void OneFailureDoesNotStopTheRest()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A"), Sound(2, "bad"), Sound(3, "C")], keepFolders: false);
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);

        Assert.Equal((2, 1), (summary.Written, summary.Failed));
        var failure = Assert.Single(summary.Failures);
        Assert.Equal("bad_2.wav", failure.RelativePath);
        Assert.Contains("corrupt", failure.Message);
        Assert.False(File.Exists(Path.Combine(_dir, "bad_2.wav")));
        Assert.False(File.Exists(Path.Combine(_dir, "bad_2.wav" + BulkExporter.PartialSuffix)));

        var csv = File.ReadAllText(summary.CsvPath!);
        Assert.DoesNotContain("bad_2.wav", csv); // only files that exist are listed
        Assert.Contains("C_3.wav", csv);
    }

    [Fact]
    public void FailedConversionLeavesNoPartialFile()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false);
        var summary = BulkExporter.Run(items, _dir, WithCsv, (_, path) =>
        {
            File.WriteAllText(path, "half");
            throw new IOException("disk full");
        });

        Assert.Equal(1, summary.Failed);
        Assert.Empty(Directory.GetFiles(_dir, "*.wav*"));

        // the next run converts it again instead of keeping a broken file
        var again = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);
        Assert.Equal((1, 0), (again.Written, again.Kept));
    }

    [Fact]
    public void CancelStopsCleanlyAndWritesTheCsv()
    {
        var items = ExportLayout.ForSounds(Enumerable.Range(1, 10).Select(i => Sound((uint)i, "S")), keepFolders: false);
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress();
        var summary = BulkExporter.Run(items, _dir, WithCsv, (s, path) =>
        {
            FakeConvert(s, path);
            if (s.Id == 4) cts.Cancel(); // the file in progress finishes, then the loop stops
        }, progress, cts.Token);

        Assert.True(summary.Cancelled);
        Assert.Equal((10, 4, 0, 0, 6), (summary.Total, summary.Written, summary.Kept, summary.Failed, summary.NotReached));
        Assert.Equal(4, progress.Reports.Count);
        Assert.Equal(5, File.ReadAllLines(summary.CsvPath!).Length);
        Assert.Contains("cancelled with 6 not done", summary.ToString());
    }

    [Fact]
    public void AlreadyCancelledWritesNothingButTheCsv()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false);
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert, cancel: new CancellationToken(canceled: true));
        Assert.True(summary.Cancelled);
        Assert.Equal(0, summary.Written);
        Assert.Single(File.ReadAllLines(summary.CsvPath!));
    }

    [Fact]
    public void NoCsvWhenTurnedOff()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false);
        var summary = BulkExporter.Run(items, _dir, new BulkExportOptions(null, "flat"), FakeConvert);
        Assert.Null(summary.CsvPath);
        Assert.False(File.Exists(Path.Combine(_dir, ExportCsv.FileName)));
    }

    [Fact]
    public void HostileItemPathFailsThatItemOnly()
    {
        var good = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false)[0];
        var evil = good with { RelativePath = P("..", "evil.wav") };
        var summary = BulkExporter.Run([evil, good], Path.Combine(_dir, "out"), WithCsv, FakeConvert);

        Assert.Equal((1, 1), (summary.Written, summary.Failed));
        Assert.False(File.Exists(Path.Combine(_dir, "evil.wav")));
        Assert.Contains("outside the export folder", summary.Failures[0].Message);
    }

    [Fact]
    public void VoicesExportThroughTheSameLoop()
    {
        var items = ExportLayout.ForVoices([Voice("a/0001.wem", dialogue: "Hi, \"you\"")], keepFolders: true);
        var summary = BulkExporter.Run(items, _dir, new BulkExportOptions(ExportLayout.VoiceColumns), (_, path) => File.WriteAllText(path, "RIFF"));

        Assert.Equal(1, summary.Written);
        Assert.True(File.Exists(Path.Combine(_dir, "Starfield.esm", "NPCFAstrid", "NPCFAstrid_0001.wav")));
        var lines = File.ReadAllText(summary.CsvPath!).TrimStart('\uFEFF').Split("\r\n");
        Assert.Equal("File,Plugin,Voice Type,Game File,Dialogue", lines[0]);
        Assert.EndsWith(",Starfield.esm,NPCFAstrid,a/0001.wem,\"Hi, \"\"you\"\"\"", lines[1]);
    }
}
