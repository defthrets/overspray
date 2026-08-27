OVERSPRAY
=========

A fire extinguisher that sprays paint.

Hold the trigger with the extinguisher out. Whatever is in front of you takes a
splatter -- walls, road, kerbs, shutters, ceilings, the side of a skip. Out to
five metres, on anything the world will let a probe land on.

The further away the surface, the wider the splatter. Up close it is a tight dot
you can write with; at four metres it blooms into something you cover a garage
door with. That is the whole reason it reads as spray rather than as decals
appearing one at a time.

  F7   the only key. Opens the picker: choose a colour, and on the bottom row
       take an extinguisher.

After that there is nothing to press. Aim and fire and it paints.

DOES A NORMAL EXTINGUISHER STILL WORK? The weapon is never modified -- it puts
fires out exactly as it always did, because all this does is watch it and add
paint on top. It will also always paint, which is the trade for there being
nothing to arm. PaintEnabled=false in the ini gives you a plain one back.

The can takes the colour you picked -- approximately. A weapon's colour comes
from its textures and the only hook a script gets is the game's fixed table of
eight tints, so your colour is snapped to the nearest of black, green, gold,
pink, army, LSPD, orange and platinum. The swatch in the corner is the real
colour; the can is the closest the game can get to it.


INSTALLING
----------
  scripts\Overspray.dll
  scripts\Overspray.ini      (optional -- every setting has a working default)

Needs ScriptHookV, ScriptHookVDotNet 3, and .NET Framework 4.8. No other
dependencies: this loads the BCL and SHVDN and nothing else, so it cannot lose a
version fight with anything else in scripts\.


KNOWN, AND HONEST ABOUT IT
--------------------------
* The plume tint goes on TOP of the extinguisher's own white spray. That effect
  is defined in the weapon's meta and a script cannot recolour it. Set
  ColourTheSmoke=false in the ini if the two read as fighting each other.

* Paint is placed in WORLD space, so spraying a moving car leaves the paint
  hanging where the car was.

* The game's decal pool is a few hundred across the entire world. The mod takes
  distant paint off the wall and puts it back as you return, which is what keeps
  a wall you sprayed ten minutes ago still sprayed. Raising MaxMarks past the
  pool does not get you more paint.

* If your colours come out dark and wrong, the log will say so: this install has
  no splatters_paint decal and has fallen back to a blood one, whose colour
  arguments multiply over a red texture instead of replacing it.
