using System;
using System.IO;
using System.Reflection;

namespace Overspray.Core
{
    /// <summary>
    /// Where this mod's files live.
    ///
    /// Worked out from the assembly rather than assumed, because "scripts\" is not always where
    /// somebody's scripts\ is -- a few loaders and a few installs put it elsewhere, and a mod
    /// that hardcodes the path fails on those in a way that looks like the mod is broken.
    /// </summary>
    internal static class Paths
    {
        private static string _scripts;

        /// <summary>The folder this dll is sitting in.</summary>
        public static string Scripts
        {
            get
            {
                if (!string.IsNullOrEmpty(_scripts)) return _scripts;

                try
                {
                    var here = Assembly.GetExecutingAssembly().Location;
                    var dir = Path.GetDirectoryName(here);

                    if (!string.IsNullOrEmpty(dir))
                    {
                        _scripts = dir;
                        return _scripts;
                    }
                }
                catch
                {
                    // Fall through to the ordinary guess.
                }

                _scripts = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
                return _scripts;
            }
        }

        /// <summary>Where the save and the log go. Made if it is not there.</summary>
        public static string Writable
        {
            get
            {
                var dir = Path.Combine(Scripts, "Overspray");

                try
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
                catch
                {
                    // Then writing will fail and say so, which is better than guessing again.
                }

                return dir;
            }
        }

        public static string Ini => Path.Combine(Scripts, "Overspray.ini");
        public static string LogFile => Path.Combine(Writable, "Overspray.log");
        public static string SaveFile => Path.Combine(Writable, "paint.json");
    }
}
