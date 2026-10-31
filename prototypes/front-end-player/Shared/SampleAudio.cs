// PROTOTYPE: generates a WAV per Track so the prototype needs no audio files.
// A soft note every second and a double beep every 10 s, so a restart or stutter is audible.
using System.Collections.Concurrent;

namespace Prototype;

public static class SampleAudio
{
    const int SampleRate = 22050;
    static readonly ConcurrentDictionary<int, byte[]> Cache = new();

    public static byte[]? Get(int trackId)
    {
        var track = Catalog.Tracks.FirstOrDefault(t => t.Id == trackId);
        return track is null ? null : Cache.GetOrAdd(trackId, _ => Generate(track));
    }

    static byte[] Generate(Track track)
    {
        int samples = track.Seconds * SampleRate;
        double[] scale = [1, 9.0 / 8, 5.0 / 4, 3.0 / 2, 5.0 / 3];
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            int second = (int)t;
            double inSecond = t - second;
            double freq = track.BaseHz * scale[second % scale.Length];
            double v = 0.08 * Math.Sin(2 * Math.PI * track.BaseHz / 2 * t); // drone
            if (inSecond < 0.25) v += 0.25 * Math.Sin(2 * Math.PI * freq * t) * (1 - inSecond / 0.25);
            if (second % 10 == 0 && inSecond is > 0.4 and < 0.5 or > 0.6 and < 0.7)
                v += 0.2 * Math.Sin(2 * Math.PI * 880 * t);
            w.Write((short)(Math.Clamp(v, -1, 1) * short.MaxValue));
        }
        w.Flush();
        return ms.ToArray();
    }
}
