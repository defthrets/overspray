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
        /// <summary>
        /// How tall the mark is drawn.
        ///
        /// A MASTHEAD, not a billboard. It has been as tall as 0.066, which filled most of
        /// the panel's width and made the header the loudest thing on a screen whose job is
        /// choosing a colour.
        ///
        /// Small enough to be a mark rather than a headline, which is also what earns it the
        /// motion below: a wordmark this size can rock and re-spray without dragging the eye
        /// off the swatches, and one at 0.066 could not.
        ///
        /// The width still follows from LogoAspect, so anything that changes that has to be
        /// checked against this -- it has caught an overflow once already.
        /// </summary>
        private const float LogoH = 0.034f;
        private const float LogoAspect = 5.6517f;
        private const float CanH = 0.052f;
        private const float CanAspect = 0.4412f;

        /// <summary>How many times wider than tall a cap icon is. Printed by tools/make_art.py.</summary>
        private const float CapAspect = 0.6406f;

        /// <summary>How the panel arrives.</summary>
        private const int OpenMs = 200;

        /// <summary>
        /// The mark spraying itself on: how many frames there are, how long each is held, and
        /// roughly how often it happens again.
        ///
        /// ON A SLOW CYCLE, like the can's shake, and offset from it so the two are not doing
        /// something at the same moment -- a panel where everything moves together reads as one
        /// animation rather than as two objects.
        /// </summary>
        private const int SprayFrames = 8;
        private const int SprayFrameMs = 45;

        /// <summary>
        /// The glow: how far the halo spreads past the mark, how strong it gets, and how long
        /// a breath takes.
        ///
        /// COPIES OFFSET AROUND THE MARK, not scaled up behind it. There is no blur to be had
        /// here -- a sprite is drawn or it is not -- so a glow has to be built out of the same
        /// art drawn several times, and the two ways of doing that are not equally good.
        ///
        /// Scaling was the first attempt and it is WRONG for a wordmark, because a wordmark is
        /// six times wider than it is tall: growing it six percent puts eleven pixels on its
        /// width and two on its height, which is a horizontal smear rather than a halo. Offsets
        /// are the same distance in every direction by construction.
        ///
        /// Eight directions on two rings, the outer one half as strong. Rendered at the real
        /// size first -- four is visibly eight-pointed, and sixteen costs draws to fix a thing
        /// eight had already fixed.
        /// </summary>
        private const int GlowDirs = 8;
        private const int GlowRings = 2;
        private const float GlowRadius = 0.0035f;
        private const float GlowStrength = 0.30f;
        private const double GlowMs = 2900.0;

        /// <summary>
        /// The idle: a slow rock and a slower drift up and down, so the mark is alive between
        /// re-sprays rather than only during them.
        ///
        /// TWO PERIODS THAT DO NOT DIVIDE INTO EACH OTHER, on purpose. Rocking and bobbing on
        /// the same clock is a pendulum and the eye finds the loop in about two swings; on
        /// 2.6 and 4.1 seconds they drift in and out of phase and it never quite repeats.
        /// </summary>
        private const float RockDegrees = 1.6f;
        private const double RockMs = 2600.0;
        private const float BobHeight = 0.0035f;
        private const double BobMs = 4100.0;

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
        private enum Row { Swatches, Cap, TakeCan, TakeExt, Paint, Clear }

        /// <summary>
        /// Derived, because it was written out as a 4 in two places and this is the second time
        /// a row has been added to the middle of that enum.
        /// </summary>
        private static readonly int LastRow = Enum.GetValues(typeof(Row)).Length - 1;

        private readonly Paint.PaintConfig _cfg;
        private readonly Paint.Marks _marks;

        private readonly Random _rng = new Random();

        private int _pick = 3;
        private Row _row = Row.Swatches;
        private int _openedAt;

        private int _shakeFrom = int.MinValue / 2;
        private int _nextShake;

        private int _sprayFrom = int.MinValue / 2;

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

            // Sprays itself on when the panel opens, and only then. It used to do it again
            // every three to five seconds, which turned an arrival into a tic -- the mark
            // was redrawing itself while you were trying to read the row under it.
            _sprayFrom = _openedAt;

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

            // THE TWO ROWS THAT ARE DIALS, not buttons. Both describe what is loaded rather
            // than doing something, so both answer to left and right and neither needs Enter --
            // though Enter walks the cap along too, because somebody who has only ever pressed
            // Enter on this panel should not find one row silently inert.
            if (_row == Row.Swatches)
            {
                if (Tapped(Control.PhoneLeft)) Step(-1);
                if (Tapped(Control.PhoneRight)) Step(1);
            }

            if (_row == Row.Cap)
            {
                if (Tapped(Control.PhoneLeft)) Cycle(-1);
                if (Tapped(Control.PhoneRight)) Cycle(1);
            }

            if (!Tapped(Control.PhoneSelect)) return;

            switch (_row)
            {
                case Row.Swatches:
                    // Picking IS choosing. There is nothing to confirm.
                    Close();
                    break;

                case Row.Cap:
                    Cycle(1);
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
            if (n < 0) n = LastRow;
            if (n > LastRow) n = 0;

            _row = (Row)n;

            // Walking away from the clear button forgets that it was armed.
            _armed = false;

            Hud.Sound("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
        }

        /// <summary>
        /// Puts the next cap on, and remembers it.
        ///
        /// Written back for the same reason the on/off switch is: somebody who paints with a
        /// fat cap paints with a fat cap, and having to say so again every time the game loads
        /// is worse than not being offered the choice.
        /// </summary>
        private void Cycle(int by)
        {
            var caps = Paint.Caps.All.Length;

            _cfg.Cap = (_cfg.Cap + by % caps + caps) % caps;

            try
            {
                IniFile.SetValue(Paths.Ini, "Paint", "Cap", _cfg.Cap.ToString());
            }
            catch (Exception ex)
            {
                Log.Debug("Could not write the cap to the ini: " + ex.Message);
            }

            Hud.Sound("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
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
                    + ButtonH * 5f + 0.024f + 0.030f;

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

            // The frame if one is due, then the finished mark, then the typed word. Three deep
            // because the frames are the only art here that is not load-bearing: an install
            // missing them should still get a wordmark, not a hole where its name goes.
            var frame = Spraying();

            // Never still. The rock is under two degrees and the drift is three thousandths of
            // the screen -- small enough that nobody looking at a swatch notices, big enough
            // that the header is not a dead sticker when they do look at it.
            var clock = Game.GameTime;

            var spin = (float)Math.Sin(clock / RockMs * Math.PI * 2.0) * RockDegrees;
            var bob = (float)Math.Sin(clock / BobMs * Math.PI * 2.0) * BobHeight;

            var mx = left + w * 0.5f;
            var my = y + LogoH * 0.5f + bob;

            // ---- the glow, in whatever is loaded ----
            //
            // IT IS THE COLOUR YOU PICKED, which is the only reason a glow earns its place
            // here: the mark stops being decoration and becomes the biggest readout on the
            // panel of what is in the can. The swatch row says it in a square; this says it
            // across the whole header.
            //
            // Legible rather than raw, so black -- which is a real choice on this rack --
            // glows a dark grey instead of glowing nothing at all against a near-black panel.
            var breath = 0.72f + 0.28f * (float)Math.Sin(clock / GlowMs * Math.PI * 2.0);

            var halo = Hud.Legible(Colour);

            var lit = frame ?? "logo.png";

            // Outermost ring first so the nearer, brighter one lands on top of it.
            for (var ring = GlowRings; ring >= 1; ring--)
            {
                var soft = GlowStrength * (1f - (ring - 1) / (float)GlowRings) * breath * eased;

                var ry = GlowRadius * ring;
                var rx = Hud.X(ry);

                for (var d = 0; d < GlowDirs; d++)
                {
                    var a = d * Math.PI * 2.0 / GlowDirs;

                    Hud.Picture(lit,
                                mx + (float)Math.Cos(a) * rx,
                                my + (float)Math.Sin(a) * ry,
                                logoW, LogoH, spin, Hud.Fade(halo, soft));
                }
            }

            // And the mark itself on top, crisp and WHITE.
            //
            // The colour is the glow and the mark is the word. Drawing the letters in the
            // loaded colour too made the whole header one hue and the wordmark stopped being
            // a wordmark -- it read as a coloured smudge with a brighter middle. White on top
            // keeps the name legible at every colour on the rack, including the dark ones,
            // and lets the halo be the thing that carries what is in the can.
            if ((frame == null || !Hud.Picture(frame, mx, my, logoW, LogoH, spin, ink)) &&
                !Hud.Picture("logo.png", mx, my, logoW, LogoH, spin, ink))
            {
                Hud.Text("OVERSPRAY", x, y, 0.42f, ink, centre: false);
            }

            // Centred against the mark's row rather than sat at a fixed offset from its top,
            // which put it high the moment the mark got shorter.
            Hud.TextRight(Tins[_pick].Name, x + inner, y + (LogoH - 0.020f) * 0.5f, 0.34f, live);

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

            // ---- the nozzle ----
            //
            // A cap sets the NARROWEST line the can can draw, not the widest. That is what a
            // cap is: a fat one cannot do fine work however close you hold it, while the far
            // end stays governed by how far off the wall you are standing.
            CapRow(x, y, inner, ink, live, eased);

            y += ButtonH + 0.008f;

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
        /// Which frame of the reveal is up, or null once it has finished and the mark is just
        /// the mark.
        ///
        /// The frames are the SAME CANVAS as logo.png, uncropped, so handing over to it at the
        /// end is invisible -- a frame cropped to its own ink would be a different shape drawn
        /// into the same box, and the word would jump on the last step.
        /// </summary>
        private string Spraying()
        {
            var i = (Game.GameTime - _sprayFrom) / SprayFrameMs;

            return i >= 0 && i < SprayFrames ? "logo_" + i + ".png" : null;
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

        /// <summary>
        /// The cap row: three little buttons rather than a word.
        ///
        /// A WORD IS THE WRONG CONTROL FOR THIS. "STOCK" tells you nothing about what it does
        /// until you have tried all three and remembered, whereas three holes at three sizes
        /// tell you before you press anything -- and the hole IS the setting, since a cap is
        /// only ever the narrowest line the can can draw.
        ///
        /// Same house style as Posted Up's app icons, and the same trick: white art on
        /// transparent, tinted at draw time, so one file is the dim one and the loaded colour
        /// on the chosen one.
        /// </summary>
        private void CapRow(float x, float y, float w, Color ink, Color live, float fade)
        {
            var active = _row == Row.Cap;

            // CAPS ARE FOR THE CAN, and the row has to say so without saying so. The engine
            // has always known -- an extinguisher never consults a cap -- but the panel did
            // not show it, so the row looked like a setting that applied to whatever you were
            // holding.
            //
            // It says so by GOING QUIET while an extinguisher is the tool in hand -- the can,
            // the plate and all three caps down to a bit over a third. That is what every
            // interface does with a control that is not connected to anything at the moment,
            // and it needs no explaining.
            //
            // Still usable while dimmed. Choosing your cap before you pick the can up is a
            // reasonable thing to do and a row you could not touch would punish it.
            var lit = _cfg.SprayCanLook ? fade : fade * 0.42f;

            // Labelled like every other row and with no hint, because three pictures are going
            // where that word would have been.
            Button(x, y, w, active, "CAP SIZE", "", ink, live, fade);

            var caps = Paint.Caps.All;

            // The plate stays square so the three read as a row of buttons; the cap inside it
            // is drawn at its own proportions, because a cap squeezed into a square is a cap
            // that looks like somebody stood on it.
            var side = ButtonH - 0.012f;
            var wide = Hud.X(side);
            var gap = Hud.X(0.005f);

            var capH = side * 0.88f;
            var capW = Hud.X(capH) * CapAspect;

            var right = x + w - Hud.X(0.010f);
            var top = y + (ButtonH - side) * 0.5f;

            // Right to left, so the rightmost is the last one and the row grows leftward from
            // where the hint text would have ended. Laying it out the other way would put the
            // set at a different place on the row from every hint above and below it.
            for (var i = caps.Length - 1; i >= 0; i--)
            {
                var left = right - wide;
                var on = i == _cfg.Cap;

                // A lit plate behind the chosen one and nothing behind the others. A ring
                // round all three would make this row louder than the swatches above it, and
                // the swatches are the row that is meant to be loudest.
                if (on)
                {
                    Hud.Box(left, top, wide, side,
                            Hud.Fade(Color.FromArgb(255, 64, 64, 64), lit));
                }

                var tint = on ? Hud.Fade(live, lit) : Hud.Fade(Dim, lit);

                if (!Hud.Picture(caps[i].Icon, left + wide * 0.5f, top + side * 0.5f,
                                 capW, capH, 0f, tint))
                {
                    // No art in the folder. A plain square at the cap's own scale is the icon
                    // with its ring taken off, and still says which of the three this is --
                    // better than three identical gaps.
                    var d = side * 0.22f * (float)Math.Sqrt(caps[i].Width);

                    Hud.Box(left + (wide - Hud.X(d)) * 0.5f, top + (side - d) * 0.5f,
                            Hud.X(d), d, tint);
                }

                right = left - gap;
            }
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
