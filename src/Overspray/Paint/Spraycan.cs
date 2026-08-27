using System;
using GTA;
using GTA.Native;
using Overspray.Core;

namespace Overspray.Paint
{
    /// <summary>
    /// The same mechanic, holding a spray can instead.
    ///
    /// NOTHING ABOUT THE PAINT CHANGES. The extinguisher is still the weapon underneath -- it
    /// is what gives you the aiming camera, the reticle and a trigger, and all three are things
    /// a prop cannot do. What changes is that its model is hidden, a can is put in his hand,
    /// and his upper body plays the game's own tagging animation over the top.
    ///
    /// A VISUAL SWAP RATHER THAN A SECOND MECHANIC, and that is the whole reason it is worth
    /// doing this way round. Re-implementing aim and fire on top of a prop means writing a
    /// worse version of something the game already does perfectly, and it means two code paths
    /// that can disagree about where the paint goes.
    ///
    /// The animation is anim@scripted@freemode@postertag@graffiti_spray@male@, which is what
    /// GTA Online's poster tagging uses. It ships with matching _spraycan clips -- the can is
    /// animated in step with the hands, shaking and spraying -- so the prop is driven from the
    /// same dictionary rather than hung there rigid.
    ///
    /// Flag 51 is what makes it work at all: the native's own list says 48 to 63 is "Upper body
    /// > Controllable", meaning it blends over whatever the legs are doing and leaves the player
    /// in charge. A full-body clip here would plant him at a wall, which is the online version's
    /// choreography and the opposite of aiming.
    /// </summary>
    internal sealed class Spraycan
    {
        private const string Prop = "prop_cs_spray_can";

        private const string Dict = "anim@scripted@freemode@postertag@graffiti_spray@male@";

        /// <summary>Holding it, and using it. Both have a matching clip for the can.</summary>
        private const string Idle = "spray_can_idle_male";
        private const string IdleCan = "spray_can_idle_spraycan";
        private const string Spray = "spray_can_male";
        private const string SprayCan = "spray_can_spraycan";

        /// <summary>Upper body, controllable. See the class note.</summary>
        private const int UpperControllable = 51;

        private readonly Settings _cfg;

        private Prop _can;
        private bool _spraying;
        private bool _dictAsked;

        public Spraycan(Settings cfg)
        {
            _cfg = cfg;
        }

        /// <summary>Whether the can is currently in his hand.</summary>
        public bool Out => _can != null && _can.Exists();

        /// <summary>
        /// Called every tick. Puts the can up when the extinguisher is out, takes it away when
        /// it is not.
        /// </summary>
        public void Update(bool spraying)
        {
            if (!_cfg.SprayCanLook)
            {
                Away();
                return;
            }

            var holding = Can.Out();

            if (!holding)
            {
                Away();
                return;
            }

            Ready();

            if (!Out) return;

            // Hidden every tick rather than once. Drawing, holstering and every animation that
            // re-equips it puts the model back, so a one-off hide lasts until the first time he
            // does anything with his hands.
            try
            {
                Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE,
                              Game.Player.Character.Handle, false, true, true, true);
            }
            catch
            {
                // Then he is holding an extinguisher and a spray can, which is odd but works.
            }

            if (spraying == _spraying) return;

            _spraying = spraying;
            Play(spraying ? Spray : Idle, spraying ? SprayCan : IdleCan);
        }

        /// <summary>Makes sure the prop and the animations are in hand.</summary>
        private void Ready()
        {
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, Dict))
            {
                if (!_dictAsked)
                {
                    Function.Call(Hash.REQUEST_ANIM_DICT, Dict);
                    _dictAsked = true;
                }

                return;
            }

            if (Out) return;

            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;

                var model = new Model(Prop);
                if (!model.IsValid || !model.IsInCdImage) return;
                if (!model.Request(800)) return;

                _can = World.CreateProp(model, me.Position, false, false);
                model.MarkAsNoLongerNeeded();

                if (_can == null || !_can.Exists()) return;

                // The right hand. 28422 is SKEL_R_Hand, and the offsets put it where a can sits
                // rather than through the palm -- the animation was authored against a can in
                // this position, so getting it wrong makes the hands look broken rather than
                // the can look misplaced.
                var bone = Function.Call<int>(Hash.GET_PED_BONE_INDEX, me.Handle, 28422);

                Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, _can.Handle, me.Handle, bone,
                              0.10f, 0.02f, -0.02f,
                              -80f, 0f, 0f,
                              false, false, false, false, 2, true);

                Play(Idle, IdleCan);

                Log.Info("Spray can out.");
            }
            catch (Exception ex)
            {
                Log.Debug("Could not put the can up: " + ex.Message);
            }
        }

        /// <summary>Ped clip and the can's own clip, from the same dictionary and in step.</summary>
        private void Play(string ped, string can)
        {
            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;

                Function.Call(Hash.TASK_PLAY_ANIM, me.Handle, Dict, ped,
                              4f, -4f, -1, UpperControllable, 0f, false, false, false);

                if (_can != null && _can.Exists())
                {
                    Function.Call(Hash.PLAY_ENTITY_ANIM, _can.Handle, can, Dict,
                                  1000f, false, true, false, 0f, 0);
                }
            }
            catch
            {
                // No animation is a man holding a can still, which is survivable.
            }
        }

        /// <summary>Can gone, weapon visible again.</summary>
        public void Away()
        {
            _spraying = false;

            try
            {
                var me = Game.Player.Character;

                if (me != null && me.Exists())
                {
                    Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, me.Handle, true, true, true, true);

                    // Only the upper-body task, so this does not cancel whatever else he is
                    // doing with his legs.
                    Function.Call(Hash.STOP_ANIM_TASK, me.Handle, Dict, Idle, 3f);
                    Function.Call(Hash.STOP_ANIM_TASK, me.Handle, Dict, Spray, 3f);
                }
            }
            catch
            {
                // Teardown.
            }

            if (_can == null) return;

            try { if (_can.Exists()) _can.Delete(); }
            catch { /* the streamer gets it */ }

            _can = null;
        }
    }
}
