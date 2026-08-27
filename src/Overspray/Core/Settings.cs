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
        public Keys MenuKey = Keys.F3;

        /// <summary>
        /// How far the paint carries.
        ///
        /// Five metres is what an extinguisher looks like it reaches. Much beyond that and the
        /// paint lands somewhere the plume visibly is not, which reads as a targeting bug
        /// rather than as spray.
        /// </summary>
        public float Range = 10f;

        /// <summary>Splatters per second while the trigger is down.</summary>
        public float Rate = 9f;

        /// <summary>
        /// How wide the splatter is at one metre. Everything follows from this.
        ///
        /// Width = SizeAtOneMetre x distance^SpreadPower. At 0.2 and a power of 1 that is:
        ///
        ///      1m  ->  0.2m
        ///      5m  ->  1.0m
        ///     10m  ->  2.0m
        ///
        /// A STRAIGHT LINE, which makes it a real cone -- width proportional to distance, which
        /// is what a spray geometrically is. The first pass at this was quadratic because the
        /// first set of estimates was; these three sit exactly on a line, so the power is 1 and
        /// the maths is the honest one.
        ///
        /// Raise the power above 1 for a plume that blooms as it loses pressure, if the cone
        /// reads too even in game.
        /// </summary>
        public float SizeAtOneMetre = 0.2f;

        /// <summary>1 is a cone. Above 1 blooms toward the far end.</summary>
        public float SpreadPower = 1f;

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
        /// Whether the extinguisher paints at all.
        ///
        /// NO HOTKEY, on purpose. There is one key in this mod and it opens the picker; the
        /// paint is simply what an extinguisher does once this is installed. An arming toggle
        /// is a second thing to remember and a second thing to have got wrong when it does not
        /// work, and the whole point of the tool is to pick up and use.
        ///
        /// It stays here as a setting for somebody who wants a plain extinguisher back without
        /// uninstalling. The weapon is never modified either way -- it puts fires out exactly
        /// as it always did, because all this does is watch it and add paint on top.
        /// </summary>
        public bool PaintEnabled = true;

        /// <summary>Whether the can takes the nearest of the game's eight weapon tints.</summary>
        public bool TintTheCan = true;

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
                s.PaintEnabled = ini.GetBool("Paint", "PaintEnabled", s.PaintEnabled);

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
