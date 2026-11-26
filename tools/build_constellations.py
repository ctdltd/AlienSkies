#!/usr/bin/env python3
"""
Regenerate src/AlienSkies.Core/Data/constellations.csv with all 88 modern (IAU) constellation
figures, and top up the embedded sample stars.csv with every star those figures use.

  Figures: Stellarium "modern" sky culture (skycultures/modern/index.json), CC BY-SA 4.0.
           https://github.com/Stellarium/stellarium/tree/master/skycultures/modern
  Stars:   HYG database v4.0 (Hipparcos new reduction positions, parallaxes, V and B-V), CC BY-SA 4.0.
           https://github.com/astronexus/HYG-Database

Rows already in stars.csv are kept as they are; only missing stars are appended. The full catalog
from tools/build_catalog.py already contains every Hipparcos star, so it needs no top-up.

Usage: python3 tools/build_constellations.py   (standard library only)
"""
from __future__ import annotations

import csv
import gzip
import io
import json
import urllib.request
from pathlib import Path

FIGURES_URL = "https://raw.githubusercontent.com/Stellarium/stellarium/master/skycultures/modern/index.json"
HYG_URL = "https://raw.githubusercontent.com/astronexus/HYG-Database/main/hyg/CURRENT/hygdata_v40.csv.gz"

DATA = Path(__file__).resolve().parents[1] / "src" / "AlienSkies.Core" / "Data"
HEADER = [
    "# All 88 IAU constellations, by stars.csv id. Seen from elsewhere they distort - that's the fun.",
    "# Figures from the Stellarium 'modern' sky culture, (c) Stellarium team, CC BY-SA 4.0",
    "# (https://creativecommons.org/licenses/by-sa/4.0/), converted to one row per line segment.",
    "# Regenerate with tools/build_constellations.py.",
]


def fetch(url: str) -> bytes:
    print(f"Downloading {url}")
    with urllib.request.urlopen(url) as r:
        return r.read()


def segments() -> list[tuple[str, str, str]]:
    sky = json.loads(fetch(FIGURES_URL))
    rows = []
    for con in sky["constellations"]:
        name = con["common_name"]["native"]
        for polyline in con["lines"]:
            hips = [str(h) for h in polyline if isinstance(h, int)]
            rows += [(name, a, b) for a, b in zip(hips, hips[1:])]
    print(f"  {len(sky['constellations'])} constellations, {len(rows)} segments")
    return rows


def write_constellations(rows: list[tuple[str, str, str]]) -> None:
    with (DATA / "constellations.csv").open("w", newline="") as f:
        f.write("constellation,from_id,to_id\n")
        f.write("\n".join(HEADER) + "\n")
        csv.writer(f, lineterminator="\n").writerows(rows)


def top_up_stars(needed: set[str]) -> None:
    path = DATA / "stars.csv"
    lines = path.read_text().splitlines()
    have = {l.split(",")[0] for l in lines if l and not l.startswith("#")}
    missing = needed - have
    if not missing:
        print("stars.csv already has every figure star")
        return

    hyg = csv.DictReader(io.TextIOWrapper(gzip.GzipFile(fileobj=io.BytesIO(fetch(HYG_URL))), "utf-8"))
    added = []
    for r in hyg:
        hip = r["hip"]
        if hip not in missing:
            continue
        dist = float(r["dist"])
        if dist <= 0 or dist >= 100_000:  # HYG uses 100000 pc for "no usable parallax"
            continue
        added.append(",".join([
            hip, r["proper"].replace(",", " "),
            f'{float(r["ra"]) * 15:.4f}', f'{float(r["dec"]):.4f}',
            f"{1000 / dist:.2f}", f'{float(r["mag"]):.2f}', r["ci"] and f'{float(r["ci"]):.2f}',
        ]))
    added.sort(key=lambda l: int(l.split(",")[0]))
    lost = len(missing) - len(added)
    note = [
        "# Constellation-figure stars below, from the HYG database v4.0 (CC BY-SA 4.0,",
        "# https://github.com/astronexus/HYG-Database), added by tools/build_constellations.py.",
    ]
    path.write_text("\n".join(lines + note + added) + "\n")
    print(f"Added {len(added)} stars to stars.csv" + (f" ({lost} had no usable parallax)" if lost else ""))


def main() -> None:
    rows = segments()
    write_constellations(rows)
    top_up_stars({h for _, a, b in rows for h in (a, b)})


if __name__ == "__main__":
    main()
