namespace CyberModManager.UI;

// small dark-themed "enter some text" dialog (passwords, category names)
internal sealed class TextPrompt : Form
{
    readonly TextBox _box;

    TextPrompt(string title, string message, string initial, string okText, string cancelText, IEnumerable<string>? suggestions)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Padding = new Padding(16);

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.Add(new Label { Text = message, AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
        _box = Theme.MakeTextBox();
        _box.Width = 320;
        _box.Margin = new Padding(0, 0, 0, 12);
        _box.Text = initial;
        if (suggestions != null)
        {
            var ac = new AutoCompleteStringCollection();
            ac.AddRange(suggestions.ToArray());
            _box.AutoCompleteCustomSource = ac;
            _box.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        }
        layout.Controls.Add(_box);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        var ok = Theme.MakeButton(okText, primary: true);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.MakeButton(cancelText);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _box.SelectAll();
        _box.Focus();
    }

    public static string? Ask(IWin32Window owner, string title, string message, string initial = "",
        string okText = "OK", string cancelText = "Cancel", IEnumerable<string>? suggestions = null)
    {
        using var p = new TextPrompt(title, message, initial, okText, cancelText, suggestions);
        return p.ShowDialog(owner) == DialogResult.OK ? p._box.Text : null;
    }

    public static string? AskPassword(IWin32Window owner, string modName) =>
        Ask(owner, "Password required", $"'{modName}' is password protected.\nEnter the password from the mod page:", cancelText: "Skip");
}
