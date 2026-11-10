using BnkExtractor;

namespace StarfieldVT.Core.Tests;

/// <summary>
/// BnkExtractor's ww2ogg port used to leave the .wem and the codebook file open after every
/// conversion. These tests open each file exclusively afterwards: that fails while any handle
/// is still open.
/// </summary>
public class WemConversionTests
{
    private static readonly string CodebooksPath = Path.Combine(Path.GetTempPath(), "packed_codebooks_aoTuV_603.bin");

    private static void AssertClosed(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void BadWemIsClosedAndLeavesNoOgg()
    {
        var dir = Directory.CreateTempSubdirectory("svt-wem-");
        try
        {
            var wem = Path.Combine(dir.FullName, "bad.wem");
            File.WriteAllBytes(wem, "RIFF\0\0\0\0WAVEnot really a wem file"u8.ToArray());

            Extractor.ConvertWem(wem);

            Assert.False(File.Exists(Path.ChangeExtension(wem, "ogg")));
            AssertClosed(wem);
            File.Delete(wem);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>
    /// Real voice files can't ship with the repo (they're game or mod assets). Set
    /// STARFIELDVT_TEST_WEM_DIR to a folder of .wem files to run this check; without it the test
    /// passes without doing anything.
    /// </summary>
    [Fact]
    public void RealWemsConvertAndAreClosed()
    {
        var source = Environment.GetEnvironmentVariable("STARFIELDVT_TEST_WEM_DIR");
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return;

        var dir = Directory.CreateTempSubdirectory("svt-wem-");
        try
        {
            var count = 0;
            foreach (var original in Directory.EnumerateFiles(source, "*.wem"))
            {
                var wem = Path.Combine(dir.FullName, Path.GetFileName(original));
                File.Copy(original, wem);
                var ogg = Path.ChangeExtension(wem, "ogg");

                Extractor.ConvertWem(wem);

                Assert.True(File.Exists(ogg), $"no .ogg for {original}");
                Assert.True(new FileInfo(ogg).Length > 0, $"empty .ogg for {original}");
                AssertClosed(wem);
                AssertClosed(ogg);
                AssertClosed(CodebooksPath);
                File.Delete(wem);
                File.Delete(ogg);
                count++;
            }

            Assert.True(count > 0, $"no .wem files in {source}");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
