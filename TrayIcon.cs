using System;
using System.Diagnostics;
using System.IO;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace Popfold;

/// <summary>Ícone na bandeja: criar pastas, opções, iniciar com o Windows, sair.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly App _app;

    public TrayIcon(App app)
    {
        _app = app;
        var menu = new WinForms.ContextMenuStrip();
        menu.Opening += (_, e) => { Fill(menu); e.Cancel = false; }; // remonta no idioma atual
        Fill(menu);

        _icon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = AppInfo.Name,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _app.ShowSettings();
    }

    private void Fill(WinForms.ContextMenuStrip menu)
    {
        menu.Items.Clear();
        menu.Items.Add(L.NewFolder, null, (_, _) => _app.AddFolder());
        // Pastas que estão só na barra de tarefas: trazer de volta para a área de trabalho.
        var hidden = new WinForms.ToolStripMenuItem(L.ShowOnDesktop);
        foreach (var g in _app.TaskbarOnlyFolders)
        {
            var group = g;
            hidden.DropDownItems.Add(group.Name, null, (_, _) => _app.SetOnDesktop(group, true));
        }
        if (hidden.DropDownItems.Count > 0) menu.Items.Add(hidden);

        menu.Items.Add(L.Options, null, (_, _) => _app.ShowSettings());

        var startup = new WinForms.ToolStripMenuItem(L.StartWithWindows) { CheckOnClick = true };
        try { startup.Checked = StartupManager.IsEnabled; } catch { }
        startup.CheckedChanged += (_, _) =>
        {
            try { StartupManager.Set(startup.Checked); } catch { }
        };
        menu.Items.Add(startup);

        menu.Items.Add(L.OpenDataFolder, null, (_, _) =>
        {
            Directory.CreateDirectory(ConfigStore.DataDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ConfigStore.DataDir}\"") { UseShellExecute = true });
        });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(L.Quit, null, (_, _) => _app.ExitApp());
    }

    public void ShowTip(string title, string text) => _icon.ShowBalloonTip(5000, title, text, WinForms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    /// <summary>Ícone do app (Popfold.ico embutido), no tamanho da bandeja.</summary>
    private static Drawing.Icon CreateIcon()
    {
        try
        {
            var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Popfold.ico"));
            if (res != null)
                using (res.Stream)
                    return new Drawing.Icon(res.Stream, WinForms.SystemInformation.SmallIconSize);
        }
        catch { }
        return DrawFallbackIcon();
    }

    /// <summary>Desenha o ícone (quadrado arredondado com grade 2×2) caso o arquivo não carregue.</summary>
    private static Drawing.Icon DrawFallbackIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using (var path = RoundedRect(new Drawing.RectangleF(1, 1, 30, 30), 8))
            using (var bg = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 79, 124, 245)))
                g.FillPath(bg, path);

            var colors = new[]
            {
                Drawing.Color.White, Drawing.Color.FromArgb(255, 255, 214, 102),
                Drawing.Color.FromArgb(255, 140, 230, 190), Drawing.Color.FromArgb(255, 255, 160, 190),
            };
            int i = 0;
            foreach (var (x, y) in new[] { (7, 7), (17, 7), (7, 17), (17, 17) })
            {
                using var p = RoundedRect(new Drawing.RectangleF(x, y, 8, 8), 2.5f);
                using var b = new Drawing.SolidBrush(colors[i++]);
                g.FillPath(b, p);
            }
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private static Drawing.Drawing2D.GraphicsPath RoundedRect(Drawing.RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
