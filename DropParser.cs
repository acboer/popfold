using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace Popfold;

/// <summary>Transforma o que foi arrastado (arquivos, atalhos, links do navegador) em itens.</summary>
public static class DropParser
{
    private static readonly string[] UrlFormats = { "UniformResourceLocatorW", "UniformResourceLocator" };

    public static bool CanAccept(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.FileDrop)) return true;
        return ReadUrl(data) != null;
    }

    public static List<FolderItem> Parse(IDataObject data)
    {
        var list = new List<FolderItem>();
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var f in files)
                if (FromPath(f) is FolderItem item) list.Add(item);
        }
        else if (ReadUrl(data) is Uri url)
        {
            list.Add(new FolderItem { Name = NameFromUrl(url), Path = url.AbsoluteUri, Kind = ItemKind.Url });
        }
        return list;
    }

    private static FolderItem? FromPath(string path)
    {
        if (Directory.Exists(path))
        {
            var name = new DirectoryInfo(path).Name;
            return new FolderItem { Name = string.IsNullOrEmpty(name) ? path : name, Path = path };
        }
        if (!File.Exists(path)) return null;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        string display = Path.GetFileNameWithoutExtension(path);

        // Atalhos são copiados para dentro do app: o original pode sair da área de trabalho.
        if (ext is ".lnk" or ".url" or ".appref-ms")
            return new FolderItem { Name = display, Path = ConfigStore.StoreShortcut(path) };

        if (ext == ".exe")
        {
            try
            {
                var desc = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                if (!string.IsNullOrEmpty(desc) && desc.Length <= 40) display = desc;
            }
            catch { }
        }
        return new FolderItem { Name = display, Path = path };
    }

    private static Uri? ReadUrl(IDataObject data)
    {
        foreach (var fmt in UrlFormats)
        {
            try
            {
                if (!data.GetDataPresent(fmt)) continue;
                if (data.GetData(fmt) is MemoryStream ms)
                {
                    var bytes = ms.ToArray();
                    var text = fmt.EndsWith("W") ? Encoding.Unicode.GetString(bytes) : Encoding.Default.GetString(bytes);
                    if (TryUrl(text, out var u)) return u;
                }
            }
            catch { }
        }
        try
        {
            if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string s)
            {
                var first = s.Split('\n')[0];
                if (TryUrl(first, out var u)) return u;
            }
        }
        catch { }
        return null;
    }

    private static bool TryUrl(string? text, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Uri.TryCreate(text.Trim().TrimEnd('\0'), UriKind.Absolute, out uri!)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string NameFromUrl(Uri url)
    {
        var labels = url.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string core = labels.Length >= 2 ? labels[^2] : url.Host;
        if (core.Length == 0) return url.Host;
        return char.ToUpper(core[0], CultureInfo.CurrentCulture) + core[1..];
    }
}
