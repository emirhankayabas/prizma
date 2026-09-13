"""Checks the themes in Assets/Scripts/Game/Themes.cs for contrast and distinctness.

    python Tools/theme-check.py

Reads the palettes straight from the C# source, so it checks what ships. Exits 1 on a failure.
Colours are compared as authored (sRGB): every surface they are checked against is opaque, and
opaque colours land on screen as authored even in Linear colour space.

What each rule protects:
- white text >= 7:1 on the surfaces cards, buttons and the board are made of
- white label >= 3:1 on the accent (primary buttons carry bold display type)
- gold and mint >= 4.5:1 on inset chips (best score, combo, moves)
- TextOnGround >= 3:1 on both ends of the ground (tagline, page numbers)
- every block >= 3:1 against the board, so a piece never sinks into it
- any two blocks of one theme >= dE 22 apart: single-colour lines score a bonus, and two colours
  that look alike would be a trap
- no block close to crystal or ice (level mode overlays), or it reads as one
- every block >= dE 24 from the ground where the tray sits: tray pieces rest on the ground, not on
  the board. Candy's pink piece once sat at dE 10 on its pink ground and all but vanished. The bar
  is Prizma's blue piece on its blue ground (dE 26), which was never a complaint.
"""
import itertools
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Assets", "Scripts", "Game", "Themes.cs")

GOLD, MINT, WHITE = "#FFC24B", "#38D39F", "#FFFFFF"
CRYSTAL, ICE = "#E8FBFF", "#C9E9FF"


def parse(text):
    themes = {}
    for m in re.finditer(r'new Theme\("([^"]+)",\s*(\d+),\s*new Mood\s*\{(.*?)\},\s*((?:"#[0-9A-Fa-f]{6}"\s*,?\s*)+)\)', text, re.S):
        name, body, blocks = m.group(1), m.group(3), m.group(4)
        mood = dict(re.findall(r'(\w+)\s*=\s*"(#[0-9A-Fa-f]{6})"', body))
        mood["Blocks"] = re.findall(r'"(#[0-9A-Fa-f]{6})"', blocks)
        themes[name] = mood
    return themes


def lin(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return [((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c]


def luminance(h):
    r, g, b = lin(h)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    x, y = sorted([luminance(a), luminance(b)], reverse=True)
    return (x + 0.05) / (y + 0.05)


def lab(h):
    r, g, b = lin(h)
    x = (r * .4124 + g * .3576 + b * .1805) / .95047
    y = r * .2126 + g * .7152 + b * .0722
    z = (r * .0193 + g * .1192 + b * .9505) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def mix(a, b, k):
    ca = [int(a[i:i + 2], 16) for i in (1, 3, 5)]
    cb = [int(b[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{round(x + (y - x) * k):02X}" for x, y in zip(ca, cb))


def delta_e(a, b):
    return sum((p - q) ** 2 for p, q in zip(lab(a), lab(b))) ** .5


def main():
    themes = parse(open(SOURCE, encoding="utf-8").read())
    if len(themes) < 2:
        print(f"could not read themes from {SOURCE}")
        return 1

    fails = []

    def need(theme, ok, message):
        if not ok:
            fails.append(f"{theme}: {message}")

    for name, t in themes.items():
        for surface in ("Surface", "SurfaceHigh", "BoardSurface", "SurfaceButton"):
            c = contrast(WHITE, t[surface])
            need(name, c >= 7, f"white on {surface} {c:.1f}:1 (want 7)")
        c = contrast(WHITE, t["AccentA"])
        need(name, c >= 3, f"white on AccentA {c:.2f}:1 (want 3)")
        for colour, label in ((GOLD, "gold"), (MINT, "mint")):
            c = contrast(colour, t["SurfaceInset"])
            need(name, c >= 4.5, f"{label} on SurfaceInset {c:.1f}:1 (want 4.5)")
        for ground in ("BgTop", "BgBottom"):
            c = contrast(t["TextOnGround"], t[ground])
            need(name, c >= 3, f"TextOnGround on {ground} {c:.2f}:1 (want 3)")
        for i, block in enumerate(t["Blocks"]):
            c = contrast(block, t["BoardSurface"])
            need(name, c >= 3, f"block {i} on board {c:.1f}:1 (want 3)")
            for overlay, label in ((CRYSTAL, "crystal"), (ICE, "ice")):
                d = delta_e(block, overlay)
                need(name, d >= 20, f"block {i} reads as {label} (dE {d:.0f})")
        tray_ground = mix(t["BgTop"], t["BgBottom"], 0.8)
        for i, block in enumerate(t["Blocks"]):
            d = delta_e(block, tray_ground)
            need(name, d >= 24, f"block {i} sinks into the ground under the tray (dE {d:.0f}, want 24)")
        closest = min((delta_e(a, b), i, j) for (i, a), (j, b) in itertools.combinations(enumerate(t["Blocks"]), 2))
        need(name, closest[0] >= 22, f"blocks {closest[1]} and {closest[2]} too alike (dE {closest[0]:.0f}, want 22)")

    print("ground apart (dE top / bottom), blocks apart (mean dE):")
    for a, b in itertools.combinations(themes, 2):
        ta, tb = themes[a], themes[b]
        blocks = sum(delta_e(x, y) for x, y in zip(ta["Blocks"], tb["Blocks"])) / len(ta["Blocks"])
        print(f"  {a:9s} {b:9s} {delta_e(ta['BgTop'], tb['BgTop']):4.0f} / {delta_e(ta['BgBottom'], tb['BgBottom']):4.0f}   {blocks:4.0f}")

    if fails:
        print("FAIL")
        for f in fails:
            print("  " + f)
        return 1

    print(f"OK — {len(themes)} themes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
