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

        // How big a canvas unit really is. The scaler matches width on every phone, so 1080 units
        // are the screen's width whatever its density — and about a third of Android phones are
        // 360dp wide or narrower. Sizes are therefore chosen for 1 unit = 1/3 dp: 3 units per sp.
        //
        // The first scale was set by eye on a desktop and sat well under the platform minimums
        // there: captions at 9sp, body at 13sp, the pause button at 35dp. On a phone it read as
        // a game that did not trust the player to see it.
        //
        // The second scale met Material's minimums — and that was the problem the player named
        // next: "everything is small next to the games I downloaded". Material sizes are for
        // apps read at arm's length for minutes; a casual game is glanced at between moves, and
        // the genre sets its controls a good step above them. Put side by side with published
        // puzzle games, the gap was not the headline sizes but everything under them: labels at
        // 15sp, icons at 20dp, power buttons whose glyph was a third of the button, cost dots
        // 5dp across. The lower steps grew most (captions +10%, labels +14%, icons +20-30%);
        // the score and the title, already large, grew least.

        /// <summary>Type scale. Ratios are roughly 1.2, so sizes read as clearly distinct steps.</summary>
        public const float Readout = 200f; // the live score, the biggest thing on screen — 67sp
        public const float Display = 184f; // app title — 61sp
        public const float Title = 92f;    // card headings — 31sp
        public const float Headline = 72f; // main button labels — 24sp
        public const float Body = 58f;     // secondary button labels — 19sp
        public const float Label = 50f;    // row labels — 17sp
        public const float Caption = 42f;  // 14sp — nothing the player reads goes smaller

        /// <summary>
        /// Display type is set tight. Labels are tracked out a little — they used to be tracked
        /// out a lot, which with the medium weight is the look of a settings app, not a game.
        /// </summary>
        public const float TrackingDisplay = -3f;
        public const float TrackingLabel = 3f;

        // ------------------------------------------------------------------ space

        public const float Space1 = 8f;
        public const float Space2 = 16f;
        public const float Space3 = 24f;
        public const float Space4 = 40f;
        public const float Space5 = 56f;
        public const float Space6 = 80f;
        public const float Space7 = 112f;

        /// <summary>Side gutter for full-width content.</summary>
        public const float Gutter = 48f;

        /// <summary>
        /// Side gutter for the board alone. The board is the game; the genre gives it almost the
        /// whole width, and every unit of gutter comes straight out of the cell size.
        /// </summary>
        public const float BoardGutter = 24f;

        /// <summary>Width of anything that spans the page between the gutters: modal cards, the play button.</summary>
        public const float ContentWidth = 1080f - Gutter * 2f;

        /// <summary>Minimum touch target: 48dp on a 360dp-wide phone. Nothing tappable is smaller.</summary>
        public const float TouchTarget = 144f;

        /// <summary>
        /// Square icon buttons in a page's corners — pause, settings, back. 56dp: the size the
        /// genre gives the one control that sits over the play field, a step over the minimum.
        /// </summary>
        public const float IconButton = 168f;

        /// <summary>The main action of a screen or card. 67dp.</summary>
        public const float ButtonLg = 204f;

        /// <summary>Every other full-width button. 59dp.</summary>
        public const float ButtonMd = 176f;

        /// <summary>Icons that sit beside text in a row. 24dp.</summary>
        public const float IconSm = 72f;

        /// <summary>Icons that carry a card or a tile on their own. 36dp.</summary>
        public const float IconMd = 108f;

        /// <summary>
        /// How much of an icon-only button its glyph fills. It was 44%, which left a pause mark
        /// the size of a caption floating in a big square.
        /// </summary>
        public const float GlyphFill = 0.54f;

        /// <summary>Padding between a card's edge and the content inside it.</summary>
        public const float CardPadding = 64f;

        // ------------------------------------------------------------------ sheets

        // Every modal is a sheet that rises from the bottom edge, where the thumb already is.
        // The first modals were cards centred on the page with a heading printed inside and a
        // full-width "close" button under their content: two rows of every card spent on chrome,
        // and the rows between them squeezed until a timer sat on the line above it. A sheet
        // closes by its corner button, a tap on the dimmed page, a swipe down or the back button,
        // so its whole height goes to what it holds.

        /// <summary>Top corners of a sheet. Its bottom corners sit below the screen edge.</summary>
        public const float SheetRadius = 72f;

        /// <summary>Space a sheet always leaves above itself, so the page behind still reads as there.</summary>
        public const float SheetTopGap = 64f;

        /// <summary>From a sheet's top edge to its content: the grabber and the title row.</summary>
        public const float SheetHeader = 232f;

        /// <summary>The title row's centre, from the sheet's top edge.</summary>
        public const float SheetTitleLine = 124f;

        /// <summary>The medallion a sheet wears over its top edge, half in, half out.</summary>
        public const float SheetHero = 176f;

        /// <summary>From a sheet's top edge to its content when it wears a medallion: the lower half of it and the title.</summary>
        public const float SheetHeroHeader = 262f;

        /// <summary>The title row's centre under a medallion.</summary>
        public const float SheetHeroTitleLine = 170f;

        /// <summary>The close button's centre, from the sheet's top edge — beside the medallion, not under it.</summary>
        public const float SheetCloseLine = 104f;

        /// <summary>Close button: the hit area is a full touch target, the disc inside it smaller.</summary>
        public const float CloseDisc = 104f;

        /// <summary>One row of a grouped list — a switch, a slider, a link.</summary>
        public const float RowHeight = 152f;

        /// <summary>Icon tile at the start of a row: a small coloured squircle with a white glyph.</summary>
        public const float RowIcon = 88f;

        /// <summary>Inner side padding of a grouped list.</summary>
        public const float GroupPadding = 36f;

        /// <summary>Between two groups.</summary>
        public const float GroupGap = 32f;

        /// <summary>A segmented control — the two halves of a sheet such as today / badges.</summary>
        public const float SegmentHeight = 124f;

        /// <summary>Switches: a phone's own proportion, a step larger.</summary>
        public const float SwitchWidth = 184f;
        public const float SwitchHeight = 108f;

        /// <summary>A progress track inside a row.</summary>
        public const float BarHeight = 16f;

        // ------------------------------------------------------------------ shape

        public const float RadiusSm = 24f;
        public const float RadiusMd = 42f;
        public const float RadiusLg = 58f;

        // ------------------------------------------------------------------ colour

        // Everything the player reads sits on a deep panel cut into the ground — the board, the
        // cards, the buttons — so white text keeps its contrast everywhere and the block colours
        // stay the loudest thing on screen.
        //
        // The ground, the surfaces and the accent belong to the chosen theme (Themes). They are
        // read live, so anything built after a theme change picks it up; AppController rebuilds
        // the interface when the player picks one. Colours that mean something in play — gold,
        // mint, the prism, crystals, ice, the line preview — are not themed.

        static Themes.Theme Mood => Themes.Current;

        /// <summary>Top of the ground, behind the score. Also the camera's clear colour.</summary>
        public static Color BgTop => Mood.BgTop;

        /// <summary>Bottom of the ground, behind the tray.</summary>
        public static Color BgBottom => Mood.BgBottom;

        /// <summary>A soft lift behind the board, so the ground is never a flat field.</summary>
        public static Color BgGlow => Mood.BgGlow;

        /// <summary>How strongly the glow shows. A dark ground needs far less light to read as lit.</summary>
        public static float GlowAlpha => Mood.GlowAlpha;

        /// <summary>Where the vignette settles, in the corners.</summary>
        public static Color BgVignette => Mood.BgVignette;

        /// <summary>The perspective grid drawn over the ground.</summary>
        public static Color BgGrid => new Color(1f, 1f, 1f, Mood.GridAlpha);

        public static Color Surface => Mood.Surface;
        public static Color SurfaceHigh => Mood.SurfaceHigh;

        /// <summary>Darker than the general surface so the block colours carry the contrast.</summary>
        public static Color BoardSurface => Mood.BoardSurface;

        // The project renders in Linear colour space, where a low-alpha white composites far
        // brighter than the sRGB arithmetic suggests — 7% white reads closer to 25%. Surfaces are
        // therefore given explicit opaque values instead of translucent white, so what is authored
        // is what ships. Only hairlines stay translucent, where the lift is wanted.

        /// <summary>Secondary buttons.</summary>
        public static Color SurfaceButton => Mood.SurfaceButton;

        /// <summary>Slider tracks and toggle backgrounds.</summary>
        public static Color SurfaceTrack => Mood.SurfaceTrack;

        /// <summary>Recessed areas: empty board cells, badges.</summary>
        public static Color SurfaceInset => Mood.SurfaceInset;

        /// <summary>The leader row in the scores table, the chosen theme's row.</summary>
        public static Color SurfaceLeader => Mood.SurfaceLeader;

        /// <summary>A sheet's own fill — the darkest of the raised surfaces, so what sits on it lifts.</summary>
        public static Color SurfaceSheet => Mood.Surface;

        /// <summary>A grouped list or a card on a sheet.</summary>
        public static Color SurfaceGroup => Mood.SurfaceHigh;

        /// <summary>
        /// Controls that sit on a group — a switch's off track, a slider's track, a segment's thumb.
        /// The track colour alone was one step off the group's and the off switch disappeared.
        /// </summary>
        public static Color SurfaceControl => Mood.SurfaceButton;

        /// <summary>
        /// Row icon tiles. Not themed: like gold and mint they name a thing, and a row is found by
        /// its colour before its word is read.
        /// </summary>
        public static readonly Color TintRose = Hex("#FF5A7E");
        public static readonly Color TintViolet = Hex("#9B6BFF");
        public static readonly Color TintBlue = Hex("#3AA9FF");
        public static readonly Color TintOrange = Hex("#FF7C4D");
        public static readonly Color TintTeal = Hex("#26C9C3");

        /// <summary>Second and third place, and an achievement's first two tiers. First is <see cref="Gold"/>.</summary>
        public static readonly Color Silver = Hex("#C3CEE6");
        public static readonly Color Bronze = Hex("#E39A62");

        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.10f);

        public static readonly Color TextPrimary = Color.white;

        /// <summary>
        /// Quiet text sitting directly on the ground rather than on a panel. Translucent white
        /// disappears on a bright ground — going darker than it is what reads as secondary. On
        /// the one dark ground (Neon) it is a light tint instead.
        /// </summary>
        public static Color TextOnGround => Mood.TextOnGround;

        public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.70f);
        public static readonly Color TextTertiary = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>The primary buttons' gradient, top and bottom.</summary>
        public static Color AccentA => Mood.AccentA;
        public static Color AccentB => Mood.AccentB;

        public static readonly Color Gold = Hex("#FFC24B");
        public static readonly Color Mint = Hex("#38D39F");

        /// <summary>Text that sits on top of a light or vivid fill.</summary>
        public static readonly Color OnAccent = Color.white;

        /// <summary>
        /// Behind a modal. Tinted with the ground rather than black: on a bright page a black
        /// scrim reads as the lights going out, which is far more drama than pausing deserves.
        /// </summary>
        public static Color Scrim => Mood.Scrim.WithAlpha(0.72f);

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

        /// <summary>
        /// The block colours of the chosen theme (<see cref="Themes"/>). Every theme has the same
        /// number of colours, so a saved run keeps its colour indices whatever the theme.
        /// </summary>
        public static Color[] Blocks => Mood.Blocks;

        /// <summary>How many colours the rules deal. Fixed, independent of the theme.</summary>
        public const int PaletteSize = 7;

        /// <summary>Crystals: a pale cyan, cooler than any block colour, so they never read as a block.</summary>
        public static readonly Color Crystal = Hex("#E8FBFF");

        /// <summary>Ice sits over a block as a cold frost, blue enough never to read as white paint.</summary>
        public static readonly Color Ice = Hex("#C9E9FF");

        /// <summary>Stone: a neutral grey no palette uses, with a darker frost for texture. Not themed.</summary>
        public static readonly Color Stone = Hex("#8C8FA1");
        public static readonly Color StoneFrost = Hex("#3B3E4F").WithAlpha(0.55f);

        /// <summary>The prism charge meter and everything that belongs to the powers.</summary>
        public static readonly Color Prism = Hex("#7FE7FF");

        /// <summary>
        /// A glow tile on the floor of the board. Orchid: warmer than ice and crystal, cooler than
        /// gold, and drawn as a rim and a diamond rather than a fill, so no block colour hides it.
        /// </summary>
        public static readonly Color TileGlow = Hex("#F59BFF");

        /// <summary>Shade: a dusk violet, darker than any block and lit from inside by its own swirl.</summary>
        public static readonly Color Shade = Hex("#B08CFF");

        /// <summary>A timer block's clock face: the rose of a three-line clear, the colour of "soon".</summary>
        public static readonly Color Timer = Hex("#FF4D7E");

        /// <summary>
        /// The adventure's ten worlds, one colour each: the road through a world, its finished
        /// levels, its banner and the light of the room while it is on screen. Not themed: like the
        /// crystal and the ice they name a place, and a player finds their way back by colour.
        /// </summary>
        public static readonly Color[] WorldColors =
        {
            Hex("#27A9F0"), // crystal shore
            Hex("#D65BC9"), // glow fields
            Hex("#7F86D8"), // stone pass
            Hex("#8456D8"), // shade woods
            Hex("#F2803A"), // colour bazaar
            Hex("#D69A22"), // clock tower
            Hex("#3E9BE0"), // ice palace
            Hex("#5163E6"), // storm peak
            Hex("#1FAE93"), // star sea
            Hex("#F0457C"), // prism crown
        };

        public static Color WorldColor(int world) => WorldColors[Mathf.Clamp(world, 0, WorldColors.Length - 1)];

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
        public static readonly Elevation E1 = new Elevation(6f, 40f, 0.26f);

        /// <summary>Buttons and raised surfaces.</summary>
        public static readonly Elevation E2 = new Elevation(10f, 60f, 0.30f);

        /// <summary>Modals lifted off the page.</summary>
        public static readonly Elevation E3 = new Elevation(18f, 96f, 0.42f);

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
