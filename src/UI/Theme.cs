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

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    // undocumented but stable since Windows 10 1903: lets native controls (scrollbars, menus) use dark visuals
    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#133")]
    static extern bool AllowDarkModeForWindow(IntPtr hwnd, bool allow);

    static bool _darkAllowed;

    public static void EnableDarkMode()
    {
        try { SetPreferredAppMode(2 /* ForceDark */); _darkAllowed = true; }
        catch { /* older Windows: controls just keep the light scrollbars */ }
    }

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

    // gives a control's native scrollbars (and other themed parts) the dark Explorer look
    public static void UseDarkScrollbars(Control c, string theme = "DarkMode_Explorer")
    {
        void Apply()
        {
            try
            {
                if (_darkAllowed) AllowDarkModeForWindow(c.Handle, true);
                SetWindowTheme(c.Handle, theme, null);
            }
            catch { /* older Windows */ }
        }
        if (c.IsHandleCreated) Apply();
        c.HandleCreated += (_, _) => Apply();
    }

    // turns off visual styles on a progress bar so it uses our colors instead of the bright native one
    public static void StyleProgressBar(ProgressBar bar)
    {
        bar.BackColor = Bg;
        bar.ForeColor = Accent;
        void Apply() { try { SetWindowTheme(bar.Handle, "", ""); } catch { } }
        if (bar.IsHandleCreated) Apply();
        bar.HandleCreated += (_, _) => Apply();
    }

    // owner drawn controls flicker a lot without this, and it isn't public on ListView
    public static void EnableDoubleBuffer(Control c) =>
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(c, true);

    public static TextBox MakeTextBox() => new()
    {
        BackColor = Panel,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
    };
}

// ListView that paints the empty space below the last row itself. With the dark theme applied
// (needed for dark scrollbars) Windows draws light column separator lines there
internal sealed class DarkListView : ListView
{
    const int WM_PAINT = 0x000F;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_PAINT || View != View.Details) return;

        int top = 0;
        if (Items.Count > 0)
        {
            var last = GetItemRect(Items.Count - 1);
            top = last.Bottom;
        }
        else if (TopItem == null)
        {
            // no rows: start under the header
            top = Columns.Count > 0 ? Font.Height + 10 : 0;
        }
        if (top >= ClientSize.Height) return;
        using var g = CreateGraphics();
        using var b = new SolidBrush(BackColor);
        g.FillRectangle(b, 0, top, ClientSize.Width, ClientSize.Height - top);
    }
}

// Dark context menus. The stock professional renderer still uses light blue for open submenus,
// pressed items and the check margin, so every one of those colors is set here
internal sealed class DarkMenuRenderer() : ToolStripProfessionalRenderer(new Colors())
{
    static readonly Color DisabledText = Color.FromArgb(82, 82, 96);

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? DisabledText : e.Item.Selected ? Theme.Accent : e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Selected == true ? Theme.Accent : Theme.Dim;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        using var bg = new SolidBrush(e.Item.Selected && e.Item.Enabled ? Theme.PanelHi : Theme.Panel);
        e.Graphics.FillRectangle(bg, r);
        if (e.Item.Selected && e.Item.Enabled)
            using (var strip = new SolidBrush(Theme.Accent))
                e.Graphics.FillRectangle(strip, r.Left, r.Top + 2, 2, r.Height - 4);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        TextRenderer.DrawText(e.Graphics, "✓", Theme.Bold, r, Theme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    sealed class Colors : ProfessionalColorTable
    {
        public Colors() { UseSystemColors = false; }
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.PanelHi;
        public override Color MenuItemSelectedGradientBegin => Theme.PanelHi;
        public override Color MenuItemSelectedGradientEnd => Theme.PanelHi;
        public override Color MenuItemPressedGradientBegin => Theme.PanelHi;
        public override Color MenuItemPressedGradientMiddle => Theme.PanelHi;
        public override Color MenuItemPressedGradientEnd => Theme.PanelHi;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color CheckBackground => Theme.Panel;
        public override Color CheckSelectedBackground => Theme.PanelHi;
        public override Color CheckPressedBackground => Theme.PanelHi;
        public override Color ButtonSelectedBorder => Color.Transparent;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
