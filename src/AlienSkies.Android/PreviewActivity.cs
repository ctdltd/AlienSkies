using AlienSkies.Rendering;
using Android.App;
using Android.OS;
using Android.Views;

namespace AlienSkies.Droid;

/// <summary>Full-screen preview of the current settings, opened from the settings screen. Tap to close.</summary>
[Activity(
    Name = "com.certificationtoolanddie.alienskies.PreviewActivity",
    Label = "Alien Skies",
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar.Fullscreen",
    Exported = false)]
public sealed class PreviewActivity : Activity
{
    private SkyView? _view;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);

        _view = new SkyView(this, SaverSettings.Load());
        _view.Click += (_, _) => Finish();
        SetContentView(_view);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _view?.Start();
    }

    protected override void OnPause()
    {
        _view?.Stop();
        base.OnPause();
    }
}
