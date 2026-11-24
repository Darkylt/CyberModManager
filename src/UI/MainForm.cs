using System.Diagnostics;
using CyberModManager.Core;

namespace CyberModManager.UI;

internal sealed class MainForm : Form
{
    readonly ModStore _store;

    readonly TextBox _gamePathBox;
    readonly Label _gameStatus;
    readonly SplitContainer _split;
    readonly ListView _list;
    readonly Label _emptyHint;
    readonly Label _detailsTitle;
    readonly Label _detailsInfo;
    readonly Label _filesHeader;
    readonly ListBox _fileList;
    readonly Button _btnAdd, _btnInstall, _btnUninstall, _btnRemove, _btnInstallAll, _btnUninstallAll;
    readonly Button _btnBrowse, _btnDetect, _btnOpenGame;
    readonly ProgressBar _progress;
    readonly Label _statusLabel;
    readonly ContextMenuStrip _menu;
    bool _busy;

    public MainForm(ModStore store)
    {
        _store = store;

        Text = "Cyber Mod Manager";
        Size = new Size(1100, 700);
        MinimumSize = new Size(820, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 12, 16, 8),
            BackColor = Theme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // header
        var header = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
        header.Controls.Add(new Label { Text = "CYBER MOD MANAGER", Font = Theme.Title, ForeColor = Theme.Accent, AutoSize = true, Margin = new Padding(0, 0, 12, 0) });
        header.Controls.Add(new Label { Text = "drag mod archives in · install · uninstall cleanly", ForeColor = Theme.Dim, AutoSize = true, Margin = new Padding(0, 9, 0, 0) });
        root.Controls.Add(header);

        // game folder
        var gameRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 6, Margin = new Padding(0, 0, 0, 10) };
        gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameRow.Controls.Add(new Label { Text = "Game folder", AutoSize = true, ForeColor = Theme.Dim, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        _gamePathBox = new TextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 8, 0),
            TabStop = false,
        };
        gameRow.Controls.Add(_gamePathBox, 1, 0);
        _btnBrowse = Theme.MakeButton("Browse...");
        _btnDetect = Theme.MakeButton("Auto-detect");
        _btnOpenGame = Theme.MakeButton("Open");
        gameRow.Controls.Add(_btnBrowse, 2, 0);
        gameRow.Controls.Add(_btnDetect, 3, 0);
        gameRow.Controls.Add(_btnOpenGame, 4, 0);
        _gameStatus = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.Bold };
        gameRow.Controls.Add(_gameStatus, 5, 0);
        root.Controls.Add(gameRow);

        // main split: mod list and details
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Border,
            SplitterWidth = 4,
            Margin = new Padding(0),
        };
        _split.Panel1.BackColor = Theme.Panel;
        _split.Panel2.BackColor = Theme.Panel;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            LabelEdit = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            OwnerDraw = true,
            AllowDrop = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Mod", 300);
        _list.Columns.Add("Status", 110);
        _list.Columns.Add("Files", 60, HorizontalAlignment.Right);
        _list.Columns.Add("Added", 130);
        _list.DrawColumnHeader += DrawHeader;
        _list.DrawItem += (_, _) => { };
        _list.DrawSubItem += DrawRow;
        _list.Resize += (_, _) => SizeColumns();
        _list.SelectedIndexChanged += (_, _) => { UpdateDetails(); UpdateButtons(); };
        _list.AfterLabelEdit += OnRenamed;
        _list.DoubleClick += (_, _) => { if (_list.SelectedItems.Count == 1) _list.SelectedItems[0].BeginEdit(); };

        _emptyHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Dim,
            BackColor = Theme.Panel,
            Font = new Font("Segoe UI", 12f),
            Text = "Drag & drop mod archives here\n(.zip, .7z, .rar)\n\nor click  Add mods...",
            AllowDrop = true,
        };
        _split.Panel1.Controls.Add(_list);
        _split.Panel1.Controls.Add(_emptyHint);
        _emptyHint.BringToFront();

        foreach (Control c in new Control[] { _list, _emptyHint, this })
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
        }

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 10, 12, 10), BackColor = Theme.Panel };
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _detailsTitle = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Theme.Accent, Margin = new Padding(0, 0, 0, 6), MaximumSize = new Size(2000, 0) };
        _detailsInfo = new Label { AutoSize = true, ForeColor = Theme.Dim, Margin = new Padding(0, 0, 0, 10), MaximumSize = new Size(2000, 0) };
        _filesHeader = new Label { AutoSize = true, Font = Theme.Bold, ForeColor = Theme.Cyan, Margin = new Padding(0, 0, 0, 4) };
        _fileList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = Theme.Mono,
            HorizontalScrollbar = true,
            IntegralHeight = false,
        };
        details.Controls.Add(_detailsTitle);
        details.Controls.Add(_detailsInfo);
        details.Controls.Add(_filesHeader);
        details.Controls.Add(_fileList);
        details.Resize += (_, _) =>
        {
            var w = Math.Max(100, details.ClientSize.Width - 24);
            _detailsTitle.MaximumSize = new Size(w, 0);
            _detailsInfo.MaximumSize = new Size(w, 0);
        };
        _split.Panel2.Controls.Add(details);
        root.Controls.Add(_split);

        // action buttons
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 10, 0, 6) };
        _btnAdd = Theme.MakeButton("Add mods...");
        _btnInstall = Theme.MakeButton("Install", primary: true);
        _btnUninstall = Theme.MakeButton("Uninstall");
        _btnRemove = Theme.MakeButton("Remove from list");
        _btnInstallAll = Theme.MakeButton("Install all");
        _btnUninstallAll = Theme.MakeButton("Uninstall all");
        var spacer = new Label { Width = 24, AutoSize = false, Height = 1 };
        actions.Controls.AddRange([_btnAdd, _btnInstall, _btnUninstall, _btnRemove, spacer, _btnInstallAll, _btnUninstallAll]);
        root.Controls.Add(actions);

        // status bar
        var status = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _statusLabel = new Label { AutoSize = true, ForeColor = Theme.Dim, Anchor = AnchorStyles.Left, AutoEllipsis = true };
        _progress = new ProgressBar { Width = 180, Height = 10, Style = ProgressBarStyle.Marquee, Visible = false, Anchor = AnchorStyles.Right };
        status.Controls.Add(_statusLabel, 0, 0);
        status.Controls.Add(_progress, 1, 0);
        root.Controls.Add(status);

        // context menu
        _menu = new ContextMenuStrip { BackColor = Theme.Panel, ForeColor = Theme.Text, ShowImageMargin = false, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
        _menu.Items.Add("Install", null, (_, _) => InstallSelected());
        _menu.Items.Add("Uninstall", null, (_, _) => UninstallSelected());
        _menu.Items.Add("Rename", null, (_, _) => { if (_list.SelectedItems.Count == 1) _list.SelectedItems[0].BeginEdit(); });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Remove from list", null, (_, _) => RemoveSelected());
        _menu.Opening += (_, e) =>
        {
            var sel = SelectedMods();
            if (sel.Count == 0 || _busy) { e.Cancel = true; return; }
            _menu.Items[0].Enabled = sel.Any(m => !m.Installed);
            _menu.Items[1].Enabled = sel.Any(m => m.Installed);
            _menu.Items[2].Enabled = sel.Count == 1;
        };
        _list.ContextMenuStrip = _menu;

        // events
        _btnBrowse.Click += (_, _) => BrowseGameFolder();
        _btnDetect.Click += (_, _) => DetectGameFolder(interactive: true);
        _btnOpenGame.Click += (_, _) => OpenFolder(_store.State.GamePath);
        _btnAdd.Click += (_, _) => AddViaDialog();
        _btnInstall.Click += (_, _) => InstallSelected();
        _btnUninstall.Click += (_, _) => UninstallSelected();
        _btnRemove.Click += (_, _) => RemoveSelected();
        _btnInstallAll.Click += (_, _) => InstallMods(_store.State.Mods.Where(m => !m.Installed).ToList());
        _btnUninstallAll.Click += (_, _) => UninstallMods(_store.State.Mods.Where(m => m.Installed).ToList(), confirm: true);
        KeyDown += OnKeyDown;

        if (string.IsNullOrEmpty(_store.State.GamePath)) DetectGameFolder(interactive: false);
        RefreshAll();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _split.Panel1MinSize = 300;
        _split.Panel2MinSize = 220;
        _split.SplitterDistance = (int)(_split.Width * 0.6);
        SizeColumns();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _gamePathBox.SelectionLength = 0;
        ActiveControl = _list.Visible ? _list : _btnAdd;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            MessageBox.Show(this, "Please wait until the current operation has finished.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    // game folder

    void BrowseGameFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select your Cyberpunk 2077 folder (the one that contains bin, archive, r6 ...)",
            UseDescriptionForTitle = true,
            InitialDirectory = _store.State.GamePath ?? "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var path = dlg.SelectedPath;
        // be forgiving if they picked bin\x64 or similar
        var probe = path;
        for (int i = 0; i < 3 && probe != null && !GameLocator.IsValidGameDir(probe); i++)
            probe = Path.GetDirectoryName(probe);
        if (probe != null && GameLocator.IsValidGameDir(probe)) path = probe;

        if (!GameLocator.IsValidGameDir(path))
        {
            var r = MessageBox.Show(this,
                $"Couldn't find {GameLocator.ExeRelative} in that folder.\n\nUse it anyway?",
                "Not a Cyberpunk 2077 folder?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }
        SetGamePath(path);
    }

    void DetectGameFolder(bool interactive)
    {
        var found = GameLocator.Find();
        if (found != null)
        {
            SetGamePath(found);
            if (interactive) SetStatus($"Found Cyberpunk 2077 at {found}");
        }
        else if (interactive)
        {
            MessageBox.Show(this, "Couldn't find Cyberpunk 2077 automatically. Use Browse... to pick the folder.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    void SetGamePath(string path)
    {
        if (_store.State.Mods.Any(m => m.Installed && !string.Equals(m.InstalledTo, path, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this,
                "Some mods are installed in the previous game folder. They stay tracked there, and uninstalling them will still remove them from that folder.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        _store.State.GamePath = path;
        _store.Save();
        RefreshGameRow();
        UpdateButtons();
    }

    void RefreshGameRow()
    {
        var p = _store.State.GamePath;
        _gamePathBox.Text = p ?? "(not set)";
        if (GameLocator.IsValidGameDir(p))
        {
            _gameStatus.Text = "✔ Cyberpunk 2077";
            _gameStatus.ForeColor = Theme.Green;
        }
        else
        {
            _gameStatus.Text = string.IsNullOrEmpty(p) ? "✖ not set" : "✖ game not found";
            _gameStatus.ForeColor = Theme.Red;
        }
    }

    // adding mods

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = !_busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_busy) return;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddFiles(paths);
    }

    void AddViaDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Add mod archives",
            Filter = "Mod archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddFiles(dlg.FileNames);
    }

    async void AddFiles(string[] paths)
    {
        // folders: add any archives inside them
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
                files.AddRange(Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories).Where(ModStore.IsSupported));
            else files.Add(p);
        }

        var unsupported = files.Where(f => !ModStore.IsSupported(f)).ToList();
        files = files.Where(ModStore.IsSupported).ToList();
        if (unsupported.Count > 0)
        {
            MessageBox.Show(this, "These aren't .zip / .7z / .rar archives and were skipped:\n\n" +
                                  string.Join("\n", unsupported.Select(Path.GetFileName)), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // decide what to do with duplicates up front (on the ui thread)
        var jobs = new List<(string Path, ModEntry? Replace)>();
        foreach (var f in files)
        {
            var existing = _store.FindByFileName(Path.GetFileName(f));
            if (existing == null) { jobs.Add((f, null)); continue; }
            var r = MessageBox.Show(this,
                $"'{existing.Name}' ({existing.OriginalFileName}) is already in the list.\n\nReplace it with this file?" +
                (existing.Installed ? "\n\nIt's currently installed, so it will be uninstalled and reinstalled from the new file." : ""),
                "Mod already added", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) jobs.Add((f, existing));
        }
        if (jobs.Count == 0) return;

        await RunBusy("Adding mods...", progress =>
        {
            var errors = new List<string>();
            foreach (var (path, replace) in jobs)
            {
                try
                {
                    progress.Report($"Copying {Path.GetFileName(path)}...");
                    if (replace == null) _store.AddArchive(path);
                    else
                    {
                        bool wasInstalled = replace.Installed;
                        if (wasInstalled) _store.Uninstall(replace, progress);
                        _store.ReplaceArchive(replace, path);
                        if (wasInstalled && !_store.NeedsPassword(replace)) _store.Install(replace, progress);
                    }
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
            }
            return errors;
        }, $"Added {jobs.Count} mod(s).");
    }

    // install / uninstall / remove

    List<ModEntry> SelectedMods() =>
        _list.SelectedItems.Cast<ListViewItem>().Select(i => (ModEntry)i.Tag!).ToList();

    void InstallSelected() => InstallMods(SelectedMods().Where(m => !m.Installed).ToList());
    void UninstallSelected() => UninstallMods(SelectedMods().Where(m => m.Installed).ToList(), confirm: false);

    async void InstallMods(List<ModEntry> mods)
    {
        if (_busy || mods.Count == 0) return;
        if (!GameLocator.IsValidGameDir(_store.State.GamePath))
        {
            MessageBox.Show(this, "Set your Cyberpunk 2077 game folder first (Browse... or Auto-detect).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var game = Path.GetFullPath(_store.State.GamePath!);

        // passwords + conflict check up front so the background work never needs to ask anything
        var toInstall = new List<ModEntry>();
        var conflictLines = new List<string>();
        var batchOwners = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mods)
        {
            if (_store.NeedsPassword(m))
            {
                var pw = PasswordPrompt.Ask(this, m.Name);
                if (pw == null) continue;
                m.Password = pw;
                _store.Save();
            }

            ArchiveLayout.Plan plan;
            try { plan = _store.Preview(m); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Can't install " + m.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (ex.Message.Contains("password")) { m.Password = null; _store.Save(); }
                continue;
            }

            var targets = plan.Map.Values.Select(r => Path.GetFullPath(Path.Combine(game, r))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var conflicts = _store.FindConflicts(targets);
            foreach (var t in targets)
                if (batchOwners.TryGetValue(t, out var earlier)) conflicts[t] = earlier;
            foreach (var group in conflicts.GroupBy(c => c.Value.Name))
                conflictLines.Add($"• {m.Name} overwrites {group.Count()} file(s) from {group.Key}");
            foreach (var t in targets) batchOwners[t] = m;
            toInstall.Add(m);
        }
        if (toInstall.Count == 0) return;

        if (conflictLines.Count > 0)
        {
            var r = MessageBox.Show(this,
                "Some mods write the same files:\n\n" + string.Join("\n", conflictLines.Take(15)) +
                (conflictLines.Count > 15 ? $"\n...and {conflictLines.Count - 15} more" : "") +
                "\n\nThe mod installed last wins. Overwritten files are backed up and put back when you uninstall.\n\nContinue?",
                "File conflicts", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        await RunBusy("Installing...", progress =>
        {
            var errors = new List<string>();
            foreach (var m in toInstall)
            {
                try { _store.Install(m, progress); }
                catch (Exception ex)
                {
                    errors.Add($"{m.Name}: {ex.Message}");
                    if (ex.Message.Contains("password")) { m.Password = null; _store.Save(); }
                }
            }
            return errors;
        }, $"Installed {toInstall.Count} mod(s).");
    }

    async void UninstallMods(List<ModEntry> mods, bool confirm)
    {
        if (_busy || mods.Count == 0) return;
        if (confirm && MessageBox.Show(this, $"Uninstall {mods.Count} mod(s) from the game folder?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        await RunBusy("Uninstalling...", progress =>
        {
            var errors = new List<string>();
            // newest first, so overwritten files unwind in the natural order
            foreach (var m in mods.OrderByDescending(m => m.InstalledAt))
            {
                try { _store.Uninstall(m, progress); }
                catch (Exception ex) { errors.Add($"{m.Name}: {ex.Message}"); }
            }
            return errors;
        }, $"Uninstalled {mods.Count} mod(s).");
    }

    async void RemoveSelected()
    {
        var mods = SelectedMods();
        if (_busy || mods.Count == 0) return;
        int installed = mods.Count(m => m.Installed);
        var msg = mods.Count == 1 ? $"Remove '{mods[0].Name}' from the list?" : $"Remove {mods.Count} mods from the list?";
        if (installed > 0) msg += installed == mods.Count && mods.Count == 1
            ? "\n\nIt's installed, so it will be uninstalled from the game first."
            : $"\n\n{installed} of them are installed and will be uninstalled from the game first.";
        msg += "\n\nThe stored archive copy is deleted too.";
        if (MessageBox.Show(this, msg, "Remove mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        await RunBusy("Removing...", progress =>
        {
            var errors = new List<string>();
            foreach (var m in mods)
            {
                try { _store.Remove(m, progress); }
                catch (Exception ex) { errors.Add($"{m.Name}: {ex.Message}"); }
            }
            return errors;
        }, $"Removed {mods.Count} mod(s).");
    }

    void OnRenamed(object? sender, LabelEditEventArgs e)
    {
        if (e.Label == null) return;
        if (string.IsNullOrWhiteSpace(e.Label)) { e.CancelEdit = true; return; }
        var mod = (ModEntry)_list.Items[e.Item].Tag!;
        _store.Rename(mod, e.Label);
        BeginInvoke(UpdateDetails);
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_busy) return;
        if (e.KeyCode == Keys.Delete && _list.Focused) { RemoveSelected(); e.Handled = true; }
        else if (e.KeyCode == Keys.F2 && _list.SelectedItems.Count == 1) { _list.SelectedItems[0].BeginEdit(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.A && _list.Focused)
        {
            foreach (ListViewItem i in _list.Items) i.Selected = true;
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.O) { AddViaDialog(); e.Handled = true; }
    }

    // busy handling

    async Task RunBusy(string status, Func<IProgress<string>, List<string>> work, string doneMessage)
    {
        _busy = true;
        UpdateButtons();
        _progress.Visible = true;
        SetStatus(status);
        var progress = new Progress<string>(SetStatus);
        List<string> errors;
        try
        {
            errors = await Task.Run(() => work(progress));
        }
        catch (Exception ex)
        {
            errors = [ex.Message];
        }
        finally
        {
            _busy = false;
            _progress.Visible = false;
        }

        RefreshAll();
        if (errors.Count > 0)
        {
            SetStatus("Finished with errors.");
            MessageBox.Show(this, string.Join("\n\n", errors), "Some things went wrong", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else SetStatus(doneMessage + "  " + Summary());
    }

    void SetStatus(string text) => _statusLabel.Text = text;

    string Summary()
    {
        var mods = _store.State.Mods;
        return $"{mods.Count} mod(s), {mods.Count(m => m.Installed)} installed.";
    }

    // rendering

    void RefreshAll()
    {
        RefreshGameRow();

        var selectedIds = SelectedMods().Select(m => m.Id).ToHashSet();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var m in _store.State.Mods)
        {
            var item = new ListViewItem(m.Name) { Tag = m, UseItemStyleForSubItems = false, ForeColor = Theme.Text, BackColor = Theme.Panel };
            item.SubItems.Add(m.Installed ? "● Installed" : "○ Not installed", m.Installed ? Theme.Green : Theme.Dim, Theme.Panel, Theme.Body);
            item.SubItems.Add(m.Installed ? m.Files.Count.ToString() : "", Theme.Dim, Theme.Panel, Theme.Body);
            item.SubItems.Add(m.AddedAt.ToString("yyyy-MM-dd HH:mm"), Theme.Dim, Theme.Panel, Theme.Body);
            item.Selected = selectedIds.Contains(m.Id);
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        _emptyHint.Visible = _store.State.Mods.Count == 0;
        _list.Visible = !_emptyHint.Visible;
        if (!_busy) SetStatus(Summary());
        UpdateDetails();
        UpdateButtons();
    }

    void UpdateButtons()
    {
        var sel = SelectedMods();
        var mods = _store.State.Mods;
        bool gameOk = GameLocator.IsValidGameDir(_store.State.GamePath);
        _btnAdd.Enabled = !_busy;
        _btnInstall.Enabled = !_busy && gameOk && sel.Any(m => !m.Installed);
        _btnUninstall.Enabled = !_busy && sel.Any(m => m.Installed);
        _btnRemove.Enabled = !_busy && sel.Count > 0;
        _btnInstallAll.Enabled = !_busy && gameOk && mods.Any(m => !m.Installed);
        _btnUninstallAll.Enabled = !_busy && mods.Any(m => m.Installed);
        _btnBrowse.Enabled = _btnDetect.Enabled = !_busy;
        _btnOpenGame.Enabled = Directory.Exists(_store.State.GamePath ?? "");
    }

    void UpdateDetails()
    {
        var sel = SelectedMods();
        _fileList.BeginUpdate();
        _fileList.Items.Clear();

        if (sel.Count != 1)
        {
            _detailsTitle.Text = sel.Count == 0 ? "No mod selected" : $"{sel.Count} mods selected";
            _detailsInfo.Text = sel.Count == 0
                ? "Select a mod to see its files.\nRight-click for more options. F2 renames, Delete removes."
                : $"{sel.Count(m => m.Installed)} installed, {sel.Count(m => !m.Installed)} not installed.";
            _filesHeader.Text = "";
            _fileList.EndUpdate();
            return;
        }

        var m = sel[0];
        _detailsTitle.Text = m.Name;
        var info = $"{m.OriginalFileName}  ·  {FormatSize(m.ArchiveSize)}\n";
        info += m.Installed
            ? $"Installed {m.InstalledAt:yyyy-MM-dd HH:mm} into\n{m.InstalledTo}"
            : "Not installed.";
        if (m.Password != null) info += "\nPassword saved.";

        if (m.Installed)
        {
            _filesHeader.Text = $"Installed files ({m.Files.Count})";
            foreach (var f in m.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) _fileList.Items.Add(f);
        }
        else
        {
            try
            {
                if (_store.NeedsPassword(m))
                {
                    _filesHeader.Text = "Contents hidden (password protected)";
                }
                else
                {
                    var plan = _store.Preview(m);
                    _filesHeader.Text = $"Will install ({plan.Map.Count} files)";
                    foreach (var f in plan.Map.Values.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) _fileList.Items.Add(f);
                    if (plan.Notes.Count > 0) info += "\n\n" + string.Join("\n", plan.Notes);
                    if (plan.Skipped.Count > 0) info += $"\n{plan.Skipped.Count} readme/junk file(s) won't be copied.";
                }
            }
            catch (Exception ex)
            {
                _filesHeader.Text = "Couldn't read archive";
                info += "\n\n" + ex.Message;
            }
        }
        _detailsInfo.Text = info;
        _fileList.EndUpdate();
    }

    void SizeColumns()
    {
        if (_list.Columns.Count < 4) return;
        int others = _list.Columns[1].Width + _list.Columns[2].Width + _list.Columns[3].Width;
        _list.Columns[0].Width = Math.Max(150, _list.ClientSize.Width - others - 2);
    }

    void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var bg = new SolidBrush(Theme.PanelHi);
        e.Graphics.FillRectangle(bg, e.Bounds);
        using var line = new Pen(Theme.Border);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    (e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
        var r = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, Theme.Bold, r, Theme.Dim, flags);
    }

    void DrawRow(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null) return;
        bool selected = e.Item.Selected;
        using (var bg = new SolidBrush(selected ? Color.FromArgb(58, 54, 12) : Theme.Panel))
            e.Graphics.FillRectangle(bg, e.Bounds);
        if (selected && e.ColumnIndex == 0)
            using (var strip = new SolidBrush(Theme.Accent))
                e.Graphics.FillRectangle(strip, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);

        var color = e.ColumnIndex == 0 ? (selected ? Theme.Accent : Theme.Text) : e.SubItem.ForeColor;
        var align = _list.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
        var r = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Body, r, color,
            align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };

    static void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.PanelHi;
        public override Color MenuItemBorder => Theme.Accent;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
