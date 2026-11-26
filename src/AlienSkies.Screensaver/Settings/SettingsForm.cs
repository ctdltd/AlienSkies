using System.Diagnostics;
using AlienSkies.Rendering;

namespace AlienSkies.Screensaver.Settings;

/// <summary>Configuration dialog shown for /c (Settings button in Windows' Screen Saver Settings).</summary>
public sealed class SettingsForm : Form
{
    private readonly SaverSettings _working;

    public SettingsForm(SaverSettings settings)
    {
        _working = settings.Clone();

        Text = "Alien Skies — Settings";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(560, 720);
        MinimumSize = new Size(420, 400);

        var grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = _working,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
        };

        var test = new Button { Text = "Test in window", AutoSize = true };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var data = new LinkLabel { Text = "Open data folder", AutoSize = true, Margin = new Padding(3, 9, 12, 3) };

        test.Click += (_, _) =>
        {
            _working.Save();
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "/w") { UseShellExecute = false });
        };
        data.LinkClicked += (_, _) =>
        {
            var dir = Path.Combine(Path.GetDirectoryName(SaverSettings.FilePath)!, "data");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(8),
        };
        buttons.Controls.AddRange([cancel, ok, test, data]);

        Controls.Add(grid);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult == DialogResult.OK) _working.Save();
        base.OnFormClosed(e);
    }
}
