using CyberModManager.Core;
using CyberModManager.UI;

namespace CyberModManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

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

    static string ResolveDataDir()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "ModManagerData");
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
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberModManager");
        }
    }
}
