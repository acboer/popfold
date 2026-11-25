using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Popfold;

/// <summary>Lê e grava a configuração em %AppData%\Popfold\config.json.</summary>
public static class ConfigStore
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Popfold");

    /// <summary>Pasta usada antes do app se chamar Popfold.</summary>
    private static string OldDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFolders");

    /// <summary>Move os dados da versão "Smart Folders" para a pasta do Popfold (uma vez só).</summary>
    public static void MigrateFromOldName()
    {
        if (Directory.Exists(DataDir) || !Directory.Exists(OldDataDir)) return;
        try { Directory.Move(OldDataDir, DataDir); } catch { }
    }

    /// <summary>Corrige caminhos de atalhos guardados que ainda apontam para a pasta antiga.</summary>
    private static bool FixMovedPaths(AppConfig cfg)
    {
        bool changed = false;
        string oldPrefix = OldDataDir + Path.DirectorySeparatorChar;
        foreach (var g in cfg.Folders)
            foreach (var item in g.Items)
                if (item.Path.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    item.Path = Path.Combine(DataDir, item.Path[oldPrefix.Length..]);
                    changed = true;
                }
        return changed;
    }

    /// <summary>Cópias dos atalhos (.lnk/.url) arrastados para dentro das pastas.</summary>
    public static string ShortcutsDir => Path.Combine(DataDir, "shortcuts");

    public static string IconCacheDir => Path.Combine(DataDir, "icons");

    private static string ConfigPath => Path.Combine(DataDir, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load(out bool isFirstRun)
    {
        isFirstRun = !File.Exists(ConfigPath);
        if (!isFirstRun)
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Options);
                if (cfg != null)
                {
                    if (FixMovedPaths(cfg)) Save(cfg);
                    return cfg;
                }
            }
            catch
            {
                // Arquivo corrompido: guarda uma cópia e recomeça com o padrão.
                try { File.Copy(ConfigPath, ConfigPath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}", true); } catch { }
            }
        }
        return CreateDefault();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(DataDir);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, ConfigPath, overwrite: true);
    }

    /// <summary>Copia um atalho para a pasta interna, para que o original possa ser apagado da área de trabalho.</summary>
    public static string StoreShortcut(string source)
    {
        Directory.CreateDirectory(ShortcutsDir);
        var dest = Path.Combine(ShortcutsDir, Guid.NewGuid().ToString("N")[..8] + "_" + Path.GetFileName(source));
        File.Copy(source, dest);
        return dest;
    }

    public static bool IsStoredShortcut(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return full.StartsWith(Path.GetFullPath(ShortcutsDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void DeleteIfStored(string path)
    {
        if (!IsStoredShortcut(path)) return;
        try { File.Delete(path); } catch { }
    }

    private static AppConfig CreateDefault()
    {
        string sys = Environment.SystemDirectory;
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var cfg = new AppConfig();

        var work = new FolderGroup { Name = L.DefWork };
        if (Directory.Exists(docs)) work.Items.Add(new FolderItem { Name = L.Documents, Path = docs });
        var downloads = Path.Combine(profile, "Downloads");
        if (Directory.Exists(downloads)) work.Items.Add(new FolderItem { Name = L.Downloads, Path = downloads });
        cfg.Folders.Add(work);

        cfg.Folders.Add(new FolderGroup { Name = L.DefGames });

        var ai = new FolderGroup { Name = L.DefAI };
        ai.Items.Add(new FolderItem { Name = "Claude", Path = "https://claude.ai", Kind = ItemKind.Url });
        cfg.Folders.Add(ai);

        var util = new FolderGroup { Name = L.DefUtilities };
        AddIfExists(util, L.Notepad, Path.Combine(sys, "notepad.exe"));
        AddIfExists(util, L.Calculator, Path.Combine(sys, "calc.exe"));
        AddIfExists(util, L.TaskManager, Path.Combine(sys, "taskmgr.exe"));
        AddIfExists(util, L.ControlPanel, Path.Combine(sys, "control.exe"));
        AddIfExists(util, L.CommandPrompt, Path.Combine(sys, "cmd.exe"));
        cfg.Folders.Add(util);

        return cfg;
    }

    private static void AddIfExists(FolderGroup g, string name, string path)
    {
        if (File.Exists(path)) g.Items.Add(new FolderItem { Name = name, Path = path });
    }
}
