#!/usr/bin/env python3
"""
Build full Alien Skies catalogs from public archives.

  stars.csv       Hipparcos (I/239 photometry + I/311 van Leeuwen 2007 parallaxes),
                  optionally topped up with Gaia DR3 for fainter stars.
  exoplanets.csv  NASA Exoplanet Archive, Planetary Systems Composite Parameters (pscomppars).

Copy the outputs to %APPDATA%\\AlienSkies\\data (from WSL: /mnt/c/Users/<you>/AppData/Roaming/AlienSkies/data)
and the screen saver will use them instead of the built-in samples.

Requires: pip install pyvo
"""
from __future__ import annotations

import argparse
import csv
import math
from pathlib import Path

import numpy as np
import pyvo

VIZIER_TAP = "https://tapvizier.cds.unistra.fr/TAPVizieR/tap"
GAIA_TAP = "https://gea.esac.esa.int/tap-server/tap"
NASA_TAP = "https://exoplanetarchive.ipac.caltech.edu/TAP"

SAMPLE_STARS = Path(__file__).resolve().parents[1] / "src" / "AlienSkies.Core" / "Data" / "stars.csv"


def sample_names() -> dict[str, str]:
    """Reuse the proper names from the bundled sample (keyed by HIP)."""
    names: dict[str, str] = {}
    with SAMPLE_STARS.open() as f:
        for row in csv.DictReader(line for line in f if not line.startswith("#")):
            if row["name"]:
                names[row["hip"]] = row["name"]
    return names


def hipparcos() -> list[dict]:
    tap = pyvo.dal.TAPService(VIZIER_TAP)
    print("Querying Hipparcos photometry (I/239)...")
    phot = tap.run_async('SELECT HIP, RAICRS, DEICRS, Vmag, "B-V" FROM "I/239/hip_main"').to_table()
    print("Querying Hipparcos new reduction parallaxes (I/311)...")
    astro = tap.run_async('SELECT HIP, Plx, e_Plx FROM "I/311/hip2"').to_table()

    plx = {int(r["HIP"]): (float(r["Plx"]), float(r["e_Plx"])) for r in astro}
    names = sample_names()
    rows = []
    for r in phot:
        hip = int(r["HIP"])
        if hip not in plx or _masked(r["Vmag"]) or _masked(r["RAICRS"]):
            continue
        p, ep = plx[hip]
        if p <= 0:
            continue
        rows.append({
            "hip": str(hip),
            "name": names.get(str(hip), ""),
            "ra_deg": f'{float(r["RAICRS"]):.6f}',
            "dec_deg": f'{float(r["DEICRS"]):.6f}',
            "plx_mas": f"{p:.3f}",
            "vmag": f'{float(r["Vmag"]):.2f}',
            "bv": "" if _masked(r["B-V"]) else f'{float(r["B-V"]):.3f}',
        })
    print(f"  {len(rows):,} Hipparcos stars with positive parallax")
    return rows


def gaia(g_limit: float, min_plx_over_error: float) -> list[dict]:
    """Gaia DR3 stars fainter than G=8 (brighter ones are saturated in Gaia and already in Hipparcos)."""
    tap = pyvo.dal.TAPService(GAIA_TAP)
    q = f"""
        SELECT source_id, ra, dec, parallax, phot_g_mean_mag, bp_rp
        FROM gaiadr3.gaia_source
        WHERE phot_g_mean_mag BETWEEN 8 AND {g_limit}
          AND parallax_over_error > {min_plx_over_error}
          AND bp_rp IS NOT NULL
    """
    print(f"Querying Gaia DR3 (8 <= G <= {g_limit}); this can take several minutes...")
    rows = []
    for r in tap.run_async(q, maxrec=5_000_000).to_table():
        x = float(r["bp_rp"])
        # Riello et al. 2021 G-V polynomial; B-V is a rough linear stand-in used only for tint.
        v = float(r["phot_g_mean_mag"]) - (-0.02704 + 0.01424 * x - 0.2156 * x**2 + 0.01426 * x**3)
        rows.append({
            "hip": f'G{int(r["source_id"])}',
            "name": "",
            "ra_deg": f'{float(r["ra"]):.6f}',
            "dec_deg": f'{float(r["dec"]):.6f}',
            "plx_mas": f'{float(r["parallax"]):.4f}',
            "vmag": f"{v:.2f}",
            "bv": f"{0.75 * x - 0.1:.3f}",
        })
    print(f"  {len(rows):,} Gaia stars")
    return rows


def exoplanets() -> list[dict]:
    tap = pyvo.dal.TAPService(NASA_TAP)
    cols = "pl_name,hostname,ra,dec,sy_dist,sy_vmag,st_rad,st_mass,pl_orbsmax,pl_orbper,pl_orbincl,pl_orbeccen"
    print("Querying NASA Exoplanet Archive (pscomppars)...")
    table = tap.search(f"SELECT {cols} FROM pscomppars WHERE sy_dist IS NOT NULL").to_table()
    rows = []
    for r in table:
        rows.append({c: "" if _masked(r[c]) else str(r[c]).replace(",", " ") for c in cols.split(",")})
    print(f"  {len(rows):,} planets")
    return rows


def write(path: Path, rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    print(f"Wrote {path} ({len(rows):,} rows)")


def _masked(v) -> bool:
    if v is None or np.ma.is_masked(v):
        return True
    try:
        return math.isnan(float(v))
    except (TypeError, ValueError):
        return str(v).strip() in ("", "--")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, default=Path("data-out"), help="output folder")
    ap.add_argument("--gaia-limit", type=float, default=0,
                    help="also include Gaia DR3 stars down to this G magnitude (e.g. 10). 0 = Hipparcos only")
    ap.add_argument("--min-plx-snr", type=float, default=5, help="Gaia parallax_over_error cut")
    ap.add_argument("--skip-stars", action="store_true")
    ap.add_argument("--skip-planets", action="store_true")
    args = ap.parse_args()

    if not args.skip_stars:
        stars = hipparcos()
        if args.gaia_limit > 8:
            stars += gaia(args.gaia_limit, args.min_plx_snr)
        write(args.out / "stars.csv", stars)
    if not args.skip_planets:
        write(args.out / "exoplanets.csv", exoplanets())

    # constellations.csv comes from tools/build_constellations.py; copy the bundled one alongside.
    src = SAMPLE_STARS.with_name("constellations.csv")
    (args.out / "constellations.csv").write_text(src.read_text())


if __name__ == "__main__":
    main()
