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

        const string LanguageKey = "blockpuzzle.language";
        static int _language = -1;

        /// <summary>
        /// The interface language. Until the player picks one it follows the phone: Turkish on a
        /// Turkish phone, English everywhere else.
        /// </summary>
        public static Language Language
        {
            get
            {
                if (_language < 0)
                {
                    int system = Application.systemLanguage == SystemLanguage.Turkish ? (int)Language.Turkish : (int)Language.English;
                    _language = PlayerPrefs.GetInt(LanguageKey, system);
                }
                return (Language)_language;
            }
            set
            {
                if (Language == value) return;
                _language = (int)value;
                PlayerPrefs.SetInt(LanguageKey, _language);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        const string ReminderKey = "blockpuzzle.reminder";
        const string ReminderAskedKey = "blockpuzzle.reminder.asked";

        /// <summary>
        /// The daily reminder. Off until the player says yes — asked once, after their first solve,
        /// when the offer means something — and switchable on the daily card afterwards.
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

        /// <summary>The one-time offer has been made, whatever the answer was.</summary>
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
