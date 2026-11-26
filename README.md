# Alien Skies

A Windows screen saver that shows the night sky as it would look from the surface of a planet around another star. It uses real star positions and distances (Hipparcos, optionally Gaia DR3) and real exoplanet orbits (NASA Exoplanet Archive).

Constellations warp as you move away from Earth, and our own Sun shows up as an ordinary star. On a tidally locked world, the host star sits motionless while the stars wheel past it.

> The idea dates from ~2006. The math was never the hard part; the missing piece was a catalog with good distances, and Gaia supplied it.

## Quick start

```powershell
dotnet build
dotnet test
# Run in a normal window while developing (Esc closes):
dotnet run --project src/AlienSkies.Screensaver -- /w
# Settings dialog:
dotnet run --project src/AlienSkies.Screensaver -- /c
# Build the screen saver:
./publish.ps1                 # framework-dependent, needs .NET 10 Desktop Runtime
./publish.ps1 -SelfContained  # runs anywhere, much larger
```

To install it, right-click `artifacts\AlienSkies.scr` and choose **Install**, or copy it to `C:\Windows\System32`.

## Layout

| Path | Purpose |
|---|---|
| `src/AlienSkies.Core` | UI-free astronomy: coordinate transforms, catalogs, orbit and spin model, sky re-projection. Fully unit-testable. |
| `src/AlienSkies.Screensaver` | WinForms host: `/s` `/p` `/c` `/w` handling, multi-monitor panorama, settings dialog, GDI+ renderer. |
| `tests/AlienSkies.Core.Tests` | xUnit tests (Sol from α Cen, tidal locking, Kepler, frames). |
| `tools/build_catalog.py` / `.sh` | Pulls full catalogs from VizieR, Gaia, and NASA TAP services. |
| `docs/DESIGN.md` | Conventions, the math, and the roadmap. |

## Catalogs

The executable embeds a small development sample: about 35 bright stars, 4 planets, Orion, and the Big Dipper. Build the real catalogs from WSL:

```bash
tools/build_catalog.sh                  # Hipparcos (~118k stars) + all exoplanets
tools/build_catalog.sh --gaia-limit 10  # add Gaia DR3 down to G=10
cp data-out/*.csv "/mnt/c/Users/$USER/AppData/Roaming/AlienSkies/data/"
```

For each file, the loader checks `%APPDATA%\AlienSkies\data` first, then `<exe dir>\data`, then the embedded sample.

## Settings

Settings are stored in `%APPDATA%\AlienSkies\settings.json` and edited through the `/c` dialog.

| Group | Setting | Notes |
|---|---|---|
| Planet | Planet | Any `pl_name` from `exoplanets.csv` |
| | Use catalog orbit | Off = use the overrides below |
| | Orbital radius (AU), period (days) | Period 0 = derive from Kepler's 3rd law and host mass |
| | Eccentricity, inclination, ascending node | Archive conventions; see DESIGN.md |
| Spin | Rotation: Auto / Tidally locked / Free | Auto locks orbits under 0.1 AU |
| | Rotation period (h), axial tilt, tilt direction | |
| Observer | Latitude, longitude | On locked worlds, longitude 0 = substellar point |
| View | Azimuth, altitude, field of view, multi-monitor panorama | |
| | Limiting magnitude, star scale, atmosphere/daylight, ground | |
| | Earth constellations, labels, mark Sol, info overlay | |
| Time | Time scale, start phase | |
| Tour | Tour mode, interval | Random planet and random latitude |
