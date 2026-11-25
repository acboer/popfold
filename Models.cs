using System;
using System.Collections.Generic;

namespace Popfold;

public enum ItemKind
{
    File, // programa, atalho, arquivo ou pasta
    Url,  // site
}

public sealed class FolderItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public ItemKind Kind { get; set; } = ItemKind.File;
    public string? Arguments { get; set; }
}

public sealed class FolderGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    // Posição na área de trabalho (DIPs). null = ainda não posicionada.
    public double? X { get; set; }
    public double? Y { get; set; }

    public List<FolderItem> Items { get; set; } = new();

    /// <summary>Tem um atalho para fixar na barra de tarefas (menu Iniciar > Popfold).</summary>
    public bool Taskbar { get; set; }

    /// <summary>Aparece na área de trabalho. False = só na barra de tarefas.</summary>
    public bool OnDesktop { get; set; } = true;
}

public sealed class AppSettings
{
    /// <summary>0 = mais opaco, 100 = mais transparente. Mesmo em 0 o papel de parede aparece um pouco.</summary>
    public int Transparency { get; set; } = 60;

    /// <summary>Desfoca o papel de parede atrás da pasta (vidro fosco).</summary>
    public bool Glass { get; set; } = true;

    /// <summary>"light" ou "dark".</summary>
    public string Style { get; set; } = "light";

    /// <summary>"auto", "pt" ou "en".</summary>
    public string Language { get; set; } = "auto";
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public AppSettings Settings { get; set; } = new();
    public List<FolderGroup> Folders { get; set; } = new();
}
