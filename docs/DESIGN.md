# Design notes

## Pipeline

1. **Catalog → heliocentric Cartesian (once per catalog load).** RA, Dec, and parallax become an ICRS position in parsecs. Apparent V plus parallax gives absolute magnitude.
2. **Re-centre on the host star (once per planet).** For each star, `rel = star − host`. The direction is `rel/|rel|`, and the new apparent magnitude is `M + 5 log10(|rel|) − 5`. Stars fainter than the limiting magnitude are dropped. Sol is added explicitly so it can be marked.
3. **Planet orbit and spin (every frame).** Only the observer's horizon basis changes between frames. Stellar directions stay fixed because a planetary orbit of a few AU is negligible against parsec distances. That keeps per-frame cost to one dot-product pass over the visible stars.

Every vector stays in ICRS-aligned axes, so no rotation matrices pile up.

## Conventions

* **Inclination and ascending node** use exoplanet-archive conventions, measured against the plane of *Earth's* sky. Inclination 0° is face-on to us and 90° is edge-on. That is why the default is 90° rather than 0°: almost every planet with a well-measured inclination was found by transit, so it is nearly edge-on. A consequence is that **our Sun lies close to that planet's ecliptic**, so Sol passes through their zodiac. With an inclination of 0°, Sol would instead sit at their ecliptic pole.
* **The ascending node** is almost never known. It defaults to 0° and only rotates the orbit about our line of sight.
* **Spin:** obliquity tilts the spin pole away from the orbit normal, toward a direction given by "tilt direction" (measured in the orbit plane from the node). With obliquity 0, the planet's celestial pole is its ecliptic pole.
* **Tidal locking:** the rotation period equals the orbital period, phased so that longitude 0 faces the star. Eccentric orbits therefore show real optical libration; the tests see about 0.6° for TRAPPIST-1 e.
* **Azimuth** is measured from the planet's north through east.

## Known simplifications (TODO)

* Proper motion and radial velocity are not yet propagated. That matters for nearby systems and for a "simultaneous" sky.
* Interstellar extinction is ignored, so distant stars look slightly too bright.
* Binary companions within max(0.02 pc, 0.5% of the host distance) are removed as "self". From the α Cen A sample, Toliman (α Cen B) disappears even though it should be a second sun of roughly magnitude −19.
* Host luminosity comes from the archive's `sy_vmag`, which is the *system* magnitude and slightly too bright for multiples.
* The B−V colour ramp is approximate. Gaia stars use a rough BP−RP → B−V stand-in.
* The SkiaSharp renderer draws on the CPU. It should handle tens of thousands of visible stars, but a GPU path (Skia's GL backend) is the next step if a full Gaia catalog is used.
* Host-star colour isn't in the exoplanet archive, so it is estimated from absolute magnitude along the main sequence; giant hosts come out too blue.
* Android: `WallpaperService.Engine` is a Java inner class, and the .NET for Android 10 wrapper generator emits an invalid constructor for C# subclasses of inner classes. The Engine therefore lives in a small Java shim (`src/AlienSkies.Android/Java/SkyWallpaperService.java`) that forwards callbacks to C#.

## Roadmap ideas

**Sky realism**
* Companion stars in multiple systems as second suns (α Cen, the Kepler-16 circumbinary planets)
* Sibling planets visible as "wanderers" (in TRAPPIST-1 they would appear larger than our Moon)
* Moons, user-defined
* Atmospheric extinction and airmass dimming near the horizon, plus sky colour from host temperature (red-dwarf daylight)
* Milky Way band from a Gaia density map. It looks nearly the same from anywhere within a few hundred parsecs, which is a lesson in itself.
* Proper-motion epoch slider: the sky from here, 100,000 years from now

**Teaching overlays**
* Earth's constellations, distorted (done for Orion and the Big Dipper; add all 88 from Stellarium's `constellationship.fab`)
* "Where is Earth?" marker with distance and light-travel time ("the light you see left the Sun in 1999")
* Local constellations, procedurally drawn by linking each system's own brightest stars
* Highlight the planet's ecliptic, celestial equator, and pole star

**Screen saver UX**
* Tour mode with a cinematic flight between systems (stars stream past)
* Choose systems by filter: habitable zone, within N light-years, transiting only
* "What can they see?" mode: show whether Earth would transit the Sun as seen from that planet
* Per-monitor modes: one panorama, or a different planet on each screen

**Platform**
* GPU rendering (SkiaSharp's GL backend)
* macOS `.saver` reusing the SkiaSharp renderer
* Android: a daylight-aware dim mode for bedside docks
