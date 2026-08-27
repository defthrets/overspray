using System;
using System.Drawing;
using GTA;
using GTA.Native;
using Overspray.Core;
using Control = GTA.Control;

namespace Overspray.UI
{
    /// <summary>
    /// Ten colours and two buttons.
    ///
    /// THIS USED TO BE A FULL HSV PICKER -- hue strip, saturation and brightness bars, a
    /// two-dimensional field drawn as six hundred and eighty rectangles. It was nice, and it
    /// was wrong: a can of spray paint is a thing you grab, and every second between wanting to
    /// paint something and painting it was being spent operating a colour tool.
    ///
    /// Ten is enough. It is more than a real can offers, and it takes one keypress to cross,
    /// which is the actual test.
    /// </summary>
    internal sealed class Picker
    {
        private const float Pad = 0.022f;
        private const float SwatchH = 0.075f;
        private const float ButtonH = 0.044f;

        private static readonly Color Ink = Color.FromArgb(255, 236, 236, 236);
        private static readonly Color Dim = Color.FromArgb(255, 132, 132, 132);
        private static readonly Color Back = Color.FromArgb(238, 12, 12, 12);
        private static readonly Color Line = Color.FromArgb(255, 80, 80, 80);
        private static readonly Color Warn = Color.FromArgb(255, 214, 78, 62);

        /// <summary>
        /// The ten.
        ///
        /// Spread round the wheel rather than picked for prettiness, so whatever somebody has
        /// in mind when they think "I want that X" has something near it.
        /// </summary>
        private static readonly Color[] Colours =
        {
            Color.FromArgb(255, 228,  46,  46),   // red
            Color.FromArgb(255, 244, 130,  30),   // orange
            Color.FromArgb(255, 245, 218,  50),   // yellow
            Color.FromArgb(255, 122, 214,  56),   // lime
            Color.FromArgb(255,  40, 180, 120),   // green
            Color.FromArgb(255,  50, 190, 226),   // cyan
            Color.FromArgb(255,  52, 110, 226),   // blue
            Color.FromArgb(255, 140,  76, 220),   // purple
            Color.FromArgb(255, 240, 100, 180),   // pink
            Color.FromArgb(255, 245, 245, 245)    // white
        };

        private static readonly string[] Names =
        {
            "red", "orange", "yellow", "lime", "green",
            "cyan", "blue", "purple", "pink", "white"
        };

        private enum Row { Swatches, Take, Clear }

        private readonly Settings _cfg;
        private readonly Paint.Marks _marks;

        private int _pick = 3;
        private Row _row = Row.Swatches;
        private int _openedAt;

        /// <summary>
        /// Whether the clear button has been pressed once already.
        ///
        /// It throws away every mark in the world and there is no undo, so it asks. One press
        /// arms it, the second does it, and moving off the row disarms it again -- which is the
        /// cheapest confirmation there is and does not cost a second screen.
        /// </summary>
        private bool _armed;

        public Picker(Settings cfg, Paint.Marks marks)
        {
            _cfg = cfg;
            _marks = marks;
        }

        public bool IsOpen { get; private set; }

        public Color Colour => Colours[_pick];

        /// <summary>
        /// Kept so the sprayer's maths does not change shape.
        ///
        /// The spread dial went with the rest of it: the size comes from how far the wall is,
        /// and a multiplier on top of that was a second number doing the first one's job.
        /// </summary>
        public float Scale => 1f;

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }

            IsOpen = true;
            _openedAt = Game.GameTime;
            _armed = false;

            Hud.Sound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _armed = false;

            Hud.Sound("BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Update()
        {
            if (!IsOpen) return;

            Lock();

            // A beat before it takes input, or the key that opened it also moves the cursor.
            if (Game.GameTime - _openedAt < 180) return;

            if (Tapped(Control.PhoneCancel)) { Close(); return; }

            if (Tapped(Control.PhoneUp)) Move(-1);
            if (Tapped(Control.PhoneDown)) Move(1);

            if (_row == Row.Swatches)
            {
                if (Tapped(Control.PhoneLeft)) Step(-1);
                if (Tapped(Control.PhoneRight)) Step(1);
            }

            if (!Tapped(Control.PhoneSelect)) return;

            switch (_row)
            {
                case Row.Swatches:
                    // Picking IS choosing. There is nothing to confirm.
                    Close();
                    break;

                case Row.Take:
                    // Equipped as well as given: somebody who just asked for one wants it in
                    // his hands, not filed in a wheel he now has to open.
                    Paint.Can.Give(true);
                    Hud.Sound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    break;

                case Row.Clear:
                    if (!_armed)
                    {
                        _armed = true;
                        Hud.Sound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        break;
                    }

                    _marks.Clear();
                    _armed = false;

                    Hud.Sound("BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    Log.Info("Every wall wiped from the picker.");
                    break;
            }
        }

        private void Move(int by)
        {
            var n = (int)_row + by;
            if (n < 0) n = 2;
            if (n > 2) n = 0;

            _row = (Row)n;

            // Walking away from the clear button forgets that it was armed.
            _armed = false;

            Hud.Sound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        private void Step(int by)
        {
            _pick = (_pick + by + Colours.Length) % Colours.Length;
            Hud.Sound("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Draw()
        {
            if (!IsOpen) return;

            var w = Hud.X(0.42f);
            var h = Pad * 2f + 0.040f + SwatchH + 0.020f + ButtonH * 2f + 0.008f + 0.030f;

            var left = 0.5f - w * 0.5f;
            var top = 0.5f - h * 0.5f;

            Hud.Box(left, top, w, h, Back);
            Hud.Box(left, top, w, 0.0035f, Colour);

            var x = left + Hud.X(Pad);
            var inner = w - Hud.X(Pad) * 2f;
            var y = top + Pad;

            Hud.Text("OVERSPRAY", x, y, 0.42f, Ink);
            Hud.Text(Names[_pick], x + inner - Hud.X(0.070f), y + 0.004f, 0.34f, Colour);

            y += 0.040f;

            // ---- the ten ----
            var gap = Hud.X(0.005f);
            var each = (inner - gap * (Colours.Length - 1)) / Colours.Length;

            for (var i = 0; i < Colours.Length; i++)
            {
                var sx = x + i * (each + gap);
                var on = _row == Row.Swatches && i == _pick;

                // The chosen one stands taller as well as brighter. On a row of ten small
                // squares a highlight ring alone is easy to lose against a pale swatch.
                var sh = on ? SwatchH : SwatchH - 0.014f;
                var sy = y + (SwatchH - sh);

                Hud.Box(sx, sy, each, sh, Colours[i]);
                Hud.Frame(sx, sy, each, sh, 0.0012f, Line);

                if (on) Hud.Frame(sx - 0.0022f, sy - 0.0022f, each + 0.0044f, sh + 0.0044f, 0.0026f, Ink);
            }

            y += SwatchH + 0.020f;

            // ---- take one ----
            var has = Paint.Can.Has();

            Button(x, y, inner, _row == Row.Take,
                   has ? "TAKE ANOTHER EXTINGUISHER" : "TAKE AN EXTINGUISHER",
                   "ENTER", Ink, Colour);

            y += ButtonH + 0.008f;

            // ---- wipe it all ----
            var marks = _marks.Count;

            Button(x, y, inner, _row == Row.Clear,
                   _armed
                       ? "PRESS AGAIN -- THIS CANNOT BE UNDONE"
                       : "CLEAR EVERY WALL" + (marks > 0 ? "  (" + marks + ")" : ""),
                   _armed ? "SURE?" : "ENTER",
                   _armed ? Warn : Ink,
                   _armed ? Warn : Colour);

            Hud.Text("ARROWS  choose      ENTER  take      BACKSPACE  close",
                     x, top + h - 0.024f, 0.27f, Dim);
        }

        private void Button(float x, float y, float w, bool active, string label, string hint,
                            Color labelOn, Color hintColour)
        {
            Hud.Box(x, y, w, ButtonH,
                    active ? Color.FromArgb(255, 42, 42, 42) : Color.FromArgb(255, 24, 24, 24));

            if (active) Hud.Frame(x, y, w, ButtonH, 0.0026f, labelOn);

            Hud.Text(label, x + Hud.X(0.014f), y + 0.011f, 0.35f, active ? labelOn : Dim);
            Hud.Text(hint, x + w - Hud.X(0.042f), y + 0.012f, 0.30f, active ? hintColour : Dim);
        }

        // ---- input -------------------------------------------------------------

        private static bool Tapped(Control c)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)c);
        }

        /// <summary>Holds off everything the panel is standing on top of.</summary>
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
    }
}
