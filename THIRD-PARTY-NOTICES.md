# Third-party notices

Alien Skies is copyright © 2026 Certification Tool and Die Ltd. Co. and licensed under the GNU General Public License, version 3 or (at your option) any later version. See [`LICENSE`](LICENSE).

The app is built on the libraries and data below. Each keeps its own license, and every license listed here allows use in, and distribution with, a GPL-3.0 program. The data files are distributed alongside the program under their own terms; the GPL covers the program's code.

## Libraries shipped in the apps

| Component | Used by | License |
|---|---|---|
| [SkiaSharp](https://github.com/mono/SkiaSharp) and SkiaSharp.Views 4.157.0 | every app | MIT, © .NET Foundation and Contributors |
| [Skia](https://skia.org) (native library inside SkiaSharp) | every app | BSD-3-Clause, © Google LLC |
| Libraries built into SkiaSharp's native library: HarfBuzz, Expat, FreeType, giflib, libpng, zlib, libjpeg-turbo, libwebp, jsoncpp, etc1, piex, sfntly, SDL headers, Dear ImGui, ANGLE (Windows only) | every app | MIT, MIT, FreeType License (FTL) or GPL-2.0-or-later, MIT, libpng, zlib, IJG/BSD-3-Clause, BSD-3-Clause, MIT, Apache-2.0, Apache-2.0, Apache-2.0, zlib, MIT, BSD-3-Clause |
| Adobe DNG SDK (built into SkiaSharp's native library for reading raw camera images; Alien Skies never uses it) | every app | Adobe DNG SDK License Agreement. **See the note below.** |

The full text of every license above ships in each SkiaSharp native package as `THIRD-PARTY-NOTICES.txt` (SkiaSharp.NativeAssets.Win32, .macOS and .Android 4.157.0).

| [.NET](https://github.com/dotnet/runtime) runtime and base class libraries, Windows Forms, .NET for Android, NativeAOT | Windows, Android, macOS | MIT, © .NET Foundation and Contributors |

FreeType asks for this credit: *Portions of this software are copyright © The FreeType Project (www.freetype.org). All rights reserved.*

The macOS saver also links Apple's ScreenSaver and AppKit frameworks, and the Android app runs on the Android framework. These are system libraries of the platform, which the GPL allows a program to use.

### Note on the Adobe DNG SDK

The DNG SDK license is permissive, but it asks anyone who distributes it "in a commercial product" to indemnify Adobe. That is an extra condition the GPL does not allow other licenses to add, so it is arguably not GPL-3.0 compatible. It does not limit Certification Tool and Die, which owns the Alien Skies copyright and can ship the program however it likes. It matters only to someone else who redistributes a modified Alien Skies. FreeType is used under its GPL-2.0-or-later option, which is GPL-3.0 compatible.

## Data bundled with the apps

| Data | Where | Source and license | Required credit |
|---|---|---|---|
| Star positions, magnitudes and colours | `stars.csv` (full catalog in `src/AlienSkies.Android/Assets/data/`, embedded in every app) | ESA Hipparcos mission: *The Hipparcos and Tycho Catalogues* (ESA 1997, SP-1200, VizieR I/239) with parallaxes from the new reduction by F. van Leeuwen (2007, A&A 474, 653, VizieR I/311), retrieved through VizieR. No separate license; VizieR asks that the original authors and publications be cited and that the service be acknowledged. | "This research has made use of the VizieR catalogue access tool, CDS, Strasbourg, France (DOI: 10.26093/cds/vizier)." Cite ESA 1997 and van Leeuwen 2007. |
| Gaia parallaxes for about 3,200 Hipparcos stars whose own parallax is zero or negative, and the BP−RP to B−V colour calibration | applied by `tools/build_catalog.py` when the Gaia archive is reachable | ESA Gaia DR3. Gaia data are licensed under [CC BY-NC 3.0 IGO](https://www.cosmos.esa.int/web/gaia-users/license). **See the note below.** | "This work has made use of data from the European Space Agency (ESA) mission Gaia (https://www.cosmos.esa.int/gaia), processed by the Gaia Data Processing and Analysis Consortium (DPAC, https://www.cosmos.esa.int/web/gaia/dpac/consortium). Funding for the DPAC has been provided by national institutions, in particular the institutions participating in the Gaia Multilateral Agreement." |
| Exoplanets and their host stars | `exoplanets.csv` | [NASA Exoplanet Archive](https://exoplanetarchive.ipac.caltech.edu), Planetary Systems Composite Parameters table. Public data, free to use with acknowledgement. | "This research has made use of the NASA Exoplanet Archive, which is operated by the California Institute of Technology, under contract with the National Aeronautics and Space Administration under the Exoplanet Exploration Program." |
| Constellation figures (all 88) | `constellations.csv` | [Stellarium](https://stellarium.org) *modern* sky culture, © the Stellarium team, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). Converted to one row per line segment by `tools/build_constellations.py`; the converted file is shared under CC BY-SA 4.0. | "Constellation figures from Stellarium's modern sky culture, © the Stellarium team, CC BY-SA 4.0." |
| Constellation-figure stars in the small built-in sample | `src/AlienSkies.Core/Data/stars.csv` | [HYG database](https://github.com/astronexus/HYG-Database) v4.0, © David Nash (astronexus), [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). The sample file is shared under CC BY-SA 4.0. | "Star data from the HYG database v4.0, CC BY-SA 4.0." |
| Star proper names | `name` column of `stars.csv` | IAU Working Group on Star Names catalogue and traditional names; names are facts, not licensed works. | none required |

CC BY-SA 4.0 is one-way compatible with GPL-3.0 (Creative Commons, 2015), so these files may also be combined into GPL-3.0 works.

### Note on Gaia data

Gaia's license is non-commercial (CC BY-NC), while the GPL allows anyone to sell copies of the program. The shipped `stars.csv` contains no Gaia-only stars; about 3,200 of its roughly 117,000 Hipparcos stars (those whose Hipparcos parallax is zero or negative) use Gaia DR3 parallaxes instead, and there is a colour calibration fitted to Gaia photometry. Commercial use of Gaia data needs ESA's prior authorisation (data.licences@esa.int; see the [ESA archive terms](https://www.cosmos.esa.int/web/esdc/terms-and-conditions)). If you want the bundled data to be free of any Gaia terms, rebuild the catalog with the Gaia archive unreachable (the builder then skips the cross-match) and do not use `--gaia-limit`. Anyone who adds Gaia DR3 stars with `--gaia-limit` should keep that catalog to non-commercial use.

## Tools and tests (not shipped in the apps)

| Component | License |
|---|---|
| xUnit, xunit.runner.visualstudio | Apache-2.0 |
| Microsoft.NET.Test.Sdk, coverlet.collector | MIT |
| SkiaSharp.NativeAssets.Linux.NoDependencies (preview tool, CI, macOS smoke test) | MIT |
| pyvo, NumPy (catalog builder) | BSD-3-Clause |

All are GPL-3.0 compatible.
