# Alien Skies

**The night sky as it would look from a planet orbiting another star.**

Alien Skies is a screen saver for Windows and Android, plus an Android live wallpaper, that puts you on the surface of a real exoplanet, such as TRAPPIST-1 e, Proxima Centauri b or any of the ~6,000 others in the NASA Exoplanet Archive, and shows the stars as they would appear from there. Star positions and distances come from real catalogs (Hipparcos, optionally Gaia DR3). The sky is computed, not painted.

Move away from Earth and familiar constellations stretch and break apart. Our own Sun turns into an ordinary star in someone else's sky, labelled so you can find it. On a tidally locked world, the host star stays fixed overhead while the stars wheel past once per orbit.

> The idea dates from about 2006, when it was called "Alien Skies" and never got finished. The math was never the hard part. What was missing was a catalog with good distances for enough stars, and Gaia supplied that.

---

## What you see

- **A real sky from another star.** Every catalog star is re-centred on the host star, with its direction and brightness recomputed for the new distance.
- **Our Sun, labelled.** Sol is marked with its distance and apparent magnitude. The info line tells you where to look for it, by azimuth and altitude, even when it is off-screen or below the horizon, and how many years ago the light you're seeing left it.
- **Earth's constellations, distorted.** Optionally, the figures of Orion and the Big Dipper are drawn so you can watch them warp with distance.
- **A living planet.** It orbits its star on its real (Keplerian) orbit and spins on its axis. Tidally locked worlds even show the small wobble (libration) caused by an eccentric orbit.
- **Day and night.** When the host star is up, the sky brightens and the stars wash out. Switch the atmosphere off for an airless world.
- **Ground and horizon.** You're standing somewhere: pick your latitude, longitude and the direction you're facing.
- **Multi-monitor panorama.** Each extra screen continues the view to the right instead of repeating it.
- **Tour mode.** Hop to a random planet and a random spot on its surface at a fixed interval.
- **Android too.** The same sky as an Android screen saver (while charging or docked) and as a live wallpaper, where swiping between home-screen pages pans across the horizon.

## Status

Early development. The astronomy core is unit-tested. All hosts draw with one shared SkiaSharp renderer, and `tools/AlienSkies.Preview` renders frames to PNG so drawing changes can be checked on any OS. The Windows saver and the Android app both build cleanly; the Android screen saver and live wallpaper have not yet been tested on a device. The repository embeds a small sample catalog so it runs out of the box. See [Star and planet data](#star-and-planet-data) to install the full catalogs.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build
- **Windows saver:** Windows 10 or 11, x64. A framework-dependent build needs the .NET 10 Desktop Runtime; a self-contained build needs nothing extra.
- **Android:** the .NET `android` workload (`dotnet workload install android`) to build; Android 8.0 (API 26) or later to run
- Optional, for the full catalogs: Python 3 (WSL works well) with `pyvo`

## Build, run and install

```powershell
git clone https://github.com/ctdltd/AlienSkies.git
cd AlienSkies

dotnet build
dotnet test

# Try it in a normal, resizable window (Esc closes):
dotnet run --project src/AlienSkies.Screensaver -- /w

# Open the settings dialog:
dotnet run --project src/AlienSkies.Screensaver -- /c
```

Build the screen saver file:

```powershell
./publish.ps1                 # small; needs the .NET 10 Desktop Runtime
./publish.ps1 -SelfContained  # larger; runs on any Windows x64 machine
```

This produces `artifacts\AlienSkies.scr`. Right-click it and choose **Install**, or copy it to `C:\Windows\System32` (needs admin) so it appears in **Settings → Personalization → Lock screen → Screen saver**.

### Android

```powershell
dotnet publish src/AlienSkies.Android -c Release
adb install src/AlienSkies.Android/bin/Release/net10.0-android/publish/com.certificationtoolanddie.alienskies-Signed.apk
```

The app opens to its settings screen, which has **Preview**, **Screen saver settings** and **Set as live wallpaper** buttons.

- **Screen saver:** Settings → Display → Screen saver → Alien Skies. It runs while the device is charging or docked; a touch dismisses it.
- **Live wallpaper:** use the button in the app, or pick Alien Skies in the system wallpaper picker. Swiping between home-screen pages pans the view; the info text is hidden so it doesn't clutter the home screen.
- **Android TV:** works as a normal screen saver. Google TV devices hide third-party screen savers from their settings, but you can still select it with adb:

  ```bash
  adb shell settings put secure screensaver_components com.certificationtoolanddie.alienskies/com.certificationtoolanddie.alienskies.AlienSkiesDream
  ```

The release build is signed with a development key, which is fine for sideloading. Publishing to Google Play needs your own signing key.

### Command-line switches (Windows)

| Switch | Meaning |
|---|---|
| `/s` | Run full screen (what Windows uses when the saver starts) |
| `/p <hwnd>` | Draw inside the small preview monitor in Screen Saver Settings |
| `/c` | Show the settings dialog (also the default with no arguments) |
| `/w` | Development: run in a normal window |

## How it works

1. **Load the catalog once.** Each star's right ascension, declination and parallax become a 3-D position in parsecs, centred on the Sun. Its apparent magnitude and distance give its true (absolute) brightness.
2. **Move the viewpoint once per planet.** Subtract the host star's position from every star. The result gives the new direction, and the new distance gives the new apparent brightness:

   `m_new = M + 5·log10(d_new) − 5`

   Stars too faint to see are dropped, and our Sun is added as a star at the origin.
3. **Every frame,** work out where the observer's horizon is from the planet's orbit, spin, latitude and longitude, then project the stars onto the screen.

A planet's orbit is a few AU across, which is tiny next to the parsecs between stars. So the stars' directions only need computing once per planet, and each frame is just one cheap pass over the visible stars. All vectors share the same ICRS-aligned axes throughout. The details are in [`docs/DESIGN.md`](docs/DESIGN.md).

## Configuration

Settings live in `%APPDATA%\AlienSkies\settings.json` on Windows (in the app's private storage on Android) and are edited through the settings dialog or the Android settings screen. Both are generated from the same settings class, so they always offer the same options.

| Group | Setting | Notes |
|---|---|---|
| **Planet** | Planet | Any planet name from the exoplanet catalog |
| | Use catalog orbit | Off = use the overrides below |
| | Orbital radius (AU), orbital period (days) | Period 0 = derive it from the radius and the star's mass (Kepler's third law) |
| | Eccentricity, inclination, ascending node | Catalog conventions; see below |
| **Spin** | Rotation | Auto (locks orbits closer than 0.1 AU), Tidally locked, or Free |
| | Rotation period (hours) | Day length for free-rotating planets; negative = retrograde |
| | Axial tilt, tilt direction | Sets which stars never set, and gives the planet seasons |
| **Observer** | Latitude, longitude | On a locked world: 0° = star overhead, 90°/270° = endless sunset, 180° = endless night |
| **View** | Look azimuth, altitude, field of view | Which way you're facing |
| | Pan across monitors | Continuous panorama on multiple screens |
| | Limiting magnitude, star size | 6.5 ≈ a dark sky to the naked eye |
| | Atmosphere / daylight, ground | |
| | Earth's constellations, star labels, mark our Sun, info overlay | |
| **Time** | Time scale, start time | 600× = one Earth day every 2.4 minutes |
| **Tour** | Tour mode, interval | Random planet and random spot on its surface |

### Two conventions worth knowing

- **Inclination is measured from Earth.** Catalog inclinations describe how an orbit is tilted *as seen from Earth*: 0° is face-on to us and 90° is edge-on. Most planets with a known inclination were found because they pass in front of their star (transit), so they're close to 90°. That means **our Sun lies near their ecliptic**, so it travels through their zodiac. At 0°, it would sit near their celestial pole instead.
- **Radius and period are linked.** For a given star mass, Kepler's third law ties orbital radius and period together. Setting the period to 0 lets the program work it out, so the two never contradict each other.

## Star and planet data

The executable embeds a small development sample: about 35 bright stars, 4 planets, and the Orion and Big Dipper figures. Build the full catalogs from WSL:

```bash
tools/build_catalog.sh                  # Hipparcos (~118,000 stars) + all known exoplanets
tools/build_catalog.sh --gaia-limit 10  # also add Gaia DR3 stars down to magnitude G = 10
cp data-out/*.csv "/mnt/c/Users/$USER/AppData/Roaming/AlienSkies/data/"
```

The Windows saver looks for each file in `%APPDATA%\AlienSkies\data`, then in a `data` folder next to the executable, and finally falls back to the embedded sample. The **Open data folder** link in the settings dialog takes you straight there.

For Android, copy the CSVs into `src/AlienSkies.Android/Assets/data/` before building, and they're bundled into the APK.

| File | Columns | Source |
|---|---|---|
| `stars.csv` | `hip, name, ra_deg, dec_deg, plx_mas, vmag, bv` | Hipparcos (I/239 photometry, I/311 parallaxes); optionally Gaia DR3 |
| `exoplanets.csv` | `pl_name, hostname, ra, dec, sy_dist, sy_vmag, st_rad, st_mass, pl_orbsmax, pl_orbper, pl_orbincl, pl_orbeccen` | NASA Exoplanet Archive, Planetary Systems Composite Parameters |
| `constellations.csv` | `constellation, from_id, to_id` | Hand-curated; ids match `stars.csv` |

## Project layout

```
src/AlienSkies.Core/          UI-free astronomy: coordinates, catalogs, orbit and spin, sky re-projection
src/AlienSkies.Rendering/     Shared scene, settings and SkiaSharp renderer used by every host
src/AlienSkies.Screensaver/   Windows host: switch handling, settings dialog, multi-monitor panorama
src/AlienSkies.Android/       Android host: screen saver, live wallpaper (with a small Java shim), settings app
tests/AlienSkies.Core.Tests/  xUnit tests: Sol seen from α Centauri, tidal locking, Kepler, reference frames
tools/AlienSkies.Preview/     Renders single frames to PNG on any OS
tools/                        Catalog builder (Python, with a WSL wrapper script)
docs/DESIGN.md                The math, conventions, known simplifications and roadmap
publish.ps1                   Builds AlienSkies.scr
```

## Roadmap

Highlights, with the full list in [`docs/DESIGN.md`](docs/DESIGN.md):

- Companion stars as second suns, and sibling planets as bright wandering lights (in the TRAPPIST-1 system they would look larger than our Moon)
- All 88 of Earth's constellations, plus procedurally drawn *local* constellations
- A Milky Way band, atmospheric dimming near the horizon, and sky colour from the host star's temperature
- Proper motion: the sky from there, thousands of years ago or ahead
- Cinematic flights between systems in tour mode
- GPU rendering, and a macOS version

## Acknowledgements

This project uses data from:

- **The NASA Exoplanet Archive**, which is operated by the California Institute of Technology, under contract with the National Aeronautics and Space Administration under the Exoplanet Exploration Program.
- **The ESA Hipparcos mission** (ESA 1997, *The Hipparcos and Tycho Catalogues*, ESA SP-1200), with parallaxes from the new reduction by van Leeuwen (2007, *A&A* 474, 653).
- **The ESA mission Gaia** (https://www.cosmos.esa.int/gaia), processed by the Gaia Data Processing and Analysis Consortium (DPAC, https://www.cosmos.esa.int/web/gaia/dpac/consortium). Funding for the DPAC has been provided by national institutions, in particular the institutions participating in the Gaia Multilateral Agreement.
- **The VizieR catalogue access tool**, CDS, Strasbourg, France.

## License

Copyright © 2026 Certification Tool and Die, Ltd. All rights reserved.
