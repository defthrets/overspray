# -*- coding: utf-8 -*-
"""
Copy the paint engine into Posted Up, or check that the two copies still match.

WHY THIS EXISTS. The engine is the same system in both mods -- the only thing that differs is
the front end, F3 and a panel here, an app on the phone there. Two hand-maintained copies of
the same code do not stay the same; one gets a fix, the other gets the same bug reported again
six weeks later, and nobody can tell which is which by looking.

So there is exactly one source of truth, this copies it, and --check fails loudly when they
have drifted. The rewrite is deliberately trivial -- the namespace and nothing else -- because
the moment it needs to be clever the two are not really the same system any more.

    python tools/sync-paint.py            copy engine -> hoodrich
    python tools/sync-paint.py --check    report drift, change nothing
"""
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FROM = os.path.join(HERE, "src", "Overspray", "Paint")
TO = os.path.join(os.path.dirname(HERE), "hoodrich", "src", "Hoodrich", "Paint")

# Everything in Paint\ is shared. Nothing in it may know which mod it is in -- that is the
# whole point, and it is why PaintConfig exists rather than the engine reading a host's
# Settings class.
RULES = [
    ("namespace Overspray.Paint", "namespace Hoodrich.Paint"),
    ("using Overspray.Core;", "using Hoodrich.Core;"),
    ("Overspray.Paint", "Hoodrich.Paint"),
    ("Overspray.Core", "Hoodrich.Core"),
]


def translate(text):
    for a, b in RULES:
        text = text.replace(a, b)
    return text


def leaks(text):
    """Any mention of either mod by name that the rules did not deal with."""
    return sorted(set(re.findall(r"\bOverspray[A-Za-z.]*", text)))


def main():
    check = "--check" in sys.argv

    if not os.path.isdir(FROM):
        print("no engine at " + FROM)
        return 1

    if not os.path.isdir(TO):
        if check:
            print("no copy at " + TO)
            return 1
        os.makedirs(TO)

    names = sorted(f for f in os.listdir(FROM) if f.endswith(".cs"))
    drift = 0

    for name in names:
        src = io.open(os.path.join(FROM, name), encoding="utf-8-sig").read()
        want = translate(src)

        left = leaks(want)
        if left:
            print("  LEAK  %-18s still says %s" % (name, ", ".join(left)))
            drift += 1
            continue

        dst = os.path.join(TO, name)
        have = io.open(dst, encoding="utf-8-sig").read() if os.path.exists(dst) else None

        if have == want:
            print("  same  " + name)
            continue

        if check:
            print("  DRIFT %-18s %s" % (name, "missing" if have is None else "differs"))
            drift += 1
            continue

        io.open(dst, "w", encoding="utf-8-sig", newline="").write(want)
        print("  %-5s %s" % ("new" if have is None else "sync", name))

    # Anything in the copy that is not in the source is a file somebody added on the wrong
    # side, which is exactly how a fork starts.
    extra = sorted(f for f in os.listdir(TO) if f.endswith(".cs") and f not in names)
    for name in extra:
        print("  EXTRA %-18s only in hoodrich" % name)
        drift += 1

    print()
    print("%d engine files, %d problem(s)" % (len(names), drift))

    return 1 if (check and drift) else 0


if __name__ == "__main__":
    sys.exit(main())
