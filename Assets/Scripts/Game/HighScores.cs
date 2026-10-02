using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Game
{
    [Serializable]
    public sealed class ScoreEntry
    {
        public int Score;
        public string Date;
        public int Lines;

        // Written round-trip ("o"), so read back the same way: parsed in the current culture, a
        // phone set to a non-Gregorian calendar would have read the year in that calendar.
        public DateTime When => DateTime.TryParse(Date, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue;
    }

    /// <summary>
    /// The local top ten, kept in PlayerPrefs as JSON. Deliberately a flat list rather than a
    /// single best score, so the scores screen has something to show after the first run.
    /// </summary>
    public static class HighScores
    {
        const string Key = "blockpuzzle.scores.v1";
        public const int Capacity = 10;

        [Serializable]
        sealed class Table
        {
            public List<ScoreEntry> Entries = new List<ScoreEntry>();
        }

        static Table _table;

        public static IReadOnlyList<ScoreEntry> All
        {
            get
            {
                Load();
                return _table.Entries;
            }
        }

        public static int Best => All.Count > 0 ? All[0].Score : 0;

        /// <summary>
        /// Records a finished run. Returns its 1-based rank in the table, or 0 when it did not
        /// make the cut — the game-over screen uses that to decide whether to celebrate.
        /// </summary>
        public static int Submit(int score, int lines)
        {
            Load();
            if (score <= 0) return 0;

            var entry = new ScoreEntry
            {
                Score = score,
                Lines = lines,
                Date = DateTime.Now.ToString("o")
            };

            _table.Entries.Add(entry);
            _table.Entries.Sort((a, b) => b.Score.CompareTo(a.Score));

            if (_table.Entries.Count > Capacity)
                _table.Entries.RemoveRange(Capacity, _table.Entries.Count - Capacity);

            Save();

            int rank = _table.Entries.IndexOf(entry);
            return rank < 0 ? 0 : rank + 1;
        }

        public static void Clear()
        {
            _table = new Table();
            Save();
        }

        static void Load()
        {
            if (_table != null) return;

            string json = PlayerPrefs.GetString(Key, null);
            if (string.IsNullOrEmpty(json))
            {
                _table = new Table();
                return;
            }

            try
            {
                _table = JsonUtility.FromJson<Table>(json) ?? new Table();
                if (_table.Entries == null) _table.Entries = new List<ScoreEntry>();
            }
            catch (Exception)
            {
                // A corrupt table is not worth crashing over; start fresh.
                _table = new Table();
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_table));
            PlayerPrefs.Save();
        }
    }
}
