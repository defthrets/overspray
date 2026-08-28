using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;
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
        private readonly Bystanders _street = new Bystanders();
        private Law _law;
        private readonly Spraycan _spraycan;

        private int _lastSave;
        private bool _parked;

        /// <summary>Whether Posted Up is installed beside this. See the note in the constructor.</summary>
        private bool _alongsidePostedUp;

        public Main()
        {
            try
            {
                // Fully qualified. Script exposes an inherited `Settings` property that
                // otherwise wins name resolution over our own -- the same trap hoodrich has a
                // comment about, and it presents as "no argument given for 'filename'", which
                // is a message about a class nobody wrote.
                _cfg = Core.Settings.Load();

                _marks = new Marks(_cfg.Paint);
                _sprayer = new Sprayer(_cfg.Paint, _marks);
                _picker = new Picker(_cfg.Paint, _marks);
                _can = new Can();
                _spraycan = new Spraycan(_cfg.Paint);
                _law = new Law(_cfg.Paint);


                // THE KEY ALWAYS WORKS. This used to park the whole script when Posted Up
                // was installed, which stopped the conflict and also stopped F3 -- so the one
                // screen that could have explained any of it was the screen you could not
                // open. A mod that silently does nothing is worse than one that does the
                // wrong thing, because at least the wrong thing tells you it is there.
                //
                // So the paint STARTS off instead. Two copies of one engine both painting is
                // not a degraded experience, it is a different one -- two cans, two plumes,
                // two decals for every one you meant -- and it is very hard to diagnose from
                // inside the game, because everything looks like it works and merely looks
                // wrong. Off by default avoids that; the switch in the picker turns it on for
                // anybody who wants both, and that choice is written to the ini and sticks.
                _alongsidePostedUp =
                    System.IO.File.Exists(System.IO.Path.Combine(Paths.Scripts, "Hoodrich.dll"));

                if (_alongsidePostedUp && _cfg.StandDownForPostedUp)
                {
                    _cfg.Paint.PaintEnabled = false;

                    Log.Warn("Posted Up is installed here and has this built in as a Graffiti " +
                             "app on the phone, running the same engine. Both painting at once " +
                             "gives two cans, two plumes and two decals for every one you " +
                             "meant, so the spray is switched OFF to start with. F3 still " +
                             "works -- turn MOD on in there if you want both.");
                }

                _picker.StandDown = _alongsidePostedUp && _cfg.StandDownForPostedUp;

                if (_cfg.Persist) Load();

                Interval = 0;
                Tick += OnTick;
                KeyDown += OnKey;
                Aborted += OnAborted;

                // What this install will hold, read off DecalPatch's own ini if it is there.
                // Looked at and reported, never written -- see DecalCap.
                DecalCap.Look();

                Log.Info(Build.Name + " " + Build.Version + " by " + Build.By + " loaded. " + _cfg.MenuKey +
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
                Hello();

                _picker.Update();
                _picker.Draw();

                // NOT WHILE THE PICKER IS UP. The panel eats the controls, and a trigger held
                // through a menu is a wall painted by accident while somebody chooses a colour.
                // SWITCHED OFF IS A HANDS-OFF STATE, and that matters more here than it
                // looks: Posted Up runs this same engine, and if this one keeps maintaining
                // its own decals while switched off it spends pool slots that the app you
                // ARE using needs -- so its paint starts vanishing for no reason a player
                // could ever connect to a mod they turned off.
                //
                // Existing marks stay on the wall until the game streams them out. They are
                // not wiped, because turning a mod off should not destroy your work, and the
                // record is kept so switching back on brings them all back.
                if (_picker.IsOpen || !_cfg.Paint.PaintEnabled)
                {
                    _sprayer.Stop();
                }
                else
                {
                    _sprayer.Colour = _picker.Colour;
                    _sprayer.Sheen = _picker.Sheen;
                    _sprayer.Scale = _picker.Scale;

                    // Set BEFORE Update, because Update is what starts the plume, and a
                    // nozzle handed over a frame late is a plume that comes out of his wrist
                    // on the first press of every trigger pull.
                    _sprayer.Nozzle = _spraycan.Handle;

                    _sprayer.Update();
                }

                // The can takes the nearest of the game's eight tints, and only while armed --
                // an extinguisher that stays hot pink after you switch paint off is a mod
                // leaving its fingerprints on somebody else's weapon.
                if (_cfg.Paint.TintTheCan) _can.Match(_picker.Colour, _cfg.Paint.PaintEnabled);

                // Unconditional, and safe to be: a weapon he is not holding does not spend
                // ammo, so there is nothing to refund and Feed does nothing. Gating it on the
                // extinguisher being out would mean resetting the tracker every other tick,
                // and the reset also clears the tint, which would then be re-applied forever.
                // Off means the tank empties the way the game intended, too.
                if (_cfg.Paint.PaintEnabled) _can.Feed(_cfg.Paint);

                // The look, over the top of all of it. Reads the sprayer rather than the
                // trigger so the animation and the paint can never disagree about whether he
                // is spraying -- one of them is the source and the other follows.
                _spraycan.Update(_sprayer.Spraying, UI.Reticle.Aiming());

                Badge();

                // WHERE THE PAINT IS ABOUT TO GO. The mod hides the weapon model, so the game
                // has no reason to draw its own reticle -- and free-aiming something you
                // cannot see the aim point of is guesswork. Not while the panel is up, which
                // is the one time the middle of the screen means nothing.
                if (!_picker.IsOpen && Can.Out() && _cfg.Paint.PaintEnabled)
                {
                    UI.Reticle.Draw(_picker.Colour, UI.Reticle.Aiming(), _sprayer.Spraying);
                }

                if (_cfg.Paint.PaintEnabled) _marks.Sweep();

                // And the law's opinion of him, which is a different thing entirely.
                Booked();

                // The street's opinion of a man with a can. Only while he is holding one, and
                // it hands everything back the moment he is not.
                _street.Update(Can.Out() && _cfg.Paint.PaintEnabled);

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

        /// <summary>
        /// Says it is here, once, a moment after the world exists.
        ///
        /// NOT FROM THE CONSTRUCTOR. Scripts start while the game is still on the loading
        /// screen, and a notification posted then is posted to nothing -- so the one message
        /// whose entire job is to prove the mod loaded would be the one message nobody sees.
        ///
        /// It also names the key. Somebody who installed this a week ago and forgot what it
        /// was bound to should not have to find a readme.
        /// </summary>
        private void Hello()
        {
            if (_saidHello) return;

            // A few seconds in, and only once the player is real.
            if (Game.GameTime < 6000) return;

            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;
            }
            catch
            {
                return;
            }

            _saidHello = true;

            UI.Hud.Ticker("~g~" + Build.Name + " " + Build.Version + " - by " + Build.By +
                          "~s~ loaded.  Press ~b~" +
                          _cfg.MenuKey + "~s~ for the can.");
        }

        private bool _saidHello;

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
        /// <summary>
        /// One star while an officer can see him tagging, and no more than one.
        ///
        /// THE CAP IS THE FEATURE. Giving the star is easy and useless on its own: a one-star
        /// chase climbs to two the moment he runs, and at two they shoot. Holding the ceiling at
        /// one for as long as a can is the worst thing in his hands is what makes this an arrest
        /// rather than the opening of a gunfight.
        ///
        /// IT ONLY EVER RAISES TO ONE. Already wanted for something real and it keeps its hands
        /// off entirely -- a man with three stars has bigger problems than a wall, and quietly
        /// dropping him to one because he happened to be holding a can would be this mod
        /// rescuing him from the game.
        ///
        /// The ceiling is put back ONCE rather than every tick, because it only owns what it
        /// took: something else may have set it since and this should not keep overruling it.
        /// </summary>
        private void Booked()
        {
            if (_law == null) return;

            _law.Update(_sprayer != null && _sprayer.Spraying);

            try
            {
                if (_law.Drawn || !_law.Watching)
                {
                    if (_capped)
                    {
                        Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                        _capped = false;
                    }

                    return;
                }

                if (Game.Player.WantedLevel > 1) return;

                if (!_capped)
                {
                    Function.Call(Hash.SET_MAX_WANTED_LEVEL, 1);
                    _capped = true;
                }

                if (Game.Player.WantedLevel < 1) Game.Player.WantedLevel = 1;
            }
            catch (Exception ex)
            {
                Log.Debug("Could not book him: " + ex.Message);
            }
        }

        private bool _capped;

        private void Badge()
        {
            if (!Can.Out() || !_cfg.Paint.PaintEnabled) return;

            var c = _picker.Colour;

            // THE CAN, not a coloured rectangle. This was the last place in either mod still
            // showing a swatch where the tool should be -- and a small square of colour in a
            // corner is the sort of thing a player reads as a bug in somebody else's HUD.
            //
            // Tinted, so it still answers the only question this badge exists for: what is
            // loaded. Legible, because the near-black would otherwise vanish into the corner.
            const float tall = 0.034f;

            var wide = UI.Hud.X(tall) * 0.4412f;

            if (!UI.Hud.Picture("can.png", 0.020f, 0.762f + tall * 0.5f, wide, tall, 0f,
                                UI.Hud.Legible(c)))
            {
                // No art, no badge shape -- fall back to what was here before rather than
                // leaving the key floating on its own with nothing beside it.
                UI.Hud.Box(0.012f, 0.762f, UI.Hud.X(0.020f), 0.026f, c);
                UI.Hud.Frame(0.012f, 0.762f, UI.Hud.X(0.020f), 0.026f, 0.0015f,
                             System.Drawing.Color.FromArgb(255, 30, 30, 30));
            }

            UI.Hud.Text(_cfg.MenuKey.ToString(), 0.032f, 0.768f, 0.28f,
                        System.Drawing.Color.FromArgb(255, 150, 150, 150));
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                if (_sprayer != null) _sprayer.Stop();

                // Before anything else: this puts the weapon model back and takes the prop off
                // his hand, and leaving either behind outlives the mod.
                if (_spraycan != null) _spraycan.Away();
                if (_street != null) _street.Release();

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
