using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AlienSkies.Rendering;

namespace AlienSkies.Mac;

/// <summary>
/// Describes <see cref="SaverSettings"/> to the native settings sheet and applies its edits, so the sheet is
/// built from the same attributes as the Windows PropertyGrid and the Android form and picks up new settings
/// without any Objective-C changes. Only simple properties (bool, number, enum, string) are offered; the
/// imaginary-planet list is edited in settings.json for now.
/// </summary>
internal static class SettingsSchema
{
    public enum Kind { Bool, Number, Choice, Text }

    public sealed record Field(PropertyInfo Property, string Group, string Label, string? Help, Kind Kind, string[]? Choices);

    public static IEnumerable<Field> Fields(SaverSettings settings)
    {
        var context = new InstanceContext(settings);
        foreach (var p in typeof(SaverSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || p.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false) continue;
            var t = p.PropertyType;
            string[]? choices = null;
            Kind kind;
            if (t == typeof(bool)) kind = Kind.Bool;
            else if (t == typeof(double) || t == typeof(float) || t == typeof(int)) kind = Kind.Number;
            else if (t.IsEnum) { kind = Kind.Choice; choices = Enum.GetNames(t); }
            else if (t == typeof(string))
            {
                var converter = ConverterFor(p);
                if (converter?.GetStandardValuesSupported(context) == true)
                {
                    kind = Kind.Choice;
                    choices = converter.GetStandardValues(context)!.Cast<object>().Select(o => o.ToString() ?? "").ToArray();
                }
                else kind = Kind.Text;
            }
            else continue;

            string group = p.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Other";
            yield return new Field(p, StripOrderPrefix(group),
                p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name,
                p.GetCustomAttribute<DescriptionAttribute>()?.Description, kind, choices);
        }
    }

    /// <summary>[{"key","group","label","help","kind","value","choices"}, ...] in declaration order.</summary>
    public static string Describe(SaverSettings settings)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartArray();
            foreach (var f in Fields(settings))
            {
                w.WriteStartObject();
                w.WriteString("key", f.Property.Name);
                w.WriteString("group", f.Group);
                w.WriteString("label", f.Label);
                if (f.Help is not null) w.WriteString("help", f.Help);
                w.WriteString("kind", f.Kind.ToString().ToLowerInvariant());
                var value = f.Property.GetValue(settings);
                switch (f.Kind)
                {
                    case Kind.Bool: w.WriteBoolean("value", (bool)value!); break;
                    case Kind.Number: w.WriteNumber("value", Convert.ToDouble(value, CultureInfo.InvariantCulture)); break;
                    default: w.WriteString("value", value?.ToString() ?? ""); break;
                }
                if (f.Choices is not null)
                {
                    w.WriteStartArray("choices");
                    foreach (var c in f.Choices) w.WriteStringValue(c);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Applies {"Key": value} pairs. Unknown keys and values of the wrong shape are ignored.</summary>
    public static void Apply(SaverSettings settings, string json)
    {
        var selected = settings.SelectedImaginaryPlanet();
        var oldName = selected?.DisplayName;
        var fields = Fields(settings).ToDictionary(f => f.Property.Name);

        using var doc = JsonDocument.Parse(json);
        foreach (var item in doc.RootElement.EnumerateObject())
        {
            if (!fields.TryGetValue(item.Name, out var f)) continue;
            var p = f.Property;
            var v = item.Value;
            switch (f.Kind)
            {
                case Kind.Bool when v.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    p.SetValue(settings, v.GetBoolean());
                    break;
                case Kind.Number when v.TryGetDouble(out var d) && double.IsFinite(d):
                    p.SetValue(settings, Convert.ChangeType(d, p.PropertyType, CultureInfo.InvariantCulture));
                    break;
                case Kind.Choice when v.ValueKind == JsonValueKind.String:
                    var s = v.GetString()!;
                    if (p.PropertyType.IsEnum)
                    {
                        if (Enum.TryParse(p.PropertyType, s, ignoreCase: true, out var e)) p.SetValue(settings, e);
                    }
                    else if (f.Choices!.Contains(s, StringComparer.OrdinalIgnoreCase)) p.SetValue(settings, s);
                    break;
                case Kind.Text when v.ValueKind == JsonValueKind.String:
                    p.SetValue(settings, v.GetString());
                    break;
            }
        }
        settings.FollowRename(selected, oldName);
    }

    // AlienSkies.Rendering is a TrimmerRootAssembly in the NativeAOT build, so its converters survive trimming.
    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("Trimming", "IL2072")]
    private static TypeConverter? ConverterFor(PropertyInfo p)
    {
        var name = p.GetCustomAttribute<TypeConverterAttribute>()?.ConverterTypeName;
        if (string.IsNullOrEmpty(name)) return null;
        var type = Type.GetType(name) ?? typeof(SaverSettings).Assembly.GetType(name.Split(',')[0].Trim());
        return type is null ? null : Activator.CreateInstance(type) as TypeConverter;
    }

    private static string StripOrderPrefix(string category)
    {
        int dot = category.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 && category[..dot].All(char.IsDigit) ? category[(dot + 2)..] : category;
    }
}
