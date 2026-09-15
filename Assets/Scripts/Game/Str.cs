using System;
using System.Globalization;

namespace BlockPuzzle.Game
{
    public enum Language
    {
        Turkish = 0,
        English = 1
    }

    /// <summary>
    /// Every word the game says, in every language it speaks. One file, so a translation is
    /// reviewed in one place and a missing one is a compile error rather than a blank label.
    ///
    /// Strings are read while the interface is built; changing the language rebuilds it
    /// (<see cref="AppController.SetLanguage"/>), the same way a theme change does.
    /// Adding a language: add it to <see cref="Language"/> and a column to <see cref="T"/>.
    /// </summary>
    public static class Str
    {
        public static Language Current => GameSettings.Language;

        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        public static CultureInfo Culture => Current == Language.Turkish ? Turkish : CultureInfo.InvariantCulture;

        static string T(string tr, string en) => Current == Language.English ? en : tr;

        /// <summary>Upper case by the language's own rules — Turkish "i" becomes "İ", English does not.</summary>
        public static string Upper(string text) => text.ToUpper(Culture);

        // ------------------------------------------------------------------ menu

        public static string Tagline => T("BLOK BULMACA", "BLOCK PUZZLE");
        public static string Play => T("OYNA", "PLAY");
        public static string Continue => T("DEVAM ET", "CONTINUE");
        public static string Adventure => T("MACERA", "ADVENTURE");
        public static string Daily => T("GÜNLÜK", "DAILY");
        public static string Scores => T("SKORLAR", "SCORES");
        public static string Stats => T("İSTATİSTİK", "STATS");
        public static string Themes => T("TEMALAR", "THEMES");
        public static string LevelN(int n) => T($"BÖLÜM {n}", $"LEVEL {n}");
        public static string DayStreak(int n) => T($"{n} GÜN SERİ", $"{n} DAY STREAK");
        public static string StreakToday(int n) => T($"SERİ {n} · BUGÜN?", $"STREAK {n} · TODAY?");
        public static string NewPuzzle => T("YENİ BULMACA", "NEW PUZZLE");

        // ------------------------------------------------------------------ settings and modals

        public static string Settings => T("Ayarlar", "Settings");
        public static string Mute => T("SESİ KAPAT", "MUTE");
        public static string Music => T("MÜZİK", "MUSIC");
        public static string Effects => T("EFEKTLER", "EFFECTS");
        public static string Vibration => T("TİTREŞİM", "VIBRATION");
        public static string ColorBlind => T("RENK KÖRÜ MODU", "COLOUR BLIND");
        /// <summary>Names the row in both languages, so it is found whichever one the phone is in.</summary>
        public static string LanguageLabel => T("DİL · LANGUAGE", "LANGUAGE · DİL");

        /// <summary>A language's name in itself, so a player who cannot read the current one still finds theirs.</summary>
        public static string LanguageName(Language language) => language == Language.English ? "ENGLISH" : "TÜRKÇE";

        public static string Ok => T("TAMAM", "OK");
        public static string Close => T("KAPAT", "CLOSE");
        public static string Paused => T("Duraklatıldı", "Paused");
        public static string Restart => T("YENİDEN BAŞLA", "RESTART");
        public static string SettingsButton => T("AYARLAR", "SETTINGS");
        public static string Home => T("ANA SAYFAYA DÖN", "BACK TO MENU");
        public static string BestScores => T("En İyi Skorlar", "Best Scores");
        public static string NoScores => T("Henüz skor yok.\nİlk oyununu oyna.", "No scores yet.\nPlay your first game.");
        public static string StatsHeading => T("İstatistikler", "Statistics");
        public static string AchievementsTab => T("BAŞARIMLAR", "ACHIEVEMENTS");
        public static string StatsTab => T("İSTATİSTİK", "STATS");
        public static string ThemesHeading => T("Temalar", "Themes");

        public static string[] StatLabels => Current == Language.English
            ? new[]
            {
                "GAMES PLAYED", "BEST SCORE", "TOTAL POINTS", "LINES CLEARED", "PIECES PLACED",
                "LONGEST COMBO", "SINGLE-COLOUR LINES", "BOARD CLEARS", "CRYSTALS FREED", "POWERS USED",
                "LEVEL STARS", "LONGEST DAILY STREAK"
            }
            : new[]
            {
                "OYNANAN OYUN", "EN İYİ SKOR", "TOPLAM PUAN", "TEMİZLENEN SATIR", "YERLEŞTİRİLEN PARÇA",
                "EN UZUN COMBO", "TEK RENK SATIR", "TAHTAYI SIFIRLAMA", "SERBEST KRİSTAL", "KULLANILAN GÜÇ",
                "BÖLÜM YILDIZI", "EN UZUN GÜNLÜK SERİ"
            };

        public static string ThemeName(string turkishName)
        {
            if (Current == Language.Turkish) return turkishName;
            switch (turkishName)
            {
                case "Mücevher": return "Jewel";
                case "Şeker": return "Candy";
                default: return turkishName;
            }
        }

        // ------------------------------------------------------------------ play

        public static string Moves(int n) => T($"{n} HAMLE", $"{n} MOVES");
        public static string Combo(float multiplier) => $"COMBO ×{multiplier.ToString("0.#", Culture)}";
        public static string End => T("BİTİR", "END");
        public static string OutOfMovesTitle => T("Hamle Bitti", "Out of Moves");
        public static string Charges(int n) => T($"{n} ŞARJ", $"{n} CHARGES");
        public static string PlusMoves(int n) => T($"+{n} HAMLE", $"+{n} MOVES");
        public static string LevelTitle(int n) => T($"Bölüm {n}", $"Level {n}");
        public static string NoMovesLeft => T("HAMLE BİTTİ", "NO MOVES LEFT");
        public static string NoRoom => T("YER KALMADI", "NO ROOM LEFT");
        public static string TryAgain => T("TEKRAR DENE", "TRY AGAIN");
        public static string Map => T("HARİTA", "MAP");
        public static string TodaysBest => T("Bugünün Rekoru", "Today's Best");
        public static string DailyPuzzle => T("Günlük Bulmaca", "Daily Puzzle");
        public static string PlayAgain => T("TEKRAR OYNA", "PLAY AGAIN");
        public static string MainMenu => T("ANA MENÜ", "MAIN MENU");
        public static string NewRecord => T("YENİ REKOR", "NEW RECORD");
        public static string Rank(int n) => T($"{n}. SIRA", $"#{n}");
        public static string Lines(int n) => T($"{n} SATIR", $"{n} LINES");
        public static string GameOver => T("Oyun Bitti", "Game Over");
        public static string NewTheme(string name) => T("YENİ TEMA: ", "NEW THEME: ") + Upper(ThemeName(name));
        public static string Next => T("SONRAKİ", "NEXT");
        public static string MovesSaved(int n) => T($"{n} HAMLE ARTTI", $"{n} MOVES TO SPARE");

        // ------------------------------------------------------------------ dates

        static readonly string[] MonthsTr =
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        static readonly string[] MonthsEn =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public static string MonthName(int month) => (Current == Language.English ? MonthsEn : MonthsTr)[month - 1];

        /// <summary>"15 EYLÜL" / "SEP 15" — the daily chip in the HUD.</summary>
        public static string ShortDate(DateTime date) => Current == Language.English
            ? $"{Upper(MonthsEn[date.Month - 1].Substring(0, 3))} {date.Day}"
            : $"{date.Day} {Upper(MonthsTr[date.Month - 1])}";

        public static string LongDate(DateTime date) => Current == Language.English
            ? $"{MonthsEn[date.Month - 1]} {date.Day}"
            : $"{date.Day} {MonthsTr[date.Month - 1]}";

        public static string MonthHeading(int year, int month) => $"{MonthName(month)} {year}";

        /// <summary>Monday first in both languages, as both regions count the week.</summary>
        public static string[] Weekdays => Current == Language.English
            ? new[] { "M", "T", "W", "T", "F", "S", "S" }
            : new[] { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz" };

        // ------------------------------------------------------------------ daily calendar and sharing

        public static string PlayToday => T("BUGÜNÜ OYNA", "PLAY TODAY");
        public static string ContinueToday => T("BUGÜNE DEVAM ET", "CONTINUE TODAY");
        public static string PlayedToday => T("BUGÜN OYNANDI", "PLAYED TODAY");
        public static string DailyOn(DateTime date) => T($"Günlük · {LongDate(date)}", $"Daily · {LongDate(date)}");
        public static string DailyScore(int score) => T($"{score} PUAN", $"{score} POINTS");
        public static string Share => T("PAYLAŞ", "SHARE");

        public static string ShareText(DateTime date, int score, int streak, string link)
        {
            string line1 = T($"PRIZMA Günlük · {LongDate(date)}", $"PRIZMA Daily · {LongDate(date)}");
            string line2 = T($"⭐ {score} puan", $"⭐ {score} points") +
                           (streak > 0 ? T($" · 🔥 {streak} gün seri", $" · 🔥 {streak} day streak") : "");
            string line3 = T("Sen kaç yapabilirsin?", "Can you beat it?");
            return $"{line1}\n{line2}\n{line3}\n{link}";
        }

        // ------------------------------------------------------------------ achievements

        public static string AchievementName(string id)
        {
            switch (id)
            {
                case "games": return T("Tutkun", "Regular");
                case "lines": return T("Satır Avcısı", "Line Hunter");
                case "score": return T("Yüksek Skor", "High Scorer");
                case "combo": return T("Combo Ustası", "Combo Master");
                case "mono": return T("Prizma Satırı", "Prism Lines");
                case "perfect": return T("Tertemiz", "Spotless");
                case "gems": return T("Kristal Toplayıcı", "Crystal Keeper");
                case "powers": return T("Güç Kullanıcı", "Power User");
                case "levels": return T("Kaşif", "Explorer");
                case "streak": return T("Kararlı", "Dedicated");
                case "months": return T("Takvim Ustası", "Full Calendar");
                default: return id;
            }
        }

        public static string AchievementGoal(string id, long target)
        {
            switch (id)
            {
                // Short on purpose: one line beside a count and three stars, down to a 16:9 phone.
                case "games": return T($"{target} oyun oyna", $"Play {target} games");
                case "lines": return T($"{target} satır temizle", $"Clear {target} lines");
                case "score": return T($"{target} puanlık oyun", $"Score {target}");
                case "combo": return T($"{target} hamlelik combo", $"{target}-move combo");
                case "mono": return T($"{target} tek renk satır", $"{target} one-colour lines");
                case "perfect": return T($"Tahtayı sıfırla ×{target}", $"Clear the board ×{target}");
                case "gems": return T($"{target} kristal topla", $"Collect {target} crystals");
                case "powers": return T($"{target} güç kullan", $"Use {target} powers");
                case "levels": return T($"{target} bölüm bitir", $"Finish {target} levels");
                case "streak": return T($"{target} gün seri", $"{target}-day streak");
                case "months": return T($"Tam ay ×{target}", $"Full month ×{target}");
                default: return "";
            }
        }
    }
}
