using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using AlienSkies.Rendering;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AlienSkies.Droid;

/// <summary>
/// The app's launcher screen and the screen saver's settings page. Builds its form from the same
/// <see cref="SaverSettings"/> attributes the Windows PropertyGrid uses ([Category], [DisplayName],
/// [Description]), so both platforms stay in step automatically. Changes are saved when you leave.
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
            foreach (var property in group) form.AddView(Editor(property));
        }

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
        foreach (var read in _readers) read();
        _settings.Save();
    }

    // ---- header ------------------------------------------------------------

    private void AddIntro(LinearLayout form)
    {
        var intro = new TextView(this)
        {
            Text = "The night sky as seen from a planet orbiting another star.\n\n" +
                   "To use as a screen saver: Settings → Display → Screen saver → Alien Skies. " +
                   "It shows while the device is charging or docked.",
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
    }

    private void TryStart(Intent intent, string failureMessage)
    {
        try { StartActivity(intent); }
        catch (ActivityNotFoundException) { Toast.MakeText(this, failureMessage, ToastLength.Long)!.Show(); }
    }

    // ---- editors -------------------------------------------------------------

    private View Editor(PropertyInfo property)
    {
        var block = new LinearLayout(this) { Orientation = Orientation.Vertical };
        block.SetPadding(0, Px(10), 0, 0);

        string label = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
        string? help = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

        if (property.PropertyType == typeof(bool))
        {
            var toggle = new Switch(this) { Text = label, Checked = (bool)property.GetValue(_settings)! };
            block.AddView(toggle);
            _readers.Add(() => property.SetValue(_settings, toggle.Checked));
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
                spinner.SetSelection(Array.IndexOf(names, property.GetValue(_settings)!.ToString()));
                block.AddView(spinner);
                _readers.Add(() => property.SetValue(_settings,
                    Enum.Parse(property.PropertyType, names[Math.Max(0, spinner.SelectedItemPosition)])));
            }
            else if (property.PropertyType == typeof(string))
            {
                // Free text with suggestions: the planet list comes from the property's TypeConverter.
                var text = new AutoCompleteTextView(this) { Text = (string?)property.GetValue(_settings), Threshold = 1 };
                var converter = TypeDescriptor.GetProperties(_settings)[property.Name]?.Converter;
                if (converter?.GetStandardValuesSupported() == true)
                {
                    var values = converter.GetStandardValues()!.Cast<object>().Select(v => v.ToString()!).ToArray();
                    text.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleDropDownItem1Line, values);
                }
                block.AddView(text);
                _readers.Add(() => property.SetValue(_settings, text.Text));
            }
            else if (property.PropertyType == typeof(double))
            {
                var number = new EditText(this)
                {
                    InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned,
                    Text = ((double)property.GetValue(_settings)!).ToString(CultureInfo.InvariantCulture),
                };
                block.AddView(number);
                _readers.Add(() =>
                {
                    if (double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        property.SetValue(_settings, v);
                });
            }
        }

        if (help is not null)
        {
            var hint = new TextView(this) { Text = help, Alpha = 0.7f };
            hint.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
            block.AddView(hint);
        }
        return block;
    }

    private TextView Heading(string text)
    {
        var heading = new TextView(this) { Text = text };
        heading.SetTextSize(Android.Util.ComplexUnitType.Sp, 18);
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
