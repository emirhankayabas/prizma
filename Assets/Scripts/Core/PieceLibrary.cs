using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// The full set of shapes the game can deal, together with the weights that decide how often
    /// each one shows up. Weights are the main difficulty dial: big awkward shapes choke the board,
    /// so they stay rarer than the small filler pieces.
    /// </summary>
    public static class PieceLibrary
    {
        public readonly struct WeightedShape
        {
            public readonly PieceShape Shape;
            public readonly float Weight;

            public WeightedShape(PieceShape shape, float weight)
            {
                Shape = shape;
                Weight = weight;
            }
        }

        public static readonly IReadOnlyList<WeightedShape> All = Build();

        static WeightedShape[] Build()
        {
            var list = new List<WeightedShape>();

            void Add(float weight, string id, params string[] rows)
                => list.Add(new WeightedShape(PieceShape.FromPattern(id, rows), weight));

            // --- Singles and short bars: the pieces that rescue a crowded board. ---
            Add(3.0f, "dot", "X");

            Add(4.0f, "bar2_h", "XX");
            Add(4.0f, "bar2_v", "X", "X");

            Add(4.0f, "bar3_h", "XXX");
            Add(4.0f, "bar3_v", "X", "X", "X");

            Add(3.0f, "bar4_h", "XXXX");
            Add(3.0f, "bar4_v", "X", "X", "X", "X");

            Add(1.6f, "bar5_h", "XXXXX");
            Add(1.6f, "bar5_v", "X", "X", "X", "X", "X");

            // --- Squares. The 3x3 is the single most dangerous piece in the deal. ---
            Add(3.2f, "square2", "XX", "XX");
            Add(0.8f, "square3", "XXX", "XXX", "XXX");

            // --- Small corners (3 cells): the workhorses of the mid game. ---
            Add(2.4f, "corner_tl", "XX", "X.");
            Add(2.4f, "corner_tr", "XX", ".X");
            Add(2.4f, "corner_bl", "X.", "XX");
            Add(2.4f, "corner_br", ".X", "XX");

            // --- Big corners (5 cells), one per rotation. ---
            Add(1.1f, "big_corner_tl", "XXX", "X..", "X..");
            Add(1.1f, "big_corner_tr", "XXX", "..X", "..X");
            Add(1.1f, "big_corner_bl", "X..", "X..", "XXX");
            Add(1.1f, "big_corner_br", "..X", "..X", "XXX");

            // --- T tetrominoes. ---
            Add(1.4f, "t_up", "XXX", ".X.");
            Add(1.4f, "t_down", ".X.", "XXX");
            Add(1.4f, "t_left", ".X", "XX", ".X");
            Add(1.4f, "t_right", "X.", "XX", "X.");

            // --- L / J tetrominoes. ---
            Add(1.2f, "l_0", "X.", "X.", "XX");
            Add(1.2f, "l_1", "XXX", "X..");
            Add(1.2f, "l_2", "XX", ".X", ".X");
            Add(1.2f, "l_3", "..X", "XXX");

            Add(1.2f, "j_0", ".X", ".X", "XX");
            Add(1.2f, "j_1", "X..", "XXX");
            Add(1.2f, "j_2", "XX", "X.", "X.");
            Add(1.2f, "j_3", "XXX", "..X");

            // --- S / Z tetrominoes: the ones that leave holes. ---
            Add(0.9f, "s_h", ".XX", "XX.");
            Add(0.9f, "s_v", "X.", "XX", ".X");
            Add(0.9f, "z_h", "XX.", ".XX");
            Add(0.9f, "z_v", ".X", "XX", "X.");

            return list.ToArray();
        }

        /// <summary>
        /// The library instance covering the same cells, so a rotated or restored piece keeps a
        /// stable id. Returns the shape itself when the library has no match.
        /// </summary>
        public static PieceShape Canonical(PieceShape shape)
        {
            if (shape == null) return null;

            for (int i = 0; i < All.Count; i++)
                if (All[i].Shape.SameCells(shape))
                    return All[i].Shape;

            return shape;
        }

        /// <summary>
        /// Picks a shape using the library weights.
        ///
        /// <paramref name="smallBias"/> tilts the roll towards smaller pieces: 0 is the plain
        /// library distribution, 1 makes a four-cell piece about half as likely as it was and a
        /// nine-cell square about a quarter as likely. Nothing is ever removed from the deal, so
        /// a crowded board can still be handed an awkward shape — it is just less likely to be.
        ///
        /// This is the dial that answers "there is nowhere to put anything". Runs were ending with
        /// the board only about two thirds full: the player was not being crowded out, they were
        /// being handed pieces that did not fit the holes they had. Without this the only defence
        /// was the candidate ranking, and when every one of the sixteen candidates happens to be
        /// built from big shapes, ranking them changes nothing.
        /// </summary>
        public static PieceShape PickWeighted(Rng random, float smallBias = 0f)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var table = new float[All.Count];
            float total = BuildWeights(smallBias, table);
            return PickFrom(random, table, total);
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the weights for a given bias and returns their sum.
        ///
        /// Built once per deal rather than once per piece: the dealer rolls three shapes for each
        /// of sixteen candidates, so folding the bias in at pick time meant raising every shape's
        /// weight to a power forty-eight times over instead of once.
        /// </summary>
        public static float BuildWeights(float smallBias, float[] into)
        {
            float total = 0f;

            for (int i = 0; i < All.Count; i++)
            {
                var entry = All[i];

                // Four cells is the pivot: smaller than that gains, larger loses.
                float weight = smallBias <= 0.001f
                    ? entry.Weight
                    : entry.Weight * (float)Math.Pow(4f / entry.Shape.CellCount, smallBias);

                into[i] = weight;
                total += weight;
            }

            return total;
        }

        /// <summary>Rolls one shape against a table from <see cref="BuildWeights"/>.</summary>
        public static PieceShape PickFrom(Rng random, float[] weights, float total)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            double roll = random.NextDouble() * total;
            for (int i = 0; i < All.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0d)
                    return All[i].Shape;
            }

            return All[All.Count - 1].Shape;
        }

        public static PieceShape ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Shape.Id == id)
                    return All[i].Shape;
            }

            throw new KeyNotFoundException($"No piece shape with id '{id}'.");
        }
    }
}
