using System;
using System.Windows.Forms;

namespace Overspray.Core
{
    /// <summary>
    /// Everything tunable, with a working value for every one of them.
    ///
    /// The ini is optional and always has been the right way round: a mod that will not run
    /// without its settings file is a mod that fails for anybody who deletes a file they were
    /// told they could edit.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>Off entirely, for somebody who wants it installed and not running.</summary>
        public bool Enabled = true;

        /// <summary>The picker key. The extinguisher does the rest.</summary>
        public Keys MenuKey = Keys.F7;

        /// <summary>
        /// How far the paint carries.
        ///
        /// Five metres is what an extinguisher looks like it reaches. Much beyond that and the
        /// paint lands somewhere the plume visibly is not, which reads as a targeting bug
        /// rather than as spray.
        /// </summary>
        public float Range = 5f;

        /// <summary>Splatters per second while the trigger is down.</summary>
        public float Rate = 9f;

        /// <summary>
        /// How wide the splatter is at one metre, before the distance curve.
        ///
        /// THE SIZE COMES FROM THE DISTANCE, and the curve is quadratic rather than a cone.
        /// A cone is what a spray geometrically is -- width proportional to distance -- and it
        /// is not what a spray LOOKS like: the plume holds together for the first stretch and
        /// then blooms as it loses pressure, so the far end widens faster than the near end.
        ///
        /// 0.125 puts it at half a metre across at two metres and two metres across at four,
        /// which is the shape somebody describes when they describe it from memory. A cone
        /// through the same two points would have to be a metre wide at two metres, and looks
        /// like a paint roller.
        /// </summary>
        public float SizeAtOneMetre = 0.125f;

        /// <summary>2 is the bloom. 1 is a straight cone, if you want the geometric answer.</summary>
        public float SpreadPower = 2f;

        /// <summary>The floor and ceiling, after the curve and the picker have had their say.</summary>
        public float MinSize = 0.08f;
        public float MaxSize = 3.50f;

        /// <summary>How solid each one goes on.</summary>
        public float Opacity = 0.92f;

        /// <summary>
        /// How many marks are kept before the oldest starts coming off the wall.
        ///
        /// The game's own pool is a few hundred across the whole world and it drops the oldest
        /// silently when it fills -- so this being too high does not get you more paint, it
        /// gets you paint that vanishes from behind while you are still spraying, which looks
        /// like a bug in the placement.
        /// </summary>
        public int MaxMarks = 420;

        /// <summary>Whether the plume is tinted to the colour you are spraying.</summary>
        public bool ColourTheSmoke = true;

        /// <summary>
        /// Whether paint mode is on when the game starts.
        ///
        /// THIS IS THE ANSWER TO "does a normal extinguisher still work". The weapon itself is
        /// never touched -- it puts fires out exactly as it always did, because nothing here
        /// modifies it. What the mod does is WATCH it and add paint, and that is the part you
        /// would not want happening while you are actually putting a fire out.
        ///
        /// So it is a mode. Off, the extinguisher is the game's. On, it also paints. Both are
        /// one key press apart and the HUD says which you are in whenever the thing is in your
        /// hands, because a mode you cannot see is a mode you forget you are in.
        /// </summary>
        public bool PaintOnByDefault = true;

        /// <summary>Toggles paint mode. The picker key opens the picker; this arms it.</summary>
        public Keys ToggleKey = Keys.F6;

        /// <summary>Whether the can takes the nearest of the game's eight weapon tints.</summary>
        public bool TintTheCan = true;

        /// <summary>Whether opening the picker hands you an extinguisher if you have none.</summary>
        public bool GiveOne = true;

        /// <summary>Whether paint survives a reload.</summary>
        public bool Persist = true;

        public static Settings Load()
        {
            var s = new Settings();

            try
            {
                var ini = IniFile.Load(Paths.Ini);
                if (ini == null) return s;

                s.Enabled = ini.GetBool("General", "Enabled", s.Enabled);
                s.Persist = ini.GetBool("General", "Persist", s.Persist);

                var key = ini.GetString("General", "MenuKey", s.MenuKey.ToString());
                Keys parsed;
                if (Enum.TryParse(key, true, out parsed)) s.MenuKey = parsed;

                s.Range = Clamp(ini.GetFloat("Paint", "Range", s.Range), 1f, 20f);
                s.Rate = Clamp(ini.GetFloat("Paint", "Rate", s.Rate), 1f, 40f);
                s.SizeAtOneMetre = Clamp(ini.GetFloat("Paint", "SizeAtOneMetre", s.SizeAtOneMetre), 0.01f, 2f);
                s.SpreadPower = Clamp(ini.GetFloat("Paint", "SpreadPower", s.SpreadPower), 0.5f, 3f);
                s.MinSize = Clamp(ini.GetFloat("Paint", "MinSize", s.MinSize), 0.02f, 3f);
                s.MaxSize = Clamp(ini.GetFloat("Paint", "MaxSize", s.MaxSize), 0.05f, 8f);
                s.Opacity = Clamp(ini.GetFloat("Paint", "Opacity", s.Opacity), 0.1f, 1f);
                s.MaxMarks = (int)Clamp(ini.GetFloat("Paint", "MaxMarks", s.MaxMarks), 16f, 2000f);

                s.ColourTheSmoke = ini.GetBool("Paint", "ColourTheSmoke", s.ColourTheSmoke);
                s.TintTheCan = ini.GetBool("Paint", "TintTheCan", s.TintTheCan);
                s.PaintOnByDefault = ini.GetBool("General", "PaintOnByDefault", s.PaintOnByDefault);
                s.GiveOne = ini.GetBool("General", "GiveOne", s.GiveOne);

                var tog = ini.GetString("General", "ToggleKey", s.ToggleKey.ToString());
                Keys tk;
                if (Enum.TryParse(tog, true, out tk)) s.ToggleKey = tk;

                if (s.MaxSize < s.MinSize) s.MaxSize = s.MinSize;
            }
            catch (Exception ex)
            {
                Log.Error("Could not read the ini; using defaults.", ex);
            }

            return s;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
