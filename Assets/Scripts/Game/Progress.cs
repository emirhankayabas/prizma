using System;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Everything the player has earned and chosen, kept in PlayerPrefs as one JSON document:
    /// level stars, the daily streak, lifetime statistics, the block theme and the accessibility
    /// options. Saved on every change, so a force-quit never costs progress.
    /// </summary>
    public static class Progress
    {
        const string Key = "prizma.progress.v1";

        [Serializable]
        sealed class Data
        {
            public List<int> Stars = new List<int>();

            // The last day whose puzzle was solved, and the run of consecutive days up to it.
            public string DailyLastDate = "";
            public int DailyStreak;
            public int DailyBestStreak;
            public int DailyGames;

            // The puzzle being attempted: which day, how many tries, the time spent on the ones
            // that ended.
            public string DailyAttemptDate = "";
            public int DailyAttemptCount;
            public float DailyAttemptSeconds;

            // The most recent solve, as the daily card shows it.
            public string DailySolveDate = "";
            public float DailySolveSeconds;
            public int DailySolveMoves;
            public int DailySolveStars;
            public int DailySolveAttempts;
            public int DailySolveMinute = -1;

            // What the badges are counted from.
            public int DailySolves;
            public int DailyFirstTry;
            public int DailyThreeStars;
            public float DailyFastest;
            public int DailyPerfectWeeks;
            public int DailySundays;
            public int DailyEarly;
            public int DailyLate;
            public int DailyBadgesSeen;
            public int DailyStreakShown;

            public int Games;
            public long TotalScore;
            public int TotalLines;
            public int TotalPieces;
            public int BestCombo;
            public int PerfectClears;
            public int MonoLines;
            public int PowersUsed;
            public int GemsCollected;

            // The daily calendar: one entry per month played, yyyymm, with a bit per day.
            // Parallel lists because JsonUtility cannot store a dictionary.
            public List<int> DailyMonths = new List<int>();
            public List<int> DailyDays = new List<int>();

            public int AchievementStarsSeen;

            // The highest stage of light a classic run has reached. Lights the title's letters.
            public int BestSpectrum;

            // The adventure: boosters in stock, the win streak, world chests opened (a bit each), and
            // the furthest level the map has already shown unlocked — past it, the road is drawn on.
            public bool BoostersGranted;
            public int BoosterMoves;
            public int BoosterCharge;
            public int BoosterHammer;
            public int WinStreak;
            public int ChestsOpened;
            public int MapRevealed;

            public int Theme;
            public bool ColorBlind;
            public bool TutorialSeen;
            public bool PowersHinted;
        }

        static Data _data;

        public static event Action Changed;

        static Data D
        {
            get
            {
                if (_data != null) return _data;

                string json = PlayerPrefs.GetString(Key, null);
                try
                {
                    _data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json) ?? new Data();
                }
                catch (Exception)
                {
                    // A corrupt document is not worth crashing over.
                    _data = new Data();
                }

                if (_data.Stars == null) _data.Stars = new List<int>();
                if (_data.DailyMonths == null || _data.DailyDays == null || _data.DailyMonths.Count != _data.DailyDays.Count)
                {
                    _data.DailyMonths = new List<int>();
                    _data.DailyDays = new List<int>();
                }
                return _data;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ levels

        /// <summary>Stars earned on a level, 0 when not yet completed.</summary>
        public static int StarsFor(int level)
        {
            int i = level - 1;
            return i >= 0 && i < D.Stars.Count ? D.Stars[i] : 0;
        }

        /// <summary>Stars won on levels alone.</summary>
        public static int LevelStars
        {
            get
            {
                int total = 0;
                foreach (int s in D.Stars) total += s;
                return total;
            }
        }

        /// <summary>
        /// Every star the player holds — levels plus achievement tiers. This is what themes are
        /// unlocked with, so a player who never touches the adventure can still earn them.
        /// </summary>
        public static int TotalStars => LevelStars + Achievements.EarnedStars + DailyBadges.EarnedStars;

        /// <summary>Achievement stars the player has not looked at yet: the stats shortcut shows a dot.</summary>
        public static bool HasUnseenAchievements => Achievements.EarnedStars > D.AchievementStarsSeen;

        public static void MarkAchievementsSeen()
        {
            int earned = Achievements.EarnedStars;
            if (D.AchievementStarsSeen == earned) return;
            D.AchievementStarsSeen = earned;
            Save();
        }

        public static int LevelsCompleted => D.Stars.Count;

        /// <summary>The furthest level the player may start. Levels unlock one at a time, up to the last.</summary>
        public static int UnlockedLevel => Math.Min(LevelGenerator.LevelCount, D.Stars.Count + 1);

        /// <summary>Every level of the adventure is done.</summary>
        public static bool AdventureComplete => D.Stars.Count >= LevelGenerator.LevelCount;

        /// <summary>Records a finished level. Returns true when it beat the previous stars.</summary>
        public static bool RecordLevel(int level, int stars)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            int i = level - 1;
            if (i < 0 || i > D.Stars.Count || i >= LevelGenerator.LevelCount) return false;

            bool improved;
            if (i == D.Stars.Count)
            {
                D.Stars.Add(stars);
                improved = true;
            }
            else
            {
                improved = stars > D.Stars[i];
                if (improved) D.Stars[i] = stars;
            }

            Save();
            return improved;
        }

        // ------------------------------------------------------------------ adventure extras

        /// <summary>Stars of every level in a world, and how many it could hold.</summary>
        public static int WorldStars(int world)
        {
            int total = 0;
            for (int n = world * LevelGenerator.WorldSize + 1; n <= (world + 1) * LevelGenerator.WorldSize; n++)
                total += StarsFor(n);
            return total;
        }

        /// <summary>A world's chest can be opened once its last level is done.</summary>
        public static bool ChestReady(int world) =>
            !ChestOpened(world) && LevelsCompleted >= (world + 1) * LevelGenerator.WorldSize;

        public static bool ChestOpened(int world) => (D.ChestsOpened & (1 << world)) != 0;

        /// <summary>
        /// What a world's chest holds: a booster of each kind, and a second hammer from the fourth
        /// world on, where stone and shade make it worth more. The last world's chest is the big one.
        /// </summary>
        public static (int moves, int charge, int hammer) ChestContents(int world)
        {
            if (world >= LevelGenerator.WorldCount - 1) return (3, 3, 3);
            return (1, 1, world >= 3 ? 2 : 1);
        }

        public static void OpenChest(int world)
        {
            if (!ChestReady(world)) return;
            var (moves, charge, hammer) = ChestContents(world);
            D.ChestsOpened |= 1 << world;
            D.BoosterMoves += moves;
            D.BoosterCharge += charge;
            D.BoosterHammer += hammer;
            Save();
        }

        /// <summary>A small starting stock, handed over the first time the adventure is opened.</summary>
        public static void GrantStarterBoosters()
        {
            if (D.BoostersGranted) return;
            D.BoostersGranted = true;
            D.BoosterMoves += 2;
            D.BoosterCharge += 2;
            D.BoosterHammer += 3;
            Save();
        }

        public static int BoosterCount(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: return D.BoosterMoves;
                case Booster.Charge: return D.BoosterCharge;
                default: return D.BoosterHammer;
            }
        }

        /// <summary>Takes one booster from the stock. False when there is none.</summary>
        public static bool SpendBooster(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: if (D.BoosterMoves <= 0) return false; D.BoosterMoves--; break;
                case Booster.Charge: if (D.BoosterCharge <= 0) return false; D.BoosterCharge--; break;
                default: if (D.BoosterHammer <= 0) return false; D.BoosterHammer--; break;
            }

            Save();
            return true;
        }

        /// <summary>
        /// Levels won in a row, capped at three steps. Each step starts the next level with a
        /// little more (<see cref="StreakBonus"/>); any loss sets it back to nothing.
        /// </summary>
        public static int WinStreak => D.WinStreak;

        public const int MaxWinStreak = 3;

        public static void RecordLevelResult(bool won)
        {
            int streak = won ? Math.Min(MaxWinStreak, D.WinStreak + 1) : 0;
            if (streak == D.WinStreak) return;
            D.WinStreak = streak;
            Save();
        }

        /// <summary>What a streak step adds to the start of a level: moves, then a charge, then both.</summary>
        public static (int moves, int charges) StreakBonus(int streak)
        {
            switch (Math.Min(streak, MaxWinStreak))
            {
                case 1: return (2, 0);
                case 2: return (2, 1);
                case 3: return (4, 1);
                default: return (0, 0);
            }
        }

        /// <summary>The furthest unlocked level the map has already shown. The map draws the road on to anything past it.</summary>
        public static int MapRevealed
        {
            get => D.MapRevealed;
            set
            {
                if (D.MapRevealed == value) return;
                D.MapRevealed = value;
                Save();
            }
        }

        // ------------------------------------------------------------------ daily

        // The daily puzzle is solved once a day, like a newspaper puzzle: the streak counts days
        // solved in a row, and there is no going back — a missed day stays missed. Attempts and
        // time are tracked per puzzle so a solve reports how long it really took, retries included.

        public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");
        public static string TodayKey => DateKey(DateTime.Now);
        static string YesterdayKey => DateKey(DateTime.Now.AddDays(-1));

        /// <summary>The streak as it stands today: alive while the last solve was today or yesterday.</summary>
        public static int DailyStreak
        {
            get
            {
                string last = D.DailyLastDate;
                return last == TodayKey || last == YesterdayKey ? D.DailyStreak : 0;
            }
        }

        public static int DailyBestStreak => D.DailyBestStreak;
        public static bool SolvedDailyToday => D.DailyLastDate == TodayKey;

        public static int DailySolves => D.DailySolves;
        public static int DailyFirstTry => D.DailyFirstTry;
        public static int DailyThreeStars => D.DailyThreeStars;
        public static int DailyPerfectWeeks => D.DailyPerfectWeeks;
        public static int DailySundays => D.DailySundays;
        public static int DailyEarly => D.DailyEarly;
        public static int DailyLate => D.DailyLate;

        /// <summary>Fastest solve in seconds, 0 before the first.</summary>
        public static float DailyFastest => D.DailyFastest;

        /// <summary>The streak the daily card last showed, so it can count up to a new one exactly once.</summary>
        public static int DailyStreakShown
        {
            get => D.DailyStreakShown;
            set
            {
                if (D.DailyStreakShown == value) return;
                D.DailyStreakShown = value;
                Save();
            }
        }

        /// <summary>Minutes past midnight of the last solve, or -1. The reminder comes back at this time.</summary>
        public static int DailySolveMinute => D.DailySolveMinute;

        /// <summary>Attempts started on a day's puzzle.</summary>
        public static int DailyAttempts(DateTime date) => D.DailyAttemptDate == DateKey(date) ? D.DailyAttemptCount : 0;

        /// <summary>Seconds spent on a day's puzzle in attempts that already ended.</summary>
        public static float DailyBankedSeconds(DateTime date) => D.DailyAttemptDate == DateKey(date) ? D.DailyAttemptSeconds : 0f;

        /// <summary>A fresh attempt at a day's puzzle begins.</summary>
        public static void DailyAttemptStarted(DateTime date)
        {
            string key = DateKey(date);
            if (D.DailyAttemptDate != key)
            {
                D.DailyAttemptDate = key;
                D.DailyAttemptCount = 0;
                D.DailyAttemptSeconds = 0f;
            }

            D.DailyAttemptCount++;
            D.DailyGames++;
            Save();
        }

        /// <summary>An attempt ended without the solve — lost, or thrown away with a restart. Its time still counts.</summary>
        public static void DailyAttemptEnded(DateTime date, float seconds)
        {
            if (D.DailyAttemptDate != DateKey(date)) return;
            D.DailyAttemptSeconds += Mathf.Max(0f, seconds);
            Save();
        }

        /// <summary>What today's solve looked like, for the daily card. Null until today is solved.</summary>
        public static DailySolve TodaySolve => SolvedDailyToday && D.DailySolveDate == TodayKey
            ? new DailySolve(D.DailySolveSeconds, D.DailySolveMoves, D.DailySolveStars, D.DailySolveAttempts)
            : null;

        public sealed class DailySolve
        {
            public readonly float Seconds;
            public readonly int Moves;
            public readonly int Stars;
            public readonly int Attempts;

            public DailySolve(float seconds, int moves, int stars, int attempts)
            {
                Seconds = seconds;
                Moves = moves;
                Stars = stars;
                Attempts = attempts;
            }
        }

        /// <summary>
        /// Records the solve of <paramref name="date"/>'s puzzle. The streak grows when the last
        /// solve was the day before; anything older starts it again at one. A puzzle solved after
        /// midnight still counts for its own day — it was that day's board — and today stays open.
        /// Returns the streak after the solve.
        /// </summary>
        public static int RecordDailySolve(DateTime date, float seconds, int moves, int stars, DateTime solvedAt)
        {
            string key = DateKey(date);
            if (PlayedDaily(date)) return DailyStreak;

            string dayBefore = DateKey(date.AddDays(-1));
            D.DailyStreak = D.DailyLastDate == dayBefore ? D.DailyStreak + 1 : 1;
            D.DailyLastDate = key;
            if (D.DailyStreak > D.DailyBestStreak) D.DailyBestStreak = D.DailyStreak;

            int attempts = Math.Max(1, DailyAttempts(date));
            D.DailySolveDate = key;
            D.DailySolveSeconds = seconds;
            D.DailySolveMoves = moves;
            D.DailySolveStars = stars;
            D.DailySolveAttempts = attempts;
            D.DailySolveMinute = solvedAt.Hour * 60 + solvedAt.Minute;

            D.DailySolves++;
            if (attempts == 1) D.DailyFirstTry++;
            if (stars >= 3) D.DailyThreeStars++;
            if (D.DailyFastest <= 0f || seconds < D.DailyFastest) D.DailyFastest = seconds;
            if (date.DayOfWeek == DayOfWeek.Sunday) D.DailySundays++;
            if (solvedAt.Hour < 8) D.DailyEarly++;
            if (solvedAt.Hour >= 23 || solvedAt.Hour < 4) D.DailyLate++;

            MarkPlayed(date);

            // A week counts once, on the solve that completes it — each day can only be solved once.
            var monday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            bool fullWeek = true;
            for (int i = 0; i < 7 && fullWeek; i++) fullWeek = PlayedDaily(monday.AddDays(i));
            if (fullWeek) D.DailyPerfectWeeks++;

            Save();
            return D.DailyStreak;
        }

        static int MonthKey(int year, int month) => year * 100 + month;

        static void MarkPlayed(DateTime date)
        {
            int key = MonthKey(date.Year, date.Month);
            int i = D.DailyMonths.IndexOf(key);
            if (i < 0)
            {
                D.DailyMonths.Add(key);
                D.DailyDays.Add(0);
                i = D.DailyMonths.Count - 1;
            }

            D.DailyDays[i] |= 1 << (date.Day - 1);
        }

        /// <summary>The day's puzzle was solved (or, in saves from before the puzzle, played).</summary>
        public static bool PlayedDaily(DateTime date)
        {
            int i = D.DailyMonths.IndexOf(MonthKey(date.Year, date.Month));
            return i >= 0 && (D.DailyDays[i] & (1 << (date.Day - 1))) != 0;
        }

        /// <summary>Months in which every day's puzzle was solved.</summary>
        public static int CompletedMonths
        {
            get
            {
                int count = 0;
                for (int i = 0; i < D.DailyMonths.Count; i++)
                {
                    int key = D.DailyMonths[i];
                    int days = DateTime.DaysInMonth(key / 100, key % 100);
                    int full = (int)((1L << days) - 1);
                    if ((D.DailyDays[i] & full) == full) count++;
                }
                return count;
            }
        }

        /// <summary>Badges the player has not looked at yet: the daily card shows a dot.</summary>
        public static bool HasUnseenBadges => DailyBadges.EarnedCount > D.DailyBadgesSeen;

        public static void MarkBadgesSeen()
        {
            int earned = DailyBadges.EarnedCount;
            if (D.DailyBadgesSeen == earned) return;
            D.DailyBadgesSeen = earned;
            Save();
        }

        // ------------------------------------------------------------------ lifetime

        public static int Games => D.Games;
        public static long TotalScore => D.TotalScore;
        public static int TotalLines => D.TotalLines;
        public static int TotalPieces => D.TotalPieces;
        public static int BestCombo => D.BestCombo;
        public static int PerfectClears => D.PerfectClears;
        public static int MonoLinesTotal => D.MonoLines;
        public static int PowersUsed => D.PowersUsed;
        public static int GemsCollected => D.GemsCollected;

        /// <summary>Folds a finished run into the lifetime statistics.</summary>
        public static void RecordRun(GameSession session)
        {
            if (session == null) return;

            D.Games++;
            D.TotalScore += session.Score;
            D.TotalLines += session.LinesCleared;
            D.TotalPieces += session.PlacedPieces;
            D.BestCombo = Math.Max(D.BestCombo, session.BestCombo);
            D.PerfectClears += session.PerfectClears;
            D.MonoLines += session.MonoLines;
            D.PowersUsed += session.PowersUsed;
            D.GemsCollected += session.GemsCollected;
            Save();
        }

        /// <summary>The furthest stage of light any classic run has reached (Spectrum).</summary>
        public static int BestSpectrum => D.BestSpectrum;

        /// <summary>Records a stage reached. Returns true when it is further than ever before.</summary>
        public static bool RecordSpectrum(int stage)
        {
            if (stage <= D.BestSpectrum) return false;
            D.BestSpectrum = stage;
            Save();
            return true;
        }

        // ------------------------------------------------------------------ preferences

        public static int Theme
        {
            get => D.Theme;
            set
            {
                if (D.Theme == value) return;
                D.Theme = value;
                Save();
            }
        }

        public static bool ColorBlind
        {
            get => D.ColorBlind;
            set
            {
                if (D.ColorBlind == value) return;
                D.ColorBlind = value;
                Save();
            }
        }

        public static bool TutorialSeen
        {
            get => D.TutorialSeen;
            set
            {
                if (D.TutorialSeen == value) return;
                D.TutorialSeen = value;
                Save();
            }
        }

        /// <summary>The hand has shown the powers once, at the first jam they could have rescued.</summary>
        public static bool PowersHinted
        {
            get => D.PowersHinted;
            set
            {
                if (D.PowersHinted == value) return;
                D.PowersHinted = value;
                Save();
            }
        }

        /// <summary>Wipes everything. Only reachable from the settings, behind a confirmation.</summary>
        public static void ResetAll()
        {
            _data = new Data();
            Save();
        }
    }

    /// <summary>The adventure's boosters: bought with nothing, earned from world chests.</summary>
    public enum Booster
    {
        /// <summary>Three more moves on a level's budget, picked before it starts.</summary>
        Moves = 0,

        /// <summary>A prism charge to start with, picked before it starts.</summary>
        Charge = 1,

        /// <summary>Smash one block during play — ice, stone, shade, a timer, anything.</summary>
        Hammer = 2
    }

    /// <summary>
    /// The run in progress, one slot per mode, so leaving the app never costs a game. Written on
    /// every move and whenever the app is backgrounded; cleared when the run ends.
    /// </summary>
    public static class RunStore
    {
        static string Key(GameMode mode) => "prizma.run." + mode.ToString().ToLowerInvariant();

        public static void Save(GameSession session)
        {
            if (session == null) return;

            var mode = session.Mode;
            if (session.IsFinished)
            {
                Clear(mode);
                return;
            }

            PlayerPrefs.SetString(Key(mode), JsonUtility.ToJson(session.CreateSnapshot()));
            PlayerPrefs.Save();
        }

        public static bool Has(GameMode mode) => PlayerPrefs.HasKey(Key(mode));

        /// <summary>The saved run for a mode, or null when there is none or it cannot be trusted.</summary>
        public static GameSession Load(GameMode mode)
        {
            string json = PlayerPrefs.GetString(Key(mode), null);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var snapshot = JsonUtility.FromJson<SessionSnapshot>(json);
                var session = GameSession.Restore(snapshot);
                if (session.Mode != mode || session.IsFinished) throw new FormatException("Stale save.");
                return session;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] Discarding unreadable {mode} save: {e.Message}");
                Clear(mode);
                return null;
            }
        }

        /// <summary>Peeks at a saved run without keeping it — the menu uses it to label "continue".</summary>
        public static SessionSnapshot Peek(GameMode mode)
        {
            string json = PlayerPrefs.GetString(Key(mode), null);
            if (string.IsNullOrEmpty(json)) return null;

            try { return JsonUtility.FromJson<SessionSnapshot>(json); }
            catch (Exception) { return null; }
        }

        public static void Clear(GameMode mode)
        {
            PlayerPrefs.DeleteKey(Key(mode));
            PlayerPrefs.Save();
        }
    }
}
