using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Popfold;

/// <summary>Obtém ícones de programas/arquivos/pastas (via Shell) e de sites (favicon do próprio site).</summary>
public static class IconProvider
{
    private const int Size = 96;

    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<ImageSource?>> UrlLoads = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Popfold/0.3");
        return h;
    }

    /// <summary>Retorna o ícone imediatamente. Para sites, devolve um ícone provisório e
    /// <paramref name="pending"/> = true (use <see cref="LoadUrlIconAsync"/>).</summary>
    public static ImageSource GetIcon(FolderItem item, out bool pending)
    {
        pending = false;
        string key = item.Kind + "|" + item.Path;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        if (item.Kind == ItemKind.Url)
        {
            pending = true;
            return LetterTile(item.Name);
        }

        var img = LoadShellIcon(item.Path) ?? LetterTile(item.Name);
        Cache[key] = img;
        return img;
    }

    public static Task<ImageSource?> LoadUrlIconAsync(FolderItem item)
    {
        string key = item.Kind + "|" + item.Path;
        if (!UrlLoads.TryGetValue(key, out var task))
        {
            task = LoadFaviconAsync(item.Path).ContinueWith(t =>
            {
                var img = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                if (img != null) Cache[key] = img;
                return img;
            }, TaskScheduler.FromCurrentSynchronizationContext());
            UrlLoads[key] = task;
        }
        return task;
    }

    // ---------- Shell ----------

    private static BitmapSource? GetImage(Native.IShellItemImageFactory factory, int px)
    {
        var size = new Native.NativeSize { cx = px, cy = px };
        if (factory.GetImage(size, Native.SIIGBF_RESIZETOFIT, out var hbmp) != 0 || hbmp == IntPtr.Zero) return null;
        try { return FromHBitmap(hbmp); }
        finally { Native.DeleteObject(hbmp); }
    }

    /// <summary>
    /// Detecta a moldura que o Windows desenha em volta de ícones pequenos ampliados:
    /// uma borda fina na beirada e uma faixa totalmente transparente entre ela e o ícone no centro.
    /// </summary>
    private static bool LooksFramed(BitmapSource bmp)
    {
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        if (w < 64 || h < 64 || bmp.Format.BitsPerPixel != 32) return false;
        int stride = w * 4;
        var px = new byte[stride * h];
        bmp.CopyPixels(px, stride, 0);
        byte A(int x, int y) => px[y * stride + x * 4 + 3];

        int mid = h / 2, band = w / 6; // faixa de ~16 px num ícone de 96
        bool edge = A(0, mid) > 0 || A(1, mid) > 0;
        if (!edge) return false;
        for (int d = 4; d < band; d++)
        {
            // esquerda, direita, cima e baixo precisam estar vazios
            if (A(d, mid) != 0 || A(w - 1 - d, mid) != 0 || A(w / 2, d) != 0 || A(w / 2, h - 1 - d) != 0)
                return false;
        }
        return true;
    }

    private static ImageSource? LoadShellIcon(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return null;
        try
        {
            var iid = typeof(Native.IShellItemImageFactory).GUID;
            if (Native.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) == 0 && factory != null)
            {
                try
                {
                    var big = GetImage(factory, Size);
                    // Ícones antigos (só até 48 px) voltam "emoldurados" num quadrado quando pedimos 96 px.
                    // Nesse caso usa o tamanho nativo, sem moldura — igual à área de trabalho.
                    if (big != null && LooksFramed(big))
                        return GetImage(factory, 48) ?? big;
                    if (big != null) return big;
                }
                finally { Marshal.ReleaseComObject(factory); }
            }
        }
        catch { /* cai para o SHGetFileInfo */ }

        try
        {
            var info = new Native.SHFILEINFO();
            Native.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), Native.SHGFI_ICON | Native.SHGFI_LARGEICON);
            if (info.hIcon != IntPtr.Zero)
            {
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { Native.DestroyIcon(info.hIcon); }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Converte um HBITMAP 32 bpp mantendo a transparência (CreateBitmapSourceFromHBitmap perde o alfa).</summary>
    private static BitmapSource? FromHBitmap(IntPtr hbmp)
    {
        int dsSize = Marshal.SizeOf<Native.DIBSECTION>();
        if (Native.GetObject(hbmp, dsSize, out var ds) == dsSize && ds.dsBm.bmBits != IntPtr.Zero && ds.dsBm.bmBitsPixel == 32)
        {
            int w = ds.dsBm.bmWidth, h = Math.Abs(ds.dsBm.bmHeight), stride = ds.dsBm.bmWidthBytes;
            var raw = new byte[stride * h];
            Marshal.Copy(ds.dsBm.bmBits, raw, 0, raw.Length);

            byte[] pixels;
            if (ds.dsBmih.biHeight > 0) // bottom-up: inverte as linhas
            {
                pixels = new byte[raw.Length];
                for (int y = 0; y < h; y++)
                    Buffer.BlockCopy(raw, y * stride, pixels, (h - 1 - y) * stride, stride);
            }
            else pixels = raw;

            bool hasAlpha = false;
            for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) { hasAlpha = true; break; }
            if (!hasAlpha) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;

            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            bmp.Freeze();
            return bmp;
        }

        var fallback = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        fallback.Freeze();
        return fallback;
    }

    // ---------- Sites ----------

    private static async Task<ImageSource?> LoadFaviconAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        string host = uri.Host.ToLowerInvariant();
        string cacheFile = Path.Combine(ConfigStore.IconCacheDir, host + ".img");

        if (File.Exists(cacheFile))
        {
            var cached = Decode(await File.ReadAllBytesAsync(cacheFile));
            if (cached != null) return cached;
        }

        // Busca direto no próprio site (sem serviços de terceiros).
        string root = $"{uri.Scheme}://{uri.Authority}";
        foreach (var candidate in new[] { "/apple-touch-icon.png", "/favicon.ico" })
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(root + candidate);
                var img = Decode(bytes);
                if (img != null)
                {
                    Directory.CreateDirectory(ConfigStore.IconCacheDir);
                    await File.WriteAllBytesAsync(cacheFile, bytes);
                    return img;
                }
            }
            catch { }
        }
        return null;
    }

    private static ImageSource? Decode(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapFrame? best = null;
            foreach (var f in dec.Frames)
                if (best == null || f.PixelWidth > best.PixelWidth) best = f;
            if (best == null || best.PixelWidth < 8) return null;
            best.Freeze();
            return best;
        }
        catch { return null; }
    }

    // ---------- Ícone provisório ----------

    private static readonly Color[] Palette =
    {
        Color.FromRgb(0x4F, 0x7C, 0xF5), Color.FromRgb(0x9B, 0x5D, 0xE5), Color.FromRgb(0xE5, 0x5D, 0x8B),
        Color.FromRgb(0xF0, 0x8A, 0x3C), Color.FromRgb(0x2F, 0xB3, 0x8A), Color.FromRgb(0x3A, 0xA6, 0xD8),
    };

    public static ImageSource LetterTile(string name)
    {
        string letter = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        int hash = 0;
        foreach (char c in name) hash = hash * 31 + c;
        var color = Palette[Math.Abs(hash % Palette.Length)];

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(4, 4, Size - 8, Size - 8), 22, 22);
            var ft = new FormattedText(letter, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                50, Brushes.White, 1.0);
            dc.DrawText(ft, new Point((Size - ft.Width) / 2, (Size - ft.Height) / 2));
        }
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
