"""Checks the language files in Assets/Resources/Lang against English.

    python Tools/lang-check.py

For every language file:
  - the same keys as en.json (a missing key shows English in the game; an extra one is a typo),
  - the same {0} placeholders in every string, and the same date tokens ({day}, {MONTH}, ...),
  - lists ("date.months", "world.names", ...) of the same length,
  - "_name" filled in (the language picker shows it),
  - every letter present in Poppins, unless the file names a fallback font in "_font".
    Poppins covers Latin (Western and Central European, Turkish, Indonesian) — not Cyrillic,
    Greek, CJK, Arabic, Thai or Vietnamese.

Exit code 1 if anything is wrong. Needs fontTools for the letter check (pip install fonttools);
without it that check is skipped with a note.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LANG = os.path.join(ROOT, "Assets", "Resources", "Lang")
FONTS = [os.path.join(ROOT, "Assets", "Fonts", f) for f in
         ("Poppins-ExtraBold.ttf", "Poppins-SemiBold.ttf", "Poppins-Regular.ttf")]

PLACEHOLDER = re.compile(r"\{(\d+)\}|\{(day|month|MONTH|mon|MON)\}")
# Characters the game draws itself or never shows in a label (share text goes to other apps).
IGNORE = set("\n\t ") | set("★☆🔥⏱")
SHARE_KEYS = ("share.", "reminder.")


def tokens(text):
    return sorted(m.group(0) for m in PLACEHOLDER.finditer(text))


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def coverage():
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return None
    sets = [set(TTFont(p).getBestCmap().keys()) for p in FONTS if os.path.exists(p)]
    return set.intersection(*sets) if sets else None


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    files = sorted(f for f in os.listdir(LANG) if f.endswith(".json"))
    if "en.json" not in files:
        print("en.json yok — karşılaştırma dili o.")
        return 1

    reference = load(os.path.join(LANG, "en.json"))
    glyphs = coverage()
    if glyphs is None:
        print("not: fontTools yok, harf kontrolü atlandı (pip install fonttools)")

    failures = 0
    for name in files:
        code = name[:-5]
        try:
            data = load(os.path.join(LANG, name))
        except json.JSONDecodeError as e:
            print(f"[{code}] okunamadı: {e}")
            failures += 1
            continue

        problems = []
        for key in reference:
            if key not in data:
                problems.append(f"eksik anahtar: {key}")
        for key in data:
            if key not in reference:
                problems.append(f"fazla anahtar: {key}")

        for key, ref in reference.items():
            value = data.get(key)
            if value is None or key.startswith("_"):
                continue
            if isinstance(ref, list):
                if not isinstance(value, list) or len(value) != len(ref):
                    problems.append(f"{key}: {len(ref)} öğeli liste olmalı")
            elif not isinstance(value, str):
                problems.append(f"{key}: metin olmalı")
            elif key.startswith("date."):
                # Each language orders and spells its dates its own way; it only has to show the day.
                if tokens(ref) and "{day}" not in value:
                    problems.append(f"{key}: {{day}} yok")
            elif tokens(value) != tokens(ref):
                problems.append(f"{key}: yer tutucular {tokens(value)} ≠ {tokens(ref)}")

        if not str(data.get("_name", "")).strip():
            problems.append("_name boş (dil seçicide görünür)")

        if glyphs is not None and not data.get("_font"):
            missing = set()
            for key, value in data.items():
                if key.startswith("_") or key.startswith(SHARE_KEYS):
                    continue
                for text in (value if isinstance(value, list) else [value]):
                    missing |= {c for c in text if c not in IGNORE and ord(c) not in glyphs}
            if missing:
                problems.append("Poppins'te olmayan harfler (\"_font\" ile yedek font gerekir): " + "".join(sorted(missing)))

        status = "TAMAM" if not problems else f"{len(problems)} sorun"
        print(f"[{code}] {data.get('_name', '?')} — {len(data)} anahtar — {status}")
        for p in problems:
            print("   " + p)
        failures += bool(problems)

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
