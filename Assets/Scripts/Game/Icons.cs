using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Icon glyphs, drawn flat white at a consistent stroke weight and tinted at use.
    /// Flat and geometric on purpose: baked fake shading inside an icon is one of the things that
    /// makes a UI look a decade old.
    /// </summary>
    public static class Icons
    {
        const int Base = 96;
        const int Super = 3;

        /// <summary>Stroke weight as a fraction of the icon box, shared by every glyph.</summary>
        const float Stroke = 0.085f;

        static Sprite _gem;
        static Sprite _gear;
        static Sprite _speaker;
        static Sprite _speakerMuted;
        static Sprite _vibrate;
        static Sprite _play;
        static Sprite _pause;
        static Sprite _close;
        static Sprite _replay;
        static Sprite _home;
        static Sprite _list;

        public static Sprite Gem { get { if (_gem == null) _gem = BuildGem(); return _gem; } }
        public static Sprite Gear { get { if (_gear == null) _gear = BuildGear(); return _gear; } }
        public static Sprite Speaker { get { if (_speaker == null) _speaker = BuildSpeaker(false); return _speaker; } }
        public static Sprite SpeakerMuted { get { if (_speakerMuted == null) _speakerMuted = BuildSpeaker(true); return _speakerMuted; } }
        public static Sprite Vibrate { get { if (_vibrate == null) _vibrate = BuildVibrate(); return _vibrate; } }
        public static Sprite Play { get { if (_play == null) _play = BuildPlay(); return _play; } }
        public static Sprite Pause { get { if (_pause == null) _pause = BuildPause(); return _pause; } }
        public static Sprite Close { get { if (_close == null) _close = BuildClose(); return _close; } }
        public static Sprite Replay { get { if (_replay == null) _replay = BuildReplay(); return _replay; } }
        public static Sprite Home { get { if (_home == null) _home = BuildHome(); return _home; } }
        public static Sprite List { get { if (_list == null) _list = BuildList(); return _list; } }

        static Sprite _rotate;
        static Sprite _dice;
        static Sprite _bomb;
        static Sprite _star;
        static Sprite _lock;
        static Sprite _calendar;
        static Sprite _flame;
        static Sprite _chart;
        static Sprite _palette;
        static Sprite _hand;
        static Sprite _chevronLeft;
        static Sprite _chevronRight;
        static Sprite _check;
        static Sprite _flag;

        public static Sprite Rotate { get { if (_rotate == null) _rotate = BuildRotate(); return _rotate; } }
        public static Sprite Dice { get { if (_dice == null) _dice = BuildDice(); return _dice; } }
        public static Sprite Bomb { get { if (_bomb == null) _bomb = BuildBomb(); return _bomb; } }
        public static Sprite Star { get { if (_star == null) _star = BuildStar(); return _star; } }
        public static Sprite Lock { get { if (_lock == null) _lock = BuildLock(); return _lock; } }
        public static Sprite Calendar { get { if (_calendar == null) _calendar = BuildCalendar(); return _calendar; } }
        public static Sprite Flame { get { if (_flame == null) _flame = BuildFlame(); return _flame; } }
        public static Sprite Chart { get { if (_chart == null) _chart = BuildChart(); return _chart; } }
        public static Sprite Palette { get { if (_palette == null) _palette = BuildPalette(); return _palette; } }
        public static Sprite Hand { get { if (_hand == null) _hand = BuildHand(); return _hand; } }
        public static Sprite ChevronLeft { get { if (_chevronLeft == null) _chevronLeft = BuildChevron(false); return _chevronLeft; } }
        public static Sprite ChevronRight { get { if (_chevronRight == null) _chevronRight = BuildChevron(true); return _chevronRight; } }
        public static Sprite Check { get { if (_check == null) _check = BuildCheck(); return _check; } }
        public static Sprite Flag { get { if (_flag == null) _flag = BuildFlag(); return _flag; } }

        static Sprite _rows;

        /// <summary>Three stacked bars: the "clear this many lines" goal.</summary>
        public static Sprite Rows
        {
            get
            {
                if (_rows != null) return _rows;

                var r = New(out float s);
                for (int i = 0; i < 3; i++)
                    r.FillRoundRect(s * 0.12f, s * (0.16f + i * 0.26f), s * 0.76f, s * 0.16f, s * 0.06f, Color.white);

                _rows = Finish(r, "IconRows");
                return _rows;
            }
        }

        static Raster New(out float s)
        {
            s = Base * Super;
            return new Raster(Base * Super, Base * Super);
        }

        static Vector2 P(float s, float x, float y) => new Vector2(s * x, s * y);

        static Sprite Finish(Raster r, string name) => r.Downsample(Super).ToSprite(name);

        // ------------------------------------------------------------------ glyphs

        /// <summary>
        /// A flat gem. Kept as a single solid silhouette with no internal seam: at HUD sizes any
        /// interior detail turns to mush, and the outline alone has to carry it.
        /// </summary>
        static Sprite BuildGem()
        {
            var r = New(out float s);

            r.FillPolygon(new[]
            {
                P(s, 0.26f, 0.80f), P(s, 0.74f, 0.80f), P(s, 0.96f, 0.56f),
                P(s, 0.50f, 0.08f), P(s, 0.04f, 0.56f)
            }, Color.white);

            return Finish(r, "IconGem");
        }

        static Sprite BuildGear()
        {
            var r = New(out float s);
            float c = s * 0.5f;
            float inner = s * 0.29f;
            float outer = s * 0.46f;
            const int teeth = 6;

            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth;
                float w = Mathf.PI / teeth * 0.5f;

                r.FillPolygon(new[]
                {
                    new Vector2(c + Mathf.Cos(a - w) * inner, c + Mathf.Sin(a - w) * inner),
                    new Vector2(c + Mathf.Cos(a - w * 0.62f) * outer, c + Mathf.Sin(a - w * 0.62f) * outer),
                    new Vector2(c + Mathf.Cos(a + w * 0.62f) * outer, c + Mathf.Sin(a + w * 0.62f) * outer),
                    new Vector2(c + Mathf.Cos(a + w) * inner, c + Mathf.Sin(a + w) * inner)
                }, Color.white);
            }

            r.FillCircle(c, c, inner + s * 0.015f, Color.white);
            r.EraseCircle(c, c, s * 0.155f);

            return Finish(r, "IconGear");
        }

        static Sprite BuildSpeaker(bool muted)
        {
            var r = New(out float s);
            var white = Color.white;
            float t = s * Stroke;

            // The waves are drawn first and masked back to their right half; the body goes on
            // afterwards so the mask cannot eat into it.
            if (!muted)
            {
                r.StrokeCircle(s * 0.34f, s * 0.5f, s * 0.30f, t, white);
                r.StrokeCircle(s * 0.34f, s * 0.5f, s * 0.46f, t, white);
                r.EraseRect(0f, 0f, s * 0.58f, s);
            }

            r.FillRoundRect(s * 0.10f, s * 0.38f, s * 0.18f, s * 0.24f, s * 0.03f, white);
            r.FillPolygon(new[]
            {
                P(s, 0.26f, 0.38f), P(s, 0.48f, 0.16f), P(s, 0.48f, 0.84f), P(s, 0.26f, 0.62f)
            }, white);

            if (muted)
            {
                r.Line(P(s, 0.62f, 0.38f), P(s, 0.88f, 0.62f), t, white);
                r.Line(P(s, 0.88f, 0.38f), P(s, 0.62f, 0.62f), t, white);
            }

            return Finish(r, muted ? "IconSpeakerMuted" : "IconSpeaker");
        }

        static Sprite BuildVibrate()
        {
            var r = New(out float s);
            var white = Color.white;

            r.FillRoundRect(s * 0.34f, s * 0.14f, s * 0.32f, s * 0.72f, s * 0.08f, white);
            r.EraseRect(s * 0.40f, s * 0.22f, s * 0.20f, s * 0.56f);

            float t = s * Stroke * 0.8f;
            r.Line(P(s, 0.22f, 0.38f), P(s, 0.22f, 0.62f), t, white);
            r.Line(P(s, 0.10f, 0.32f), P(s, 0.10f, 0.68f), t, white);
            r.Line(P(s, 0.78f, 0.38f), P(s, 0.78f, 0.62f), t, white);
            r.Line(P(s, 0.90f, 0.32f), P(s, 0.90f, 0.68f), t, white);

            return Finish(r, "IconVibrate");
        }

        static Sprite BuildPause()
        {
            var r = New(out float s);
            float w = s * 0.16f;
            r.FillRoundRect(s * 0.26f, s * 0.20f, w, s * 0.60f, w * 0.35f, Color.white);
            r.FillRoundRect(s * 0.58f, s * 0.20f, w, s * 0.60f, w * 0.35f, Color.white);
            return Finish(r, "IconPause");
        }

        static Sprite BuildPlay()
        {
            var r = New(out float s);
            r.FillPolygon(new[] { P(s, 0.28f, 0.16f), P(s, 0.84f, 0.50f), P(s, 0.28f, 0.84f) }, Color.white);
            return Finish(r, "IconPlay");
        }

        static Sprite BuildClose()
        {
            var r = New(out float s);
            float t = s * Stroke * 1.2f;
            r.Line(P(s, 0.26f, 0.26f), P(s, 0.74f, 0.74f), t, Color.white);
            r.Line(P(s, 0.74f, 0.26f), P(s, 0.26f, 0.74f), t, Color.white);
            return Finish(r, "IconClose");
        }

        static Sprite BuildReplay()
        {
            var r = New(out float s);
            float c = s * 0.5f;

            r.StrokeCircle(c, c, s * 0.35f, s * Stroke, Color.white);
            r.ErasePolygon(new[] { P(s, 0.50f, 0.50f), P(s, 1.05f, 0.50f), P(s, 1.05f, 1.05f), P(s, 0.58f, 1.05f) });
            r.FillPolygon(new[] { P(s, 0.46f, 0.96f), P(s, 0.46f, 0.62f), P(s, 0.82f, 0.79f) }, Color.white);

            return Finish(r, "IconReplay");
        }

        static Sprite BuildHome()
        {
            var r = New(out float s);
            r.FillPolygon(new[] { P(s, 0.10f, 0.50f), P(s, 0.50f, 0.88f), P(s, 0.90f, 0.50f) }, Color.white);
            r.FillRoundRect(s * 0.22f, s * 0.14f, s * 0.56f, s * 0.38f, s * 0.05f, Color.white);
            r.EraseRect(s * 0.42f, s * 0.14f, s * 0.16f, s * 0.24f);
            return Finish(r, "IconHome");
        }

        static Sprite BuildList()
        {
            var r = New(out float s);
            float t = s * Stroke;

            for (int i = 0; i < 3; i++)
            {
                float y = 0.28f + i * 0.22f;
                r.FillCircle(s * 0.18f, s * y, t * 0.6f, Color.white);
                r.Line(P(s, 0.34f, y), P(s, 0.86f, y), t, Color.white);
            }

            return Finish(r, "IconList");
        }

        /// <summary>A block inside a clockwise arrow: "turn this piece".</summary>
        static Sprite BuildRotate()
        {
            var r = New(out float s);
            float c = s * 0.5f;

            r.StrokeCircle(c, c, s * 0.42f, s * Stroke, Color.white);
            r.ErasePolygon(new[] { P(s, 0.50f, 0.50f), P(s, 1.05f, 0.62f), P(s, 1.05f, 1.05f), P(s, 0.62f, 1.05f) });
            r.FillPolygon(new[] { P(s, 0.60f, 1.00f), P(s, 0.60f, 0.70f), P(s, 0.90f, 0.85f) }, Color.white);
            r.FillRoundRect(s * 0.34f, s * 0.34f, s * 0.32f, s * 0.32f, s * 0.07f, Color.white);

            return Finish(r, "IconRotate");
        }

        /// <summary>A die: fresh pieces are a new roll.</summary>
        static Sprite BuildDice()
        {
            var r = New(out float s);
            r.FillRoundRect(s * 0.14f, s * 0.14f, s * 0.72f, s * 0.72f, s * 0.16f, Color.white);

            float pip = s * 0.07f;
            r.EraseCircle(s * 0.33f, s * 0.67f, pip);
            r.EraseCircle(s * 0.50f, s * 0.50f, pip);
            r.EraseCircle(s * 0.67f, s * 0.33f, pip);
            r.EraseCircle(s * 0.67f, s * 0.67f, pip);
            r.EraseCircle(s * 0.33f, s * 0.33f, pip);

            return Finish(r, "IconDice");
        }

        static Sprite BuildBomb()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.44f, s * 0.42f, s * 0.32f, Color.white);
            r.FillRoundRect(s * 0.54f, s * 0.64f, s * 0.14f, s * 0.12f, s * 0.03f, Color.white);
            r.Line(P(s, 0.64f, 0.74f), P(s, 0.76f, 0.86f), s * Stroke * 0.8f, Color.white);

            // A four-point spark at the end of the fuse.
            r.FillPolygon(new[]
            {
                P(s, 0.86f, 0.98f), P(s, 0.89f, 0.91f), P(s, 0.96f, 0.88f), P(s, 0.89f, 0.85f),
                P(s, 0.86f, 0.78f), P(s, 0.83f, 0.85f), P(s, 0.76f, 0.88f), P(s, 0.83f, 0.91f)
            }, Color.white);

            // A highlight cut out of the body keeps it from reading as a plain dot.
            r.EraseCircle(s * 0.33f, s * 0.53f, s * 0.07f);
            return Finish(r, "IconBomb");
        }

        static Sprite BuildStar()
        {
            var r = New(out float s);
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? s * 0.47f : s * 0.20f;
                points[i] = new Vector2(s * 0.5f + Mathf.Cos(angle) * radius, s * 0.47f + Mathf.Sin(angle) * radius);
            }

            r.FillPolygon(points, Color.white);
            return Finish(r, "IconStar");
        }

        static Sprite BuildLock()
        {
            var r = New(out float s);
            r.StrokeCircle(s * 0.5f, s * 0.60f, s * 0.22f, s * Stroke * 1.1f, Color.white);
            r.EraseRect(0f, 0f, s, s * 0.56f);
            r.FillRoundRect(s * 0.20f, s * 0.10f, s * 0.60f, s * 0.48f, s * 0.08f, Color.white);
            r.EraseCircle(s * 0.5f, s * 0.36f, s * 0.07f);
            r.EraseRect(s * 0.47f, s * 0.20f, s * 0.06f, s * 0.16f);
            return Finish(r, "IconLock");
        }

        static Sprite BuildCalendar()
        {
            var r = New(out float s);
            r.FillRoundRect(s * 0.12f, s * 0.12f, s * 0.76f, s * 0.70f, s * 0.10f, Color.white);
            r.EraseRect(s * 0.20f, s * 0.20f, s * 0.60f, s * 0.44f);

            // Ring binders, and a single filled day: today.
            r.FillRoundRect(s * 0.28f, s * 0.74f, s * 0.08f, s * 0.18f, s * 0.04f, Color.white);
            r.FillRoundRect(s * 0.64f, s * 0.74f, s * 0.08f, s * 0.18f, s * 0.04f, Color.white);
            r.FillRoundRect(s * 0.52f, s * 0.26f, s * 0.18f, s * 0.16f, s * 0.03f, Color.white);
            return Finish(r, "IconCalendar");
        }

        static Sprite BuildFlame()
        {
            var r = New(out float s);
            var outer = new Vector2[24];
            for (int i = 0; i < outer.Length; i++)
            {
                float k = i / (float)outer.Length * Mathf.PI * 2f;
                // A teardrop: a circle whose top is pulled up into a point.
                float x = Mathf.Sin(k);
                float y = -Mathf.Cos(k);
                float lift = y > 0f ? y * y * 0.55f : 0f;
                outer[i] = new Vector2(s * (0.5f + x * 0.30f * (1f - lift * 0.9f)), s * (0.38f + y * 0.30f + lift * 0.62f));
            }

            r.FillPolygon(outer, Color.white);
            r.EraseCircle(s * 0.5f, s * 0.30f, s * 0.11f);
            return Finish(r, "IconFlame");
        }

        static Sprite BuildChart()
        {
            var r = New(out float s);
            float w = s * 0.16f;
            r.FillRoundRect(s * 0.16f, s * 0.14f, w, s * 0.30f, s * 0.04f, Color.white);
            r.FillRoundRect(s * 0.42f, s * 0.14f, w, s * 0.50f, s * 0.04f, Color.white);
            r.FillRoundRect(s * 0.68f, s * 0.14f, w, s * 0.72f, s * 0.04f, Color.white);
            return Finish(r, "IconChart");
        }

        static Sprite BuildPalette()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.5f, s * 0.5f, s * 0.40f, Color.white);
            r.EraseCircle(s * 0.66f, s * 0.30f, s * 0.09f);
            r.EraseCircle(s * 0.32f, s * 0.58f, s * 0.07f);
            r.EraseCircle(s * 0.48f, s * 0.72f, s * 0.07f);
            r.EraseCircle(s * 0.68f, s * 0.64f, s * 0.07f);
            return Finish(r, "IconPalette");
        }

        /// <summary>A pointing finger, used by the tutorial instead of any written instruction.</summary>
        static Sprite BuildHand()
        {
            var r = New(out float s);
            var white = Color.white;

            r.FillRoundRect(s * 0.38f, s * 0.42f, s * 0.15f, s * 0.52f, s * 0.075f, white); // index finger
            r.FillRoundRect(s * 0.52f, s * 0.36f, s * 0.13f, s * 0.30f, s * 0.065f, white);
            r.FillRoundRect(s * 0.64f, s * 0.33f, s * 0.12f, s * 0.27f, s * 0.06f, white);
            r.FillRoundRect(s * 0.75f, s * 0.30f, s * 0.11f, s * 0.24f, s * 0.055f, white);
            r.FillRoundRect(s * 0.34f, s * 0.06f, s * 0.52f, s * 0.40f, s * 0.14f, white);  // palm
            r.Line(P(s, 0.37f, 0.30f), P(s, 0.20f, 0.46f), s * 0.12f, white);            // thumb
            return Finish(r, "IconHand");
        }

        static Sprite BuildChevron(bool right)
        {
            var r = New(out float s);
            float t = s * Stroke * 1.3f;
            if (right)
            {
                r.Line(P(s, 0.38f, 0.20f), P(s, 0.66f, 0.50f), t, Color.white);
                r.Line(P(s, 0.66f, 0.50f), P(s, 0.38f, 0.80f), t, Color.white);
            }
            else
            {
                r.Line(P(s, 0.62f, 0.20f), P(s, 0.34f, 0.50f), t, Color.white);
                r.Line(P(s, 0.34f, 0.50f), P(s, 0.62f, 0.80f), t, Color.white);
            }

            return Finish(r, right ? "IconChevronRight" : "IconChevronLeft");
        }

        static Sprite BuildCheck()
        {
            var r = New(out float s);
            float t = s * Stroke * 1.4f;
            r.Line(P(s, 0.18f, 0.50f), P(s, 0.42f, 0.26f), t, Color.white);
            r.Line(P(s, 0.42f, 0.26f), P(s, 0.84f, 0.72f), t, Color.white);
            return Finish(r, "IconCheck");
        }

        static Sprite _share;

        /// <summary>Three nodes joined by two strokes: the platform's own share mark.</summary>
        public static Sprite Share { get { if (_share == null) _share = BuildShare(); return _share; } }

        static Sprite BuildShare()
        {
            var r = New(out float s);
            float t = s * Stroke;
            r.Line(P(s, 0.30f, 0.50f), P(s, 0.70f, 0.76f), t, Color.white);
            r.Line(P(s, 0.30f, 0.50f), P(s, 0.70f, 0.24f), t, Color.white);
            r.FillCircle(s * 0.30f, s * 0.50f, s * 0.13f, Color.white);
            r.FillCircle(s * 0.70f, s * 0.76f, s * 0.13f, Color.white);
            r.FillCircle(s * 0.70f, s * 0.24f, s * 0.13f, Color.white);
            return Finish(r, "IconShare");
        }

        // ------------------------------------------------------------------ the daily's glyphs

        static Sprite _target;
        static Sprite _bolt;
        static Sprite _crown;
        static Sprite _sun;
        static Sprite _moon;
        static Sprite _clock;
        static Sprite _bell;

        /// <summary>Two rings and a centre: solved on the first try.</summary>
        public static Sprite Target { get { if (_target == null) _target = BuildTarget(); return _target; } }

        /// <summary>A lightning bolt: a fast solve.</summary>
        public static Sprite Bolt { get { if (_bolt == null) _bolt = BuildBolt(); return _bolt; } }

        /// <summary>A three-point crown: the hardest day, a whole week.</summary>
        public static Sprite Crown { get { if (_crown == null) _crown = BuildCrown(); return _crown; } }

        public static Sprite Sun { get { if (_sun == null) _sun = BuildSun(); return _sun; } }
        public static Sprite Moon { get { if (_moon == null) _moon = BuildMoon(); return _moon; } }

        /// <summary>The daily's timer.</summary>
        public static Sprite Clock { get { if (_clock == null) _clock = BuildClock(); return _clock; } }

        /// <summary>The reminder.</summary>
        public static Sprite Bell { get { if (_bell == null) _bell = BuildBell(); return _bell; } }

        static Sprite BuildTarget()
        {
            var r = New(out float s);
            r.StrokeCircle(s * 0.5f, s * 0.5f, s * 0.38f, s * Stroke, Color.white);
            r.StrokeCircle(s * 0.5f, s * 0.5f, s * 0.22f, s * Stroke, Color.white);
            r.FillCircle(s * 0.5f, s * 0.5f, s * 0.08f, Color.white);
            return Finish(r, "IconTarget");
        }

        static Sprite BuildBolt()
        {
            var r = New(out float s);
            r.FillPolygon(new[]
            {
                P(s, 0.58f, 0.94f), P(s, 0.22f, 0.44f), P(s, 0.46f, 0.44f),
                P(s, 0.38f, 0.06f), P(s, 0.80f, 0.58f), P(s, 0.54f, 0.58f)
            }, Color.white);
            return Finish(r, "IconBolt");
        }

        static Sprite BuildCrown()
        {
            var r = New(out float s);
            r.FillPolygon(new[]
            {
                P(s, 0.12f, 0.24f), P(s, 0.88f, 0.24f), P(s, 0.90f, 0.74f), P(s, 0.68f, 0.50f),
                P(s, 0.50f, 0.82f), P(s, 0.32f, 0.50f), P(s, 0.10f, 0.74f)
            }, Color.white);
            r.FillCircle(s * 0.10f, s * 0.76f, s * 0.06f, Color.white);
            r.FillCircle(s * 0.50f, s * 0.84f, s * 0.06f, Color.white);
            r.FillCircle(s * 0.90f, s * 0.76f, s * 0.06f, Color.white);
            r.FillRoundRect(s * 0.12f, s * 0.10f, s * 0.76f, s * 0.10f, s * 0.04f, Color.white);
            return Finish(r, "IconCrown");
        }

        static Sprite BuildSun()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.5f, s * 0.5f, s * 0.20f, Color.white);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var c = new Vector2(s * 0.5f, s * 0.5f);
                r.Line(c + dir * s * 0.31f, c + dir * s * 0.43f, s * Stroke, Color.white);
            }
            return Finish(r, "IconSun");
        }

        static Sprite BuildMoon()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.46f, s * 0.5f, s * 0.36f, Color.white);
            r.EraseCircle(s * 0.66f, s * 0.62f, s * 0.30f);
            return Finish(r, "IconMoon");
        }

        static Sprite BuildClock()
        {
            var r = New(out float s);
            r.StrokeCircle(s * 0.5f, s * 0.5f, s * 0.38f, s * Stroke, Color.white);
            r.Line(P(s, 0.5f, 0.5f), P(s, 0.5f, 0.74f), s * Stroke, Color.white);
            r.Line(P(s, 0.5f, 0.5f), P(s, 0.66f, 0.40f), s * Stroke, Color.white);
            return Finish(r, "IconClock");
        }

        static Sprite BuildBell()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.5f, s * 0.56f, s * 0.26f, Color.white);
            r.FillPolygon(new[] { P(s, 0.24f, 0.56f), P(s, 0.76f, 0.56f), P(s, 0.84f, 0.26f), P(s, 0.16f, 0.26f) }, Color.white);
            r.FillRoundRect(s * 0.12f, s * 0.20f, s * 0.76f, s * 0.09f, s * 0.04f, Color.white);
            r.FillCircle(s * 0.5f, s * 0.12f, s * 0.08f, Color.white);
            r.FillCircle(s * 0.5f, s * 0.85f, s * 0.05f, Color.white);
            return Finish(r, "IconBell");
        }

        static Sprite _note;
        static Sprite _globe;

        /// <summary>An eighth note: the music row, told apart from the effects row's speaker.</summary>
        public static Sprite Note { get { if (_note == null) _note = BuildNote(); return _note; } }

        /// <summary>The language row.</summary>
        public static Sprite Globe { get { if (_globe == null) _globe = BuildGlobe(); return _globe; } }

        static Sprite BuildNote()
        {
            var r = New(out float s);
            r.FillCircle(s * 0.36f, s * 0.26f, s * 0.15f, Color.white);
            r.FillRoundRect(s * 0.44f, s * 0.26f, s * 0.08f, s * 0.62f, s * 0.04f, Color.white);
            r.FillPolygon(new[] { P(s, 0.44f, 0.88f), P(s, 0.76f, 0.70f), P(s, 0.76f, 0.56f), P(s, 0.52f, 0.68f) }, Color.white);
            return Finish(r, "IconNote");
        }

        static Sprite BuildGlobe()
        {
            var r = New(out float s);
            float t = s * Stroke;
            r.StrokeCircle(s * 0.5f, s * 0.5f, s * 0.38f, t, Color.white);
            r.Line(P(s, 0.14f, 0.5f), P(s, 0.86f, 0.5f), t, Color.white);

            // The meridian: an ellipse, drawn as a ring of short strokes.
            var previous = Vector2.zero;
            for (int i = 0; i <= 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                var point = new Vector2(s * (0.5f + Mathf.Cos(a) * 0.16f), s * (0.5f + Mathf.Sin(a) * 0.38f));
                if (i > 0) r.Line(previous, point, t, Color.white);
                previous = point;
            }

            return Finish(r, "IconGlobe");
        }

        // ------------------------------------------------------------------ the adventure's glyphs

        static Sprite _hammer;
        static Sprite _plusMoves;
        static Sprite _chest;
        static Sprite _map;

        /// <summary>A mallet: the booster that smashes one block.</summary>
        public static Sprite Hammer { get { if (_hammer == null) _hammer = BuildHammer(); return _hammer; } }

        /// <summary>A plus in a ring: more moves.</summary>
        public static Sprite PlusMoves { get { if (_plusMoves == null) _plusMoves = BuildPlusMoves(); return _plusMoves; } }

        /// <summary>A chest with its lid on: a world's reward.</summary>
        public static Sprite Chest { get { if (_chest == null) _chest = BuildChest(); return _chest; } }

        /// <summary>A winding road between two points: the adventure map.</summary>
        public static Sprite Map { get { if (_map == null) _map = BuildMap(); return _map; } }

        static Sprite BuildHammer()
        {
            var r = New(out float s);
            // The handle runs corner to corner; the head sits across its top end.
            r.Line(P(s, 0.20f, 0.12f), P(s, 0.58f, 0.54f), s * 0.11f, Color.white);
            r.FillPolygon(new[] { P(s, 0.38f, 0.70f), P(s, 0.64f, 0.96f), P(s, 0.94f, 0.66f), P(s, 0.68f, 0.40f) }, Color.white);
            r.EraseCircle(s * 0.66f, s * 0.68f, s * 0.05f);
            return Finish(r, "IconHammer");
        }

        static Sprite BuildPlusMoves()
        {
            var r = New(out float s);
            float t = s * Stroke;
            r.StrokeCircle(s * 0.5f, s * 0.5f, s * 0.42f, t, Color.white);
            r.FillRoundRect(s * 0.44f, s * 0.24f, s * 0.12f, s * 0.52f, s * 0.05f, Color.white);
            r.FillRoundRect(s * 0.24f, s * 0.44f, s * 0.52f, s * 0.12f, s * 0.05f, Color.white);
            return Finish(r, "IconPlusMoves");
        }

        static Sprite BuildChest()
        {
            var r = New(out float s);
            // Body, a domed lid, the band between them and a lock plate cut out of it.
            r.FillRoundRect(s * 0.10f, s * 0.12f, s * 0.80f, s * 0.42f, s * 0.07f, Color.white);
            r.FillRoundRect(s * 0.10f, s * 0.58f, s * 0.80f, s * 0.26f, s * 0.13f, Color.white);
            r.EraseRect(s * 0.44f, s * 0.36f, s * 0.12f, s * 0.26f);
            r.FillRoundRect(s * 0.46f, s * 0.40f, s * 0.08f, s * 0.14f, s * 0.03f, Color.white);
            return Finish(r, "IconChest");
        }

        static Sprite BuildMap()
        {
            var r = New(out float s);
            float t = s * Stroke;
            var previous = Vector2.zero;
            for (int i = 0; i <= 24; i++)
            {
                float k = i / 24f;
                var point = new Vector2(s * (0.5f + Mathf.Sin(k * Mathf.PI * 2f) * 0.22f), s * (0.18f + k * 0.64f));
                if (i > 0) r.Line(previous, point, t, Color.white);
                previous = point;
            }

            r.FillCircle(s * 0.5f, s * 0.14f, s * 0.10f, Color.white);
            r.FillCircle(s * 0.5f, s * 0.86f, s * 0.10f, Color.white);
            return Finish(r, "IconMap");
        }

        static Sprite BuildFlag()
        {
            var r = New(out float s);
            r.FillRoundRect(s * 0.20f, s * 0.08f, s * 0.08f, s * 0.84f, s * 0.04f, Color.white);
            r.FillPolygon(new[] { P(s, 0.26f, 0.90f), P(s, 0.84f, 0.74f), P(s, 0.26f, 0.52f) }, Color.white);
            return Finish(r, "IconFlag");
        }
    }
}
