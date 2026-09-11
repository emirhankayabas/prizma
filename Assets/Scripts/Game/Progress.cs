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

            public string DailyLastDate = "";
            public int DailyStreak;
            public int DailyBestStreak;
            public string DailyBestDate = "";
            public int DailyBestScore;
            public int DailyGames;

            public int Games;
            public long TotalScore;
            public int TotalLines;
            public int TotalPieces;
            public int BestCombo;
            public int PerfectClears;
            public int MonoLines;
            public int PowersUsed;
            public int GemsCollected;

            public int Theme;
            public bool ColorBlind;
            public bool TutorialSeen;
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

        public static int TotalStars
        {
            get
            {
                int total = 0;
                foreach (int s in D.Stars) total += s;
                return total;
            }
        }

        public static int LevelsCompleted => D.Stars.Count;

        /// <summary>The furthest level the player may start. Levels unlock one at a time.</summary>
        public static int UnlockedLevel => D.Stars.Count + 1;

        /// <summary>Records a finished level. Returns true when it beat the previous stars.</summary>
        public static bool RecordLevel(int level, int stars)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            int i = level - 1;
            if (i < 0 || i > D.Stars.Count) return false;

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

        // ------------------------------------------------------------------ daily

        public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");
        public static string TodayKey => DateKey(DateTime.Now);
        static string YesterdayKey => DateKey(DateTime.Now.AddDays(-1));

        /// <summary>The streak as it stands today: still alive if the last daily was today or yesterday.</summary>
        public static int DailyStreak
        {
            get
            {
                string last = D.DailyLastDate;
                return last == TodayKey || last == YesterdayKey ? D.DailyStreak : 0;
            }
        }

        public static int DailyBestStreak => D.DailyBestStreak;
        public static bool PlayedDailyToday => D.DailyLastDate == TodayKey;
        public static int DailyBestToday => D.DailyBestDate == TodayKey ? D.DailyBestScore : 0;

        /// <summary>Records a finished daily run. Returns true when it is today's best.</summary>
        public static bool RecordDaily(int score)
        {
            string today = TodayKey;

            if (D.DailyLastDate != today)
            {
                D.DailyStreak = D.DailyLastDate == YesterdayKey ? D.DailyStreak + 1 : 1;
                D.DailyLastDate = today;
                if (D.DailyStreak > D.DailyBestStreak) D.DailyBestStreak = D.DailyStreak;
            }

            D.DailyGames++;

            bool best = D.DailyBestDate != today || score > D.DailyBestScore;
            if (best)
            {
                D.DailyBestDate = today;
                D.DailyBestScore = score;
            }

            Save();
            return best;
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

        /// <summary>Wipes everything. Only reachable from the settings, behind a confirmation.</summary>
        public static void ResetAll()
        {
            _data = new Data();
            Save();
        }
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
