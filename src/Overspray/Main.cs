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


                if (_cfg.Persist) Load();

                Interval = 0;
                Tick += OnTick;
                KeyDown += OnKey;
                Aborted += OnAborted;

                Log.Info(Build.Name + " " + Build.Version + " loaded. " + _cfg.MenuKey +
                         " opens the picker; take an extinguisher from it and spray.");
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
                if (_cfg.TintTheCan) _can.Match(_picker.Colour, _cfg.PaintEnabled);

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
            // ONE KEY. Taking an extinguisher is a row inside the picker rather than a second
            // binding, because a mod with two hotkeys has one the player has forgotten.
            if (e.KeyCode != _cfg.MenuKey) return;

            _picker.Toggle();
        }

        /// <summary>
        /// The colour loaded, in the corner, while the extinguisher is in his hands.
        ///
        /// Not a mode indicator any more -- there is no mode. It is the swatch, which is the
        /// one thing you cannot tell by looking at the world: the can is only ever the nearest
        /// of eight tints, so the actual colour about to come out of it lives here.
        ///
        /// Nothing at all when the extinguisher is away.
        /// </summary>
        private void Badge()
        {
            if (!Can.Out() || !_cfg.PaintEnabled) return;

            var c = _picker.Colour;

            UI.Hud.Box(0.012f, 0.762f, UI.Hud.X(0.020f), 0.026f, c);
            UI.Hud.Frame(0.012f, 0.762f, UI.Hud.X(0.020f), 0.026f, 0.0015f,
                         System.Drawing.Color.FromArgb(255, 30, 30, 30));

            UI.Hud.Text(_cfg.MenuKey.ToString(), 0.040f, 0.763f, 0.28f,
                        System.Drawing.Color.FromArgb(255, 150, 150, 150));
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
