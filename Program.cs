using System;
using System.Threading;

namespace Popfold;

/// <summary>
/// Ponto de entrada. Os pedidos rápidos (clique na Popfold fixada, item da lista de atalhos, "Novo > Popfold")
/// são resolvidos aqui, antes de carregar o WPF: com o Popfold já aberto, a resposta chega bem mais rápido.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string first = args.Length > 0 ? args[0].ToLowerInvariant() : "";

        if (first == "--run" && args.Length >= 3)
        {
            App.RunFromJumpList(args[1], args[2]);
            return 0;
        }

        if (Mutex.TryOpenExisting(@"Local\Popfold.SingleInstance", out var running))
        {
            running.Dispose();
            if (first == "--open" && args.Length >= 2) { ShellIntegration.SignalOpenFolder(args[1]); return 0; }
            if (first == "--new")
            {
                App.DiscardPopfoldFiles(args);
                ShellIntegration.SignalNewFolder();
                return 0;
            }
        }

        // Aberto de dentro de um app empacotado (MSIX)? O Windows desviaria %AppData% e o registro para uma
        // cópia particular daquele app: as pastas "sumiriam" quando o Popfold fosse aberto normalmente.
        if (RunningInsidePackage())
        {
            L.Apply("auto");
            MessageBoxW(IntPtr.Zero, L.PackagedWarning, AppInfo.Name, 0x30 /* MB_ICONWARNING */);
            return 1;
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    /// <summary>
    /// Testa na prática: grava um arquivinho em %AppData%\Popfold e vê se ele foi parar na pasta particular
    /// de algum app empacotado (%LocalAppData%\Packages\*\LocalCache\Roaming). Isso acontece mesmo quando
    /// o processo não tem identidade de pacote, então perguntar ao Windows não basta.
    /// </summary>
    private static bool RunningInsidePackage()
    {
        string probe = "probe-" + Guid.NewGuid().ToString("N");
        string path = System.IO.Path.Combine(ConfigStore.DataDir, probe);
        try
        {
            System.IO.Directory.CreateDirectory(ConfigStore.DataDir);
            System.IO.File.WriteAllBytes(path, Array.Empty<byte>());
            string packages = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (!System.IO.Directory.Exists(packages)) return false;
            foreach (var pkg in System.IO.Directory.EnumerateDirectories(packages))
                if (System.IO.File.Exists(System.IO.Path.Combine(pkg, @"LocalCache\Roaming", AppInfo.Name, probe)))
                    return true;
            return false;
        }
        catch { return false; }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
}
