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
        AudioClip _clear;
        AudioClip _gameOver;
        AudioClip _click;
        AudioClip _fanfare;
        AudioClip[] _combo;

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

            // Lifting a piece: a soft upward blip, quiet enough to hear a hundred times.
            _pickup = Tone("sfx_pickup", 480f, 620f, 0.07f, 0.20f, attack: 0.012f, lowpass: 2600f);

            // Setting a piece down: a warm low thud with a trace of filtered noise for body.
            _place = Tone("sfx_place", 220f, 130f, 0.14f, 0.32f, attack: 0.006f, lowpass: 850f, noise: 0.10f);

            // Rejected drop: low and short. Never a buzzer — the player already knows.
            _invalid = Tone("sfx_invalid", 165f, 130f, 0.11f, 0.18f, attack: 0.014f, lowpass: 700f);

            // Clearing a line: a warm bell on the root, fifth and octave.
            _clear = Chime("sfx_clear", new[] { Pentatonic[5], Pentatonic[8], Pentatonic[9] }, 0.055f, 0.55f, 0.26f, 2800f);

            _gameOver = Chime("sfx_gameover", new[] { Pentatonic[7], Pentatonic[5], Pentatonic[3], Pentatonic[0] },
                0.14f, 0.7f, 0.24f, 2000f);

            _click = Tone("sfx_click", 620f, 700f, 0.035f, 0.14f, attack: 0.008f, lowpass: 2200f);

            _fanfare = Chime("sfx_fanfare", new[] { Pentatonic[3], Pentatonic[5], Pentatonic[7], Pentatonic[9] },
                0.09f, 0.75f, 0.28f, 3200f);

            // Each combo step climbs the pentatonic, so a streak sings a rising phrase.
            _combo = new AudioClip[5];
            for (int i = 0; i < _combo.Length; i++)
            {
                int root = Mathf.Min(i + 3, Pentatonic.Length - 3);
                _combo[i] = Chime($"sfx_combo{i}",
                    new[] { Pentatonic[root], Pentatonic[root + 2] }, 0.06f, 0.5f, 0.28f, 3000f);
            }

            // A freed crystal: high and glassy, the brightest sound in the set.
            _gem = Chime("sfx_gem", new[] { Pentatonic[7], Pentatonic[9], 1046.5f }, 0.045f, 0.45f, 0.22f, 5200f);

            // Ice taking a hit: a short filtered crackle, no pitch to clash with the chimes.
            _ice = Tone("sfx_ice", 1800f, 900f, 0.07f, 0.16f, attack: 0.002f, lowpass: 4800f, noise: 0.6f);

            _rotate = Tone("sfx_rotate", 520f, 880f, 0.09f, 0.18f, attack: 0.01f, lowpass: 3000f);
            _reroll = Chime("sfx_reroll", new[] { Pentatonic[3], Pentatonic[4], Pentatonic[5] }, 0.035f, 0.14f, 0.2f, 3000f);

            // The bomb: a low sweep buried in noise. Kept round rather than loud.
            _bomb = Tone("sfx_bomb", 120f, 42f, 0.5f, 0.55f, attack: 0.004f, lowpass: 520f, noise: 0.45f);

            // The prism filling to a new charge.
            _charge = Chime("sfx_charge", new[] { Pentatonic[4], Pentatonic[7] }, 0.07f, 0.4f, 0.22f, 3600f);

            // A single-colour line: the whole scale run up, like light fanning out of a prism.
            _prism = Chime("sfx_prism", new[] { Pentatonic[5], Pentatonic[6], Pentatonic[7], Pentatonic[8], Pentatonic[9] },
                0.035f, 0.5f, 0.22f, 4200f);

            // Out of room: two soft falling notes. Not a failure buzzer — the run may yet be saved.
            _stuck = Chime("sfx_stuck", new[] { Pentatonic[4], Pentatonic[2] }, 0.12f, 0.5f, 0.2f, 1800f);

            _stars = new AudioClip[3];
            for (int i = 0; i < _stars.Length; i++)
                _stars[i] = Chime($"sfx_star{i}", new[] { Pentatonic[5 + i * 2 - (i == 2 ? 1 : 0)], Pentatonic[Mathf.Min(9, 7 + i)] },
                    0.05f, 0.45f, 0.24f, 4200f);
        }

        public void PlayGem() => Play(_gem);
        public void PlayIce() => Play(_ice);
        public void PlayRotate() => Play(_rotate);
        public void PlayReroll() => Play(_reroll);
        public void PlayBomb() => Play(_bomb);
        public void PlayCharge() => Play(_charge);
        public void PlayPrism() => Play(_prism);
        public void PlayStuck() => Play(_stuck);
        public void PlayStar(int index) => Play(_stars[Mathf.Clamp(index, 0, _stars.Length - 1)]);

        public void PlayPickup() => Play(_pickup);
        public void PlayPlace() => Play(_place);
        public void PlayInvalid() => Play(_invalid);
        public void PlayClear() => Play(_clear);
        public void PlayGameOver() => Play(_gameOver);
        public void PlayClick() => Play(_click);
        public void PlayFanfare() => Play(_fanfare);

        public void PlayCombo(int streak)
        {
            if (_combo == null || _combo.Length == 0) return;
            Play(_combo[Mathf.Clamp(streak - 1, 0, _combo.Length - 1)]);
        }

        void Play(AudioClip clip)
        {
            if (clip == null || _source == null) return;

            float volume = GameSettings.EffectiveSfxVolume;
            if (volume <= 0.001f) return;

            _source.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ synthesis

        /// <summary>A single tone sweeping between two frequencies under a soft envelope.</summary>
        static AudioClip Tone(string name, float startHz, float endHz, float duration, float volume,
            float attack, float lowpass, float noise = 0f)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[count];

            float phase = 0f;
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
            return FromSamples(name, data);
        }

        /// <summary>Notes struck in quick succession and left to ring together.</summary>
        static AudioClip Chime(string name, float[] notes, float spacing, float duration, float volume, float lowpass)
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
            return FromSamples(name, data);
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

        static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
