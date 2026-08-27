using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;
using Overspray.Core;

namespace Overspray.UI
{
    /// <summary>
    /// Rectangles and text, and nothing else.
    ///
    /// Deliberately small. This mod draws one panel; it does not need a UI framework and it
    /// certainly does not need somebody else's, which would be a second assembly in scripts\
    /// that can lose a version fight with whatever else the player has installed.
    /// </summary>
    internal static class Hud
    {
        public const int FontCondensed = 4;
        public const int FontChalet = 0;

        /// <summary>
        /// The screen's shape, so a square comes out square.
        ///
        /// Everything the game draws is in 0..1 of the screen on both axes, which means a
        /// width and a height that are the same number are the same number and NOT the same
        /// size. A colour square laid out without this is a colour rectangle on every monitor
        /// anybody owns.
        /// </summary>
        public static float Aspect
        {
            get
            {
                try { return Function.Call<float>(Hash.GET_ASPECT_RATIO, false); }
                catch { return 16f / 9f; }
            }
        }

        /// <summary>A height-fraction converted to the width-fraction that looks the same.</summary>
        public static float X(float h)
        {
            var a = Aspect;
            return a <= 0.01f ? h : h / a;
        }

        /// <summary>Filled rectangle, placed by its centre -- which is how DRAW_RECT works.</summary>
        public static void Rect(float cx, float cy, float w, float h, Color c)
        {
            Function.Call(Hash.DRAW_RECT, cx, cy, w, h, (int)c.R, (int)c.G, (int)c.B, (int)c.A);
        }

        /// <summary>Filled rectangle placed by its TOP-LEFT, which is how a person lays out a panel.</summary>
        public static void Box(float left, float top, float w, float h, Color c)
        {
            Rect(left + w * 0.5f, top + h * 0.5f, w, h, c);
        }

        /// <summary>A one-pixel-ish outline, drawn as four thin boxes.</summary>
        public static void Frame(float left, float top, float w, float h, float thick, Color c)
        {
            Box(left, top, w, thick, c);
            Box(left, top + h - thick, w, thick, c);
            Box(left, top, thick, h, c);
            Box(left + w - thick, top, thick, h, c);
        }

        public static void Text(string text, float x, float y, float scale, Color c,
                                bool centre = false, int font = FontCondensed)
        {
            if (string.IsNullOrEmpty(text)) return;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, (int)c.R, (int)c.G, (int)c.B, (int)c.A);
            Function.Call(Hash.SET_TEXT_CENTRE, centre);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, centre ? 0 : 1);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
            Function.Call(Hash.SET_TEXT_DROP_SHADOW);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        /// <summary>
        /// Text ending at a given edge, rather than starting at a given point.
        ///
        /// THE FIXED-OFFSET VERSION OF THIS IS A BUG WAITING FOR A LONGER WORD. Drawing a
        /// right-hand value by starting it a guessed distance in from the edge works for
        /// "ENTER" and pushes "SPRAY CAN" out through the side of the panel, because the guess
        /// was really a measurement of one particular string.
        ///
        /// SET_TEXT_WRAP is not optional here and the native's own documentation says so:
        /// right justification aligns to the wrap END, and with no wrap set that is the far
        /// right of the SCREEN, not of the panel.
        /// </summary>
        public static void TextRight(string text, float right, float y, float scale, Color c,
                                     int font = FontCondensed)
        {
            if (string.IsNullOrEmpty(text)) return;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, (int)c.R, (int)c.G, (int)c.B, (int)c.A);
            Function.Call(Hash.SET_TEXT_CENTRE, false);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 2);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, right);
            Function.Call(Hash.SET_TEXT_DROP_SHADOW);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, right, y);
        }

        /// <summary>How bright a colour reads. Rec. 601, which is plenty for "can I see this".</summary>
        public static float Luma(Color c)
        {
            return (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;
        }

        /// <summary>
        /// The same colour, lifted until it can be read against a dark panel.
        ///
        /// A SWATCH CAN BE ANY COLOUR. TEXT CANNOT. Black paint drawn as black text on a black
        /// panel is a blank space exactly where the name of the colour should be -- and naming
        /// the colour is most of what the picker is for, since the can itself only ever shows
        /// the nearest of eight tints.
        ///
        /// Lifted toward white rather than raised in value, so a dark blue stays blue instead
        /// of going grey: pushing the channels up evenly is what turns near-black to charcoal.
        /// The floor sits below every hue in the palette but well above black, so this is a
        /// no-op for ten of the eleven and only ever fires where it has to.
        /// </summary>
        public static Color Legible(Color c)
        {
            const float Floor = 0.35f;

            var l = Luma(c);
            if (l >= Floor) return c;

            var t = 1f - l / Floor;

            return Color.FromArgb(c.A,
                                  (int)(c.R + (255 - c.R) * t),
                                  (int)(c.G + (255 - c.G) * t),
                                  (int)(c.B + (255 - c.B) * t));
        }

        /// <summary>
        /// The scaled draw space CustomSprite.ScaledDraw works in.
        ///
        /// 720, which is ScriptHookVDotNet's number and not a guess -- positions handed to a
        /// scaled draw are in this space, not in the 0..1 one everything else here uses.
        /// </summary>
        private const float ScaledHeight = 720f;

        private static readonly Dictionary<string, GTA.UI.CustomSprite> Pictures =
            new Dictionary<string, GTA.UI.CustomSprite>();

        /// <summary>
        /// A PNG off disk, once.
        /// </summary>
        private static GTA.UI.CustomSprite Load(string file)
        {
            GTA.UI.CustomSprite found;

            // The null is cached too. A missing file must be a miss ONCE, not a failed disk
            // hit and a log line every frame for the rest of the session.
            if (Pictures.TryGetValue(file, out found)) return found;

            Pictures[file] = null;

            try
            {
                var path = System.IO.Path.Combine(Paths.Icons, file);

                if (!System.IO.File.Exists(path))
                {
                    Log.Info("No art at " + path + "; drawing without it.");
                    return null;
                }

                found = new GTA.UI.CustomSprite(path, new SizeF(32f, 32f), new PointF(0f, 0f),
                                                Color.White, 0f, true);

                Pictures[file] = found;
                Log.Info("Art loaded: " + file + ".");

                return found;
            }
            catch (Exception ex)
            {
                Log.Info("Art '" + file + "' would not load: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// A picture, by its CENTRE, tinted, optionally turned.
        ///
        /// White-on-transparent art tinted at draw time, which is why one logo file serves
        /// every colour in the picker instead of eleven of them.
        ///
        /// Returns false when there is nothing to draw, so the caller can fall back to text
        /// rather than leaving a hole where the mark should be.
        /// </summary>
        public static bool Picture(string file, float cx, float cy, float w, float h,
                                   float spin, Color c)
        {
            var sprite = Load(file);
            if (sprite == null) return false;

            try
            {
                var wide = w * GTA.UI.Screen.ScaledWidth;
                var tall = h * ScaledHeight;

                if (wide < 1f || tall < 1f) return false;

                sprite.Size = new SizeF(wide, tall);
                sprite.Position = new PointF(cx * GTA.UI.Screen.ScaledWidth, cy * ScaledHeight);
                sprite.Color = c;
                sprite.Rotation = spin;

                sprite.ScaledDraw();
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug("Art '" + file + "' would not draw: " + ex.Message);
                return false;
            }
        }

        /// <summary>The same colour at a different alpha, for fades.</summary>
        public static Color Fade(Color c, float t)
        {
            if (t <= 0f) return Color.FromArgb(0, c.R, c.G, c.B);
            if (t >= 1f) return c;

            return Color.FromArgb((int)(c.A * t), c.R, c.G, c.B);
        }

        /// <summary>
        /// Hue, saturation and value to a colour.
        ///
        /// Written out rather than pulled from anywhere, because System.Drawing can go from a
        /// colour to a hue and not back, and a picker needs the direction it does not have.
        /// </summary>
        public static Color FromHsv(float h, float s, float v)
        {
            h = h - (float)Math.Floor(h);          // wrap, so the hue bar is a loop
            if (s < 0f) s = 0f; else if (s > 1f) s = 1f;
            if (v < 0f) v = 0f; else if (v > 1f) v = 1f;

            var i = (int)(h * 6f);
            var f = h * 6f - i;

            var p = v * (1f - s);
            var q = v * (1f - f * s);
            var t = v * (1f - (1f - f) * s);

            float r, g, b;

            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }

            return Color.FromArgb(255, (int)(r * 255f), (int)(g * 255f), (int)(b * 255f));
        }

        public static void Sound(string name, string set)
        {
            try { Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, set, true); }
            catch { /* silence is survivable */ }
        }
    }
}
