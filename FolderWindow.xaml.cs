using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Popfold;

/// <summary>A pasta recolhida: ícone com prévia 2×2 e nome, presa à área de trabalho.</summary>
public partial class FolderWindow : Window
{
    public const double CellWidth = 100;
    public const double CellHeight = 124;
    private const double BoxTop = 8, BoxSize = 76, GlassMargin = 24;

    /// <summary>Distância do topo da célula até o centro do ícone (DIPs).</summary>
    public const double BoxCenterY = BoxTop + BoxSize / 2;

    private readonly App _app;
    private PanelWindow? _panel;
    private IntPtr _hwnd;
    private Point _downPos;
    private bool _pressed;
    private bool _ctrlOnDown;
    private bool _highlighted;
    private bool _selected;
    private ImageBrush? _glassBrush;
    private bool _glassOn;
    private Point _dragStart;
    private List<(FolderWindow W, double Left, double Top)>? _companions;

    private readonly DropShadowEffect _shadow = new() { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.3 };

    public FolderGroup Group { get; }
    public bool ClosedIntentionally { get; private set; }

    public bool IsSelected
    {
        get => _selected;
        set
        {
            _selected = value;
            SelectionFrame.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public FolderWindow(App app, FolderGroup group)
    {
        _app = app;
        Group = group;
        InitializeComponent();

        Width = CellWidth;
        Height = CellHeight;
        Left = group.X ?? 0;
        Top = group.Y ?? 0;

        // Dono = janela da área de trabalho (Progman): fica logo acima do papel de parede
        // e continua visível com Win+D / "Mostrar área de trabalho".
        var progman = Native.FindWindow("Progman", null);
        if (progman != IntPtr.Zero) new WindowInteropHelper(this).Owner = progman;

        Root.MouseLeftButtonDown += OnMouseDown;
        Root.MouseMove += OnMouseMove;
        Root.MouseLeftButtonUp += OnMouseUp;
        Root.MouseEnter += (_, _) => AnimateBox(1.06);
        Root.MouseLeave += (_, _) => AnimateBox(1.0);
        LocationChanged += OnLocationChanged;

        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => SetDropHighlight(false);
        Drop += OnDrop;

        Root.ContextMenu = new ContextMenu();
        Root.ContextMenuOpening += (_, _) => FillContextMenu();

        Refresh();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        // Sem botão na barra de tarefas/Alt+Tab, e clicar não "rouba" o foco.
        Native.AddExStyle(_hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        SendToBottom();
        ApplyAppearance();
    }

    private static readonly int TaskbarCreatedMsg = Native.RegisterWindowMessage("TaskbarCreated");

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // O Explorer reiniciou: a janela da área de trabalho é nova, então a pasta precisa se prender a ela de novo.
        if (msg == TaskbarCreatedMsg)
            Dispatcher.BeginInvoke(ReattachToDesktop);
        return IntPtr.Zero;
    }

    private async void ReattachToDesktop()
    {
        for (int i = 0; i < 20; i++)
        {
            var progman = Native.FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                Native.SetWindowLongPtr(_hwnd, -8 /* GWLP_HWNDPARENT */, progman);
                SendToBottom();
                return;
            }
            await System.Threading.Tasks.Task.Delay(500);
        }
    }

    public IntPtr Handle => _hwnd;

    public void SendToBottom() => Native.SendToBottom(_hwnd);

    /// <summary>Centro do ícone na tela (DIPs), usado para ancorar o painel.</summary>
    public Point BoxCenter => new(Left + ActualWidth / 2, Top + BoxTop + BoxSize / 2);

    /// <summary>Retângulo da célula em pixels físicos.</summary>
    public Rect PhysicalBounds
    {
        get
        {
            Native.GetWindowRect(_hwnd, out var r);
            return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }
    }

    // ---------- Conteúdo ----------

    public void Refresh()
    {
        Label.Text = Group.Name;
        PreviewGrid.Children.Clear();
        foreach (var item in Group.Items.Take(4))
        {
            var vm = new ItemViewModel(item);
            var img = new Image { Width = 24, Height = 24, Margin = new Thickness(2) };
            img.SetBinding(Image.SourceProperty, new Binding(nameof(ItemViewModel.Icon)) { Source = vm });
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            PreviewGrid.Children.Add(img);
        }
        EmptyGlyph.Visibility = Group.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void OnGroupChanged()
    {
        Refresh();
        _panel?.Reload();
    }

    // ---------- Aparência (transparência, vidro, estilo) ----------

    public void ApplyAppearance()
    {
        var s = _app.Config.Settings;
        _glassOn = s.Glass && UpdateGlass();
        if (!_glassOn) GlassBg.Visibility = Visibility.Collapsed;

        // Mesmo no mínimo de transparência a pasta nunca fica 100% opaca: o fundo sempre aparece um pouco.
        double t = Math.Clamp(s.Transparency, 0, 100) / 100.0;
        double alpha = _glassOn ? Lerp(0.80, 0.08, t) : Lerp(0.86, 0.10, t);
        bool dark = s.Style == "dark";
        var c = dark ? Color.FromRgb(0x1C, 0x1D, 0x22) : Colors.White;
        Tint.Background = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B));
        Tint.Effect = _glassOn ? null : _shadow;
        SetStroke();
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private void SetStroke()
    {
        bool dark = _app.Config.Settings.Style == "dark";
        byte a = _highlighted ? (byte)0xDD : dark ? (byte)0x30 : (byte)0x66;
        Stroke.BorderBrush = new SolidColorBrush(Color.FromArgb(a, 0xFF, 0xFF, 0xFF));
        Stroke.BorderThickness = new Thickness(_highlighted ? 2 : 1);
    }

    /// <summary>Atualiza o recorte do papel de parede atrás da pasta. Retorna false se não houver papel de parede.</summary>
    private bool UpdateGlass()
    {
        if (_hwnd == IntPtr.Zero || !_app.Config.Settings.Glass) return false;
        Native.GetWindowRect(_hwnd, out var wr);
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = wr.Left + ((CellWidth - BoxSize) / 2 - GlassMargin) * dpi.DpiScaleX;
        double y = wr.Top + (BoxTop - GlassMargin) * dpi.DpiScaleY;
        double w = (BoxSize + GlassMargin * 2) * dpi.DpiScaleX;
        double h = (BoxSize + GlassMargin * 2) * dpi.DpiScaleY;

        var map = Wallpaper.Map(new Rect(x, y, w, h));
        if (map is not { } m) return false;

        if (_glassBrush == null || _glassBrush.ImageSource != m.Image)
        {
            _glassBrush = new ImageBrush(m.Image) { ViewboxUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };
            GlassBg.Fill = _glassBrush;
        }
        _glassBrush.Viewbox = m.Viewbox;
        GlassBg.Visibility = Visibility.Visible;
        return true;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_companions != null)
        {
            double dx = Left - _dragStart.X, dy = Top - _dragStart.Y;
            foreach (var c in _companions)
            {
                c.W.Left = c.Left + dx;
                c.W.Top = c.Top + dy;
            }
        }
        if (_glassOn) UpdateGlass();
    }

    // ---------- Clique x arrastar ----------

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _downPos = e.GetPosition(this);
        _pressed = true;
        _ctrlOnDown = Native.CtrlDown;
        Root.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _downPos.X) < 5 && Math.Abs(p.Y - _downPos.Y) < 5) return;

        // Passou do limite: é um arrasto. Se esta pasta faz parte de uma seleção, move o grupo todo.
        _pressed = false;
        Root.ReleaseMouseCapture();
        _panel?.CloseAnimated();

        if (!IsSelected) _app.ClearSelection();
        _companions = _app.SelectedWindows.Where(w => w != this).Select(w => (w, w.Left, w.Top)).ToList();
        _dragStart = new Point(Left, Top);
        try { DragMove(); } catch (InvalidOperationException) { }
        var moved = _companions;
        _companions = null;

        SavePosition();
        foreach (var c in moved) c.W.SavePosition();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        Root.ReleaseMouseCapture();

        if (_ctrlOnDown)
        {
            IsSelected = !IsSelected; // Ctrl+clique: seleciona sem abrir
            return;
        }
        _app.ClearSelection();
        OpenPanel();
    }

    public void SavePosition()
    {
        var wa = ScreenUtil.WorkAreaFor(this);
        Left = Math.Max(wa.Left, Math.Min(Left, wa.Right - ActualWidth));
        Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - ActualHeight));
        Group.X = Left;
        Group.Y = Top;
        _app.SaveConfig();
        SendToBottom();
    }

    // ---------- Painel ----------

    public void OpenPanel(bool rename = false)
    {
        if (_panel != null)
        {
            if (!rename) _panel.CloseAnimated(); // clicar de novo na pasta recolhe
            return;
        }
        ShowPanel(new PanelWindow(_app, this, rename));
    }

    // Onde o painel aberto pela barra de tarefas foi fechado por um clique na própria barra.
    private Native.POINT? _taskbarClosePoint;
    private DateTime _taskbarClosedAt;

    /// <summary>Clique no ícone fixado na barra de tarefas (ponto em pixels físicos): abre ou recolhe o painel.</summary>
    internal void OpenPanelFromTaskbar(Native.POINT cursor)
    {
        if (_panel != null) { _panel.CloseAnimated(); return; }
        // O clique no ícone tira o foco do painel, que se fecha antes de o pedido chegar aqui.
        // Se foi esse mesmo clique, o painel deve ficar fechado (clicar de novo recolhe).
        if (_taskbarClosePoint is { } cp && (DateTime.Now - _taskbarClosedAt).TotalSeconds < 2.5 &&
            Math.Abs(cp.X - cursor.X) < 40 && Math.Abs(cp.Y - cursor.Y) < 40)
        {
            _taskbarClosePoint = null;
            return;
        }
        var panel = new PanelWindow(_app, this, rename: false, taskbarPoint: cursor);
        panel.ClosedByTaskbarClick += p => { _taskbarClosePoint = p; _taskbarClosedAt = DateTime.Now; };
        ShowPanel(panel);
    }

    /// <summary>Some da área de trabalho (a pasta continua acessível pela barra de tarefas).</summary>
    public void HideFromDesktop()
    {
        _panel?.Close();
        Hide();
    }

    private void ShowPanel(PanelWindow panel)
    {
        _panel = panel;
        _panel.Closed += (_, _) => { _panel = null; SendToBottom(); };
        _panel.Show();
        _panel.Activate();
    }

    public void CloseIntentionally()
    {
        ClosedIntentionally = true;
        _panel?.Close();
        Close();
    }

    // ---------- Arrastar para dentro ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool ok = DropParser.CanAccept(e.Data);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropHighlight(ok);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
        var items = DropParser.Parse(e.Data);
        if (items.Count > 0) _app.AddItems(Group, items);
        e.Handled = true;
    }

    private void SetDropHighlight(bool on)
    {
        if (_highlighted == on) return;
        _highlighted = on;
        AnimateBox(on ? 1.15 : 1.0);
        SetStroke();
    }

    private void AnimateBox(double scale)
    {
        var anim = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase() };
        BoxScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        BoxScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    // ---------- Menu ----------

    private void FillContextMenu()
    {
        if (!IsSelected) _app.ClearSelection();
        var selected = _app.SelectedWindows.ToList();

        var m = Root.ContextMenu;
        m.Items.Clear();
        m.Items.Add(Item(L.Open, () => OpenPanel()));
        m.Items.Add(Item(L.RenameFolder, () => OpenPanel(rename: true)));
        m.Items.Add(Item(L.PinToTaskbar, () => _app.PinToTaskbar(Group)));
        if (Group.Taskbar)
            m.Items.Add(Item(L.OnlyOnTaskbar, () => _app.SetOnDesktop(Group, false)));
        m.Items.Add(new Separator());
        m.Items.Add(Item(L.NewFolder, _app.AddFolder));
        if (IsSelected && selected.Count > 1)
            m.Items.Add(Item(L.DeleteSelected(selected.Count), () => _app.DeleteFolders(selected.Select(w => w.Group).ToList())));
        else
            m.Items.Add(Item(L.DeleteFolder, () => _app.DeleteFolders(new List<FolderGroup> { Group })));
        m.Items.Add(new Separator());
        m.Items.Add(Item(L.Options, _app.ShowSettings));
        m.Items.Add(Item(L.QuitApp, _app.ExitApp));
    }

    private static MenuItem Item(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (_, _) => action();
        return mi;
    }
}
