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

        /// <summary>
        /// The mark and the can, and the shape of the files behind them.
        ///
        /// THE TAG IS TALLER THAN A WORDMARK, and the height had to roughly double because of
        /// it. Impact filled its box with letters; a tag spends the top of the box on an arrow,
        /// the bottom on drips, and gives the word itself about half. Keeping the old height
        /// would have kept the FILE the same size and shrunk the reading matter by half.
        /// </summary>
        private const float LogoH = 0.066f;
        private const float LogoAspect = 3.1416f;
        private const float CanH = 0.052f;
        private const float CanAspect = 0.4412f;

        /// <summary>How the panel arrives.</summary>
        private const int OpenMs = 200;

        /// <summary>How long a shake lasts, how hard, and roughly how often.</summary>
        private const int ShakeMs = 900;
        private const int ShakeSpreadMs = 3500;
        private const float ShakeDegrees = 13f;
        private const double ShakeCycles = 4.0;

        private static readonly Color Ink = Color.FromArgb(255, 236, 236, 236);
        private static readonly Color Dim = Color.FromArgb(255, 132, 132, 132);
        private static readonly Color Back = Color.FromArgb(238, 12, 12, 12);
        private static readonly Color Line = Color.FromArgb(255, 80, 80, 80);
        private static readonly Color Warn = Color.FromArgb(255, 214, 78, 62);

        /// <summary>
        /// The rack, which lives in the engine now rather than here.
        ///
        /// It used to be a pair of arrays in this file and another pair in Posted Up's phone
        /// app -- two hand-kept copies of one list, which is the exact thing the shared engine
        /// exists to stop. Adding a colour is one edit.
        /// </summary>
        private static readonly Paint.Swatch[] Tins = Paint.Rack.All;

        /// <summary>
        /// WHICH TOOL YOU TAKE IS THE WHOLE CHOICE. There used to be one spawn button and a
        /// separate switch for what it looked like, and that split one decision across two
        /// rows -- worse, it let the two disagree, so you could take "an extinguisher" and be
        /// handed the can's four-metre reach because the look was still set to can.
        ///
        /// Two buttons. The tool you ask for is the tool you get, reach and all.
        /// </summary>
        private enum Row { Swatches, TakeCan, TakeExt, Paint, Clear }

        private readonly Paint.PaintConfig _cfg;
        private readonly Paint.Marks _marks;

        private readonly Random _rng = new Random();

        private int _pick = 3;
        private Row _row = Row.Swatches;
        private int _openedAt;

        private int _shakeFrom = int.MinValue / 2;
        private int _nextShake;

        /// <summary>
        /// Whether the clear button has been pressed once already.
        ///
        /// It throws away every mark in the world and there is no undo, so it asks. One press
        /// arms it, the second does it, and moving off the row disarms it again -- which is the
        /// cheapest confirmation there is and does not cost a second screen.
        /// </summary>
        private bool _armed;

        /// <summary>
        /// Whether the ini still has the stand-down set, so turning the spray ON can retire it.
        ///
        /// Not shown anywhere. It used to spell itself out on the row -- "OFF, POSTED UP HAS
        /// IT ON THE PHONE" -- which is a paragraph where a state belongs. ON and OFF is what
        /// a switch says; the reason it started off is in the log, once, where a reason goes.
        /// </summary>
        public bool StandDown { set { _standDown = value; } }

        private bool _standDown;

        public Picker(Paint.PaintConfig cfg, Paint.Marks marks)
        {
            _cfg = cfg;
            _marks = marks;
        }

        public bool IsOpen { get; private set; }

        public Color Colour => Tins[_pick].Colour;

        /// <summary>How far the loaded paint scatters its shade. Zero for all but the two metallics.</summary>
        public float Sheen => Tins[_pick].Sheen;

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

                case Row.TakeCan:
                    Take(true);
                    break;

                case Row.TakeExt:
                    Take(false);
                    break;

                case Row.Paint:
                    Switch();
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

        /// <summary>
        /// Hands over the tool, and sets everything that follows from which one it is.
        ///
        /// The weapon is the same object either way -- the extinguisher, because it is what
        /// carries the aim camera and the trigger. What the choice actually sets is the look
        /// and, through it, the reach and the cone: four metres and a metre across for a can,
        /// ten and two for a hose.
        ///
        /// Equipped as well as given, because somebody who just asked for one wants it in his
        /// hands, not filed in a wheel he now has to open.
        /// </summary>
        private void Take(bool asCan)
        {
            _cfg.SprayCanLook = asCan;

            // Already true here by default -- an extinguisher paints, that is the whole mod --
            // but set explicitly so the engine's gate has one obvious owner in each host.
            _cfg.Armed = true;

            Paint.Can.Give(true);

            Hud.Sound("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
            Log.Info(asCan ? "Took a spray can." : "Took an extinguisher.");
        }

        /// <summary>
        /// Turns the paint on or off, and remembers which.
        ///
        /// WRITTEN BACK TO THE INI, because a switch that forgets is not a setting -- it is a
        /// thing you have to turn off again every time you load a save, which is worse than
        /// not having it. IniFile can write a single key without rewriting the file, so the
        /// comments and everything else somebody has tuned survive it.
        /// </summary>
        private void Switch()
        {
            _cfg.PaintEnabled = !_cfg.PaintEnabled;

            var word = _cfg.PaintEnabled ? "true" : "false";

            var kept = false;

            try
            {
                kept = IniFile.SetValue(Paths.Ini, "Paint", "PaintEnabled", word);

                // TURNING IT ON HAS TO STICK. With Posted Up installed the paint is switched
                // off at every launch on purpose, so without this, choosing to run both would
                // be undone by the next load and look like the switch simply did not work.
                // Using it is the explicit decision that retires the automatic default.
                if (_cfg.PaintEnabled && _standDown)
                {
                    IniFile.SetValue(Paths.Ini, "General", "StandDownForPostedUp", "false");

                    Log.Info("Both this and Posted Up's app will paint from now on. They are " +
                             "the same engine, so expect two of everything -- set " +
                             "StandDownForPostedUp back to true, or switch the spray off " +
                             "here, to undo it.");
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Could not write the ini: " + ex.Message);
            }

            Hud.Sound(_cfg.PaintEnabled ? "SELECT" : "BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");

            Log.Info("Spray paint " + (_cfg.PaintEnabled ? "on" : "off") +
                     (kept
                         ? ", and the ini remembers it."
                         : " -- the ini could not be written, so this lasts the session only."));
        }

        private void Move(int by)
        {
            var n = (int)_row + by;
            if (n < 0) n = 4;
            if (n > 4) n = 0;

            _row = (Row)n;

            // Walking away from the clear button forgets that it was armed.
            _armed = false;

            Hud.Sound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        private void Step(int by)
        {
            _pick = (_pick + by + Tins.Length) % Tins.Length;
            Hud.Sound("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        public void Draw()
        {
            if (!IsOpen) return;

            var w = Hud.X(0.45f);
            var h = Pad * 2f + LogoH + 0.004f + CanH + 0.014f + SwatchH + 0.020f
                    + ButtonH * 4f + 0.024f + 0.030f;

            var left = 0.5f - w * 0.5f;

            // ---- arriving ----
            //
            // Eased rather than linear, and it moves as well as fades. A panel that only
            // fades looks like a rendering glitch resolving; one that rises the last few
            // millimetres into place reads as a thing being put in front of you. Cubic
            // ease-out because the useful part of a 200ms move is the beginning.
            var age = Game.GameTime - _openedAt;
            var in01 = age >= OpenMs ? 1f : age / (float)OpenMs;
            var eased = 1f - (1f - in01) * (1f - in01) * (1f - in01);

            var top = 0.5f - h * 0.5f + (1f - eased) * 0.030f;

            // ---- the world, dimmed ----
            //
            // A panel floating over a bright street with nothing behind it is the single thing
            // that most makes a mod menu look bolted on. Everything the game itself opens dims
            // what is behind it first, and it costs one rectangle.
            Hud.Box(0f, 0f, 1f, 1f, Hud.Fade(Color.FromArgb(150, 0, 0, 0), eased));

            Hud.Box(left, top, w, h, Hud.Fade(Back, eased));

            var accent = Hud.Fade(Hud.Legible(Colour), eased);

            Hud.Box(left, top, w, 0.0035f, accent);

            // Three short runs off the accent bar, uneven, in the colour that is loaded. The
            // logo drips; the panel it sits on may as well.
            Hud.Box(left + w * 0.17f, top, 0.0030f, 0.0135f, accent);
            Hud.Box(left + w * 0.55f, top, 0.0030f, 0.0082f, accent);
            Hud.Box(left + w * 0.79f, top, 0.0030f, 0.0175f, accent);

            var x = left + Hud.X(Pad);
            var inner = w - Hud.X(Pad) * 2f;
            var y = top + Pad;

            var ink = Hud.Fade(Ink, eased);
            var dim = Hud.Fade(Dim, eased);
            var live = Hud.Fade(Hud.Legible(Colour), eased);

            // ---- the mark ----
            //
            // Drawn from a file so it can be a real wordmark rather than the word typed in a
            // game font, and tinted at draw time -- which is why one white PNG serves all
            // eleven colours. Falls back to the typed word if the art is missing, because a
            // panel with a hole where its name should be is worse than a plain heading.
            var logoW = Hud.X(LogoH) * LogoAspect;

            if (!Hud.Picture("logo.png", left + w * 0.5f, y + LogoH * 0.5f, logoW, LogoH, 0f, ink))
            {
                Hud.Text("OVERSPRAY", x, y, 0.42f, ink, centre: false);
            }

            Hud.TextRight(Tins[_pick].Name, x + inner, y + 0.004f, 0.34f, live);

            y += LogoH + 0.004f;

            // ---- the can, having a shake ----
            Can(left + w * 0.5f, y, eased);

            y += CanH + 0.014f;

            // ---- the rack ----
            var gap = Hud.X(0.005f);
            var each = (inner - gap * (Tins.Length - 1)) / Tins.Length;

            for (var i = 0; i < Tins.Length; i++)
            {
                var sx = x + i * (each + gap);

                // MARKED WHATEVER ROW YOU ARE ON. It used to light up only while the cursor
                // was on the swatch row, so stepping down to a button left nothing on screen
                // saying which colour was loaded -- and the one word that did say so is at the
                // far end of the header.
                var on = i == _pick;
                var focused = _row == Row.Swatches;

                // The chosen one stands taller as well as brighter. On a row of thirteen
                // small squares a highlight ring alone is easy to lose against a pale swatch.
                var sh = on ? SwatchH : SwatchH - 0.014f;
                var sy = y + (SwatchH - sh);

                Chip(sx, sy, each, sh, Tins[i], eased);

                // A near-black swatch on a near-black panel is an empty slot rather than a
                // colour, so the outline brightens as the swatch darkens -- the border is the
                // only thing saying there is anything there at all.
                Hud.Frame(sx, sy, each, sh, 0.0012f,
                          Hud.Luma(Tins[i].Colour) < 0.18f ? Dim : Line);

                if (on)
                {
                    // A slow breath on the ring while the cursor is actually on this row.
                    // Small on purpose -- it should catch the eye of somebody looking for the
                    // selection, not pull it away from somebody reading a button.
                    var beat = focused
                        ? 0.78f + 0.22f * (float)Math.Sin(Game.GameTime / 260.0)
                        : 1f;

                    Hud.Frame(sx - 0.0022f, sy - 0.0022f, each + 0.0044f, sh + 0.0044f, 0.0026f,
                              Hud.Fade(focused ? Ink : Dim, eased * beat));
                }
            }

            y += SwatchH + 0.020f;

            // ---- take one, or the other ----
            //
            // The right-hand word says which one you are already carrying, so the panel
            // answers "what have I got" without you having to close it and look.
            var has = Paint.Can.Has();

            Button(x, y, inner, _row == Row.TakeCan,
                   "TAKE A SPRAY CAN",
                   has && _cfg.SprayCanLook ? "IN HAND" : "ENTER",
                   ink, live, eased);

            y += ButtonH + 0.008f;

            Button(x, y, inner, _row == Row.TakeExt,
                   "TAKE AN EXTINGUISHER",
                   has && !_cfg.SprayCanLook ? "IN HAND" : "ENTER",
                   ink, live, eased);

            y += ButtonH + 0.008f;

            // ---- on or off ----
            //
            // This turns the PAINT off, not the script. The script has to stay alive or the
            // key that opens this panel stops working too, and then the only way back is
            // editing a file -- which is a fine way to lose somebody who just wanted to try
            // spraying without it.
            //
            // Everything else does stop: no marks, no jet, no reticle, no can in his hand and
            // no hidden weapon. The extinguisher goes back to being the game's.
            Button(x, y, inner, _row == Row.Paint,
                   "SPRAY PAINT",
                   _cfg.PaintEnabled ? "ON" : "OFF",
                   ink,
                   Hud.Fade(_cfg.PaintEnabled ? Hud.Legible(Colour) : Warn, eased), eased);

            y += ButtonH + 0.008f;

            // ---- wipe it all ----
            var marks = _marks.Count;

            Button(x, y, inner, _row == Row.Clear,
                   _armed
                       ? "PRESS AGAIN -- THIS CANNOT BE UNDONE"
                       : "CLEAR EVERY WALL" + (marks > 0 ? "  (" + marks + ")" : ""),
                   _armed ? "SURE?" : "ENTER",
                   Hud.Fade(_armed ? Warn : Ink, eased),
                   Hud.Fade(_armed ? Warn : Hud.Legible(Colour), eased), eased);

            Hud.Text("ARROWS  move      ENTER  choose      BACKSPACE  close",
                     x, top + h - 0.024f, 0.27f, dim);
        }

        /// <summary>
        /// One square on the rack.
        ///
        /// A METALLIC IS DRAWN AS THE RANGE IT SPRAYS, not as its middle. Chrome's middle is a
        /// mid-grey, and a flat mid-grey square sitting next to the white one says "grey
        /// paint" -- the player would only discover it was chrome by going and covering a wall
        /// with it. Five bands lit from the top is the least a gradient can be and still read
        /// as metal.
        ///
        /// Ten extra boxes on a panel already drawing thirty. Nothing here is near a budget.
        /// </summary>
        private static void Chip(float x, float y, float w, float h, Paint.Swatch s, float fade)
        {
            if (!s.Metallic)
            {
                Hud.Box(x, y, w, h, Hud.Fade(s.Colour, fade));
                return;
            }

            const int bands = 5;

            for (var i = 0; i < bands; i++)
            {
                // Brightest at the top down to darkest at the bottom, because light comes from
                // above and a chrome swatch shaded the other way reads as a hole in the panel.
                var t = 1f - i * 2f / (bands - 1);

                // The band is a hair taller than its share, so rounding cannot leave a seam of
                // panel showing between two of them.
                Hud.Box(x, y + h * i / bands, w, h / bands + 0.0004f,
                        Hud.Fade(Paint.Rack.Lit(s.Colour, t * s.Sheen), fade));
            }
        }

        /// <summary>
        /// The can under the mark, shaking every few seconds.
        ///
        /// THE SAME HABIT HE HAS IN THE WORLD. He shakes the can now and then while he is
        /// holding it, and this does the same on the same sort of interval -- so the panel is
        /// showing you the tool rather than decorating itself.
        ///
        /// A damped oscillation rather than a plain sine: it starts hard, rattles, and settles,
        /// which is what shaking a can looks like. A constant-amplitude wobble reads as a
        /// broken transform.
        /// </summary>
        private void Can(float cx, float top, float fade)
        {
            var now = Game.GameTime;

            if (now >= _nextShake)
            {
                _shakeFrom = now;
                _nextShake = now + ShakeMs + _rng.Next(ShakeSpreadMs);
            }

            var t = (now - _shakeFrom) / (float)ShakeMs;

            var spin = 0f;
            var bob = 0f;

            if (t < 1f)
            {
                // Falls away as it goes, so the last shake of a burst is the gentlest.
                var decay = 1f - t;
                decay *= decay;

                var wave = (float)Math.Sin(t * Math.PI * 2.0 * ShakeCycles);

                spin = wave * ShakeDegrees * decay;

                // Half the frequency on the bob, or it reads as buzzing rather than shaking.
                bob = (float)Math.Sin(t * Math.PI * 2.0 * ShakeCycles * 0.5) * 0.0035f * decay;
            }

            var canW = Hud.X(CanH) * CanAspect;

            if (!Hud.Picture("can.png", cx, top + CanH * 0.5f + bob, canW, CanH, spin,
                             Hud.Fade(Hud.Legible(Colour), fade)))
            {
                return;
            }

            // A shadow under it, squashed by the bob, so it is standing on the panel rather
            // than floating over it. Cheap, and it is the difference between a sprite and an
            // object.
            var lift = 1f - bob / 0.0035f * 0.35f;

            Hud.Box(cx - canW * 0.30f * lift, top + CanH + 0.002f,
                    canW * 0.60f * lift, 0.0022f,
                    Hud.Fade(Color.FromArgb(90, 0, 0, 0), fade));
        }

        private void Button(float x, float y, float w, bool active, string label, string hint,
                            Color labelOn, Color hintColour, float fade)
        {
            Hud.Box(x, y, w, ButtonH,
                    Hud.Fade(active ? Color.FromArgb(255, 42, 42, 42)
                                    : Color.FromArgb(255, 24, 24, 24), fade));

            if (active)
            {
                Hud.Frame(x, y, w, ButtonH, 0.0026f, labelOn);

                // A sheen travelling along the highlighted row, and only that one. It says
                // "this is the live line" without another colour or another border, and it
                // stops the panel looking frozen while you read it.
                var t = (Game.GameTime % 1600) / 1600f;
                var band = w * 0.22f;
                var at = x - band + (w + band * 2f) * t;

                var a = Math.Max(x, at);
                var b = Math.Min(x + w, at + band);

                if (b > a) Hud.Box(a, y, b - a, ButtonH, Hud.Fade(Color.FromArgb(26, 255, 255, 255), fade));
            }

            Hud.Text(label, x + Hud.X(0.014f), y + 0.011f, 0.35f,
                     active ? labelOn : Hud.Fade(Dim, fade));

            // Ends at the button's inner edge whatever the word is. The old version started it
            // a fixed distance in from the right, which is a measurement of the word "ENTER"
            // dressed up as a layout rule -- "SPRAY CAN" is wider and went out through the side.
            Hud.TextRight(hint, x + w - Hud.X(0.014f), y + 0.012f, 0.30f,
                          active ? hintColour : Hud.Fade(Dim, fade));
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
