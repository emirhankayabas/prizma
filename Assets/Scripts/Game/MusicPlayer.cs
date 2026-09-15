using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Plays the calm looping background track — synthesised by <see cref="SoundSynth"/>, no audio
    /// files in the project — and follows the music setting.
    ///
    /// The loop is built and mastered off the main thread and starts as soon as it is ready, a
    /// fraction of a second after launch. Sixteen seconds of voices plus the mastering pass is far
    /// too much work to freeze the first frame for.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        AudioSource _source;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            ApplyVolume();
            StartCoroutine(BuildAndPlay());
        }

        IEnumerator BuildAndPlay()
        {
            float[] data = null;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var loop = SoundSynth.MusicLoop();
                    SoundMaster.Master(loop, SoundSynth.MusicSampleRate, SoundMaster.TargetFor("music"));
                    data = loop;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    data = new float[0];
                }
            });

            while (data == null) yield return null;
            if (data.Length == 0) yield break;

            var clip = AudioClip.Create("music_loop", data.Length, 1, SoundSynth.MusicSampleRate, false);
            clip.SetData(data, 0);
            _source.clip = clip;
            _source.Play();
        }

        // Tracking the setting directly means volume stays correct no matter who changed it —
        // the settings slider, a restored preference, or code.
        void OnEnable() => GameSettings.Changed += ApplyVolume;
        void OnDisable() => GameSettings.Changed -= ApplyVolume;

        public void ApplyVolume()
        {
            if (_source == null) return;

            // The loop is mastered to sit under the effects on its own, so the slider is the
            // whole story. It used to be capped at 45% on top of an unmastered loop.
            _source.volume = GameSettings.EffectiveMusicVolume;
            _source.mute = GameSettings.EffectiveMusicVolume <= 0.001f;
        }
    }
}
