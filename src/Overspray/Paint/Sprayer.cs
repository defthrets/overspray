using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using Overspray.Core;
using Control = GTA.Control;

namespace Overspray.Paint
{
    /// <summary>
    /// Holding the trigger on a fire extinguisher and getting paint.
    ///
    /// The whole mod is this: while the extinguisher is out and the trigger is down, probe
    /// forward, and put a splatter on whatever the probe found. Everything else -- the pool,
    /// the picker, the plume -- exists to make that read as spraying rather than as decals
    /// appearing.
    ///
    /// GATED ON THE CONTROL, NOT ON IS_PED_SHOOTING. The extinguisher is a weapon by the game's
    /// reckoning but a strange one, and whether it counts as "shooting" is not something to
    /// find out from a mod that has already shipped. Reading the attack control is true
    /// whatever the answer, and it is also true on the frame the trigger goes down rather than
    /// once the plume has spun up.
    /// </summary>
    internal sealed class Sprayer
    {
        private const string Extinguisher = "WEAPON_FIREEXTINGUISHER";

        /// <summary>
        /// The plume, tinted.
        ///
        /// core is the always-loaded asset dictionary, and ent_amb_smoke_foundry is a slow pale
        /// smoke that takes a colour cleanly. It goes on TOP of the extinguisher's own white
        /// spray rather than replacing it -- the weapon's effect is defined in its meta and
        /// script cannot recolour it. Whether that reads as coloured smoke or as two effects
        /// fighting is the one thing in this mod that has to be looked at rather than reasoned
        /// about, which is why it is a setting.
        /// </summary>
        private const string FxAsset = "core";
        private const string FxName = "ent_amb_smoke_foundry";

        private readonly Settings _cfg;
        private readonly Marks _marks;

        private int _nextDab;
        private int _fx = -1;
        private bool _wasSpraying;

        public Sprayer(Settings cfg, Marks marks)
        {
            _cfg = cfg;
            _marks = marks;
        }

        /// <summary>What the picker last set.</summary>
        public Color Colour = Color.FromArgb(255, 40, 200, 90);

        /// <summary>
        /// The picker's dial, as a MULTIPLIER rather than a width.
        ///
        /// The width itself comes from how far away the wall is -- see Dab -- so a fixed
        /// number here would fight the thing that makes it read as spray. This is nozzle
        /// pressure: same plume, more or less of it.
        /// </summary>
        public float Scale = 1f;

        /// <summary>True while paint is actually coming out, for anything that wants to know.</summary>
        public bool Spraying { get; private set; }

        public void Update()
        {
            var spraying = Holding();

            if (spraying != _wasSpraying)
            {
                _wasSpraying = spraying;

                if (spraying) StartPlume();
                else StopPlume();
            }

            Spraying = spraying;

            if (!spraying) return;

            var now = Game.GameTime;
            if (now < _nextDab) return;

            // Rate rather than every frame. A dab per frame at 60fps empties the decal pool in
            // about seven seconds and puts three hundred splatters inside one square metre.
            _nextDab = now + (int)(1000f / Math.Max(1f, _cfg.Rate));

            Dab();
        }

        /// <summary>Extinguisher out, trigger down.</summary>
        private bool Holding()
        {
            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists() || !me.IsAlive) return false;

                var want = Function.Call<uint>(Hash.GET_HASH_KEY, Extinguisher);
                var got = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, me.Handle);

                if (got != want) return false;

                return Game.IsControlPressed(Control.Attack) ||
                       Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, (int)Control.Attack);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>One splatter, wherever the spray is pointing.</summary>
        private void Dab()
        {
            var hit = Surface.InFront(_cfg.Range);
            if (!hit.Landed) return;

            // OFF THE SURFACE BY A HAIR. A decal placed exactly on the geometry fights it for
            // the same pixels and flickers -- z-fighting, and at spray rates it flickers a
            // hundred times a second across a whole wall.
            var at = hit.At + hit.Normal * 0.02f;

            // Into the wall, which is the opposite of the way it faces.
            var into = -hit.Normal;
            var side = Surface.Along(hit.Normal);

            // THE FURTHER THE WALL, THE WIDER THE SPLATTER, and this is what turns a stream of
            // decals into a spray. Up close it is a tight dot you can write with; at arm's
            // reach and beyond it blooms into something you cover a garage door with.
            //
            // Quadratic rather than a cone. A cone is what a spray geometrically is and it is
            // not what one looks like -- the plume holds together while it has pressure and
            // opens out as it loses it, so the far half widens faster than the near half.
            var away = GameplayCamera.Position.DistanceTo(hit.At);

            var size = _cfg.SizeAtOneMetre *
                       (float)Math.Pow(Math.Max(0.2f, away), _cfg.SpreadPower) *
                       Scale;

            // A little variation, or a held trigger paints one splatter repeatedly in place and
            // reads as a decal rather than as spray.
            size *= 0.85f + (float)_rng.NextDouble() * 0.3f;

            if (size < _cfg.MinSize) size = _cfg.MinSize;
            if (size > _cfg.MaxSize) size = _cfg.MaxSize;

            _marks.Put(at, into, side, size,
                       Colour.R / 255f, Colour.G / 255f, Colour.B / 255f);
        }

        private readonly Random _rng = new Random();

        // ---- the plume ---------------------------------------------------------

        private void StartPlume()
        {
            if (!_cfg.ColourTheSmoke) return;

            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;

                if (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, FxAsset))
                {
                    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, FxAsset);
                    return;   // next press, once it is in
                }

                Function.Call(Hash.USE_PARTICLE_FX_ASSET, FxAsset);

                Function.Call(Hash.SET_PARTICLE_FX_NON_LOOPED_COLOUR,
                              Colour.R / 255f, Colour.G / 255f, Colour.B / 255f);

                // On the right hand, pushed forward so it leaves the nozzle rather than his
                // wrist. Bone 28422 is SKEL_R_Hand -- the standard one every mod uses for a
                // held-object effect.
                _fx = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_ON_ENTITY_BONE,
                                         FxName, me.Handle,
                                         0.35f, 0.1f, 0f,
                                         0f, 0f, 0f,
                                         28422, 0.7f, false, false, false);

                if (_fx == 0) { _fx = -1; return; }

                Function.Call(Hash.SET_PARTICLE_FX_LOOPED_COLOUR, _fx,
                              Colour.R / 255f, Colour.G / 255f, Colour.B / 255f, false);
            }
            catch (Exception ex)
            {
                _fx = -1;
                Log.Debug("No coloured plume: " + ex.Message);
            }
        }

        private void StopPlume()
        {
            if (_fx == -1) return;

            try { Function.Call(Hash.STOP_PARTICLE_FX_LOOPED, _fx, false); }
            catch { /* it stops when the asset unloads */ }

            _fx = -1;
        }

        /// <summary>Everything off, for a reload.</summary>
        public void Stop()
        {
            StopPlume();
            Spraying = false;
            _wasSpraying = false;
        }
    }
}
