using System;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Drives a run: owns the board, the three-piece tray, the score, the combo streak and the
    /// prism charges, and decides when the run is over. Fully deterministic for a given seed —
    /// the daily puzzle and the generated levels both rely on that — and savable at any point
    /// through <see cref="CreateSnapshot"/>.
    /// </summary>
    public sealed class GameSession
    {
        public const int TraySlots = 3;

        readonly Rng _random;
        readonly PieceDealer _dealer;

        public SessionConfig Config { get; }
        public BoardModel Board { get; }
        public TrayPiece[] Tray { get; }

        public int Seed => Config.Seed;
        public GameMode Mode => Config.Mode;
        public LevelDefinition Level => Config.Level;

        public int Score { get; private set; }
        public int ComboStreak { get; private set; }
        public int BestCombo { get; private set; }
        public SessionState State { get; private set; }

        /// <summary>Spendable prism charges.</summary>
        public int Charges { get; private set; }

        /// <summary>Lines banked towards the next charge, 0 .. <see cref="ChargeTarget"/>.</summary>
        public int ChargeProgress { get; private set; }

        /// <summary>Charges this run has earned by clearing lines. Each one makes the next dearer.</summary>
        public int ChargesEarned { get; private set; }

        /// <summary>Lines the prism needs for its next charge.</summary>
        public int ChargeTarget => PowerRules.LinesForCharge(ChargesEarned);

        public int MovesUsed { get; private set; }
        public int LinesCleared { get; private set; }
        public int GemsCollected { get; private set; }
        public int PlacedPieces { get; private set; }
        public int PerfectClears { get; private set; }
        public int MonoLines { get; private set; }
        public int PowersUsed { get; private set; }

        public bool IsGameOver => State == SessionState.Lost;
        public bool IsFinished => State == SessionState.Won || State == SessionState.Lost;

        /// <summary>
        /// Seconds of play on this run, kept by the view (Core has no clock) and saved with it.
        /// The daily puzzle is timed; the other modes simply carry the number along.
        /// </summary>
        public float PlaySeconds { get; set; }

        /// <summary>Moves bought when a level's budget ran out. Zero until then; at most one purchase.</summary>
        public int BonusMoves { get; private set; }

        public int MovesLeft => Level == null ? int.MaxValue : Math.Max(0, Level.MoveLimit + BonusMoves - MovesUsed);

        public bool CanBuyMoves => State == SessionState.OutOfMoves && BonusMoves == 0 && Charges >= PowerRules.ExtraMovesCost;

        /// <summary>How far the level goal has come. Zero outside the level mode.</summary>
        public int GoalProgress
        {
            get
            {
                if (Level == null) return 0;
                switch (Level.Goal)
                {
                    case GoalKind.Gems: return GemsCollected;
                    case GoalKind.Lines: return LinesCleared;
                    default: return Score;
                }
            }
        }

        public bool GoalMet => Level != null && GoalProgress >= Level.Target;

        /// <summary>Assist applied to the most recent deal, 0..1. Surfaced for tuning and tests.</summary>
        public float LastAssist => _dealer.LastAssist;

        public event Action<MoveResult> Moved;
        public event Action TrayRefilled;
        public event Action<PowerResult> PowerUsed;
        public event Action Stuck;
        public event Action OutOfMoves;
        public event Action GameOver;
        public event Action LevelWon;

        public GameSession(SessionConfig config) : this(config, deal: true) { }

        public GameSession(int seed, int boardSize = 8, int paletteSize = 6)
            : this(new SessionConfig { Seed = seed, BoardSize = boardSize, PaletteSize = paletteSize }) { }

        GameSession(SessionConfig config, bool deal)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.Mode == GameMode.Level && config.Level == null)
                throw new ArgumentException("Level mode needs a level definition.", nameof(config));

            config.PaletteSize = Math.Max(1, config.PaletteSize);

            _random = new Rng(config.Seed);
            _dealer = new PieceDealer(_random);

            Board = new BoardModel(config.BoardSize);
            Tray = new TrayPiece[TraySlots];
            for (int i = 0; i < TraySlots; i++)
                Tray[i] = new TrayPiece();

            if (!deal) return;

            if (config.Level != null)
            {
                foreach (var cell in config.Level.Prefill)
                    if (Board.InBounds(cell.X, cell.Y))
                        Board.SetPrefill(cell.X, cell.Y, cell.Color % config.PaletteSize, cell.Gem, cell.Ice);
            }

            Charges = config.PowersEnabled ? Math.Min(PowerRules.MaxCharges, Math.Max(0, config.StartCharges)) : 0;

            FillTray();
            State = SessionState.Playing;
        }

        public static GameSession NewRandomRun(int boardSize = 8, int paletteSize = 6)
            => new GameSession(new SessionConfig
            {
                Mode = GameMode.Classic,
                Seed = Environment.TickCount,
                BoardSize = boardSize,
                PaletteSize = paletteSize
            });

        /// <summary>
        /// The day's puzzle: a generated board with a goal and a move budget, the same for
        /// everyone on that calendar day (<see cref="LevelGenerator.GenerateDaily"/>). It plays
        /// by the level rules; only the mode differs, so the game records it as the daily.
        /// </summary>
        public static GameSession NewDailyRun(LevelDefinition puzzle, int boardSize = 8, int paletteSize = 6)
            => new GameSession(new SessionConfig
            {
                Mode = GameMode.Daily,
                Seed = puzzle.Seed,
                BoardSize = boardSize,
                PaletteSize = paletteSize,
                StartCharges = puzzle.StartCharges,
                Level = puzzle
            });

        public static GameSession NewLevelRun(LevelDefinition level, int boardSize = 8, int paletteSize = 6)
            => new GameSession(new SessionConfig
            {
                Mode = GameMode.Level,
                Seed = level.Seed,
                BoardSize = boardSize,
                PaletteSize = paletteSize,
                StartCharges = level.StartCharges,
                Level = level
            });

        // ------------------------------------------------------------------ placing

        public bool CanPlace(int trayIndex, int col, int row)
        {
            if (State != SessionState.Playing) return false;
            var piece = GetPlayablePiece(trayIndex);
            return piece != null && Board.CanPlace(piece.Shape, col, row);
        }

        /// <summary>
        /// Places a tray piece. Returns null when the move is illegal, so callers can treat a
        /// rejected drop as "snap the piece back" without exception handling.
        /// </summary>
        public MoveResult TryPlace(int trayIndex, int col, int row)
        {
            if (State != SessionState.Playing) return null;
            var piece = GetPlayablePiece(trayIndex);
            if (piece == null) return null;
            if (!Board.CanPlace(piece.Shape, col, row)) return null;

            var clear = Board.Place(piece.Shape, col, row, piece.ColorIndex);
            piece.Used = true;
            MovesUsed++;
            PlacedPieces++;

            // The streak advances only while consecutive placements keep clearing lines.
            ComboStreak = clear.Any ? ComboStreak + 1 : 0;
            if (ComboStreak > BestCombo) BestCombo = ComboStreak;

            var result = new MoveResult
            {
                Clear = clear,
                PlacementScore = ScoreRules.PlacementScore(piece.Shape),
                ClearScore = ScoreRules.ClearScore(clear, ComboStreak),
                ComboStreak = ComboStreak,
                PlacedShape = piece.Shape,
                PlacedColumn = col,
                PlacedRow = row,
                PlacedColorIndex = piece.ColorIndex
            };

            Score += result.TotalScore;
            LinesCleared += clear.LinesCleared;
            GemsCollected += clear.CollectedGems.Count;
            MonoLines += clear.MonoLines;
            if (clear.PerfectClear) PerfectClears++;

            result.ChargesGained = BankLines(clear.LinesCleared + clear.MonoLines, clear.PerfectClear);

            if (IsTrayEmpty())
            {
                FillTray();
                result.TrayRefilled = true;
            }

            var before = State;
            Evaluate();
            result.State = State;
            result.GameOver = State == SessionState.Lost;

            Moved?.Invoke(result);

            if (result.TrayRefilled)
                TrayRefilled?.Invoke();

            RaiseStateChange(before);
            return result;
        }

        /// <summary>True while at least one unused tray piece still fits somewhere.</summary>
        public bool HasAnyLegalMove()
        {
            for (int i = 0; i < Tray.Length; i++)
            {
                var piece = Tray[i];
                if (!piece.Used && Board.HasAnyPlacement(piece.Shape))
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ powers

        public bool CanUsePower(PowerKind kind)
            => Config.PowersEnabled
               && (State == SessionState.Playing || State == SessionState.Stuck)
               && Charges >= PowerRules.Cost(kind);

        /// <summary>What a slot's piece would become if rotated, or null when rotating changes nothing.</summary>
        public PieceShape RotatedShape(int slot)
        {
            var piece = GetPlayablePiece(slot);
            if (piece == null) return null;

            var rotated = PieceLibrary.Canonical(piece.Shape.RotatedClockwise());
            return rotated.SameCells(piece.Shape) ? null : rotated;
        }

        public bool CanRotate(int slot) => CanUsePower(PowerKind.Rotate) && RotatedShape(slot) != null;

        public PowerResult TryRotate(int slot)
        {
            if (!CanRotate(slot)) return null;

            Tray[slot].Shape = RotatedShape(slot);
            return FinishPower(new PowerResult { Kind = PowerKind.Rotate, Slot = slot });
        }

        public bool CanReroll() => CanUsePower(PowerKind.Reroll) && !IsTrayEmpty();

        /// <summary>Deals fresh pieces into every slot that still holds one.</summary>
        public PowerResult TryReroll()
        {
            if (!CanReroll()) return null;

            _dealer.Deal(Board, Tray, Config.PaletteSize, Score, onlyUnused: true);
            return FinishPower(new PowerResult { Kind = PowerKind.Reroll });
        }

        /// <summary>A bomb only goes off where it would actually remove something.</summary>
        public bool CanBomb(int col, int row)
        {
            if (!CanUsePower(PowerKind.Bomb) || !Board.InBounds(col, row)) return false;

            int r = PowerRules.BombRadius;
            for (int y = row - r; y <= row + r; y++)
            for (int x = col - r; x <= col + r; x++)
                if (Board.InBounds(x, y) && Board.IsOccupied(x, y))
                    return true;

            return false;
        }

        public PowerResult TryBomb(int col, int row)
        {
            if (!CanBomb(col, row)) return null;

            var blast = Board.Blast(col, row, PowerRules.BombRadius);
            int gained = ScoreRules.BlastScore(blast);

            Score += gained;
            GemsCollected += blast.CollectedGems.Count;
            if (blast.PerfectClear) PerfectClears++;

            return FinishPower(new PowerResult
            {
                Kind = PowerKind.Bomb,
                Column = col,
                Row = row,
                Blast = blast,
                ScoreGained = gained
            });
        }

        /// <summary>Gives up from a rescuable state rather than spending the remaining charges.</summary>
        public void Concede()
        {
            if (State != SessionState.Stuck && State != SessionState.OutOfMoves) return;

            State = SessionState.Lost;
            GameOver?.Invoke();
        }

        /// <summary>Spends charges on <see cref="PowerRules.ExtraMoves"/> more moves. Returns false when not on offer.</summary>
        public bool TryBuyMoves()
        {
            if (!CanBuyMoves) return false;

            Charges -= PowerRules.ExtraMovesCost;
            BonusMoves += PowerRules.ExtraMoves;
            PowersUsed++;

            var before = State;
            Evaluate();
            RaiseStateChange(before);
            return true;
        }

        PowerResult FinishPower(PowerResult result)
        {
            Charges -= PowerRules.Cost(result.Kind);
            PowersUsed++;

            var before = State;
            Evaluate();
            result.State = State;

            PowerUsed?.Invoke(result);
            RaiseStateChange(before);
            return result;
        }

        /// <summary>Adds cleared lines to the prism and returns how many charges that earned.</summary>
        int BankLines(int lines, bool perfectClear)
        {
            if (!Config.PowersEnabled) return 0;

            int gained = 0;

            // A perfect clear fills the prism outright — and does not raise the price of the next.
            if (perfectClear && Charges < PowerRules.MaxCharges)
            {
                Charges++;
                gained++;
            }

            ChargeProgress += lines;
            while (ChargeProgress >= ChargeTarget && Charges < PowerRules.MaxCharges)
            {
                ChargeProgress -= ChargeTarget;
                ChargesEarned++;
                Charges++;
                gained++;
            }

            // With the prism full, hold the progress one short so spending a charge does not
            // immediately hand it back.
            if (Charges >= PowerRules.MaxCharges && ChargeProgress >= ChargeTarget)
                ChargeProgress = ChargeTarget - 1;

            return gained;
        }

        // ------------------------------------------------------------------ state

        void Evaluate()
        {
            if (Level != null && GoalMet)
            {
                State = SessionState.Won;
                return;
            }

            if (Level != null && MovesLeft <= 0)
            {
                State = Config.PowersEnabled && BonusMoves == 0 && Charges >= PowerRules.ExtraMovesCost
                    ? SessionState.OutOfMoves
                    : SessionState.Lost;
                return;
            }

            if (HasAnyLegalMove())
            {
                State = SessionState.Playing;
                return;
            }

            State = Config.PowersEnabled && Charges > 0 ? SessionState.Stuck : SessionState.Lost;
        }

        void RaiseStateChange(SessionState before)
        {
            if (State == before) return;

            switch (State)
            {
                case SessionState.Stuck: Stuck?.Invoke(); break;
                case SessionState.OutOfMoves: OutOfMoves?.Invoke(); break;
                case SessionState.Lost: GameOver?.Invoke(); break;
                case SessionState.Won: LevelWon?.Invoke(); break;
            }
        }

        /// <summary>Stars for a won level. Finishing on bought moves is always one star.</summary>
        public int StarsEarned
        {
            get
            {
                if (State != SessionState.Won || Level == null) return 0;
                return BonusMoves > 0 ? 1 : Level.StarsFor(MovesLeft);
            }
        }

        TrayPiece GetPlayablePiece(int trayIndex)
        {
            if (trayIndex < 0 || trayIndex >= Tray.Length) return null;
            var piece = Tray[trayIndex];
            return piece.Used ? null : piece;
        }

        bool IsTrayEmpty()
        {
            for (int i = 0; i < Tray.Length; i++)
                if (!Tray[i].Used) return false;
            return true;
        }

        void FillTray() => _dealer.Deal(Board, Tray, Config.PaletteSize, Score);

        // ------------------------------------------------------------------ save / restore

        public SessionSnapshot CreateSnapshot()
        {
            int n = Board.Size;
            var s = new SessionSnapshot
            {
                Mode = (int)Config.Mode,
                Seed = Config.Seed,
                BoardSize = n,
                PaletteSize = Config.PaletteSize,
                PowersEnabled = Config.PowersEnabled,
                StartCharges = Config.StartCharges,

                RngState = _random.SaveState(),
                Drift = _dealer.Drift,

                Cells = new int[n * n],
                Gems = new bool[n * n],
                Ice = new int[n * n],

                TrayShapes = new string[Tray.Length],
                TrayColors = new int[Tray.Length],
                TrayUsed = new bool[Tray.Length],

                Score = Score,
                ComboStreak = ComboStreak,
                BestCombo = BestCombo,
                State = (int)State,
                Charges = Charges,
                ChargeProgress = ChargeProgress,
                ChargesEarned = ChargesEarned,
                BonusMoves = BonusMoves,
                MovesUsed = MovesUsed,
                LinesCleared = LinesCleared,
                GemsCollected = GemsCollected,
                PlacedPieces = PlacedPieces,
                PerfectClears = PerfectClears,
                MonoLines = MonoLines,
                PowersUsed = PowersUsed,
                PlaySeconds = PlaySeconds
            };

            if (Level != null)
            {
                s.LevelNumber = Level.Number;
                s.LevelGoal = (int)Level.Goal;
                s.LevelTarget = Level.Target;
                s.LevelMoveLimit = Level.MoveLimit;
            }

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                s.Cells[i] = Board.GetCell(x, y);
                s.Gems[i] = Board.HasGem(x, y);
                s.Ice[i] = Board.IceAt(x, y);
            }

            for (int i = 0; i < Tray.Length; i++)
            {
                s.TrayShapes[i] = Tray[i].Shape?.ToCompact() ?? "X";
                s.TrayColors[i] = Tray[i].ColorIndex;
                s.TrayUsed[i] = Tray[i].Used;
            }

            return s;
        }

        /// <summary>
        /// Rebuilds a run from a snapshot. Throws on anything malformed — a save that cannot be
        /// trusted should be discarded, not half-loaded.
        /// </summary>
        public static GameSession Restore(SessionSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Version != SessionSnapshot.CurrentVersion) throw new FormatException("Unsupported save version.");

            int n = s.BoardSize;
            if (n < 2 || s.Cells == null || s.Cells.Length != n * n ||
                s.Gems == null || s.Gems.Length != n * n || s.Ice == null || s.Ice.Length != n * n)
                throw new FormatException("Board data does not match the board size.");

            if (s.TrayShapes == null || s.TrayShapes.Length != TraySlots ||
                s.TrayColors == null || s.TrayColors.Length != TraySlots ||
                s.TrayUsed == null || s.TrayUsed.Length != TraySlots)
                throw new FormatException("Tray data is malformed.");

            var mode = (GameMode)s.Mode;
            LevelDefinition level = null;
            // A level always has a budget; so does the daily puzzle. A daily saved before the
            // daily became a puzzle has none, and comes back without a level for the caller to drop.
            if (mode == GameMode.Level || (mode == GameMode.Daily && s.LevelMoveLimit > 0))
            {
                level = new LevelDefinition
                {
                    Number = s.LevelNumber,
                    Seed = s.Seed,
                    Goal = (GoalKind)s.LevelGoal,
                    Target = s.LevelTarget,
                    MoveLimit = s.LevelMoveLimit,
                    StartCharges = s.StartCharges
                };
            }

            var config = new SessionConfig
            {
                Mode = mode,
                Seed = s.Seed,
                BoardSize = n,
                PaletteSize = Math.Max(1, s.PaletteSize),
                PowersEnabled = s.PowersEnabled,
                StartCharges = s.StartCharges,
                Level = level
            };

            var session = new GameSession(config, deal: false);
            session._random.LoadState(s.RngState);
            session._dealer.Drift = s.Drift;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (s.Cells[i] == BoardModel.Empty) continue;
                session.Board.SetPrefill(x, y, s.Cells[i], s.Gems[i], s.Ice[i]);
            }

            for (int i = 0; i < TraySlots; i++)
            {
                session.Tray[i].Shape = PieceLibrary.Canonical(PieceShape.FromCompact("saved", s.TrayShapes[i]));
                session.Tray[i].ColorIndex = s.TrayColors[i];
                session.Tray[i].Used = s.TrayUsed[i];
            }

            session.Score = s.Score;
            session.ComboStreak = s.ComboStreak;
            session.BestCombo = s.BestCombo;
            session.State = (SessionState)s.State;
            session.Charges = s.Charges;
            session.ChargeProgress = s.ChargeProgress;
            session.ChargesEarned = s.ChargesEarned;
            session.BonusMoves = s.BonusMoves;
            session.MovesUsed = s.MovesUsed;
            session.LinesCleared = s.LinesCleared;
            session.GemsCollected = s.GemsCollected;
            session.PlacedPieces = s.PlacedPieces;
            session.PerfectClears = s.PerfectClears;
            session.MonoLines = s.MonoLines;
            session.PowersUsed = s.PowersUsed;
            session.PlaySeconds = Math.Max(0f, s.PlaySeconds);

            return session;
        }
    }
}
