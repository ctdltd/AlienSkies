using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using AlienSkies.Core.Astronomy;
using AlienSkies.Rendering;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AlienSkies.Droid;

/// <summary>
/// The app's launcher screen and the settings page for both the screen saver and the live wallpaper. Builds its form from the same
/// <see cref="SaverSettings"/> attributes the Windows PropertyGrid uses ([Category], [DisplayName],
/// [Description]), so both platforms stay in step automatically. Properties with a list of
/// standard values (the planet) appear as drop-downs. Imaginary planets get their own add/remove section, each
/// generated the same way from <see cref="ImaginaryPlanet"/>. Changes are saved when you leave.
/// </summary>
[Activity(
    Name = "com.certificationtoolanddie.alienskies.SettingsActivity",
    Label = "Alien Skies",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.DeviceDefault")]
public sealed class SettingsActivity : Activity
{
    /// <summary>Windows-only settings that make no sense on a phone.</summary>
    private static readonly HashSet<string> Hidden = [nameof(SaverSettings.PanoramaAcrossMonitors)];

    private readonly List<Action> _readers = [];
    private SaverSettings _settings = new();
    private float _dp;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AppSetup.EnsureInitialized(this);
        _settings = SaverSettings.Load();
        _dp = Resources!.DisplayMetrics!.Density;

        var form = new LinearLayout(this) { Orientation = Orientation.Vertical };
        form.SetPadding(Px(16), Px(12), Px(16), Px(32));

        AddIntro(form);
        AddButtons(form);

        var properties = typeof(SaverSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !Hidden.Contains(p.Name))
            .GroupBy(p => p.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Other")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in properties)
        {
            form.AddView(Heading(StripOrderPrefix(group.Key)));
            foreach (var property in group)
            {
                if (property.PropertyType == typeof(List<ImaginaryPlanet>)) AddImaginaryPlanets(form, property);
                else form.AddView(Editor(property, _settings));
            }
        }

        AddAbout(form);

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        SetContentView(scroll);
    }

    protected override void OnPause()
    {
        Persist();
        base.OnPause();
    }

    private void Persist()
    {
        var selected = _settings.SelectedImaginaryPlanet();
        var oldName = selected?.DisplayName;
        foreach (var read in _readers) read();
        _settings.FollowRename(selected, oldName);
        _settings.Save();
    }

    // ---- header ------------------------------------------------------------

    private void AddIntro(LinearLayout form)
    {
        var intro = new TextView(this)
        {
            Text = "The night sky as seen from a planet orbiting another star.\n\n" +
                   "To use as a screen saver: Settings → Display → Screen saver → Alien Skies. " +
                   "It shows while the device is charging or docked.\n\n" +
                   "It also works as a live wallpaper: swiping between home-screen pages pans across the horizon.",
        };
        intro.SetPadding(0, 0, 0, Px(8));
        form.AddView(intro);
    }

    private void AddButtons(LinearLayout form)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };

        var preview = new Button(this) { Text = "Preview" };
        preview.Click += (_, _) =>
        {
            Persist();
            StartActivity(new Intent(this, typeof(PreviewActivity)));
        };
        row.AddView(preview);

        var screenSaver = new Button(this) { Text = "Screen saver settings" };
        screenSaver.Click += (_, _) => TryStart(new Intent("android.settings.DREAM_SETTINGS"),
            "This device has no screen saver settings page.");
        row.AddView(screenSaver);

        form.AddView(row);

        var wallpaper = new Button(this) { Text = "Set as live wallpaper" };
        wallpaper.Click += (_, _) =>
        {
            Persist();
            var intent = new Intent(WallpaperManager.ActionChangeLiveWallpaper);
            intent.PutExtra(WallpaperManager.ExtraLiveWallpaperComponent,
                new ComponentName(this, Java.Lang.Class.FromType(typeof(AlienSkiesWallpaper))));
            TryStart(intent, "This device does not support live wallpapers.");
        };
        form.AddView(wallpaper);
    }

    /// <summary>Copyright, GPL notice and credits at the foot of the page, with tappable links.</summary>
    private void AddAbout(LinearLayout form)
    {
        form.AddView(Heading("About"));
        // The link mask applies only to text set after it, so set it first.
        var about = new TextView(this) { AutoLinkMask = Android.Text.Util.MatchOptions.WebUrls };
        about.Text = AboutInfo.Text();
        about.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
        form.AddView(about);
    }

    private void TryStart(Intent intent, string failureMessage)
    {
        try { StartActivity(intent); }
        catch (ActivityNotFoundException) { Toast.MakeText(this, failureMessage, ToastLength.Long)!.Show(); }
    }

    // ---- imaginary planets ---------------------------------------------------

    private void AddImaginaryPlanets(LinearLayout form, PropertyInfo property)
    {
        var help = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
        form.AddView(Heading(property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name, 16));
        if (help is not null) form.AddView(Hint(help));

        var fields = typeof(ImaginaryPlanet)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
            .ToArray();

        var planets = _settings.ImaginaryPlanets;
        for (int i = 0; i < planets.Count; i++)
        {
            var planet = planets[i];
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetPadding(Px(12), Px(4), 0, Px(8));
            card.AddView(Heading($"{i + 1}. {planet.DisplayName}", 15));
            foreach (var field in fields) card.AddView(Editor(field, planet));

            var remove = new Button(this) { Text = "Remove this planet" };
            remove.Click += (_, _) => ChangeImaginaryPlanets(() => planets.Remove(planet));
            card.AddView(remove);
            form.AddView(card);
        }

        var add = new Button(this) { Text = "Add an imaginary planet" };
        add.Click += (_, _) => ChangeImaginaryPlanets(() => planets.Add(new ImaginaryPlanet()));
        form.AddView(add);
    }

    /// <summary>Saves what's on screen, applies the change, and rebuilds the form (so the Planet list picks it up too).</summary>
    private void ChangeImaginaryPlanets(Action change)
    {
        Persist();
        change();
        _settings.Save();
        Recreate();
    }

    // ---- editors -------------------------------------------------------------

    private View Editor(PropertyInfo property, object target)
    {
        var block = new LinearLayout(this) { Orientation = Orientation.Vertical };
        block.SetPadding(0, Px(10), 0, 0);

        string label = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
        string? help = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

        if (property.PropertyType == typeof(bool))
        {
            var toggle = new Switch(this) { Text = label, Checked = (bool)property.GetValue(target)! };
            block.AddView(toggle);
            _readers.Add(() => property.SetValue(target, toggle.Checked));
        }
        else
        {
            block.AddView(new TextView(this) { Text = label });

            if (property.PropertyType.IsEnum)
            {
                var names = Enum.GetNames(property.PropertyType);
                var spinner = new Spinner(this)
                {
                    Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, names),
                };
                spinner.SetSelection(Array.IndexOf(names, property.GetValue(target)!.ToString()));
                block.AddView(spinner);
                _readers.Add(() => property.SetValue(target,
                    Enum.Parse(property.PropertyType, names[Math.Max(0, spinner.SelectedItemPosition)])));
            }
            else if (property.PropertyType == typeof(string) &&
                     TypeDescriptor.GetProperties(target)[property.Name]?.Converter is { } converter &&
                     converter.GetStandardValuesSupported())
            {
                // Pick-list (e.g. Planet): choices come from the property's TypeConverter.
                var values = converter.GetStandardValues(new InstanceContext(target))!.Cast<object>().Select(v => v.ToString()!).ToArray();
                var spinner = new Spinner(this)
                {
                    Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, values),
                };
                // A saved name no longer in the catalog shows (and saves) the first entry rather than a stale value.
                int current = Array.FindIndex(values, v =>
                    string.Equals(v, (string?)property.GetValue(target), StringComparison.OrdinalIgnoreCase));
                spinner.SetSelection(Math.Max(0, current));
                block.AddView(spinner);
                _readers.Add(() =>
                {
                    if (values.Length > 0) property.SetValue(target, values[Math.Max(0, spinner.SelectedItemPosition)]);
                });
            }
            else if (property.PropertyType == typeof(string))
            {
                var text = new EditText(this) { Text = (string?)property.GetValue(target) };
                block.AddView(text);
                _readers.Add(() => property.SetValue(target, text.Text));
            }
            else if (property.PropertyType == typeof(double))
            {
                var number = new EditText(this)
                {
                    InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned,
                    Text = ((double)property.GetValue(target)!).ToString(CultureInfo.InvariantCulture),
                };
                block.AddView(number);
                _readers.Add(() =>
                {
                    if (double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        property.SetValue(target, v);
                });
            }
        }

        if (help is not null)
        {
            block.AddView(Hint(help));
        }
        return block;
    }

    private TextView Hint(string text)
    {
        var hint = new TextView(this) { Text = text, Alpha = 0.7f };
        hint.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
        return hint;
    }

    private TextView Heading(string text, int sizeSp = 18)
    {
        var heading = new TextView(this) { Text = text };
        heading.SetTextSize(Android.Util.ComplexUnitType.Sp, sizeSp);
        heading.SetPadding(0, Px(24), 0, Px(4));
        return heading;
    }

    /// <summary>"1. Planet" → "Planet" (the number only orders the categories).</summary>
    private static string StripOrderPrefix(string category)
    {
        int dot = category.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 && category[..dot].All(char.IsDigit) ? category[(dot + 2)..] : category;
    }

    private int Px(int dp) => (int)(dp * _dp + 0.5f);
}
