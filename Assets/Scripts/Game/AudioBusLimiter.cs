using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Keeps the game's summed output under full scale. It sits on the AudioListener, where
    /// <c>OnAudioFilterRead</c> sees the whole mix — every effect and the music together.
    ///
    /// Each clip is mastered to -1 dBFS on its own (<see cref="SoundMaster"/>); what this catches
    /// is a good move, where the place thud, the clear and the combo reply land within 70 ms of
    /// each other and would otherwise clip hard on the device. The work is two multiplications
    /// a frame, on the audio thread, with nothing allocated.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioBusLimiter : MonoBehaviour
    {
        readonly SoundMaster.BusLimiter _limiter = new SoundMaster.BusLimiter();

        /// <summary>Puts the limiter on the scene's listener, adding a listener to the camera if the scene has none.</summary>
        public static void Install()
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                listener = cam.gameObject.AddComponent<AudioListener>();
            }

            if (listener.GetComponent<AudioBusLimiter>() == null)
                listener.gameObject.AddComponent<AudioBusLimiter>();
        }

        void Awake() => _limiter.Reset(AudioSettings.outputSampleRate);

        // The device can change the output format (headphones plugged in, a Bluetooth route).
        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnConfigurationChanged;

        void OnConfigurationChanged(bool deviceWasChanged) => _limiter.Reset(AudioSettings.outputSampleRate);

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0) return;

            for (int frame = 0; frame + channels <= data.Length; frame += channels)
            {
                // Stereo-linked: both channels take the same gain, so the image does not wander.
                float peak = 0f;
                for (int c = 0; c < channels; c++)
                {
                    float a = data[frame + c];
                    if (a < 0f) a = -a;
                    if (a > peak) peak = a;
                }

                float gain = _limiter.Gain(peak);
                if (gain >= 1f) continue;

                for (int c = 0; c < channels; c++)
                    data[frame + c] *= gain;
            }
        }
    }
}
