using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace Popfold;

/// <summary>
/// Coloca "Popfold" no menu Novo do botão direito da área de trabalho (só para o usuário atual,
/// sem precisar de administrador) e recebe o pedido na instância que já está aberta.
/// </summary>
internal static class ShellIntegration
{
    private const string Extension = ".popfold";
    private const string ProgId = "Popfold.Folder";
    private const string EventName = @"Local\Popfold.NewFolder";

    private const string OpenEventName = @"Local\Popfold.OpenFolder";
    private static string OpenRequestPath => Path.Combine(ConfigStore.DataDir, "open-request");

    private static readonly System.Collections.Generic.List<EventWaitHandle> _events = new();
    private static volatile bool _stopping;

    /// <summary>
    /// Registra (ou atualiza) a entrada do menu Novo. O Windows 11 só mostra entradas que criam um arquivo,
    /// então o Explorer cria um ".popfold" vazio; o <see cref="DesktopWatcher"/> troca esse arquivo por uma pasta.
    /// </summary>
    /// <summary>
    /// Modelo que o Explorer copia em "Novo > Popfold". É um arquivo oculto: a cópia também nasce oculta,
    /// então o arquivo temporário não chega a aparecer na área de trabalho antes de o Popfold apagá-lo.
    /// </summary>
    public static string TemplatePath => Path.Combine(AppContext.BaseDirectory, "Popfold.template");

    public static void EnsureTemplate()
    {
        try
        {
            if (!File.Exists(TemplatePath)) File.WriteAllBytes(TemplatePath, Array.Empty<byte>());
            File.SetAttributes(TemplatePath, FileAttributes.Hidden | FileAttributes.System); // "sistema": some mesmo com "mostrar ocultos" ligado
        }
        catch { }
    }

    public static void Register()
    {
        EnsureTemplate();
        string exe = Environment.ProcessPath ?? "";
        if (exe.Length == 0) return;
        string open = $"\"{exe}\" --new \"%1\"";

        using (var check = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}\ShellNew"))
            if (check?.GetValue("FileName") as string == TemplatePath) return;

        using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
        {
            ext.SetValue("", ProgId);
            ext.DeleteSubKeyTree("ShellNew", false);
            using var shellNew = ext.CreateSubKey("ShellNew");
            shellNew.SetValue("FileName", TemplatePath);
        }
        using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            prog.SetValue("", AppInfo.Name); // nome que aparece no menu Novo
            using (var icon = prog.CreateSubKey("DefaultIcon"))
                icon.SetValue("", $"{exe},0");
            // Se um .popfold for criado fora da área de trabalho, abrir o arquivo também vira uma pasta.
            using var cmd = prog.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", open);
        }
        Native.SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);

        // O Explorer guarda a lista do menu Novo em cache; apagar o cache faz ele reler.
        try
        {
            using var cache = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew", writable: true);
            cache?.DeleteValue("Classes", false);
        }
        catch { }
    }

    /// <summary>Remove a entrada do menu Novo (usado ao desinstalar).</summary>
    public static void Unregister()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{Extension}", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false); } catch { }
    }

    /// <summary>Chamado pela segunda instância ("Popfold.exe --new"): avisa a instância aberta.</summary>
    public static void SignalNewFolder() => Signal(EventName);

    /// <summary>Clique numa Popfold fixada na barra de tarefas ("Popfold.exe --open id"): pede à instância aberta para abrir o painel.</summary>
    public static void SignalOpenFolder(string id)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.DataDir);
            File.WriteAllText(OpenRequestPath, id);
        }
        catch { return; }
        Signal(OpenEventName);
    }

    private static void Signal(string eventName)
    {
        // Deixa a instância aberta trazer o painel para a frente (quem tem permissão agora somos nós).
        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcessesByName("Popfold").Where(p => p.Id != self))
            Native.AllowSetForegroundWindow(p.Id);

        try
        {
            if (EventWaitHandle.TryOpenExisting(eventName, out var ev))
                using (ev) ev.Set();
        }
        catch { }
    }

    public static void ListenForNewFolder(Action onRequest) => Listen(EventName, onRequest);

    public static void ListenForOpenFolder(Action<string> onRequest) => Listen(OpenEventName, () =>
    {
        string id = "";
        try
        {
            id = File.ReadAllText(OpenRequestPath).Trim();
            File.Delete(OpenRequestPath);
        }
        catch { }
        if (id.Length > 0) onRequest(id);
    });

    private static void Listen(string eventName, Action onRequest)
    {
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        _events.Add(ev);
        new Thread(() =>
        {
            while (!_stopping)
            {
                try
                {
                    ev.WaitOne();
                    if (!_stopping) onRequest();
                }
                catch { return; }
            }
        })
        { IsBackground = true, Name = "Popfold.Listener." + eventName }.Start();
    }

    public static void StopListening()
    {
        _stopping = true;
        foreach (var ev in _events)
            try { ev.Set(); } catch { }
    }
}
