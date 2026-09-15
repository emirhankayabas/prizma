using System;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Decides how loud each synthesised clip leaves the game — the step a published mobile game
    /// gets from a mastering pass, done here in code because there are no sound files to master.
    ///
    /// The set used to ship at whatever level each recipe happened to produce: measured, the
    /// loudest clip peaked at -5 dBFS and the most frequent ones (pickup, place, the menu tap)
    /// at -13 to -19. Nothing in the chain ever raised them, so "effects at 100%" was a ceiling
    /// set 5 to 24 dB under what the device could play. Now every clip is brought to a loudness
    /// chosen for its role and held under -1 dBFS by a transparent limiter; the recipe — pitch,
    /// envelope, filter, the timbre — is not touched.
    ///
    /// Unity-free, so <c>Tools/AudioCheck</c> measures exactly what the game plays.
    /// </summary>
    public static class SoundMaster
    {
        /// <summary>-1 dBFS: the usual true-peak ceiling. Room for the resampler and the bus limiter.</summary>
        public const float Ceiling = 0.891f;

        /// <summary>
        /// Slider defaults for a fresh install. Effects used to start at 85% and music at 55% of
        /// a 45% ceiling; with the clips mastered, full effects is the level the mix was set at,
        /// and the music sits about nine dB under a clear at 70%.
        /// </summary>
        public const float DefaultEffectsVolume = 1f;
        public const float DefaultMusicVolume = 0.7f;

        /// <summary>
        /// Target loudness per role, in dB — the loudest 100 ms of a clip, RMS, after a
        /// K-weighting-like filter. The ladder between roles is the mix: a clear is the reward
        /// and sits on top; the place thud is heard hundreds of times a run and sits under it;
        /// the menu tap and the pickup are the quietest things that still have to be heard.
        /// </summary>
        public static float TargetFor(string clip)
        {
            if (clip.StartsWith("clear", StringComparison.Ordinal)) return -9f;
            if (clip.StartsWith("combo", StringComparison.Ordinal)) return -10.5f;
            if (clip.StartsWith("star", StringComparison.Ordinal)) return -10f;

            switch (clip)
            {
                case "fanfare": return -8.5f;
                case "prism": return -10f;
                case "gem": return -12f;
                case "charge": return -11f;
                case "bomb": return -9.5f;
                case "place": return -12f;
                case "ice": return -13f;
                case "rotate": return -13f;
                case "reroll": return -13f;
                case "gameover": return -11f;
                case "stuck": return -12f;
                case "invalid": return -14f;
                case "pickup": return -15f;
                case "click": return -15f;
                case "music": return -15f;
                default: return -12f;
            }
        }

        /// <summary>
        /// Most gain reduction the limiter may apply to reach a target. Beyond this a clip is
        /// left quieter rather than squashed: a flattened attack is what makes loud game audio
        /// sound cheap.
        /// </summary>
        const float MaxReductionDb = 6f;

        /// <summary>Brings a clip to its target loudness, in place.</summary>
        public static void Master(float[] data, int sampleRate, float targetDb)
        {
            if (data == null || data.Length == 0) return;

            var source = (float[])data.Clone();
            double loudness = Loudness(source, sampleRate);
            if (loudness < -90.0) return;

            float gain = (float)Math.Pow(10.0, (targetDb - loudness) / 20.0);

            // Never more than the limiter is allowed to take back.
            float peak = Peak(source);
            float maxGain = Ceiling / peak * (float)Math.Pow(10.0, MaxReductionDb / 20.0);
            gain = Math.Min(gain, maxGain);

            // Limiting takes a little loudness back; two corrections land within a few tenths of a dB.
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < data.Length; i++) data[i] = source[i] * gain;
                Limit(data, sampleRate, Ceiling);

                double reached = Loudness(data, sampleRate);
                double error = targetDb - reached;
                if (Math.Abs(error) < 0.25 || gain >= maxGain) break;
                gain = Math.Min(maxGain, gain * (float)Math.Pow(10.0, error / 20.0));
            }
        }

        /// <summary>
        /// A look-ahead peak limiter run over a whole clip. The gain needed at each sample is
        /// smoothed so it falls ahead of a peak (the backward pass, ~1.5 ms) and recovers slowly
        /// after it (the forward pass, ~60 ms), which is what keeps it from being heard as
        /// distortion. Applied offline, once, when the clip is built.
        /// </summary>
        public static void Limit(float[] data, int sampleRate, float ceiling)
        {
            int n = data.Length;
            var gain = new float[n];

            for (int i = 0; i < n; i++)
            {
                float a = Math.Abs(data[i]);
                gain[i] = a > ceiling ? ceiling / a : 1f;
            }

            float release = 1f - (float)Math.Exp(-1.0 / (0.060 * sampleRate));
            float attack = 1f - (float)Math.Exp(-1.0 / (0.0015 * sampleRate));

            float g = 1f;
            for (int i = 0; i < n; i++)
            {
                g = gain[i] < g ? gain[i] : g + (1f - g) * release;
                gain[i] = g;
            }

            g = 1f;
            for (int i = n - 1; i >= 0; i--)
            {
                g = gain[i] < g ? gain[i] : g + (1f - g) * attack;
                gain[i] = g;
            }

            for (int i = 0; i < n; i++)
            {
                float v = data[i] * gain[i];
                data[i] = v > ceiling ? ceiling : v < -ceiling ? -ceiling : v;
            }
        }

        public static float Peak(float[] data)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Math.Abs(data[i]);
                if (a > peak) peak = a;
            }
            return peak;
        }

        /// <summary>
        /// The loudest 100 ms of a clip, RMS in dB, after a high-pass at 60 Hz and a +4 dB shelf
        /// from 1.5 kHz — close to the K-weighting broadcast loudness uses, so a bright bell and a
        /// low thud with the same number sound about as loud as each other.
        /// </summary>
        public static double Loudness(float[] data, int sampleRate)
        {
            var x = new double[data.Length];
            for (int i = 0; i < data.Length; i++) x[i] = data[i];

            Biquad.HighPass(sampleRate, 60, 0.5).Run(x);
            Biquad.HighShelf(sampleRate, 1500, 4.0).Run(x);

            int window = Math.Min(sampleRate / 10, x.Length);
            int hop = Math.Max(1, sampleRate / 200);

            double sum = 0, best = 0;
            for (int i = 0; i < window; i++) sum += x[i] * x[i];
            best = sum;

            // Sliding window, advanced one hop at a time.
            for (int start = hop; start + window <= x.Length; start += hop)
            {
                for (int i = start - hop; i < start; i++) sum -= x[i] * x[i];
                for (int i = start + window - hop; i < start + window; i++) sum += x[i] * x[i];
                if (sum > best) best = sum;
            }

            return 10.0 * Math.Log10(Math.Max(1e-12, best / window));
        }

        /// <summary>
        /// The limiter on the game's output. Each clip is mastered to -1 dBFS on its own, but a
        /// good move plays the place thud, the clear and the combo reply within 70 ms of each
        /// other, and their sum would go past full scale and clip hard on the device. This holds
        /// the sum under the ceiling instead: the gain follows the peak down at once and eases
        /// back over 80 ms. Nothing it does is heard on a single sound, which never reaches it.
        /// </summary>
        public sealed class BusLimiter
        {
            const float Threshold = 0.94f;

            float _envelope;
            float _release;

            public void Reset(int sampleRate)
            {
                _envelope = 0f;
                _release = (float)Math.Exp(-1.0 / (0.080 * Math.Max(1, sampleRate)));
            }

            /// <summary>Gain for a frame whose loudest channel is <paramref name="peak"/>.</summary>
            public float Gain(float peak)
            {
                _envelope = peak > _envelope ? peak : _envelope * _release + peak * (1f - _release);
                return _envelope > Threshold ? Threshold / _envelope : 1f;
            }

            public float Process(float sample) => sample * Gain(Math.Abs(sample));
        }

        /// <summary>
        /// Harmonics of the low end, added above 500 Hz — the usual trick for small speakers. A
        /// phone speaker plays almost nothing under 700 Hz, so a 130-220 Hz thud reaches the
        /// player as silence however loud it is; its overtones let the ear fill the fundamental
        /// back in. This changes the timbre (brighter, woodier on headphones), so it is not in the
        /// game: <c>Tools/AudioCheck wav</c> renders it as a third variant to listen to first.
        /// </summary>
        public static void AddPresence(float[] data, int sampleRate, float amount)
        {
            var low = new double[data.Length];
            for (int i = 0; i < data.Length; i++) low[i] = data[i];
            Biquad.LowPass(sampleRate, 450, 0.707).Run(low);

            double peak = 0;
            for (int i = 0; i < low.Length; i++) peak = Math.Max(peak, Math.Abs(low[i]));
            if (peak <= 1e-9) return;

            for (int i = 0; i < low.Length; i++)
            {
                double s = Math.Tanh(4.0 * low[i] / peak);
                low[i] = (s + 0.35 * s * s) * peak; // odd and even overtones
            }

            Biquad.HighPass(sampleRate, 500, 0.707).Run(low);
            Biquad.HighPass(sampleRate, 500, 0.707).Run(low);

            for (int i = 0; i < data.Length; i++) data[i] += amount * (float)low[i];
        }

        /// <summary>A standard RBJ biquad, run in place over a buffer.</summary>
        public sealed class Biquad
        {
            double _b0, _b1, _b2, _a1, _a2;

            public static Biquad LowPass(int rate, double hz, double q)
            {
                double w = 2 * Math.PI * hz / rate, alpha = Math.Sin(w) / (2 * q), cos = Math.Cos(w), a0 = 1 + alpha;
                return new Biquad
                {
                    _b0 = (1 - cos) / 2 / a0, _b1 = (1 - cos) / a0, _b2 = (1 - cos) / 2 / a0,
                    _a1 = -2 * cos / a0, _a2 = (1 - alpha) / a0
                };
            }

            public static Biquad HighPass(int rate, double hz, double q)
            {
                double w = 2 * Math.PI * hz / rate, alpha = Math.Sin(w) / (2 * q), cos = Math.Cos(w), a0 = 1 + alpha;
                return new Biquad
                {
                    _b0 = (1 + cos) / 2 / a0, _b1 = -(1 + cos) / a0, _b2 = (1 + cos) / 2 / a0,
                    _a1 = -2 * cos / a0, _a2 = (1 - alpha) / a0
                };
            }

            public static Biquad HighShelf(int rate, double hz, double gainDb)
            {
                double a = Math.Pow(10, gainDb / 40), w = 2 * Math.PI * hz / rate, cos = Math.Cos(w), sin = Math.Sin(w);
                double sq = 2 * Math.Sqrt(a) * (sin / 2 * Math.Sqrt(2));
                double a0 = (a + 1) - (a - 1) * cos + sq;
                return new Biquad
                {
                    _b0 = a * ((a + 1) + (a - 1) * cos + sq) / a0,
                    _b1 = -2 * a * ((a - 1) + (a + 1) * cos) / a0,
                    _b2 = a * ((a + 1) + (a - 1) * cos - sq) / a0,
                    _a1 = 2 * ((a - 1) - (a + 1) * cos) / a0,
                    _a2 = ((a + 1) - (a - 1) * cos - sq) / a0
                };
            }

            public void Run(double[] x)
            {
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    double v = _b0 * x[i] + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;
                    x2 = x1; x1 = x[i]; y2 = y1; y1 = v; x[i] = v;
                }
            }
        }
    }
}
