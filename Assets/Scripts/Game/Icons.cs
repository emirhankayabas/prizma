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
    }
}
