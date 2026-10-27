using StarfieldVT.Core.Audio;

namespace StarfieldVT.Core.Tests;

public class OggToWavTests
{
    private static Func<float[], int> Feed(params float[][] chunks)
    {
        var queue = new Queue<float[]>(chunks);
        return buffer =>
        {
            if (queue.Count == 0) return 0;
            var chunk = queue.Dequeue();
            chunk.CopyTo(buffer, 0);
            return chunk.Length;
        };
    }

    private static byte[] WriteWav(int channels, int sampleRate, Func<float[], int> readSamples)
    {
        using var stream = new MemoryStream();
        OggToWav.Write(channels, sampleRate, readSamples, stream);
        return stream.ToArray();
    }

    [Fact]
    public void Write_ProducesValidPcmHeaderWithPatchedSizes()
    {
        var wav = WriteWav(2, 44100, Feed([0f, 0.5f, -0.5f, 1f], [0.25f, -0.25f]));

        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal("WAVEfmt "u8.ToArray(), wav[8..16]);
        Assert.Equal(16, BitConverter.ToInt32(wav, 16));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // PCM
        Assert.Equal(2, BitConverter.ToInt16(wav, 22)); // channels
        Assert.Equal(44100, BitConverter.ToInt32(wav, 24));
        Assert.Equal(44100 * 4, BitConverter.ToInt32(wav, 28)); // byte rate
        Assert.Equal(4, BitConverter.ToInt16(wav, 32)); // block align
        Assert.Equal(16, BitConverter.ToInt16(wav, 34)); // bits per sample
        Assert.Equal("data"u8.ToArray(), wav[36..40]);
        Assert.Equal(6 * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(44 + 12, wav.Length);
    }

    [Fact]
    public void Write_ScalesAndClampsSamples()
    {
        var wav = WriteWav(1, 8000, Feed([0f, 1f, -1f, 0.5f, 2f, -2f]));
        var samples = Enumerable.Range(0, 6).Select(i => BitConverter.ToInt16(wav, 44 + i * 2)).ToArray();

        Assert.Equal(new short[] { 0, short.MaxValue, -short.MaxValue, 16384, short.MaxValue, -short.MaxValue }, samples);
    }

    [Fact]
    public void Write_HandlesEmptyAudio()
    {
        var wav = WriteWav(1, 22050, Feed());

        Assert.Equal(44, wav.Length);
        Assert.Equal(0, BitConverter.ToInt32(wav, 40));
        Assert.Equal(36, BitConverter.ToInt32(wav, 4));
    }

    [Fact]
    public void Write_StopsADecoderThatNeverEnds()
    {
        // a decoder bug that keeps returning audio must not run forever
        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() =>
            OggToWav.Write(1, 100, buffer => buffer.Length, stream));
    }
}
