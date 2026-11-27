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
SAMPLE_PLANETS = SAMPLE_STARS.with_name("exoplanets.csv")

# Parallax overrides (mas). Hipparcos fits alpha Centauri A and B separately and puts them
# 0.07 pc apart along the line of sight, so they would not be linked as companions (second suns).
# Use the joint system parallax of Kervella et al. 2016 (A&A 594, A107) for both.
PLX_OVERRIDES = {71683: 747.17, 71681: 747.17}


def sample_names() -> dict[str, str]:
    """Reuse the proper names from the bundled sample (keyed by HIP)."""
    names: dict[str, str] = {}
    with SAMPLE_STARS.open() as f:
        for row in csv.DictReader(line for line in f if not line.startswith("#")):
            if row["name"]:
                names[row["hip"]] = row["name"]
    return names


def gaia_hipparcos_crossmatch() -> dict[int, dict]:
    """
    Gaia DR3's own Hipparcos-2 cross-match, keyed by HIP: Gaia source id, parallax and BP-RP.
    Used to drop Gaia duplicates of Hipparcos stars, to rescue Hipparcos stars with no usable
    parallax, and to calibrate BP-RP -> B-V. Returns {} if the Gaia archive can't be reached.
    """
    print("Querying the Gaia DR3 / Hipparcos-2 cross-match...")
    try:
        table = pyvo.dal.TAPService(GAIA_TAP).run_async("""
            SELECT x.original_ext_source_id AS hip, g.source_id, g.parallax, g.parallax_over_error, g.bp_rp
            FROM gaiadr3.hipparcos2_best_neighbour AS x
            JOIN gaiadr3.gaia_source AS g ON g.source_id = x.source_id
        """, maxrec=200_000).to_table()
    except Exception as e:  # noqa: BLE001 - the catalog still builds without it, just less well
        print(f"  cross-match unavailable ({e}); Gaia duplicates and parallax rescue skipped")
        return {}
    xm = {}
    for r in table:
        xm[int(r["hip"])] = {
            "source_id": int(r["source_id"]),
            "plx": None if _masked(r["parallax"]) else float(r["parallax"]),
            "plx_snr": None if _masked(r["parallax_over_error"]) else float(r["parallax_over_error"]),
            "bp_rp": None if _masked(r["bp_rp"]) else float(r["bp_rp"]),
        }
    print(f"  {len(xm):,} Hipparcos stars matched in Gaia")
    return xm


def hipparcos(xmatch: dict[int, dict], min_rescue_snr: float = 5) -> list[dict]:
    tap = pyvo.dal.TAPService(VIZIER_TAP)
    print("Querying Hipparcos photometry (I/239)...")
    phot = tap.run_async('SELECT HIP, RAICRS, DEICRS, Vmag, "B-V" FROM "I/239/hip_main"').to_table()
    print("Querying Hipparcos new reduction parallaxes (I/311)...")
    astro = tap.run_async('SELECT HIP, Plx, e_Plx FROM "I/311/hip2"').to_table()

    plx = {int(r["HIP"]): (float(r["Plx"]), float(r["e_Plx"])) for r in astro}
    names = sample_names()
    rows = []
    rescued = 0
    for r in phot:
        hip = int(r["HIP"])
        if _masked(r["Vmag"]) or _masked(r["RAICRS"]):
            continue
        p = PLX_OVERRIDES.get(hip, plx.get(hip, (0.0, 0.0))[0])
        if p <= 0:
            p = rescue_parallax(xmatch.get(hip), min_rescue_snr)
            if p is None:
                continue
            rescued += 1
        rows.append({
            "hip": str(hip),
            "name": names.get(str(hip), ""),
            "ra_deg": f'{float(r["RAICRS"]):.6f}',
            "dec_deg": f'{float(r["DEICRS"]):.6f}',
            "plx_mas": f"{p:.3f}",
            "vmag": f'{float(r["Vmag"]):.2f}',
            "bv": "" if _masked(r["B-V"]) else f'{float(r["B-V"]):.3f}',
        })
    print(f"  {len(rows):,} Hipparcos stars with positive parallax ({rescued} using Gaia's parallax)")
    return rows


def rescue_parallax(g: dict | None, min_snr: float) -> float | None:
    """
    Gaia's parallax for a Hipparcos star that has no usable one of its own (beta Phe, rho Leo and
    about 25 other stars brighter than V=5 would otherwise vanish from every sky), when significant.
    """
    if not g or g["plx"] is None or g["plx"] <= 0 or (g["plx_snr"] or 0) < min_snr:
        return None
    return g["plx"]


def bv_from_bp_rp_calibration(hip_rows: list[dict], xmatch: dict[int, dict]):
    """
    Empirical BP-RP -> B-V curve from the Hipparcos stars that have both colours. The old straight
    line, 0.75 x - 0.1, made Sun-like stars (BP-RP 0.82, B-V 0.65) about 0.13 too blue.
    """
    pairs = []
    for r in hip_rows:
        g = xmatch.get(int(r["hip"]))
        if r["bv"] and g and g["bp_rp"] is not None:
            pairs.append((g["bp_rp"], float(r["bv"])))
    return fit_bv_curve(pairs)


def fit_bv_curve(pairs, width: float = 0.05, min_per_bin: int = 25):
    """Binned medians of (BP-RP, B-V), linearly interpolated; the end values hold beyond the range."""
    xs, ys = [], []
    if pairs:
        a = np.array(pairs, dtype=float)
        edges = np.arange(-0.6, 5.0 + width, width)
        idx = np.digitize(a[:, 0], edges)
        for i in range(1, len(edges)):
            sel = a[idx == i]
            if len(sel) >= min_per_bin:
                xs.append(float(np.median(sel[:, 0])))
                ys.append(float(np.median(sel[:, 1])))
    if len(xs) < 5:
        print("  BP-RP -> B-V: too few cross-matched stars, using a linear stand-in")
        return lambda x: 0.85 * x - 0.05
    # Keep the curve monotonic so binning noise can't reverse two stars' colours.
    ys = list(np.maximum.accumulate(ys))
    print(f"  BP-RP -> B-V calibrated on {len(pairs):,} stars ({len(xs)} bins, BP-RP {xs[0]:.2f} to {xs[-1]:.2f})")
    return lambda x: float(np.interp(x, xs, ys))


def gaia(g_limit: float, min_plx_over_error: float, hip_source_ids: set[int], bv_of) -> list[dict]:
    """
    Gaia DR3 stars from G=8 (brighter ones are saturated in Gaia) down to g_limit, minus the ones
    already in Hipparcos, which reaches V~12. Kept, those duplicates would split into false double
    stars when seen from another system, because the two catalogs' parallaxes differ.
    """
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
    dupes = 0
    for r in tap.run_async(q, maxrec=5_000_000).to_table():
        if int(r["source_id"]) in hip_source_ids:
            dupes += 1
            continue
        x = float(r["bp_rp"])
        # Riello et al. 2021 G-V polynomial.
        v = float(r["phot_g_mean_mag"]) - (-0.02704 + 0.01424 * x - 0.2156 * x**2 + 0.01426 * x**3)
        rows.append({
            "hip": f'G{int(r["source_id"])}',
            "name": "",
            "ra_deg": f'{float(r["ra"]):.6f}',
            "dec_deg": f'{float(r["dec"]):.6f}',
            "plx_mas": f'{float(r["parallax"]):.4f}',
            "vmag": f"{v:.2f}",
            "bv": f"{bv_of(x):.3f}",
        })
    print(f"  {len(rows):,} Gaia stars ({dupes:,} already in Hipparcos, skipped)")
    return rows


def exoplanets() -> list[dict]:
    tap = pyvo.dal.TAPService(NASA_TAP)
    cols = "pl_name,hostname,ra,dec,sy_dist,sy_vmag,st_rad,st_mass,pl_orbsmax,pl_orbper,pl_orbincl,pl_orbeccen,pl_rade"
    print("Querying NASA Exoplanet Archive (pscomppars)...")
    table = tap.search(f"SELECT {cols} FROM pscomppars WHERE sy_dist IS NOT NULL").to_table()
    rows = []
    for r in table:
        rows.append({c: "" if _masked(r[c]) else str(r[c]).replace(",", " ") for c in cols.split(",")})
    print(f"  {len(rows):,} planets")
    # Keep the sample's hypothetical planets (the default "alf Cen A (hypothetical Earth)" among them),
    # which the archive does not list.
    with SAMPLE_PLANETS.open() as f:
        extra = [r for r in csv.DictReader(line for line in f if not line.startswith("#"))
                 if "hypothetical" in r["pl_name"].lower()]
    known = {r["pl_name"] for r in rows}
    rows = [r for r in extra if r["pl_name"] not in known] + rows
    print(f"  + {len(extra)} hypothetical planet(s) from the bundled sample")
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
        xmatch = gaia_hipparcos_crossmatch()
        stars = hipparcos(xmatch)
        if args.gaia_limit > 8:
            bv_of = bv_from_bp_rp_calibration(stars, xmatch)
            hip_sources = {g["source_id"] for g in xmatch.values()}
            stars += gaia(args.gaia_limit, args.min_plx_snr, hip_sources, bv_of)
        write(args.out / "stars.csv", stars)
    if not args.skip_planets:
        write(args.out / "exoplanets.csv", exoplanets())

    # constellations.csv comes from tools/build_constellations.py; copy the bundled one alongside.
    src = SAMPLE_STARS.with_name("constellations.csv")
    (args.out / "constellations.csv").write_text(src.read_text())


if __name__ == "__main__":
    main()
