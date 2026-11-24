namespace CyberModManager.UI;

internal sealed class PasswordPrompt : Form
{
    readonly TextBox _box;

    PasswordPrompt(string modName)
    {
        Text = "Password required";
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
        layout.Controls.Add(new Label { Text = $"'{modName}' is password protected.\nEnter the password from the mod page:", AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
        _box = new TextBox { Width = 320, BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(_box);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        var ok = Theme.MakeButton("OK", primary: true);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.MakeButton("Skip");
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

    public static string? Ask(IWin32Window owner, string modName)
    {
        using var p = new PasswordPrompt(modName);
        return p.ShowDialog(owner) == DialogResult.OK ? p._box.Text : null;
    }
}
