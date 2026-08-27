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

                // CODEBASE, NOT LOCATION, AND THE DIFFERENCE IS NOT ACADEMIC.
                //
                // ScriptHookVDotNet shadow-copies every script into
                // AppData\Local\assembly\dl3\... before running it. Under a shadow copy
                // Location is that temp folder; CodeBase stays the file it was loaded from.
                //
                // Reading Location meant this mod looked for its ini beside the COPY, where
                // there has never been one and never will be -- so every setting anybody
                // edited was silently ignored, the log was written somewhere nobody would
                // think to look, and saved paint was orphaned in a new folder on every
                // rebuild. It logged "No ini ... using built-in defaults" each launch, which
                // reads like a missing file rather than a mod looking in the wrong place.
                try
                {
                    var code = Assembly.GetExecutingAssembly().CodeBase;

                    if (!string.IsNullOrEmpty(code))
                    {
                        var dir = Path.GetDirectoryName(new Uri(code).LocalPath);

                        // Only if it is really there. A CodeBase that does not resolve is
                        // worse than no answer, because everything downstream trusts this.
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            _scripts = dir;
                            return _scripts;
                        }
                    }
                }
                catch
                {
                    // Fall through to Location, which is right when nothing shadow-copies.
                }

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

        /// <summary>
        /// The wordmark and the can.
        ///
        /// Beside the log and the save rather than loose in scripts\, because they belong to
        /// this mod and a folder full of other people's DLLs is not a place to leave two PNGs
        /// called logo and can.
        /// </summary>
        public static string Icons => Path.Combine(Writable, "icons");
    }
}
