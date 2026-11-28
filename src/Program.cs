using CyberModManager.Core;
using CyberModManager.UI;

namespace CyberModManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Theme.EnableDarkMode();

        ModStore store;
        try
        {
            store = new ModStore(ResolveDataDir());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Couldn't open the mod manager's data folder:\n\n" + ex.Message, "Cyber Mod Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(store));
    }

    // If that folder isn't writable, fall back to %LocalAppData%\CyberModManager
    static string ResolveDataDir()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "ModManagerData");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberModManager");

        if (!HasState(portable) && HasState(local)) return local;

        try
        {
            Directory.CreateDirectory(portable);
            var probe = Path.Combine(portable, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return portable;
        }
        catch
        {
            return local;
        }
    }

    static bool HasState(string dir) =>
        File.Exists(Path.Combine(dir, "state.json")) || File.Exists(Path.Combine(dir, "state.json.bak"));
}
