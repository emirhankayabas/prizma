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
        /// <summary>
        /// Trays considered per deal, and how many more get considered when help is due.
        ///
        /// Sixteen is plenty on an open board, and on a tight one it is the real ceiling on what
        /// the dealer can do: every knob can be at full strength and still only pick the best of
        /// sixteen random trays, none of which may fit. The extra candidates are only rolled when
        /// the assist is already high, so the cost is paid exactly when it buys something.
        /// </summary>
        const int CandidateCount = 16;
        const int CandidateBonus = 28;

        /// <summary>Assist applied even on a wide-open board, so trays are never purely random.</summary>
        const float BaseAssist = 0.26f;

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
        const float PressureStart = 0.25f;
        const float PressureFull = 0.68f;

        /// <summary>
        /// Shape of the pressure ramp. Squared was too back-loaded: half a board full returned
        /// barely a tenth of full strength, so help only really arrived once the run was already
        /// lost. Just above linear keeps the late-game urgency without the dead middle.
        /// </summary>
        const float PressureCurve = 1.25f;

        /// <summary>
        /// How far the deal leans towards smaller pieces when the board is crowded. Applied to the
        /// candidates themselves rather than to the ranking, because ranking sixteen trays that
        /// are all built from big shapes cannot produce a small one.
        /// </summary>
        const float MaxSmallBias = 1.15f;

        /// <summary>
        /// The long-run difficulty ramp: where it starts biting, where it maxes out, and how much
        /// it takes. It used to start at zero and reach half the assist by 3,500 points, which
        /// meant an ordinary casual run had already surrendered a third of its help while still
        /// being short and unsatisfying. It should be the thing that eventually brings an expert
        /// back down, not the thing that stops everybody — so nothing happens at all until a run
        /// is past the point a casual player usually reaches.
        /// </summary>
        const float FatigueStart = 3000f;
        const float FatigueScore = 14000f;
        const float FatigueMax = 0.5f;

        readonly Rng _random;

        // Reused across every candidate so a deal allocates almost nothing.
        BoardModel _scratch;
        readonly List<PieceShape> _remaining = new List<PieceShape>(4);
        readonly List<int> _slots = new List<int>(4);
        readonly float[] _weights = new float[PieceLibrary.All.Count];

        // Everything a deal needs, allocated once. The dealer runs on the main thread in the
        // middle of play, so its garbage is a frame-rate problem, not a memory one.
        readonly ClearResult _simClear = new ClearResult();
        readonly List<Candidate> _candidates = new List<Candidate>(CandidateCount + CandidateBonus);
        PieceShape[][] _shapePool;

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

            // Smaller pieces at both ends of the board, for opposite reasons.
            //
            // A crowded board needs them to have anywhere to go at all. A nearly empty one needs
            // them too: that is the moment the board could actually be finished off, and handing
            // out big shapes there refills it immediately. The first version only did the crowded
            // half, which is why the board was never once emptied in forty runs while the
            // reference game does it often enough that players chase it.
            //
            // Held well under full strength either way, so the deal never turns into a handful of
            // dots — that reads instantly as the game taking pity.
            int boardCells = board.Size * board.Size;
            float fill = boardCells == 0 ? 0f : board.OccupiedCount / (float)boardCells;

            float crowded = Clamp01((fill - 0.40f) / 0.40f);
            float nearlyClear = board.OccupiedCount == 0 ? 0f : Clamp01((14f - board.OccupiedCount) / 14f);
            float smallBias = Math.Max(crowded, nearlyClear * 0.85f) * MaxSmallBias * (0.45f + 0.55f * assist);
            float weightTotal = PieceLibrary.BuildWeights(smallBias, _weights);

            int candidateCount = CandidateCount + (int)(assist * CandidateBonus);

            if (_shapePool == null || _shapePool.Length < candidateCount ||
                _shapePool[0].Length != _slots.Count)
            {
                _shapePool = new PieceShape[CandidateCount + CandidateBonus][];
                for (int i = 0; i < _shapePool.Length; i++) _shapePool[i] = new PieceShape[_slots.Count];
            }

            var candidates = _candidates;
            candidates.Clear();

            for (int i = 0; i < candidateCount; i++)
            {
                var shapes = _shapePool[i];
                for (int s = 0; s < shapes.Length; s++)
                    shapes[s] = PieceLibrary.PickFrom(_random, _weights, weightTotal);

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

            // Eased so an open board asks quietly and a crowded one asks loudly — but the middle
            // of the board, where most of a run is actually played, now counts for something.
            float p = Clamp01((pressure - PressureStart) / (PressureFull - PressureStart));
            float fromPressure = (float)Math.Pow(p, PressureCurve);

            // Long runs get steadily less help, so the game keeps getting harder — but only once
            // the run is genuinely long.
            float fatigue = Clamp01((score - FatigueStart) / (FatigueScore - FatigueStart)) * FatigueMax;

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

            /// <summary>
            /// The most lines any single placement took. <see cref="Lines"/> alone could not see
            /// the difference between a tray that clears three lines across three moves and one
            /// that clears three at once — and the second is the whole feeling the player was
            /// missing. Raising the reward on Lines did nothing for it; this is the term that does.
            /// </summary>
            public readonly int BestSingle;

            /// <summary>
            /// How many of the placements cleared something. The combo streak counts consecutive
            /// clearing <em>moves</em>, so this is the term that actually keeps a streak alive: a
            /// tray where all three pieces clear carries the multiplier three moves further, and a
            /// tray where only one does breaks it.
            /// </summary>
            public readonly int ClearingMoves;

            /// <summary>
            /// How many of the pieces fit the board as it stands, before anything is placed.
            ///
            /// <see cref="Placed"/> is not the same promise. It comes from a play-out that picks
            /// the best order, so a tray counted as fully playable can still be one a person
            /// bricks themselves with: put the wrong piece down first and the other two no longer
            /// fit anywhere. A player does not get to try six orderings and keep the good one.
            /// This counts the pieces that fit right now, in any order, which is the guarantee
            /// that actually matches how the tray is played.
            /// </summary>
            public readonly int FitsNow;

            /// <summary>
            /// Rows and columns left one or two cells short of completing, weighted towards the
            /// ones that are one short.
            ///
            /// This is the term that resolves the contradiction in what the board is asked to be.
            /// Pushing it towards empty gives the player room, but an empty board has nothing
            /// close to a line, so nothing clears, so the streak dies and the run feels flat —
            /// measured, the average streak sat at 1.3 no matter how hard clears were rewarded.
            /// A board that is half full of nearly-finished lines is what actually produces
            /// combos, and it still leaves somewhere to put things.
            /// </summary>
            public readonly int Primed;

            public Outcome(int placed, int lines, int contact, int finalOccupancy, int bestSingle,
                int clearingMoves, int primed, int fitsNow)
            {
                Placed = placed;
                Lines = lines;
                Contact = contact;
                FinalOccupancy = finalOccupancy;
                BestSingle = bestSingle;
                ClearingMoves = clearingMoves;
                Primed = primed;
                FitsNow = fitsNow;
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
            int fitsNow = 0;
            for (int i = 0; i < shapes.Length; i++)
            {
                _remaining.Add(shapes[i]);
                if (FitsAnywhere(board, shapes[i])) fitsNow++;
            }

            int placed = 0, lines = 0, contact = 0, bestSingle = 0, clearingMoves = 0;

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

                var clear = _scratch.Place(chosen, bestCol, bestRow, 0, _simClear);
                placed++;
                lines += clear.LinesCleared;
                contact += bestContact;
                if (clear.LinesCleared > bestSingle) bestSingle = clear.LinesCleared;
                if (clear.LinesCleared > 0) clearingMoves++;
            }

            return new Outcome(placed, lines, contact, _scratch.OccupiedCount, bestSingle,
                clearingMoves, CountPrimed(_scratch), fitsNow);
        }

        /// <summary>Whether a shape has anywhere at all to go on the board as it stands.</summary>
        static bool FitsAnywhere(BoardModel board, PieceShape shape)
        {
            int maxCol = board.Size - shape.Width;
            int maxRow = board.Size - shape.Height;

            for (int row = 0; row <= maxRow; row++)
            for (int col = 0; col <= maxCol; col++)
                if (board.CanPlace(shape, col, row))
                    return true;

            return false;
        }

        /// <summary>
        /// Lines the next tray could finish: two points for one cell short, one for two cells
        /// short. Cheap — the board keeps these tallies as cells change.
        /// </summary>
        static int CountPrimed(BoardModel board)
        {
            int primed = 0;
            int size = board.Size;

            for (int i = 0; i < size; i++)
            {
                int row = board.RowCount(i);
                if (row == size - 1) primed += 2;
                else if (row == size - 2) primed += 1;

                int col = board.ColumnCount(i);
                if (col == size - 1) primed += 2;
                else if (col == size - 2) primed += 1;
            }

            return primed;
        }

        // ------------------------------------------------------------------ scoring

        static float ScoreOutcome(Outcome outcome, PieceShape[] shapes, float assist)
        {
            float score = outcome.Placed * 120f;

            // A tray that cannot be played out is what kills a run without warning, and measured,
            // that is how runs were actually ending: with the board only two thirds full and
            // nothing in hand that fit. At 200 x assist this penalty was worth about sixty points
            // against terms worth hundreds, so it barely competed. Now it is the loudest term on
            // the board — still never absolute, so an awkward tray can still get through.
            if (outcome.Placed < shapes.Length)
                score -= (shapes.Length - outcome.Placed) * (150f + 650f * assist);

            // A piece with nowhere to go the moment the tray appears is the worst thing the dealer
            // can hand out, whatever the play-out says about the best ordering of the other two.
            if (outcome.FitsNow < shapes.Length)
                score -= (shapes.Length - outcome.FitsNow) * (120f + 520f * assist);

            // Clearing at all, kept moderate — this term mostly just keeps the run going.
            score += outcome.Lines * (20f + 46f * assist);

            // Keeping the streak alive. This is what the player means by "combo": the multiplier
            // on screen counts consecutive clearing moves, and it was dying almost every other
            // move because a tray only had to clear once to look good to the old scoring.
            score += outcome.ClearingMoves * (60f + 170f * assist);

            // Two or three lines from a single placement: the moment the whole mode is built
            // around, and the one the player said was missing. Rewarded steeply and separately,
            // because a tray that offers it is worth far more than one that merely clears a lot
            // over three separate moves.
            if (outcome.BestSingle > 1)
                score += (outcome.BestSingle - 1) * (70f + 150f * assist);

            score += outcome.Contact * 1.6f;

            // Lines left within reach of the next tray. Without this the dealer emptied the board
            // and the run went quiet; with it the board keeps a few nearly-finished lines around,
            // which is where the next clear — and the streak — comes from.
            score += outcome.Primed * (9f + 26f * assist);

            // Room to breathe. Eased back from the previous pass: pushed too hard it flattens the
            // board, and a flat board has nothing to clear.
            score -= outcome.FinalOccupancy * (7f + 20f * assist);

            // Emptying the board. In the reference game this happens often enough that players
            // chase it; here it happened once in forty runs.
            //
            // Measured, the board already gets down to about four blocks at some point in almost
            // every run — the moment is not far away, it is just never closed out. So the endgame
            // pull starts much earlier and bites much harder than the flat occupancy term above:
            // from a dozen blocks down, clearing the rest is worth more than anything else the
            // tray could do. The player still has to place them.
            if (outcome.FinalOccupancy == 0)
                score += 520f * assist;
            else if (outcome.FinalOccupancy <= 12)
                score += (13 - outcome.FinalOccupancy) * (9f + 24f * assist);

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
