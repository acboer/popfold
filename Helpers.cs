using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Popfold;

internal static class ScreenUtil
{
    /// <summary>Área útil (sem a barra de tarefas) do monitor onde está a janela, em DIPs.</summary>
    public static Rect WorkAreaFor(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return SystemParameters.WorkArea;
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
        var dpi = VisualTreeHelper.GetDpi(w);
        var r = screen.WorkingArea;
        return new Rect(r.Left / dpi.DpiScaleX, r.Top / dpi.DpiScaleY, r.Width / dpi.DpiScaleX, r.Height / dpi.DpiScaleY);
    }
}

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Popfold";

    public static bool IsEnabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(ValueName, false);
    }

    /// <summary>Se a versão "Smart Folders" estava configurada para iniciar com o Windows, passa a iniciar o Popfold.</summary>
    public static void MigrateFromOldName()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k?.GetValue("SmartFolders") is not string) return;
            k.DeleteValue("SmartFolders", false);
            Set(true);
        }
        catch { }
    }
}

internal static class InputDialog
{
    public static string? Show(Window? owner, string title, string prompt, string initial)
    {
        var w = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStyle = WindowStyle.ToolWindow,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
        };
        var tb = new TextBox { Text = initial, Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(4) };
        var ok = new Button { Content = L.Ok, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = L.Cancel, Width = 80, IsCancel = true };
        ok.Click += (_, _) => w.DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = prompt });
        root.Children.Add(tb);
        root.Children.Add(buttons);
        w.Content = root;
        w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };

        return w.ShowDialog() == true ? tb.Text.Trim() : null;
    }
}
