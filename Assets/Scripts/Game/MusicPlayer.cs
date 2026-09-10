using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Synthesises a calm looping background track at startup — no audio files in the project.
    /// Notes are written into the buffer with a wrapping index, so every release tail spills into
    /// the start of the loop and the seam is inaudible.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        const int SampleRate = 22050;
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

        AudioSource _source;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = BuildLoop();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            ApplyVolume();
            _source.Play();
        }

        // Tracking the setting directly means volume stays correct no matter who changed it —
        // the settings slider, a restored preference, or code.
        void OnEnable() => GameSettings.Changed += ApplyVolume;
        void OnDisable() => GameSettings.Changed -= ApplyVolume;

        public void ApplyVolume()
        {
            if (_source == null) return;

            // Music sits well under the effects; 0.45 at full slider is the right ceiling.
            _source.volume = GameSettings.EffectiveMusicVolume * 0.45f;
            _source.mute = GameSettings.EffectiveMusicVolume <= 0.001f;
        }

        AudioClip BuildLoop()
        {
            int chordSamples = Mathf.RoundToInt(SampleRate * ChordSeconds);
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

            var clip = AudioClip.Create("music_loop", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A sustained tone with a slow attack and a release that wraps past the loop end.</summary>
        static void AddVoice(float[] data, int start, int holdSamples, float freq, float amp,
            float attack, float release, float warmth)
        {
            int attackSamples = Mathf.RoundToInt(SampleRate * attack);
            int releaseSamples = Mathf.RoundToInt(SampleRate * release);
            int length = holdSamples + releaseSamples;

            float phase = 0f;
            float step = 2f * Mathf.PI * freq / SampleRate;

            for (int i = 0; i < length; i++)
            {
                phase += step;

                float envelope;
                if (i < attackSamples) envelope = i / (float)attackSamples;
                else if (i < holdSamples) envelope = 1f;
                else envelope = 1f - (i - holdSamples) / (float)releaseSamples;

                envelope = Mathf.Clamp01(envelope);
                envelope *= envelope; // gentler swell than a linear ramp

                float sample = Mathf.Sin(phase) + warmth * 0.5f * Mathf.Sin(phase * 2f) + warmth * 0.2f * Mathf.Sin(phase * 3f);
                data[(start + i) % data.Length] += sample * envelope * amp;
            }
        }

        /// <summary>A short plucked note: instant attack, exponential decay.</summary>
        static void AddPluck(float[] data, int start, int noteSamples, float freq, float amp)
        {
            int length = noteSamples * 3; // let it ring past its slot
            float phase = 0f;
            float step = 2f * Mathf.PI * freq / SampleRate;

            for (int i = 0; i < length; i++)
            {
                phase += step;

                float k = i / (float)length;
                float envelope = Mathf.Exp(-6f * k);
                if (i < 64) envelope *= i / 64f; // avoid a click on the attack

                float sample = Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f);
                data[(start + i) % data.Length] += sample * envelope * amp;
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
    }
}
