using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Popfold;

/// <summary>
/// Vigia as pastas da área de trabalho: quando "Novo > Popfold" cria um arquivo .popfold,
/// apaga o arquivo e avisa o app para criar a pasta no lugar do clique.
/// </summary>
internal sealed class DesktopWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Action _onNewFolder;

    public DesktopWatcher(Action onNewFolder)
    {
        _onNewFolder = onNewFolder;
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };
        foreach (var dir in dirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            try
            {
                var w = new FileSystemWatcher(dir, "*.popfold") { IncludeSubdirectories = false };
                w.Created += (_, e) => Handle(e.FullPath);
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch { }
        }
    }

    private void Handle(string path)
    {
        _onNewFolder();
        // O Explorer ainda está mexendo no arquivo (modo renomear): apaga com algumas tentativas.
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                if (i > 0) await Task.Delay(100); // primeira tentativa imediata
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    break;
                }
                catch { }
            }
            // Avisa o Explorer que o arquivo sumiu. Muitas vezes o arquivo é apagado antes de o Explorer
            // pôr o ícone na tela (e entrar no modo "renomear"); um aviso só, cedo demais, deixa um
            // ícone fantasma. Por isso avisa de novo depois e, no fim, atualiza a área de trabalho (F5).
            foreach (int wait in new[] { 0, 300, 900 })
            {
                await Task.Delay(wait);
                Notify(path);
            }
            await Task.Delay(1200);
            if (!File.Exists(path)) Native.RefreshDesktop();
        });
    }

    private static void Notify(string path)
    {
        Native.SHChangeNotifyPath(0x4 /* SHCNE_DELETE */, 0x5 /* SHCNF_PATHW */, path, IntPtr.Zero);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Native.SHChangeNotifyPath(0x1000 /* SHCNE_UPDATEDIR */, 0x5, dir, IntPtr.Zero);
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
}
