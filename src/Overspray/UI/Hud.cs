using System;
using System.Drawing;
using GTA;
using GTA.Native;

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
