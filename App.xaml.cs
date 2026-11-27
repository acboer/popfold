using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Popfold;

public partial class App : Application
{
    private Mutex? _mutex;
    private TrayIcon? _tray;
    private DesktopSelection? _selection;
    private SettingsWindow? _settings;
    private DispatcherTimer? _wallpaperTimer;
    private DesktopWatcher? _desktopWatcher;
    private bool _exiting;
    private readonly Dictionary<string, FolderWindow> _windows = new();

    public AppConfig Config { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Item da lista de atalhos da barra de tarefas ("--run idDaPasta índice"): só abre o item e sai.
        if (e.Args.Length >= 3 && e.Args[0].Equals("--run", StringComparison.OrdinalIgnoreCase))
        {
            RunFromJumpList(e.Args[1], e.Args[2]);
            Shutdown();
            return;
        }
        string? openId = e.Args.Length >= 2 && e.Args[0].Equals("--open", StringComparison.OrdinalIgnoreCase) ? e.Args[1] : null;

        bool newFolderRequested = e.Args.Any(a => a.Equals("--new", StringComparison.OrdinalIgnoreCase));
        DiscardPopfoldFiles(e.Args);

        _mutex = new Mutex(true, @"Local\Popfold.SingleInstance", out bool created);
        if (!created)
        {
            // Já está rodando: "Novo > Popfold" pede à instância aberta para criar a pasta.
            if (newFolderRequested) ShellIntegration.SignalNewFolder();
            if (openId != null) ShellIntegration.SignalOpenFolder(openId);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        ConfigStore.MigrateFromOldName();
        StartupManager.MigrateFromOldName();

        L.Apply("auto");
        Config = ConfigStore.Load(out bool firstRun);
        L.Apply(Config.Settings.Language);

        Wallpaper.Reload();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        EnsurePositions();
        foreach (var g in Config.Folders.ToList()) ShowFolder(g);
        SaveConfig();

        _tray = new TrayIcon(this);
        _selection = new DesktopSelection(Dispatcher, OnDesktopDragSelect, OnDeleteKey,
            hwnd => _windows.Values.Any(w => w.Handle == hwnd));

        try { ShellIntegration.Register(); } catch (Exception ex) { Log(ex); }
        ShellIntegration.ListenForNewFolder(() => Dispatcher.BeginInvoke(AddFolderFromShell));
        _desktopWatcher = new DesktopWatcher(() => Dispatcher.BeginInvoke(AddFolderFromShell));
        ShellIntegration.ListenForOpenFolder(id => Dispatcher.BeginInvoke(() => OpenFromTaskbar(id)));

        if (firstRun) _tray.ShowTip(L.FirstRunTitle, L.FirstRunText);
        if (newFolderRequested) AddFolderFromShell();
        if (openId != null) Dispatcher.BeginInvoke(() => OpenFromTaskbar(openId), DispatcherPriority.ApplicationIdle);

        // Atualiza os atalhos da barra de tarefas (o caminho do programa ou os ícones podem ter mudado).
        foreach (var g in Config.Folders.Where(g => g.Taskbar)) ScheduleTaskbarUpdate(g, TimeSpan.FromSeconds(3));

        StartUpdateTimer();
    }

    // ---------- Barra de tarefas ----------

    private readonly Dictionary<string, DispatcherTimer> _taskbarTimers = new();

    /// <summary>"Fixar na barra de tarefas…": cria o atalho e mostra onde ele está para o usuário fixar.</summary>
    public async void PinToTaskbar(FolderGroup g)
    {
        try
        {
            g.Taskbar = true;
            SaveConfig();
            string lnk = await TaskbarPins.UpdateAsync(g);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{lnk}\"");
            _tray?.ShowTip(L.PinTipTitle, L.PinTipText(g.Name));
        }
        catch (Exception ex) { Log(ex); }
    }

    /// <summary>Mostra a pasta na área de trabalho ou deixa só na barra de tarefas.</summary>
    public void SetOnDesktop(FolderGroup g, bool onDesktop)
    {
        g.OnDesktop = onDesktop;
        SaveConfig();
        if (!_windows.TryGetValue(g.Id, out var w)) return;
        if (onDesktop)
        {
            w.Show();
            w.SendToBottom();
        }
        else
        {
            w.IsSelected = false;
            w.HideFromDesktop();
            _tray?.ShowTip(L.HiddenTipTitle, L.HiddenTipText(g.Name));
        }
    }

    /// <summary>Pastas que estão só na barra de tarefas (para o menu da bandeja trazê-las de volta).</summary>
    public IEnumerable<FolderGroup> TaskbarOnlyFolders => Config.Folders.Where(g => !g.OnDesktop);

    /// <summary>Atualiza ícone/nome/lista de atalhos depois de uma mudança (com atraso, para juntar várias).</summary>
    public void ScheduleTaskbarUpdate(FolderGroup g) => ScheduleTaskbarUpdate(g, TimeSpan.FromSeconds(1));

    private void ScheduleTaskbarUpdate(FolderGroup g, TimeSpan delay)
    {
        if (!g.Taskbar) return;
        if (_taskbarTimers.TryGetValue(g.Id, out var old)) old.Stop();
        var t = new DispatcherTimer { Interval = delay };
        _taskbarTimers[g.Id] = t;
        t.Tick += async (_, _) =>
        {
            t.Stop();
            _taskbarTimers.Remove(g.Id);
            if (!Config.Folders.Contains(g)) return;
            try { await TaskbarPins.UpdateAsync(g); }
            catch (Exception ex) { Log(ex); }
        };
        t.Start();
    }

    /// <summary>Clique na Popfold fixada: abre o painel logo acima do ícone (onde está o mouse).</summary>
    private void OpenFromTaskbar(string id)
    {
        if (!_windows.TryGetValue(id, out var w))
        {
            _tray?.ShowTip(L.FolderGoneTitle, L.FolderGoneText);
            return;
        }
        Native.GetCursorPos(out var p);
        w.OpenPanelFromTaskbar(p);
    }

    /// <summary>"Popfold.exe --new arquivo.popfold" (um .popfold criado fora da área de trabalho): descarta o arquivo.</summary>
    internal static void DiscardPopfoldFiles(string[] args)
    {
        foreach (var arg in args)
            if (arg.EndsWith(".popfold", StringComparison.OrdinalIgnoreCase))
                try { File.Delete(arg); } catch { }
    }

    internal static void RunFromJumpList(string id, string index)
    {
        try
        {
            var cfg = ConfigStore.Load(out _);
            var g = cfg.Folders.FirstOrDefault(f => f.Id == id);
            if (g != null && int.TryParse(index, out int i) && i >= 0 && i < g.Items.Count)
                ShellLauncher.Launch(g.Items[i]);
        }
        catch (Exception ex) { Log(ex); }
    }

    /// <summary>"Novo > Popfold" no menu da área de trabalho: cria a pasta onde foi o clique direito.</summary>
    private void AddFolderFromShell()
    {
        Point? at = null;
        if (_selection?.LastDesktopRightClick is { } rc && (DateTime.Now - rc.When).TotalSeconds < 60)
        {
            double scale = Native.GetDpiForSystem() / 96.0;
            at = new Point(rc.Point.X / scale - FolderWindow.CellWidth / 2,
                           rc.Point.Y / scale - FolderWindow.BoxCenterY);
        }
        AddFolder(at);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _selection?.Dispose();
        _tray?.Dispose();
        ShellIntegration.StopListening();
        _desktopWatcher?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }

    public void SaveConfig()
    {
        try { ConfigStore.Save(Config); }
        catch (Exception ex) { Log(ex); }
    }

    // ---------- Aparência / idioma ----------

    public void ApplyAppearance()
    {
        foreach (var w in _windows.Values) w.ApplyAppearance();
    }

    public void SetLanguage(string language)
    {
        Config.Settings.Language = language;
        L.Apply(language);
        SaveConfig();
    }

    // ---------- Atualizações ----------

    private DispatcherTimer? _updateTimer;
    private bool _checkingUpdate;

    /// <summary>Versão nova encontrada no GitHub nesta sessão (null = nenhuma, ou ainda não verificou).</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    public event Action? UpdateStateChanged;

    /// <summary>Primeira verificação 1 min depois de abrir; depois confere a cada hora se já passou um dia.</summary>
    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(1);
            var s = Config.Settings;
            if (!s.CheckUpdates) return;
            if (s.LastUpdateCheck is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(23)) return;
            _ = CheckForUpdatesAsync(manual: false);
        };
        _updateTimer.Start();
    }

    /// <summary>Consulta o GitHub. Automática: avisa uma vez por versão e fica quieta se falhar. Manual: sempre responde.</summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;
        try
        {
            UpdateInfo? info;
            try { info = await UpdateChecker.CheckAsync(); }
            catch (Exception ex)
            {
                if (manual) _tray?.ShowTip(L.UpdateFailedTitle, L.UpdateFailedText);
                else Log(ex);
                return;
            }

            var s = Config.Settings;
            s.LastUpdateCheck = DateTime.UtcNow;
            AvailableUpdate = info;
            UpdateStateChanged?.Invoke();

            if (info == null)
            {
                if (manual) _tray?.ShowTip(L.UpToDateTitle, L.UpToDateText(UpdateChecker.CurrentText));
            }
            else if (manual || s.NotifiedVersion != info.Version.ToString(3))
            {
                s.NotifiedVersion = info.Version.ToString(3);
                _tray?.ShowTip(L.UpdateTitle(info.Version.ToString(3)), L.UpdateText, OpenUpdatePage);
            }
            SaveConfig();
        }
        finally { _checkingUpdate = false; }
    }

    public void OpenUpdatePage()
    {
        string url = AvailableUpdate?.PageUrl ?? UpdateChecker.ReleasesPage;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log(ex); }
    }

    public void ShowSettings()
    {
        if (_settings != null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(this);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(ScheduleWallpaperReload);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ScheduleWallpaperReload);

    /// <summary>Recarrega o papel de parede com um pequeno atraso (o Windows avisa antes de gravar o arquivo).</summary>
    private void ScheduleWallpaperReload()
    {
        _wallpaperTimer?.Stop();
        _wallpaperTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _wallpaperTimer.Tick += (_, _) =>
        {
            _wallpaperTimer.Stop();
            Wallpaper.Reload();
            ApplyAppearance();
        };
        _wallpaperTimer.Start();
    }

    // ---------- Seleção ----------

    public IEnumerable<FolderWindow> SelectedWindows => _windows.Values.Where(w => w.IsSelected);

    public void ClearSelection()
    {
        foreach (var w in _windows.Values) w.IsSelected = false;
    }

    /// <summary>Arrasto (ou clique) que começou na área de trabalho. Pontos em pixels físicos.</summary>
    private void OnDesktopDragSelect(Native.POINT down, Native.POINT up, bool ctrl)
    {
        bool isDrag = Math.Abs(up.X - down.X) > 4 || Math.Abs(up.Y - down.Y) > 4;
        if (!isDrag)
        {
            if (!ctrl) ClearSelection(); // clique no vazio limpa a seleção
            return;
        }
        var box = new Rect(new Point(down.X, down.Y), new Point(up.X, up.Y));
        foreach (var w in _windows.Values.Where(w => w.IsVisible)) // as que estão só na barra de tarefas ficam de fora
        {
            if (w.PhysicalBounds.IntersectsWith(box)) w.IsSelected = true;
            else if (!ctrl) w.IsSelected = false;
        }
    }

    // ---------- Pastas ----------

    private FolderWindow ShowFolder(FolderGroup g)
    {
        var w = new FolderWindow(this, g);
        _windows[g.Id] = w;
        w.Closed += (_, _) =>
        {
            if (_windows.TryGetValue(g.Id, out var cur) && cur == w) _windows.Remove(g.Id);
            // Se o Explorer reiniciar, as janelas presas à área de trabalho são destruídas: recria.
            if (!_exiting && !w.ClosedIntentionally && Config.Folders.Contains(g))
                ScheduleRecreate(g);
        };
        if (g.OnDesktop) w.Show(); // as que estão só na barra de tarefas não aparecem
        return w;
    }

    private void ScheduleRecreate(FolderGroup g)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        t.Tick += (_, _) =>
        {
            if (_exiting || !Config.Folders.Contains(g) || _windows.ContainsKey(g.Id)) { t.Stop(); return; }
            if (Native.FindWindow("Progman", null) == IntPtr.Zero) return; // Explorer ainda não voltou
            t.Stop();
            ShowFolder(g);
        };
        t.Start();
    }

    public void AddFolder() => AddFolder(null);

    public void AddFolder(Point? at)
    {
        var g = new FolderGroup { Name = L.NewFolder };
        var p = at ?? NextFreeSlot();
        g.X = p.X; g.Y = p.Y;
        Config.Folders.Add(g);
        SaveConfig();
        ShowFolder(g).OpenPanel(rename: true);
    }

    /// <summary>Delete / Shift+Delete com a área de trabalho em foco: exclui as Popfolds selecionadas.</summary>
    private void OnDeleteKey(bool shift)
    {
        if (_deleting) return; // tecla segurada repete o evento
        var groups = SelectedWindows.Select(w => w.Group).ToList();
        if (groups.Count > 0) DeleteFolders(groups);
    }

    private bool _deleting;

    /// <summary>
    /// Pergunta Sim/Não na frente de tudo. Necessário porque o pedido pode vir de uma tecla ou clique
    /// que foi para o Explorer; sem isso o Windows deixaria a caixa atrás das outras janelas.
    /// </summary>
    private static bool Confirm(string message)
    {
        var owner = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            Width = 1, Height = 1, Left = -100, Top = -100,
        };
        try
        {
            owner.Show();
            Native.ForceForeground(new System.Windows.Interop.WindowInteropHelper(owner).Handle);
            return MessageBox.Show(owner, message, AppInfo.Name, MessageBoxButton.YesNo,
                MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        finally { owner.Close(); }
    }

    public void DeleteFolders(List<FolderGroup> groups)
    {
        if (groups.Count == 0 || _deleting) return;
        string msg = groups.Count == 1 ? L.ConfirmDeleteOne(groups[0].Name) : L.ConfirmDeleteMany(groups.Count);
        _deleting = true;
        bool ok;
        try { ok = Confirm(msg); }
        finally { _deleting = false; }
        if (!ok) return;

        foreach (var g in groups)
        {
            foreach (var item in g.Items) ConfigStore.DeleteIfStored(item.Path);
            if (g.Taskbar) try { TaskbarPins.Remove(g); } catch (Exception ex) { Log(ex); }
            Config.Folders.Remove(g);
            if (_windows.TryGetValue(g.Id, out var w)) w.CloseIntentionally();
        }
        SaveConfig();
    }

    public void AddItems(FolderGroup g, IEnumerable<FolderItem> items)
    {
        bool changed = false;
        foreach (var item in items)
        {
            if (g.Items.Any(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
            {
                ConfigStore.DeleteIfStored(item.Path);
                continue;
            }
            g.Items.Add(item);
            changed = true;
        }
        if (changed) { SaveConfig(); NotifyChanged(g); }
    }

    public void RemoveItem(FolderGroup g, FolderItem item)
    {
        g.Items.Remove(item);
        ConfigStore.DeleteIfStored(item.Path);
        SaveConfig();
        NotifyChanged(g);
    }

    /// <summary>
    /// Move um item de uma pasta para outra (arrastado do painel). O atalho guardado em shortcuts\ continua o mesmo,
    /// então nada é apagado. Se o destino já tem o mesmo caminho, o item só sai da origem.
    /// </summary>
    public bool MoveItem(FolderGroup from, FolderGroup to, FolderItem item)
    {
        if (from == to || !from.Items.Contains(item)) return false;
        from.Items.Remove(item);
        if (!to.Items.Any(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
            to.Items.Add(item);
        SaveConfig();
        NotifyChanged(from);
        NotifyChanged(to);
        return true;
    }

    public void NotifyChanged(FolderGroup g)
    {
        if (_windows.TryGetValue(g.Id, out var w)) w.OnGroupChanged();
        ScheduleTaskbarUpdate(g);
    }

    public void ExitApp()
    {
        _exiting = true;
        SaveConfig();
        foreach (var w in _windows.Values.ToList()) w.CloseIntentionally();
        _settings?.Close();
        _selection?.Dispose();
        _selection = null;
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    // ---------- Posicionamento ----------

    private void EnsurePositions()
    {
        var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                          SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        foreach (var g in Config.Folders)
        {
            if (g.X is double x && g.Y is double y && vs.Contains(new Point(x + 40, y + 40))) continue;
            g.X = null; g.Y = null;
        }
        foreach (var g in Config.Folders.Where(g => g.X == null))
        {
            var p = NextFreeSlot();
            g.X = p.X; g.Y = p.Y;
        }
    }

    /// <summary>Próxima célula livre numa coluna junto à borda direita da tela principal.</summary>
    private Point NextFreeSlot()
    {
        var wa = SystemParameters.WorkArea;
        int perCol = Math.Max(1, (int)((wa.Height - 40) / FolderWindow.CellHeight));
        for (int i = 0; i < 200; i++)
        {
            int col = i / perCol, row = i % perCol;
            var p = new Point(wa.Right - 24 - FolderWindow.CellWidth * (col + 1), wa.Top + 24 + row * FolderWindow.CellHeight);
            bool taken = Config.Folders.Any(g => g.X is double x && g.Y is double y &&
                                                 Math.Abs(x - p.X) < 50 && Math.Abs(y - p.Y) < 60);
            if (!taken) return p;
        }
        return new Point(wa.Left + 40, wa.Top + 40);
    }

    // ---------- Erros ----------

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        MessageBox.Show(L.UnexpectedError + "\n\n" + e.Exception.Message, AppInfo.Name,
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.DataDir);
            File.AppendAllText(Path.Combine(ConfigStore.DataDir, "error.log"), $"[{DateTime.Now:s}] {ex}\n\n");
        }
        catch { }
    }
}
