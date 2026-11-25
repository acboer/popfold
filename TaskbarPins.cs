using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Popfold;

/// <summary>
/// Popfolds na barra de tarefas. O Windows 11 não deixa um programa se fixar sozinho, então o Popfold
/// cria um atalho por pasta (menu Iniciar > Popfold) com o ícone 2×2 dela; o usuário fixa esse atalho.
/// Clicar no ícone fixado roda "Popfold.exe --open &lt;id&gt;", que pede à instância aberta para abrir o painel.
/// </summary>
internal static class TaskbarPins
{
    private static string LinkDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.Name);
    private static string IconDir => Path.Combine(ConfigStore.DataDir, "taskbar");
    private static string ItemIconDir => Path.Combine(IconDir, "items");
    private static string PinnedDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    private static readonly int[] FolderSizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
    private static readonly int[] ItemSizes = { 16, 24, 32, 48 };

    public static string AppId(FolderGroup g) => "Popfold.Folder." + g.Id;
    private static string OpenArgs(FolderGroup g) => "--open " + g.Id;
    public static string RunArgs(FolderGroup g, int index) => $"--run {g.Id} {index}";

    /// <summary>Cria ou atualiza o atalho, o ícone e a lista de atalhos. Retorna o caminho do atalho.</summary>
    public static async Task<string> UpdateAsync(FolderGroup g)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath");
        var images = await LoadImagesAsync(g);

        // Ícone da pasta. O nome do arquivo muda junto com o conteúdo: o Windows guarda ícones em cache
        // pelo caminho, e um caminho novo obriga a barra de tarefas a desenhar o ícone novo.
        Directory.CreateDirectory(IconDir);
        byte[] ico = BuildIco(FolderSizes, s => DrawFolder(images, s));
        string icon = Path.Combine(IconDir, $"{g.Id}-{Hash(ico)}.ico");
        if (!File.Exists(icon)) File.WriteAllBytes(icon, ico);

        // Atalho no menu Iniciar (nome = nome da pasta).
        Directory.CreateDirectory(LinkDir);
        string lnk = Path.Combine(LinkDir, SafeFileName(g.Name) + ".lnk");
        foreach (var old in FindLinks(LinkDir, g))
            if (!string.Equals(old, lnk, StringComparison.OrdinalIgnoreCase)) TryDelete(old);
        ShellLink.Create(lnk, exe, OpenArgs(g), icon, L.TaskbarLinkDescription(g.Name), AppId(g));
        Native.SHChangeNotifyPath(0x2000 /* SHCNE_UPDATEITEM */, 0x5 /* SHCNF_PATHW */, lnk, IntPtr.Zero);

        // Cópia que o Windows guardou quando o atalho foi fixado: atualiza o ícone e o programa também.
        foreach (var pinned in FindLinks(PinnedDir, g))
        {
            try
            {
                ShellLink.Retarget(pinned, exe, icon);
                Native.SHChangeNotifyPath(0x2000, 0x5, pinned, IntPtr.Zero);
            }
            catch { }
        }

        // Ícones antigos desta pasta.
        foreach (var f in Directory.GetFiles(IconDir, g.Id + "-*.ico"))
            if (!string.Equals(f, icon, StringComparison.OrdinalIgnoreCase)) TryDelete(f);

        // Lista de atalhos (botão direito no ícone fixado): os itens da pasta.
        var jump = new List<ShellLink.JumpItem>();
        Directory.CreateDirectory(ItemIconDir);
        for (int i = 0; i < g.Items.Count; i++)
        {
            var img = images.Count > i ? images[i] : await LoadImageAsync(g.Items[i]);
            byte[] itemIco = BuildIco(ItemSizes, s => DrawItem(img, s));
            string itemIcon = Path.Combine(ItemIconDir, Hash(itemIco) + ".ico");
            if (!File.Exists(itemIcon)) File.WriteAllBytes(itemIcon, itemIco);
            jump.Add(new ShellLink.JumpItem(g.Items[i].Name, RunArgs(g, i), itemIcon));
        }
        try { ShellLink.SetJumpList(AppId(g), g.Name, exe, jump); } catch { }

        return lnk;
    }

    /// <summary>Pasta excluída: apaga o atalho, os ícones e a lista. O ícone já fixado fica (o Windows não deixa desafixar).</summary>
    public static void Remove(FolderGroup g)
    {
        if (Directory.Exists(LinkDir))
            foreach (var l in FindLinks(LinkDir, g)) TryDelete(l);
        if (Directory.Exists(IconDir))
            foreach (var f in Directory.GetFiles(IconDir, g.Id + "-*.ico")) TryDelete(f);
        ShellLink.DeleteJumpList(AppId(g));
    }

    private static IEnumerable<string> FindLinks(string dir, FolderGroup g)
    {
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        string args = OpenArgs(g);
        return Directory.GetFiles(dir, "*.lnk")
            .Where(f => string.Equals(ShellLink.ReadArguments(f)?.Trim(), args, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ---------- Desenho ----------

    private static async Task<List<ImageSource>> LoadImagesAsync(FolderGroup g)
    {
        var list = new List<ImageSource>();
        foreach (var item in g.Items.Take(4)) list.Add(await LoadImageAsync(item));
        return list;
    }

    private static async Task<ImageSource> LoadImageAsync(FolderItem item)
    {
        var img = IconProvider.GetIcon(item, out bool pending);
        if (pending) img = await IconProvider.LoadUrlIconAsync(item) ?? img;
        return img;
    }

    private static readonly Brush Lavanda = Frozen(Color.FromRgb(0xB7, 0x9C, 0xFF));
    private static readonly Brush EmptyCell = Frozen(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    private static readonly Brush[] Candies =
    {
        Frozen(Color.FromRgb(0xFF, 0x7A, 0xB6)), Frozen(Color.FromRgb(0x7F, 0xE3, 0xC4)),
        Frozen(Color.FromRgb(0xFF, 0xE2, 0x7A)), Frozen(Colors.White),
    };

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Quadrado lilás com os 4 primeiros itens em 2×2 (pasta vazia: as balas do ícone do Popfold).</summary>
    private static void DrawFolder(IReadOnlyList<ImageSource> images, int s, DrawingContext dc)
    {
        bool tiny = s <= 24;
        double pad = s * (tiny ? 0.07 : 0.11), gap = s * (tiny ? 0.05 : 0.06);
        double cell = (s - 2 * pad - gap) / 2;
        dc.DrawRoundedRectangle(Lavanda, null, new Rect(0, 0, s, s), s * 0.26, s * 0.26);
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect(pad + (i % 2) * (cell + gap), pad + (i / 2) * (cell + gap), cell, cell);
            if (i < images.Count) dc.DrawImage(images[i], r);
            else if (images.Count == 0)
            {
                double rad = cell * 0.46;
                dc.DrawEllipse(Candies[i], null, new Point(r.X + cell / 2, r.Y + cell / 2), rad, rad);
            }
            else dc.DrawRoundedRectangle(EmptyCell, null, r, cell * 0.25, cell * 0.25);
        }
    }

    private static void DrawItem(ImageSource img, int s, DrawingContext dc) => dc.DrawImage(img, new Rect(0, 0, s, s));

    private static DrawingVisual DrawFolder(IReadOnlyList<ImageSource> images, int s)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) DrawFolder(images, s, dc);
        return v;
    }

    private static DrawingVisual DrawItem(ImageSource img, int s)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) DrawItem(img, s, dc);
        return v;
    }

    // ---------- .ico ----------

    /// <summary>Arquivo .ico com um PNG por tamanho (formato aceito desde o Windows Vista).</summary>
    private static byte[] BuildIco(int[] sizes, Func<int, DrawingVisual> draw)
    {
        var frames = new List<(int Size, byte[] Png)>();
        foreach (int s in sizes)
        {
            var visual = draw(s);
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            var bmp = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            enc.Save(ms);
            frames.Add((s, ms.ToArray()));
        }

        using var o = new MemoryStream();
        using var w = new BinaryWriter(o);
        w.Write((short)0); w.Write((short)1); w.Write((short)frames.Count);
        int offset = 6 + 16 * frames.Count;
        foreach (var (size, png) in frames)
        {
            w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(png.Length); w.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in frames) w.Write(png);
        w.Flush();
        return o.ToArray();
    }

    // ---------- Utilitários ----------

    private static string Hash(byte[] data) => Convert.ToHexString(SHA1.HashData(data))[..10].ToLowerInvariant();

    private static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return s.Length == 0 ? AppInfo.Name : s;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
