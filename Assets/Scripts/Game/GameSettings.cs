using System;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Player-facing options, persisted immediately on change so a force-quit never loses them.
    /// Anything that reacts to a change subscribes to <see cref="Changed"/>.
    /// </summary>
    public static class GameSettings
    {
        const string MusicKey = "blockpuzzle.volume.music";
        const string SfxKey = "blockpuzzle.volume.sfx";
        const string HapticsKey = "blockpuzzle.haptics";
        const string MutedKey = "blockpuzzle.muted";

        static float _music = -1f;
        static float _sfx = -1f;
        static int _haptics = -1;
        static int _muted = -1;

        public static event Action Changed;

        public static float MusicVolume
        {
            get
            {
                if (_music < 0f) _music = PlayerPrefs.GetFloat(MusicKey, SoundMaster.DefaultMusicVolume);
                return _music;
            }
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(MusicVolume, v)) return;

                _music = v;
                PlayerPrefs.SetFloat(MusicKey, v);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get
            {
                if (_sfx < 0f) _sfx = PlayerPrefs.GetFloat(SfxKey, SoundMaster.DefaultEffectsVolume);
                return _sfx;
            }
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(SfxVolume, v)) return;

                _sfx = v;
                PlayerPrefs.SetFloat(SfxKey, v);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Master mute. Kept separate from the volume sliders so silencing the game and returning
        /// from it does not cost the player the levels they had set.
        /// </summary>
        public static bool Muted
        {
            get
            {
                if (_muted < 0) _muted = PlayerPrefs.GetInt(MutedKey, 0);
                return _muted != 0;
            }
            set
            {
                int v = value ? 1 : 0;
                if (_muted == v) return;

                _muted = v;
                PlayerPrefs.SetInt(MutedKey, v);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Effective music level after the master mute.</summary>
        public static float EffectiveMusicVolume => Muted ? 0f : MusicVolume;

        /// <summary>Effective effects level after the master mute.</summary>
        public static float EffectiveSfxVolume => Muted ? 0f : SfxVolume;

        // The language used to be stored as a number (0 Turkish, 1 English); since the languages
        // became files it is their code. The old number is read once, so a choice made before the
        // change survives it.
        const string LanguageKey = "blockpuzzle.language";
        const string LanguageCodeKey = "blockpuzzle.language.code";
        static string _language;

        /// <summary>
        /// The interface language, as the code of its file in Resources/Lang ("tr", "en", …). Until
        /// the player picks one it follows the phone, and English where there is no file for it.
        /// </summary>
        public static string Language
        {
            get
            {
                if (_language == null)
                {
                    if (PlayerPrefs.HasKey(LanguageCodeKey)) _language = PlayerPrefs.GetString(LanguageCodeKey);
                    else if (PlayerPrefs.HasKey(LanguageKey)) _language = PlayerPrefs.GetInt(LanguageKey) == 0 ? "tr" : "en";
                    else _language = Str.SystemLanguage();

                    // A language whose file has since been removed.
                    if (!Str.Has(_language)) _language = Str.SystemLanguage();
                }
                return _language;
            }
            set
            {
                if (Language == value || !Str.Has(value)) return;
                _language = value;
                PlayerPrefs.SetString(LanguageCodeKey, _language);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        const string ReminderKey = "blockpuzzle.reminder";
        const string ReminderAskedKey = "blockpuzzle.reminder.asked";

        /// <summary>
        /// The daily reminder. Set by the system's own permission dialog, asked once on the first
        /// launch (<see cref="AppController"/>), and switchable in settings afterwards.
        /// </summary>
        public static bool Reminder
        {
            get => PlayerPrefs.GetInt(ReminderKey, 0) != 0;
            set
            {
                if (Reminder == value) return;
                PlayerPrefs.SetInt(ReminderKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The first-launch permission has been asked, whatever the answer was.</summary>
        public static bool ReminderAsked
        {
            get => PlayerPrefs.GetInt(ReminderAskedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(ReminderAskedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool Haptics
        {
            get
            {
                if (_haptics < 0) _haptics = PlayerPrefs.GetInt(HapticsKey, 1);
                return _haptics != 0;
            }
            set
            {
                int v = value ? 1 : 0;
                if (_haptics == v) return;

                _haptics = v;
                PlayerPrefs.SetInt(HapticsKey, v);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
