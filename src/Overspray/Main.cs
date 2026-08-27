using System;
using System.Windows.Forms;
using GTA;
using Overspray.Core;
using Overspray.Paint;
using Overspray.UI;

namespace Overspray
{
    /// <summary>
    /// A fire extinguisher that sprays paint.
    ///
    /// Hold the trigger with the extinguisher out and whatever is in front of you takes a
    /// splatter, out to five metres, on any surface the world will let a probe land on. The
    /// further the wall, the wider the splatter -- which is the whole reason it reads as spray
    /// rather than as decals appearing.
    ///
    /// Nothing here talks to any other mod and nothing here is loaded from anywhere but the
    /// BCL and SHVDN.
    /// </summary>
    public sealed class Main : Script
    {
        private readonly Settings _cfg;
        private readonly Marks _marks;
        private readonly Sprayer _sprayer;
        private readonly Picker _picker;
        private readonly Can _can;

        private int _lastSave;
        private bool _parked;

        public Main()
        {
            try
            {
                // Fully qualified. Script exposes an inherited `Settings` property that
                // otherwise wins name resolution over our own -- the same trap hoodrich has a
                // comment about, and it presents as "no argument given for 'filename'", which
                // is a message about a class nobody wrote.
                _cfg = Core.Settings.Load();

                _marks = new Marks(_cfg);
                _sprayer = new Sprayer(_cfg, _marks);
                _picker = new Picker(_cfg);
                _can = new Can();

                _sprayer.Armed = _cfg.PaintOnByDefault;

                if (_cfg.Persist) Load();

                Interval = 0;
                Tick += OnTick;
                KeyDown += OnKey;
                Aborted += OnAborted;

                Log.Info(Build.Name + " " + Build.Version + " loaded. " +
                         _cfg.ToggleKey + " arms it, " + _cfg.MenuKey + " picks a colour. " +
                         "Paint mode is " + (_sprayer.Armed ? "ON" : "OFF") + " to start.");
            }
            catch (Exception ex)
            {
                _parked = true;
                Log.Error("Failed to start; disabled for this session.", ex);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_parked || _cfg == null || !_cfg.Enabled) return;

            try
            {
                _picker.Update();
                _picker.Draw();

                // NOT WHILE THE PICKER IS UP. The panel eats the controls, and a trigger held
                // through a menu is a wall painted by accident while somebody chooses a colour.
                if (_picker.IsOpen)
                {
                    _sprayer.Stop();
                }
                else
                {
                    _sprayer.Colour = _picker.Colour;
                    _sprayer.Scale = _picker.Scale;
                    _sprayer.Update();
                }

                // The can takes the nearest of the game's eight tints, and only while armed --
                // an extinguisher that stays hot pink after you switch paint off is a mod
                // leaving its fingerprints on somebody else's weapon.
                if (_cfg.TintTheCan) _can.Match(_picker.Colour, _sprayer.Armed);

                Badge();

                _marks.Sweep();

                if (!_cfg.Persist) return;

                // Only when there is something new to write, and not often.
                var now = Game.GameTime;
                if (now - _lastSave < 30000) return;

                _lastSave = now;
                Save();
            }
            catch (Exception ex)
            {
                Log.Error("Tick threw.", ex);
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (_parked || _cfg == null || !_cfg.Enabled) return;
            if (e.KeyCode == _cfg.ToggleKey)
            {
                _sprayer.Armed = !_sprayer.Armed;

                if (!_sprayer.Armed)
                {
                    _sprayer.Stop();
                    _can.Reset();
                }

                UI.Hud.Sound(_sprayer.Armed ? "SELECT" : "BACK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                return;
            }

            if (e.KeyCode != _cfg.MenuKey) return;

            // The picker is the front door, so it hands you one on the way in. Vanilla leaves
            // extinguishers lying about in fire stations, which is a scavenger hunt before the
            // mod can be used at all.
            if (_cfg.GiveOne) Can.Give(false);

            _picker.Toggle();
        }

        /// <summary>
        /// A line in the corner, but only while the thing is in his hands.
        ///
        /// A MODE YOU CANNOT SEE IS A MODE YOU FORGET YOU ARE IN, and the two failure reports
        /// this avoids are opposite: "it sprayed paint all over a fire I was putting out" and
        /// "it stopped working". Both are the same question -- which mode am I in -- and both
        /// go away the moment the screen answers it without being asked.
        ///
        /// Nothing at all when the extinguisher is away, because then it is not a mode, it is
        /// a setting for a tool nobody is holding.
        /// </summary>
        private void Badge()
        {
            if (!Can.Out()) return;

            var on = _sprayer.Armed;
            var c = on ? _picker.Colour : System.Drawing.Color.FromArgb(255, 150, 150, 150);

            UI.Hud.Box(0.012f, 0.760f, UI.Hud.X(0.006f), 0.030f, c);

            UI.Hud.Text(on ? "PAINT" : "EXTINGUISHER",
                        0.024f, 0.762f, 0.32f, c);

            UI.Hud.Text(on
                        ? _cfg.ToggleKey + " off      " + _cfg.MenuKey + " colour"
                        : _cfg.ToggleKey + " to paint",
                        0.024f, 0.784f, 0.26f, System.Drawing.Color.FromArgb(255, 140, 140, 140));
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                if (_sprayer != null) _sprayer.Stop();

                // Saved BEFORE the decals come off, or the record is written after the thing it
                // is a record of has been taken down.
                if (_cfg != null && _cfg.Persist) Save();

                if (_marks != null) _marks.LetGo();
            }
            catch
            {
                // Teardown. Nothing left to tell.
            }
        }

        private void Save()
        {
            try { JsonFile.Write(Paths.SaveFile, _marks.ToJson()); }
            catch (Exception ex) { Log.Error("Could not save the paint.", ex); }
        }

        private void Load()
        {
            try
            {
                var doc = JsonFile.Read(Paths.SaveFile);
                if (doc != null) _marks.LoadFrom(doc);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read the paint.", ex);
            }
        }
    }
}
