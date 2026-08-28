using System;
using System.IO;

namespace Overspray.Core
{
    /// <summary>
    /// How many decals this install will hold, and who decided that.
    ///
    /// WHY THIS IS A READER AND NOT A WRITER. The number is trivially writable -- DecalPatch's
    /// ini is a text file next to GTA5.exe and this mod already writes one of its own. It is
    /// not written anyway, for three reasons and any one of them is enough:
    ///
    ///   It belongs to somebody else. Editing another author's config because it suits this
    ///   mod is the behaviour that gets a mod a reputation, and the player never asked for it.
    ///
    ///   It would not work when it was done. DecalPatch reads its ini when the ASI attaches,
    ///   which is before any script exists -- so a write from here lands a whole launch late,
    ///   and a setting that appears to do nothing is worse than one nobody touched.
    ///
    ///   It is not always there to write to. On Legacy there is no DecalPatch at all and the
    ///   cap lives in a gameconfig inside an RPF, which is not a thing to go editing on
    ///   somebody's behalf under any circumstances.
    ///
    /// So it looks, and it says what it found. The player decides.
    /// </summary>
    internal static class DecalCap
    {
        /// <summary>What each DecalPatch level is worth, straight off its own ini comment.</summary>
        private static readonly int[] Levels = { 512, 768, 896, 1024, 2048 };

        /// <summary>The cap in force, or 0 if nothing here could tell.</summary>
        public static int Held { get; private set; }

        /// <summary>Whether this looked like an Enhanced install.</summary>
        public static bool Enhanced { get; private set; }

        /// <summary>
        /// Reads what is beside the exe and puts one line in the log about it.
        ///
        /// Said at startup rather than only when the pool runs out, because "how much will this
        /// hold" is the first question anybody asks about a mod that covers walls, and the
        /// answer being in the log means it can be asked for rather than guessed at.
        /// </summary>
        public static void Look()
        {
            try
            {
                var game = Path.GetDirectoryName(Paths.Scripts);
                if (string.IsNullOrEmpty(game) || !Directory.Exists(game)) return;

                Enhanced = File.Exists(Path.Combine(game, "GTA5_Enhanced.exe"));

                var ini = Path.Combine(game, "DecalPatch.ini");

                if (!File.Exists(ini))
                {
                    Log.Info("No DecalPatch here, so the decal cap is whatever the game ships " +
                             "with -- about 512, which is roughly seven seconds of solid " +
                             "spraying on screen at once. Everything you paint is still " +
                             "remembered and put back as you walk up to it; the cap only " +
                             "decides how much is visible at the same time. " +
                             (Enhanced
                                 ? "On Enhanced, DecalPatch.asi raises it to 2048 and is a " +
                                   "drop-in -- no OpenIV, no RPF."
                                 : "On Legacy it takes a gameconfig with raised pools, and it " +
                                   "has to match your game build."));
                    return;
                }

                var level = Read(ini);

                if (level < 0)
                {
                    Log.Info("DecalPatch is here but its Level could not be read.");
                    return;
                }

                Held = level < Levels.Length ? Levels[level] : 0;

                if (Held == 0)
                {
                    Log.Info("DecalPatch is here at Level " + level + ", which this does not " +
                             "have a number for. Newer than this mod, most likely.");
                    return;
                }

                Log.Info("DecalPatch is here at Level " + level + " -- about " + Held +
                         " decals at once, which is roughly " + (Held / 66) +
                         " seconds of solid spraying." +
                         (level < Levels.Length - 1
                             ? " Level " + (Levels.Length - 1) + " in DecalPatch.ini is the top, at " +
                               Levels[Levels.Length - 1] + "."
                             : " That is the top setting."));
            }
            catch (Exception ex)
            {
                Log.Debug("Could not work out the decal cap: " + ex.Message);
            }
        }

        /// <summary>
        /// The Level out of DecalPatch's ini, or -1.
        ///
        /// Read by hand rather than through this mod's own IniFile, because that one is built
        /// around knowing its keys and their defaults -- this is one integer out of a file
        /// belonging to somebody else, and it should not care about anything else in there.
        /// </summary>
        private static int Read(string path)
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();

                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                var eq = line.IndexOf('=');
                if (eq <= 0) continue;

                if (!string.Equals(line.Substring(0, eq).Trim(), "Level",
                                   StringComparison.OrdinalIgnoreCase)) continue;

                int n;
                if (int.TryParse(line.Substring(eq + 1).Trim(), out n)) return n;
            }

            return -1;
        }
    }
}
