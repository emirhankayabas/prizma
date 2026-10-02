#if PRIZMA_AUTOTEST
using System;
using System.Collections;
using System.IO;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Walks through every screen on its own and saves a screenshot of each, then quits. Only
    /// compiled into the Windows test build (see Editor/AutoTestBuild), never into the game.
    /// Board states come from the computer player and from hand-made saves, so the pictures show
    /// real positions: a run in progress, a jam with charges left, a finished level.
    /// </summary>
    public sealed class AutoTest : MonoBehaviour
    {
        /// <summary>Simulated camera cutout, in canvas units. Read before the first layout.</summary>
        public static float TopInsetArg =>
            float.TryParse(Arg("-autotestTopInset"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

        string _out;
        AppController _app;

        // Read through the app every time: a theme change rebuilds the interface, play page included.
        GameScreen _game => _app.Game;

        IEnumerator Start()
        {
            _out = Arg("-autotestOut") ?? Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(_out);
            Log($"writing to {_out}");

            _app = GetComponent<AppController>();

            // Stray clicks from the desktop must not change screens mid-sequence.
            GetComponent<PointerRouter>().enabled = false;

            // The launch splash goes first, before anything slow: baking the icons below blocks
            // the main thread for longer than the splash lasts, and a splash whose clock ran out
            // while the thread was busy is already fading by the time the shutter opens.
            yield return Frames(14);
            yield return Shot("00b_splash");

            while (BootSplash.Active) yield return null;

            // Two narrower runs: the adventure alone, or a prepared save left open to be played.
            string only = Arg("-autotestOnly");
            if (only == "adventure")
            {
                yield return Adventure();
                Log("done");
                Application.Quit();
                yield break;
            }

            if (only == "modals")
            {
                yield return ModalShots();
                Log("done");
                Application.Quit();
                yield break;
            }

            if (only == "map")
            {
                yield return MapShots();
                Log("done");
                Application.Quit();
                yield break;
            }

            if (only == "play")
            {
                PreparePlayableSave();
                GetComponent<PointerRouter>().enabled = true;
                _app.ShowLevelSelect();
                Log("ready to play");
                yield break;
            }

            // The app icon, as the editor will bake it.
            SavePng("00_icon_full", AppIconArt.Full(512));
            SavePng("00_icon_round", AppIconArt.Round(512));
            SavePng("00_icon_foreground", AppIconArt.Foreground(432));

            yield return Frames(24);
            yield return Shot("01_menu");

            // Classic, first run: the tutorial hand should be visible.
            _app.PlayClassic(fresh: true);
            yield return Wait(1.6f);
            yield return Shot("02_classic_start");

            var bot = new Autoplayer(7, 0.8f);
            for (int i = 0; i < 16; i++)
            {
                _game.AutoStep(bot);
                yield return Frames(4);
            }

            yield return Wait(0.8f);
            yield return Shot("03_classic_mid");

            _game.AutoPower(PowerKind.Rotate);
            yield return Wait(0.3f);
            yield return Shot("03b_rotate_aim");
            _game.AutoPower(PowerKind.Rotate);

            // The two moments a still picture used to miss: the preview under a held piece, and
            // the blocks on their way out. Both are timed against a board built to clear.
            _game.Begin(ClearReadySession());
            yield return Wait(0.6f);
            _game.AutoHover(0, 5, 7);
            yield return Frames(3);
            yield return Shot("03c_line_preview");

            _game.AutoPlace(0, 5, 7);
            yield return Frames(7);
            yield return Shot("03d_clearing");
            yield return Wait(1.2f);
            yield return Shot("03e_cleared");

            _app.OpenPause();
            yield return Wait(0.4f);
            yield return Shot("04_pause");

            _app.OpenSettings();
            yield return Wait(0.4f);
            yield return Shot("05_settings");
            _app.CloseModal();
            _app.CloseModal();

            int guard = 0;
            while (!_game.Session.IsFinished && guard++ < 1500)
            {
                if (!_game.AutoStep(bot)) break;
                if (guard % 8 == 0) yield return null;
            }

            yield return Wait(1.5f);
            yield return Shot("06_classic_over");

            _app.ShowMenu();
            yield return Wait(0.5f);
            yield return Shot("07_menu_after");

            _app.OpenScores();
            yield return Wait(0.5f);
            yield return Shot("07b_scores");
            _app.CloseModal();

            _app.ShowLevelSelect();
            yield return Wait(0.5f);
            yield return Shot("08_levels");

            _app.PlayLevel(1);
            yield return Wait(0.8f);
            yield return Shot("09_level1");

            var levelBot = new Autoplayer(3, 0.9f);
            guard = 0;
            while (!_game.Session.IsFinished && guard++ < 300)
            {
                if (!_game.AutoStep(levelBot)) break;
                yield return Frames(6);
            }

            yield return Wait(2f);
            yield return Shot("10_level1_result");

            for (int n = 1; n <= 11; n++) Progress.RecordLevel(n, 1 + n % 3);
            _app.PlayLevel(12);
            yield return Wait(0.8f);
            yield return Shot("11_level12");

            _game.Begin(OutOfMovesSession());
            yield return Wait(0.8f);
            yield return Shot("11b_out_of_moves");

            _app.PlayLevel(3);
            yield return Wait(0.8f);
            yield return Shot("12_level3_ice");

            // The computer used a power in the first classic run, which retires the hint for good;
            // bring it back so the jam shows what a first-time player sees.
            Progress.PowersHinted = false;
            _app.PlayClassic(fresh: true);
            yield return Frames(5);
            _game.Begin(StuckSession());
            yield return Wait(1.2f);
            yield return Shot("13_stuck");

            _game.AutoPower(PowerKind.Bomb);
            _game.AutoBombPreview(3, 3);
            yield return Wait(0.3f);
            yield return Shot("13b_bomb_aim");
            _game.AutoPower(PowerKind.Bomb);

            _app.PlayDaily(fresh: true);
            yield return Wait(0.8f);
            yield return Shot("14_daily");

            _app.ShowMenu();
            _app.OpenStats();
            _app.StatsPage.ShowTab(false);
            yield return Wait(0.5f);
            yield return Shot("15_stats");
            _app.StatsPage.ShowTab(true);
            yield return Wait(0.3f);
            yield return Shot("15b_achievements");
            _app.CloseModal();

            _app.OpenDaily();
            yield return Wait(0.5f);
            yield return Shot("14b_daily_card");
            _app.CloseModal();

            // Today's puzzle, played by a good bot until it is solved: the solve card, the streak,
            // then the daily sheet solved and its badges.
            var dailyBot = new Autoplayer(9, 0.85f);
            for (int attempt = 0; attempt < 6 && !Progress.SolvedDailyToday; attempt++)
            {
                _app.PlayDaily(fresh: true);
                guard = 0;
                while (!_game.Session.IsFinished && guard++ < 1500)
                {
                    if (!_game.AutoStep(dailyBot)) break;
                    if (guard % 8 == 0) yield return null;
                }
                yield return Wait(0.6f);
            }
            yield return Wait(0.9f);
            yield return Shot("14c_daily_solved");

            _app.ShowMenu();
            yield return Wait(0.4f);
            yield return Shot("14e_menu_after_solve");
            _app.OpenDaily();
            yield return Wait(1.2f);
            yield return Shot("14f_daily_card_solved");
            _app.DailyPage.ShowTab(true);
            yield return Wait(0.4f);
            yield return Shot("14g_daily_badges");
            _app.CloseModal();

            for (int n = 12; n <= 24; n++) Progress.RecordLevel(n, 2);
            _app.PlayLevel(25);
            yield return Wait(0.8f);
            yield return Shot("12b_level25_stone");

            // English: the same key screens after a language switch, which rebuilds in place.
            _app.ShowMenu();
            _app.SetLanguage(Language.English);
            yield return Wait(0.5f);
            yield return Shot("20_en_menu");
            _app.OpenSettings();
            yield return Wait(0.4f);
            yield return Shot("21_en_settings");
            _app.CloseModal();
            _app.OpenStats();
            _app.StatsPage.ShowTab(true);
            yield return Wait(0.4f);
            yield return Shot("22_en_achievements");
            _app.CloseModal();
            _app.OpenDaily();
            yield return Wait(0.4f);
            yield return Shot("23_en_daily");
            _app.DailyPage.ShowTab(true);
            yield return Wait(0.3f);
            yield return Shot("23b_en_badges");
            _app.CloseModal();
            _app.PlayLevel(25);
            yield return Wait(0.8f);
            yield return Shot("24_en_level");
            _app.ShowMenu();
            _app.SetLanguage(Language.Turkish);

            _app.OpenThemes();
            yield return Wait(0.5f);
            yield return Shot("16_themes");
            _app.CloseModal();

            // A theme not earned yet, previewed: 33 stars so far, Mücevher needs 45.
            _app.OpenThemes();
            _app.ThemesPage.AutoFocus(3);
            yield return Wait(0.4f);
            yield return Shot("18_theme_locked_preview");
            _app.CloseModal();

            // Every theme, on the three screens that show the most of it. Stars are granted so each
            // can be applied; SetTheme rebuilds the interface in place.
            for (int n = 12; n <= 40; n++) Progress.RecordLevel(n, 3);
            for (int t = 0; t < Themes.All.Length; t++)
            {
                _app.ShowMenu();
                _app.OpenThemes();
                _app.SetTheme(t);
                yield return Wait(0.5f);
                yield return Shot($"18_theme{t}_picker");

                _app.CloseModal();
                yield return Wait(0.3f);
                yield return Shot($"18_theme{t}_menu");

                _app.PlayClassic(fresh: true);
                var themeBot = new Autoplayer(21 + t, 0.8f);
                for (int i = 0; i < 14; i++)
                {
                    _game.AutoStep(themeBot);
                    yield return Frames(3);
                }

                yield return Wait(0.8f);
                yield return Shot($"18_theme{t}_game");
            }

            _app.ShowMenu();
            Progress.ColorBlind = true;
            _app.SetTheme(1);
            _app.PlayClassic(fresh: true);
            var cbBot = new Autoplayer(11, 0.8f);
            for (int i = 0; i < 12; i++)
            {
                _game.AutoStep(cbBot);
                yield return Frames(3);
            }

            yield return Wait(0.8f);
            yield return Shot("17_colorblind_pastel");

            Progress.ColorBlind = false;
            _app.SetTheme(0);

            // The run's light: a run well into the spectrum, the moment a stage changes, the turning
            // top stage, and the menu's title lit by the best stage reached.
            _game.Begin(ScoredSession(9000));
            yield return Wait(0.8f);
            yield return Shot("30_spectrum_stage3");

            _game.Begin(ScoredSession(12950));
            var stageBot = new Autoplayer(5, 0.85f);
            for (int i = 0; i < 80 && _game.Session.Score < 13000 && !_game.Session.IsFinished; i++)
            {
                _game.AutoStep(stageBot);
                yield return Frames(2);
            }
            yield return Wait(0.45f);
            yield return Shot("31_stage_up_wash");
            yield return Wait(1.8f);
            yield return Shot("31b_stage4_settled");

            _game.Begin(ScoredSession(46000));
            yield return Wait(1.2f);
            yield return Shot("32_spectrum_top");

            _app.ShowMenu();
            yield return Wait(1.8f);
            yield return Shot("33_menu_title_lit");
            Log("done");
            Application.Quit();
        }

        // ------------------------------------------------------------------ adventure

        /// <summary>
        /// The adventure from end to end: the road at the start, a level's sheet with boosters, a
        /// hard level, a win walked on along the road to a chest, the chest opened, every intro
        /// board, the hammer, a timer running out, the streak's gift, the road's end — and the map
        /// and the sheet again in English.
        /// </summary>
        IEnumerator Adventure()
        {
            _app.ShowLevelSelect();
            yield return Wait(0.8f);
            yield return Shot("40_map_start");

            _app.OpenLevelStart(1);
            yield return Wait(0.6f);
            yield return Shot("41_start_level1");
            _app.CloseModal();
            yield return Wait(0.3f);

            // Most of the first world done; the map on its ninth level.
            for (int n = 1; n <= 9; n++) Progress.RecordLevel(n, 1 + (n * 7) % 3);
            Progress.MapRevealed = 10;
            _app.ShowLevelSelect();
            yield return Wait(0.8f);
            yield return Shot("42_map_world1");

            _app.OpenLevelStart(10);
            yield return Wait(0.5f);
            _app.LevelStartPage.AutoToggle(Booster.Moves);
            _app.LevelStartPage.AutoToggle(Booster.Charge);
            yield return Wait(0.4f);
            yield return Shot("43_start_hard_boosters");

            // Level 10, played with the boosters until it is won, then the walk along the road.
            _app.PlayLevel(10, true, true);
            yield return Wait(0.8f);
            yield return Shot("44_level10_boosted");

            var bot = new Autoplayer(17, 0.95f);
            for (int attempt = 0; attempt < 4 && Progress.StarsFor(10) == 0; attempt++)
            {
                if (attempt > 0) _app.PlayLevel(10);
                int guard = 0;
                while (!_game.Session.IsFinished && guard++ < 400)
                {
                    if (!_game.AutoStep(bot)) break;
                    if (guard % 4 == 0) yield return null;
                }
                yield return Wait(0.6f);
            }

            if (Progress.StarsFor(10) == 0) Progress.RecordLevel(10, 2);
            yield return Wait(1.4f);
            yield return Shot("45_level10_result");

            _app.ShowLevelSelect(afterWin: 10);
            yield return Wait(0.9f);
            yield return Shot("46_map_walk");
            yield return Wait(1.8f);
            yield return Shot("47_map_chest_ready");

            _app.OpenChest(0);
            yield return Wait(0.7f);
            yield return Shot("48_chest");
            GetComponent<PointerRouter>().enabled = false;
            _app.CloseModal();
            Progress.OpenChest(0);
            yield return Wait(0.5f);

            // One more win: the walk from a level to the next opens that level's sheet on its own.
            Progress.MapRevealed = 11;
            Progress.RecordLevel(11, 3);
            Progress.RecordLevelResult(true);
            _app.ShowLevelSelect(afterWin: 11);
            yield return Wait(3f);
            yield return Shot("49_after_win_sheet");
            _app.CloseModal();

            // Every board that brings something new, and a few moves into each.
            int[] intros = { LevelGenerator.TilesFrom, LevelGenerator.StoneFrom, LevelGenerator.ShadeFrom,
                LevelGenerator.ColorsFrom, LevelGenerator.TimersFrom, LevelGenerator.DoubleIceFrom };
            foreach (int level in intros)
            {
                for (int n = 1; n < level; n++) if (Progress.StarsFor(n) == 0) Progress.RecordLevel(n, 2);
                Progress.MapRevealed = level;
                _app.OpenLevelStart(level);
                yield return Wait(0.5f);
                yield return Shot($"50_start_intro{level}");

                _app.PlayLevel(level);
                yield return Wait(0.8f);
                yield return Shot($"51_intro{level}");

                var introBot = new Autoplayer(level, 0.6f);
                for (int i = 0; i < 4 && !_game.Session.IsFinished; i++)
                {
                    _game.AutoStep(introBot);
                    yield return Frames(4);
                }
                yield return Wait(0.8f);
                yield return Shot($"52_intro{level}_moves");
            }

            // A generated board from each of the later worlds.
            foreach (int level in new[] { 34, 44, 57, 78, 95 })
            {
                for (int n = 1; n < level; n++) if (Progress.StarsFor(n) == 0) Progress.RecordLevel(n, 2);
                _app.PlayLevel(level);
                yield return Wait(0.8f);
                yield return Shot($"53_level{level}");
            }

            // The hammer, aimed at a stone and brought down.
            _app.PlayLevel(LevelGenerator.StoneFrom);
            yield return Wait(0.6f);
            _game.AutoPower(PowerKind.Hammer);
            _game.AutoHammerPreview(1, 7);
            yield return Wait(0.3f);
            yield return Shot("54_hammer_aim");
            _game.AutoHammer(1, 7);
            yield return Wait(0.5f);
            yield return Shot("55_hammer_done");

            // A timer about to run out, and the move that lets it.
            _game.Begin(TimerSession());
            yield return Wait(0.6f);
            yield return Shot("56_timer_last_move");
            var timerBot = new Autoplayer(3, 0f);
            _game.AutoStep(timerBot);
            yield return Wait(1.4f);
            yield return Shot("57_timer_burst");

            // The streak at its top step, on a hard level's sheet.
            for (int i = 0; i < 3; i++) Progress.RecordLevelResult(true);
            _app.ShowLevelSelect();
            _app.OpenLevelStart(46);
            yield return Wait(0.6f);
            yield return Shot("58_start_streak3");
            _app.CloseModal();

            // The road's end.
            for (int n = 1; n <= LevelGenerator.LevelCount; n++) if (Progress.StarsFor(n) == 0) Progress.RecordLevel(n, 3);
            Progress.MapRevealed = LevelGenerator.LevelCount;
            _app.ShowLevelSelect();
            yield return Wait(0.3f);
            _app.LevelsPage.AutoFocus(100);
            yield return Wait(1.2f);
            yield return Shot("59_map_end");
            _app.LevelsPage.AutoFocus(55);
            yield return Wait(1.2f);
            yield return Shot("59b_map_world6");

            _app.SetLanguage(Language.English);
            yield return Wait(0.6f);
            _app.LevelsPage.AutoFocus(22);
            yield return Wait(1f);
            yield return Shot("60_en_map");
            _app.OpenLevelStart(31);
            yield return Wait(0.6f);
            yield return Shot("61_en_start");
            _app.CloseModal();
            _app.SetLanguage(Language.Turkish);
        }

        /// <summary>Every sheet, caught mid-entrance and at rest — for working on their look and motion.</summary>
        IEnumerator ModalShots()
        {
            yield return Wait(0.5f);
            _app.OpenSettings();
            yield return Wait(0.14f);
            yield return Shot("80_settings_enter");
            yield return Wait(1f);
            yield return Shot("81_settings");
            _app.CloseModal();
            yield return Wait(0.4f);

            _app.OpenScores();
            yield return Wait(1f);
            yield return Shot("82_scores");
            _app.CloseModal();

            _app.OpenStats();
            _app.StatsPage.ShowTab(false);
            yield return Wait(1f);
            yield return Shot("83_stats");
            _app.StatsPage.ShowTab(true);
            yield return Wait(0.6f);
            yield return Shot("84_achievements");
            _app.CloseModal();

            _app.OpenThemes();
            yield return Wait(1f);
            yield return Shot("85_themes");
            _app.CloseModal();

            _app.OpenDaily();
            yield return Wait(1.2f);
            yield return Shot("86_daily");
            _app.CloseModal();

            _app.PlayClassic(fresh: true);
            yield return Wait(0.6f);
            _app.OpenPause();
            yield return Wait(0.2f);
            yield return Shot("87_pause_enter");
            yield return Wait(0.9f);
            yield return Shot("88_pause");
            _app.OpenSettings();
            yield return Wait(1f);
            yield return Shot("89_settings_over_pause");
            _app.CloseModal();
            _app.CloseModal();

            for (int n = 1; n <= 10; n++) Progress.RecordLevel(n, 2);
            Progress.MapRevealed = 11;
            _app.ShowLevelSelect();
            yield return Wait(0.5f);
            _app.OpenLevelStart(16);
            yield return Wait(1.1f);
            yield return Shot("90_level_start_hard");
            _app.CloseModal();
            _app.OpenChest(0);
            yield return Wait(1.1f);
            yield return Shot("91_chest");
            _app.CloseModal();

            _app.SetLanguage(Language.English);
            yield return Wait(0.4f);
            _app.ShowMenu();
            _app.OpenSettings();
            yield return Wait(1f);
            yield return Shot("92_en_settings");
            _app.CloseModal();
            _app.SetLanguage(Language.Turkish);
        }

        /// <summary>The map alone, at the moments that show the most of it — for working on its look.</summary>
        IEnumerator MapShots()
        {
            _app.ShowLevelSelect();
            yield return Wait(0.8f);
            yield return Shot("70_map_fresh");

            for (int n = 1; n <= 9; n++) Progress.RecordLevel(n, 1 + (n * 7) % 3);
            Progress.MapRevealed = 9;
            _app.ShowLevelSelect(afterWin: 9);
            yield return Wait(0.8f);
            yield return Shot("71_map_walk");
            yield return Wait(1.6f);
            yield return Shot("72_map_level10");
            _app.CloseModal();

            Progress.RecordLevel(10, 3);
            Progress.MapRevealed = 11;
            _app.ShowLevelSelect();
            yield return Wait(0.3f);
            _app.LevelsPage.AutoFocus(10);
            yield return Wait(1f);
            yield return Shot("73_map_chest");

            for (int n = 11; n <= 34; n++) Progress.RecordLevel(n, 1 + (n * 5) % 3);
            Progress.OpenChest(0);
            Progress.MapRevealed = 35;
            _app.ShowLevelSelect();
            yield return Wait(1f);
            yield return Shot("74_map_world4");
            _app.LevelsPage.AutoFocus(30);
            yield return Wait(1f);
            yield return Shot("75_map_world3_end");
            _app.LevelsPage.AutoFocus(47);
            yield return Wait(1f);
            yield return Shot("76_map_locked");
        }

        /// <summary>A timer block on one move, in a level whose tray cannot reach its row this turn.</summary>
        static GameSession TimerSession()
        {
            var def = new LevelDefinition { Number = LevelGenerator.TimersFrom + 3, Seed = 77, Goal = GoalKind.Gems, Target = 2, MoveLimit = 30, StartCharges = 1 };
            for (int x = 0; x < 5; x++) def.Prefill.Add(new PrefillCell(x, 7, 2, x == 1, 0, timer: x == 3 ? 1 : 0));
            for (int x = 2; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 3, 4, x == 4, 0, timer: x == 2 ? 6 : 0));
            return GameSession.NewLevelRun(def, 8, Design.PaletteSize);
        }

        /// <summary>
        /// A save to play from: the first worlds done with mixed stars, a chest waiting, boosters in
        /// stock — so the map, the sheet and a level are all there to be tried.
        /// </summary>
        static void PreparePlayableSave()
        {
            for (int n = 1; n <= 29; n++) Progress.RecordLevel(n, 1 + (n * 5) % 3);
            Progress.OpenChest(0);
            Progress.MapRevealed = 30;
            Progress.GrantStarterBoosters();
            Progress.RecordLevelResult(true);
            Progress.TutorialSeen = true;
        }

        /// <summary>
        /// A board with exactly one isolated hole in every row and column: nothing but a single
        /// block fits, and the tray holds no single block. Two charges left — the stuck state.
        /// </summary>
        /// <summary>
        /// A board one placement away from taking a row and a column at once: the bottom row three
        /// cells short at its right end, and a column standing on that gap one cell short of the
        /// bottom. Dropping the flat three-piece at (5, 7) completes both.
        /// </summary>
        static GameSession ClearReadySession()
        {
            var seed = new GameSession(new SessionConfig { Seed = 11, PaletteSize = Design.PaletteSize, StartCharges = 2 });
            var snap = seed.CreateSnapshot();
            int n = snap.BoardSize;

            for (int i = 0; i < snap.Cells.Length; i++) snap.Cells[i] = BoardModel.Empty;

            for (int x = 0; x < n - 3; x++) snap.Cells[(n - 1) * n + x] = x % Design.PaletteSize;
            for (int y = 0; y < n - 1; y++) snap.Cells[y * n + (n - 2)] = (y + 2) % Design.PaletteSize;

            // A little else on the board, so the picture reads as a run rather than a diagram.
            for (int x = 1; x < 4; x++) snap.Cells[(n - 4) * n + x] = (x + 3) % Design.PaletteSize;

            snap.TrayShapes = new[] { "XXX", "XX/XX", "X" };
            snap.TrayUsed = new[] { false, false, false };
            snap.State = (int)SessionState.Playing;
            snap.Charges = 2;
            return GameSession.Restore(snap);
        }

        /// <summary>A fresh classic run carrying a given score, to show the light that score has reached.</summary>
        static GameSession ScoredSession(int score)
        {
            var snap = GameSession.NewRandomRun(8, Design.PaletteSize).CreateSnapshot();
            snap.Score = score;
            return GameSession.Restore(snap);
        }

        static GameSession StuckSession()
        {
            var seed = new GameSession(new SessionConfig { Seed = 5, PaletteSize = Design.PaletteSize, StartCharges = 2 });
            var snap = seed.CreateSnapshot();
            int n = snap.BoardSize;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                snap.Cells[y * n + x] = (x * 3 + y) % Design.PaletteSize;

            for (int x = 0; x < n; x++)
                snap.Cells[(x * 3 % n) * n + x] = BoardModel.Empty;

            snap.TrayShapes = new[] { "XXX", "XX/XX", "XX/X." };
            snap.TrayUsed = new[] { false, false, false };
            snap.State = (int)SessionState.Stuck;
            snap.Charges = 2;
            return GameSession.Restore(snap);
        }

        void SavePng(string name, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), texture.EncodeToPNG());
            Destroy(texture);
        }

        /// <summary>Level 12 with its whole budget spent and two charges left: the "more moves" offer.</summary>
        static GameSession OutOfMovesSession()
        {
            var level = LevelGenerator.Generate(12, Design.PaletteSize);
            var snap = GameSession.NewLevelRun(level, 8, Design.PaletteSize).CreateSnapshot();
            snap.MovesUsed = snap.LevelMoveLimit;
            snap.Charges = PowerRules.ExtraMovesCost;
            snap.State = (int)SessionState.OutOfMoves;
            return GameSession.Restore(snap);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_out, name + ".png"));
            Log("shot " + name);
            yield return Frames(3);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        static void Log(string message) => Debug.Log("[AutoTest] " + message);

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
