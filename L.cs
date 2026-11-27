using System.Globalization;

namespace Popfold;

public static class AppInfo
{
    public const string Name = "Popfold";
}

/// <summary>Textos da interface em português e inglês.</summary>
public static class L
{
    public static bool IsPt { get; private set; }

    /// <summary>"auto" (idioma do Windows), "pt" ou "en".</summary>
    public static void Apply(string? setting)
    {
        IsPt = setting switch
        {
            "pt" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt",
        };
    }

    private static string T(string pt, string en) => IsPt ? pt : en;

    // ---- Menu da pasta ----
    public static string Open => T("Abrir", "Open");
    public static string RenameFolder => T("Renomear pasta", "Rename folder");
    public static string NewFolder => T("Nova pasta", "New folder");
    public static string DeleteFolder => T("Excluir esta pasta…", "Delete this folder…");
    public static string DeleteSelected(int n) => T($"Excluir {n} pastas selecionadas…", $"Delete {n} selected folders…");
    public static string Options => T("Opções…", "Options…");
    public static string PinToTaskbar => T("Fixar na barra de tarefas…", "Pin to taskbar…");
    public static string OnlyOnTaskbar => T("Deixar só na barra de tarefas", "Keep only on the taskbar");
    public static string ShowOnDesktop => T("Mostrar na área de trabalho", "Show on desktop");
    public static string HiddenTipTitle => T("Agora só na barra de tarefas", "Now only on the taskbar");
    public static string HiddenTipText(string name) => T(
        $"Para trazer \"{name}\" de volta: ícone do Popfold aqui na bandeja → Mostrar na área de trabalho.",
        $"To bring \"{name}\" back: Popfold icon here in the tray → Show on desktop.");
    public static string PinTipTitle => T("Quase lá!", "Almost there!");
    public static string PinTipText(string name) => T(
        $"Clique com o botão direito em \"{name}\" → Mostrar mais opções → Fixar na barra de tarefas. Também dá para achar \"{name}\" no menu Iniciar.",
        $"Right-click \"{name}\" → Show more options → Pin to taskbar. You can also find \"{name}\" in the Start menu.");
    public static string TaskbarLinkDescription(string name) => T($"Abrir a Popfold {name}", $"Open the {name} Popfold");
    public static string PackagedWarning => T(
        "O Popfold foi aberto de dentro de outro aplicativo, e o Windows guardaria as suas pastas num lugar isolado daquele aplicativo.\n\nAbra o Popfold pelo menu Iniciar, pelo Explorador de Arquivos ou por um terminal do Windows.",
        "Popfold was opened from inside another app, and Windows would store your folders in that app's isolated storage.\n\nOpen Popfold from the Start menu, File Explorer or a Windows terminal.");
    public static string FolderGoneTitle => T("Popfold não encontrada", "Popfold not found");
    public static string FolderGoneText => T("Esta Popfold foi excluída. Clique com o botão direito no ícone da barra de tarefas → Desafixar.", "This Popfold was deleted. Right-click its taskbar icon → Unpin.");
    public static string QuitApp => T($"Sair do {AppInfo.Name}", $"Quit {AppInfo.Name}");

    public static string ConfirmDeleteOne(string name) => T(
        $"Excluir a pasta \"{name}\"?\n\nOs programas e arquivos não serão apagados — só o grupo e os atalhos guardados nele.",
        $"Delete the folder \"{name}\"?\n\nYour programs and files won't be deleted — only the group and the shortcuts stored in it.");

    public static string ConfirmDeleteMany(int n) => T(
        $"Excluir {n} pastas?\n\nOs programas e arquivos não serão apagados — só os grupos e os atalhos guardados neles.",
        $"Delete {n} folders?\n\nYour programs and files won't be deleted — only the groups and the shortcuts stored in them.");

    // ---- Painel ----
    public static string EmptyFolder => T(
        "Pasta vazia. Arraste programas, atalhos, arquivos ou links até o ícone da pasta.",
        "This folder is empty. Drag programs, shortcuts, files or links onto the folder icon.");
    public static string RunAsAdmin => T("Executar como administrador", "Run as administrator");
    public static string OpenFileLocation => T("Abrir local do arquivo", "Open file location");
    public static string RenameItem => T("Renomear…", "Rename…");
    public static string RemoveFromFolder => T("Remover da pasta", "Remove from folder");
    public static string RenameTitle => T("Renomear", "Rename");
    public static string ItemNamePrompt => T("Nome do item:", "Item name:");
    public static string Ok => "OK";
    public static string Cancel => T("Cancelar", "Cancel");

    public static string CannotOpen(string name) => T($"Não foi possível abrir \"{name}\".", $"Couldn't open \"{name}\".");
    public static string UnexpectedError => T("Ocorreu um erro inesperado:", "An unexpected error occurred:");
    public static string FirstRunTitle => T($"{AppInfo.Name} está rodando", $"{AppInfo.Name} is running");
    public static string FirstRunText => T(
        "Clique numa pasta para abrir. Arraste atalhos para dentro dela. Clique com o botão direito para mais opções.",
        "Click a folder to open it. Drag shortcuts onto it. Right-click for more options.");

    // ---- Bandeja ----
    public static string StartWithWindows => T("Iniciar com o Windows", "Start with Windows");
    public static string OpenDataFolder => T("Abrir pasta de dados", "Open data folder");
    public static string Quit => T("Sair", "Quit");
    public static string CheckForUpdates => T("Procurar atualizações", "Check for updates");
    public static string UpdateMenu(string v) => T($"Baixar a versão {v}…", $"Download version {v}…");

    // ---- Atualizações ----
    public static string UpdateTitle(string v) => T($"Popfold {v} disponível", $"Popfold {v} is available");
    public static string UpdateText => T(
        "Clique aqui para abrir a página de download. Suas pastas são mantidas ao instalar por cima.",
        "Click here to open the download page. Your folders are kept when you install over it.");
    public static string UpToDateTitle => T("Tudo em dia", "You're up to date");
    public static string UpToDateText(string v) => T($"Você já tem a versão mais nova ({v}).", $"You have the latest version ({v}).");
    public static string UpdateFailedTitle => T("Não deu para verificar", "Couldn't check for updates");
    public static string UpdateFailedText => T("Confira a conexão com a internet e tente de novo mais tarde.", "Check your internet connection and try again later.");
    public static string AutoUpdateCheck => T("Avisar sobre versões novas", "Tell me about new versions");
    public static string AutoUpdateHint => T("Uma vez por dia, pergunta ao GitHub se saiu versão nova", "Once a day, asks GitHub if there's a new version");
    public static string NewVersionLink(string v) => T($"Versão {v} disponível", $"Version {v} available");

    // ---- Opções ----
    public static string OptionsTitle => T($"Opções — {AppInfo.Name}", $"Options — {AppInfo.Name}");
    public static string Appearance => T("Aparência", "Appearance");
    public static string FolderTransparency => T("Transparência da pasta", "Folder transparency");
    public static string GlassEffect => T("Efeito vidro", "Glass effect");
    public static string GlassHint => T("Desfoca o papel de parede atrás da pasta", "Blurs the wallpaper behind the folder");
    public static string StartHint => T("Abre o Popfold quando você liga o PC", "Opens Popfold when you turn on your PC");
    public static string Tagline => T("Seus apps, num pop.", "Your apps, in a pop.");
    public static string Auto => T("Automático", "Automatic");
    public static string MoreTransparent => T("Mais transparente", "More see-through");
    public static string MoreSolid => T("Mais sólida", "More solid");
    public static string Style => T("Estilo", "Style");
    public static string StyleLight => T("Claro", "Light");
    public static string StyleDark => T("Escuro", "Dark");
    public static string General => T("Geral", "General");
    public static string Language => T("Idioma", "Language");
    public static string LanguageAuto => T("Automático (idioma do Windows)", "Automatic (Windows language)");
    public static string Close => T("Fechar", "Close");
    public static string SelectionTip => T(
        "Dica: Ctrl+clique nas pastas ou arraste uma seleção na área de trabalho para selecionar várias e movê-las juntas.",
        "Tip: Ctrl+click folders or drag a selection box on the desktop to select several and move them together.");

    // ---- Conteúdo inicial ----
    public static string DefWork => T("Trabalho", "Work");
    public static string DefGames => T("Jogos", "Games");
    public static string DefAI => T("IA", "AI");
    public static string DefUtilities => T("Utilitários", "Utilities");
    public static string Documents => T("Documentos", "Documents");
    public static string Downloads => "Downloads";
    public static string Notepad => T("Bloco de Notas", "Notepad");
    public static string Calculator => T("Calculadora", "Calculator");
    public static string TaskManager => T("Gerenciador de Tarefas", "Task Manager");
    public static string ControlPanel => T("Painel de Controle", "Control Panel");
    public static string CommandPrompt => T("Prompt de Comando", "Command Prompt");
}
