namespace CyberModManager.Core;

public sealed class AppState
{
    // bumped when the file format changes in a way that needs migrating. Older files have 0/1.
    // New fields must always be optional so state.json from older versions keeps loading
    public int FormatVersion { get; set; } = 1;

    public string? GamePath { get; set; }
    public List<ModEntry> Mods { get; set; } = new();


    // for every file a mod has written into the game folder (full path), the stack of mod ids
    // that wrote it, oldest first. The last id is the mod whose version is currently on disk
    public Dictionary<string, List<string>> FileOwners { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // folders (full paths) that didn't exist until a mod was installed. Removed again once empty
    public List<string> CreatedDirs { get; set; } = new();
}

public sealed class ModEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";

    // user-chosen category, null = uncategorized
    public string? Category { get; set; }

    // file name of the stored copy inside the data folder's archives directory
    public string ArchiveFile { get; set; } = "";

    // file name the user originally dropped in
    public string OriginalFileName { get; set; } = "";
    public long ArchiveSize { get; set; }
    public DateTime AddedAt { get; set; }

    public bool Installed { get; set; }
    public DateTime? InstalledAt { get; set; }

    // The game folder this mod was installed into (uninstall always uses this)
    public string? InstalledTo { get; set; }
    
    // Installed files, relative to "InstalledTo"
    public List<string> Files { get; set; } = new();

    public string? Password { get; set; }
}
