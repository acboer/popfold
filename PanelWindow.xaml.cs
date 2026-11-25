using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Popfold;

/// <summary>O painel expandido da pasta.</summary>
public partial class PanelWindow : Window
{
    private const double TileWidth = 92;

    private readonly App _app;
    private readonly FolderWindow _folder;
    private readonly bool _rename;
    private readonly Native.POINT? _taskbarPoint;

    /// <summary>Painel aberto pela barra de tarefas e fechado por um clique nela (ponto do clique, pixels físicos).</summary>
    internal event Action<Native.POINT>? ClosedByTaskbarClick;
    private bool _closing;
    private bool _suppressDeactivate;

    public ObservableCollection<ItemViewModel> Items { get; } = new();

    private FolderGroup Group => _folder.Group;

    internal PanelWindow(App app, FolderWindow folder, bool rename, Native.POINT? taskbarPoint = null)
    {
        _app = app;
        _folder = folder;
        _rename = rename;
        _taskbarPoint = taskbarPoint;
        InitializeComponent();
        ApplyTheme();

        EmptyText.Text = L.EmptyFolder;
        ItemsHost.ItemsSource = Items;
        TitleBox.Text = Group.Name;
        Reload();

        Loaded += OnLoaded;
        Deactivated += (_, _) =>
        {
            if (_suppressDeactivate) return;
            if (_taskbarPoint != null && Native.GetCursorPos(out var p) && Native.IsOverTaskbar(p))
                ClosedByTaskbarClick?.Invoke(p);
            CloseAnimated();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        TitleBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitTitle();
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, this);
                e.Handled = true;
            }
        };
        TitleBox.LostKeyboardFocus += (_, _) => CommitTitle();

        Root.DragEnter += OnDragOver;
        Root.DragOver += OnDragOver;
        Root.Drop += OnDrop;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.AddExStyle(new WindowInteropHelper(this).Handle, Native.WS_EX_TOOLWINDOW);
    }

    /// <summary>Cores do painel conforme o estilo escolhido nas Opções (Bala de Goma clara ou noturna).</summary>
    private void ApplyTheme()
    {
        bool dark = _app.Config.Settings.Style == "dark";
        static SolidColorBrush B(uint argb) => new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        Resources["PanelBg"] = dark ? B(0xF5221840) : B(0xFAFFFFFF);
        Resources["PanelBorder"] = dark ? B(0xFF3B2D6B) : B(0x66B79CFF);
        Resources["PanelText"] = dark ? B(0xFFF4EEFF) : B(0xFF2B1B4A);
        Resources["PanelSubtle"] = dark ? B(0xFFBDB2D9) : B(0xFF5A4A75);
        Resources["PanelLine"] = dark ? B(0x55FFFFFF) : B(0x55B79CFF);
        Resources["TileHover"] = dark ? B(0x24FFFFFF) : B(0x33B79CFF);
        Resources["TilePressed"] = dark ? B(0x3DFFFFFF) : B(0x55FF7AB6);
        Resources["PanelShadow"] = dark ? 0.6 : 0.28;
    }

    public void Reload()
    {
        Items.Clear();
        foreach (var item in Group.Items) Items.Add(new ItemViewModel(item));

        int n = Items.Count;
        int cols = n <= 9 ? 3 : n <= 16 ? 4 : 5;
        ItemsHost.Width = cols * TileWidth;
        ItemsHost.Visibility = n == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyText.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (IsLoaded) Dispatcher.BeginInvoke(Reposition, DispatcherPriority.Loaded);
    }

    // ---------- Abrir / fechar ----------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Reposition();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scale = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

        if (_taskbarPoint != null) Native.ForceForeground(new WindowInteropHelper(this).Handle);

        if (_rename)
        {
            // Pasta nova (inclusive via "Novo > Popfold" no Explorer): garante o foco para digitar o nome.
            Native.ForceForeground(new WindowInteropHelper(this).Handle);
            TitleBox.Focus();
            TitleBox.SelectAll();
        }
    }

    /// <summary>Centraliza o painel sobre o ícone da pasta, sem sair da tela.</summary>
    private void Reposition()
    {
        UpdateLayout();
        double w = ActualWidth, h = ActualHeight;
        Point anchor;
        Rect wa;
        double left, top;

        if (_taskbarPoint is { } tp)
        {
            // Aberto pela barra de tarefas: encostado nela, alinhado com o ícone clicado.
            double scale = Native.GetDpiForSystem() / 96.0;
            anchor = new Point(tp.X / scale, tp.Y / scale);
            var r = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(tp.X, tp.Y)).WorkingArea;
            wa = new Rect(r.Left / scale, r.Top / scale, r.Width / scale, r.Height / scale);
            double m = Root.Margin.Left - 12; // a margem transparente (sombra) menos um respiro de 12
            left = Math.Max(wa.Left - m, Math.Min(anchor.X - w / 2, wa.Right - w + m));
            top = Math.Max(wa.Top - m, Math.Min(anchor.Y - h / 2, wa.Bottom - h + m));
            if (anchor.Y >= wa.Bottom) top = wa.Bottom - h + m;          // barra embaixo
            else if (anchor.Y < wa.Top) top = wa.Top - m;                // barra em cima
            else if (anchor.X < wa.Left) left = wa.Left - m;             // barra à esquerda
            else if (anchor.X >= wa.Right) left = wa.Right - w + m;      // barra à direita
        }
        else
        {
            anchor = _folder.BoxCenter;
            wa = ScreenUtil.WorkAreaFor(_folder);
            left = Math.Max(wa.Left, Math.Min(anchor.X - w / 2, wa.Right - w));
            top = Math.Max(wa.Top, Math.Min(anchor.Y - h / 2, wa.Bottom - h));
        }
        Left = left;
        Top = top;

        // A animação "nasce" do ícone da pasta.
        double ox = (anchor.X - left - Root.Margin.Left) / Math.Max(1, Root.ActualWidth);
        double oy = (anchor.Y - top - Root.Margin.Top) / Math.Max(1, Root.ActualHeight);
        Root.RenderTransformOrigin = new Point(Math.Clamp(ox, 0, 1), Math.Clamp(oy, 0, 1));
    }

    public void CloseAnimated()
    {
        if (_closing) return;
        _closing = true;
        CommitTitle();

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var scale = new DoubleAnimation(0.7, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseAnimated();
            e.Handled = true;
        }
    }

    private void CommitTitle()
    {
        var t = TitleBox.Text.Trim();
        if (t.Length == 0) { TitleBox.Text = Group.Name; return; }
        if (t == Group.Name) return;
        Group.Name = t;
        _app.SaveConfig();
        _folder.Refresh();
        _app.ScheduleTaskbarUpdate(Group);
    }

    // ---------- Itens ----------

    private static ItemViewModel? VmOf(object sender) => (sender as FrameworkElement)?.DataContext as ItemViewModel;

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is not { } vm) return;
        ShellLauncher.Launch(vm.Model);
        CloseAnimated();
    }

    private void ItemOpen_Click(object sender, RoutedEventArgs e) => Item_Click(sender, e);

    private void ItemAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is not { } vm) return;
        _suppressDeactivate = true; // o UAC tira o foco
        CloseAnimated();
        ShellLauncher.Launch(vm.Model, asAdmin: true);
    }

    private void ItemLocation_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is not { } vm) return;
        ShellLauncher.OpenLocation(vm.Model);
        CloseAnimated();
    }

    private void ItemRename_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is not { } vm) return;
        _suppressDeactivate = true;
        var name = InputDialog.Show(this, L.RenameTitle, L.ItemNamePrompt, vm.Model.Name);
        _suppressDeactivate = false;
        if (!string.IsNullOrWhiteSpace(name) && name != vm.Model.Name)
        {
            vm.Model.Name = name;
            _app.SaveConfig();
            _app.NotifyChanged(Group);
        }
        Activate();
    }

    private void ItemRemove_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is not { } vm) return;
        _app.RemoveItem(Group, vm.Model);
    }

    // ---------- Arrastar para dentro do painel ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DropParser.CanAccept(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var items = DropParser.Parse(e.Data);
        if (items.Count > 0) _app.AddItems(Group, items);
        e.Handled = true;
    }
}
