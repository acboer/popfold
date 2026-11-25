using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Popfold;

/// <summary>
/// Sabe qual papel de parede está atrás de cada ponto da tela, para desenhar o efeito vidro
/// (o pedaço do papel de parede atrás da pasta, desfocado).
/// </summary>
public static class Wallpaper
{
    private enum Position { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }

    private sealed class Monitor
    {
        public Rect Bounds;          // pixels físicos
        public BitmapSource? Image;  // versão reduzida (vai ser desfocada mesmo)
        public double PixelW, PixelH; // tamanho original da imagem
    }

    private static readonly List<Monitor> Monitors = new();
    private static Position _position = Position.Fill;
    private static Rect _virtual;

    public static void Reload()
    {
        Monitors.Clear();
        try
        {
            var dw = (IDesktopWallpaper)new DesktopWallpaperClass();
            try
            {
                _position = (Position)dw.GetPosition();
                var cache = new Dictionary<string, (BitmapSource? img, double w, double h)>(StringComparer.OrdinalIgnoreCase);
                uint count = dw.GetMonitorDevicePathCount();
                for (uint i = 0; i < count; i++)
                {
                    string id = dw.GetMonitorDevicePathAt(i);
                    var r = dw.GetMonitorRECT(id);
                    if (r.Right <= r.Left) continue; // monitor desligado
                    string path = "";
                    try { path = dw.GetWallpaper(id); } catch { }
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) path = FallbackPath() ?? "";

                    var m = new Monitor { Bounds = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top) };
                    if (path.Length > 0)
                    {
                        if (!cache.TryGetValue(path, out var loaded))
                            cache[path] = loaded = Load(path);
                        (m.Image, m.PixelW, m.PixelH) = loaded;
                    }
                    Monitors.Add(m);
                }
            }
            finally { Marshal.ReleaseComObject(dw); }
        }
        catch { /* sem efeito vidro */ }

        _virtual = Rect.Empty;
        foreach (var m in Monitors) _virtual.Union(m.Bounds);
    }

    /// <summary>Para um retângulo da tela (pixels físicos), devolve a imagem e o recorte relativo (0..1) dela.</summary>
    public static (ImageSource Image, Rect Viewbox)? Map(Rect r)
    {
        if (Monitors.Count == 0) return null;
        var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        var mon = Monitors.FirstOrDefault(m => m.Bounds.Contains(center)) ?? Monitors[0];
        if (mon.Image == null || mon.PixelW <= 0 || mon.PixelH <= 0) return null;

        Rect area = _position == Position.Span && !_virtual.IsEmpty ? _virtual : mon.Bounds;
        double iw = mon.PixelW, ih = mon.PixelH, sx, sy;
        switch (_position)
        {
            case Position.Stretch:
                sx = area.Width / iw; sy = area.Height / ih; break;
            case Position.Fit:
                sx = sy = Math.Min(area.Width / iw, area.Height / ih); break;
            case Position.Center:
                sx = sy = 1; break;
            default: // Fill, Span, Tile (aproximado)
                sx = sy = Math.Max(area.Width / iw, area.Height / ih); break;
        }
        double ox = area.X + (area.Width - iw * sx) / 2;
        double oy = area.Y + (area.Height - ih * sy) / 2;

        var vb = new Rect((r.X - ox) / sx / iw, (r.Y - oy) / sy / ih, r.Width / sx / iw, r.Height / sy / ih);
        return (mon.Image, vb);
    }

    private static (BitmapSource?, double, double) Load(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            double w, h;
            using (var ms = new MemoryStream(bytes))
            {
                var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                w = dec.Frames[0].PixelWidth;
                h = dec.Frames[0].PixelHeight;
            }
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.StreamSource = new MemoryStream(bytes);
            if (w > 1280) bi.DecodePixelWidth = 1280;
            bi.EndInit();
            bi.Freeze();
            return (bi, w, h);
        }
        catch { return (null, 0, 0); }
    }

    private static string? FallbackPath()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (k?.GetValue("WallPaper") is string p && File.Exists(p)) return p;
        }
        catch { }
        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Themes\TranscodedWallpaper");
        return File.Exists(transcoded) ? transcoded : null;
    }

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        Native.RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
    }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass { }
}
