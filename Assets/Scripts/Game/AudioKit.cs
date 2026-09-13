using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Synthesises the whole sound set at startup, so the project ships with audio and no files.
    ///
    /// Everything is deliberately soft: gentle attacks, exponential decays and a one-pole low-pass
    /// on every clip. Raw sine blips with instant attacks are what make procedural game audio sound
    /// cheap and tiring, and this is a game people play for half an hour at a time.
    ///
    /// Pitched sounds use a C major pentatonic scale, where no two notes can clash, so overlapping
    /// clears and combos always land in tune with each other.
    /// </summary>
    public sealed class AudioKit : MonoBehaviour
    {
        const int SampleRate = 44100;

        /// <summary>C major pentatonic across two octaves.</summary>
        static readonly float[] Pentatonic =
        {
            261.63f, 293.66f, 329.63f, 392.00f, 440.00f,
            523.25f, 587.33f, 659.25f, 783.99f, 880.00f
        };

        AudioSource _source;

        AudioClip _pickup;
        AudioClip _place;
        AudioClip _invalid;
        AudioClip[] _clear;
        AudioClip _gameOver;
        AudioClip _click;
        AudioClip _fanfare;
        AudioClip[] _combo;

        /// <summary>
        /// The rungs the combo climbs, as root-and-third pairs. The old set stopped after five
        /// steps and then repeated itself, so a long streak flattened out exactly where the
        /// player was doing best.
        /// </summary>
        static readonly float[][] ComboLadder =
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

        AudioClip _gem;
        AudioClip _ice;
        AudioClip _rotate;
        AudioClip _reroll;
        AudioClip _bomb;
        AudioClip _charge;
        AudioClip _prism;
        AudioClip _stuck;
        AudioClip[] _stars;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            // The handful of sounds a player can reach before anything else could finish: a tap
            // on a menu button, and the pickup and place of the very first piece. Cheap enough to
            // build here — measured, these four cost about ten milliseconds together.
            //
            // Everything else is built off the main thread while the menu is already on screen.
            // Synthesising the whole set in Awake cost nearly two hundred milliseconds on a
            // desktop, so several times that on a phone, and every one of those milliseconds is a
            // frozen frame at launch. <see cref="Play"/> ignores a clip that is not ready yet.

            // Lifting a piece: a soft upward blip, quiet enough to hear a hundred times.
            _pickup = Clip("sfx_pickup", Tone("sfx_pickup", 480f, 620f, 0.07f, 0.20f, attack: 0.012f, lowpass: 2600f));

            // Setting a piece down: a warm low thud with a trace of filtered noise for body.
            _place = Clip("sfx_place", Tone("sfx_place", 220f, 130f, 0.14f, 0.32f, attack: 0.006f, lowpass: 850f, noise: 0.10f));

            // Rejected drop: low and short. Never a buzzer — the player already knows.
            _invalid = Clip("sfx_invalid", Tone("sfx_invalid", 165f, 130f, 0.11f, 0.18f, attack: 0.014f, lowpass: 700f));

            _click = Clip("sfx_click", Tone("sfx_click", 620f, 700f, 0.035f, 0.14f, attack: 0.008f, lowpass: 2200f));

            StartCoroutine(BuildRest());
        }

        /// <summary>
        /// Every recipe that is not needed in the first moments. Pure float maths — it touches no
        /// Unity object, so it is safe to run on the thread pool.
        /// </summary>
        static List<KeyValuePair<string, float[]>> BuildRestData()
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

            for (int i = 0; i < 3; i++)
                Add("star" + i, Chime(new[] { Pentatonic[5 + i * 2 - (i == 2 ? 1 : 0)], Pentatonic[Mathf.Min(9, 7 + i)] },
                    0.05f, 0.45f, 0.24f, 4200f));

            return built;
        }

        IEnumerator BuildRest()
        {
            _clear = new AudioClip[4];
            _combo = new AudioClip[ComboLadder.Length];
            _stars = new AudioClip[3];

            List<KeyValuePair<string, float[]>> built = null;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { built = BuildRestData(); }
                catch (Exception e) { Debug.LogException(e); built = new List<KeyValuePair<string, float[]>>(); }
            });

            while (built == null) yield return null;

            // A handful per frame: turning thirty buffers into clips at once is a visible hitch.
            for (int i = 0; i < built.Count; i++)
            {
                Assign(built[i].Key, Clip("sfx_" + built[i].Key, built[i].Value));
                if (i % 6 == 5) yield return null;
            }
        }

        void Assign(string name, AudioClip clip)
        {
            switch (name)
            {
                case "gameover": _gameOver = clip; return;
                case "fanfare": _fanfare = clip; return;
                case "gem": _gem = clip; return;
                case "ice": _ice = clip; return;
                case "rotate": _rotate = clip; return;
                case "reroll": _reroll = clip; return;
                case "bomb": _bomb = clip; return;
                case "charge": _charge = clip; return;
                case "prism": _prism = clip; return;
                case "stuck": _stuck = clip; return;
            }

            if (name.StartsWith("clear", StringComparison.Ordinal)) _clear[int.Parse(name.Substring(5))] = clip;
            else if (name.StartsWith("combo", StringComparison.Ordinal)) _combo[int.Parse(name.Substring(5))] = clip;
            else if (name.StartsWith("star", StringComparison.Ordinal)) _stars[int.Parse(name.Substring(4))] = clip;
        }

        public void PlayGem() => Play(_gem);
        public void PlayIce() => Play(_ice);
        public void PlayRotate() => Play(_rotate);
        public void PlayReroll() => Play(_reroll);
        public void PlayBomb() => Play(_bomb);
        public void PlayCharge() => Play(_charge);
        public void PlayStuck() => Play(_stuck);
        public void PlayStar(int index)
        {
            if (_stars == null) return;
            Play(_stars[Mathf.Clamp(index, 0, _stars.Length - 1)]);
        }

        public void PlayPickup() => Play(_pickup);
        public void PlayPlace() => Play(_place);
        public void PlayInvalid() => Play(_invalid);
        public void PlayGameOver() => Play(_gameOver);
        public void PlayClick() => Play(_click);
        public void PlayFanfare() => Play(_fanfare);

        /// <summary>
        /// The whole of a clear in one call. It used to take four: the clear, the combo, the
        /// single-colour line and the fanfare each fired on their own, in the same frame, and a
        /// good move simply stacked them on top of each other. Now they arrive in order, a few
        /// hundredths of a second apart, so a big clear is heard as a phrase rather than a pile.
        ///
        /// <paramref name="lines"/> chooses how far the bell climbs — how much was done in this
        /// one move. <paramref name="comboStreak"/> answers it a rung higher for every
        /// consecutive clearing move, which is the part the player could not hear before.
        /// </summary>
        public void PlayClear(int lines, int comboStreak, int monoLines, bool perfectClear)
        {
            if (_clear == null || lines <= 0) return;

            Play(_clear[Mathf.Clamp(lines, 1, _clear.Length) - 1]);

            // Quieter than the clear it answers: a reply, not a second announcement.
            if (comboStreak > 1 && _combo != null && _combo.Length > 0)
                PlayAfter(_combo[Mathf.Clamp(comboStreak - 1, 0, _combo.Length - 1)], 0.07f, 0.8f);

            if (monoLines > 0) PlayAfter(_prism, 0.13f, 0.9f);
            if (perfectClear) PlayAfter(_fanfare, 0.22f, 1f);
        }

        void PlayAfter(AudioClip clip, float seconds, float scale)
        {
            if (clip == null || !isActiveAndEnabled) return;
            StartCoroutine(AfterRoutine(clip, seconds, scale));
        }

        IEnumerator AfterRoutine(AudioClip clip, float seconds, float scale)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Play(clip, scale);
        }

        void Play(AudioClip clip, float scale = 1f)
        {
            if (clip == null || _source == null) return;

            float volume = GameSettings.EffectiveSfxVolume;
            if (volume <= 0.001f) return;

            _source.PlayOneShot(clip, volume * scale);
        }

        // ------------------------------------------------------------------ synthesis

        /// <summary>A single tone sweeping between two frequencies under a soft envelope.</summary>
        static float[] Tone(string name, float startHz, float endHz, float duration, float volume,
            float attack, float lowpass, float noise = 0f)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[count];

            float phase = 0f;

            // Seeded from the clip name, so every sound keeps the exact noise texture it has
            // always had. The name serves no other purpose here.
            var rng = new System.Random(name.GetHashCode());

            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float hz = Mathf.Lerp(startHz, endHz, k);
                phase += 2f * Mathf.PI * hz / SampleRate;

                // A touch of second harmonic gives body without brightness.
                float sample = Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2f);

                if (noise > 0f)
                    sample = Mathf.Lerp(sample, (float)(rng.NextDouble() * 2d - 1d), noise);

                data[i] = sample * Envelope(i, count, attack) * volume;
            }

            Lowpass(data, lowpass);
            return data;
        }

        /// <summary>Notes struck in quick succession and left to ring together.</summary>
        static float[] Chime(float[] notes, float spacing, float duration, float volume, float lowpass)
        {
            int spacingSamples = Mathf.CeilToInt(SampleRate * spacing);
            int noteSamples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[spacingSamples * notes.Length + noteSamples];

            for (int n = 0; n < notes.Length; n++)
            {
                int offset = n * spacingSamples;
                float phase = 0f;

                for (int i = 0; i < noteSamples; i++)
                {
                    int index = offset + i;
                    if (index >= data.Length) break;

                    phase += 2f * Mathf.PI * notes[n] / SampleRate;

                    // Bell-like: the octave decays faster than the fundamental.
                    float k = i / (float)noteSamples;
                    float sample = Mathf.Sin(phase) + 0.3f * Mathf.Exp(-3f * k) * Mathf.Sin(phase * 2f);

                    data[index] += sample * Envelope(i, noteSamples, 0.01f) * volume;
                }
            }

            Lowpass(data, lowpass);
            Normalise(data, 0.9f);
            return data;
        }

        /// <summary>
        /// Smooth attack, exponential decay, and a short fade at the very end so a clip never
        /// stops mid-wave — that cut is heard as a click.
        /// </summary>
        static float Envelope(int index, int count, float attackSeconds)
        {
            int attackSamples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * attackSeconds));
            float envelope;

            if (index < attackSamples)
            {
                // Raised cosine rather than a straight ramp: no corner at the peak.
                float a = index / (float)attackSamples;
                envelope = 0.5f - 0.5f * Mathf.Cos(a * Mathf.PI);
            }
            else
            {
                float k = (index - attackSamples) / (float)Mathf.Max(1, count - attackSamples);
                envelope = Mathf.Exp(-4.2f * k);
            }

            int tail = Mathf.Min(400, count / 4);
            int remaining = count - index;
            if (remaining < tail) envelope *= remaining / (float)tail;

            return envelope;
        }

        /// <summary>Two passes of a one-pole filter. This is what takes the edge off the sine.</summary>
        static void Lowpass(float[] data, float cutoffHz)
        {
            if (cutoffHz <= 0f) return;

            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
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

        static void Normalise(float[] data, float ceiling)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));

            if (peak <= ceiling || peak <= 0f) return;

            float scale = ceiling / peak;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
