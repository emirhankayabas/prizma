using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlockPuzzle.Game;

/// <summary>
/// Loudness of the synthesised sound set, measured outside Unity, before and after
/// <see cref="SoundMaster"/>. "Before" is the raw recipe at the old playback gain (effects slider
/// at its old default of 85%, music at 45% of a 55% slider) — the level the game used to ship.
/// </summary>
static class Program
{
    const float OldSfxGain = 0.85f;
    const float OldMusicGain = 0.45f * 0.55f;

    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "measure";
        switch (mode)
        {
            case "measure": Measure(); return 0;
            case "wav": Wav(args.Length > 1 ? args[1] : "audio_out"); return 0;
            default: Console.WriteLine("usage: measure | wav <folder>"); return 2;
        }
    }

    static List<KeyValuePair<string, float[]>> Raw()
    {
        var clips = new List<KeyValuePair<string, float[]>>();
        clips.AddRange(SoundSynth.BuildEarly());
        clips.AddRange(SoundSynth.BuildRest());
        return clips;
    }

    static float[] Mastered(string name, float[] raw, int rate)
    {
        var data = (float[])raw.Clone();
        SoundMaster.Master(data, rate, SoundMaster.TargetFor(name));
        return data;
    }

    static void Measure()
    {
        Console.WriteLine("                   BEFORE                        AFTER (slider 100%)");
        Console.WriteLine($"{"clip",-10} {"peak",7} {"loud",7} {"phone",7}   {"peak",7} {"loud",7} {"phone",7} {"limit",6}");

        foreach (var c in Raw())
            Row(c.Key, c.Value, SoundSynth.SampleRate, OldSfxGain, SoundMaster.DefaultEffectsVolume);

        Row("music", SoundSynth.MusicLoop(), SoundSynth.MusicSampleRate, OldMusicGain, SoundMaster.DefaultMusicVolume);

        Console.WriteLine();
        Console.WriteLine("not in the game — with SoundMaster.AddPresence (phone dB):");
        foreach (var c in Raw())
            if (Array.IndexOf(PresenceClips, c.Key) >= 0)
                Console.WriteLine($"  {c.Key,-10} {Phone(WithPresence(c.Key, c.Value, SoundSynth.SampleRate, 0.8f), SoundSynth.SampleRate),7:0.0}");
        Console.WriteLine($"  {"music",-10} {Phone(Scale(WithPresence("music", SoundSynth.MusicLoop(), SoundSynth.MusicSampleRate, 0.5f), SoundMaster.DefaultMusicVolume), SoundSynth.MusicSampleRate),7:0.0}");
        Console.WriteLine();
        // What building costs: the early set runs on the main thread in AudioKit.Awake.
        for (int warm = 0; warm < 2; warm++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var c in SoundSynth.BuildEarly()) SoundMaster.Master(c.Value, SoundSynth.SampleRate, SoundMaster.TargetFor(c.Key));
            double early = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            foreach (var c in SoundSynth.BuildRest()) SoundMaster.Master(c.Value, SoundSynth.SampleRate, SoundMaster.TargetFor(c.Key));
            double rest = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            SoundMaster.Master(SoundSynth.MusicLoop(), SoundSynth.MusicSampleRate, SoundMaster.TargetFor("music"));
            if (warm == 1)
                Console.WriteLine($"build + master: early {early:0.0} ms (main thread), rest {rest:0} ms, music {watch.Elapsed.TotalMilliseconds:0} ms (thread pool)");
        }

        Console.WriteLine("loud = loudest 100 ms, K-weighted RMS (dB). phone = same through a 700 Hz 4th-order high-pass.");
        Console.WriteLine("limit = most gain reduction the clip limiter applied (dB). music AFTER is at its new 70% default.");
    }

    static void Row(string name, float[] raw, int rate, float oldGain, float newGain)
    {
        var before = Scale(raw, oldGain);

        var gained = Mastered(name, raw, rate);
        var after = Scale(gained, newGain);

        // How hard the limiter worked: the gain the clip got, against its own peak ratio.
        double preLimitPeak = SoundMaster.Peak(raw) * Math.Pow(10, (SoundMaster.Loudness(gained, rate) - SoundMaster.Loudness(raw, rate)) / 20);
        double reduction = 20 * Math.Log10(Math.Max(1e-9, preLimitPeak / Math.Max(1e-9, SoundMaster.Peak(gained))));

        Console.WriteLine($"{name,-10} {Db(SoundMaster.Peak(before)),7:0.0} {SoundMaster.Loudness(before, rate),7:0.0} {Phone(before, rate),7:0.0}   " +
                          $"{Db(SoundMaster.Peak(after)),7:0.0} {SoundMaster.Loudness(after, rate),7:0.0} {Phone(after, rate),7:0.0} {Math.Max(0, reduction),6:0.0}");
    }

    static float[] Scale(float[] data, float gain)
    {
        var copy = new float[data.Length];
        for (int i = 0; i < data.Length; i++) copy[i] = data[i] * gain;
        return copy;
    }

    static double Db(double v) => 20.0 * Math.Log10(Math.Max(1e-9, v));

    /// <summary>A small phone speaker: nothing much below 700 Hz.</summary>
    static double Phone(float[] data, int rate)
    {
        var x = new double[data.Length];
        for (int i = 0; i < data.Length; i++) x[i] = data[i];
        SoundMaster.Biquad.HighPass(rate, 700, 0.707).Run(x);
        SoundMaster.Biquad.HighPass(rate, 700, 0.707).Run(x);
        var f = new float[x.Length];
        for (int i = 0; i < x.Length; i++) f[i] = (float)x[i];
        return SoundMaster.Loudness(f, rate);
    }

    // ------------------------------------------------------------------ listening

    /// <summary>
    /// Every clip before and after, plus the thing that actually matters: a short stretch of
    /// play — pickups, places, clears climbing a combo, a bomb — laid out with the game's own
    /// timings over the music, once at the old level and once at the new one.
    /// </summary>
    static void Wav(string folder)
    {
        Directory.CreateDirectory(folder);
        var raw = new Dictionary<string, float[]>();
        var mastered = new Dictionary<string, float[]>();

        foreach (var c in Raw())
        {
            raw[c.Key] = Scale(c.Value, OldSfxGain);
            mastered[c.Key] = Scale(Mastered(c.Key, c.Value, SoundSynth.SampleRate), SoundMaster.DefaultEffectsVolume);
        }

        var musicRaw = SoundSynth.MusicLoop();
        var musicOld = Scale(Upsample(musicRaw), OldMusicGain);
        var musicNew = Scale(Upsample(Mastered("music", musicRaw, SoundSynth.MusicSampleRate)), SoundMaster.DefaultMusicVolume);

        // The variant that is not in the game: overtones for the sounds a phone speaker cannot play.
        var present = new Dictionary<string, float[]>(mastered);
        foreach (var name in PresenceClips)
            present[name] = Scale(WithPresence(name, SoundSynth.BuildEarly().Concat(SoundSynth.BuildRest()).First(c => c.Key == name).Value,
                SoundSynth.SampleRate, 0.8f), SoundMaster.DefaultEffectsVolume);
        var musicPresent = Scale(Upsample(WithPresence("music", musicRaw, SoundSynth.MusicSampleRate, 0.5f)), SoundMaster.DefaultMusicVolume);

        var once = Sequence(raw, musicOld);
        var sonra = Sequence(mastered, musicNew);
        var telefon = Sequence(present, musicPresent);

        WriteWav(Path.Combine(folder, "1_ONCE_oyun_dizisi.wav"), once);
        WriteWav(Path.Combine(folder, "2_SONRA_oyun_dizisi.wav"), sonra);
        WriteWav(Path.Combine(folder, "3_SONRA_telefon_eki_oyun_dizisi.wav"), telefon);

        // The added layers, A/B: one combo run climbing to ten, once as the game plays it now and
        // once with only the sounds it had before. Same timings, same music.
        WriteWav(Path.Combine(folder, "4_KATMANLI_combo_dizisi.wav"), ComboRun(mastered, musicNew, layers: true));
        WriteWav(Path.Combine(folder, "4b_KATMANSIZ_combo_dizisi.wav"), ComboRun(mastered, musicNew, layers: false));

        // The same three through a phone speaker model, for listening on a desktop.
        WriteWav(Path.Combine(folder, "hoparlor_simulasyonu", "1_ONCE.wav"), PhoneSpeaker(once));
        WriteWav(Path.Combine(folder, "hoparlor_simulasyonu", "2_SONRA.wav"), PhoneSpeaker(sonra));
        WriteWav(Path.Combine(folder, "hoparlor_simulasyonu", "3_SONRA_telefon_eki.wav"), PhoneSpeaker(telefon));

        foreach (var name in raw.Keys)
        {
            WriteWav(Path.Combine(folder, "clips", name + "_once.wav"), raw[name]);
            WriteWav(Path.Combine(folder, "clips", name + "_sonra.wav"), mastered[name]);
        }

        Console.WriteLine("wrote " + Path.GetFullPath(folder));
    }

    static readonly string[] PresenceClips = { "place", "invalid", "bomb" };

    static float[] WithPresence(string name, float[] raw, int rate, float amount)
    {
        var data = (float[])raw.Clone();
        SoundMaster.AddPresence(data, rate, amount);
        SoundMaster.Master(data, rate, SoundMaster.TargetFor(name));
        return data;
    }

    static float[] PhoneSpeaker(float[] data)
    {
        var x = Array.ConvertAll(data, s => (double)s);
        SoundMaster.Biquad.HighPass(SoundSynth.SampleRate, 700, 0.707).Run(x);
        SoundMaster.Biquad.HighPass(SoundSynth.SampleRate, 700, 0.707).Run(x);
        return Array.ConvertAll(x, s => (float)s);
    }

    static float[] Sequence(Dictionary<string, float[]> s, float[] music)
    {
        const int rate = SoundSynth.SampleRate;
        var mix = new float[rate * 14];

        void At(double seconds, string name, float scale = 1f)
        {
            var clip = s[name];
            int start = (int)(seconds * rate);
            for (int i = 0; i < clip.Length && start + i < mix.Length; i++) mix[start + i] += clip[i] * scale;
        }

        // Menu taps, then a run: pick up, drop, sometimes clear, the combo climbing.
        At(0.3, "click"); At(0.9, "click");
        double t = 1.6;
        for (int move = 0; move < 12; move++)
        {
            At(t, "pickup");
            At(t + 0.55, "place");
            if (move % 2 == 1 || move > 6)
            {
                int streak = move > 6 ? move - 5 : 1;
                int lines = move == 9 ? 3 : move == 11 ? 2 : 1;
                At(t + 0.55, "clear" + (lines - 1));
                if (streak > 1) At(t + 0.62, "combo" + Math.Min(8, streak - 1), 0.8f);
            }
            t += 0.85;
        }
        At(t, "click"); At(t + 0.3, "bomb");
        At(t + 1.2, "stuck");

        for (int i = 0; i < mix.Length; i++) mix[i] += music[i % music.Length];

        // The bus limiter the game runs on its output, so overlaps sound here as they do there.
        var limiter = new SoundMaster.BusLimiter();
        limiter.Reset(rate);
        for (int i = 0; i < mix.Length; i++) mix[i] = limiter.Process(mix[i]);
        return mix;
    }

    /// <summary>
    /// A streak of ten clearing moves at the game's own pace, with a tray refill every third move
    /// and the record passed near the end. With <paramref name="layers"/> off it is exactly what
    /// the same moves sounded like before the layers were added.
    /// </summary>
    static float[] ComboRun(Dictionary<string, float[]> s, float[] music, bool layers)
    {
        const int rate = SoundSynth.SampleRate;
        var mix = new float[rate * 13];

        void At(double seconds, string name, float scale = 1f)
        {
            var clip = s[name];
            int start = (int)(seconds * rate);
            for (int i = 0; i < clip.Length && start + i < mix.Length; i++) mix[start + i] += clip[i] * scale;
        }

        double t = 0.5;
        for (int streak = 1; streak <= 10; streak++)
        {
            At(t, "pickup");
            double drop = t + 0.45;
            At(drop, "place");

            int lines = streak == 4 || streak == 8 ? 2 : streak == 10 ? 3 : 1;
            At(drop, "clear" + (lines - 1));
            if (streak > 1) At(drop + 0.07, "combo" + Math.Min(8, streak - 1), 0.8f);

            if (layers)
            {
                if (streak >= 3) At(drop + 0.11, "sparkle" + Math.Min(SoundSynth.SparkleSteps - 1, (streak - 3) / 2), 0.85f);
                if (streak >= 5 && streak % 5 == 0) At(drop + 0.16, "surge", 0.9f);
                if (streak % 3 == 0) At(drop + 0.3, "deal");
                if (streak == 9) At(drop + 0.35, "record");
            }

            t += 1.05;
        }

        if (layers)
        {
            At(t + 0.3, "fanfare");
            At(t + 0.9, "streak");
            At(t + 1.4, "badge");
        }

        for (int i = 0; i < mix.Length; i++) mix[i] += music[i % music.Length];

        var limiter = new SoundMaster.BusLimiter();
        limiter.Reset(rate);
        for (int i = 0; i < mix.Length; i++) mix[i] = limiter.Process(mix[i]);
        return mix;
    }

    static float[] Upsample(float[] music)
    {
        var up = new float[music.Length * 2];
        for (int i = 0; i < music.Length; i++)
        {
            up[i * 2] = music[i];
            up[i * 2 + 1] = 0.5f * (music[i] + music[(i + 1) % music.Length]);
        }
        return up;
    }

    static void WriteWav(string path, float[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var w = new BinaryWriter(File.Create(path));
        int rate = SoundSynth.SampleRate;
        w.Write("RIFF".ToCharArray()); w.Write(36 + data.Length * 2); w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data".ToCharArray()); w.Write(data.Length * 2);
        foreach (var s in data) w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767f));
    }
}
