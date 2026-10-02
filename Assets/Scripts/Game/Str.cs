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
        public static string Notifications => T("BİLDİRİMLER", "NOTIFICATIONS");
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

        // ------------------------------------------------------------------ adventure

        static readonly string[] WorldsTr =
        {
            "Kristal Kıyısı", "Işık Tarlası", "Taş Geçit", "Gölge Ormanı", "Renk Çarşısı",
            "Saat Kulesi", "Buz Sarayı", "Fırtına Tepesi", "Yıldız Denizi", "Prizma Tacı"
        };

        static readonly string[] WorldsEn =
        {
            "Crystal Shore", "Glow Fields", "Stone Pass", "Shade Woods", "Colour Bazaar",
            "Clock Tower", "Ice Palace", "Storm Peak", "Star Sea", "Prism Crown"
        };

        public static string WorldName(int world) =>
            (Current == Language.English ? WorldsEn : WorldsTr)[Math.Max(0, Math.Min(world, WorldsTr.Length - 1))];

        public static string WorldN(int world) => T($"DÜNYA {world + 1}", $"WORLD {world + 1}");

        public static string Goal => T("HEDEF", "GOAL");
        public static string Boosters => T("GÜÇLENDİRİCİLER", "BOOSTERS");
        public static string Hard => T("ZOR", "HARD");
        public static string VeryHard => T("ÇOK ZOR", "SUPER HARD");
        public static string New => T("YENİ", "NEW");
        public static string WinStreak => T("GALİBİYET SERİSİ", "WIN STREAK");
        public static string StreakGift(int moves, int charges) => charges > 0
            ? T($"+{moves} HAMLE · +{charges} ŞARJ", $"+{moves} MOVES · +{charges} CHARGE")
            : T($"+{moves} HAMLE", $"+{moves} MOVES");
        public static string NoStreak => T("KAZANDIKÇA HEDİYE", "WIN FOR GIFTS");

        public static string BoosterName(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: return T("+3 HAMLE", "+3 MOVES");
                case Booster.Charge: return T("ŞARJ", "CHARGE");
                default: return T("ÇEKİÇ", "HAMMER");
            }
        }

        public static string WorldChest => T("Dünya Sandığı", "World Chest");
        public static string Collect => T("AL", "COLLECT");
        public static string ChestLocked(int world) => T($"{world + 1}. DÜNYAYI BİTİR", $"FINISH WORLD {world + 1}");
        public static string AdventureDone => T("Macera Tamamlandı", "Adventure Complete");
        public static string AllStars(int stars, int max) => $"{stars} / {max}";

        /// <summary>What a level adds to the board the first time it appears — the start sheet's chip.</summary>
        public static string IntroName(int level)
        {
            switch (level)
            {
                case Core.LevelGenerator.TilesFrom: return T("IŞIK KAROSU", "GLOW TILE");
                case Core.LevelGenerator.StoneFrom: return T("TAŞ", "STONE");
                case Core.LevelGenerator.ShadeFrom: return T("GÖLGE", "SHADE");
                case Core.LevelGenerator.ColorsFrom: return T("RENK SİPARİŞİ", "COLOUR ORDER");
                case Core.LevelGenerator.TimersFrom: return T("SAATLİ BLOK", "TIMER BLOCK");
                case Core.LevelGenerator.DoubleIceFrom: return T("ÇİFT BUZ", "THICK ICE");
                default: return "";
            }
        }

        public static string TimerBurst => T("SAAT DOLDU", "TIME'S UP");

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


        /// <summary>Monday first in both languages, as both regions count the week.</summary>
        public static string[] Weekdays => Current == Language.English
            ? new[] { "M", "T", "W", "T", "F", "S", "S" }
            : new[] { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz" };

        // ------------------------------------------------------------------ the daily puzzle

        /// <summary>Short on purpose: it heads the daily card beside its close button, and the result card.</summary>
        public static string DailyTitle(int number) => T($"Bulmaca #{number}", $"Puzzle #{number}");
        public static string TodayTab => T("BUGÜN", "TODAY");
        public static string BadgesTab => T("ROZETLER", "BADGES");
        public static string StartPuzzle => T("BAŞLA", "START");
        public static string Solved => T("ÇÖZÜLDÜ", "SOLVED");
        public static string Share => T("PAYLAŞ", "SHARE");
        public static string StreakWord => T("GÜN SERİ", "DAY STREAK");
        public static string BestStreak(int n) => T($"EN İYİ {n}", $"BEST {n}");
        public static string Attempt(int n) => T($"{n}. DENEME", $"ATTEMPT {n}");
        public static string TimeLabel => T("SÜRE", "TIME");
        public static string MovesLabel => T("HAMLE", "MOVES");
        public static string AttemptsLabel => T("DENEME", "TRIES");
        public static string StarsLabel => T("YILDIZ", "STARS");
        public static string Earned => T("KAZANILDI", "EARNED");
        public static string NextPuzzle => T("YENİ BULMACA", "NEXT PUZZLE");

        /// <summary>The word under the goal's number: "KRİSTAL" / "SATIR".</summary>
        public static string GoalWord(Core.GoalKind goal)
        {
            switch (goal)
            {
                case Core.GoalKind.Lines: return T("SATIR", "LINES");
                case Core.GoalKind.Score: return T("PUAN", "POINTS");
                case Core.GoalKind.Tiles: return T("IŞIK", "GLOW");
                case Core.GoalKind.Colors: return T("BLOK", "BLOCKS");
                case Core.GoalKind.Shade: return T("GÖLGE", "SHADE");
                default: return T("KRİSTAL", "CRYSTALS");
            }
        }

        public static string Grade(int grade)
        {
            switch (grade)
            {
                case 0: return T("KOLAY", "EASY");
                case 1: return T("ORTA", "MEDIUM");
                case 2: return T("ZOR", "HARD");
                default: return T("ÇOK ZOR", "EXPERT");
            }
        }

        static readonly string[] DaysTr = { "PAZAR", "PAZARTESİ", "SALI", "ÇARŞAMBA", "PERŞEMBE", "CUMA", "CUMARTESİ" };
        static readonly string[] DaysEn = { "SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" };

        public static string DayName(DayOfWeek day) => (Current == Language.English ? DaysEn : DaysTr)[(int)day];

        /// <summary>"12 KRİSTAL" / "8 SATIR" — what the day's puzzle asks for.</summary>
        public static string GoalText(Core.GoalKind goal, int target) => goal == Core.GoalKind.Lines
            ? T($"{target} SATIR", $"{target} LINES")
            : T($"{target} KRİSTAL", $"{target} CRYSTALS");

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

        public static string NewBadge(string name) => T("YENİ ROZET: ", "NEW BADGE: ") + Upper(name);

        public static string ShareText(int number, float seconds, int stars, int attempts, int streak, string link)
        {
            string starRow = new string('★', Math.Max(0, stars)) + new string('☆', Math.Max(0, 3 - stars));
            string line1 = T($"PRIZMA · Günün Bulmacası #{number}", $"PRIZMA · Daily Puzzle #{number}");
            string line2 = $"⏱ {Clock(seconds)}  {starRow}  " +
                           (attempts <= 1 ? T("ilk denemede", "first try") : T($"{attempts}. denemede", $"{attempts} tries"));
            string line3 = streak > 0 ? T($"🔥 {streak} gün seri", $"🔥 {streak}-day streak") : "";
            string line4 = T("Sen kaç dakikada çözersin?", "How fast can you solve it?");
            return string.Join("\n", new[] { line1, line2, line3, line4, link }).Replace("\n\n", "\n");
        }

        // ------------------------------------------------------------------ reminder

        public static string ReminderTitle => "PRIZMA";
        public static string ReminderChannel => T("Günlük bulmaca", "Daily puzzle");

        public static string ReminderBody(int day, bool streak) => streak
            ? T($"🔥 {day}. gün seni bekliyor — serini sürdür!", $"🔥 Day {day} is waiting — keep your streak alive!")
            : T("Günün bulmacası hazır. Hadi çöz!", "Today's puzzle is ready. Can you solve it?");

        public static string ReminderFollowUp => T("Yeni bir bulmaca seni bekliyor.", "A new puzzle is waiting for you.");

        // ------------------------------------------------------------------ badges

        public static string BadgeName(string id)
        {
            switch (id)
            {
                case "first": return T("İlk Işık", "First Light");
                case "streak3": return T("Kıvılcım", "Spark");
                case "streak7": return T("Tam Hafta", "Full Week");
                case "streak14": return T("Alev", "Blaze");
                case "streak30": return T("Ateş Topu", "Fireball");
                case "streak100": return T("Efsane", "Legend");
                case "solves10": return T("Düzenli", "Regular");
                case "solves50": return T("Müdavim", "Devotee");
                case "firsttry": return T("Tek Atış", "One Shot");
                case "firsttry10": return T("Keskin Nişancı", "Sharpshooter");
                case "stars3": return T("Kusursuz", "Flawless");
                case "speed": return T("Şimşek", "Lightning");
                case "sunday": return T("Pazar Ustası", "Sunday Master");
                case "week": return T("Mükemmel Hafta", "Perfect Week");
                case "early": return T("Erkenci", "Early Bird");
                case "night": return T("Gece Kuşu", "Night Owl");
                default: return id;
            }
        }

        public static string BadgeGoal(string id, long target)
        {
            switch (id)
            {
                case "first": return T("İlk günlük bulmacanı çöz", "Solve your first daily puzzle");
                case "streak3":
                case "streak7":
                case "streak14":
                case "streak30":
                case "streak100": return T($"{target} gün üst üste çöz", $"Solve {target} days in a row");
                case "solves10":
                case "solves50": return T($"{target} günlük bulmaca çöz", $"Solve {target} daily puzzles");
                case "firsttry": return T("Bir bulmacayı ilk denemede çöz", "Solve a puzzle on the first try");
                case "firsttry10": return T($"{target} bulmacayı ilk denemede çöz", $"Solve {target} puzzles on the first try");
                case "stars3": return T("Bir bulmacayı 3 yıldızla çöz", "Solve a puzzle with 3 stars");
                case "speed": return T("Bir bulmacayı 1 dakikadan kısa sürede çöz", "Solve a puzzle in under a minute");
                case "sunday": return T("Pazar bulmacasını çöz", "Solve a Sunday puzzle");
                case "week": return T("Pazartesiden pazara her günü çöz", "Solve every day, Monday to Sunday");
                case "early": return T("Sabah 8'den önce çöz", "Solve before 8 a.m.");
                case "night": return T("Gece 11'den sonra çöz", "Solve after 11 p.m.");
                default: return "";
            }
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
