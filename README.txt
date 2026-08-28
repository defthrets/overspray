================================================================================
  OVERSPRAY  0.2.0
  Spray paint on any surface in GTA V.
================================================================================

  Hold the trigger with a spray can out and whatever you are aiming at takes
  paint. Walls, shutters, kerbs, the road, the side of a skip, a ceiling --
  anything solid. Thirteen colours, chrome and gold among them, and thin,
  stock and fat caps for the can. The paint stays where you put it and is still
  there when you come back.

  Press F3 for the can.


--------------------------------------------------------------------------------
  WHAT YOU NEED FIRST
--------------------------------------------------------------------------------

  Overspray is a script mod. It cannot run on its own, and if these two are not
  installed then nothing in this zip will do anything at all:

    1. Script Hook V                    http://www.dev-c.com/gtav/scripthookv/
    2. ScriptHookVDotNet 3              https://github.com/scripthookvdotnet/
                                        scripthookvdotnet/releases

  Version 3. Not version 2. The file you need from that download is
  ScriptHookVDotNet3.dll, and it goes in your GTA V folder next to GTA5.exe.

  Windows also needs .NET Framework 4.8, which Windows 10 and 11 already have.

  BOTH GTA V LEGACY AND GTA V ENHANCED WORK. Enhanced needs a recent
  ScriptHookVDotNet -- 3.6 or newer. Older ones load on Legacy only and will
  simply not start this on Enhanced.


--------------------------------------------------------------------------------
  INSTALLING
--------------------------------------------------------------------------------

  Copy the "scripts" folder out of this zip into your GTA V folder -- the one
  with GTA5.exe in it. Say yes to merging if Windows asks.

  That is the whole install. When you are done it must look EXACTLY like this:

    Grand Theft Auto V\
      GTA5.exe
      ScriptHookV.dll
      ScriptHookVDotNet3.dll
      dinput8.dll
      scripts\
        Overspray.dll
        Overspray.ini
        Overspray\
          icons\
            logo.png
            logo_0.png ... logo_7.png
            can.png
            cap_thin.png
            cap_stock.png
            cap_fat.png

  THE FOLDER INSIDE A FOLDER IS NOT A MISTAKE. "scripts\Overspray\icons\" is
  where the artwork lives and where the mod writes its log and your saved paint.
  If you flatten it, the mod still runs -- it just draws its name as plain text
  instead of the logo.

  If you use OpenIV with a "mods" folder: this does NOT go in there. Script mods
  live in the real game folder, not in mods\.


--------------------------------------------------------------------------------
  USING IT
--------------------------------------------------------------------------------

    F3                  open the picker
    UP / DOWN           move between rows
    LEFT / RIGHT        choose a colour, or a cap
    ENTER               take what is on the row
    BACKSPACE           close

  Take a SPRAY CAN or an EXTINGUISHER from the picker. They are the same tool
  with different reach:

    spray can           2 metres, a narrow band, for detail. Three caps:
                        thin, stock and fat
    extinguisher        10 metres, up to 2m across, for covering things

  Then aim and hold the fire button. A dot in the middle of the screen shows
  where the paint will land. Neither one ever runs out.

  CAP SIZE sets the finest line the can can draw -- thin, stock or fat. A
  fat cap cannot do detail however close you hold it, which is what a cap
  is. The row goes quiet while an extinguisher is in your hands, because
  caps are the can's.

  MOD switches the paint off without closing the mod. F3 still opens the
  panel when it is off, so it is always one keypress back.

  CLEAR EVERY WALL removes all of it. It asks twice because there is no undo.


--------------------------------------------------------------------------------
  IF IT IS NOT WORKING
--------------------------------------------------------------------------------

  Work through these in order. Nearly every report is one of the first four.

  1. IS THE DLL BLOCKED?
     WINDOWS BLOCKS FILES THAT CAME OUT OF A DOWNLOADED ZIP, and .NET refuses to
     load a blocked assembly. It looks exactly like a broken mod: ScriptHookV
     DotNet reports an error and nothing appears in game. It is not your install
     and it is not the mod -- and it is the one fault the author can never
     reproduce, because a file built on your own machine is never marked.

         Right-click the ZIP -> Properties -> tick "Unblock" -> OK, THEN extract.

     Doing it on the zip fixes every file inside at once. If you have already
     extracted, right-click Overspray.dll and do the same thing there.

  2. DID IT LOAD?
     A few seconds after you spawn, Overspray posts a line in the top-left
     saying it loaded and which key opens it. If you never see that line, the
     script is not running -- go to step 2. If you DO see it, the mod is fine
     and the problem is somewhere in step 5.

  3. IS SCRIPTHOOKVDOTNET INSTALLED, AND VERSION 3?
     Look in your GTA V folder for ScriptHookVDotNet3.dll. If it is not there,
     or you only have ScriptHookVDotNet2.dll, that is the problem.

  4. HAS THE GAME UPDATED?
     Script Hook V stops working every time Rockstar patch the game, and takes
     every script mod down with it until it is updated. This is by far the most
     common cause and it has nothing to do with this mod. Get the current
     Script Hook V from dev-c.com.

  5. READ THE LOGS. There are two and they say different things:

       ScriptHookVDotNet.log       in your GTA V folder.
                                   Says whether Overspray was loaded at all.

       scripts\Overspray\Overspray.log
                                   Only exists if Overspray started. Says what
                                   it is doing and complains loudly when
                                   something is wrong.

     If the second file does not exist, the mod never started -- steps 2 and 3.

  6. IT LOADS BUT NOTHING PAINTS.
     - Are you actually holding one? Take it from the picker with ENTER.
     - Are you close enough? The CAN only reaches 2 metres, measured from the
       can itself. Aim at a wall you could reach out and touch, not one across
       the street. The extinguisher is the one for range.
     - Is MOD switched to OFF in the picker? Turn it back on. F3 still opens
       the panel either way, which is why that switch is safe to use.
     - Is paint landing but vanishing? See the note about the decal pool below.

  7. PAINT DISAPPEARS AFTER A FEW TAGS.
     That is the GAME's limit, not this mod's. GTA V keeps only a few hundred
     decals in the whole world at once and quietly drops the oldest. Overspray
     remembers everything you have painted and puts it back as you approach,
     which is why walking away and returning brings it back -- but it cannot
     make the game hold more at once.

     If you want more of it on the wall at once, that is a limit adjuster's job
     and not this mod's:

       GTA V ENHANCED -- DecalPatch.asi. A drop-in ASI, no OpenIV and no RPF
         editing. It patches the cap at startup and its own ini sets the level:

             [Decals]
             ; 0=Vanilla (512), 1=Low (768), 2=Medium (896),
             ; 3=High (1024), 4=Ultra (2048)
             Level=4

         Vanilla 512 is about seven seconds of solid spraying on screen at once.
         Ultra 2048 is about thirty.

       GTA V LEGACY -- a gameconfig with raised pools, installed with OpenIV.
         Get one that matches your game version. A gameconfig for the wrong
         build is the classic cause of an infinite loading screen, which is why
         this mod does not ship one and why you should not take one from a mod
         that is not maintained for your version.

     Neither is required. Overspray works on a stock install; the cap only
     decides how much is on screen at once, never how much is remembered.


--------------------------------------------------------------------------------
  SETTINGS
--------------------------------------------------------------------------------

  Overspray.ini next to the DLL. Every line is commented, every setting has a
  working default, and the file is optional -- delete it and the mod runs
  exactly the same.

  The ones people change first:

    MenuKey             which key opens the picker
    CanRange            how far the spray can reaches
    Rate / Overlap      how dense the paint is
    MaxMarks            how much is remembered
    Enabled             false turns the whole thing off


--------------------------------------------------------------------------------
  DOES IT BREAK ANYTHING?
--------------------------------------------------------------------------------

  It should not. Overspray does not replace any game file, does not touch your
  save, and adds nothing to the world. The fire extinguisher is unmodified and
  still puts fires out exactly as it always did -- the mod only watches it and
  adds paint on top. Uninstall by deleting the four files listed above.

  It does not need OpenIV, a mods folder, or a modified gameconfig.

  If you also run POSTED UP, that mod has this built in as a Graffiti app on the
  phone, using the same engine. Both installed is fine: this one notices and
  starts with its spray switched off so the two do not paint over each other.
  F3 still opens, and you can turn it on in there if you want both.


--------------------------------------------------------------------------------
  CREDITS AND TERMS
--------------------------------------------------------------------------------

  Built on Script Hook V by Alexander Blade, and ScriptHookVDotNet.

  Free. Do what you like with it. Please do not re-upload it as your own, and if
  you build on it, say where it came from.

  Single player only. Do not use script mods in GTA Online.
