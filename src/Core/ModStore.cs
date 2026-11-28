using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace CyberModManager.Core;

public sealed class ModManagerException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ModStore
{
    public static readonly string[] SupportedExtensions = [".zip", ".7z", ".rar"];

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    readonly object _lock = new();

    public string DataDir { get; }
    public string ArchivesDir { get; }
    public string BackupsDir { get; }
    public string StatePath { get; }
    public AppState State { get; private set; } = new();

    public ModStore(string dataDir)
    {
        DataDir = dataDir;
        ArchivesDir = Path.Combine(dataDir, "archives");
        BackupsDir = Path.Combine(dataDir, "backups");
        StatePath = Path.Combine(dataDir, "state.json");
        Directory.CreateDirectory(ArchivesDir);
        Directory.CreateDirectory(BackupsDir);
        Load();
    }

    // persistence

    public string BackupStatePath => StatePath + ".bak";
    bool _backedUpThisSession;

    void Load()
    {
        if (!File.Exists(StatePath))
        {
            // a crash between writing the temp file and moving it leaves only the backup
            State = File.Exists(BackupStatePath) ? Read(BackupStatePath) : new AppState();
            return;
        }
        try
        {
            State = Read(StatePath);
        }
        catch (Exception ex) when (File.Exists(BackupStatePath))
        {
            try { State = Read(BackupStatePath); }
            catch { throw new ModManagerException($"state.json is damaged ({ex.Message}) and the backup couldn't be read either.", ex); }
        }
    }

    static AppState Read(string path)
    {
        var loaded = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path)) ?? new AppState();
        loaded.FileOwners = new Dictionary<string, List<string>>(loaded.FileOwners ?? new(), StringComparer.OrdinalIgnoreCase);
        loaded.Mods ??= new();
        loaded.CreatedDirs ??= new();
        foreach (var m in loaded.Mods) m.Files ??= new();
        return loaded;
    }

    public void Save()
    {
        lock (_lock)
        {
            // keep a copy of the state as it was when this version of the app started, so an update
            // never costs the mod list even if something goes wrong on the first save
            if (!_backedUpThisSession && File.Exists(StatePath))
            {
                try { File.Copy(StatePath, BackupStatePath, overwrite: true); } catch { /* not fatal */ }
                _backedUpThisSession = true;
            }
            var tmp = StatePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(State, JsonOpts));
            File.Move(tmp, StatePath, overwrite: true);
        }
    }

    // mod list

    public string ArchivePath(ModEntry m) => Path.Combine(ArchivesDir, m.ArchiveFile);

    public ModEntry? FindByFileName(string originalFileName) =>
        State.Mods.FirstOrDefault(m => m.OriginalFileName.Equals(originalFileName, StringComparison.OrdinalIgnoreCase));

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    //copies an archive into the data folder and adds it to the list (not installed yet)
    public ModEntry AddArchive(string sourcePath)
    {
        if (!IsSupported(sourcePath))
            throw new ModManagerException($"'{Path.GetFileName(sourcePath)}' isn't a supported archive (.zip, .7z, .rar).");

        var entry = new ModEntry
        {
            Name = CleanName(Path.GetFileNameWithoutExtension(sourcePath)),
            OriginalFileName = Path.GetFileName(sourcePath),
            AddedAt = DateTime.Now,
        };
        entry.ArchiveFile = entry.Id + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, ArchivePath(entry), overwrite: true);
        entry.ArchiveSize = new FileInfo(ArchivePath(entry)).Length;

        lock (_lock) State.Mods.Add(entry);
        Save();
        return entry;
    }

    // replaces the stored archive of an existing (uninstalled) mod, e.g. for an update.
    // The new file is copied next to the old one first, so a failed copy leaves the old archive intact
    public void ReplaceArchive(ModEntry m, string sourcePath)
    {
        if (m.Installed) throw new ModManagerException("Uninstall the mod before replacing its archive.");
        if (!IsSupported(sourcePath))
            throw new ModManagerException($"'{Path.GetFileName(sourcePath)}' isn't a supported archive (.zip, .7z, .rar).");
        var oldPath = ArchivePath(m);
        var newFile = m.Id + Path.GetExtension(sourcePath).ToLowerInvariant();
        var tmp = Path.Combine(ArchivesDir, newFile + ".new");
        File.Copy(sourcePath, tmp, overwrite: true);
        try
        {
            File.Move(tmp, Path.Combine(ArchivesDir, newFile), overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
        m.ArchiveFile = newFile;
        if (!oldPath.Equals(ArchivePath(m), StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath)) File.Delete(oldPath);
        m.ArchiveSize = new FileInfo(ArchivePath(m)).Length;
        m.OriginalFileName = Path.GetFileName(sourcePath);
        m.AddedAt = DateTime.Now;
        m.Password = null;
        Save();
    }

    // throws if a file isn't an archive we can read. Encrypted archives pass: the password is asked for at install
    public static void CheckArchive(string path)
    {
        if (!IsSupported(path))
            throw new ModManagerException($"'{Path.GetFileName(path)}' isn't a .zip / .7z / .rar archive.");
        try
        {
            using var a = ArchiveFactory.OpenArchive(path);
            if (!a.Entries.Any(e => !e.IsDirectory))
                throw new ModManagerException($"'{Path.GetFileName(path)}' is empty.");
        }
        catch (Exception ex) when (IsPasswordError(ex)) { /* fine, it's just locked */ }
        catch (Exception ex) when (ex is not ModManagerException)
        {
            throw new ModManagerException($"'{Path.GetFileName(path)}' couldn't be read as an archive ({ex.Message}).", ex);
        }
    }

    // removes a mod from the list (uninstalling it first if needed) and deletes its stored archive
    public void Remove(ModEntry m, IProgress<string>? progress = null)
    {
        if (m.Installed) Uninstall(m, progress);
        try { File.Delete(ArchivePath(m)); } catch { /* already gone */ }
        lock (_lock) State.Mods.Remove(m);
        Save();
    }

    public void Rename(ModEntry m, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        m.Name = name.Trim();
        Save();
    }

    // categories

    public IReadOnlyList<string> Categories() =>
        State.Mods.Select(m => m.Category).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();

    public void SetCategory(IEnumerable<ModEntry> mods, string? category)
    {
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        // reuse the existing spelling so "visuals" and "Visuals" don't become two categories
        if (category != null)
            category = Categories().FirstOrDefault(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)) ?? category;
        foreach (var m in mods) m.Category = category;
        Save();
    }

    public void RenameCategory(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        newName = newName.Trim();
        // merging into another existing category keeps that one's spelling; a case-only rename is allowed
        newName = Categories().FirstOrDefault(c => !c.Equals(oldName, StringComparison.OrdinalIgnoreCase) &&
                                                   c.Equals(newName, StringComparison.OrdinalIgnoreCase)) ?? newName;
        foreach (var m in State.Mods.Where(m => string.Equals(m.Category, oldName, StringComparison.OrdinalIgnoreCase)))
            m.Category = newName;
        Save();
    }

    // "Cyber Engine Tweaks-107-1-32-1-1714153406" (Nexus download name) -> "Cyber Engine Tweaks"
    public static string CleanName(string fileName)
    {
        var m = Regex.Match(fileName, @"^(?<n>.+?)-\d+(?:-[0-9A-Za-z]+)*-\d{10}$");
        var name = m.Success ? m.Groups["n"].Value : fileName;
        return name.Replace('_', ' ').Trim();
    }

    // archives

    IArchive OpenArchive(ModEntry m)
    {
        var path = ArchivePath(m);
        if (!File.Exists(path)) throw new ModManagerException($"The stored archive for '{m.Name}' is missing from the data folder.");
        try
        {
            return ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = m.Password });
        }
        catch (Exception ex) when (IsPasswordError(ex))
        {
            throw new ModManagerException($"'{m.Name}' is password protected and the password is missing or wrong.", ex);
        }
        catch (Exception ex) when (ex is not ModManagerException)
        {
            throw new ModManagerException($"Couldn't open the archive for '{m.Name}': {ex.Message}", ex);
        }
    }

    static bool IsPasswordError(Exception ex) =>
        ex is CryptographicException || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        (ex.InnerException != null && IsPasswordError(ex.InnerException));

    // true if the archive is encrypted and no password has been stored for it yet
    public bool NeedsPassword(ModEntry m)
    {
        if (m.Password != null) return false;
        try
        {
            using var a = ArchiveFactory.OpenArchive(ArchivePath(m));
            return a.Entries.Any(e => e.IsEncrypted);
        }
        catch (Exception ex) when (IsPasswordError(ex)) { return true; }
        catch { return false; } // other problems get reported properly by Install
    }

    // reads only the archive's file list and works out where everything would go
    public ArchiveLayout.Plan Preview(ModEntry m)
    {
        using var archive = OpenArchive(m);
        return BuildPlan(archive, m);
    }

    // reads the file list (archives load it lazily, so wrong passwords often surface here)
    static ArchiveLayout.Plan BuildPlan(IArchive archive, ModEntry m)
    {
        List<string> keys;
        try
        {
            keys = archive.Entries.Where(e => !e.IsDirectory && e.Key != null).Select(e => e.Key!).ToList();
        }
        catch (Exception ex) when (IsPasswordError(ex))
        {
            throw new ModManagerException($"'{m.Name}': wrong or missing password.", ex);
        }
        catch (Exception ex)
        {
            throw new ModManagerException($"Couldn't read the archive for '{m.Name}': {ex.Message}", ex);
        }
        return ArchiveLayout.Build(keys);
    }

    //Other installed mods that own files this mod would overwrite
    public Dictionary<string, ModEntry> FindConflicts(IEnumerable<string> targetFullPaths)
    {
        var result = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targetFullPaths)
        {
            if (State.FileOwners.TryGetValue(t, out var stack) && stack.Count > 0)
            {
                var owner = State.Mods.FirstOrDefault(x => x.Id == stack[^1]);
                if (owner != null) result[t] = owner;
            }
        }
        return result;
    }

    // install

    public void Install(ModEntry m, IProgress<string>? progress = null)
    {
        if (m.Installed) return;
        var game = State.GamePath;
        if (!GameLocator.IsValidGameDir(game))
            throw new ModManagerException("Set a valid Cyberpunk 2077 game folder first.");
        game = Path.GetFullPath(game!);

        using var archive = OpenArchive(m);
        var plan = BuildPlan(archive, m);
        if (plan.Map.Count == 0) throw new ModManagerException($"'{m.Name}' doesn't contain any files to install.");

        m.Files.Clear();
        m.InstalledTo = game;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int done = 0;

        void WriteEntry(string? key, Action<Stream> copyTo)
        {
            if (key == null || !plan.Map.TryGetValue(key, out var rel)) return;
            var target = SafeCombine(game, rel);

            if (written.Add(target))
            {
                EnsureDirectory(Path.GetDirectoryName(target)!, game);
                if (File.Exists(target))
                {
                    var bak = Path.Combine(BackupsDir, m.Id, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                    File.Move(target, bak, overwrite: true);
                }
                m.Files.Add(rel);
                lock (_lock)
                {
                    if (!State.FileOwners.TryGetValue(target, out var stack))
                        State.FileOwners[target] = stack = new List<string>();
                    stack.Add(m.Id);
                }
            }

            using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                copyTo(fs);
            progress?.Report($"Installing {m.Name}: {++done}/{plan.Map.Count}  {rel}");
        }

        try
        {
            if (archive.IsSolid || archive.Type == SharpCompress.Common.ArchiveType.SevenZip)
            {
                // solid archives must be read front to back
                using var reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory) continue;
                    WriteEntry(reader.Entry.Key, s => reader.WriteEntryTo(s));
                }
            }
            else
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;
                    WriteEntry(entry.Key, s => { using var es = entry.OpenEntryStream(); es.CopyTo(s); });
                }
            }
        }
        catch (Exception ex)
        {
            // undo whatever got written so the game folder is left as it was
            try { RemoveFiles(m, null); } catch { /* best effort */ }
            m.Files.Clear();
            m.InstalledTo = null;
            Save();
            if (IsPasswordError(ex)) throw new ModManagerException($"'{m.Name}': wrong or missing password.", ex);
            if (ex is IOException or UnauthorizedAccessException)
                throw new ModManagerException($"Couldn't write into the game folder while installing '{m.Name}' ({ex.Message}). Is the game running?", ex);
            if (ex is ModManagerException) throw;
            throw new ModManagerException($"Installing '{m.Name}' failed: {ex.Message}", ex);
        }

        m.Installed = true;
        m.InstalledAt = DateTime.Now;
        Save();
    }

    // uninstall

    public void Uninstall(ModEntry m, IProgress<string>? progress = null)
    {
        if (!m.Installed) return;
        try
        {
            RemoveFiles(m, progress);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Save(); // keep track of what's left
            throw new ModManagerException($"Couldn't remove a file of '{m.Name}' ({ex.Message}). Is the game running? Nothing else was changed for the remaining files.", ex);
        }
        m.Installed = false;
        m.InstalledAt = null;
        m.InstalledTo = null;
        Save();
    }

    // deletes (or restores) every file of the mod, removing each from m.Files as it goes
    void RemoveFiles(ModEntry m, IProgress<string>? progress)
    {
        var game = m.InstalledTo ?? State.GamePath ?? throw new ModManagerException("Unknown game folder.");
        int total = m.Files.Count, done = 0;

        for (int i = m.Files.Count - 1; i >= 0; i--)
        {
            var rel = m.Files[i];
            var target = SafeCombine(game, rel);
            var myBackup = Path.Combine(BackupsDir, m.Id, rel);

            lock (_lock)
            {
                State.FileOwners.TryGetValue(target, out var stack);
                int idx = stack?.LastIndexOf(m.Id) ?? -1;

                if (stack == null || idx < 0 || idx == stack.Count - 1)
                {
                    // this mod's version is the one on disk: put back what was there before (or delete)
                    if (File.Exists(myBackup)) File.Move(myBackup, target, overwrite: true);
                    else if (File.Exists(target)) File.Delete(target);
                }
                else
                {
                    // a later mod overwrote this file. That mod's backup is *our* version; replace it with
                    // whatever was underneath us so uninstalling that mod later restores the right thing
                    var aboveBackup = Path.Combine(BackupsDir, stack[idx + 1], rel);
                    if (File.Exists(aboveBackup)) File.Delete(aboveBackup);
                    if (File.Exists(myBackup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(aboveBackup)!);
                        File.Move(myBackup, aboveBackup);
                    }
                }

                if (stack != null && idx >= 0)
                {
                    stack.RemoveAt(idx);
                    if (stack.Count == 0) State.FileOwners.Remove(target);
                }
                m.Files.RemoveAt(i);
            }
            progress?.Report($"Uninstalling {m.Name}: {++done}/{total}  {rel}");
        }

        PruneCreatedDirs();

        try
        {
            var backupDir = Path.Combine(BackupsDir, m.Id);
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, recursive: true);
        }
        catch { /* leave it */ }
    }

    // helpers

    static string SafeCombine(string root, string rel)
    {
        var full = Path.GetFullPath(Path.Combine(root, rel));
        var rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new ModManagerException($"Refusing to write outside the game folder: {rel}");
        return full;
    }

    void EnsureDirectory(string dir, string root)
    {
        var missing = new List<string>();
        var d = dir;
        var rootFull = Path.GetFullPath(root).TrimEnd('\\');
        while (!Directory.Exists(d) && !d.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            missing.Add(d);
            d = Path.GetDirectoryName(d)!;
        }
        Directory.CreateDirectory(dir);
        lock (_lock)
            foreach (var m in missing)
                if (!State.CreatedDirs.Contains(m, StringComparer.OrdinalIgnoreCase)) State.CreatedDirs.Add(m);
    }

    void PruneCreatedDirs()
    {
        lock (_lock)
        {
            foreach (var dir in State.CreatedDirs.OrderByDescending(d => d.Length).ToList())
            {
                try
                {
                    if (!Directory.Exists(dir)) State.CreatedDirs.Remove(dir);
                    else if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                        State.CreatedDirs.Remove(dir);
                    }
                }
                catch { /* leave it */ }
            }
        }
    }
}
