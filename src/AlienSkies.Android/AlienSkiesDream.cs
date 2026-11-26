using AlienSkies.Rendering;
using Android.App;
using Android.Content;
using Android.Service.Dreams;

namespace AlienSkies.Droid;

/// <summary>
/// The Android screen saver ("Daydream"). Shown while the device is charging or docked, once the
/// user picks Alien Skies under Settings → Display → Screen saver. Touching the screen dismisses it.
/// </summary>
[Service(
    Name = "com.certificationtoolanddie.alienskies.AlienSkiesDream",
    Label = "Alien Skies",
    Exported = true,
    Permission = "android.permission.BIND_DREAM_SERVICE")]
[IntentFilter(["android.service.dreams.DreamService"], Categories = [Intent.CategoryDefault])]
[MetaData("android.service.dream", Resource = "@xml/dream_info")]
public sealed class AlienSkiesDream : DreamService
{
    private SkyView? _view;

    public override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        Interactive = false;  // any touch ends the screen saver, like a desktop one
        Fullscreen = true;
        ScreenBright = true;  // keep the stars crisp; Android dims to its own level otherwise

        _view = new SkyView(this, SaverSettings.Load());
        SetContentView(_view);
    }

    public override void OnDreamingStarted()
    {
        base.OnDreamingStarted();
        _view?.Start();
    }

    public override void OnDreamingStopped()
    {
        _view?.Stop();
        base.OnDreamingStopped();
    }

    public override void OnDetachedFromWindow()
    {
        _view?.Dispose();
        _view = null;
        base.OnDetachedFromWindow();
    }
}
