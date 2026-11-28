using System.Drawing.Drawing2D;
using CyberModManager.Core;

namespace CyberModManager.UI;

internal sealed class UpdateDropZone : Control
{
    static readonly Font IconFont = new("Segoe UI Symbol", 17f);
    bool _hover, _dragging;

    public event Action? BrowseRequested;
    public event Action<string>? FileDropped;

    public UpdateDropZone()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Theme.Panel;
        Height = 58;
        Cursor = Cursors.Hand;
        AllowDrop = true;
        TabStop = true;
        AccessibleName = "Update this mod";
        AccessibleRole = AccessibleRole.PushButton;
    }

    // lets screen readers and UI automation "press" the box like a button
    protected override AccessibleObject CreateAccessibilityInstance() => new ZoneAccessible(this);

    sealed class ZoneAccessible(UpdateDropZone owner) : ControlAccessibleObject(owner)
    {
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() { if (owner.Enabled) owner.BrowseRequested?.Invoke(); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled) BrowseRequested?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Enabled && e.KeyCode is Keys.Enter or Keys.Space) { BrowseRequested?.Invoke(); e.Handled = true; }
    }

    static string? SingleArchive(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files && File.Exists(files[0]) && ModStore.IsSupported(files[0])
            ? files[0] : null;

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        bool ok = Enabled && SingleArchive(e) != null;
        e.Effect = ok ? DragDropEffects.Copy : DragDropEffects.None;
        _dragging = ok;
        Invalidate();
    }

    protected override void OnDragLeave(EventArgs e) { base.OnDragLeave(e); _dragging = false; Invalidate(); }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        _dragging = false;
        Invalidate();
        var file = SingleArchive(e);
        // let the drag source (Explorer) finish before a dialog pops up
        if (file != null) BeginInvoke(() => FileDropped?.Invoke(file));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool active = Enabled && (_hover || _dragging || Focused);
        g.Clear(Parent?.BackColor ?? Theme.Panel);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var bg = new SolidBrush(_dragging ? Color.FromArgb(40, 38, 12) : active ? Theme.PanelHi : Theme.Bg))
            g.FillRectangle(bg, r);
        using (var pen = new Pen(!Enabled ? Theme.Border : active ? Theme.Accent : Color.FromArgb(90, 88, 40)) { DashStyle = _dragging ? DashStyle.Solid : DashStyle.Dash })
            g.DrawRectangle(pen, r);

        var accent = Enabled ? Theme.Accent : Theme.Dim;
        var iconRect = new Rectangle(10, 0, 34, Height);
        TextRenderer.DrawText(g, "⟳", IconFont, iconRect, accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var textRect = new Rectangle(50, 9, Width - 60, Height - 18);
        var title = _dragging ? "Release to update" : "Update this mod";
        var sub = _dragging ? "You'll be asked to confirm first." : "Drop the new version here, or click to choose the file.";
        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, title, Theme.Bold, new Rectangle(textRect.X, textRect.Y, textRect.Width, 20), accent, flags);
        TextRenderer.DrawText(g, sub, Theme.Body, new Rectangle(textRect.X, textRect.Y + 20, textRect.Width, 20), Theme.Dim, flags);
    }
}
