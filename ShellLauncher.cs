using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Popfold;

/// <summary>Executa itens via ShellExecute — o mesmo que um duplo clique, sem abrir o Explorador.</summary>
public static class ShellLauncher
{
    public static void Launch(FolderItem item, bool asAdmin = false)
    {
        try
        {
            var psi = new ProcessStartInfo(item.Path) { UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(item.Arguments)) psi.Arguments = item.Arguments;
            if (asAdmin) psi.Verb = "runas";
            if (item.Kind == ItemKind.File && File.Exists(item.Path) && !item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                psi.WorkingDirectory = Path.GetDirectoryName(item.Path) ?? "";
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Usuário cancelou o UAC.
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{L.CannotOpen(item.Name)}\n\n{item.Path}\n\n{ex.Message}",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Abre o Explorador no local do item (só quando o usuário pede).</summary>
    public static void OpenLocation(FolderItem item)
    {
        string target = item.Path;
        if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            target = ResolveShortcut(target) ?? target;

        try
        {
            if (Directory.Exists(target))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
            else if (File.Exists(target))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
        }
        catch { }
    }

    public static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return null;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic sc = shell.CreateShortcut(lnkPath);
            string target = sc.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch { return null; }
    }
}
