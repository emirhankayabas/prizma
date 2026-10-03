using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Every word the game says. The words themselves live in one JSON file per language,
    /// <c>Assets/Resources/Lang/&lt;code&gt;.json</c> (tr, en, …); this class only says which word goes
    /// where. A new language is a new file — nothing here changes — and <c>Tools/lang-check.py</c>
    /// checks it against English: the same keys, the same {0} placeholders, the same list lengths,
    /// and whether Poppins has its letters.
    ///
    /// A file is a flat object: "key": "text", or "key": ["text", …] for lists (months, worlds).
    /// Three keys describe the language itself: "_name" (its name in itself, for the picker),
    /// "_culture" (number grouping and upper-casing; empty is the invariant culture) and "_font"
    /// (a TMP font asset in Resources/Fonts to fall back to for letters Poppins does not have —
    /// Cyrillic, Greek, CJK, Arabic, Thai, Vietnamese). A key missing from a file falls back to
    /// English, so a half-finished translation shows English words rather than blanks.
    ///
    /// Strings are read while the interface is built; changing the language rebuilds it
    /// (<see cref="AppController.SetLanguage"/>), the same way a theme change does.
    /// </summary>
    public static class Str
    {
        public const string Fallback = "en";

        sealed class Table
        {
            public string Code;
            public readonly Dictionary<string, string> Text = new Dictionary<string, string>();
            public readonly Dictionary<string, string[]> Lists = new Dictionary<string, string[]>();
            public CultureInfo Culture = CultureInfo.InvariantCulture;
        }

        static Dictionary<string, Table> _tables;
        static List<string> _codes;
        static Table _current;
        static Table _fallback;

        static Dictionary<string, Table> Tables
        {
            get
            {
                if (_tables == null) LoadAll();
                return _tables;
            }
        }

        /// <summary>Every language there is a file for: Turkish and English first, the rest by code.</summary>
        public static IReadOnlyList<string> Languages
        {
            get
            {
                if (_tables == null) LoadAll();
                return _codes;
            }
        }

        public static bool Has(string code) => code != null && Tables.ContainsKey(code);

        /// <summary>The language being shown. Follows <see cref="GameSettings.Language"/>.</summary>
        public static string Current => GameSettings.Language;

        static Table Active
        {
            get
            {
                string code = Current;
                if (_current == null || _current.Code != code)
                    _current = Tables.TryGetValue(code, out var table) ? table : FallbackTable;
                return _current;
            }
        }

        static Table FallbackTable
        {
            get
            {
                if (_fallback == null) Tables.TryGetValue(Fallback, out _fallback);
                return _fallback ?? new Table { Code = Fallback };
            }
        }

        public static CultureInfo Culture => Active.Culture;

        /// <summary>Upper case by the language's own rules — Turkish "i" becomes "İ", English does not.</summary>
        public static string Upper(string text) => text.ToUpper(Culture);

        /// <summary>The phone's language, if there is a file for it; English otherwise.</summary>
        public static string SystemLanguage()
        {
            string code;
            switch (Application.systemLanguage)
            {
                case UnityEngine.SystemLanguage.Turkish: code = "tr"; break;
                case UnityEngine.SystemLanguage.German: code = "de"; break;
                case UnityEngine.SystemLanguage.French: code = "fr"; break;
                case UnityEngine.SystemLanguage.Spanish: code = "es"; break;
                case UnityEngine.SystemLanguage.Portuguese: code = "pt"; break;
                case UnityEngine.SystemLanguage.Italian: code = "it"; break;
                case UnityEngine.SystemLanguage.Dutch: code = "nl"; break;
                case UnityEngine.SystemLanguage.Polish: code = "pl"; break;
                case UnityEngine.SystemLanguage.Indonesian: code = "id"; break;
                case UnityEngine.SystemLanguage.Russian: code = "ru"; break;
                case UnityEngine.SystemLanguage.Ukrainian: code = "uk"; break;
                case UnityEngine.SystemLanguage.Japanese: code = "ja"; break;
                case UnityEngine.SystemLanguage.Korean: code = "ko"; break;
                case UnityEngine.SystemLanguage.ChineseSimplified:
                case UnityEngine.SystemLanguage.Chinese: code = "zh-Hans"; break;
                case UnityEngine.SystemLanguage.ChineseTraditional: code = "zh-Hant"; break;
                case UnityEngine.SystemLanguage.Vietnamese: code = "vi"; break;
                case UnityEngine.SystemLanguage.Thai: code = "th"; break;
                case UnityEngine.SystemLanguage.Arabic: code = "ar"; break;
                default: code = Fallback; break;
            }

            return Has(code) ? code : Fallback;
        }

        /// <summary>A language's name in itself, so a player who cannot read the current one still finds theirs.</summary>
        public static string LanguageName(string code) =>
            Tables.TryGetValue(code, out var table) && table.Text.TryGetValue("_name", out var name) && name.Length > 0
                ? name
                : code.ToUpperInvariant();

        // ------------------------------------------------------------------ lookup

        static string L(string key)
        {
            if (Active.Text.TryGetValue(key, out var text)) return text;
            if (FallbackTable.Text.TryGetValue(key, out text)) return text;
            Debug.LogWarning($"[Str] '{key}' yok ({Current}).");
            return key;
        }

        static string F(string key, params object[] args)
        {
            try { return string.Format(Culture, L(key), args); }
            catch (FormatException) { return L(key); }
        }

        static string[] List(string key, int length)
        {
            if (Active.Lists.TryGetValue(key, out var list) && list.Length >= length) return list;
            if (FallbackTable.Lists.TryGetValue(key, out list) && list.Length >= length) return list;
            Debug.LogWarning($"[Str] '{key}' listesi eksik ({Current}).");
            var blank = new string[length];
            for (int i = 0; i < length; i++) blank[i] = "";
            return blank;
        }

        static string Item(string key, int index, int length) => List(key, length)[Math.Max(0, Math.Min(index, length - 1))];

        // ------------------------------------------------------------------ menu

        public static string Tagline => L("menu.tagline");
        public static string Play => L("menu.play");
        public static string Continue => L("menu.continue");
        public static string Adventure => L("menu.adventure");
        public static string Daily => L("menu.daily");
        public static string Scores => L("menu.scores");
        public static string Stats => L("menu.stats");
        public static string Themes => L("menu.themes");
        public static string LevelN(int n) => F("menu.level_n", n);
        public static string DayStreak(int n) => F("menu.day_streak", n);
        public static string StreakToday(int n) => F("menu.streak_today", n);
        public static string NewPuzzle => L("menu.new_puzzle");

        // ------------------------------------------------------------------ settings and modals

        public static string Settings => L("settings.title");
        public static string Mute => L("settings.mute");
        public static string Music => L("settings.music");
        public static string Effects => L("settings.effects");
        public static string Vibration => L("settings.vibration");
        public static string ColorBlind => L("settings.colour_blind");
        public static string Notifications => L("settings.notifications");
        /// <summary>Names the row in the current language and in English, so it is found whichever one the phone is in.</summary>
        public static string LanguageLabel => L("settings.language");
        public static string PrivacyPolicy => L("settings.privacy");

        public static string Ok => L("common.ok");
        public static string Close => L("common.close");
        public static string Paused => L("pause.title");
        public static string Restart => L("pause.restart");
        public static string SettingsButton => L("pause.settings");
        public static string BestScores => L("scores.title");
        public static string NoScores => L("scores.empty");
        public static string StatsHeading => L("stats.title");
        public static string AchievementsTab => L("stats.tab_achievements");
        public static string StatsTab => L("stats.tab_stats");
        public static string ThemesHeading => L("themes.title");

        public static string[] StatLabels => List("stats.labels", 12);

        /// <summary>A theme's name, from the name it has in Themes.cs.</summary>
        public static string ThemeName(string name)
        {
            if (Active.Text.TryGetValue("theme." + name, out var text)) return text;
            if (FallbackTable.Text.TryGetValue("theme." + name, out text)) return text;
            return name;
        }

        // ------------------------------------------------------------------ play

        public static string Moves(int n) => F("play.moves", n);
        public static string Combo(float multiplier) => F("play.combo", multiplier.ToString("0.#", Culture));
        public static string End => L("play.end");
        public static string OutOfMovesTitle => L("play.out_of_moves_title");
        public static string Charges(int n) => F("play.charges", n);
        public static string PlusMoves(int n) => F("play.plus_moves", n);
        public static string LevelTitle(int n) => F("play.level_title", n);
        public static string NoMovesLeft => L("play.no_moves_left");
        public static string NoRoom => L("play.no_room");
        public static string TryAgain => L("play.try_again");
        public static string Map => L("play.map");
        public static string DailyPuzzle => L("play.daily_puzzle");
        public static string PlayAgain => L("play.play_again");
        public static string MainMenu => L("play.main_menu");
        public static string NewRecord => L("play.new_record");
        public static string Rank(int n) => F("play.rank", n);
        public static string Lines(int n) => F("play.lines", n);
        public static string GameOver => L("play.game_over");
        public static string NewTheme(string name) => F("play.new_theme", Upper(ThemeName(name)));
        public static string Next => L("play.next");
        public static string MovesSaved(int n) => F("play.moves_saved", n);

        // ------------------------------------------------------------------ adventure

        public static string WorldName(int world) => Item("world.names", world, 10);

        public static string WorldN(int world) => F("world.n", world + 1);

        public static string Goal => L("adventure.goal");
        public static string Boosters => L("adventure.boosters");
        public static string Hard => L("adventure.hard");
        public static string VeryHard => L("adventure.very_hard");
        public static string New => L("adventure.new");
        public static string WinStreak => L("adventure.win_streak");
        public static string StreakGift(int moves, int charges) => charges > 0
            ? F("adventure.streak_gift_charges", moves, charges)
            : F("adventure.streak_gift", moves);
        public static string NoStreak => L("adventure.no_streak");

        public static string BoosterName(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: return L("booster.moves");
                case Booster.Charge: return L("booster.charge");
                default: return L("booster.hammer");
            }
        }

        public static string WorldChest => L("chest.title");
        public static string Collect => L("chest.collect");
        public static string ChestLocked(int world) => F("chest.locked", world + 1);
        public static string AdventureDone => L("adventure.done");
        public static string AllStars(int stars, int max) => $"{stars} / {max}";

        /// <summary>What a level adds to the board the first time it appears — the start sheet's chip.</summary>
        public static string IntroName(int level)
        {
            switch (level)
            {
                case Core.LevelGenerator.TilesFrom: return L("intro.tiles");
                case Core.LevelGenerator.StoneFrom: return L("intro.stone");
                case Core.LevelGenerator.ShadeFrom: return L("intro.shade");
                case Core.LevelGenerator.ColorsFrom: return L("intro.colors");
                case Core.LevelGenerator.TimersFrom: return L("intro.timers");
                case Core.LevelGenerator.DoubleIceFrom: return L("intro.double_ice");
                default: return "";
            }
        }

        public static string TimerBurst => L("play.timer_burst");

        // ------------------------------------------------------------------ dates

        public static string MonthName(int month) => Item("date.months", month - 1, 12);

        /// <summary>"15 EYLÜL" / "SEP 15" — the daily chip in the HUD.</summary>
        public static string ShortDate(DateTime date) => DateText("date.short", date);

        public static string LongDate(DateTime date) => DateText("date.long", date);

        /// <summary>
        /// A date from its template: {day}, {month} ("September"), {MONTH} ("SEPTEMBER"),
        /// {mon} ("Sep"), {MON} ("SEP"). Languages put day and month in their own order.
        /// </summary>
        static string DateText(string key, DateTime date)
        {
            string month = MonthName(date.Month);
            string mon = month.Length > 3 ? month.Substring(0, 3) : month;
            return L(key)
                .Replace("{day}", date.Day.ToString(CultureInfo.InvariantCulture))
                .Replace("{MONTH}", Upper(month))
                .Replace("{month}", month)
                .Replace("{MON}", Upper(mon))
                .Replace("{mon}", mon);
        }

        /// <summary>Monday first in every language, as the daily's week is drawn.</summary>
        public static string[] Weekdays => List("date.weekdays_short", 7);

        public static string DayName(DayOfWeek day) => Item("date.days", (int)day, 7);

        // ------------------------------------------------------------------ the daily puzzle

        /// <summary>Short on purpose: it heads the daily card beside its close button, and the result card.</summary>
        public static string DailyTitle(int number) => F("daily.title", number);
        public static string TodayTab => L("daily.tab_today");
        public static string BadgesTab => L("daily.tab_badges");
        public static string StartPuzzle => L("daily.start");
        public static string Solved => L("daily.solved");
        public static string Share => L("daily.share");
        public static string StreakWord => L("daily.streak_word");
        public static string BestStreak(int n) => F("daily.best_streak", n);
        public static string Attempt(int n) => F("daily.attempt", n);
        public static string TimeLabel => L("daily.time");
        public static string MovesLabel => L("daily.moves");
        public static string AttemptsLabel => L("daily.attempts");
        public static string StarsLabel => L("daily.stars");
        public static string Earned => L("daily.earned");
        public static string NextPuzzle => L("daily.next_puzzle");

        /// <summary>The word under the goal's number: "KRİSTAL" / "SATIR".</summary>
        public static string GoalWord(Core.GoalKind goal)
        {
            switch (goal)
            {
                case Core.GoalKind.Lines: return L("goal.lines");
                case Core.GoalKind.Score: return L("goal.score");
                case Core.GoalKind.Tiles: return L("goal.tiles");
                case Core.GoalKind.Colors: return L("goal.colors");
                case Core.GoalKind.Shade: return L("goal.shade");
                default: return L("goal.gems");
            }
        }

        public static string Grade(int grade) => Item("grade", grade, 4);

        /// <summary>"12 KRİSTAL" / "8 SATIR" — what the day's puzzle asks for.</summary>
        public static string GoalText(Core.GoalKind goal, int target) => goal == Core.GoalKind.Lines
            ? F("daily.goal_lines", target)
            : F("daily.goal_gems", target);

        /// <summary>"1:42", or "1:02:05" past an hour.</summary>
        public static string Clock(float seconds)
        {
            int total = Math.Max(0, (int)seconds);
            int h = total / 3600, m = total / 60 % 60, s = total % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        }

        /// <summary>A countdown, always with hours: "07:12:44".</summary>
        public static string Countdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        public static string NewBadge(string name) => F("badge.new", Upper(name));

        public static string ShareText(int number, float seconds, int stars, int attempts, int streak, string link)
        {
            string starRow = new string('★', Math.Max(0, stars)) + new string('☆', Math.Max(0, 3 - stars));
            string line1 = F("share.title", number);
            string line2 = $"⏱ {Clock(seconds)}  {starRow}  " + (attempts <= 1 ? L("share.first_try") : F("share.tries", attempts));
            string line3 = streak > 0 ? F("share.streak", streak) : "";
            string line4 = L("share.challenge");
            return string.Join("\n", new[] { line1, line2, line3, line4, link }).Replace("\n\n", "\n");
        }

        // ------------------------------------------------------------------ reminder

        public static string ReminderTitle => "PRIZMA";
        public static string ReminderChannel => L("reminder.channel");

        public static string ReminderBody(int day, bool streak) => streak ? F("reminder.streak", day) : L("reminder.ready");

        public static string ReminderFollowUp => L("reminder.follow_up");

        // ------------------------------------------------------------------ badges

        public static string BadgeName(string id) =>
            Active.Text.ContainsKey($"badge.{id}.name") || FallbackTable.Text.ContainsKey($"badge.{id}.name") ? L($"badge.{id}.name") : id;

        public static string BadgeGoal(string id, long count)
        {
            // The streak and solve badges share one sentence each, with their own count.
            string key;
            if (id.StartsWith("streak", StringComparison.Ordinal)) key = "badge.streak.goal";
            else if (id.StartsWith("solves", StringComparison.Ordinal)) key = "badge.solves.goal";
            else key = $"badge.{id}.goal";

            return Active.Text.ContainsKey(key) || FallbackTable.Text.ContainsKey(key)
                ? F(key, count.ToString("N0", Culture))
                : "";
        }

        // ------------------------------------------------------------------ achievements

        public static string AchievementName(string id) =>
            Active.Text.ContainsKey($"achievement.{id}.name") || FallbackTable.Text.ContainsKey($"achievement.{id}.name")
                ? L($"achievement.{id}.name")
                : id;

        /// <summary>
        /// Grouped like the count beside it ("1.000", "1,000"): "1000 lines" over "130 / 1.000" read as
        /// two numbers. Short on purpose: one line beside a count and three stars, down to a 16:9 phone.
        /// </summary>
        public static string AchievementGoal(string id, long count)
        {
            string key = $"achievement.{id}.goal";
            return Active.Text.ContainsKey(key) || FallbackTable.Text.ContainsKey(key)
                ? F(key, count.ToString("N0", Culture))
                : "";
        }

        // ------------------------------------------------------------------ font

        /// <summary>
        /// Gives the Poppins assets the language's fallback font, for the letters Poppins does not
        /// have. Languages in Latin script name none, and then nothing is touched.
        /// </summary>
        public static void ApplyFont()
        {
            Active.Text.TryGetValue("_font", out var name);
            TMP_FontAsset fallback = string.IsNullOrEmpty(name) ? null : Resources.Load<TMP_FontAsset>("Fonts/" + name);
            if (!string.IsNullOrEmpty(name) && fallback == null)
                Debug.LogWarning($"[Str] '{Current}' dili için font yok: Resources/Fonts/{name}");

            foreach (var font in new[] { Design.FontDisplay, Design.FontMedium, Design.FontBody })
            {
                if (font == null) continue;
                var table = font.fallbackFontAssetTable;
                bool empty = table == null || table.Count == 0;
                if (fallback == null && empty) continue;
                if (fallback != null && !empty && table.Count == 1 && table[0] == fallback) continue;
                font.fallbackFontAssetTable = fallback == null ? new List<TMP_FontAsset>() : new List<TMP_FontAsset> { fallback };
            }
        }

        // ------------------------------------------------------------------ loading

        static void LoadAll()
        {
            _tables = new Dictionary<string, Table>();
            foreach (var asset in Resources.LoadAll<TextAsset>("Lang"))
            {
                var table = new Table { Code = asset.name };
                try { Json.Read(asset.text, table.Text, table.Lists); }
                catch (Exception e)
                {
                    Debug.LogError($"[Str] Lang/{asset.name}.json okunamadı: {e.Message}");
                    continue;
                }

                if (table.Text.TryGetValue("_culture", out var culture) && culture.Length > 0)
                {
                    try { table.Culture = new CultureInfo(culture); }
                    catch (CultureNotFoundException) { Debug.LogWarning($"[Str] Kültür bulunamadı: {culture}"); }
                }

                _tables[table.Code] = table;
            }

            _codes = new List<string>(_tables.Keys);
            _codes.Sort((a, b) =>
            {
                int ra = Rank(a), rb = Rank(b);
                return ra != rb ? ra.CompareTo(rb) : string.CompareOrdinal(a, b);
            });
        }

        static int Rank(string code) => code == "tr" ? 0 : code == "en" ? 1 : 2;

        /// <summary>
        /// Just enough JSON for the language files: one object of strings and lists of strings.
        /// JsonUtility cannot read a dictionary, and a library for this would be the project's only one.
        /// </summary>
        static class Json
        {
            public static void Read(string text, Dictionary<string, string> strings, Dictionary<string, string[]> lists)
            {
                int i = 0;
                Skip(text, ref i);
                Expect(text, ref i, '{');
                Skip(text, ref i);
                if (Peek(text, i) == '}') return;

                while (true)
                {
                    Skip(text, ref i);
                    string key = ReadString(text, ref i);
                    Skip(text, ref i);
                    Expect(text, ref i, ':');
                    Skip(text, ref i);

                    if (Peek(text, i) == '[')
                    {
                        i++;
                        var items = new List<string>();
                        Skip(text, ref i);
                        if (Peek(text, i) != ']')
                            while (true)
                            {
                                Skip(text, ref i);
                                items.Add(ReadString(text, ref i));
                                Skip(text, ref i);
                                if (Peek(text, i) == ',') { i++; continue; }
                                break;
                            }

                        Expect(text, ref i, ']');
                        lists[key] = items.ToArray();
                    }
                    else
                    {
                        strings[key] = ReadString(text, ref i);
                    }

                    Skip(text, ref i);
                    if (Peek(text, i) == ',') { i++; continue; }
                    Expect(text, ref i, '}');
                    return;
                }
            }

            static char Peek(string text, int i) => i < text.Length ? text[i] : '\0';

            static void Skip(string text, ref int i)
            {
                while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '﻿')) i++;
            }

            static void Expect(string text, ref int i, char c)
            {
                if (Peek(text, i) != c) throw new FormatException($"'{c}' bekleniyordu, konum {i}");
                i++;
            }

            static string ReadString(string text, ref int i)
            {
                Expect(text, ref i, '"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= text.Length) throw new FormatException("kapanmamış metin");
                    char c = text[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    char e = text[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(text.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }
        }
    }
}
