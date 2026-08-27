using System;
using System.Drawing;
using GTA;
using GTA.Native;
using Overspray.Core;
using Control = GTA.Control;

namespace Overspray.UI
{
    /// <summary>
    /// Choosing a colour, properly.
    ///
    /// A row of eight preset swatches would have been half an hour's work and it is what most
    /// mods do. It is also the thing that makes them feel like a settings menu -- you are not
    /// picking a colour, you are picking one of somebody else's colours, and the one you wanted
    /// is never there.
    ///
    /// So this is a real picker: a hue strip and a saturation/value field, drawn as a grid of
    /// filled rectangles because that is the only drawing primitive the game gives a script.
    /// It costs a few hundred DRAW_RECT calls on the frames it is open, which is nothing, and
    /// it is the difference between a tool and a menu.
    ///
    /// The presets are still there. They are a shortcut, not the offer.
    /// </summary>
    internal sealed class Picker
    {
        // ---- layout, in height-fractions so it is the same shape on any monitor ----
        // Laid out on paper before the game saw it: the stack came to 0.639 against a panel
        // of 0.600, so the spread row and the hint line were hanging out of the bottom of the
        // box. In game that reads as text floating under a menu, which is a strange bug to
        // report and a stranger one to find.
        private const float PanelH = 0.66f;
        private const float Pad = 0.022f;

        private const float FieldH = 0.22f;      // the saturation/value square
        private const float HueH = 0.030f;       // the hue strip
        private const float SwatchH = 0.070f;    // the big preview

        /// <summary>
        /// How finely the gradients are drawn.
        ///
        /// Each cell is a DRAW_RECT, so this is a straight quality-for-calls trade: 34 x 20 is
        /// 680 rectangles and looks continuous at any sane resolution. Twice that is four times
        /// the calls to fix something nobody can see.
        /// </summary>
        private const int FieldCols = 34;
        private const int FieldRows = 20;
        private const int HueSteps = 60;

        private static readonly Color Ink = Color.FromArgb(255, 236, 236, 236);
        private static readonly Color Dim = Color.FromArgb(255, 132, 132, 132);
        private static readonly Color Back = Color.FromArgb(238, 12, 12, 12);
        private static readonly Color Line = Color.FromArgb(255, 90, 90, 90);

        /// <summary>Somewhere to start, and a shortcut for the obvious ones.</summary>
        private static readonly Color[] Presets =
        {
            Color.FromArgb(255, 235, 60, 60),
            Color.FromArgb(255, 245, 150, 35),
            Color.FromArgb(255, 245, 225, 55),
            Color.FromArgb(255, 70, 205, 90),
            Color.FromArgb(255, 60, 175, 235),
            Color.FromArgb(255, 130, 80, 220),
            Color.FromArgb(255, 240, 110, 190),
            Color.FromArgb(255, 250, 250, 250),
            Color.FromArgb(255, 20, 20, 20)
        };

        /// <summary>
        /// Five rows, and every one of them works the same way.
        ///
        /// THE FIELD IS TWO DIMENSIONAL AND THE CONTROLS ARE NOT. Up and down move between
        /// rows, so a 2D field driven by the arrow keys can only ever have one of its axes --
        /// which is how you end up with a picker where the brightness is whatever it was when
        /// you opened it and there is no way to say otherwise.
        ///
        /// So brightness is its own row. The field still draws in two dimensions with the
        /// cursor on it, because that is what makes a colour pickable at a glance; it just is
        /// not pretending to be steerable in two dimensions with controls that are not.
        /// </summary>
        private enum Row { Field, Bright, Hue, Presets, Size }

        private readonly Settings _cfg;

        private float _h = 0.35f;
        private float _s = 0.85f;
        private float _v = 0.90f;

        private Row _row = Row.Field;
        private int _preset;
        private int _openedAt;

        public Picker(Settings cfg)
        {
            _cfg = cfg;
        }

        public bool IsOpen { get; private set; }

        /// <summary>What the sprayer should be using.</summary>
        public Color Colour => Hud.FromHsv(_h, _s, _v);

        /// <summary>Nozzle pressure, 0.35x to 2.2x.</summary>
        public float Scale { get; private set; } = 1f;

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }

            IsOpen = true;
            _openedAt = Game.GameTime;
            Hud.Sound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            Hud.Sound("BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Update()
        {
            if (!IsOpen) return;

            Lock();

            // A beat after opening before it takes input, or the key that opened it moves the
            // cursor on the same frame.
            if (Game.GameTime - _openedAt < 180) return;

            if (Tapped(Control.PhoneCancel)) { Close(); return; }

            if (Tapped(Control.PhoneUp)) Move(-1);
            if (Tapped(Control.PhoneDown)) Move(1);

            // Held rather than tapped on the axes -- a colour picker you have to press two
            // hundred times to cross is a colour picker nobody crosses.
            var fast = Game.GameTime % 60 < 20;

            if (Held(Control.PhoneLeft) && fast) Nudge(-1);
            if (Held(Control.PhoneRight) && fast) Nudge(1);

            if (Tapped(Control.PhoneSelect) && _row == Row.Presets) TakePreset();
        }

        private void Move(int by)
        {
            var n = (int)_row + by;
            if (n < 0) n = 4;
            if (n > 4) n = 0;

            _row = (Row)n;
            Hud.Sound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        private void Nudge(int dir)
        {
            switch (_row)
            {
                case Row.Field:
                    _s = Clamp(_s + dir * 0.02f, 0f, 1f);
                    break;

                case Row.Bright:
                    _v = Clamp(_v + dir * 0.02f, 0.02f, 1f);
                    break;

                case Row.Hue:
                    _h += dir * 0.006f;
                    break;

                case Row.Presets:
                    _preset = (_preset + dir + Presets.Length) % Presets.Length;
                    break;

                case Row.Size:
                    Scale = Clamp(Scale + dir * 0.04f, 0.35f, 2.2f);
                    break;
            }
        }

        private void TakePreset()
        {
            var c = Presets[_preset];

            // Straight back to HSV, so the field and the strip land on the swatch that was
            // picked rather than staying where they were and disagreeing with the preview.
            float h, s, v;
            ToHsv(c, out h, out s, out v);

            _h = h; _s = s; _v = v;

            Hud.Sound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        // ---- drawing -----------------------------------------------------------

        public void Draw()
        {
            if (!IsOpen) return;

            var w = Hud.X(0.40f);
            var left = 0.5f - w * 0.5f;
            var top = 0.5f - PanelH * 0.5f;

            Hud.Box(left, top, w, PanelH, Back);
            Hud.Box(left, top, w, 0.0035f, Colour);

            var x = left + Hud.X(Pad);
            var inner = w - Hud.X(Pad) * 2f;
            var y = top + Pad;

            Hud.Text("OVERSPRAY", x, y, 0.44f, Ink);
            y += 0.045f;

            // ---- the saturation / value field ----
            Field(x, y, inner, FieldH);
            if (_row == Row.Field || _row == Row.Bright)
            {
                Hud.Frame(x - 0.002f, y - 0.002f, inner + 0.004f, FieldH + 0.004f, 0.0025f,
                          _row == Row.Field ? Ink : Line);
            }
            y += FieldH + 0.012f;

            // Saturation and brightness as their own labelled bars under the field, so both
            // axes are reachable with the same two keys as everything else.
            Bar("SATURATION", x, y, inner, _s, _row == Row.Field);
            y += 0.030f;

            Bar("BRIGHTNESS", x, y, inner, _v, _row == Row.Bright);
            y += 0.034f;

            // ---- the hue strip ----
            HueStrip(x, y, inner, HueH);
            if (_row == Row.Hue) Hud.Frame(x - 0.002f, y - 0.002f, inner + 0.004f, HueH + 0.004f, 0.0025f, Ink);
            y += HueH + 0.018f;

            // ---- presets ----
            Swatches(x, y, inner);
            y += 0.036f + 0.014f;

            // ---- the preview and the numbers ----
            var half = inner * 0.5f - Hud.X(0.006f);

            Hud.Box(x, y, half, SwatchH, Colour);
            Hud.Frame(x, y, half, SwatchH, 0.002f, Line);

            var c = Colour;
            Hud.Text("R " + c.R + "   G " + c.G + "   B " + c.B,
                     x + half + Hud.X(0.012f), y + 0.004f, 0.34f, Ink);

            Hud.Text("#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"),
                     x + half + Hud.X(0.012f), y + 0.030f, 0.30f, Dim);

            y += SwatchH + 0.016f;

            // ---- size ----
            SizeRow(x, y, inner);

            y += 0.052f;
            Hud.Text("ARROWS  move and adjust     ENTER  take a preset     BACKSPACE  close",
                     x, top + PanelH - 0.030f, 0.27f, Dim);
        }

        /// <summary>A labelled fill bar, for the two axes the field cannot steer on its own.</summary>
        private void Bar(string label, float left, float top, float w, float t, bool active)
        {
            Hud.Text(label, left, top - 0.003f, 0.28f, active ? Ink : Dim);

            var barLeft = left + Hud.X(0.075f);
            var barW = w - Hud.X(0.075f);

            Hud.Box(barLeft, top + 0.006f, barW, 0.005f, Color.FromArgb(255, 44, 44, 44));
            Hud.Box(barLeft, top + 0.006f, barW * t, 0.005f, Colour);

            if (active)
            {
                Hud.Box(barLeft + barW * t - Hud.X(0.002f), top + 0.002f, Hud.X(0.004f), 0.013f, Ink);
            }
        }

        /// <summary>The saturation/value square, as a grid of little filled cells.</summary>
        private void Field(float left, float top, float w, float h)
        {
            var cw = w / FieldCols;
            var ch = h / FieldRows;

            for (var col = 0; col < FieldCols; col++)
            {
                var s = (col + 0.5f) / FieldCols;

                for (var row = 0; row < FieldRows; row++)
                {
                    // Top is bright, bottom is dark, which is the way round every picker
                    // anybody has used works.
                    var v = 1f - (row + 0.5f) / FieldRows;

                    Hud.Box(left + col * cw, top + row * ch, cw + 0.0006f, ch + 0.0006f,
                            Hud.FromHsv(_h, s, v));
                }
            }

            // Where you are, as a ring rather than a dot -- a dot the colour of the thing
            // underneath it is invisible exactly when you need it.
            var px = left + _s * w;
            var py = top + (1f - _v) * h;

            Hud.Frame(px - Hud.X(0.008f), py - 0.008f, Hud.X(0.016f), 0.016f, 0.0022f, Color.Black);
            Hud.Frame(px - Hud.X(0.007f), py - 0.007f, Hud.X(0.014f), 0.014f, 0.0016f, Color.White);
        }

        private void HueStrip(float left, float top, float w, float h)
        {
            var cw = w / HueSteps;

            for (var i = 0; i < HueSteps; i++)
            {
                Hud.Box(left + i * cw, top, cw + 0.0006f, h, Hud.FromHsv((i + 0.5f) / HueSteps, 1f, 1f));
            }

            var px = left + (_h - (float)Math.Floor(_h)) * w;

            Hud.Box(px - Hud.X(0.0022f), top - 0.004f, Hud.X(0.0044f), h + 0.008f, Color.Black);
            Hud.Box(px - Hud.X(0.0012f), top - 0.003f, Hud.X(0.0024f), h + 0.006f, Color.White);
        }

        private void Swatches(float left, float top, float w)
        {
            var gap = Hud.X(0.006f);
            var each = (w - gap * (Presets.Length - 1)) / Presets.Length;

            for (var i = 0; i < Presets.Length; i++)
            {
                var sx = left + i * (each + gap);

                Hud.Box(sx, top, each, 0.036f, Presets[i]);

                if (_row == Row.Presets && i == _preset)
                {
                    Hud.Frame(sx - 0.002f, top - 0.002f, each + 0.004f, 0.040f, 0.0025f, Ink);
                }
                else
                {
                    Hud.Frame(sx, top, each, 0.036f, 0.0012f, Line);
                }
            }
        }

        /// <summary>
        /// The size dial, showing what it means rather than what it is.
        ///
        /// "1.35x" is a number about the mod. "0.9m at 2m, 3.6m at 4m" is a number about the
        /// wall in front of you, and it is the one that tells somebody whether to turn the dial.
        /// </summary>
        private void SizeRow(float left, float top, float w)
        {
            var at2 = _cfg.SizeAtOneMetre * (float)Math.Pow(2.0, _cfg.SpreadPower) * Scale;
            var at4 = _cfg.SizeAtOneMetre * (float)Math.Pow(4.0, _cfg.SpreadPower) * Scale;

            at2 = Clamp(at2, _cfg.MinSize, _cfg.MaxSize);
            at4 = Clamp(at4, _cfg.MinSize, _cfg.MaxSize);

            Hud.Text("SPREAD", left, top, 0.32f, _row == Row.Size ? Ink : Dim);

            Hud.Text(at2.ToString("0.0") + "m at 2m       " + at4.ToString("0.0") + "m at 4m",
                     left + Hud.X(0.075f), top, 0.32f, Ink);

            // The bar underneath, so the dial has a position and not just a number.
            var bar = w;
            var t = (Scale - 0.35f) / (2.2f - 0.35f);

            Hud.Box(left, top + 0.026f, bar, 0.006f, Color.FromArgb(255, 44, 44, 44));
            Hud.Box(left, top + 0.026f, bar * t, 0.006f, Colour);

            if (_row == Row.Size)
            {
                Hud.Box(left + bar * t - Hud.X(0.002f), top + 0.022f, Hud.X(0.004f), 0.014f, Ink);
            }
        }

        // ---- input -------------------------------------------------------------

        private static bool Tapped(Control c)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)c);
        }

        private static bool Held(Control c)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, (int)c);
        }

        /// <summary>
        /// Holds off everything the panel is standing on top of.
        ///
        /// The whole set rather than a handful, because the one that gets missed is always the
        /// one that fires -- this is a picker, not a screen worth spending an afternoon
        /// enumerating controls for.
        /// </summary>
        private static void Lock()
        {
            try
            {
                Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);

                foreach (var c in new[]
                         {
                             Control.PhoneUp, Control.PhoneDown, Control.PhoneLeft,
                             Control.PhoneRight, Control.PhoneSelect, Control.PhoneCancel,
                             Control.LookLeftRight, Control.LookUpDown
                         })
                {
                    Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, (int)c, true);
                }
            }
            catch
            {
                // A frame without the lock is a frame where the arrow keys also move him.
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static void ToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var d = max - min;

            v = max;
            s = max <= 0.0001f ? 0f : d / max;

            if (d <= 0.0001f) { h = 0f; return; }

            if (max == r) h = (g - b) / d / 6f;
            else if (max == g) h = (2f + (b - r) / d) / 6f;
            else h = (4f + (r - g) / d) / 6f;

            if (h < 0f) h += 1f;
        }
    }
}
