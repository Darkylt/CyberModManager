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
    readonly UpdateDropZone _updateZone;
    readonly Button _btnAdd, _btnInstall, _btnUninstall, _btnRemove, _btnInstallAll, _btnUninstallAll;
    readonly Button _btnBrowse, _btnDetect, _btnOpenGame;
    readonly ProgressBar _progress;
    readonly Label _statusLabel;
    readonly ContextMenuStrip _menu;
    readonly ToolStripMenuItem _categoryMenu;
    readonly TextBox _search;
    readonly ComboBox _categoryFilter;
    readonly Label _shownLabel;
    readonly System.Windows.Forms.Timer _statusTimer;
    bool _busy;

    // progress from background work: the worker only stores the latest text and a timer puts it on screen
    volatile string? _pendingStatus;

    int _sortColumn = ColCategory;
    bool _sortDesc;

    const int ColName = 0, ColCategory = 1, ColStatus = 2, ColFiles = 3, ColAdded = 4;
    const string AllCategories = "All categories", Uncategorized = "(no category)";
    static readonly string[] SuggestedCategories =
        ["Frameworks", "Gameplay", "Visuals", "Audio", "UI", "Vehicles", "Weapons", "Clothing & Appearance", "Utilities"];

    public MainForm(ModStore store)
    {
        _store = store;

        Text = "Cyber Mod Manager " + Application.ProductVersion.Split('+')[0];
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
            RowCount = 6,
            Padding = new Padding(16, 12, 16, 8),
            BackColor = Theme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
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

        // search + category filter
        var filterRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 0, 0, 8) };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _search = Theme.MakeTextBox();
        _search.Dock = DockStyle.Fill;
        _search.Margin = new Padding(0, 3, 12, 0);
        _search.PlaceholderText = "Search mods...   (Ctrl+F)";
        _search.TextChanged += (_, _) => RefreshList();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { _search.Clear(); e.Handled = e.SuppressKeyPress = true; }
            else if ((e.KeyCode is Keys.Down or Keys.Enter) && _list!.Items.Count > 0)
            {
                _list.Focus();
                if (_list.SelectedItems.Count == 0) { _list.Items[0].Selected = true; _list.Items[0].Focused = true; }
                e.Handled = e.SuppressKeyPress = true;
            }
        };
        filterRow.Controls.Add(_search, 0, 0);
        filterRow.Controls.Add(new Label { Text = "Category", AutoSize = true, ForeColor = Theme.Dim, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 1, 0);
        _categoryFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 12, 0),
            ItemHeight = 20,
            MaxDropDownItems = 20,
        };
        _categoryFilter.DrawItem += DrawComboItem;
        _categoryFilter.SelectedIndexChanged += (_, _) => RefreshList();
        Theme.UseDarkScrollbars(_categoryFilter, "DarkMode_CFD");
        filterRow.Controls.Add(_categoryFilter, 2, 0);
        _shownLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Theme.Dim, TextAlign = ContentAlignment.MiddleRight, Margin = Padding.Empty };
        filterRow.Controls.Add(_shownLabel, 3, 0);
        root.Controls.Add(filterRow);

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

        _list = new DarkListView
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
            HeaderStyle = ColumnHeaderStyle.Clickable,
        };
        Theme.EnableDoubleBuffer(_list);
        Theme.UseDarkScrollbars(_list);
        _list.Columns.Add("Mod", 300);
        _list.Columns.Add("Category", 140);
        _list.Columns.Add("Status", 110);
        _list.Columns.Add("Files", 60, HorizontalAlignment.Right);
        _list.Columns.Add("Added", 130);
        _list.DrawColumnHeader += DrawHeader;
        _list.DrawItem += (_, _) => { };
        _list.DrawSubItem += DrawRow;
        _list.Resize += (_, _) => SizeColumns();
        _list.SelectedIndexChanged += (_, _) => { UpdateDetails(); UpdateButtons(); };
        _list.AfterLabelEdit += OnRenamed;
        _list.ColumnClick += (_, e) =>
        {
            if (_sortColumn == e.Column) _sortDesc = !_sortDesc;
            else { _sortColumn = e.Column; _sortDesc = e.Column == ColAdded; }
            _list.Invalidate();
            RefreshList();
        };
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

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12, 10, 12, 10), BackColor = Theme.Panel };
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _detailsTitle = new Label { AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Theme.Accent, Margin = new Padding(0, 0, 0, 6), MaximumSize = new Size(2000, 0) };
        _detailsInfo = new Label { AutoSize = true, UseMnemonic = false, ForeColor = Theme.Dim, Margin = new Padding(0, 0, 0, 10), MaximumSize = new Size(2000, 0) };
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
        Theme.UseDarkScrollbars(_fileList);
        details.Controls.Add(_detailsTitle);
        details.Controls.Add(_detailsInfo);
        _updateZone = new UpdateDropZone { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), Visible = false };
        _updateZone.BrowseRequested += () => { if (SelectedMods() is [var m]) UpdateViaDialog(m); };
        _updateZone.FileDropped += path => { if (SelectedMods() is [var m]) UpdateModFrom(m, path); };
        details.Controls.Add(_updateZone);
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
        var status = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        _statusLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Theme.Dim, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false, Margin = Padding.Empty };
        _progress = new ProgressBar { Width = 180, Height = 10, Style = ProgressBarStyle.Marquee, Visible = false, Anchor = AnchorStyles.Right, Margin = new Padding(10, 0, 0, 0) };
        Theme.StyleProgressBar(_progress);
        _statusTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _statusTimer.Tick += (_, _) =>
        {
            var t = _pendingStatus;
            if (t != null && t != _statusLabel.Text) _statusLabel.Text = t;
        };
        status.Controls.Add(_statusLabel, 0, 0);
        status.Controls.Add(_progress, 1, 0);
        root.Controls.Add(status);

        // context menu
        _menu = new ContextMenuStrip { BackColor = Theme.Panel, ForeColor = Theme.Text, ShowImageMargin = false, Renderer = new DarkMenuRenderer() };
        _menu.Items.Add("Install", null, (_, _) => InstallSelected());
        _menu.Items.Add("Uninstall", null, (_, _) => UninstallSelected());
        _menu.Items.Add("Rename", null, (_, _) => { if (_list.SelectedItems.Count == 1) _list.SelectedItems[0].BeginEdit(); });
        _menu.Items.Add("Update...", null, (_, _) => { if (SelectedMods() is [var m]) UpdateViaDialog(m); });
        _categoryMenu = new ToolStripMenuItem("Category");
        var catDrop = (ToolStripDropDownMenu)_categoryMenu.DropDown;
        catDrop.BackColor = Theme.Panel;
        catDrop.ForeColor = Theme.Text;
        catDrop.ShowImageMargin = false;
        catDrop.ShowCheckMargin = true;
        catDrop.Renderer = _menu.Renderer;
        catDrop.Font = _menu.Font;
        _menu.Items.Add(_categoryMenu);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Remove from list", null, (_, _) => RemoveSelected());
        _menu.Opening += (_, e) =>
        {
            var sel = SelectedMods();
            if (sel.Count == 0 || _busy) { e.Cancel = true; return; }
            _menu.Items[0].Enabled = sel.Any(m => !m.Installed);
            _menu.Items[1].Enabled = sel.Any(m => m.Installed);
            _menu.Items[2].Enabled = sel.Count == 1;
            _menu.Items[3].Enabled = sel.Count == 1;
            BuildCategoryMenu(sel);
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
        _btnInstallAll.Click += (_, _) => _ = InstallMods(ShownMods().Where(m => !m.Installed).ToList());
        _btnUninstallAll.Click += (_, _) => UninstallMods(ShownMods().Where(m => m.Installed).ToList(), confirm: true);
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
        var adds = new List<string>();
        var replaces = new List<(string Path, ModEntry Mod)>();
        foreach (var f in files)
        {
            var existing = _store.FindByFileName(Path.GetFileName(f));
            if (existing == null) { adds.Add(f); continue; }
            var r = MessageBox.Show(this,
                $"'{existing.Name}' ({existing.OriginalFileName}) is already in the list.\n\nReplace it with this file?" +
                (existing.Installed ? "\n\nIt's currently installed, so it will be uninstalled and reinstalled from the new file." : ""),
                "Mod already added", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) replaces.Add((f, existing));
        }
        if (adds.Count + replaces.Count == 0) return;
        await ReplaceMods(replaces, adds);
    }

    // updating a mod

    void UpdateViaDialog(ModEntry m)
    {
        if (_busy) return;
        using var dlg = new OpenFileDialog
        {
            Title = $"Choose the new version of {m.Name}",
            Filter = "Mod archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) UpdateModFrom(m, dlg.FileName);
    }

    async void UpdateModFrom(ModEntry m, string path)
    {
        if (_busy) return;
        if (!ModStore.IsSupported(path))
        {
            MessageBox.Show(this, $"'{Path.GetFileName(path)}' isn't a .zip / .7z / .rar archive.", "Update " + m.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(_store.ArchivePath(m)), StringComparison.OrdinalIgnoreCase)) return;

        var other = _store.FindByFileName(Path.GetFileName(path));
        var msg = $"Update '{m.Name}'?\n\n" +
                  $"Current:  {m.OriginalFileName}  ({FormatSize(m.ArchiveSize)}, added {m.AddedAt:yyyy-MM-dd})\n" +
                  $"New:        {Path.GetFileName(path)}  ({FormatSize(new FileInfo(path).Length)})\n\n" +
                  (m.Installed
                      ? "It's installed, so its files will be removed and the new version installed in their place."
                      : "It isn't installed, so only the stored archive is replaced.") +
                  "\nThe name and category are kept.";
        if (other != null && other != m)
            msg += $"\n\nNote: '{other.Name}' in your list was added from a file with the same name.";
        if (MessageBox.Show(this, msg, "Update mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        await ReplaceMods([(path, m)], []);
    }

    // replaces stored archives (uninstalling first where needed), adds new archives, then reinstalls
    // the replaced mods that were installed. Reinstalling goes through InstallMods so passwords
    // and file conflicts are asked about exactly like a normal install
    async Task ReplaceMods(List<(string Path, ModEntry Mod)> replaces, List<string> adds)
    {
        var reinstall = new List<ModEntry>();
        await RunBusy(replaces.Count > 0 && adds.Count == 0 ? "Updating..." : "Adding mods...", progress =>
        {
            var errors = new List<string>();
            foreach (var path in adds)
            {
                try
                {
                    progress.Report($"Copying {Path.GetFileName(path)}...");
                    _store.AddArchive(path);
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
            }
            foreach (var (path, m) in replaces)
            {
                try
                {
                    // make sure the new file is usable before taking the old version out of the game
                    progress.Report($"Checking {Path.GetFileName(path)}...");
                    ModStore.CheckArchive(path);
                    bool wasInstalled = m.Installed;
                    if (wasInstalled) _store.Uninstall(m, progress);
                    progress.Report($"Copying {Path.GetFileName(path)}...");
                    _store.ReplaceArchive(m, path);
                    if (wasInstalled) reinstall.Add(m);
                }
                catch (Exception ex) { errors.Add($"{m.Name}: {ex.Message}"); }
            }
            return errors;
        }, replaces.Count > 0 && adds.Count == 0
            ? $"Updated {Describe(replaces.Select(r => r.Mod).ToList())}."
            : $"Added {adds.Count + replaces.Count} mod(s).");

        if (reinstall.Count > 0) await InstallMods(reinstall, reinstalling: true);
    }

    // install / uninstall / remove

    List<ModEntry> SelectedMods() =>
        _list.SelectedItems.Cast<ListViewItem>().Select(i => (ModEntry)i.Tag!).ToList();

    void InstallSelected() => _ = InstallMods(SelectedMods().Where(m => !m.Installed).ToList());
    void UninstallSelected() => UninstallMods(SelectedMods().Where(m => m.Installed).ToList(), confirm: false);

    async Task InstallMods(List<ModEntry> mods, bool reinstalling = false)
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
                var pw = TextPrompt.AskPassword(this, m.Name);
                if (pw == null)
                {
                    if (reinstalling) SetStatus($"'{m.Name}' was updated but not reinstalled (no password). Install it when you have the password.");
                    continue;
                }
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
        }, reinstalling ? $"Updated and reinstalled {Describe(toInstall)}." : $"Installed {toInstall.Count} mod(s).");
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
        if (e.Control && e.KeyCode == Keys.F) { _search.Focus(); _search.SelectAll(); e.Handled = e.SuppressKeyPress = true; return; }
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
        _pendingStatus = null;
        var progress = new LatestStatus(this);
        _statusTimer.Start();
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
            _statusTimer.Stop();
            _pendingStatus = null;
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

    sealed class LatestStatus(MainForm form) : IProgress<string>
    {
        public void Report(string value) => form._pendingStatus = value;
    }

    string Summary()
    {
        var mods = _store.State.Mods;
        return $"{mods.Count} mod(s), {mods.Count(m => m.Installed)} installed.";
    }

    // rendering

    void RefreshAll()
    {
        RefreshGameRow();
        RefreshCategoryFilter();
        RefreshList();
    }

    // rebuilds the category dropdown, keeping the current choice when it still exists
    void RefreshCategoryFilter()
    {
        var current = _categoryFilter.SelectedItem as string ?? AllCategories;
        var items = new List<string> { AllCategories };
        var cats = _store.Categories();
        if (cats.Count > 0 && _store.State.Mods.Any(m => m.Category == null)) items.Add(Uncategorized);
        items.AddRange(cats);
        if (_categoryFilter.SelectedIndex >= 0 && items.SequenceEqual(_categoryFilter.Items.Cast<string>())) return;

        _suppressFilterEvents = true;
        _categoryFilter.BeginUpdate();
        _categoryFilter.Items.Clear();
        _categoryFilter.Items.AddRange(items.ToArray());
        _categoryFilter.SelectedIndex = Math.Max(0, items.FindIndex(i => i.Equals(current, StringComparison.OrdinalIgnoreCase)));
        _categoryFilter.EndUpdate();
        _suppressFilterEvents = false;
    }

    bool _suppressFilterEvents;

    bool FilterActive => _search.Text.Trim().Length > 0 || _categoryFilter.SelectedIndex > 0;

    bool Matches(ModEntry m)
    {
        var cat = _categoryFilter.SelectedIndex > 0 ? _categoryFilter.SelectedItem as string : null;
        if (cat == Uncategorized) { if (m.Category != null) return false; }
        else if (cat != null && !string.Equals(m.Category, cat, StringComparison.OrdinalIgnoreCase)) return false;

        // every word has to appear in the name, the file name or the category
        var words = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return true;
        var hay = $"{m.Name}\n{m.OriginalFileName}\n{m.Category}";
        return words.All(w => hay.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    List<ModEntry> ShownMods() => _store.State.Mods.Where(Matches).ToList();

    IEnumerable<ModEntry> Sorted(IEnumerable<ModEntry> mods)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        IOrderedEnumerable<ModEntry> o = _sortColumn switch
        {
            // mods without a category always go after the named categories
            ColCategory => _sortDesc
                ? mods.OrderBy(m => m.Category == null).ThenByDescending(m => m.Category, cmp)
                : mods.OrderBy(m => m.Category == null).ThenBy(m => m.Category, cmp),
            ColStatus => _sortDesc ? mods.OrderBy(m => m.Installed) : mods.OrderByDescending(m => m.Installed),
            ColFiles => _sortDesc ? mods.OrderByDescending(m => m.Installed ? m.Files.Count : -1) : mods.OrderBy(m => m.Installed ? m.Files.Count : -1),
            ColAdded => _sortDesc ? mods.OrderByDescending(m => m.AddedAt) : mods.OrderBy(m => m.AddedAt),
            _ => _sortDesc ? mods.OrderByDescending(m => m.Name, cmp) : mods.OrderBy(m => m.Name, cmp),
        };
        return _sortColumn == ColName ? o : o.ThenBy(m => m.Name, cmp);
    }

    void RefreshList()
    {
        if (_suppressFilterEvents) return;
        var selectedIds = SelectedMods().Select(m => m.Id).ToHashSet();
        var shown = Sorted(ShownMods()).ToList();
        int top = _list.Items.Count > 0 ? _list.TopItem?.Index ?? 0 : 0;

        var items = new ListViewItem[shown.Count];
        for (int i = 0; i < shown.Count; i++)
        {
            var m = shown[i];
            var item = new ListViewItem(m.Name) { Tag = m, UseItemStyleForSubItems = false, ForeColor = Theme.Text, BackColor = Theme.Panel };
            item.SubItems.Add(m.Category ?? "", Theme.Cyan, Theme.Panel, Theme.Body);
            item.SubItems.Add(m.Installed ? "● Installed" : "○ Not installed", m.Installed ? Theme.Green : Theme.Dim, Theme.Panel, Theme.Body);
            item.SubItems.Add(m.Installed ? m.Files.Count.ToString() : "", Theme.Dim, Theme.Panel, Theme.Body);
            item.SubItems.Add(m.AddedAt.ToString("yyyy-MM-dd HH:mm"), Theme.Dim, Theme.Panel, Theme.Body);
            item.Selected = selectedIds.Contains(m.Id);
            items[i] = item;
        }
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(items);
        if (top > 0 && top < _list.Items.Count) _list.TopItem = _list.Items[top];
        _list.EndUpdate();

        bool anyMods = _store.State.Mods.Count > 0;
        _emptyHint.Text = anyMods
            ? "No mods match.\n\nPress Esc in the search box to clear it,\nor pick \"All categories\"."
            : "Drag & drop mod archives here\n(.zip, .7z, .rar)\n\nor click  Add mods...";
        _emptyHint.Visible = shown.Count == 0;
        _list.Visible = !_emptyHint.Visible;
        _shownLabel.Text = FilterActive ? $"{shown.Count} of {_store.State.Mods.Count} shown" : "";
        _btnInstallAll.Text = FilterActive ? "Install shown" : "Install all";
        _btnUninstallAll.Text = FilterActive ? "Uninstall shown" : "Uninstall all";
        if (!_busy) SetStatus(Summary());
        UpdateDetails();
        UpdateButtons();
    }

    // categories

    void BuildCategoryMenu(List<ModEntry> sel)
    {
        var items = _categoryMenu.DropDownItems;
        items.Clear();
        _categoryMenu.Enabled = !_busy;
        var existing = _store.Categories();
        string? shared = sel.Select(m => m.Category?.ToLowerInvariant()).Distinct().Count() == 1 ? sel[0].Category : null;

        // the user's own categories first, then a few suggestions to get started
        var suggestions = SuggestedCategories.Where(c => !existing.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        var names = existing.Concat(suggestions).ToList();
        void AddChoice(string n) =>
            items.Add(new ToolStripMenuItem(n.Replace("&", "&&"), null, (_, _) => SetCategory(sel, n)) // && = literal &, not a shortcut key
            {
                Checked = shared != null && shared.Equals(n, StringComparison.OrdinalIgnoreCase),
                ForeColor = Theme.Text,
            });
        foreach (var n in existing) AddChoice(n);
        if (suggestions.Count > 0)
        {
            if (existing.Count > 0) items.Add(new ToolStripSeparator());
            items.Add(new ToolStripLabel("Suggestions") { ForeColor = Theme.Dim, Font = new Font(Theme.Body.FontFamily, 8.25f) });
            foreach (var n in suggestions) AddChoice(n);
        }
        items.Add(new ToolStripSeparator());
        items.Add(new ToolStripMenuItem("New category...", null, (_, _) =>
        {
            var name = TextPrompt.Ask(this, "New category", $"Category for {Describe(sel)}:", suggestions: names);
            if (!string.IsNullOrWhiteSpace(name)) SetCategory(sel, name);
        }) { ForeColor = Theme.Text });
        if (shared != null)
        {
            var old = shared;
            items.Add(new ToolStripMenuItem($"Rename \"{old.Replace("&", "&&")}\"...", null, (_, _) =>
            {
                var name = TextPrompt.Ask(this, "Rename category", $"New name for the category \"{old}\"\n(changes it for every mod in it):", old);
                if (string.IsNullOrWhiteSpace(name)) return;
                bool filtered = string.Equals(_categoryFilter.SelectedItem as string, old, StringComparison.OrdinalIgnoreCase);
                _store.RenameCategory(old, name);
                RefreshCategoryFilter();
                if (filtered)
                {
                    var idx = _categoryFilter.Items.Cast<string>().ToList().FindIndex(i => i.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) _categoryFilter.SelectedIndex = idx;
                }
                RefreshList();
            }) { ForeColor = Theme.Text });
        }
        items.Add(new ToolStripMenuItem("No category", null, (_, _) => SetCategory(sel, null))
        {
            Enabled = sel.Any(m => m.Category != null),
            ForeColor = Theme.Text,
        });
    }

    void SetCategory(List<ModEntry> mods, string? category)
    {
        _store.SetCategory(mods, category);
        RefreshAll();
    }

    static string Describe(List<ModEntry> mods) => mods.Count == 1 ? $"'{mods[0].Name}'" : $"{mods.Count} mods";

    void UpdateButtons()
    {
        var sel = SelectedMods();
        var mods = _store.State.Mods;
        bool gameOk = GameLocator.IsValidGameDir(_store.State.GamePath);
        _btnAdd.Enabled = !_busy;
        _updateZone.Enabled = !_busy;
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

        _updateZone.Visible = sel.Count == 1;
        _updateZone.Enabled = !_busy;
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
        var info = $"{m.OriginalFileName}  ·  {FormatSize(m.ArchiveSize)}" + (m.Category != null ? $"  ·  {m.Category}" : "") + "\n";
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
        if (_list.Columns.Count < 5) return;
        int others = 0;
        for (int i = 1; i < _list.Columns.Count; i++) others += _list.Columns[i].Width;
        var w = Math.Max(150, _list.ClientSize.Width - others - 2);
        if (_list.Columns[0].Width != w) _list.Columns[0].Width = w;
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
        bool sorted = e.ColumnIndex == _sortColumn;
        var text = e.Header?.Text + (sorted ? (_sortDesc ? "  ▼" : "  ▲") : "");
        TextRenderer.DrawText(e.Graphics, text, Theme.Bold, r, sorted ? Theme.Text : Theme.Dim, flags);
    }

    void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool hot = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var bg = new SolidBrush(hot ? Theme.PanelHi : Theme.Panel))
            e.Graphics.FillRectangle(bg, e.Bounds);
        var text = _categoryFilter.Items[e.Index] as string ?? "";
        TextRenderer.DrawText(e.Graphics, text, Theme.Body, Rectangle.Inflate(e.Bounds, -4, 0), hot ? Theme.Accent : Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
}
