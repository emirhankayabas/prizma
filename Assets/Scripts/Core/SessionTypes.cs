using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    public enum GameMode
    {
        /// <summary>Endless, random seed.</summary>
        Classic = 0,

        /// <summary>
        /// The day's puzzle: one generated board, goal and move budget per calendar day, the same
        /// for everyone. Solved once a day; retried as often as it takes.
        /// </summary>
        Daily = 1,

        /// <summary>A generated level with a goal and a move limit.</summary>
        Level = 2
    }

    public enum SessionState
    {
        Playing = 0,

        /// <summary>No tray piece fits, but charges remain: a power can still save the run.</summary>
        Stuck = 1,

        Won = 2,
        Lost = 3,

        /// <summary>
        /// A level's moves ran out short of the goal, but there are charges enough to buy more.
        /// The player chooses: spend them, or give up.
        /// </summary>
        OutOfMoves = 4
    }

    public enum PowerKind
    {
        /// <summary>Turn one tray piece a quarter turn.</summary>
        Rotate = 0,

        /// <summary>Deal fresh pieces into the unused tray slots.</summary>
        Reroll = 1,

        /// <summary>Blast a 3x3 area of the board clear.</summary>
        Bomb = 2,

        /// <summary>
        /// Smash a single block — ice, stone, shade and all. Not a prism power: a booster the
        /// adventure hands out, spent from the player's own stock, so it costs no charges.
        /// </summary>
        Hammer = 3
    }

    /// <summary>Why a run was lost — the result card says which, in its own words.</summary>
    public enum LossReason
    {
        None = 0,

        /// <summary>No piece in the tray fits anywhere.</summary>
        NoRoom = 1,

        /// <summary>A level's moves ran out.</summary>
        NoMoves = 2,

        /// <summary>A timer block counted down to zero before a line took it.</summary>
        Timer = 3
    }

    public enum GoalKind
    {
        /// <summary>Free every crystal on the board.</summary>
        Gems = 0,

        /// <summary>Clear a number of lines.</summary>
        Lines = 1,

        /// <summary>Reach a score.</summary>
        Score = 2,

        /// <summary>
        /// Light up every glow tile: a tile sits under a cell and goes out when a block on it is
        /// cleared away. Candy Crush's jelly, in a block puzzle's grammar — the floor, not a piece.
        /// </summary>
        Tiles = 3,

        /// <summary>Clear a number of blocks of one colour (<see cref="LevelDefinition.OrderColor"/>).</summary>
        Colors = 4,

        /// <summary>Clear every shade block off the board. Shade creeps into a free cell when left alone.</summary>
        Shade = 5
    }

    /// <summary>One piece sitting in the tray, waiting to be dragged out.</summary>
    public sealed class TrayPiece
    {
        public PieceShape Shape;
        public int ColorIndex;
        public bool Used;
    }

    /// <summary>How a session is set up. Everything a run needs to be rebuilt from scratch.</summary>
    public sealed class SessionConfig
    {
        public GameMode Mode = GameMode.Classic;
        public int Seed;
        public int BoardSize = 8;
        public int PaletteSize = 6;
        public bool PowersEnabled = true;
        public int StartCharges = PowerRules.StartCharges;

        /// <summary>
        /// Moves added to a level's budget before it starts — the "+3 moves" booster and the win
        /// streak. They do not count towards stars: a booster helps a player through a level, it
        /// does not buy a better result on it.
        /// </summary>
        public int ExtraMoves;

        /// <summary>Only in <see cref="GameMode.Level"/>.</summary>
        public LevelDefinition Level;
    }

    /// <summary>Everything that happened during a single successful placement.</summary>
    public sealed class MoveResult
    {
        public ClearResult Clear;
        public int PlacementScore;
        public int ClearScore;
        public int ComboStreak;
        public bool TrayRefilled;
        public bool GameOver;

        /// <summary>Charges earned by this move — the prism filling up.</summary>
        public int ChargesGained;

        /// <summary>Session state once the move settled.</summary>
        public SessionState State;

        /// <summary>Where shade crept to after this move, or (-1, -1) when it did not.</summary>
        public CellOffset ShadeSpread = new CellOffset(-1, -1);

        /// <summary>Blocks of the level's order colour this move cleared.</summary>
        public int OrderCleared;

        /// <summary>What was placed and where, so the view can animate exactly those cells.</summary>
        public PieceShape PlacedShape;
        public int PlacedColumn;
        public int PlacedRow;
        public int PlacedColorIndex;

        public int TotalScore => PlacementScore + ClearScore;
    }

    /// <summary>What a power did.</summary>
    public sealed class PowerResult
    {
        public PowerKind Kind;
        public int Slot = -1;
        public int Column;
        public int Row;

        /// <summary>Only for the bomb.</summary>
        public ClearResult Blast;

        public int ScoreGained;
        public SessionState State;
    }

    /// <summary>A block a level starts with.</summary>
    public readonly struct PrefillCell
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Color;
        public readonly bool Gem;
        public readonly int Ice;

        /// <summary>A glow tile under the cell. The only layer that may sit on an empty cell (<see cref="Color"/> <see cref="BoardModel.Empty"/>).</summary>
        public readonly bool Tile;

        /// <summary>Moves on a timer block's clock; 0 for an ordinary block.</summary>
        public readonly int Timer;

        public PrefillCell(int x, int y, int color, bool gem, int ice, bool tile = false, int timer = 0)
        {
            X = x;
            Y = y;
            Color = color;
            Gem = gem;
            Ice = ice;
            Tile = tile;
            Timer = timer;
        }

        public PrefillCell WithTile(bool tile) => new PrefillCell(X, Y, Color, Gem, Ice, tile, Timer);
    }

    /// <summary>A generated level: its opening board, goal and move budget.</summary>
    public sealed class LevelDefinition
    {
        public int Number;
        public int Seed;
        public GoalKind Goal;
        public int Target;
        public int MoveLimit;
        public int StartCharges = 1;
        public readonly List<PrefillCell> Prefill = new List<PrefillCell>();

        /// <summary>The colour a <see cref="GoalKind.Colors"/> level asks for; -1 otherwise.</summary>
        public int OrderColor = -1;

        /// <summary>
        /// Quiet moves after which shade creeps into a neighbouring free cell. 0: it never spreads.
        /// A move that clears any shade resets the count.
        /// </summary>
        public int ShadeSpread;

        /// <summary>0 an ordinary level, 1 hard, 2 very hard — the map marks them; the generator builds them tighter.</summary>
        public int Hardness;

        /// <summary>Stars for finishing with this many moves left over.</summary>
        public int StarsFor(int movesLeft)
        {
            if (MoveLimit <= 0) return 1;
            float spare = movesLeft / (float)MoveLimit;
            if (spare >= 0.30f) return 3;
            if (spare >= 0.12f) return 2;
            return 1;
        }
    }

    /// <summary>
    /// A saved run. Plain public fields and arrays only, so it serialises with anything —
    /// including Unity's JsonUtility — without Core having to know about Unity.
    /// </summary>
    [Serializable]
    public sealed class SessionSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        public int Mode;
        public int Seed;
        public int BoardSize;
        public int PaletteSize;
        public bool PowersEnabled;
        public int StartCharges;

        public int LevelNumber;
        public int LevelGoal;
        public int LevelTarget;
        public int LevelMoveLimit;
        public int LevelOrderColor = -1;
        public int LevelShadeSpread;
        public int LevelHardness;
        public int ExtraMoves;

        public string RngState;
        public float Drift;

        public int[] Cells;
        public bool[] Gems;
        public int[] Ice;

        /// <summary>Glow tiles and timer clocks. Missing from older saves, which had neither.</summary>
        public bool[] Tiles;
        public int[] Timers;

        public string[] TrayShapes;
        public int[] TrayColors;
        public bool[] TrayUsed;

        public int Score;
        public int ComboStreak;
        public int BestCombo;
        public int State;
        public int Charges;
        public int ChargeProgress;
        public int ChargesEarned;
        public int BonusMoves;
        public int MovesUsed;
        public int LinesCleared;
        public int GemsCollected;
        public int PlacedPieces;
        public int PerfectClears;
        public int MonoLines;
        public int PowersUsed;

        /// <summary>Seconds of play. Missing from older saves, which read as zero.</summary>
        public float PlaySeconds;

        public int TilesCollected;
        public int ColorCollected;
        public int ShadeCleared;
        public int ShadeQuiet;
        public int LossReason;
    }
}
