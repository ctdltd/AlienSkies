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

        // Renaming the imaginary planet that's currently chosen keeps it chosen.
        var selected = _working.SelectedImaginaryPlanet();
        var selectedName = selected?.DisplayName;
        grid.PropertyValueChanged += (_, _) =>
        {
            _working.FollowRename(selected, selectedName);
            selected = _working.SelectedImaginaryPlanet();
            selectedName = selected?.DisplayName;
            grid.Refresh();
        };

        var test = new Button { Text = "Test in window", AutoSize = true };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var data = new LinkLabel { Text = "Open data folder", AutoSize = true, Margin = new Padding(3, 9, 12, 3) };
        var about = new LinkLabel { Text = "About", AutoSize = true, Margin = new Padding(3, 9, 12, 3) };

        // The dialog runs as the application's main window (Application.Run), not through ShowDialog, so a button's
        // DialogResult alone doesn't close it: close explicitly. OnFormClosed saves when the result is OK.
        ok.Click += (_, _) => Close();
        cancel.Click += (_, _) => Close();
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

        about.LinkClicked += (_, _) => ShowAbout();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(8),
        };
        buttons.Controls.AddRange([cancel, ok, test, data, about]);

        Controls.Add(grid);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>Copyright, GPL notice and credits, with links to the license and the source code.</summary>
    private void ShowAbout()
    {
        using var dialog = new Form
        {
            Text = "About Alien Skies",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(520, 480),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
        };
        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            DetectUrls = true,
            Text = AboutInfo.Text().Replace("\n", Environment.NewLine),
        };
        text.LinkClicked += (_, e) =>
        {
            if (e.LinkText is { } url) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };
        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true, Dock = DockStyle.Bottom };
        dialog.Controls.Add(text);
        dialog.Controls.Add(close);
        dialog.AcceptButton = close;
        dialog.CancelButton = close;
        dialog.ShowDialog(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult == DialogResult.OK) _working.Save();
        base.OnFormClosed(e);
    }
}
