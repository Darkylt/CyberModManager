namespace CyberModManager.Core;

public static class ArchiveLayout
{
    // top-level folders of a Cyberpunk 2077 install that mods write into
    public static readonly HashSet<string> KnownRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "archive", "bin", "engine", "r6", "red4ext", "mods",
    };

    static readonly HashSet<string> JunkNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", "Thumbs.db", "desktop.ini",
    };

    static readonly HashSet<string> DocExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".url", ".html", ".htm", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".rtf", ".docx",
    };

    static readonly HashSet<string> LooseArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".archive", ".xl",
    };

    public sealed class Plan
    {
        // archive entry key (exactly as in the archive) -> path relative to the game folder
        public Dictionary<string, string> Map { get; } = new(StringComparer.Ordinal);
        public List<string> Notes { get; } = new();
        public List<string> Skipped { get; } = new();
    }

    public static Plan Build(IEnumerable<string> entryKeys)
    {
        var plan = new Plan();
        var files = new List<(string Key, string[] Parts)>();

        foreach (var key in entryKeys)
        {
            var parts = key.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts.Any(p => p == ".." || p == "." || p.Contains(':')))
            {
                plan.Skipped.Add(key);
                plan.Notes.Add($"Skipped unsafe path: {key}");
                continue;
            }
            if (parts.Any(p => p.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)) || JunkNames.Contains(parts[^1]))
            {
                plan.Skipped.Add(key);
                continue;
            }
            files.Add((key, parts));
        }

        // strip wrapper folders ("MyMod v1.2\archive\pc\mod\x.archive" -> "archive\pc\mod\x.archive")
        // only the real content counts when deciding; readmes sitting next to the wrapper folder are ignored
        var content = files.Where(f => !IsDoc(f.Parts[^1])).ToList();
        if (content.Count == 0) content = files;
        int strip = 0;
        while (true)
        {
            if (content.Count == 0 || content.Any(f => f.Parts.Length <= strip + 1)) break;
            var first = content[0].Parts[strip];
            if (KnownRoots.Contains(first)) break;
            if (!content.All(f => f.Parts[strip].Equals(first, StringComparison.OrdinalIgnoreCase))) break;
            strip++;
        }
        var prefix = content.Count > 0 ? content[0].Parts.Take(strip).ToArray() : Array.Empty<string>();
        if (strip > 0) plan.Notes.Add($"Removed wrapper folder \"{string.Join('\\', prefix)}\".");

        var mapped = new List<(string Key, string[] Parts)>();
        foreach (var f in files)
        {
            bool hasPrefix = f.Parts.Length > strip &&
                             prefix.Select((p, i) => f.Parts[i].Equals(p, StringComparison.OrdinalIgnoreCase)).All(x => x);
            mapped.Add((f.Key, hasPrefix ? f.Parts[strip..] : f.Parts));
        }

        // loose .archive files with no folder structure belong in archive\pc\mod
        bool anyUnderRoot = mapped.Any(m => m.Parts.Length > 1 && KnownRoots.Contains(m.Parts[0]));
        var nonDoc = mapped.Where(m => !IsDoc(m.Parts[^1])).ToList();
        if (!anyUnderRoot && nonDoc.Count > 0 && nonDoc.All(m => LooseArchiveExtensions.Contains(Path.GetExtension(m.Parts[^1]))))
        {
            plan.Notes.Add("Loose .archive files: placing them in archive\\pc\\mod.");
            mapped = mapped.Select(m => IsDoc(m.Parts[^1]) ? m : (m.Key, new[] { "archive", "pc", "mod" }.Concat(m.Parts).ToArray())).ToList();
        }
        else if (!anyUnderRoot && nonDoc.Count > 0)
        {
            plan.Notes.Add("This archive doesn't use the usual folder layout (archive\\, bin\\, r6\\ ...). Files will be extracted to the game folder as-is.");
        }

        foreach (var m in mapped)
        {
            // readmes / screenshots that would land loose in the game root are left out
            if (m.Parts.Length == 1 && IsDoc(m.Parts[0]))
            {
                plan.Skipped.Add(m.Key);
                continue;
            }
            plan.Map[m.Key] = string.Join('\\', m.Parts);
        }

        return plan;
    }

    static bool IsDoc(string fileName) => DocExtensions.Contains(Path.GetExtension(fileName));
}
