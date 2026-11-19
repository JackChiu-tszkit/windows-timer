using WindowsTimer.Models;

namespace WindowsTimer.Services;

/// <summary>
/// Generates short notification tones in memory so the app needs no network
/// and no loose audio files. Peaks stay well below full scale.
/// </summary>
public static class ToneLibrary
{
    public const int SampleRate = 44100;

    public static byte[] CreateWav(AlertSoundId id, double volume)
    {
        var gain = Math.Clamp(volume, 0, 1);
        var samples = id switch
        {
            AlertSoundId.Chime => Chime(),
            AlertSoundId.Soft => Tone(494.0, 1.25, 0.40, 0.09, 0.8),
            _ => Bell()
        };

        FadeOut(samples);
        return Encode(samples, gain);
    }

    private static double[] Bell()
    {
        var fundamental = Tone(880, 1.15, 0.38, 0.004, 1.55);
        var harmonic = Tone(1760, 0.85, 0.10, 0.002, 2.1);
        return Mix(fundamental, harmonic);
    }

    private static double[] Chime()
    {
        var notes = new[] { 523.25, 659.25, 783.99 };
        var parts = new List<double>(SampleRate * 2);
        foreach (var frequency in notes)
        {
            parts.AddRange(Tone(frequency, 0.38, 0.36, 0.006, 1.25));
            parts.AddRange(new double[(int)(SampleRate * 0.045)]);
        }

        return parts.ToArray();
    }

    private static double[] Tone(double frequency, double seconds, double peak, double attackSeconds, double decayPower)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var attack = Math.Max(1, (int)(SampleRate * attackSeconds));
        var data = new double[count];
        for (var i = 0; i < count; i++)
        {
            var attackEnv = Math.Min(1.0, i / (double)attack);
            var decay = Math.Pow(1.0 - (i / (double)count), decayPower);
            var wave = Math.Sin(2 * Math.PI * frequency * (i / (double)SampleRate));
            data[i] = wave * peak * attackEnv * decay;
        }

        return data;
    }

    private static double[] Mix(double[] first, double[] second)
    {
        var count = Math.Max(first.Length, second.Length);
        var mixed = new double[count];
        for (var i = 0; i < count; i++)
        {
            var a = i < first.Length ? first[i] : 0;
            var b = i < second.Length ? second[i] : 0;
            mixed[i] = a + b;
        }

        return mixed;
    }

    private static void FadeOut(double[] data)
    {
        var fade = Math.Min(data.Length, SampleRate / 100);
        for (var i = 0; i < fade; i++)
        {
            var gain = 1 - (i / (double)fade);
            data[data.Length - fade + i] *= gain;
        }
    }

    private static byte[] Encode(double[] samples, double gain)
    {
        using var stream = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(stream);
        var dataSize = samples.Length * 2;
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            var value = Math.Clamp(sample * gain, -1, 1);
            writer.Write((short)Math.Round(value * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
