# Alien Skies

**The night sky as it would look from a planet orbiting another star.**

Alien Skies is a screen saver for Windows and Android, plus an Android live wallpaper, that puts you on the surface of a real exoplanet, such as TRAPPIST-1 e, Proxima Centauri b or any of the ~6,000 others in the NASA Exoplanet Archive, and shows the stars as they would appear from there. Star positions and distances come from real catalogs (Hipparcos, optionally Gaia DR3). The sky is computed, not painted.

Move away from Earth and familiar constellations stretch and break apart. Our own Sun turns into an ordinary star in someone else's sky, labelled so you can find it. On a tidally locked world, the host star stays fixed overhead while the stars wheel past once per orbit.

> The idea dates from about 2006, when it was called "Alien Skies" and never got finished. The math was never the hard part. What was missing was a catalog with good distances for enough stars, and Gaia supplied that.

---

## What you see

- **A real sky from another star.** Every catalog star is re-centred on the host star, with its direction and brightness recomputed for the new distance.
- **Our Sun, labelled.** Sol is marked with its distance and apparent magnitude. The info line tells you where to look for it, by azimuth and altitude, even when it is off-screen or below the horizon, and how many years ago the light you're seeing left it.
- **Earth's constellations, distorted.** Optionally, the line figures of all 88 constellations are drawn so you can watch them warp with distance.
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
- **Android:** to build, the .NET `android` workload, a JDK (17 or later) and the Android SDK (see [Android](#android)); Android 8.0 (API 26) or later to run. The Android app builds on Windows, macOS or Linux.
- Optional, for the full catalogs: Python 3 (WSL works well) with `pyvo`

## Build, run and install

```powershell
git clone https://github.com/ctdltd/AlienSkies.git
cd AlienSkies

dotnet build          # whole solution, including Android (needs the android workload)
dotnet test

# Without the android workload, use the solution filter that leaves Android out
# (this is what CI builds), or build a single project:
dotnet build AlienSkies.CI.slnf
dotnet test AlienSkies.CI.slnf
dotnet build src/AlienSkies.Screensaver

# Try it in a normal, resizable window (Esc closes):
dotnet run --project src/AlienSkies.Screensaver -- /w

# Open the settings dialog:
dotnet run --project src/AlienSkies.Screensaver -- /c
```

Render a single frame to a PNG, on any OS (handy for checking drawing changes without a screen saver):

```bash
dotnet run --project tools/AlienSkies.Preview -- --planet "TRAPPIST-1 e" --lat 30 --az 180 --out sky.png
```

Use `--star "Tau Ceti"` (and optionally `--name "My world"`) instead of `--planet` to stand on an imaginary planet around any catalog star. Other options are `--lon`, `--days`, `--alt`, `--fov`, `--width`, `--height` and `--density`.

Build the screen saver file:

```powershell
./publish.ps1                 # small; needs the .NET 10 Desktop Runtime
./publish.ps1 -SelfContained  # larger; runs on any Windows x64 machine
```

This produces `artifacts\AlienSkies.scr`. Right-click it and choose **Install**, or copy it to `C:\Windows\System32` (needs admin) so it appears in **Settings → Personalization → Lock screen → Screen saver**.

### Android

**One-time setup.** Install the workload, then the JDK and Android SDK. Visual Studio's ".NET Multi-platform App UI" workload installs all three; from the command line:

```powershell
dotnet workload install android

# Downloads the Android SDK and platform tools (adb) into the folder you name.
# Point JavaSdkDirectory at an installed JDK 17+, e.g. Microsoft Build of OpenJDK.
dotnet build src/AlienSkies.Android -t:InstallAndroidDependencies `
  -p:AndroidSdkDirectory="$HOME/android-sdk" -p:JavaSdkDirectory="<path to JDK>" `
  -p:AcceptAndroidSDKLicenses=True
```

If the SDK or JDK isn't in a default location, pass the same `-p:AndroidSdkDirectory=...` and `-p:JavaSdkDirectory=...` to the build and publish commands below, or set the `ANDROID_HOME` and `JAVA_HOME` environment variables.

**Build the APK.**

```powershell
dotnet publish src/AlienSkies.Android -c Release
```

This writes two packages to `src/AlienSkies.Android/bin/Release/net10.0-android/publish/`:

- `com.certificationtoolanddie.alienskies-Signed.apk`, for installing directly (sideloading)
- `com.certificationtoolanddie.alienskies-Signed.aab`, the bundle format Google Play wants

**Install it.** Either way works:

- **With adb (USB or Wi-Fi debugging).** Turn on Developer options on the device (tap **Build number** seven times in Settings → About phone), enable **USB debugging**, connect it, and run:

  ```powershell
  adb devices    # the device should be listed as "device", not "unauthorized"
  adb install -r src/AlienSkies.Android/bin/Release/net10.0-android/publish/com.certificationtoolanddie.alienskies-Signed.apk
  ```

  `-r` replaces an installed copy and keeps its settings. `adb` lives in `platform-tools` inside the Android SDK folder. For Android TV, use `adb connect <tv-ip-address>` first.
- **Without adb.** Copy the `.apk` to the device (USB, cloud drive, email), open it in a file manager, and allow that app to **install unknown apps** when Android asks.

To remove it: `adb uninstall com.certificationtoolanddie.alienskies`, or uninstall it from the device's app settings.

**Use it.** The app opens to its settings screen, which has **Preview**, **Screen saver settings** and **Set as live wallpaper** buttons.

- **Screen saver:** Settings → Display → Screen saver → Alien Skies. It runs while the device is charging or docked; a touch dismisses it.
- **Live wallpaper:** use the button in the app, or pick Alien Skies in the system wallpaper picker. Swiping between home-screen pages pans the view; the info text is hidden so it doesn't clutter the home screen.
- **Android TV:** works as a normal screen saver. Google TV devices hide third-party screen savers from their settings, but you can still select it with adb:

  ```bash
  adb shell settings put secure screensaver_components com.certificationtoolanddie.alienskies/com.certificationtoolanddie.alienskies.AlienSkiesDream
  ```

The release build is signed with the SDK's debug key, which is fine for sideloading. An APK built on a different machine has a different key, so Android refuses to install it over the old one; uninstall first. Publishing to Google Play needs your own signing key (`AndroidKeyStore` and the related MSBuild properties).

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
| **Planet** | Planet | Drop-down of every planet in the exoplanet catalog |
| | Imaginary planet: host star, name | Put your own made-up planet around any catalog star (overrides Planet); see below |
| | Use catalog orbit | Off = use the overrides below. For an imaginary planet, on = an Earth-like orbit |
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

### Imaginary planets

Pick any named star as **Imaginary planet: host star** (on Windows you can also type `HIP 12345` for an unnamed one) and the sky is drawn from a planet that doesn't exist. The star's luminosity, mass and radius are estimated from its brightness and colour, and the planet starts on a circular orbit at the distance where it gets the same starlight Earth gets from the Sun, with the period from Kepler's third law. Around a red dwarf like Proxima that's about 0.04 AU and 8 days, close enough to be tidally locked; around Tau Ceti it's 0.69 AU and 228 days. Turn off **Use catalog orbit** to set your own orbit, and use the Spin and Observer settings as for any planet. Set the host star back to **(none)** to return to the catalog planet. The estimates assume a main-sequence star, so a giant's mass comes out too high.

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

For Android, copy the CSVs into `src/AlienSkies.Android/Assets/data/` before building, and they're bundled into the APK. The app looks in its private storage first, then in those bundled assets, and finally falls back to the embedded sample. The full Hipparcos catalog adds a few megabytes to the APK.

| File | Columns | Source |
|---|---|---|
| `stars.csv` | `hip, name, ra_deg, dec_deg, plx_mas, vmag, bv` | Hipparcos (I/239 photometry, I/311 parallaxes); optionally Gaia DR3 |
| `exoplanets.csv` | `pl_name, hostname, ra, dec, sy_dist, sy_vmag, st_rad, st_mass, pl_orbsmax, pl_orbper, pl_orbincl, pl_orbeccen` | NASA Exoplanet Archive, Planetary Systems Composite Parameters |
| `constellations.csv` | `constellation, from_id, to_id` | Stellarium's modern sky culture (all 88 IAU figures), via `tools/build_constellations.py`; ids match `stars.csv` |

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
- Procedurally drawn *local* constellations, linking each system's own brightest stars
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
- **Stellarium** (https://stellarium.org): constellation figures from its *modern* sky culture, © the Stellarium team, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). The derived `constellations.csv` is shared under the same license.
- **The HYG database** v4.0 (https://github.com/astronexus/HYG-Database), [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/): positions and magnitudes for the constellation stars added to the bundled sample `stars.csv`.

## License

Copyright © 2026 Certification Tool and Die, Ltd. All rights reserved.
