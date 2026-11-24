using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CyberModManager.Core;

public static class GameLocator
{
    public const string ExeRelative = @"bin\x64\Cyberpunk2077.exe";

    public static bool IsValidGameDir(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, ExeRelative));

    // looks for the game in Steam libraries, GOG and Epic. returns null if not found
    public static string? Find()
    {
        foreach (var candidate in Candidates())
        {
            try
            {
                if (IsValidGameDir(candidate)) return Path.GetFullPath(candidate);
            }
            catch { /* ignore bad candidates */ }
        }
        return null;
    }

    static IEnumerable<string> Candidates()
    {
        // Steam (all library folders)
        var steamRoots = new List<string>();
        var steamPath = ReadReg(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
                        ?? ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        if (steamPath != null) steamRoots.Add(steamPath.Replace('/', '\\'));
        steamRoots.Add(@"C:\Program Files (x86)\Steam");

        foreach (var root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(root, "steamapps", "common", "Cyberpunk 2077");
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            string text = "";
            try { if (File.Exists(vdf)) text = File.ReadAllText(vdf); } catch { }
            foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"(.+?)\""))
            {
                var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                yield return Path.Combine(lib, "steamapps", "common", "Cyberpunk 2077");
            }
        }

        // GOG
        foreach (var key in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games\1423049311", @"SOFTWARE\GOG.com\Games\1423049311" })
        {
            var p = ReadReg(Registry.LocalMachine, key, "path");
            if (p != null) yield return p;
        }

        // common default folders on every drive
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.RootDirectory.FullName))
        {
            yield return Path.Combine(drive, @"Program Files (x86)\Steam\steamapps\common\Cyberpunk 2077");
            yield return Path.Combine(drive, @"SteamLibrary\steamapps\common\Cyberpunk 2077");
            yield return Path.Combine(drive, @"Program Files (x86)\GOG Galaxy\Games\Cyberpunk 2077");
            yield return Path.Combine(drive, @"GOG Games\Cyberpunk 2077");
            yield return Path.Combine(drive, @"Program Files\Epic Games\Cyberpunk2077");
            yield return Path.Combine(drive, @"Epic Games\Cyberpunk2077");
        }
    }

    static string? ReadReg(RegistryKey hive, string key, string value)
    {
        try
        {
            using var k = hive.OpenSubKey(key);
            return k?.GetValue(value) as string;
        }
        catch { return null; }
    }
}
