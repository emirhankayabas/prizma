using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Plays the synthesised sound set, so the project ships with audio and no files.
    ///
    /// The recipes live in <see cref="SoundSynth"/>: gentle attacks, exponential decays and a
    /// one-pole low-pass on every clip, on a C major pentatonic where no two notes can clash.
    /// Each clip then goes through <see cref="SoundMaster"/>, which sets how loud it is — before
    /// that step the whole set sat 5 to 24 dB under full scale and "effects at 100%" was quiet
    /// next to any published game on the same phone.
    /// </summary>
    public sealed class AudioKit : MonoBehaviour
    {
        AudioSource _source;

        AudioClip _pickup;
        AudioClip _place;
        AudioClip _invalid;
        AudioClip[] _clear;
        AudioClip _gameOver;
        AudioClip _click;
        AudioClip _fanfare;
        AudioClip[] _combo;

        AudioClip _gem;
        AudioClip _ice;
        AudioClip _rotate;
        AudioClip _reroll;
        AudioClip _bomb;
        AudioClip _charge;
        AudioClip _prism;
        AudioClip _stuck;
        AudioClip[] _stars;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _clear = new AudioClip[SoundSynth.ClearSteps];
            _combo = new AudioClip[SoundSynth.ComboLadder.Length];
            _stars = new AudioClip[SoundSynth.StarCount];

            AudioBusLimiter.Install();

            // The handful of sounds a player can reach before anything else could finish: a tap
            // on a menu button, and the pickup and place of the very first piece. Short enough to
            // build and master here — a few milliseconds together.
            //
            // Everything else is built off the main thread while the menu is already on screen.
            // Synthesising the whole set in Awake cost nearly two hundred milliseconds on a
            // desktop, so several times that on a phone, and every one of those milliseconds is a
            // frozen frame at launch. <see cref="Play"/> ignores a clip that is not ready yet.
            foreach (var early in SoundSynth.BuildEarly())
                Assign(early.Key, Clip(early.Key, Mastered(early.Key, early.Value)));

            StartCoroutine(BuildRest());
        }

        static float[] Mastered(string name, float[] data)
        {
            SoundMaster.Master(data, SoundSynth.SampleRate, SoundMaster.TargetFor(name));
            return data;
        }

        IEnumerator BuildRest()
        {
            List<KeyValuePair<string, float[]>> built = null;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var rest = SoundSynth.BuildRest();
                    foreach (var clip in rest) Mastered(clip.Key, clip.Value);
                    built = rest;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    built = new List<KeyValuePair<string, float[]>>();
                }
            });

            while (built == null) yield return null;

            // A handful per frame: turning thirty buffers into clips at once is a visible hitch.
            for (int i = 0; i < built.Count; i++)
            {
                Assign(built[i].Key, Clip(built[i].Key, built[i].Value));
                if (i % 6 == 5) yield return null;
            }
        }

        void Assign(string name, AudioClip clip)
        {
            switch (name)
            {
                case "pickup": _pickup = clip; return;
                case "place": _place = clip; return;
                case "invalid": _invalid = clip; return;
                case "click": _click = clip; return;
                case "gameover": _gameOver = clip; return;
                case "fanfare": _fanfare = clip; return;
                case "gem": _gem = clip; return;
                case "ice": _ice = clip; return;
                case "rotate": _rotate = clip; return;
                case "reroll": _reroll = clip; return;
                case "bomb": _bomb = clip; return;
                case "charge": _charge = clip; return;
                case "prism": _prism = clip; return;
                case "stuck": _stuck = clip; return;
            }

            if (name.StartsWith("clear", StringComparison.Ordinal)) _clear[int.Parse(name.Substring(5))] = clip;
            else if (name.StartsWith("combo", StringComparison.Ordinal)) _combo[int.Parse(name.Substring(5))] = clip;
            else if (name.StartsWith("star", StringComparison.Ordinal)) _stars[int.Parse(name.Substring(4))] = clip;
        }

        public void PlayGem() => Play(_gem);
        public void PlayIce() => Play(_ice);
        public void PlayRotate() => Play(_rotate);
        public void PlayReroll() => Play(_reroll);
        public void PlayBomb() => Play(_bomb);
        public void PlayCharge() => Play(_charge);
        public void PlayStuck() => Play(_stuck);
        public void PlayStar(int index) => Play(_stars[Mathf.Clamp(index, 0, _stars.Length - 1)]);

        public void PlayPickup() => Play(_pickup);
        public void PlayPlace() => Play(_place);
        public void PlayInvalid() => Play(_invalid);
        public void PlayGameOver() => Play(_gameOver);
        public void PlayClick() => Play(_click);
        public void PlayFanfare() => Play(_fanfare);

        /// <summary>
        /// The whole of a clear in one call. It used to take four: the clear, the combo, the
        /// single-colour line and the fanfare each fired on their own, in the same frame, and a
        /// good move simply stacked them on top of each other. Now they arrive in order, a few
        /// hundredths of a second apart, so a big clear is heard as a phrase rather than a pile.
        ///
        /// <paramref name="lines"/> chooses how far the bell climbs — how much was done in this
        /// one move. <paramref name="comboStreak"/> answers it a rung higher for every
        /// consecutive clearing move, which is the part the player could not hear before.
        /// </summary>
        public void PlayClear(int lines, int comboStreak, int monoLines, bool perfectClear)
        {
            if (lines <= 0) return;

            Play(_clear[Mathf.Clamp(lines, 1, _clear.Length) - 1]);

            // Quieter than the clear it answers: a reply, not a second announcement.
            if (comboStreak > 1)
                PlayAfter(_combo[Mathf.Clamp(comboStreak - 1, 0, _combo.Length - 1)], 0.07f, 0.8f);

            if (monoLines > 0) PlayAfter(_prism, 0.13f, 0.9f);
            if (perfectClear) PlayAfter(_fanfare, 0.22f, 1f);
        }

        void PlayAfter(AudioClip clip, float seconds, float scale)
        {
            if (clip == null || !isActiveAndEnabled) return;
            StartCoroutine(AfterRoutine(clip, seconds, scale));
        }

        IEnumerator AfterRoutine(AudioClip clip, float seconds, float scale)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Play(clip, scale);
        }

        void Play(AudioClip clip, float scale = 1f)
        {
            if (clip == null || _source == null) return;

            float volume = GameSettings.EffectiveSfxVolume;
            if (volume <= 0.001f) return;

            _source.PlayOneShot(clip, volume * scale);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create("sfx_" + name, data.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
