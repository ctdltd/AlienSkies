using System.ComponentModel;

namespace AlienSkies.Core.Catalog;

/// <summary>
/// Offers named catalog stars as a drop-down (PropertyGrid on Windows, Spinner on Android). Not exclusive, so on
/// Windows you can also type "HIP 12345" for an unnamed star.
/// </summary>
public sealed class StarNameConverter : StringConverter
{
    private static readonly Lazy<string[]> Names = new(() =>
        CatalogLoader.LoadStars().Select(s => s.Name).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray());

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Names.Value);
}
