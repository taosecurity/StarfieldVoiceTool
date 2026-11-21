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
    public void CsvGroupMatchesTheWindowsGroupList()
    {
        var game = Sound(1, "A", "WPN", "Hand");
        var dlc = Sound(2, "B", "WPN", "Hand", source: "ShatteredSpace - Main01.ba2");
        dlc.FromMod = true;
        var items = ExportLayout.ForSounds([game, dlc], keepFolders: true);

        var labels = SoundCatalog.Groups([game, dlc]).Skip(1).Select(g => g.Label).ToList();
        Assert.Equal(["WPN (1)", "Mod: ShatteredSpace - Main01.ba2 (1)"], labels);
        Assert.Equal("WPN", items[0].CsvRow[2]);
        Assert.Equal("Mod: ShatteredSpace - Main01.ba2", items[1].CsvRow[2]);
        Assert.Equal(["WPN", "WPN"], items.Select(i => i.CsvRow[3])); // Wwise Group keeps the folder group
        Assert.Equal("Wwise Group", ExportLayout.SoundColumns[4]);

        // A Group value from list.csv works as --group, with or without "Mod: ".
        var groups = SoundCatalog.Groups([game, dlc]);
        Assert.Equal("ShatteredSpace - Main01.ba2", SoundCatalog.FindGroup(groups, "Mod: ShatteredSpace - Main01.ba2")?.ModSource);
        Assert.Equal("ShatteredSpace - Main01.ba2", SoundCatalog.FindGroup(groups, "shatteredspace - main01.ba2")?.ModSource);
        Assert.Equal("WPN", SoundCatalog.FindGroup(groups, "wpn")?.Group);
        Assert.Null(SoundCatalog.FindGroup(groups, "Mod: WPN"));
        // The file still goes under its Wwise folder, as the game lays it out.
        Assert.Equal(P("WPN", "Hand", "B_2.wav"), items[1].RelativePath);
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
    public void NameClashesGetTheirSource()
    {
        // same name and ID from three sources, one a case-only difference, as Windows sees it
        var items = ExportLayout.ForSounds(
            [Sound(5, "Boom"), Sound(5, "Boom", source: "Mod.ba2"), Sound(5, "BOOM", source: "Other.ba2"), Sound(6, "Quiet")],
            keepFolders: false);
        Assert.Equal(["Boom_5_Starfield - WwiseSounds01.wav", "Boom_5_Mod.wav", "BOOM_5_Other.wav", "Quiet_6.wav"],
            items.Select(i => i.RelativePath));

        // flat voices from two plugins: each gets its plugin
        var flat = ExportLayout.ForVoices([Voice("a/x.wem", plugin: "ModA.esm"), Voice("a/x.wem", plugin: "ModB.esm")], keepFolders: false);
        Assert.Equal(["NPCFAstrid_x_ModA.wav", "NPCFAstrid_x_ModB.wav"], flat.Select(i => i.RelativePath));
    }

    [Fact]
    public void ClashesInOnePluginGetAStableHash()
    {
        // same plugin, voice type and file name, different game paths: the plugin tag still clashes
        var voices = ExportLayout.ForVoices([Voice("a/x.wem"), Voice("b/x.wem")], keepFolders: true);
        var names = voices.Select(v => Path.GetFileName(v.RelativePath)).ToList();
        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => Assert.Matches("^NPCFAstrid_x_[0-9a-f]{8}\\.wav$", n));

        // the same in another run, in the other order
        var again = ExportLayout.ForVoices([Voice("b/x.wem"), Voice("a/x.wem")], keepFolders: true);
        Assert.Equal(voices[0].RelativePath, again[1].RelativePath);
        Assert.Equal(voices[1].RelativePath, again[0].RelativePath);

        // two items the same in every way still get paths of their own
        var twins = ExportLayout.ForVoices([Voice("a/x.wem"), Voice("a/x.wem")], keepFolders: false);
        Assert.NotEqual(twins[0].RelativePath, twins[1].RelativePath, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClashNamesDoNotDependOnWhatElseIsExported()
    {
        // run 1 exports ModA's line alone; run 2 exports every plugin with ModB first
        var alone = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var all = ExportLayout.ForVoices(
            [Voice("a/0001.wem", plugin: "ModB.esm"), Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);

        Assert.Equal("NPCFAstrid_0001.wav", alone[0].RelativePath);
        // no item of run 2 claims run 1's file, so nothing "keeps" another plugin's audio
        Assert.DoesNotContain(all, i => i.RelativePath.Equals(alone[0].RelativePath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["NPCFAstrid_0001_ModB.wav", "NPCFAstrid_0001_ModA.wav"], all.Select(i => i.RelativePath));
    }

    [Fact]
    public void ClashNamesAvoidOtherItemsPaths()
    {
        // an item whose own name is what a clash would be tagged to
        var items = ExportLayout.ForVoices(
            [Voice("x/x_ModA.wem", plugin: "Other.esm"), Voice("a/x.wem", plugin: "ModA.esm"), Voice("a/x.wem", plugin: "ModB.esm")],
            keepFolders: false);
        Assert.Equal(3, items.Select(i => i.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("NPCFAstrid_x_ModA.wav", items[0].RelativePath);
        Assert.Matches("^NPCFAstrid_x_[0-9a-f]{8}\\.wav$", items[1].RelativePath);
    }

    [Fact]
    public void StableHashIsFixed() => Assert.Equal("811c9dc5", ExportLayout.StableHash(""));

    [Fact]
    public void DriveRootIsAValidExportFolder()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(_dir))!;
        Assert.Equal(Path.Combine(root, "WPN", "a.wav"), ExportLayout.ResolveInside(root, P("WPN", "a.wav")));
        Assert.Equal(Path.Combine(root, "a.wav"), ExportLayout.ResolveInside(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, "a.wav"));
        Assert.Throws<InvalidOperationException>(() => ExportLayout.ResolveInside(root, Path.Combine("Z:" + Path.DirectorySeparatorChar, "a.wav")));
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
        Assert.Equal(["7", "MUS_A", "MUS", "MUS", "Lounge", "MUS_X; MUS_Y", "Starfield_MUS", "Mod.ba2", "Starfield - WwiseSounds01.ba2"], item.CsvRow);
    }

    // ---------- CSV ----------

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("cr\rhere", "\"cr\rhere\"")]
    [InlineData("", "")]
    [InlineData("=1+2", "'=1+2")]
    [InlineData("+SUM(A1)", "'+SUM(A1)")]
    [InlineData("- Wait, don't go!", "\"'- Wait, don't go!\"")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("=HYPERLINK(\"http://x\",\"click\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"click\"\")\"")]
    [InlineData("\tTab", "'\tTab")]
    [InlineData("a=b", "a=b")]
    [InlineData("'quoted", "''quoted")]
    public void CsvQuoting(string cell, string expected) => Assert.Equal(expected, ExportCsv.Escape(cell));

    [Fact]
    public void CsvParseReadsWhatWriteWrote()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "list.csv");
        List<IReadOnlyList<string>> rows = [["a.wav", "Hi, \"you\"\r\nnext"], ["b.wav", "=1+2"], ["c.wav", ""], ["-d.wav", "é"]];
        ExportCsv.Write(path, ["File", "Dialogue"], rows);

        var parsed = ExportCsv.Parse(File.ReadAllText(path));
        Assert.Equal(["File", "Dialogue"], parsed[0]);
        Assert.Equal(rows.Count, parsed.Count - 1);
        for (var i = 0; i < rows.Count; i++) Assert.Equal(rows[i], parsed[i + 1]);
    }

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
        Assert.StartsWith(P("WPN", "Hand", "A_1.wav") + ",1,A,WPN,WPN,Hand", lines[1]);
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
    public void AlreadyCancelledWritesNothing()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false);
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert, cancel: new CancellationToken(canceled: true));
        Assert.True(summary.Cancelled);
        Assert.Equal(0, summary.Written);
        Assert.Null(summary.CsvPath);
        Assert.False(File.Exists(Path.Combine(_dir, ExportCsv.FileName)));
    }

    [Fact]
    public void AlreadyCancelledLeavesAnEarlierListAsItIs()
    {
        // Ctrl+C while the CLI still reads the archives reaches the exporter already cancelled
        var items = ExportLayout.ForSounds([Sound(1, "A"), Sound(2, "B")], keepFolders: false);
        var first = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);
        var before = File.ReadAllText(first.CsvPath!);

        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert, cancel: new CancellationToken(canceled: true));
        Assert.Equal(first.CsvPath, summary.CsvPath);
        Assert.Equal(before, File.ReadAllText(first.CsvPath!));
    }

    [Fact]
    public void ASecondExportKeepsTheEarlierRows()
    {
        var wpn = ExportLayout.ForSounds([Sound(1, "A"), Sound(2, "B"), Sound(3, "C")], keepFolders: false);
        BulkExporter.Run(wpn, _dir, WithCsv, FakeConvert);

        // a narrower search into the same folder: one old file, one new
        var search = ExportLayout.ForSounds([Sound(2, "B", folder: "Changed"), Sound(4, "D")], keepFolders: false);
        var summary = BulkExporter.Run(search, _dir, WithCsv, FakeConvert);

        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!));
        Assert.Equal(["File", .. ExportLayout.SoundColumns.Skip(1)], rows[0]);
        Assert.Equal(["A_1.wav", "B_2.wav", "C_3.wav", "D_4.wav"], rows.Skip(1).Select(r => r[0]));
        Assert.Equal("Changed", rows[2][5]); // this run's row replaces the earlier one for the same file
    }

    [Fact]
    public void RowsWhoseFilesAreGoneAreDropped()
    {
        var items = ExportLayout.ForSounds([Sound(1, "A"), Sound(2, "B")], keepFolders: false);
        BulkExporter.Run(items, _dir, WithCsv, FakeConvert);
        File.Delete(Path.Combine(_dir, "A_1.wav"));

        var summary = BulkExporter.Run(ExportLayout.ForSounds([Sound(3, "C")], keepFolders: false), _dir, WithCsv, FakeConvert);
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!));
        Assert.Equal(["B_2.wav", "C_3.wav"], rows.Skip(1).Select(r => r[0]));
    }

    [Fact]
    public void AListWithOtherColumnsIsReplaced()
    {
        Directory.CreateDirectory(_dir);
        BulkExporter.Run(ExportLayout.ForVoices([Voice("a/1.wem")], keepFolders: false), _dir,
            new BulkExportOptions(ExportLayout.VoiceColumns), (_, path) => File.WriteAllText(path, "RIFF"));

        var summary = BulkExporter.Run(ExportLayout.ForSounds([Sound(3, "C")], keepFolders: false), _dir, WithCsv, FakeConvert);
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!));
        Assert.Equal(ExportLayout.SoundColumns, rows[0]);
        Assert.Equal(["C_3.wav"], rows.Skip(1).Select(r => r[0]));
    }

    [Fact]
    public void EarlierRowsWithHostilePathsAreDropped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "evil.wav"), "x");
        var root = Path.Combine(_dir, "out");
        Directory.CreateDirectory(root);
        ExportCsv.Write(Path.Combine(root, ExportCsv.FileName), ExportLayout.SoundColumns,
            [[P("..", "evil.wav"), "1", "", "", "", "", "", "", ""]]);

        var summary = BulkExporter.Run(ExportLayout.ForSounds([Sound(3, "C")], keepFolders: false), root, WithCsv, FakeConvert);
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!));
        Assert.Equal(["C_3.wav"], rows.Skip(1).Select(r => r[0]));
    }

    [Fact]
    public void AnExportFolderThatIsAFileGivesAClearError()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "afile");
        File.WriteAllText(file, "x");
        var ex = Assert.Throws<IOException>(() =>
            BulkExporter.Run(ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false), file, WithCsv, FakeConvert));
        Assert.Contains("Could not make the export folder " + file, ex.Message);
        Assert.Contains("Choose a folder", ex.Message);
    }

    [Fact]
    public void ListAndRunGiveTheSameFolderMessage()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "afile2");
        File.WriteAllText(file, "x");
        var problem = BulkExporter.FolderProblem(Path.Combine(file, "sub"));
        Assert.NotNull(problem);
        var ex = Assert.Throws<IOException>(() =>
            BulkExporter.Run(ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false), Path.Combine(file, "sub"), WithCsv, FakeConvert));
        Assert.Equal(problem, ex.Message);
        Assert.StartsWith(BulkExporter.FolderMessage(Path.GetFullPath(Path.Combine(file, "sub")), "").Split(" (")[0], problem);
        Assert.EndsWith("Choose a folder you can write to that isn't the name of a file.", BulkExporter.FolderMessage("x", "y"));
    }

    [Fact]
    public void SampleCancelledChecksTheTokenToo()
    {
        var done = new ExportSummary(50, 50, 0, 0, false, 1, null, []);
        Assert.False(ExportPlan.SampleCancelled(done, CancellationToken.None));
        Assert.True(ExportPlan.SampleCancelled(done, new CancellationToken(canceled: true)));
        Assert.True(ExportPlan.SampleCancelled(done with { Cancelled = true }, CancellationToken.None));
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

    // ---------- Code review round 2 ----------

    private static void VoiceConvert(VoiceLine line, string path) => File.WriteAllText(path, "RIFF " + line.ModName);

    private static readonly BulkExportOptions VoiceCsv = new(ExportLayout.VoiceColumns, "flat");

    [Fact]
    public void AnotherItemsFileIsNotKept()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);

        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false);
        Assert.Equal(a[0].RelativePath, b[0].RelativePath); // alone, each wants the bare name
        var summary = BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);

        Assert.Equal((1, 0), (summary.Written, summary.Kept));
        var moved = ExportLayout.WithSuffix(b[0].RelativePath, ExportLayout.StableHash(b[0].Description));
        Assert.Equal("RIFF ModA.esm", File.ReadAllText(Path.Combine(_dir, a[0].RelativePath)));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, moved)));

        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1).ToList();
        Assert.Equal([(a[0].RelativePath, "ModA.esm"), (moved, "ModB.esm")], rows.Select(r => (r[0], r[1])));

        // a third run of ModB keeps its own moved file
        var again = BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);
        Assert.Equal((0, 1), (again.Written, again.Kept));
        Assert.Equal(2, ExportCsv.Parse(File.ReadAllText(again.CsvPath!)).Count - 1);
    }

    [Fact]
    public void PlannedPathsShowTheHashedName()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false);
        Assert.Equal(b[0].RelativePath, BulkExporter.PlannedPaths(b, _dir, ExportLayout.VoiceColumns)[0].Item.RelativePath); // no folder yet
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);

        var planned = BulkExporter.PlannedPaths(b, _dir, ExportLayout.VoiceColumns);
        var moved = ExportLayout.WithSuffix(b[0].RelativePath, ExportLayout.StableHash(b[0].Description));
        Assert.Equal(moved, planned[0].Item.RelativePath);
        Assert.Null(planned[0].Problem);
        Assert.Equal(a[0].RelativePath, BulkExporter.PlannedPaths(a, _dir, ExportLayout.VoiceColumns)[0].Item.RelativePath);
        Assert.Equal(b[0].RelativePath, BulkExporter.PlannedPaths(b, _dir, null)[0].Item.RelativePath); // --no-csv checks nothing
        Assert.False(File.Exists(Path.Combine(_dir, moved))); // nothing written

        var summary = BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);
        Assert.True(File.Exists(Path.Combine(_dir, moved)));
        Assert.Equal(1, summary.Written);
    }

    [Fact]
    public void WithNoFreeNameTheItemFailsInsteadOfKeepingAnotherFile()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false);
        Directory.CreateDirectory(_dir);
        var hash = ExportLayout.StableHash(b[0].Description);
        var taken = new List<string> { b[0].RelativePath };
        for (var n = 1; n <= 99; n++) taken.Add(ExportLayout.WithSuffix(b[0].RelativePath, hash + (n == 1 ? "" : "_" + n)));
        foreach (var path in taken) File.WriteAllText(Path.Combine(_dir, path), "RIFF ModA.esm");
        ExportCsv.Write(Path.Combine(_dir, ExportCsv.FileName), ExportLayout.VoiceColumns,
            taken.Select(p => (IReadOnlyList<string>)[p, .. a[0].CsvRow]).ToList());

        var summary = BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);
        Assert.Equal((0, 0, 1), (summary.Written, summary.Kept, summary.Failed));
        Assert.Contains("new folder", summary.Failures[0].Message);
        Assert.All(taken, p => Assert.Equal("RIFF ModA.esm", File.ReadAllText(Path.Combine(_dir, p))));
        Assert.DoesNotContain(ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1), r => r[1] == "ModB.esm");

        // --list with --out says so, instead of showing the taken name as if it would be written
        var planned = BulkExporter.PlannedPaths(b, _dir, ExportLayout.VoiceColumns);
        Assert.Equal(b[0].RelativePath, planned[0].Item.RelativePath);
        Assert.Contains("new folder", planned[0].Problem);
    }

    [Fact]
    public void PlannedPathsReportAPathTheRunWouldRefuseInsteadOfThrowing()
    {
        var a = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);
        var rooted = a[0] with { RelativePath = Path.Combine(_dir, "elsewhere.wav") };

        var planned = BulkExporter.PlannedPaths([rooted, a[0]], _dir, ExportLayout.VoiceColumns);
        Assert.NotNull(planned[0].Problem);
        Assert.Null(planned[1].Problem);

        var summary = BulkExporter.Run([rooted], _dir, VoiceCsv, VoiceConvert);
        Assert.Equal(1, summary.Failed); // the run fails the same item
    }

    [Fact]
    public void AnItemMovedToAHashedNameIsNotWrittenAgainWhenItsFirstPathIsFreed()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false);
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);
        BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);
        var moved = ExportLayout.WithSuffix(b[0].RelativePath, ExportLayout.StableHash(b[0].Description));
        Assert.True(File.Exists(Path.Combine(_dir, moved)));

        File.Delete(Path.Combine(_dir, a[0].RelativePath)); // ModA's file is gone, so the first path is free
        Assert.Equal(moved, BulkExporter.PlannedPaths(b, _dir, ExportLayout.VoiceColumns)[0].Item.RelativePath);
        var summary = BulkExporter.Run(b, _dir, VoiceCsv, VoiceConvert);

        Assert.Equal((0, 1), (summary.Written, summary.Kept));
        Assert.False(File.Exists(Path.Combine(_dir, a[0].RelativePath)));
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1).ToList();
        Assert.Equal([(moved, "ModB.esm")], rows.Select(r => (r[0], r[1])));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AHashedNameNeverTakesAnotherItemsPathInTheSameRun(bool renamedFirst)
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false);
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);

        // item C's own layout path is exactly the hashed name item B would move to
        var moved = ExportLayout.WithSuffix(b[0].RelativePath, ExportLayout.StableHash(b[0].Description));
        var c = ExportLayout.ForVoices([Voice("sound/voice/y/other/0002.wem", plugin: "ModC.esm")], keepFolders: false)[0] with { RelativePath = moved };
        List<ExportItem<VoiceLine>> items = renamedFirst ? [b[0], c] : [c, b[0]];

        var summary = BulkExporter.Run(items, _dir, VoiceCsv, VoiceConvert);
        var second = ExportLayout.WithSuffix(b[0].RelativePath, ExportLayout.StableHash(b[0].Description) + "_2");
        Assert.Equal((2, 0, 0), (summary.Written, summary.Kept, summary.Failed));
        Assert.Equal("RIFF ModA.esm", File.ReadAllText(Path.Combine(_dir, a[0].RelativePath)));
        Assert.Equal("RIFF ModC.esm", File.ReadAllText(Path.Combine(_dir, moved)));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, second)));
        // a rerun keeps both where they are
        Assert.Equal([moved, second], BulkExporter.PlannedPaths(items, _dir, ExportLayout.VoiceColumns)
            .Select(p => p.Item.RelativePath).OrderBy(p => p.Length));
    }

    [Fact]
    public void ASampleRunLeavesThePathsOfTheRestOfTheExportFree()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false)[0];
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);
        var moved = ExportLayout.WithSuffix(b.RelativePath, ExportLayout.StableHash(b.Description));
        var second = ExportLayout.WithSuffix(b.RelativePath, ExportLayout.StableHash(b.Description) + "_2");
        var c = ExportLayout.ForVoices([Voice("sound/voice/y/other/0002.wem", plugin: "ModC.esm")], keepFolders: false)[0] with { RelativePath = moved };
        List<ExportItem<VoiceLine>> all = [b, c];

        // the window: B alone in the 50-file sample, then the whole list
        var sample = BulkExporter.Run([b], _dir, VoiceCsv, VoiceConvert, wholeExportPaths: all.Select(i => i.RelativePath));
        Assert.Equal([second], sample.PresentPaths); // C's path stays free
        Assert.Equal([(long)"RIFF ModB.esm".Length], ExportPlan.SampleSizes(_dir, sample.PresentPaths)); // measured at B's own file
        var whole = BulkExporter.Run(all, _dir, VoiceCsv, VoiceConvert);

        Assert.Equal((1, 1, 0), (whole.Written, whole.Kept, whole.Failed));
        Assert.Equal("RIFF ModC.esm", File.ReadAllText(Path.Combine(_dir, moved)));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, second)));
        Assert.Equal(3, Directory.GetFiles(_dir, "*.wav").Length); // A, B and C once each
        Assert.Equal([second, moved], whole.PresentPaths);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnItemsEarlierHashedFileIsKeptWhenAnotherItemNowWantsThatPath(bool keptFirst)
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false)[0];
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);
        BulkExporter.Run([b], _dir, VoiceCsv, VoiceConvert);
        var moved = ExportLayout.WithSuffix(b.RelativePath, ExportLayout.StableHash(b.Description));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, moved)));

        // a later run has an item C whose own path is B's earlier hashed name
        var c = ExportLayout.ForVoices([Voice("sound/voice/y/other/0002.wem", plugin: "ModC.esm")], keepFolders: false)[0] with { RelativePath = moved };
        List<ExportItem<VoiceLine>> items = keptFirst ? [b, c] : [c, b];
        var cMoved = ExportLayout.WithSuffix(moved, ExportLayout.StableHash(c.Description));
        Assert.Equal([moved, cMoved], BulkExporter.PlannedPaths(items, _dir, ExportLayout.VoiceColumns)
            .Select(p => p.Item.RelativePath).OrderBy(p => p.Length));

        var summary = BulkExporter.Run(items, _dir, VoiceCsv, VoiceConvert);
        Assert.Equal((1, 1, 0), (summary.Written, summary.Kept, summary.Failed));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, moved)));
        Assert.Equal("RIFF ModC.esm", File.ReadAllText(Path.Combine(_dir, cMoved)));
        Assert.Equal(3, Directory.GetFiles(_dir, "*.wav").Length); // B is not written a second time
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Single(rows, r => r[1] == "ModB.esm");
    }

    [Fact]
    public void AnEarlierHashedFileIsNotKeptWhenAnotherItemWroteThatPathEarlierInTheRun()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false)[0];
        BulkExporter.Run(a, _dir, VoiceCsv, VoiceConvert);
        BulkExporter.Run([b], _dir, VoiceCsv, VoiceConvert);
        var hash = ExportLayout.StableHash(b.Description);
        var moved = ExportLayout.WithSuffix(b.RelativePath, hash);
        var second = ExportLayout.WithSuffix(b.RelativePath, hash + "_2");
        File.Delete(Path.Combine(_dir, moved)); // B's file is gone; list.csv still lists B there

        // C, whose own path is B's earlier hashed name, comes first and writes its file there
        var c = ExportLayout.ForVoices([Voice("sound/voice/y/other/0002.wem", plugin: "ModC.esm")], keepFolders: false)[0] with { RelativePath = moved };
        List<ExportItem<VoiceLine>> items = [c, b];
        Assert.Equal([moved, second], BulkExporter.PlannedPaths(items, _dir, ExportLayout.VoiceColumns).Select(p => p.Item.RelativePath));

        var summary = BulkExporter.Run(items, _dir, VoiceCsv, VoiceConvert);
        Assert.Equal((2, 0, 0), (summary.Written, summary.Kept, summary.Failed));
        Assert.Equal("RIFF ModC.esm", File.ReadAllText(Path.Combine(_dir, moved)));
        Assert.Equal("RIFF ModB.esm", File.ReadAllText(Path.Combine(_dir, second)));
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows.Select(r => r[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(second, Assert.Single(rows, r => r[1] == "ModB.esm")[0]);
        Assert.Equal(moved, Assert.Single(rows, r => r[1] == "ModC.esm")[0]);
    }

    [Fact]
    public void PlannedPathsMarkEveryItemWhenTheFolderNameCantBeUsed()
    {
        var a = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var planned = BulkExporter.PlannedPaths(a, "bad\0name", null);
        Assert.Contains("Could not make the export folder", Assert.Single(planned).Problem);
        Assert.NotNull(BulkExporter.PlannedPaths(a, "bad\0name", ExportLayout.VoiceColumns)[0].Problem);
    }

    [Fact]
    public void PlannedPathsAndTheRunAgreeOnAFolderNameWithACharacterFileNamesCantHold()
    {
        if (!OperatingSystem.IsWindows()) return; // on Linux only \0 and / are refused, and / splits the path
        var a = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var bad = Path.Combine(_dir, "Ex|ports");

        // .NET's GetFullPath accepts the name, so only this check catches it before the run
        _ = Path.GetFullPath(bad); // doesn't throw
        var problem = BulkExporter.PlannedPaths(a, bad, null)[0].Problem;
        Assert.Contains("\"Ex|ports\"", problem);
        var ex = Assert.Throws<IOException>(() => BulkExporter.Run(a, bad, VoiceCsv, VoiceConvert));
        Assert.Equal(problem, ex.Message);
        Assert.False(Directory.Exists(_dir)); // nothing was made
    }

    [Fact]
    public void PlannedPathsMarkEveryItemWhenAPartOfTheFolderIsAFile()
    {
        var a = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm"), Voice("a/0002.wem", plugin: "ModA.esm")], keepFolders: false);
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "notafolder");
        File.WriteAllText(file, "x");

        Assert.Null(BulkExporter.FolderProblem(Path.Combine(_dir, "new", "deeper")));
        var planned = BulkExporter.PlannedPaths(a, Path.Combine(file, "sub"), ExportLayout.VoiceColumns);
        Assert.All(planned, p => Assert.Contains("is a file", p.Problem));
        Assert.Contains("is a file", BulkExporter.FolderProblem(file));
        Assert.Throws<IOException>(() => BulkExporter.Run(a, Path.Combine(file, "sub"), VoiceCsv, VoiceConvert));
    }

    [Fact]
    public void TheListFailureLinesShowFailingItemsBelowTheShownRows()
    {
        var items = Enumerable.Range(1, 10)
            .Select(n => new PlannedPath<int>(new ExportItem<int>($"f{n}.wav", [], n, $"item {n}"), n is 2 or 6 or 7 or 9 ? "no room" : null))
            .ToList();

        Assert.Empty(PlannedPathReport.FailureLines(items.Where(p => p.Problem == null).ToList(), 3, ExportKind.Sounds));

        // the first 3 rows are shown; 6, 7 and 9 are below them, and only 3 of those fit
        var lines = PlannedPathReport.FailureLines(items, 3, ExportKind.Sounds);
        Assert.Equal("Of the items not shown above, these would fail:", lines[0]);
        Assert.Equal(["  f6.wav  (item 6) WILL FAIL: no room", "  f7.wav  (item 7) WILL FAIL: no room", "  f9.wav  (item 9) WILL FAIL: no room"], lines[1..4]);
        Assert.Equal("4 of them would fail; each one's reason follows WILL FAIL above.", lines[^1]);

        var tight = PlannedPathReport.FailureLines(items, 2, ExportKind.Sounds); // 6, 7 and 9 below; 2 fit
        Assert.Equal("  ... and 1 more that would fail", tight[^2]);
        Assert.StartsWith("4 of them would fail; the reasons of the ones shown", tight[^1]);
        Assert.EndsWith("(--group or --search) to see the rest.", tight[^1]);

        // the hint names only options the kind accepts (ExportOptions refuses --group for voices)
        var voices = PlannedPathReport.FailureLines(items, 2, ExportKind.Voices);
        Assert.EndsWith("(--plugin, --voice-type or --search) to see the rest.", voices[^1]);
        foreach (var kind in new[] { ExportKind.Sounds, ExportKind.Voices })
        {
            var hint = PlannedPathReport.FailureLines(items, 2, kind)[^1];
            var named = System.Text.RegularExpressions.Regex.Matches(hint, "--[a-z-]+").Select(m => m.Value).ToList();
            var args = new List<string> { kind == ExportKind.Sounds ? "sounds" : "voices", "--out", "x" };
            foreach (var option in named) args.AddRange([option, "y"]);
            Assert.Empty(ExportOptions.Parse(args.ToArray()).Errors);
        }

        var allShown = PlannedPathReport.FailureLines(items, 10, ExportKind.Sounds);
        Assert.Equal(["4 of them would fail; each one's reason follows WILL FAIL above."], allShown);
    }

    [Fact]
    public void ANameFarDownTheHashedListIsStillFoundAsTheItemsOwn()
    {
        var a = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var b = ExportLayout.ForVoices([Voice("sound/voice/x/npcfastrid/0001.wem", plugin: "ModB.esm")], keepFolders: false)[0];
        var hash = ExportLayout.StableHash(b.Description);
        var at42 = ExportLayout.WithSuffix(b.RelativePath, hash + "_42");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, a[0].RelativePath), "RIFF ModA.esm");
        File.WriteAllText(Path.Combine(_dir, at42), "RIFF ModB.esm");
        File.WriteAllText(Path.Combine(_dir, ExportLayout.WithSuffix(b.RelativePath, hash + "_042")), "not B's");
        ExportCsv.Write(Path.Combine(_dir, ExportCsv.FileName), ExportLayout.VoiceColumns,
        [
            [a[0].RelativePath, .. a[0].CsvRow],
            [ExportLayout.WithSuffix(b.RelativePath, hash + "_042"), .. b.CsvRow], // not one of B's names
            [at42, .. b.CsvRow],
        ]);

        var summary = BulkExporter.Run([b], _dir, VoiceCsv, VoiceConvert);
        Assert.Equal((0, 1), (summary.Written, summary.Kept));
        Assert.Equal([at42], summary.PresentPaths);
    }

    [Fact]
    public void PlannedPathsReportARefusedPathAlsoWithoutAListOrAFolder()
    {
        var a = ExportLayout.ForVoices([Voice("a/0001.wem", plugin: "ModA.esm")], keepFolders: false);
        var rooted = a[0] with { RelativePath = Path.Combine(_dir, "elsewhere.wav") };

        Assert.False(Directory.Exists(_dir));
        Assert.NotNull(BulkExporter.PlannedPaths([rooted], _dir, ExportLayout.VoiceColumns)[0].Problem); // no folder yet
        Directory.CreateDirectory(_dir);
        Assert.NotNull(BulkExporter.PlannedPaths([rooted], _dir, null)[0].Problem); // --no-csv
        Assert.Null(BulkExporter.PlannedPaths(a, _dir, null)[0].Problem);
    }

    [Fact]
    public void TheSameItemIsKeptWhenOnlyItsDialogueChanged()
    {
        BulkExporter.Run(ExportLayout.ForVoices([Voice("a/0001.wem", dialogue: "Hello")], keepFolders: false), _dir, VoiceCsv, VoiceConvert);
        var summary = BulkExporter.Run(ExportLayout.ForVoices([Voice("a/0001.wem", dialogue: "Hello there")], keepFolders: false),
            _dir, VoiceCsv, VoiceConvert);

        Assert.Equal((0, 1), (summary.Written, summary.Kept));
        var rows = ExportCsv.Parse(File.ReadAllText(summary.CsvPath!));
        Assert.Equal("Hello there", rows[1][4]);
    }

    [Fact]
    public void WithoutAListAnExistingFileIsKept()
    {
        Directory.CreateDirectory(_dir);
        var items = ExportLayout.ForSounds([Sound(1, "A")], keepFolders: false);
        File.WriteAllText(Path.Combine(_dir, items[0].RelativePath), "old");
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);
        Assert.Equal(1, summary.Kept);
    }

    [Fact]
    public void IdentityColumnsNameTheItem()
    {
        Assert.Equal([1, 8], ExportLayout.IdentityColumns(ExportLayout.SoundColumns));
        Assert.Equal([1, 3], ExportLayout.IdentityColumns(ExportLayout.VoiceColumns));
        Assert.Equal([1, 2], ExportLayout.IdentityColumns(["File", "X", "Y"]));
    }

    [Theory]
    [InlineData("-Impact", "_-Impact")]
    [InlineData("=x", "_=x")]
    [InlineData("+x", "_+x")]
    [InlineData("@x", "_@x")]
    [InlineData("a-b", "a-b")]
    public void FormulaStartsGetAnUnderscore(string name, string expected) => Assert.Equal(expected, ExportLayout.SafePart(name));

    [Fact]
    public void FileCellsMatchTheFilesOnDisk()
    {
        var items = ExportLayout.ForSounds([Sound(5, "-Impact"), Sound(6, "'=Boom")], keepFolders: false);
        Assert.Equal(["_-Impact_5.wav", "'=Boom_6.wav"], items.Select(i => i.RelativePath));
        var summary = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);

        var text = File.ReadAllText(summary.CsvPath!);
        Assert.Contains("\r\n_-Impact_5.wav,", text);
        var rows = ExportCsv.Parse(text).Skip(1).ToList();
        Assert.All(rows, r => Assert.True(File.Exists(Path.Combine(_dir, r[0])), r[0]));

        // the rerun reads both names back and keeps them, with no rows dropped
        var again = BulkExporter.Run(items, _dir, WithCsv, FakeConvert);
        Assert.Equal(2, again.Kept);
        Assert.Equal(["_-Impact_5.wav", "'=Boom_6.wav"], ExportCsv.Parse(File.ReadAllText(again.CsvPath!)).Skip(1).Select(r => r[0]));
    }

    [Theory]
    [InlineData("'=x")]
    [InlineData("''")]
    [InlineData("'a")]
    [InlineData("'")]
    [InlineData("=a")]
    [InlineData("a")]
    [InlineData("")]
    public void GuardAndUnguardAreInverses(string cell) => Assert.Equal(cell, ExportCsv.Unguard(ExportCsv.Guard(cell)));

    [Fact]
    public void HandEditedRowsDoNotStopTheMerge()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "B_2.wav"), "x");
        ExportCsv.Write(Path.Combine(_dir, ExportCsv.FileName), ExportLayout.SoundColumns,
        [
            ["B_2.wav", "2", "B"], // lost columns in Excel
            ["bad\0name.wav", "9", "", "", "", "", "", "", ""],
        ]);

        var summary = BulkExporter.Run(ExportLayout.ForSounds([Sound(3, "C")], keepFolders: false), _dir, WithCsv, FakeConvert);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(["C_3.wav"], ExportCsv.Parse(File.ReadAllText(summary.CsvPath!)).Skip(1).Select(r => r[0]));
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
