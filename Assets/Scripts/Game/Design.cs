using TMPro;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The design system. Every size, colour and radius in the game comes from here rather than
    /// being typed at the call site, which is what keeps the screens consistent with each other.
    /// Values are in the 1080x1920 canvas reference space.
    /// </summary>
    public static class Design
    {
        // ------------------------------------------------------------------ type

        /// <summary>Type scale. Ratios are roughly 1.3, so sizes read as clearly distinct steps.</summary>
        public const float Readout = 156f; // the live score, the biggest thing on screen
        public const float Display = 118f; // app title
        public const float Title = 64f;    // card headings
        public const float Headline = 50f;
        public const float Body = 40f;
        public const float Label = 34f;
        public const float Caption = 28f;

        /// <summary>Display type is set tight; small labels are tracked out to stay legible.</summary>
        public const float TrackingDisplay = -3f;
        public const float TrackingLabel = 8f;

        // ------------------------------------------------------------------ space

        public const float Space1 = 8f;
        public const float Space2 = 16f;
        public const float Space3 = 24f;
        public const float Space4 = 40f;
        public const float Space5 = 56f;
        public const float Space6 = 80f;
        public const float Space7 = 112f;

        /// <summary>Side gutter for full-width content.</summary>
        public const float Gutter = 56f;

        /// <summary>Minimum comfortable touch target.</summary>
        public const float TouchTarget = 120f;

        // ------------------------------------------------------------------ shape

        public const float RadiusSm = 20f;
        public const float RadiusMd = 34f;
        public const float RadiusLg = 48f;

        // ------------------------------------------------------------------ colour

        public static readonly Color BgBase = Hex("#08080F");
        public static readonly Color BgPoolA = Hex("#2E1F78");
        public static readonly Color BgPoolB = Hex("#0A3C4C");
        public static readonly Color BgPoolC = Hex("#3E1550");

        public static readonly Color Surface = Hex("#14141F");
        public static readonly Color SurfaceHigh = Hex("#1E1E2C");

        /// <summary>Darker than the general surface so the block colours carry the contrast.</summary>
        public static readonly Color BoardSurface = Hex("#0C0C14");

        // The project renders in Linear colour space, where a low-alpha white over a dark ground
        // composites far brighter than the sRGB arithmetic suggests — 7% white reads closer to 25%.
        // Surfaces are therefore given explicit opaque values instead of translucent white, so what
        // is authored is what ships. Only hairlines stay translucent, where the lift is wanted.

        /// <summary>Secondary buttons.</summary>
        public static readonly Color SurfaceButton = Hex("#23232F");

        /// <summary>Slider tracks and toggle backgrounds.</summary>
        public static readonly Color SurfaceTrack = Hex("#22222E");

        /// <summary>Recessed areas: empty board cells, badges.</summary>
        public static readonly Color SurfaceInset = Hex("#191922");


        /// <summary>The leader row in the scores table.</summary>
        public static readonly Color SurfaceLeader = Hex("#332918");

        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.07f);

        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.62f);
        public static readonly Color TextTertiary = new Color(1f, 1f, 1f, 0.36f);

        public static readonly Color AccentA = Hex("#7B5CFF");
        public static readonly Color AccentB = Hex("#4B3BE0");
        public static readonly Color Gold = Hex("#FFC24B");
        public static readonly Color Mint = Hex("#38D39F");

        /// <summary>Text that sits on top of a light or vivid fill.</summary>
        public static readonly Color OnAccent = Color.white;

        /// <summary>
        /// Colour of the pre-clear preview, escalating with how many lines a drop would take.
        /// The colour alone tells the player it is a bigger move, before they read any number.
        /// </summary>
        public static Color PreviewTint(int lines)
        {
            if (lines >= 3) return Hex("#FF4D7E");
            if (lines == 2) return Gold;
            return Mint;
        }

        public static readonly Color[] Blocks =
        {
            Hex("#FF5A7E"), // rose
            Hex("#FFB13C"), // amber
            Hex("#38D39F"), // mint
            Hex("#3AA9FF"), // azure
            Hex("#9B6BFF"), // violet
            Hex("#FF7C4D"), // coral
            Hex("#26C9C3")  // teal
        };

        // ------------------------------------------------------------------ elevation

        public readonly struct Elevation
        {
            public readonly float OffsetY;
            public readonly float Spread;
            public readonly float Alpha;

            public Elevation(float offsetY, float spread, float alpha)
            {
                OffsetY = offsetY;
                Spread = spread;
                Alpha = alpha;
            }
        }

        // Offsets stay small relative to spread. A large offset pushes the shadow's solid core out
        // from under the element, which reads as a hard lip rather than a cast shadow.

        /// <summary>Resting cards.</summary>
        public static readonly Elevation E1 = new Elevation(6f, 40f, 0.32f);

        /// <summary>Buttons and raised surfaces.</summary>
        public static readonly Elevation E2 = new Elevation(10f, 60f, 0.38f);

        /// <summary>Modals lifted off the page.</summary>
        public static readonly Elevation E3 = new Elevation(18f, 96f, 0.5f);

        // ------------------------------------------------------------------ fonts

        static TMP_FontAsset _display;
        static TMP_FontAsset _medium;
        static TMP_FontAsset _body;

        /// <summary>Poppins ExtraBold. Headlines, scores, buttons.</summary>
        public static TMP_FontAsset FontDisplay
        {
            get { if (_display == null) _display = Load("Poppins-ExtraBold SDF"); return _display; }
        }

        /// <summary>Poppins SemiBold. Labels and secondary emphasis.</summary>
        public static TMP_FontAsset FontMedium
        {
            get { if (_medium == null) _medium = Load("Poppins-SemiBold SDF"); return _medium; }
        }

        /// <summary>Poppins Regular. Body copy.</summary>
        public static TMP_FontAsset FontBody
        {
            get { if (_body == null) _body = Load("Poppins-Regular SDF"); return _body; }
        }

        static TMP_FontAsset Load(string name)
        {
            var asset = Resources.Load<TMP_FontAsset>("Fonts/" + name);
            if (asset != null) return asset;

            Debug.LogWarning($"[Design] Font 'Fonts/{name}' not found in Resources; falling back to the TMP default.");
            return TMP_Settings.defaultFontAsset;
        }

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color)) return color;
            return Color.magenta;
        }

        /// <summary>Same colour at a different alpha. Saves a lot of struct copying at call sites.</summary>
        public static Color WithAlpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
