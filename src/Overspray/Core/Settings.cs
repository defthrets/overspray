using System;
using System.Windows.Forms;
using Overspray.Paint;

namespace Overspray.Core
{
    /// <summary>
    /// Everything tunable, with a working value for every one of them.
    ///
    /// The ini is optional and always has been the right way round: a mod that will not run
    /// without its settings file is a mod that fails for anybody who deletes a file they were
    /// told they could edit.
    ///
    /// THE PAINT KNOBS DO NOT LIVE HERE ANY MORE, they live in Paint.PaintConfig, and this
    /// fills that in. The engine is shared verbatim with Posted Up, whose own settings are a
    /// completely different shape -- so the moment the engine reads a host's Settings class
    /// directly it has to be forked, and a forked engine drifts silently. What is left here is
    /// only what is genuinely about THIS mod: whether it runs, which key opens it, and whether
    /// paint survives a reload.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>Off entirely, for somebody who wants it installed and not running.</summary>
        public bool Enabled = true;

        /// <summary>The picker key. The extinguisher does the rest.</summary>
        public Keys MenuKey = Keys.F3;

        /// <summary>Whether paint survives a reload.</summary>
        public bool Persist = true;

        /// <summary>
        /// Whether to switch off when Posted Up is installed alongside.
        ///
        /// IT HAS THIS BUILT IN, as a Graffiti app on the phone, running THE SAME ENGINE --
        /// literally the same files. With both installed both engines wake up, both watch the
        /// same trigger, and both paint: two cans in his hand, two plumes stacked on top of
        /// each other, and two decals for every one you meant. It looks like the effect is
        /// twice as thick as it should be, because it is.
        ///
        /// The standalone is the one that gives way, because Posted Up is the bigger mod and
        /// its version is the one with the phone app attached.
        /// </summary>
        public bool StandDownForPostedUp = true;

        /// <summary>Everything the engine reads. Shared, unchanged, with Posted Up.</summary>
        public readonly PaintConfig Paint = new PaintConfig();

        public static Settings Load()
        {
            var s = new Settings();
            var p = s.Paint;

            try
            {
                var ini = IniFile.Load(Paths.Ini);
                if (ini == null) return s;

                s.Enabled = ini.GetBool("General", "Enabled", s.Enabled);
                s.Persist = ini.GetBool("General", "Persist", s.Persist);
                s.StandDownForPostedUp = ini.GetBool("General", "StandDownForPostedUp", s.StandDownForPostedUp);

                var key = ini.GetString("General", "MenuKey", s.MenuKey.ToString());
                Keys parsed;
                if (Enum.TryParse(key, true, out parsed)) s.MenuKey = parsed;

                p.Range = Clamp(ini.GetFloat("Paint", "Range", p.Range), 1f, 20f);
                p.Rate = Clamp(ini.GetFloat("Paint", "Rate", p.Rate), 1f, 60f);
                p.Continuous = ini.GetBool("Paint", "Continuous", p.Continuous);
                p.Overlap = Clamp(ini.GetFloat("Paint", "Overlap", p.Overlap), 0.1f, 2f);
                p.MaxFill = (int)Clamp(ini.GetFloat("Paint", "MaxFill", p.MaxFill), 0f, 64f);
                p.SizeAtOneMetre = Clamp(ini.GetFloat("Paint", "SizeAtOneMetre", p.SizeAtOneMetre), 0.01f, 2f);
                p.SpreadPower = Clamp(ini.GetFloat("Paint", "SpreadPower", p.SpreadPower), 0.5f, 3f);
                p.MinSize = Clamp(ini.GetFloat("Paint", "MinSize", p.MinSize), 0.02f, 3f);
                p.MaxSize = Clamp(ini.GetFloat("Paint", "MaxSize", p.MaxSize), 0.05f, 8f);
                p.Opacity = Clamp(ini.GetFloat("Paint", "Opacity", p.Opacity), 0.1f, 1f);
                p.SizeJitter = Clamp(ini.GetFloat("Paint", "SizeJitter", p.SizeJitter), 0f, 0.5f);
                p.MetallicShine = Clamp(ini.GetFloat("Paint", "MetallicShine", p.MetallicShine), 0f, 2f);
                p.MaxMarks = (int)Clamp(ini.GetFloat("Paint", "MaxMarks", p.MaxMarks), 16f, 50000f);

                p.ColourTheSmoke = ini.GetBool("Paint", "ColourTheSmoke", p.ColourTheSmoke);
                p.JetScale = Clamp(ini.GetFloat("Paint", "JetScale", p.JetScale), 0.05f, 4f);
                p.CanJetScale = Clamp(ini.GetFloat("Paint", "CanJetScale", p.CanJetScale), 0.05f, 4f);
                p.JetFollowsAim = ini.GetBool("Paint", "JetFollowsAim", p.JetFollowsAim);

                p.CanJetUp = Clamp(ini.GetFloat("Paint", "CanJetUp", p.CanJetUp), -0.5f, 0.5f);
                p.CanJetOut = Clamp(ini.GetFloat("Paint", "CanJetOut", p.CanJetOut), -0.5f, 0.5f);
                p.CanJetSide = Clamp(ini.GetFloat("Paint", "CanJetSide", p.CanJetSide), -0.5f, 0.5f);

                p.FirstPersonClip = ini.GetString("Paint", "FirstPersonClip", p.FirstPersonClip);
                p.TintTheCan = ini.GetBool("Paint", "TintTheCan", p.TintTheCan);
                p.CanSeat = Clamp(ini.GetFloat("Paint", "CanSeat", p.CanSeat), -0.2f, 0.2f);
                p.PaintEnabled = ini.GetBool("Paint", "PaintEnabled", p.PaintEnabled);
                p.SprayCanLook = ini.GetBool("Paint", "SprayCanLook", p.SprayCanLook);

                p.CanRange = Clamp(ini.GetFloat("Paint", "CanRange", p.CanRange), 1f, 20f);
                p.CanSizeAtOneMetre = Clamp(ini.GetFloat("Paint", "CanSizeAtOneMetre", p.CanSizeAtOneMetre), 0.01f, 2f);
                p.CanSpreadPower = Clamp(ini.GetFloat("Paint", "CanSpreadPower", p.CanSpreadPower), 0.5f, 3f);
                p.CanMinSize = Clamp(ini.GetFloat("Paint", "CanMinSize", p.CanMinSize), 0.02f, 3f);
                p.CanMaxSize = Clamp(ini.GetFloat("Paint", "CanMaxSize", p.CanMaxSize), 0.05f, 8f);

                p.ExtinguisherLasts = Clamp(ini.GetFloat("Paint", "ExtinguisherLasts", p.ExtinguisherLasts), 1f, 50f);
                p.CanRunsOut = ini.GetBool("Paint", "CanRunsOut", p.CanRunsOut);
                p.ExtinguisherRunsOut = ini.GetBool("Paint", "ExtinguisherRunsOut", p.ExtinguisherRunsOut);

                if (p.MaxSize < p.MinSize) p.MaxSize = p.MinSize;
                if (p.CanMaxSize < p.CanMinSize) p.CanMaxSize = p.CanMinSize;
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
