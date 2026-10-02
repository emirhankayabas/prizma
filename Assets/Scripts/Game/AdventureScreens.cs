using System;
using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The adventure as a road: a hundred levels winding up the screen through ten worlds, the way
    /// the saga games draw theirs — in this game's own language, which is light.
    ///
    /// The road is a beam. Where the player has been it is lit: a bright lane with a white core and
    /// a soft glow of the world's colour around it. Where they have not, it is only a trail of
    /// quiet dots. (The first version laid a dark recessed bed under the lane, and it read as a
    /// heavy black border around everything — the one thing on the page the eye could not avoid.)
    ///
    /// A finished level is a lit button in its world's colour on a white rim, with its stars; the
    /// next is a larger white button with the number in the world's colour, a halo and the prism
    /// marker over it; a locked one is a small frosted disc. Each world is its own field of colour
    /// with crystals drifting behind the road at a slower pace than it scrolls, a card at its
    /// entrance and a chest at its end, and the room's light turns to it while it is on screen.
    ///
    /// Only the worlds near the viewport are switched on; the whole road is a few thousand images.
    /// </summary>
    public sealed class LevelSelectScreen : AppScreen
    {
        const float NodeSize = 136f;
        const float LockedSize = 112f;
        const float CurrentSize = 176f;
        const float NodeStep = 230f;
        const float BannerSpace = 380f;
        const float ChestStep = 250f;
        const float BottomRoom = 140f;
        const float TopRoom = 600f;
        const float Amplitude = 270f;
        const float LaneWidth = 24f;
        const float CoreWidth = 8f;
        const float SegmentStep = 30f;
        const float DotStep = 44f;
        const float DotSize = 14f;
        const float GlowStep = 58f;
        const float GlowSize = 130f;
        const float StarSize = 42f;
        const float Parallax = 0.3f;
        const float HeaderHeight = Design.Space2 * 2f + Design.IconButton;

        /// <summary>A place on the road: a level, or a world's chest.</summary>
        sealed class Stop
        {
            public int Level;
            public int World;
            public bool Chest;
            public Vector2 Position;

            public RectTransform Rect;
            public RectTransform Body;
            public Image Shadow;
            public Image Disc;
            public Image Rim;
            public Image Halo;
            public Image Icon;
            public TextMeshProUGUI Label;
            public Image[] Stars;

            /// <summary>The hard badge on the right shoulder and the "new" picture on the left; null when none.</summary>
            public RectTransform HardBadge;
            public RectTransform NewBadge;

            /// <summary>The road leading up to this stop: lit (lane, core, glow) and unlit (dots).</summary>
            public readonly List<Image> Lane = new List<Image>();
            public readonly List<Image> Core = new List<Image>();
            public readonly List<Image> Glow = new List<Image>();
            public readonly List<Image> Dots = new List<Image>();
        }

        const int Worlds = LevelGenerator.WorldCount;

        readonly List<Stop> _stops = new List<Stop>();
        readonly RectTransform[] _worlds = new RectTransform[Worlds];
        readonly RectTransform[] _decor = new RectTransform[Worlds];
        readonly float[] _worldBottom = new float[Worlds];
        readonly float[] _worldTop = new float[Worlds];

        readonly Image[] _cardFill = new Image[Worlds];
        readonly Image[] _cardEdge = new Image[Worlds];
        readonly Image[] _emblem = new Image[Worlds];
        readonly Image[] _emblemGlyph = new Image[Worlds];
        readonly TextMeshProUGUI[] _cardCaption = new TextMeshProUGUI[Worlds];
        readonly TextMeshProUGUI[] _cardName = new TextMeshProUGUI[Worlds];
        readonly TextMeshProUGUI[] _cardStars = new TextMeshProUGUI[Worlds];
        readonly Image[] _cardStar = new Image[Worlds];
        readonly Image[] _cardBar = new Image[Worlds];

        MapScroll _scroll;
        RectTransform _content;
        RectTransform _top;
        RectTransform _marker;
        Image _markerRing;
        Vector2 _markerHome;
        float _contentHeight;
        TextMeshProUGUI _starTotal;
        UiButton _jump;
        int _lightWorld = -1;
        bool _dirty;
        bool _revealing;
        bool _built;
        Stop _pressed;
        int _afterWin;

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            // The map itself, under everything. The road is built on the first visit, not at
            // launch — it is a few thousand images, and most launches go straight to a classic run.
            _scroll = MapScroll.Create(Root, "Map");
            UiBuilder.Stretch(_scroll.Rect);
            _scroll.Tapped += OnMapTap;
            _scroll.Pressed += OnMapPress;
            _scroll.Released += OnMapRelease;
            _content = _scroll.Content;

            BuildHeader();
        }

        void BuildMap()
        {
            _built = true;
            LayOutStops();
            _scroll.SetContentHeight(_contentHeight, App.PageHeight);
            _scroll.Moved += OnScrolled;

            var layers = new RectTransform[Worlds, 7];
            for (int w = 0; w < Worlds; w++)
            {
                var world = Layer(_content, "World" + (w + 1));
                _worlds[w] = world;
                layers[w, 0] = Layer(world, "Field");
                layers[w, 1] = _decor[w] = Layer(world, "Decor");
                layers[w, 2] = Layer(world, "Glow");
                layers[w, 3] = Layer(world, "Dots");
                layers[w, 4] = Layer(world, "Lane");
                layers[w, 5] = Layer(world, "Core");
                layers[w, 6] = Layer(world, "Stops");
            }

            for (int w = 0; w < Worlds; w++)
            {
                BuildField(layers[w, 0], w);
                BuildDecor(layers[w, 1], w);
            }

            BuildRoad(layers);
            foreach (var stop in _stops) BuildStop(layers[stop.World, 6], stop);
            for (int w = 0; w < Worlds; w++) BuildCard(layers[w, 6], w);

            _top = Layer(_content, "Top");
            BuildFinale();
            BuildMarker();
        }

        static RectTransform Layer(RectTransform parent, string name)
        {
            var layer = UiBuilder.Node(parent, name);
            layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0f);
            layer.pivot = new Vector2(0.5f, 0f);
            layer.anchoredPosition = Vector2.zero;
            return layer;
        }

        static Image Place(RectTransform parent, string name, Sprite sprite, Color colour, Vector2 at, float size)
        {
            var image = UiBuilder.Image(parent, name, sprite, colour);
            image.type = Image.Type.Simple;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = at;
            return image;
        }

        static Material _shadowed;

        /// <summary>
        /// One text shadow shared by every label on the map. TMP gives each label its own material
        /// instance when its shadow is set, and a hundred of them would be a hundred draw calls.
        /// </summary>
        static void Shadowed(TextMeshProUGUI label)
        {
            if (_shadowed == null)
            {
                UiBuilder.TextShadow(label, 0.45f, -0.35f, 0.45f);
                _shadowed = label.fontMaterial;
                return;
            }

            label.fontSharedMaterial = _shadowed;
        }

        /// <summary>
        /// Where every stop sits. The road swings from side to side on a slow wave, one level a
        /// step up the screen; a world starts with room for its card and ends on its chest.
        /// </summary>
        void LayOutStops()
        {
            float y = BottomRoom;
            int step = 0;

            for (int w = 0; w < Worlds; w++)
            {
                _worldBottom[w] = y;
                y += BannerSpace;

                for (int i = 0; i < LevelGenerator.WorldSize; i++)
                {
                    int level = w * LevelGenerator.WorldSize + i + 1;
                    _stops.Add(new Stop { Level = level, World = w, Position = new Vector2(RoadX(step), y) });
                    y += NodeStep;
                    step++;
                }

                // The chest sits where the road turns back towards the middle.
                _stops.Add(new Stop { Level = -1, World = w, Chest = true, Position = new Vector2(RoadX(step) * 0.3f, y + ChestStep - NodeStep) });
                y += ChestStep + 30f;
                step++;
                _worldTop[w] = y;
            }

            _contentHeight = y + TopRoom;
        }

        static float RoadX(int step) => Amplitude * Mathf.Sin(step * 0.78f + 0.4f);

        Vector2 StopPoint(int index)
        {
            if (index < 0) return _stops[0].Position + new Vector2(0f, -BottomRoom - 60f);
            if (index >= _stops.Count) return _stops[_stops.Count - 1].Position + new Vector2(0f, TopRoom * 0.4f);
            return _stops[index].Position;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Lays every stretch of road twice: lit (a glow, a lane, a white core) and unlit (a trail
        /// of dots). Painting switches between them; the reveal lights one piece at a time.
        /// </summary>
        void BuildRoad(RectTransform[,] layers)
        {
            var samples = new List<Vector2>(128);

            // The road starts at the first level; below it sits the first world's card.
            for (int k = 1; k < _stops.Count; k++)
            {
                var stop = _stops[k];
                Vector2 p0 = StopPoint(k - 2), p1 = StopPoint(k - 1), p2 = StopPoint(k), p3 = StopPoint(k + 1);

                // Dense samples along the curve, then walked by distance, so pieces and dots are
                // evenly spaced however the curve bends.
                samples.Clear();
                for (int i = 0; i <= 96; i++) samples.Add(CatmullRom(p0, p1, p2, p3, i / 96f));

                float length = 0f;
                for (int i = 1; i < samples.Count; i++) length += Vector2.Distance(samples[i - 1], samples[i]);

                // The first stretch starts off the bottom of the map, the rest at the previous stop.
                // A stop's own disc covers the last part of the road into it, so nothing is drawn there.
                float start = NodeSize * 0.42f;
                float end = length - NodeSize * 0.42f;

                var previous = PointAt(samples, start);
                for (float d = start + SegmentStep; d <= end + 0.01f; d += SegmentStep)
                {
                    var point = PointAt(samples, Mathf.Min(d, end));
                    stop.Lane.Add(Piece(layers[stop.World, 4], previous, point, LaneWidth));
                    stop.Core.Add(Piece(layers[stop.World, 5], previous, point, CoreWidth));
                    previous = point;
                }

                for (float d = start + DotStep * 0.5f; d < end; d += DotStep)
                    stop.Dots.Add(Place(layers[stop.World, 3], "Dot", Art.Disc, Color.white, PointAt(samples, d), DotSize));

                for (float d = start + GlowStep * 0.5f; d < end; d += GlowStep)
                    stop.Glow.Add(Place(layers[stop.World, 2], "Glow", Art.SoftCircle, Color.white, PointAt(samples, d), GlowSize));
            }
        }

        static Vector2 PointAt(List<Vector2> samples, float distance)
        {
            for (int i = 1; i < samples.Count; i++)
            {
                float step = Vector2.Distance(samples[i - 1], samples[i]);
                if (distance <= step) return Vector2.Lerp(samples[i - 1], samples[i], step <= 0f ? 0f : distance / step);
                distance -= step;
            }

            return samples[samples.Count - 1];
        }

        static Image Piece(RectTransform parent, Vector2 a, Vector2 b, float width)
        {
            var delta = b - a;
            var image = UiBuilder.Image(parent, "Road", Art.Panel(width * 0.5f), Color.white);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(delta.magnitude + width, width);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return image;
        }

        void BuildStop(RectTransform parent, Stop stop)
        {
            var rect = UiBuilder.Node(parent, stop.Chest ? $"Chest{stop.World + 1}" : $"Level{stop.Level}");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = stop.Position;
            rect.sizeDelta = new Vector2(CurrentSize, CurrentSize);
            stop.Rect = rect;

            stop.Halo = UiBuilder.Image(rect, "Halo", Art.Pool, Color.white);
            stop.Halo.type = Image.Type.Simple;
            stop.Halo.rectTransform.sizeDelta = new Vector2(CurrentSize * 2.6f, CurrentSize * 2.6f);
            stop.Halo.gameObject.SetActive(false);

            var body = UiBuilder.Node(rect, "Body");
            body.sizeDelta = rect.sizeDelta;
            stop.Body = body;

            stop.Shadow = UiBuilder.Image(body, "Shadow", Art.DiscShadow(NodeSize, 30f), Color.black.WithAlpha(0.3f));
            stop.Shadow.type = Image.Type.Simple;

            stop.Rim = UiBuilder.Image(body, "Rim", Art.Disc, Color.white);
            stop.Rim.type = Image.Type.Simple;

            stop.Disc = UiBuilder.Image(body, "Disc", Art.Node, Color.white);
            stop.Disc.type = Image.Type.Simple;

            if (stop.Chest)
            {
                stop.Icon = UiBuilder.Image(body, "Chest", Icons.Chest, Color.white);
                stop.Icon.type = Image.Type.Simple;
                stop.Icon.rectTransform.anchoredPosition = new Vector2(0f, 3f);
                return;
            }

            stop.Label = UiBuilder.Label(body, "Number", stop.Level.ToString(), Design.Headline, Color.white,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            stop.Label.rectTransform.sizeDelta = new Vector2(CurrentSize, CurrentSize * 0.6f);
            stop.Label.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            // Three stars in a shallow arc under the button.
            stop.Stars = new Image[3];
            for (int s = 0; s < 3; s++)
            {
                float size = s == 1 ? StarSize * 1.15f : StarSize;
                var star = UiBuilder.Image(body, "Star" + s, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.rectTransform.anchoredPosition = new Vector2((s - 1) * 44f, -NodeSize * 0.5f - (s == 1 ? 14f : 6f));
                stop.Stars[s] = star;
            }

            // Hard levels wear a badge; an intro level shows what it brings, as a picture.
            int hardness = LevelGenerator.HardnessOf(stop.Level);
            if (hardness > 0)
            {
                var badge = UiBuilder.Image(body, "Hard", Art.Disc, HardColour(hardness));
                badge.type = Image.Type.Simple;
                badge.rectTransform.sizeDelta = new Vector2(58f, 58f);
                stop.HardBadge = badge.rectTransform;
                var glyph = UiBuilder.Image(badge.rectTransform, "Glyph", hardness == 2 ? Icons.Crown : Icons.Bolt, Color.white);
                glyph.type = Image.Type.Simple;
                glyph.rectTransform.sizeDelta = new Vector2(36f, 36f);
            }

            var intro = IntroSprite(stop.Level);
            if (intro.sprite != null)
            {
                var badge = UiBuilder.Image(body, "New", Art.Disc, Color.white);
                badge.type = Image.Type.Simple;
                badge.rectTransform.sizeDelta = new Vector2(62f, 62f);
                stop.NewBadge = badge.rectTransform;
                var glyph = UiBuilder.Image(badge.rectTransform, "Glyph", intro.sprite, intro.colour);
                glyph.type = Image.Type.Simple;
                glyph.rectTransform.sizeDelta = new Vector2(40f, 40f);
            }
        }

        public static Color HardColour(int hardness) => hardness >= 2 ? Design.TintViolet : Design.PreviewTint(3);

        /// <summary>The picture of what an intro level brings onto the board.</summary>
        public static (Sprite sprite, Color colour) IntroSprite(int level)
        {
            switch (level)
            {
                case LevelGenerator.TilesFrom: return (Art.Tile, Design.TileGlow);
                case LevelGenerator.StoneFrom: return (Art.Block, Design.Stone);
                case LevelGenerator.ShadeFrom: return (Art.ShadeBlock, Design.Shade);
                case LevelGenerator.ColorsFrom: return (Art.Block, Design.TintOrange);
                case LevelGenerator.TimersFrom: return (Art.TimerRing, Design.Timer);
                case LevelGenerator.DoubleIceFrom: return (Art.Ice(2), Design.Ice);
                default: return (null, Color.white);
            }
        }

        /// <summary>A world's sign on its card: what it is about, drawn white.</summary>
        static Sprite WorldGlyph(int world)
        {
            switch (world)
            {
                case 1: return Art.Tile;
                case 2: return Art.Block;
                case 3: return Art.ShadeBlock;
                case 4: return Icons.Palette;
                case 5: return Icons.Clock;
                case 6: return Art.Ice(2);
                case 7: return Icons.Bolt;
                case 8: return Icons.Star;
                case 9: return Icons.Crown;
                default: return Icons.Gem;
            }
        }

        /// <summary>
        /// A world's card at its entrance: an emblem in its colour with its sign, its number and
        /// name, and how many of its thirty stars are won, as a bar. Tinted with the world, opaque.
        /// </summary>
        void BuildCard(RectTransform parent, int world)
        {
            const float width = 820f, height = 176f, radius = 58f;
            var rect = UiBuilder.Node(parent, "Card");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, _worldBottom[world] + BannerSpace * 0.44f);

            UiBuilder.Shadow(rect, "Shadow", rect.sizeDelta, radius, Design.E2);
            _cardFill[world] = UiBuilder.Panel(rect, "Fill", rect.sizeDelta, Color.white, radius);
            _cardEdge[world] = UiBuilder.Hairline(rect, "Edge", rect.sizeDelta, radius);

            const float emblem = 124f;
            float emblemX = -width * 0.5f + 26f + emblem * 0.5f;
            _emblem[world] = UiBuilder.Image(rect, "Emblem", Art.Node, Color.white);
            _emblem[world].type = Image.Type.Simple;
            _emblem[world].rectTransform.sizeDelta = new Vector2(emblem, emblem);
            _emblem[world].rectTransform.anchoredPosition = new Vector2(emblemX, 0f);
            _emblemGlyph[world] = UiBuilder.Image(_emblem[world].rectTransform, "Glyph", WorldGlyph(world), Color.white);
            _emblemGlyph[world].type = Image.Type.Simple;
            _emblemGlyph[world].rectTransform.sizeDelta = new Vector2(emblem * 0.5f, emblem * 0.5f);

            float left = emblemX + emblem * 0.5f + Design.Space3;
            float textWidth = width * 0.5f - 30f - left;

            _cardCaption[world] = UiBuilder.Label(rect, "Caption", Str.WorldN(world), Design.Caption, Color.white,
                Design.FontDisplay, TextAlignmentOptions.Left, Design.TrackingLabel);
            _cardCaption[world].rectTransform.sizeDelta = new Vector2(textWidth, 50f);
            _cardCaption[world].rectTransform.anchoredPosition = new Vector2(left + textWidth * 0.5f, 44f);

            _cardName[world] = UiBuilder.Label(rect, "Name", Str.Upper(Str.WorldName(world)), Design.Body, Color.white,
                Design.FontDisplay, TextAlignmentOptions.Left, Design.TrackingLabel * 0.3f);
            _cardName[world].rectTransform.sizeDelta = new Vector2(textWidth - 190f, 70f);
            _cardName[world].rectTransform.anchoredPosition = new Vector2(left + (textWidth - 190f) * 0.5f, 0f);
            _cardName[world].enableAutoSizing = true;
            _cardName[world].fontSizeMin = Design.Caption;
            _cardName[world].fontSizeMax = Design.Body;
            _cardName[world].textWrappingMode = TextWrappingModes.NoWrap;

            _cardStar[world] = UiBuilder.Image(rect, "Star", Icons.Star, Design.Gold);
            _cardStar[world].type = Image.Type.Simple;
            _cardStar[world].rectTransform.sizeDelta = new Vector2(44f, 44f);
            _cardStar[world].rectTransform.anchoredPosition = new Vector2(width * 0.5f - 176f, 2f);
            _cardStars[world] = UiBuilder.Label(rect, "Stars", "0/30", Design.Label, Color.white, Design.FontDisplay, TextAlignmentOptions.Left);
            _cardStars[world].rectTransform.sizeDelta = new Vector2(140f, 60f);
            _cardStars[world].rectTransform.anchoredPosition = new Vector2(width * 0.5f - 148f + 70f, 4f);
            _cardStars[world].textWrappingMode = TextWrappingModes.NoWrap;

            // Stars won in the world, as a bar along the bottom of the text.
            var track = UiBuilder.Panel(rect, "Track", new Vector2(width * 0.5f - 30f - left, 12f), Color.white.WithAlpha(0.16f), 6f);
            track.rectTransform.anchoredPosition = new Vector2(left + (width * 0.5f - 30f - left) * 0.5f, -48f);
            _cardBar[world] = UiBuilder.Image(track.rectTransform, "Fill", Art.Panel(6f), Design.Gold);
            _cardBar[world].rectTransform.anchorMin = _cardBar[world].rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _cardBar[world].rectTransform.pivot = new Vector2(0f, 0.5f);
            _cardBar[world].rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// The world's field of colour behind everything: two broad pools of its colour, one each
        /// side, and a third of the next world's at the top, so one world fades into the next.
        /// </summary>
        void BuildField(RectTransform parent, int world)
        {
            float bottom = _worldBottom[world], top = _worldTop[world];
            var colour = Design.WorldColor(world);
            Place(parent, "Pool", Art.Pool, colour.WithAlpha(0.34f), new Vector2(-300f, Mathf.Lerp(bottom, top, 0.28f)), 1700f);
            Place(parent, "Pool", Art.Pool, colour.WithAlpha(0.30f), new Vector2(320f, Mathf.Lerp(bottom, top, 0.7f)), 1600f);
            if (world < Worlds - 1)
                Place(parent, "Pool", Art.Pool, Design.WorldColor(world + 1).WithAlpha(0.2f), new Vector2(0f, top), 1400f);
        }

        /// <summary>
        /// What drifts behind the road, a little slower than it scrolls: large crystals turned at
        /// odd angles, small pieces of what the world is about, and sparkles. Kept to the sides of
        /// the road and faint, so they read as far away rather than as things to tap.
        /// </summary>
        void BuildDecor(RectTransform parent, int world)
        {
            var rng = new System.Random(world * 7717 + 13);
            var colour = Design.WorldColor(world);
            var light = SheetKit.Tinted(colour, Color.white, 0.55f);
            float bottom = _worldBottom[world], top = _worldTop[world];
            var motif = Motif(world);

            for (int i = 0; i < 8; i++)
            {
                float y = Mathf.Lerp(bottom + 200f, top - 100f, (i + (float)rng.NextDouble() * 0.7f) / 8f);
                float side = RoadSideAt(y) > 0f ? -1f : 1f;
                float x = side * (340f + (float)rng.NextDouble() * 140f);
                bool big = i % 2 == 0;

                var piece = big || motif == null
                    ? Place(parent, "Crystal", Art.Crystal, light.WithAlpha(0.30f + (float)rng.NextDouble() * 0.15f), new Vector2(x, y),
                        (big ? 160f : 90f) + (float)rng.NextDouble() * 110f)
                    : Place(parent, "Motif", motif, light.WithAlpha(0.45f), new Vector2(x, y),
                        70f + (float)rng.NextDouble() * 30f);
                piece.rectTransform.localEulerAngles = new Vector3(0f, 0f, (float)rng.NextDouble() * 50f - 25f);
            }

            for (int i = 0; i < 16; i++)
            {
                float size = 16f + (float)rng.NextDouble() * 26f;
                Place(parent, "Sparkle", Art.Sparkle, Color.white.WithAlpha(0.3f + (float)rng.NextDouble() * 0.4f),
                    new Vector2((float)rng.NextDouble() * 1000f - 500f, Mathf.Lerp(bottom, top, (float)rng.NextDouble())), size);
            }
        }

        /// <summary>Which side of the middle the road is on at a height: positive right.</summary>
        float RoadSideAt(float y)
        {
            for (int i = 1; i < _stops.Count; i++)
                if (_stops[i].Position.y >= y)
                {
                    var a = _stops[i - 1].Position;
                    var b = _stops[i].Position;
                    return Mathf.Lerp(a.x, b.x, Mathf.InverseLerp(a.y, b.y, y));
                }

            return 0f;
        }

        /// <summary>
        /// What a world scatters beside its road, drawn in a pale tint of its colour. Nothing that
        /// looks like a block: scattered blocks read as pieces lying about, and pale ones as mud.
        /// </summary>
        static Sprite Motif(int world)
        {
            switch (world)
            {
                case 1: return Art.Tile;
                case 5: return Art.TimerRing;
                case 6: return Art.Ice(2);
                case 7: return Icons.Bolt;
                case 8: return Icons.Star;
                case 9: return Icons.Crown;
                default: return null;
            }
        }

        /// <summary>The end of the road: a crown in a pool of gold over the last chest.</summary>
        void BuildFinale()
        {
            var last = _stops[_stops.Count - 1].Position;
            var at = new Vector2(0f, last.y + 300f);
            Place(_top, "Glow", Art.Pool, Design.Gold.WithAlpha(0.3f), at, 760f);
            Place(_top, "Crown", Icons.Crown, Design.Gold, at, 210f);
        }

        /// <summary>The player on the map: the prism crystal in a white pin, over the next level.</summary>
        void BuildMarker()
        {
            _marker = UiBuilder.Node(_top, "Marker");
            _marker.anchorMin = _marker.anchorMax = new Vector2(0.5f, 0f);
            _marker.sizeDelta = new Vector2(120f, 150f);

            var shadow = UiBuilder.Image(_marker, "Shadow", Art.DiscShadow(100f, 30f), Color.black.WithAlpha(0.3f));
            shadow.type = Image.Type.Simple;
            float pad = Art.ShadowPad(30f);
            shadow.rectTransform.sizeDelta = new Vector2(100f + pad * 2f, 100f + pad * 2f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, 14f);

            var tip = UiBuilder.Image(_marker, "Tip", Icons.Play, Color.white);
            tip.type = Image.Type.Simple;
            tip.rectTransform.sizeDelta = new Vector2(46f, 46f);
            tip.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            tip.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);

            var pin = UiBuilder.Image(_marker, "Pin", Art.Disc, Color.white);
            pin.type = Image.Type.Simple;
            pin.rectTransform.sizeDelta = new Vector2(108f, 108f);
            pin.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            _markerRing = UiBuilder.Image(_marker, "Ring", Art.Disc, Design.Prism);
            _markerRing.type = Image.Type.Simple;
            _markerRing.rectTransform.sizeDelta = new Vector2(88f, 88f);
            _markerRing.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            var crystal = UiBuilder.Image(_marker, "Crystal", Art.Crystal, Color.white);
            crystal.type = Image.Type.Simple;
            crystal.rectTransform.sizeDelta = new Vector2(60f, 60f);
            crystal.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        }

        void BuildHeader()
        {
            // The header has a canvas of its own: the map's content does too, and a nested canvas
            // draws over whatever of its parent comes before it — the road ran over the title.
            var header = UiBuilder.Child(Root, "Header");
            header.gameObject.AddComponent<Canvas>();

            // Ground colour at the top, fading down, so the road passes under the header.
            var fade = UiBuilder.Image(header, "Fade", Art.VerticalFade, Design.BgTop);
            fade.type = Image.Type.Simple;
            fade.rectTransform.anchorMin = new Vector2(0f, 1f);
            fade.rectTransform.anchorMax = new Vector2(1f, 1f);
            // Turned about its own middle: turned about its top edge it swung up off the screen.
            fade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fade.rectTransform.offsetMin = new Vector2(-60f, -HeaderHeight - 190f);
            fade.rectTransform.offsetMax = new Vector2(60f, AppController.SafeInsets.w);
            fade.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

            float headerCenter = -(Design.Space2 + Design.IconButton * 0.5f);

            var back = UiBuilder.Button(header, "Back", new Vector2(Design.IconButton, Design.IconButton), UiButton.Style.Icon,
                null, Design.Body, Icons.ChevronLeft);
            back.Rect.anchorMin = back.Rect.anchorMax = new Vector2(0f, 1f);
            back.Rect.pivot = new Vector2(0f, 0.5f);
            back.Rect.anchoredPosition = new Vector2(Design.Gutter, headerCenter);
            back.Clicked += () => { Audio.PlayClick(); App.ShowMenu(); };

            var heading = UiBuilder.Label(header, "Heading", Str.Adventure, Design.Headline, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(500f, 110f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, headerCenter);
            UiBuilder.TextShadow(heading, 0.4f, -0.3f, 0.45f);

            var chip = UiBuilder.Node(header, "Stars");
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.sizeDelta = new Vector2(236f, 124f);
            chip.anchoredPosition = new Vector2(-Design.Gutter, headerCenter);
            UiBuilder.Panel(chip, "Fill", chip.sizeDelta, Design.SurfaceInset, 62f);
            UiBuilder.Hairline(chip, "Edge", chip.sizeDelta, 62f);
            var star = UiBuilder.Image(chip, "Icon", Icons.Star, Design.Gold);
            star.type = Image.Type.Simple;
            star.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            star.rectTransform.anchoredPosition = new Vector2(-56f, 0f);
            _starTotal = UiBuilder.Label(chip, "Total", "0", Design.Body, Design.Gold, Design.FontDisplay, TextAlignmentOptions.Left);
            _starTotal.rectTransform.sizeDelta = new Vector2(130f, 100f);
            _starTotal.rectTransform.anchoredPosition = new Vector2(58f, 3f);

            // Back to the next level when the player has scrolled away from it.
            _jump = UiBuilder.Button(header, "Jump", new Vector2(Design.IconButton, Design.IconButton), UiButton.Style.Icon,
                null, Design.Body, Icons.Flag);
            _jump.Rect.anchorMin = _jump.Rect.anchorMax = new Vector2(1f, 0f);
            _jump.Rect.pivot = new Vector2(1f, 0f);
            _jump.Rect.anchoredPosition = new Vector2(-Design.Gutter, Design.Space4);
            _jump.Clicked += () =>
            {
                Audio.PlayClick();
                StartCoroutine(_scroll.GlideTo(OffsetFor(Current.Position.y), 0.5f));
            };
            _jump.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ state

        Stop Current => _stops[IndexOfLevel(Progress.UnlockedLevel)];

        static int IndexOfLevel(int level)
        {
            level = Mathf.Clamp(level, 1, LevelGenerator.LevelCount);
            return level - 1 + (level - 1) / LevelGenerator.WorldSize;
        }

        /// <summary>The scroll offset that puts a height a little below the middle of the page.</summary>
        float OffsetFor(float y) => Mathf.Clamp(y - App.PageHeight * 0.42f, 0f, Mathf.Max(0f, _contentHeight - App.PageHeight));

        /// <summary>Opened after a level was won: the road is drawn on to the next one, and its sheet opens.</summary>
        public void Prepare(int afterWin) => _afterWin = afterWin;

        protected override void OnShow()
        {
            if (!_built) BuildMap();
            Progress.GrantStarterBoosters();
            Progress.Changed -= OnProgressChanged;
            Progress.Changed += OnProgressChanged;

            _lightWorld = -1;
            _revealing = false;
            _pressed = null;

            int unlocked = Progress.UnlockedLevel;
            int revealed = Mathf.Max(1, Progress.MapRevealed);

            // One new level since the map last looked: that step is animated. More than one (a
            // fresh install, an old save) is simply shown.
            bool animate = unlocked == revealed + 1 && (_afterWin > 0 || Progress.MapRevealed > 0);
            Paint(animate ? revealed : unlocked);

            float focus = animate ? _stops[IndexOfLevel(revealed)].Position.y : Current.Position.y;
            _scroll.JumpTo(OffsetFor(focus));
            OnScrolled(_scroll.Offset);

            int afterWin = _afterWin;
            _afterWin = 0;

            if (animate) StartCoroutine(RevealRoutine(revealed, unlocked, afterWin > 0));
            else Progress.MapRevealed = unlocked;

            // The next level is generated in the background, so tapping it starts instantly.
            int next = unlocked;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.Generate(next, Design.PaletteSize));
        }

        protected override void OnHide() => Progress.Changed -= OnProgressChanged;

        void OnDestroy() => Progress.Changed -= OnProgressChanged;

        void OnProgressChanged() => _dirty = true;

        /// <summary>Paints every stop and every stretch of road as if <paramref name="reached"/> were the next level.</summary>
        void Paint(int reached)
        {
            _starTotal.text = Progress.TotalStars.ToString();
            bool complete = Progress.AdventureComplete;

            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                bool open = stop.Chest ? Progress.LevelsCompleted >= (stop.World + 1) * LevelGenerator.WorldSize : stop.Level <= reached;
                PaintRoad(stop, open, open ? stop.Lane.Count : 0);

                if (stop.Chest) PaintChest(stop);
                else PaintLevel(stop, reached, complete);
            }

            for (int w = 0; w < Worlds; w++) PaintCard(w, w * LevelGenerator.WorldSize + 1 <= reached);

            PlaceMarker(_stops[IndexOfLevel(reached)], reached);
            _marker.gameObject.SetActive(!complete);
        }

        void PaintLevel(Stop stop, int reached, bool complete)
        {
            var colour = Design.WorldColor(stop.World);
            bool current = stop.Level == reached && !complete;
            bool done = stop.Level < reached || (stop.Level == reached && Progress.StarsFor(stop.Level) > 0);
            int hardness = LevelGenerator.HardnessOf(stop.Level);
            var hard = HardColour(hardness);

            float size = current ? CurrentSize : done ? NodeSize : LockedSize;
            float rim = current ? 16f : done ? 12f : 8f;

            stop.Disc.rectTransform.sizeDelta = new Vector2(size, size);
            stop.Rim.rectTransform.sizeDelta = new Vector2(size + rim, size + rim);
            float pad = Art.ShadowPad(30f);
            stop.Shadow.rectTransform.sizeDelta = new Vector2(size + rim + pad * 2f, size + rim + pad * 2f);
            stop.Shadow.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            stop.Shadow.gameObject.SetActive(done || current);

            // The badges ride the disc's shoulders at whatever size it is drawn: placed for the
            // finished size, they sat inside the larger white disc of the level to play.
            float shoulder = (size + rim) * 0.5f;
            if (stop.HardBadge != null) stop.HardBadge.anchoredPosition = new Vector2(shoulder * 0.78f, shoulder * 0.7f);
            if (stop.NewBadge != null) stop.NewBadge.anchoredPosition = new Vector2(-shoulder * 0.78f, shoulder * 0.7f);

            if (current)
            {
                // The one to play: a white button with the number in the world's colour. Flat: the
                // lit button's own shading turned white into a grey ball.
                stop.Disc.sprite = Art.Disc;
                stop.Disc.color = Color.white;
                stop.Rim.color = hardness > 0 ? hard : colour;
                stop.Label.color = colour;
                stop.Label.fontSize = Design.Title * 0.82f;
                stop.Label.fontSharedMaterial = stop.Label.font.material;
            }
            else if (done)
            {
                stop.Disc.sprite = Art.Node;
                stop.Disc.color = colour;
                stop.Rim.color = hardness > 0 ? hard : Color.white;
                stop.Label.color = Color.white;
                stop.Label.fontSize = Design.Headline;
                Shadowed(stop.Label);
            }
            else
            {
                // Not yet: a small disc of frosted glass. Here, and only here, translucent on
                // purpose — it takes on whatever field of colour it sits in, where an opaque mix
                // of world and ground came out a muddy beige in half the worlds. Linear blending
                // makes the low alpha read as frost, which is the point.
                stop.Disc.sprite = Art.Disc;
                // A hard one still says so, as tinted glass — a solid rim under see-through glass
                // showed as a solid disc, and a solid disc reads as a level already played.
                stop.Disc.color = hardness > 0 ? hard.WithAlpha(0.3f) : Glass;
                stop.Rim.color = hardness > 0 ? hard.WithAlpha(0.22f) : GlassRim;
                stop.Label.color = Color.white;
                stop.Label.fontSize = Design.Body;
                Shadowed(stop.Label);
            }

            stop.Halo.gameObject.SetActive(current);
            stop.Halo.color = colour.WithAlpha(0.55f);

            int stars = Progress.StarsFor(stop.Level);
            for (int s = 0; s < 3; s++)
            {
                stop.Stars[s].gameObject.SetActive(done);
                stop.Stars[s].color = s < stars ? Design.Gold : Color.white.WithAlpha(0.3f);
            }
        }

        static readonly Color Glass = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color GlassRim = new Color(1f, 1f, 1f, 0.22f);

        void PaintChest(Stop stop)
        {
            bool ready = Progress.ChestReady(stop.World);
            bool opened = Progress.ChestOpened(stop.World);
            float size = ready ? NodeSize + 8f : LockedSize;

            stop.Disc.rectTransform.sizeDelta = new Vector2(size, size);
            stop.Rim.rectTransform.sizeDelta = new Vector2(size + 12f, size + 12f);
            float pad = Art.ShadowPad(30f);
            stop.Shadow.rectTransform.sizeDelta = new Vector2(size + pad * 2f, size + pad * 2f);
            stop.Shadow.gameObject.SetActive(ready);

            stop.Disc.sprite = ready ? Art.Node : Art.Disc;
            stop.Disc.color = ready ? Design.Gold : Glass;
            stop.Rim.color = ready ? Color.white : GlassRim;
            stop.Icon.sprite = opened ? Icons.Check : Icons.Chest;
            stop.Icon.color = ready ? new Color(0.42f, 0.24f, 0.04f) : opened ? Design.Mint : Color.white.WithAlpha(0.75f);
            stop.Icon.rectTransform.sizeDelta = new Vector2(size * 0.52f, size * 0.52f);
            stop.Halo.gameObject.SetActive(ready);
            stop.Halo.color = Design.Gold.WithAlpha(0.6f);
        }

        /// <summary>The stretch of road into a stop, lit up to <paramref name="lit"/> pieces; the rest shows its dots.</summary>
        static void PaintRoad(Stop stop, bool open, int lit)
        {
            var colour = Design.WorldColor(stop.World);
            var lane = SheetKit.Tinted(colour, Color.white, 0.25f);
            var core = SheetKit.Tinted(colour, Color.white, 0.82f);
            var glow = colour.WithAlpha(0.26f);
            var dot = Color.white.WithAlpha(0.55f);

            int count = stop.Lane.Count;
            for (int i = 0; i < count; i++)
            {
                bool on = i < lit;
                stop.Lane[i].gameObject.SetActive(on);
                stop.Core[i].gameObject.SetActive(on);
                if (!on) continue;
                stop.Lane[i].color = lane;
                stop.Core[i].color = core;
            }

            // Dots and glow go by how far along the lit part has come.
            float litShare = count == 0 ? (open ? 1f : 0f) : lit / (float)count;
            for (int i = 0; i < stop.Dots.Count; i++)
            {
                bool covered = (i + 0.5f) / stop.Dots.Count <= litShare;
                stop.Dots[i].gameObject.SetActive(!covered);
                stop.Dots[i].color = dot;
            }

            for (int i = 0; i < stop.Glow.Count; i++)
            {
                bool on = (i + 0.5f) / stop.Glow.Count <= litShare;
                stop.Glow[i].gameObject.SetActive(on);
                stop.Glow[i].color = glow;
            }
        }

        void PaintCard(int world, bool reached)
        {
            var colour = Design.WorldColor(world);
            _cardFill[world].color = reached ? SheetKit.Tinted(Design.Surface, colour, 0.34f) : Design.Surface;
            _cardEdge[world].color = reached ? SheetKit.Tinted(colour, Color.white, 0.4f).WithAlpha(0.45f) : Design.Hairline;
            _emblem[world].color = reached ? colour : Design.SurfaceButton;
            _emblemGlyph[world].sprite = reached ? WorldGlyph(world) : Icons.Lock;
            _emblemGlyph[world].color = reached ? Color.white : Design.TextTertiary;
            _cardCaption[world].color = reached ? SheetKit.Tinted(colour, Color.white, 0.55f) : Design.TextTertiary;
            _cardName[world].color = reached ? Color.white : Design.TextSecondary;

            int stars = Progress.WorldStars(world);
            int max = LevelGenerator.WorldSize * 3;
            _cardStars[world].text = $"{stars}/{max}";
            _cardStars[world].gameObject.SetActive(reached);
            _cardStar[world].gameObject.SetActive(reached);

            float width = ((RectTransform)_cardBar[world].rectTransform.parent).sizeDelta.x;
            float k = reached ? stars / (float)max : 0f;
            _cardBar[world].rectTransform.sizeDelta = new Vector2(k <= 0f ? 0f : Mathf.Max(12f, width * k), 12f);
        }

        void PlaceMarker(Stop stop, int reached)
        {
            float size = stop.Level == reached ? CurrentSize : NodeSize;
            _markerHome = stop.Position + new Vector2(0f, size * 0.5f + 74f);
            _marker.anchoredPosition = _markerHome;
            _markerRing.color = Design.WorldColor(stop.World);
        }

        /// <summary>
        /// The walk from one level to the next: the road lights up piece by piece in the world's
        /// colour, the next level turns into the white button, the marker hops along. Then, after
        /// a win, that level's sheet opens — unless a chest just became ready, which comes first.
        /// </summary>
        IEnumerator RevealRoutine(int from, int to, bool openNext)
        {
            _revealing = true;
            var target = _stops[IndexOfLevel(to)];
            var origin = _stops[IndexOfLevel(from)];

            yield return new WaitForSecondsRealtime(0.35f);

            int a = IndexOfLevel(from), b = IndexOfLevel(to);
            StartCoroutine(_scroll.GlideTo(OffsetFor(target.Position.y), 0.9f));

            // Every stop between the two — a chest sits between worlds — is walked in turn.
            for (int k = a + 1; k <= b; k++)
            {
                var stop = _stops[k];
                for (int lit = 1; lit <= stop.Lane.Count; lit++)
                {
                    PaintRoad(stop, true, lit);
                    if (lit % 2 == 0) yield return null;
                }

                if (stop.Chest)
                {
                    PaintChest(stop);
                    StartCoroutine(Tween.Punch(stop.Body, 0.18f, 0.28f));
                }
            }

            // The marker hops along to the new level.
            var start = _marker.anchoredPosition;
            var end = target.Position + new Vector2(0f, CurrentSize * 0.5f + 74f);
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutCubic(t / 0.5f);
                _marker.anchoredPosition = Vector2.Lerp(start, end, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 90f);
                yield return null;
            }

            Paint(to);
            Progress.MapRevealed = to;
            StartCoroutine(Tween.Scale(target.Body, Vector3.one * 0.7f, Vector3.one, 0.36f, Ease.OutBack));
            StartCoroutine(Tween.Punch(origin.Body, 0.12f, 0.24f));
            Audio.PlayStage();
            _revealing = false;

            int world = LevelGenerator.WorldOf(from);
            if (!openNext || Progress.ChestReady(world) || to == from) yield break;

            yield return new WaitForSecondsRealtime(0.3f);
            if (IsVisible) App.OpenLevelStart(to);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!_built) return;

            if (_dirty && !_revealing)
            {
                _dirty = false;
                Paint(Progress.UnlockedLevel);
            }

            if (_marker != null && !_revealing)
                _marker.anchoredPosition = _markerHome + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 2.4f) * 8f);
        }

        void OnScrolled(float offset)
        {
            if (!_built) return;

            float view = App.PageHeight;
            const float margin = 700f;
            float centre = offset + view * 0.5f;

            int centreWorld = 0;
            for (int w = 0; w < Worlds; w++)
            {
                bool visible = _worldTop[w] >= offset - margin && _worldBottom[w] <= offset + view + margin;
                if (_worlds[w].gameObject.activeSelf != visible) _worlds[w].gameObject.SetActive(visible);
                if (centre >= _worldBottom[w]) centreWorld = w;

                // Depth: what drifts behind the road moves at a fraction of its pace, measured
                // from the world's own middle so it sits where it was placed when centred.
                if (visible)
                {
                    float middle = (_worldBottom[w] + _worldTop[w]) * 0.5f;
                    _decor[w].anchoredPosition = new Vector2(0f, (centre - middle) * Parallax);
                }
            }

            // The room takes the colour of the world on screen.
            if (centreWorld != _lightWorld)
            {
                _lightWorld = centreWorld;
                Backdrop.Current?.SetLight(Design.WorldColor(centreWorld));
            }

            if (_stops.Count > 0 && _jump != null)
            {
                float y = Current.Position.y;
                bool away = y < offset + 120f || y > offset + view - HeaderHeight - 120f;
                if (_jump.gameObject.activeSelf != away) _jump.gameObject.SetActive(away);
            }
        }

        // ------------------------------------------------------------------ taps

        Stop StopAt(Vector2 contentPoint)
        {
            Stop best = null;
            float bestDistance = CurrentSize * 0.55f;
            foreach (var stop in _stops)
            {
                if (!_worlds[stop.World].gameObject.activeSelf) continue;
                float d = Vector2.Distance(stop.Position, contentPoint);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = stop;
                }
            }

            return best;
        }

        void OnMapPress(Vector2 contentPoint)
        {
            _pressed = StopAt(contentPoint);
            if (_pressed != null) _pressed.Body.localScale = Vector3.one * 0.93f;
        }

        void OnMapRelease()
        {
            if (_pressed != null) _pressed.Body.localScale = Vector3.one;
            _pressed = null;
        }

        void OnMapTap(Vector2 contentPoint)
        {
            if (_revealing) return;
            var stop = StopAt(contentPoint);
            if (stop == null) return;

            if (stop.Chest)
            {
                if (Progress.ChestReady(stop.World))
                {
                    Audio.PlayClick();
                    App.OpenChest(stop.World);
                }
                else
                {
                    Audio.PlayInvalid();
                    StartCoroutine(Tween.Punch(stop.Body, 0.12f, 0.2f));
                }

                return;
            }

            if (stop.Level > Progress.UnlockedLevel)
            {
                Audio.PlayInvalid();
                StartCoroutine(Tween.Punch(stop.Body, 0.12f, 0.2f));
                return;
            }

            Audio.PlayClick();
            App.OpenLevelStart(stop.Level);
        }

#if PRIZMA_AUTOTEST
        /// <summary>Test hook: scrolls the map so a level sits where the player would see it.</summary>
        public void AutoFocus(int level)
        {
            _scroll.JumpTo(OffsetFor(_stops[IndexOfLevel(level)].Position.y));
            OnScrolled(_scroll.Offset);
        }
#endif
    }

    /// <summary>
    /// The map's scroll: drags move it, a fling carries on and slows, the ends give softly. A press
    /// that never moves far is a tap, handed back with the point in the content's own space.
    /// Unlike <see cref="UiScroll"/> it reports taps, since the map's stops are drawn, not buttons.
    /// </summary>
    public sealed class MapScroll : MonoBehaviour, IPointerWidget
    {
        const float TapSlop = 22f;

        RectTransform _rect;
        RectTransform _content;
        float _offset;
        float _max;
        float _velocity;
        bool _dragging;
        bool _moved;
        float _pressY;
        float _pressOffset;
        float _lastY;
        float _lastTime;
        int _glideId;

        public RectTransform Rect => _rect;
        public RectTransform Content => _content;
        public bool Interactable { get; set; } = true;
        public float Offset => _offset;

        public event Action<Vector2> Tapped;
        public event Action<Vector2> Pressed;
        public event Action Released;
        public event Action<float> Moved;

        public static MapScroll Create(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var scroll = go.AddComponent<MapScroll>();
            scroll._rect = (RectTransform)go.transform;

            scroll._content = UiBuilder.Node(scroll._rect, "Content");
            scroll._content.anchorMin = scroll._content.anchorMax = new Vector2(0.5f, 0f);
            scroll._content.pivot = new Vector2(0.5f, 0f);
            // The road moves every frame of a drag; on its own canvas that re-batches the map
            // alone, not the header over it.
            scroll._content.gameObject.AddComponent<Canvas>();
            return scroll;
        }

        public void SetContentHeight(float height, float view)
        {
            _content.sizeDelta = new Vector2(1080f, height);
            _max = Mathf.Max(0f, height - view);
        }

        public void JumpTo(float offset)
        {
            _glideId++;
            _velocity = 0f;
            _offset = Mathf.Clamp(offset, 0f, _max);
            Apply();
        }

        /// <summary>Slides to an offset. A drag, a jump or a newer glide takes over from it.</summary>
        public IEnumerator GlideTo(float offset, float duration)
        {
            int id = ++_glideId;
            _velocity = 0f;
            float from = _offset;
            float to = Mathf.Clamp(offset, 0f, _max);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (_dragging || id != _glideId) yield break;
                _offset = Mathf.Lerp(from, to, Ease.OutCubic(t / duration));
                Apply();
                yield return null;
            }

            _offset = to;
            Apply();
        }

        void Apply()
        {
            _content.anchoredPosition = new Vector2(0f, -_offset);
            Moved?.Invoke(_offset);
        }

        float LocalY(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, null, out var local);
            return local.y;
        }

        Vector2 ContentPoint(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPoint, null, out var local);
            // Content's pivot is its bottom centre; stops are placed from there.
            return local;
        }

        void OnEnable() => PointerRouter.Register(this);

        void OnDisable()
        {
            PointerRouter.Unregister(this);
            _dragging = false;
        }

        public void OnPress(Vector2 screenPoint)
        {
            _dragging = true;
            _moved = false;
            _velocity = 0f;
            _glideId++;
            _pressY = _lastY = LocalY(screenPoint);
            _pressOffset = _offset;
            _lastTime = Time.unscaledTime;
            Pressed?.Invoke(ContentPoint(screenPoint));
        }

        public void OnDrag(Vector2 screenPoint)
        {
            float y = LocalY(screenPoint);
            if (!_moved && Mathf.Abs(y - _pressY) > TapSlop)
            {
                _moved = true;
                Released?.Invoke();
            }

            if (!_moved) return;

            float target = _pressOffset - (y - _pressY);
            if (target < 0f) target *= 0.33f;
            else if (target > _max) target = _max + (target - _max) * 0.33f;
            _offset = target;
            Apply();

            float dt = Time.unscaledTime - _lastTime;
            if (dt > 0.0001f)
            {
                _velocity = Mathf.Lerp(_velocity, -(y - _lastY) / dt, 0.5f);
                _lastY = y;
                _lastTime = Time.unscaledTime;
            }
        }

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            _dragging = false;
            if (!_moved)
            {
                Released?.Invoke();
                Tapped?.Invoke(ContentPoint(screenPoint));
            }
        }

        void Update()
        {
            if (_dragging) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (_offset < 0f || _offset > _max)
            {
                float edge = _offset < 0f ? 0f : _max;
                _velocity = 0f;
                _offset = Mathf.Lerp(_offset, edge, 1f - Mathf.Exp(-14f * dt));
                if (Mathf.Abs(_offset - edge) < 0.5f) _offset = edge;
                Apply();
                return;
            }

            if (Mathf.Abs(_velocity) < 8f) return;

            _offset += _velocity * dt;
            _velocity *= Mathf.Exp(-2.6f * dt);
            if (_offset < 0f || _offset > _max) _velocity *= 0.3f;
            Apply();
        }
    }

    /// <summary>
    /// A booster as a tile: its icon in a coloured squircle, its name, and how many are held. The
    /// start sheet uses it as a switch, the chest sheet as a display of what is inside.
    /// </summary>
    sealed class BoosterTile
    {
        public UiButton Button;
        public TextMeshProUGUI Count;
        public Image Check;
        public Image Ring;
        public Booster Kind;

        public static Color Tint(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: return Design.TintTeal;
                case Booster.Charge: return Design.TintViolet;
                default: return Design.TintOrange;
            }
        }

        public static Sprite Glyph(Booster booster)
        {
            switch (booster)
            {
                case Booster.Moves: return Icons.PlusMoves;
                case Booster.Charge: return Art.Crystal;
                default: return Icons.Hammer;
            }
        }

        public static BoosterTile Build(RectTransform parent, Booster booster, Vector2 size, float x, float top)
        {
            var tile = new BoosterTile { Kind = booster };
            var button = UiBuilder.Button(parent, "Booster" + booster, size, UiButton.Style.Bare);
            SheetKit.PinTop(button.Rect, top, x);
            button.SetRestColor(Design.SurfaceGroup);
            tile.Button = button;

            tile.Ring = UiBuilder.Image(button.Content, "Ring", Art.Stroke(Design.RadiusMd), Design.AccentA);
            tile.Ring.rectTransform.sizeDelta = size;
            tile.Ring.gameObject.SetActive(false);

            const float icon = 104f;
            var glyph = SheetKit.RowIcon(button.Content, "Icon", Glyph(booster), Tint(booster), 0f, size.y * 0.5f - icon * 0.5f - 30f, icon);
            glyph.type = Image.Type.Simple;

            var name = UiBuilder.Label(button.Content, "Name", Str.BoosterName(booster), Design.Caption, Design.TextPrimary, Design.FontDisplay);
            name.rectTransform.sizeDelta = new Vector2(size.x - 20f, 60f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.5f + 78f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 32f;
            name.fontSizeMax = Design.Caption;
            name.textWrappingMode = TextWrappingModes.NoWrap;

            var badge = UiBuilder.Image(button.Content, "Badge", Art.Disc, Design.Gold);
            badge.type = Image.Type.Simple;
            badge.rectTransform.sizeDelta = new Vector2(66f, 66f);
            badge.rectTransform.anchoredPosition = new Vector2(icon * 0.5f + 14f, size.y * 0.5f - 44f);
            tile.Count = UiBuilder.Label(badge.rectTransform, "Count", "0", Design.Caption, new Color(0.16f, 0.1f, 0.02f), Design.FontDisplay);
            tile.Count.rectTransform.sizeDelta = badge.rectTransform.sizeDelta;
            tile.Count.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            // Picked: a mint disc with a tick in the corner opposite the count — the settings tiles'
            // mark. A bare tick here sat half over the icon's corner.
            tile.Check = UiBuilder.Image(button.Content, "Check", Art.Disc, Design.Mint);
            tile.Check.type = Image.Type.Simple;
            tile.Check.rectTransform.sizeDelta = new Vector2(52f, 52f);
            tile.Check.rectTransform.anchoredPosition = new Vector2(-size.x * 0.5f + 40f, size.y * 0.5f - 40f);
            var tick = UiBuilder.Image(tile.Check.rectTransform, "Tick", Icons.Check, Color.white);
            tick.type = Image.Type.Simple;
            tick.rectTransform.sizeDelta = new Vector2(34f, 34f);
            tile.Check.gameObject.SetActive(false);
            return tile;
        }

        public void SetSelected(bool on)
        {
            Ring.gameObject.SetActive(on);
            Check.gameObject.SetActive(on);
            Button.SetHighlighted(on, SheetKit.Tinted(Design.SurfaceGroup, Design.AccentA, 0.22f));
        }
    }

    /// <summary>
    /// A level's sheet, before it starts: which world, how hard, what it asks for and in how many
    /// moves, the best stars so far, the win streak's gift, and the boosters to take in. The saga
    /// games' pre-level card, in this game's sheet — one look, one tap on play.
    /// </summary>
    public sealed class LevelStartScreen : SheetScreen
    {
        const float WorldRow = 64f;
        const float GoalCard = 290f;
        const float StarsRow = 96f;
        const float TileHeight = 244f;

        int _level = 1;
        bool _resume;
        bool _useMoves;
        bool _useCharge;

        Image _worldDot;
        TextMeshProUGUI _world;
        RectTransform _hardChip;
        Image _hardFill;
        Image _hardIcon;
        TextMeshProUGUI _hardLabel;

        RectTransform _goal;
        Image _goalIcon;
        TextMeshProUGUI _goalCount;
        TextMeshProUGUI _goalWord;
        TextMeshProUGUI _moves;
        TextMeshProUGUI _movesWord;

        RectTransform _stars;
        readonly Image[] _starImages = new Image[3];
        RectTransform _intro;
        Image _introIcon;
        TextMeshProUGUI _introLabel;

        RectTransform _streak;
        readonly Image[] _flames = new Image[Progress.MaxWinStreak];
        TextMeshProUGUI _streakGift;

        RectTransform _boosterHeading;
        BoosterTile[] _tiles;
        UiButton _play;

        protected override void Build()
        {
            BuildSheet(Str.LevelTitle(1), Icons.Flag, Design.WorldColor(0));

            // World line, and the hard chip at its right end.
            _worldDot = UiBuilder.Image(Body, "Dot", Art.Disc, Color.white);
            _worldDot.type = Image.Type.Simple;
            _worldDot.rectTransform.sizeDelta = new Vector2(30f, 30f);
            SheetKit.PinTop(_worldDot.rectTransform, (WorldRow - 30f) * 0.5f, -Design.ContentWidth * 0.5f + 15f);
            _world = UiBuilder.Label(Body, "World", "", Design.Label, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _world.rectTransform.sizeDelta = new Vector2(620f, WorldRow);
            SheetKit.PinTop(_world.rectTransform, 0f, -Design.ContentWidth * 0.5f + 48f + 310f);
            _world.enableAutoSizing = true;
            _world.fontSizeMin = Design.Caption;
            _world.fontSizeMax = Design.Label;
            _world.textWrappingMode = TextWrappingModes.NoWrap;

            _hardChip = UiBuilder.Node(Body, "Hard");
            _hardChip.sizeDelta = new Vector2(280f, WorldRow);
            SheetKit.PinTop(_hardChip, 0f, Design.ContentWidth * 0.5f - 140f);
            _hardFill = UiBuilder.Panel(_hardChip, "Fill", _hardChip.sizeDelta, Design.PreviewTint(3), WorldRow * 0.5f);
            _hardIcon = UiBuilder.Image(_hardChip, "Icon", Icons.Bolt, Color.white);
            _hardIcon.type = Image.Type.Simple;
            _hardIcon.rectTransform.sizeDelta = new Vector2(40f, 40f);
            _hardIcon.rectTransform.anchoredPosition = new Vector2(-100f, 0f);
            _hardLabel = UiBuilder.Label(_hardChip, "Label", "", Design.Caption, Color.white, Design.FontDisplay);
            _hardLabel.rectTransform.sizeDelta = new Vector2(200f, WorldRow);
            _hardLabel.rectTransform.anchoredPosition = new Vector2(20f, 2f);
            _hardLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // The goal and the moves, side by side on one card.
            _goal = SheetKit.Group(Body, "Goal", 0f, GoalCard);
            _goalIcon = UiBuilder.Image(_goal, "Icon", Art.Crystal, Design.Crystal);
            _goalIcon.type = Image.Type.Simple;
            _goalIcon.rectTransform.sizeDelta = new Vector2(150f, 150f);
            _goalIcon.rectTransform.anchoredPosition = new Vector2(-Design.ContentWidth * 0.25f - 128f, 10f);
            _goalCount = UiBuilder.Label(_goal, "Count", "0", Design.Display * 0.8f, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingDisplay);
            _goalCount.rectTransform.sizeDelta = new Vector2(260f, 160f);
            _goalCount.rectTransform.anchoredPosition = new Vector2(-Design.ContentWidth * 0.25f + 96f, 26f);
            UiBuilder.TextShadow(_goalCount, 0.4f, -0.3f, 0.45f);
            _goalWord = UiBuilder.Label(_goal, "Word", "", Design.Label, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _goalWord.rectTransform.sizeDelta = new Vector2(260f, 60f);
            _goalWord.rectTransform.anchoredPosition = new Vector2(-Design.ContentWidth * 0.25f + 96f, -70f);

            var divider = UiBuilder.Image(_goal, "Divider", Art.Panel(0f), Design.Hairline);
            divider.type = Image.Type.Simple;
            divider.rectTransform.sizeDelta = new Vector2(3f, GoalCard - 80f);
            divider.rectTransform.anchoredPosition = new Vector2(Design.ContentWidth * 0.14f, 0f);

            _moves = UiBuilder.Label(_goal, "Moves", "0", Design.Display * 0.8f, Design.TextPrimary, Design.FontDisplay,
                tracking: Design.TrackingDisplay);
            _moves.rectTransform.sizeDelta = new Vector2(300f, 160f);
            _moves.rectTransform.anchoredPosition = new Vector2(Design.ContentWidth * 0.32f, 26f);
            UiBuilder.TextShadow(_moves, 0.4f, -0.3f, 0.45f);
            _movesWord = UiBuilder.Label(_goal, "MovesWord", Str.MovesLabel, Design.Label, Design.TextSecondary, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _movesWord.rectTransform.sizeDelta = new Vector2(300f, 60f);
            _movesWord.rectTransform.anchoredPosition = new Vector2(Design.ContentWidth * 0.32f, -70f);

            // The best stars so far, or what is new on this level.
            _stars = UiBuilder.Node(Body, "Stars");
            _stars.sizeDelta = new Vector2(Design.ContentWidth, StarsRow);
            for (int i = 0; i < 3; i++)
            {
                var star = UiBuilder.Image(_stars, "Star" + i, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                float size = i == 1 ? 88f : 74f;
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.rectTransform.anchoredPosition = new Vector2((i - 1) * 100f, i == 1 ? 6f : -4f);
                _starImages[i] = star;
            }

            _intro = UiBuilder.Node(Body, "Intro");
            _intro.sizeDelta = new Vector2(520f, StarsRow - 16f);
            UiBuilder.Panel(_intro, "Fill", _intro.sizeDelta, Design.SurfaceGroup, (StarsRow - 16f) * 0.5f);
            _introIcon = UiBuilder.Image(_intro, "Icon", Art.Tile, Design.TileGlow);
            _introIcon.type = Image.Type.Simple;
            _introIcon.rectTransform.sizeDelta = new Vector2(56f, 56f);
            _introIcon.rectTransform.anchoredPosition = new Vector2(-200f, 0f);
            _introLabel = UiBuilder.Label(_intro, "Label", "", Design.Label, Design.Mint, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _introLabel.rectTransform.sizeDelta = new Vector2(380f, StarsRow - 16f);
            _introLabel.rectTransform.anchoredPosition = new Vector2(30f, 2f);
            _introLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // The win streak: three flames and what they give.
            _streak = SheetKit.Group(Body, "Streak", 0f, Design.RowHeight);
            SheetKit.RowIcon(_streak, "Icon", Icons.Flame, Design.TintOrange, SheetKit.RowLeft + Design.RowIcon * 0.5f, 0f);
            var streakName = SheetKit.RowLabel(_streak, "Name", Str.WinStreak, SheetKit.RowText, 420f, 20f, Design.Label);
            streakName.rectTransform.sizeDelta = new Vector2(420f, 60f);
            _streakGift = SheetKit.RowLabel(_streak, "Gift", "", SheetKit.RowText, 420f, -30f, Design.Caption, Design.Gold);
            _streakGift.rectTransform.sizeDelta = new Vector2(420f, 50f);
            for (int i = 0; i < _flames.Length; i++)
            {
                var flame = UiBuilder.Image(_streak, "Flame" + i, Icons.Flame, Design.TintOrange);
                flame.type = Image.Type.Simple;
                flame.rectTransform.sizeDelta = new Vector2(64f, 64f);
                flame.rectTransform.anchoredPosition = new Vector2(SheetKit.RowRight - 32f - (_flames.Length - 1 - i) * 72f, 0f);
                _flames[i] = flame;
            }

            _boosterHeading = UiBuilder.Label(Body, "Boosters", Str.Boosters, Design.Caption, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel).rectTransform;
            _boosterHeading.sizeDelta = new Vector2(Design.ContentWidth, 56f);

            float tileWidth = (Design.ContentWidth - Design.Space3 * 2f) / 3f;
            _tiles = new BoosterTile[3];
            for (int i = 0; i < 3; i++)
            {
                var kind = (Booster)i;
                float x = -Design.ContentWidth * 0.5f + tileWidth * 0.5f + i * (tileWidth + Design.Space3);
                _tiles[i] = BoosterTile.Build(Body, kind, new Vector2(tileWidth, TileHeight), x, 0f);
                _tiles[i].Button.Clicked += () => Toggle(kind);
            }

            _play = UiBuilder.Button(Body, "Play", new Vector2(Design.ContentWidth, Design.ButtonLg), UiButton.Style.Primary,
                Str.Play, Design.Headline, Icons.Play);
            _play.Clicked += Play;
        }

        public void Prepare(int level)
        {
            _level = Mathf.Clamp(level, 1, LevelGenerator.LevelCount);
            _useMoves = _useCharge = false;
        }

        protected override void OnShow()
        {
            var def = LevelGenerator.Generate(_level, Design.PaletteSize);
            int world = LevelGenerator.WorldOf(_level);
            int hardness = LevelGenerator.HardnessOf(_level);

            var saved = RunStore.Peek(GameMode.Level);
            _resume = saved != null && saved.LevelNumber == _level;

            Sheet.Title.text = Str.LevelTitle(_level);
            Sheet.SetHero(hardness == 2 ? Icons.Crown : hardness == 1 ? Icons.Bolt : Icons.Flag,
                hardness > 0 ? LevelSelectScreen.HardColour(hardness) : Design.WorldColor(world));
            _worldDot.color = Design.WorldColor(world);
            _world.text = $"{Str.WorldN(world)} · {Str.Upper(Str.WorldName(world))}";

            _hardChip.gameObject.SetActive(hardness > 0);
            _hardFill.color = LevelSelectScreen.HardColour(hardness);
            _hardIcon.sprite = hardness == 2 ? Icons.Crown : Icons.Bolt;
            _hardLabel.text = hardness == 2 ? Str.VeryHard : Str.Hard;
            if (hardness > 0)
            {
                float width = FitChip(_hardChip, _hardIcon, _hardLabel, Design.Space3, Design.Space1, Design.ContentWidth * 0.4f);
                SheetKit.PinTop(_hardChip, 0f, Design.ContentWidth * 0.5f - width * 0.5f);
            }

            _goalIcon.sprite = GameHud.GoalSprite(def);
            _goalIcon.color = GameHud.GoalColor(def);
            _goalCount.text = def.Target.ToString();
            _goalWord.text = Str.GoalWord(def.Goal);
            _moves.text = def.MoveLimit.ToString();

            float top = WorldRow + Design.Space3;
            SheetKit.PinTop(_goal, top);
            top += GoalCard + Design.Space3;

            // Stars already won, or — on a level that brings something new — its picture and name.
            var intro = LevelSelectScreen.IntroSprite(_level);
            int stars = Progress.StarsFor(_level);
            bool showIntro = intro.sprite != null && stars == 0;
            _stars.gameObject.SetActive(!showIntro && stars > 0);
            _intro.gameObject.SetActive(showIntro);
            if (showIntro)
            {
                _introIcon.sprite = intro.sprite;
                _introIcon.color = intro.colour;
                _introLabel.text = $"{Str.New} · {Str.IntroName(_level)}";
                FitChip(_intro, _introIcon, _introLabel, Design.Space4, Design.Space2, Design.ContentWidth);
                SheetKit.PinTop(_intro, top + 8f);
                top += StarsRow + Design.Space2;
            }
            else if (stars > 0)
            {
                for (int i = 0; i < 3; i++) _starImages[i].color = i < stars ? Design.Gold : Design.SurfaceControl;
                SheetKit.PinTop(_stars, top);
                top += StarsRow + Design.Space2;
            }

            int streak = Progress.WinStreak;
            var (moves, charges) = Progress.StreakBonus(streak);
            for (int i = 0; i < _flames.Length; i++)
                _flames[i].color = i < streak ? Design.TintOrange : Design.SurfaceControl;
            _streakGift.text = streak > 0 ? Str.StreakGift(moves, charges) : Str.NoStreak;
            _streakGift.color = streak > 0 ? Design.Gold : Design.TextTertiary;
            SheetKit.PinTop(_streak, top);
            top += Design.RowHeight + Design.Space4;

            SheetKit.PinTop(_boosterHeading, top);
            top += 56f + Design.Space2;
            foreach (var tile in _tiles)
            {
                SheetKit.PinTop(tile.Button.Rect, top, tile.Button.Rect.anchoredPosition.x);
                RefreshTile(tile);
            }
            top += TileHeight + Design.Space5;

            _play.Label.text = _resume ? Str.Continue : Str.Play;
            SheetKit.PinTop(_play.Rect, top);
            top += Design.ButtonLg;

            Sheet.SetBodyHeight(top);

            // Built behind the sheet while it is read.
            System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.Generate(_level, Design.PaletteSize));
        }

        /// <summary>
        /// Sizes a chip to what it says: icon and word centred as one group, the pill around them.
        /// At a fixed width a short word ("HARD") sat far from its icon in a pill made for a long one.
        /// Returns the chip's width.
        /// </summary>
        static float FitChip(RectTransform chip, Image icon, TextMeshProUGUI label, float pad, float gap, float maxWidth)
        {
            float iconSize = icon.rectTransform.sizeDelta.x;
            float text = Mathf.Min(label.GetPreferredValues(label.text).x, maxWidth - pad * 2f - iconSize - gap);
            float group = iconSize + gap + text;
            float width = group + pad * 2f;
            float height = chip.sizeDelta.y;

            chip.sizeDelta = new Vector2(width, height);
            var fill = chip.Find("Fill") as RectTransform;
            if (fill != null) fill.sizeDelta = chip.sizeDelta;

            icon.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + iconSize * 0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(text, height);
            label.rectTransform.anchoredPosition = new Vector2(group * 0.5f - text * 0.5f, 2f);
            return width;
        }

        void RefreshTile(BoosterTile tile)
        {
            int count = Progress.BoosterCount(tile.Kind);
            tile.Count.text = count.ToString();

            // The hammer is taken in play, not picked here; a resumed attempt has already begun.
            bool pickable = tile.Kind != Booster.Hammer && !_resume && count > 0;
            tile.Button.SetEnabled(pickable || tile.Kind == Booster.Hammer);
            tile.Button.Interactable = pickable;

            bool on = tile.Kind == Booster.Moves ? _useMoves : tile.Kind == Booster.Charge && _useCharge;
            tile.SetSelected(on && pickable);
        }

        void Toggle(Booster kind)
        {
            Audio.PlayClick();
            if (kind == Booster.Moves) _useMoves = !_useMoves;
            else if (kind == Booster.Charge) _useCharge = !_useCharge;

            foreach (var tile in _tiles)
                if (tile.Kind == kind)
                {
                    RefreshTile(tile);
                    StartCoroutine(Tween.Punch(tile.Button.Content, 0.1f, 0.2f));
                }
        }

        void Play()
        {
            Audio.PlayClick();
            App.PlayLevel(_level, _useMoves, _useCharge);
        }

#if PRIZMA_AUTOTEST
        public void AutoToggle(Booster kind) => Toggle(kind);
#endif
    }

    /// <summary>
    /// A world's chest, opened: what is inside, and one button to take it. The boosters it holds
    /// are what the player spends on the hard levels of the next world.
    /// </summary>
    public sealed class ChestScreen : SheetScreen
    {
        const float Hero = 300f;
        const float TileHeight = 244f;

        int _world;
        TextMeshProUGUI _world_label;
        RectTransform _hero;
        Image _heroDisc;
        BoosterTile[] _tiles;
        UiButton _collect;
        bool _taken;

        protected override void Build()
        {
            BuildSheet(Str.WorldChest, null, Design.Gold);

            _world_label = UiBuilder.Label(Body, "World", "", Design.Label, Design.TextSecondary, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _world_label.rectTransform.sizeDelta = new Vector2(Design.ContentWidth, 64f);
            SheetKit.PinTop(_world_label.rectTransform, 0f);

            _hero = UiBuilder.Node(Body, "Hero");
            _hero.sizeDelta = new Vector2(Hero, Hero);
            SheetKit.PinTop(_hero, 64f + Design.Space3);
            var halo = UiBuilder.Image(_hero, "Halo", Art.Halo, Design.Gold.WithAlpha(0.8f));
            halo.type = Image.Type.Simple;
            halo.rectTransform.sizeDelta = new Vector2(Hero * 1.5f, Hero * 1.5f);
            _heroDisc = UiBuilder.Image(_hero, "Disc", Art.Node, Design.Gold);
            _heroDisc.type = Image.Type.Simple;
            _heroDisc.rectTransform.sizeDelta = new Vector2(Hero * 0.72f, Hero * 0.72f);
            var chest = UiBuilder.Image(_hero, "Chest", Icons.Chest, new Color(0.36f, 0.2f, 0.02f));
            chest.type = Image.Type.Simple;
            chest.rectTransform.sizeDelta = new Vector2(Hero * 0.42f, Hero * 0.42f);

            float top = 64f + Design.Space3 + Hero + Design.Space4;
            float tileWidth = (Design.ContentWidth - Design.Space3 * 2f) / 3f;
            _tiles = new BoosterTile[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -Design.ContentWidth * 0.5f + tileWidth * 0.5f + i * (tileWidth + Design.Space3);
                _tiles[i] = BoosterTile.Build(Body, (Booster)i, new Vector2(tileWidth, TileHeight), x, top);
                _tiles[i].Button.Interactable = false;
            }

            top += TileHeight + Design.Space5;
            _collect = UiBuilder.Button(Body, "Collect", new Vector2(Design.ContentWidth, Design.ButtonLg), UiButton.Style.Primary,
                Str.Collect, Design.Headline);
            SheetKit.PinTop(_collect.Rect, top);
            _collect.Clicked += Collect;
            top += Design.ButtonLg;
            Sheet.SetBodyHeight(top);
        }

        public void Prepare(int world) => _world = world;

        protected override void OnShow()
        {
            _taken = false;
            // A sheet closed mid-shake left the chest turned and swollen.
            _hero.localEulerAngles = Vector3.zero;
            _hero.localScale = Vector3.one;
            _world_label.text = $"{Str.WorldN(_world)} · {Str.Upper(Str.WorldName(_world))}";
            _heroDisc.color = Design.Gold;
            var (moves, charge, hammer) = Progress.ChestContents(_world);
            int[] amounts = { moves, charge, hammer };
            for (int i = 0; i < 3; i++) _tiles[i].Count.text = "+" + amounts[i];
            _collect.SetEnabled(Progress.ChestReady(_world));
            StartCoroutine(Tween.Scale(_hero, Vector3.one * 0.6f, Vector3.one, 0.4f, Ease.OutBack, 0.1f));
        }

        void Collect()
        {
            if (_taken || !Progress.ChestReady(_world)) return;
            _taken = true;
            Progress.OpenChest(_world);
            Audio.PlayBadge();
            App.Vibrate();
            StartCoroutine(CollectRoutine());
        }

        IEnumerator CollectRoutine()
        {
            // The chest shakes, swells and bursts with light before the boosters count up.
            for (float t = 0f; t < 0.42f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.42f;
                _hero.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 7f) * 9f * (1f - k));
                _hero.localScale = Vector3.one * (1f + 0.12f * k);
                yield return null;
            }

            _hero.localEulerAngles = Vector3.zero;
            _heroDisc.color = SheetKit.Tinted(Design.Gold, Color.white, 0.4f);
            StartCoroutine(Tween.Scale(_hero, Vector3.one * 1.3f, Vector3.one, 0.4f, Ease.OutBack));

            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].Count.text = Progress.BoosterCount((Booster)i).ToString();
                StartCoroutine(Tween.Punch(_tiles[i].Button.Content, 0.2f, 0.3f));
                yield return new WaitForSecondsRealtime(0.1f);
            }

            yield return new WaitForSecondsRealtime(0.7f);
            if (IsVisible) App.CloseModal();
        }
    }
}
