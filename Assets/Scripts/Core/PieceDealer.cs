using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Chooses each tray of three pieces by looking at the board instead of rolling dice.
    ///
    /// Pure random generation is what makes a block puzzle feel unfair: it deals dead sets with no
    /// warning and never sets up a satisfying clear. This deals several candidate trays, plays each
    /// one out greedily, and prefers the ones that keep the player alive and leave a line clear
    /// within reach.
    ///
    /// The bias is deliberately soft. It rises as the board fills and eases off during a long run,
    /// and the winning tray is picked from near the top of the ranking rather than off it, so the
    /// help stays felt rather than seen — and the player can still lose.
    /// </summary>
    public sealed class PieceDealer
    {
        /// <summary>Trays considered per deal. More candidates means finer selection, at a cost.</summary>
        const int CandidateCount = 16;

        /// <summary>Assist applied even on a wide-open board, so trays are never purely random.</summary>
        const float BaseAssist = 0.16f;

        /// <summary>
        /// How far the per-deal drift can wander, and how much of it carries to the next deal.
        /// Without this the assist is a smooth function of the board, and a player quickly senses
        /// the game easing off exactly when they are in trouble. The drift makes help arrive in
        /// waves — a few generous trays, then a lean stretch — which reads as luck.
        /// </summary>
        const float DriftAmount = 0.55f;
        const float DriftMemory = 0.7f;

        /// <summary>
        /// Assist can never reach 1. At full strength the dealer would keep a competent player
        /// alive forever — the run stops being a game and becomes a screensaver. This ceiling is
        /// what guarantees every run eventually ends.
        /// </summary>
        const float AssistCeiling = 0.82f;

        /// <summary>Board fill fraction at which help starts, and where it is at full strength.</summary>
        const float PressureStart = 0.35f;
        const float PressureFull = 0.80f;

        /// <summary>Score at which the long-run difficulty ramp reaches its maximum.</summary>
        const float FatigueScore = 3500f;
        const float FatigueMax = 0.5f;

        readonly Rng _random;

        // Reused across every candidate so a deal allocates almost nothing.
        BoardModel _scratch;
        readonly List<PieceShape> _remaining = new List<PieceShape>(4);
        readonly List<int> _slots = new List<int>(4);

        public PieceDealer(Rng random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>The correlated random walk behind the assist. Part of a saved run.</summary>
        public float Drift { get; set; }

        /// <summary>How strongly the last deal was steered. Exposed for tuning and tests.</summary>
        public float LastAssist { get; private set; }

        /// <summary>Whether the last chosen tray could be played out in full. For tests.</summary>
        public bool LastFullyPlayable { get; private set; }

        readonly struct Candidate
        {
            public readonly PieceShape[] Shapes;
            public readonly float Score;
            public readonly int Placed;

            public Candidate(PieceShape[] shapes, float score, int placed)
            {
                Shapes = shapes;
                Score = score;
                Placed = placed;
            }
        }

        /// <summary>
        /// Fills the tray in place. With <paramref name="onlyUnused"/> it deals only into the slots
        /// still holding a piece — the reroll power — and leaves spent slots spent.
        /// </summary>
        public void Deal(BoardModel board, TrayPiece[] tray, int paletteSize, int score, bool onlyUnused = false)
        {
            _slots.Clear();
            for (int i = 0; i < tray.Length; i++)
                if (!onlyUnused || !tray[i].Used)
                    _slots.Add(i);

            if (_slots.Count == 0) return;

            float assist = ComputeAssist(board, score);
            LastAssist = assist;

            var candidates = new List<Candidate>(CandidateCount);

            for (int i = 0; i < CandidateCount; i++)
            {
                var shapes = new PieceShape[_slots.Count];
                for (int s = 0; s < shapes.Length; s++)
                    shapes[s] = PieceLibrary.PickWeighted(_random);

                var outcome = PlayOut(board, shapes);
                candidates.Add(new Candidate(shapes, ScoreOutcome(outcome, shapes, assist), outcome.Placed));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            var chosen = Choose(candidates, assist);
            LastFullyPlayable = chosen.Placed == _slots.Count;

            for (int i = 0; i < _slots.Count; i++)
            {
                var piece = tray[_slots[i]];
                piece.Shape = chosen.Shapes[i];
                piece.ColorIndex = _random.Next(paletteSize);
                piece.Used = false;
            }

            // Three identical colours looks like a bug rather than a coincidence.
            if (_slots.Count == tray.Length && tray.Length >= 3 && paletteSize > 1 &&
                tray[0].ColorIndex == tray[1].ColorIndex && tray[1].ColorIndex == tray[2].ColorIndex)
            {
                tray[2].ColorIndex = (tray[2].ColorIndex + 1 + _random.Next(paletteSize - 1)) % paletteSize;
            }
        }

        // ------------------------------------------------------------------ assist

        float ComputeAssist(BoardModel board, int score)
        {
            int cells = board.Size * board.Size;
            float pressure = cells == 0 ? 0f : board.OccupiedCount / (float)cells;

            // Eased so a half-full board barely registers and a crowded one asks loudly.
            float p = Clamp01((pressure - PressureStart) / (PressureFull - PressureStart));
            float fromPressure = p * p;

            // Long runs get steadily less help, so the game keeps getting harder.
            float fatigue = Clamp01(score / FatigueScore) * FatigueMax;

            // A correlated random walk, so runs of generosity and runs of hardship both happen.
            Drift = Drift * DriftMemory + (float)(_random.NextDouble() - 0.5d) * DriftAmount;

            float assist = BaseAssist + fromPressure * (1f - BaseAssist) - fatigue + Drift;
            return Clamp01(assist) * AssistCeiling;
        }

        // ------------------------------------------------------------------ simulation

        readonly struct Outcome
        {
            public readonly int Placed;
            public readonly int Lines;
            public readonly int Contact;
            public readonly int FinalOccupancy;

            public Outcome(int placed, int lines, int contact, int finalOccupancy)
            {
                Placed = placed;
                Lines = lines;
                Contact = contact;
                FinalOccupancy = finalOccupancy;
            }
        }

        /// <summary>
        /// Plays the tray out the way a reasonable player would: at each step take the piece and
        /// position that clears the most, breaking ties on the snuggest fit.
        /// </summary>
        Outcome PlayOut(BoardModel board, PieceShape[] shapes)
        {
            if (_scratch == null || _scratch.Size != board.Size) _scratch = new BoardModel(board.Size);
            _scratch.CopyFrom(board);

            _remaining.Clear();
            for (int i = 0; i < shapes.Length; i++) _remaining.Add(shapes[i]);

            int placed = 0, lines = 0, contact = 0;

            while (_remaining.Count > 0)
            {
                int bestIndex = -1, bestCol = 0, bestRow = 0;
                int bestLines = -1, bestContact = -1;

                for (int i = 0; i < _remaining.Count; i++)
                {
                    var shape = _remaining[i];
                    int maxCol = _scratch.Size - shape.Width;
                    int maxRow = _scratch.Size - shape.Height;

                    for (int row = 0; row <= maxRow; row++)
                    for (int col = 0; col <= maxCol; col++)
                    {
                        if (!_scratch.CanPlace(shape, col, row)) continue;

                        int producedLines = _scratch.CountCompletedLines(shape, col, row);
                        int producedContact = _scratch.ContactScore(shape, col, row);

                        if (producedLines > bestLines ||
                            (producedLines == bestLines && producedContact > bestContact))
                        {
                            bestIndex = i;
                            bestCol = col;
                            bestRow = row;
                            bestLines = producedLines;
                            bestContact = producedContact;
                        }
                    }
                }

                if (bestIndex < 0) break; // nothing left fits

                var chosen = _remaining[bestIndex];
                _remaining.RemoveAt(bestIndex);

                var clear = _scratch.Place(chosen, bestCol, bestRow, 0);
                placed++;
                lines += clear.LinesCleared;
                contact += bestContact;
            }

            return new Outcome(placed, lines, contact, _scratch.OccupiedCount);
        }

        // ------------------------------------------------------------------ scoring

        static float ScoreOutcome(Outcome outcome, PieceShape[] shapes, float assist)
        {
            float score = outcome.Placed * 120f;

            // A tray that cannot be played out is what kills a run without warning. Strongly
            // discouraged, never forbidden — at low assist an awkward tray still gets through.
            if (outcome.Placed < shapes.Length)
                score -= (shapes.Length - outcome.Placed) * 200f * assist;

            // The combo carrot: reachable clears matter more the more help is due. Kept modest so
            // the game is not constantly handing out chains.
            score += outcome.Lines * (14f + 42f * assist);

            score += outcome.Contact * 1.6f;
            score -= outcome.FinalOccupancy * (6f + 22f * assist);
            score += DistinctCount(shapes) * 8f;

            return score;
        }

        static int DistinctCount(PieceShape[] shapes)
        {
            int distinct = 0;
            for (int i = 0; i < shapes.Length; i++)
            {
                bool seen = false;
                for (int j = 0; j < i && !seen; j++)
                    if (shapes[j].Id == shapes[i].Id) seen = true;
                if (!seen) distinct++;
            }

            return distinct;
        }

        /// <summary>
        /// Walks down the ranking, stopping with a probability that rises with assist. Under
        /// pressure it nearly always takes the best tray; on an open board it wanders further down,
        /// which is what stops the deals from feeling scripted.
        /// </summary>
        Candidate Choose(List<Candidate> ranked, float assist)
        {
            Candidate chosen;

            // Most deals ignore the ranking entirely and take a candidate at random — which, since
            // every candidate was itself rolled at random, is a straight random tray. The ranking
            // only takes over as help becomes due. Without this gate the mere act of ranking makes
            // every tray a good one, and the game quietly plays itself.
            float useRanking = assist * 1.15f + 0.10f;

            if (_random.NextDouble() > useRanking)
            {
                chosen = ranked[_random.Next(ranked.Count)];
            }
            else
            {
                float stopChance = Lerp(0.35f, 0.85f, assist);

                int index = 0;
                while (index < ranked.Count - 1 && _random.NextDouble() > stopChance)
                    index++;

                chosen = ranked[index];
            }

            // Never hand out a completely dead tray while a playable one was on the table.
            if (chosen.Placed == 0 && ranked[0].Placed > 0)
                chosen = ranked[0];

            return chosen;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
