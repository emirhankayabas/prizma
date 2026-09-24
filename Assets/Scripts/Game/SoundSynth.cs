using System;
using System.Collections.Generic;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Every sound recipe in the game, as plain float maths. It touches no Unity type, so the
    /// same code that builds the clips on a phone also runs in <c>Tools/AudioCheck</c>, which
    /// measures their level and writes them out as WAV files to listen to.
    ///
    /// The recipes (pitches, envelopes, filters) are the ones the set has always had; this file
    /// only moved them out of <see cref="AudioKit"/> and <see cref="MusicPlayer"/>. How loud each
    /// clip leaves the synthesiser is decided afterwards, by <see cref="SoundMaster"/>.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;
        public const int MusicSampleRate = 22050;

        const float Pi = 3.14159274f;

        /// <summary>C major pentatonic across two octaves.</summary>
        static readonly float[] Pentatonic =
        {
            261.63f, 293.66f, 329.63f, 392.00f, 440.00f,
            523.25f, 587.33f, 659.25f, 783.99f, 880.00f
        };

        /// <summary>
        /// The rungs the combo climbs, as root-and-third pairs. The old set stopped after five
        /// steps and then repeated itself, so a long streak flattened out exactly where the
        /// player was doing best.
        /// </summary>
        public static readonly float[][] ComboLadder =
        {
            new[] { 392.00f, 523.25f },
            new[] { 440.00f, 587.33f },
            new[] { 523.25f, 659.25f },
            new[] { 587.33f, 783.99f },
            new[] { 659.25f, 880.00f },
            new[] { 783.99f, 1046.50f },
            new[] { 880.00f, 1174.66f },
            new[] { 1046.50f, 1318.51f },
            new[] { 1174.66f, 1567.98f }
        };

        public const int ClearSteps = 4;
        public const int StarCount = 3;

        /// <summary>How many sizes of combo glitter there are; the longest comes from a streak of nine.</summary>
        public const int SparkleSteps = 4;

        /// <summary>The top of the pentatonic, continued an octave up, for the glitter to run along.</summary>
        static readonly float[] SparkleScale = { 1046.50f, 1174.66f, 1318.51f, 1567.98f, 1760.00f, 2093.00f, 2349.32f };

        /// <summary>
        /// The handful of sounds a player can reach before anything else could finish: a tap on a
        /// menu button, and the pickup and place of the very first piece.
        /// </summary>
        public static List<KeyValuePair<string, float[]>> BuildEarly()
        {
            var built = new List<KeyValuePair<string, float[]>>(4);
            void Add(string name, float[] data) => built.Add(new KeyValuePair<string, float[]>(name, data));

            // Lifting a piece: a soft upward blip, quiet enough to hear a hundred times.
            Add("pickup", Tone("sfx_pickup", 480f, 620f, 0.07f, 0.20f, attack: 0.012f, lowpass: 2600f));

            // Setting a piece down: a warm low thud with a trace of filtered noise for body.
            Add("place", Tone("sfx_place", 220f, 130f, 0.14f, 0.32f, attack: 0.006f, lowpass: 850f, noise: 0.10f));

            // Rejected drop: low and short. Never a buzzer — the player already knows.
            Add("invalid", Tone("sfx_invalid", 165f, 130f, 0.11f, 0.18f, attack: 0.014f, lowpass: 700f));

            Add("click", Tone("sfx_click", 620f, 700f, 0.035f, 0.14f, attack: 0.008f, lowpass: 2200f));

            return built;
        }

        /// <summary>Every recipe that is not needed in the first moments. Safe to run on the thread pool.</summary>
        public static List<KeyValuePair<string, float[]>> BuildRest()
        {
            var built = new List<KeyValuePair<string, float[]>>(24);
            void Add(string name, float[] data) => built.Add(new KeyValuePair<string, float[]>(name, data));

            // Clearing lines. One line is the warm bell on root, fifth and octave that the set
            // has always had — it is the overwhelming majority of clears and is left exactly as
            // it was. Clearing more than one in a single move simply carries the same bell
            // further up the same scale: the same sound, saying more. Nothing new is introduced,
            // because a different timbre for a bigger clear reads as a different event rather
            // than a better one.
            Add("clear0", Chime(new[] { Pentatonic[5], Pentatonic[8], Pentatonic[9] },
                0.055f, 0.55f, 0.26f, 2800f));
            Add("clear1", Chime(new[] { Pentatonic[5], Pentatonic[7], Pentatonic[8], Pentatonic[9] },
                0.05f, 0.6f, 0.26f, 3000f));
            Add("clear2", Chime(new[] { Pentatonic[5], Pentatonic[7], Pentatonic[8], Pentatonic[9], 1046.50f },
                0.048f, 0.66f, 0.26f, 3300f));
            Add("clear3", Chime(new[] { Pentatonic[5], Pentatonic[7], Pentatonic[8], Pentatonic[9], 1046.50f, 1174.66f },
                0.045f, 0.72f, 0.26f, 3600f));

            // Each combo step climbs the pentatonic, so a streak sings a rising phrase.
            for (int i = 0; i < ComboLadder.Length; i++)
                Add("combo" + i, Chime(ComboLadder[i], 0.06f, 0.5f, 0.28f, 3000f));

            Add("gameover", Chime(new[] { Pentatonic[7], Pentatonic[5], Pentatonic[3], Pentatonic[0] },
                0.14f, 0.7f, 0.24f, 2000f));

            Add("fanfare", Chime(new[] { Pentatonic[3], Pentatonic[5], Pentatonic[7], Pentatonic[9] },
                0.09f, 0.75f, 0.28f, 3200f));

            // A freed crystal: high and glassy, the brightest sound in the set.
            Add("gem", Chime(new[] { Pentatonic[7], Pentatonic[9], 1046.5f }, 0.045f, 0.45f, 0.22f, 5200f));

            // Ice taking a hit: a short filtered crackle, no pitch to clash with the chimes.
            Add("ice", Tone("sfx_ice", 1800f, 900f, 0.07f, 0.16f, attack: 0.002f, lowpass: 4800f, noise: 0.6f));

            Add("rotate", Tone("sfx_rotate", 520f, 880f, 0.09f, 0.18f, attack: 0.01f, lowpass: 3000f));
            Add("reroll", Chime(new[] { Pentatonic[3], Pentatonic[4], Pentatonic[5] }, 0.035f, 0.14f, 0.2f, 3000f));

            // The bomb: a low sweep buried in noise. Kept round rather than loud.
            Add("bomb", Tone("sfx_bomb", 120f, 42f, 0.5f, 0.55f, attack: 0.004f, lowpass: 520f, noise: 0.45f));

            // The prism filling to a new charge.
            Add("charge", Chime(new[] { Pentatonic[4], Pentatonic[7] }, 0.07f, 0.4f, 0.22f, 3600f));

            // A single-colour line: the whole scale run up, like light fanning out of a prism.
            Add("prism", Chime(new[] { Pentatonic[5], Pentatonic[6], Pentatonic[7], Pentatonic[8], Pentatonic[9] },
                0.035f, 0.5f, 0.22f, 4200f));

            // Out of room: two soft falling notes. Not a failure buzzer — the run may yet be saved.
            Add("stuck", Chime(new[] { Pentatonic[4], Pentatonic[2] }, 0.12f, 0.5f, 0.2f, 1800f));

            for (int i = 0; i < StarCount; i++)
                Add("star" + i, Chime(new[] { Pentatonic[5 + i * 2 - (i == 2 ? 1 : 0)], Pentatonic[Math.Min(9, 7 + i)] },
                    0.05f, 0.45f, 0.24f, 4200f));

            // ---- Added layers. Everything above is the set the player already knows and is left
            // exactly as it was; what follows only ever plays *on top* of it. Same primitives, same
            // scale, same dry short decays — more of the same voice, not a new one.

            // The combo's glitter: a quick run up the top of the scale that joins the clear from the
            // third consecutive clear on, one note longer every two steps of the streak. The clear
            // and the combo answer underneath are unchanged — the streak is heard as the phrase
            // gaining notes, so a long run sounds fuller rather than different.
            for (int i = 0; i < SparkleSteps; i++)
            {
                int notes = 3 + i;
                var run = new float[notes];
                for (int n = 0; n < notes; n++) run[n] = SparkleScale[Math.Min(SparkleScale.Length - 1, n + i)];
                Add("sparkle" + i, Chime(run, 0.028f, 0.2f, 0.2f, 5200f));
            }

            // Every fifth consecutive clear: the whole chord struck at once, low to high, under the
            // glitter. A landmark in the streak rather than one more rung.
            Add("surge", Chime(new[] { Pentatonic[0], Pentatonic[3], Pentatonic[5], Pentatonic[8], 1046.50f, 1318.51f },
                0.018f, 0.62f, 0.2f, 3800f));

            // A fresh tray arriving: three soft taps a hair apart, one per piece. Quiet — it happens
            // every third move.
            Add("deal", Chime(new[] { Pentatonic[7], Pentatonic[8], Pentatonic[9] }, 0.06f, 0.07f, 0.16f, 3400f));

            // The move that passes your own best: the top of the fanfare, carried an octave higher
            // and left ringing.
            Add("record", Chime(new[] { Pentatonic[8], 1046.50f, 1318.51f, 1567.98f }, 0.075f, 0.5f, 0.24f, 4600f));

            // A badge landing on the result card: the gem's glass, one octave climbed.
            Add("badge", Chime(new[] { Pentatonic[5], Pentatonic[7], Pentatonic[9], 1046.50f, 1318.51f }, 0.05f, 0.55f, 0.24f, 5000f));

            // The streak counter ticking over: a short rising blip with a breath of noise, like a
            // match catching.
            Add("streak", Tone("sfx_streak", 330f, 990f, 0.16f, 0.24f, attack: 0.01f, lowpass: 3600f, noise: 0.12f));

            // The run's light moving to its next stage: the scale climbed from the middle to the
            // top in one breath, the same bell as a clear. A threshold crossed, heard as ascent.
            Add("stage", Chime(new[] { Pentatonic[3], Pentatonic[5], Pentatonic[7], Pentatonic[9], 1046.50f, 1318.51f },
                0.055f, 0.6f, 0.22f, 4800f));

            return built;
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>A single tone sweeping between two frequencies under a soft envelope.</summary>
        static float[] Tone(string name, float startHz, float endHz, float duration, float volume,
            float attack, float lowpass, float noise = 0f)
        {
            int count = CeilToInt(SampleRate * duration);
            var data = new float[count];

            float phase = 0f;

            // Seeded from the clip name, so every sound keeps the exact noise texture it has
            // always had. The name serves no other purpose here.
            var rng = new Random(name.GetHashCode());

            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float hz = Lerp(startHz, endHz, k);
                phase += 2f * Pi * hz / SampleRate;

                // A touch of second harmonic gives body without brightness.
                float sample = Sin(phase) + 0.22f * Sin(phase * 2f);

                if (noise > 0f)
                    sample = Lerp(sample, (float)(rng.NextDouble() * 2d - 1d), noise);

                data[i] = sample * Envelope(i, count, attack) * volume;
            }

            Lowpass(data, lowpass, SampleRate);
            return data;
        }

        /// <summary>Notes struck in quick succession and left to ring together.</summary>
        static float[] Chime(float[] notes, float spacing, float duration, float volume, float lowpass)
        {
            int spacingSamples = CeilToInt(SampleRate * spacing);
            int noteSamples = CeilToInt(SampleRate * duration);
            var data = new float[spacingSamples * notes.Length + noteSamples];

            for (int n = 0; n < notes.Length; n++)
            {
                int offset = n * spacingSamples;
                float phase = 0f;

                for (int i = 0; i < noteSamples; i++)
                {
                    int index = offset + i;
                    if (index >= data.Length) break;

                    phase += 2f * Pi * notes[n] / SampleRate;

                    // Bell-like: the octave decays faster than the fundamental.
                    float k = i / (float)noteSamples;
                    float sample = Sin(phase) + 0.3f * (float)Math.Exp(-3f * k) * Sin(phase * 2f);

                    data[index] += sample * Envelope(i, noteSamples, 0.01f) * volume;
                }
            }

            Lowpass(data, lowpass, SampleRate);
            Normalise(data, 0.9f);
            return data;
        }

        /// <summary>
        /// Smooth attack, exponential decay, and a short fade at the very end so a clip never
        /// stops mid-wave — that cut is heard as a click.
        /// </summary>
        static float Envelope(int index, int count, float attackSeconds)
        {
            int attackSamples = Math.Max(1, CeilToInt(SampleRate * attackSeconds));
            float envelope;

            if (index < attackSamples)
            {
                // Raised cosine rather than a straight ramp: no corner at the peak.
                float a = index / (float)attackSamples;
                envelope = 0.5f - 0.5f * (float)Math.Cos(a * Pi);
            }
            else
            {
                float k = (index - attackSamples) / (float)Math.Max(1, count - attackSamples);
                envelope = (float)Math.Exp(-4.2f * k);
            }

            int tail = Math.Min(400, count / 4);
            int remaining = count - index;
            if (remaining < tail) envelope *= remaining / (float)tail;

            return envelope;
        }

        /// <summary>Two passes of a one-pole filter. This is what takes the edge off the sine.</summary>
        static void Lowpass(float[] data, float cutoffHz, int sampleRate)
        {
            if (cutoffHz <= 0f) return;

            float dt = 1f / sampleRate;
            float rc = 1f / (2f * Pi * cutoffHz);
            float alpha = dt / (rc + dt);

            for (int pass = 0; pass < 2; pass++)
            {
                float previous = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    previous += alpha * (data[i] - previous);
                    data[i] = previous;
                }
            }
        }

        public static void Normalise(float[] data, float ceiling)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
                peak = Math.Max(peak, Math.Abs(data[i]));

            if (peak <= ceiling || peak <= 0f) return;

            float scale = ceiling / peak;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }

        // ------------------------------------------------------------------ music

        const float ChordSeconds = 4f;
        const int ArpNotesPerChord = 8;

        /// <summary>A minor progression: i - VI - III - VII. Warm without being sleepy.</summary>
        static readonly float[][] Progression =
        {
            new[] { 220.00f, 261.63f, 329.63f }, // Am
            new[] { 174.61f, 220.00f, 261.63f }, // F
            new[] { 261.63f, 329.63f, 392.00f }, // C
            new[] { 196.00f, 246.94f, 293.66f }  // G
        };

        static readonly float[] BassNotes = { 110.00f, 87.31f, 130.81f, 98.00f };

        /// <summary>
        /// The calm background loop. Notes are written with a wrapping index, so every release
        /// tail spills into the start of the loop and the seam is inaudible.
        /// </summary>
        public static float[] MusicLoop()
        {
            int chordSamples = RoundToInt(MusicSampleRate * ChordSeconds);
            int total = chordSamples * Progression.Length;
            var data = new float[total];

            for (int c = 0; c < Progression.Length; c++)
            {
                int start = c * chordSamples;
                var chord = Progression[c];

                // Pad: the chord tones held across the whole bar, slightly detuned for width.
                foreach (float note in chord)
                {
                    AddVoice(data, start, chordSamples, note, 0.085f, attack: 0.9f, release: 1.4f, warmth: 0.45f);
                    AddVoice(data, start, chordSamples, note * 1.004f, 0.055f, attack: 1.1f, release: 1.4f, warmth: 0.3f);
                }

                // Bass: one long root note per bar.
                AddVoice(data, start, chordSamples, BassNotes[c], 0.11f, attack: 0.35f, release: 1.0f, warmth: 0.2f);

                // Arpeggio: plucked chord tones walking up and back down.
                int noteSamples = chordSamples / ArpNotesPerChord;
                for (int n = 0; n < ArpNotesPerChord; n++)
                {
                    int step = n < chord.Length ? n : ArpNotesPerChord - n - 1;
                    if (step < 0) step = 0;
                    if (step >= chord.Length) step = chord.Length - 1;

                    float freq = chord[step] * 2f; // an octave up, so it sings over the pad
                    AddPluck(data, start + n * noteSamples, noteSamples, freq, 0.075f);
                }
            }

            Normalise(data, 0.82f);
            return data;
        }

        /// <summary>A sustained tone with a slow attack and a release that wraps past the loop end.</summary>
        static void AddVoice(float[] data, int start, int holdSamples, float freq, float amp,
            float attack, float release, float warmth)
        {
            int attackSamples = RoundToInt(MusicSampleRate * attack);
            int releaseSamples = RoundToInt(MusicSampleRate * release);
            int length = holdSamples + releaseSamples;

            float phase = 0f;
            float step = 2f * Pi * freq / MusicSampleRate;

            for (int i = 0; i < length; i++)
            {
                phase += step;

                float envelope;
                if (i < attackSamples) envelope = i / (float)attackSamples;
                else if (i < holdSamples) envelope = 1f;
                else envelope = 1f - (i - holdSamples) / (float)releaseSamples;

                envelope = Clamp01(envelope);
                envelope *= envelope; // gentler swell than a linear ramp

                float sample = Sin(phase) + warmth * 0.5f * Sin(phase * 2f) + warmth * 0.2f * Sin(phase * 3f);
                data[(start + i) % data.Length] += sample * envelope * amp;
            }
        }

        /// <summary>A short plucked note: instant attack, exponential decay.</summary>
        static void AddPluck(float[] data, int start, int noteSamples, float freq, float amp)
        {
            int length = noteSamples * 3; // let it ring past its slot
            float phase = 0f;
            float step = 2f * Pi * freq / MusicSampleRate;

            for (int i = 0; i < length; i++)
            {
                phase += step;

                float k = i / (float)length;
                float envelope = (float)Math.Exp(-6f * k);
                if (i < 64) envelope *= i / 64f; // avoid a click on the attack

                float sample = Sin(phase) + 0.25f * Sin(phase * 2f);
                data[(start + i) % data.Length] += sample * envelope * amp;
            }
        }

        // ------------------------------------------------------------------ maths
        // Written to match UnityEngine.Mathf exactly, so moving the recipes here changed no sample.

        static float Sin(float x) => (float)Math.Sin(x);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        static int CeilToInt(float f) => (int)Math.Ceiling(f);
        static int RoundToInt(float f) => (int)Math.Round(f);
    }
}
