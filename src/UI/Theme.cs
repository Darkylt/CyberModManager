using System.Runtime.InteropServices;

namespace CyberModManager.UI;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(14, 14, 18);
    public static readonly Color Panel = Color.FromArgb(23, 23, 29);
    public static readonly Color PanelHi = Color.FromArgb(32, 32, 40);
    public static readonly Color Border = Color.FromArgb(48, 48, 58);
    public static readonly Color Text = Color.FromArgb(230, 230, 230);
    public static readonly Color Dim = Color.FromArgb(138, 138, 153);
    public static readonly Color Accent = Color.FromArgb(252, 238, 10); // vaguely cyberpunk yellow
    public static readonly Color Cyan = Color.FromArgb(0, 240, 255);
    public static readonly Color Red = Color.FromArgb(255, 59, 92);
    public static readonly Color Green = Color.FromArgb(61, 255, 162);

    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font Bold = new("Segoe UI Semibold", 9.75f);
    public static readonly Font Title = new("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9f);

    public static Button MakeButton(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Panel,
            ForeColor = primary ? Color.Black : Accent,
            Font = Bold,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = Accent;
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 245, 90) : PanelHi;
        b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(220, 205, 0) : Border;
        b.EnabledChanged += (_, _) =>
        {
            b.FlatAppearance.BorderColor = b.Enabled ? Accent : Border;
            b.BackColor = b.Enabled ? (primary ? Accent : Panel) : Panel;
        };
        return b;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void UseDarkTitleBar(IntPtr handle)
    {
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
        }
        catch { /* older Windows */ }
    }
}
