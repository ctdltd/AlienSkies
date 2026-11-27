using System.Reflection;

namespace AlienSkies.Rendering;

/// <summary>
/// The copyright, license and credits notice every host shows in its about/settings screen (GPL-3.0 section 5(d):
/// interactive programs display "appropriate legal notices"). One copy here keeps Windows, macOS and Android in step.
/// The full texts are in LICENSE and THIRD-PARTY-NOTICES.md at the root of the source repository.
/// </summary>
public static class AboutInfo
{
    public const string Copyright = "Copyright © 2026 Certification Tool and Die Ltd. Co.";

    public const string SourceUrl = "https://github.com/ctdltd/AlienSkies";

    public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    public const string NoticesUrl = SourceUrl + "/blob/main/THIRD-PARTY-NOTICES.md";

    public const string License =
        "Alien Skies is free software: you can redistribute it and/or modify it under the terms of the " +
        "GNU General Public License as published by the Free Software Foundation, either version 3 of the " +
        "License, or (at your option) any later version. It is distributed in the hope that it will be useful, " +
        "but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A " +
        "PARTICULAR PURPOSE. See the GNU General Public License for more details.";

    public const string Credits =
        "Star data: ESA Hipparcos mission (van Leeuwen 2007 reduction) via VizieR, CDS, Strasbourg; " +
        "ESA Gaia mission, processed by the Gaia DPAC. " +
        "Planets: NASA Exoplanet Archive, operated by Caltech under contract with NASA. " +
        "Constellation figures: Stellarium modern sky culture, CC BY-SA 4.0. " +
        "Constellation stars: HYG database v4.0, CC BY-SA 4.0. " +
        "Drawing: SkiaSharp (MIT) and Skia (BSD-3-Clause). Built with .NET (MIT).";

    /// <summary>The release number (YYddd.hhmm) every project is stamped with by Directory.Build.props.</summary>
    public static string Version =>
        typeof(AboutInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "";

    /// <summary>The whole notice as plain text with blank lines between paragraphs.</summary>
    public static string Text() =>
        $"Alien Skies {Version}\n" +
        $"{Copyright}\n\n" +
        $"{License}\n\n" +
        $"License: {LicenseUrl}\n" +
        $"Source code: {SourceUrl}\n\n" +
        $"{Credits}\n\n" +
        $"Full credits and third-party licenses: {NoticesUrl}";
}
