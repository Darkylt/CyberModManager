using System.IO.Compression;
using CyberModManager.Core;

var root = Path.Combine(Path.GetTempPath(), "cmm-smoke-" + Guid.NewGuid().ToString("N")[..8]);
var game = Path.Combine(root, "Cyberpunk 2077");
var data = Path.Combine(root, "data");
var src = Path.Combine(root, "src");
int failures = 0;

void Check(bool ok, string what)
{
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
    if (!ok) failures++;
}

string G(string rel) => Path.Combine(game, rel);
string Read(string rel) => File.ReadAllText(G(rel));

string MakeZip(string name, Dictionary<string, string> files)
{
    var path = Path.Combine(src, name);
    using var z = ZipFile.Open(path, ZipArchiveMode.Create);
    foreach (var (k, v) in files)
    {
        using var w = new StreamWriter(z.CreateEntry(k).Open());
        w.Write(v);
    }
    return path;
}

try
{
    Directory.CreateDirectory(src);
    Directory.CreateDirectory(G(@"bin\x64"));
    File.WriteAllText(G(GameLocator.ExeRelative), "exe");
    Directory.CreateDirectory(G(@"engine\config"));
    File.WriteAllText(G(@"engine\config\base.ini"), "VANILLA");
    var vanillaSnapshot = Snapshot();

    // Mod A: wrapped in an extra folder, with a readme next to it
    var zipA = MakeZip("Mod A-1234-1-0-1714153406.zip", new()
    {
        ["Mod A v1.0/archive/pc/mod/a.archive"] = "A-archive",
        ["Mod A v1.0/r6/scripts/shared.reds"] = "A-shared",
        ["Mod A v1.0/readme.txt"] = "read me",
        ["readme.txt"] = "read me too",
    });
    // Mod B: overwrites A's shared.reds and a vanilla game file
    var zipB = MakeZip("ModB.zip", new()
    {
        ["r6/scripts/shared.reds"] = "B-shared",
        ["engine/config/base.ini"] = "B-INI",
        ["red4ext/plugins/b/b.dll"] = "B-dll",
    });
    // Mod C: loose .archive files
    var zipC = MakeZip("ModC.zip", new() { ["c.archive"] = "C-archive", ["c.xl"] = "C-xl" });

    var store = new ModStore(data);
    store.State.GamePath = game;

    Console.WriteLine("Add");
    var a = store.AddArchive(zipA);
    var b = store.AddArchive(zipB);
    var c = store.AddArchive(zipC);
    Check(a.Name == "Mod A", $"Nexus name cleaned (got '{a.Name}')");
    Check(File.Exists(store.ArchivePath(a)), "archive copied into data folder");

    Console.WriteLine("Install A, B, C");
    store.Install(a);
    Check(Read(@"archive\pc\mod\a.archive") == "A-archive", "wrapper folder stripped");
    Check(!File.Exists(G("readme.txt")), "readme not dumped into game root");
    store.Install(b);
    Check(Read(@"r6\scripts\shared.reds") == "B-shared", "B overwrote A's file");
    Check(Read(@"engine\config\base.ini") == "B-INI", "B overwrote vanilla file");
    store.Install(c);
    Check(Read(@"archive\pc\mod\c.archive") == "C-archive" && Read(@"archive\pc\mod\c.xl") == "C-xl", "loose .archive/.xl placed in archive\\pc\\mod");

    Console.WriteLine("Uninstall A (middle of the stack)");
    store.Uninstall(a);
    Check(!File.Exists(G(@"archive\pc\mod\a.archive")), "A's own file removed");
    Check(Read(@"r6\scripts\shared.reds") == "B-shared", "B's version of shared file kept");

    Console.WriteLine("Reload state from disk");
    store = new ModStore(data);
    b = store.State.Mods.Single(m => m.Id == b.Id);
    c = store.State.Mods.Single(m => m.Id == c.Id);
    Check(b.Installed && c.Installed && b.Files.Count == 3, "state persisted");

    Console.WriteLine("Categories");
    store.SetCategory([b], "Visuals");
    store.SetCategory([c], "visuals ");
    Check(c.Category == "Visuals", $"category reuses existing spelling (got '{c.Category}')");
    store.RenameCategory("VISUALS", "Graphics");
    store = new ModStore(data);
    b = store.State.Mods.Single(m => m.Id == b.Id);
    c = store.State.Mods.Single(m => m.Id == c.Id);
    Check(b.Category == "Graphics" && c.Category == "Graphics" && store.Categories().SequenceEqual(["Graphics"]), "category rename persisted");
    Check(File.Exists(store.BackupStatePath), "state backup written");

    Console.WriteLine("Uninstall B");
    store.Uninstall(b);
    Check(!File.Exists(G(@"r6\scripts\shared.reds")), "shared file gone (A was uninstalled earlier)");
    Check(Read(@"engine\config\base.ini") == "VANILLA", "vanilla file restored");
    Check(!Directory.Exists(G("red4ext")), "folders created by B removed");

    Console.WriteLine("Remove C (installed) from list");
    store.Remove(c);
    Check(!File.Exists(G(@"archive\pc\mod\c.archive")), "removing an installed mod uninstalls it");
    Check(!File.Exists(store.ArchivePath(c)), "stored archive deleted");

    Console.WriteLine("Overlap, uninstalled newest-first");
    store.Install(a);
    store.Install(b);
    store.Uninstall(b);
    Check(Read(@"r6\scripts\shared.reds") == "A-shared", "A's version restored after B uninstall");
    store.Uninstall(a);

    var leftover = Snapshot().Except(vanillaSnapshot).Concat(vanillaSnapshot.Except(Snapshot())).ToList();
    Check(leftover.Count == 0, "game folder exactly back to original" + string.Concat(leftover.Select(l => "\n          diff: " + l)));
    Check(store.State.FileOwners.Count == 0, "no leftover ownership records");
    Check(!Directory.EnumerateFileSystemEntries(store.BackupsDir).Any(), "no leftover backups");

    Console.WriteLine("Failed install rolls back");
    var bad = store.AddArchive(MakeZip("Bad.zip", new() { ["archive/pc/mod/x.archive"] = "x", ["bin/x64/locked.dll"] = "new" }));
    File.WriteAllText(G(@"bin\x64\locked.dll"), "orig");
    using (File.Open(G(@"bin\x64\locked.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
    {
        try { store.Install(bad); Check(false, "install should fail on locked file"); }
        catch (ModManagerException ex) { Check(ex.Message.Contains("game running"), "friendly error: " + ex.Message); }
    }
    Check(!bad.Installed && !File.Exists(G(@"archive\pc\mod\x.archive")), "partial install rolled back");
    Check(Read(@"bin\x64\locked.dll") == "orig", "locked file untouched");

    Console.WriteLine("Update an installed mod");
    var d = store.AddArchive(MakeZip("ModD.zip", new() { ["archive/pc/mod/d.archive"] = "D1", ["r6/scripts/d_old.reds"] = "old" }));
    store.Rename(d, "My Mod D");
    store.SetCategory([d], "Gameplay");
    store.Install(d);
    var garbage = Path.Combine(src, "ModD v2.zip");
    File.WriteAllText(garbage, "this is not a zip");
    try { ModStore.CheckArchive(garbage); Check(false, "broken archive should be rejected"); }
    catch (ModManagerException) { Check(Read(@"archive\pc\mod\d.archive") == "D1", "broken update rejected before touching the installed mod"); }
    var v2 = MakeZip("ModD-v2.zip", new() { ["archive/pc/mod/d.archive"] = "D2" });
    ModStore.CheckArchive(v2);
    store.Uninstall(d);
    store.ReplaceArchive(d, v2);
    store.Install(d);
    Check(Read(@"archive\pc\mod\d.archive") == "D2", "new version installed");
    Check(!File.Exists(G(@"r6\scripts\d_old.reds")), "files dropped in the new version removed");
    Check(d.Name == "My Mod D" && d.Category == "Gameplay" && d.OriginalFileName == "ModD-v2.zip", "name and category kept, file name updated");
    Check(!Directory.EnumerateFiles(store.ArchivesDir, "*.new").Any(), "no temp files left in archives folder");
    store.Remove(d);

    Console.WriteLine("Data from older versions");
    var oldData = Path.Combine(root, "olddata");
    Directory.CreateDirectory(oldData);
    // state.json as written by 1.0.0: no FormatVersion, no Category
    File.WriteAllText(Path.Combine(oldData, "state.json"), """
        { "GamePath": "C:\\Games\\Cyberpunk 2077", "Mods": [ { "Id": "abc123", "Name": "Old Mod", "ArchiveFile": "abc123.zip",
          "OriginalFileName": "Old Mod.zip", "ArchiveSize": 10, "AddedAt": "2026-01-01T00:00:00", "Installed": false, "Files": [] } ],
          "FileOwners": {}, "CreatedDirs": [] }
        """);
    var old = new ModStore(oldData);
    Check(old.State.Mods.Count == 1 && old.State.Mods[0].Name == "Old Mod" && old.State.Mods[0].Category == null, "1.0.0 state.json loads");
    old.SetCategory(old.State.Mods, "Gameplay");
    File.WriteAllText(old.StatePath, "{ broken");
    old = new ModStore(oldData);
    Check(old.State.Mods.Count == 1 && old.State.GamePath == @"C:\Games\Cyberpunk 2077", "damaged state.json falls back to the backup");

    // .7z / password tests need 7-Zip to build the archives
    var sevenZip = @"C:\Program Files\7-Zip\7z.exe";
    if (!File.Exists(sevenZip)) { Console.WriteLine("(7-Zip not installed: skipping .7z / password tests)"); }
    else
    {
        var stage = Path.Combine(root, "stage");
        Directory.CreateDirectory(Path.Combine(stage, @"Wrapped\archive\pc\mod"));
        Directory.CreateDirectory(Path.Combine(stage, @"Wrapped\bin\x64\plugins"));
        File.WriteAllText(Path.Combine(stage, @"Wrapped\archive\pc\mod\s.archive"), "S-archive");
        File.WriteAllText(Path.Combine(stage, @"Wrapped\bin\x64\plugins\s.asi"), "S-asi");

        string Make7z(string name, params string[] extra)
        {
            var path = Path.Combine(src, name);
            var psi = new System.Diagnostics.ProcessStartInfo(sevenZip) { WorkingDirectory = stage, RedirectStandardOutput = true, UseShellExecute = false };
            foreach (var arg in new[] { "a" }.Concat(extra).Concat([path, "Wrapped"])) psi.ArgumentList.Add(arg);
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return path;
        }

        Console.WriteLine("Solid .7z");
        var s = store.AddArchive(Make7z("Solid.7z", "-ms=on"));
        Check(!store.NeedsPassword(s), "plain 7z needs no password");
        store.Install(s);
        Check(Read(@"archive\pc\mod\s.archive") == "S-archive" && Read(@"bin\x64\plugins\s.asi") == "S-asi", "7z extracted with wrapper stripped");
        store.Uninstall(s);
        Check(!Directory.Exists(G(@"bin\x64\plugins")), "7z uninstall cleaned up");

        foreach (var (file, zipArgs) in new[] { ("Locked.7z", new[] { "-psecret", "-mhe=on" }), ("Locked.zip", new[] { "-tzip", "-psecret" }) })
        {
            Console.WriteLine($"Password protected {Path.GetExtension(file)}");
            var p = store.AddArchive(Make7z(file, zipArgs));
            Check(store.NeedsPassword(p), "password detected");
            p.Password = "wrong";
            try { store.Install(p); Check(false, "wrong password should fail"); }
            catch (ModManagerException ex) { Check(ex.Message.Contains("password"), "wrong password reported: " + ex.Message); }
            Check(!p.Installed && !File.Exists(G(@"archive\pc\mod\s.archive")), "nothing left behind after wrong password");
            p.Password = "secret";
            store.Install(p);
            Check(Read(@"archive\pc\mod\s.archive") == "S-archive", "installed with correct password");
            store.Uninstall(p);
        }
    }
    Check(Snapshot().Where(x => !x.Contains("locked.dll")).SequenceEqual(vanillaSnapshot), "game folder back to original at the end");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

Console.WriteLine(failures == 0 ? "\nALL PASSED" : $"\n{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;

string[] Snapshot() =>
    Directory.EnumerateFileSystemEntries(game, "*", SearchOption.AllDirectories)
        .Select(p => p + "|" + (File.Exists(p) ? File.ReadAllText(p) : "<dir>"))
        .Order().ToArray();
