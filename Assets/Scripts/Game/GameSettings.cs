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
                if (_music < 0f) _music = PlayerPrefs.GetFloat(MusicKey, 0.55f);
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
                if (_sfx < 0f) _sfx = PlayerPrefs.GetFloat(SfxKey, 0.85f);
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
