using System;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// The one random source the rules use. A plain xorshift64* rather than System.Random,
    /// because its entire state is a single number: a run can be saved mid-game and resumed on
    /// the exact same sequence, and a seed produces the same deals on every platform.
    /// </summary>
    public sealed class Rng
    {
        const ulong Fallback = 0x2545F4914F6CDD1DUL;

        ulong _state;

        public Rng(int seed)
        {
            _state = SplitMix((ulong)(uint)seed ^ 0x9E3779B97F4A7C15UL);
            if (_state == 0) _state = Fallback;
        }

        /// <summary>Complete generator state. Round-trips through a save.</summary>
        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? Fallback : value;
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>Uniform in [0, maxExclusive). Returns 0 for a range of one or less.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)((NextULong() >> 33) % (ulong)maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

        public bool Chance(double probability) => NextDouble() < probability;

        static ulong SplitMix(ulong x)
        {
            unchecked
            {
                ulong z = x + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Stable text form of the state, for JSON saves that do not carry 64-bit integers.</summary>
        public string SaveState() => _state.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void LoadState(string text)
        {
            if (!ulong.TryParse(text, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong value))
                throw new FormatException($"Invalid generator state '{text}'.");
            State = value;
        }
    }
}
