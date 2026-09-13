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

            // The app icon, as the editor will bake it.
            SavePng("00_icon_full", AppIconArt.Full(512));
            SavePng("00_icon_round", AppIconArt.Round(512));
            SavePng("00_icon_foreground", AppIconArt.Foreground(432));

            yield return Frames(40);
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
            yield return Wait(0.5f);
            yield return Shot("15_stats");
            _app.CloseModal();

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
            Log("done");
            Application.Quit();
        }

        /// <summary>
        /// A board with exactly one isolated hole in every row and column: nothing but a single
        /// block fits, and the tray holds no single block. Two charges left — the stuck state.
        /// </summary>
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
