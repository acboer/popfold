using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Popfold;

/// <summary>
/// Observa (sem bloquear) o mouse e o teclado para integrar as pastas à área de trabalho:
/// - arrastos que começam na área de trabalho: o Windows desenha o retângulo de seleção normalmente;
///   ao soltar, as pastas que ele cobriu ficam selecionadas;
/// - clique direito na área de trabalho: guarda o ponto para "Novo > Popfold";
/// - Delete / Shift+Delete com a área de trabalho em foco: exclui as pastas selecionadas.
/// </summary>
internal sealed class DesktopSelection : IDisposable
{
    private readonly Action<Native.POINT, Native.POINT, bool> _onSelect;
    private readonly Action<bool> _onDelete;
    private readonly Func<IntPtr, bool> _isFolderWindow;
    private readonly Dispatcher _dispatcher;
    private readonly Native.LowLevelMouseProc _mouseProc; // referências mantidas para o GC não coletar
    private readonly Native.LowLevelMouseProc _keyProc;
    private IntPtr _mouseHook;
    private IntPtr _keyHook;
    private bool _tracking;
    private bool _ctrl;
    private Native.POINT _down;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_DELETE = 0x2E;

    /// <summary>Último clique direito na área de trabalho (pixels físicos), para "Novo > Popfold".</summary>
    public (Native.POINT Point, DateTime When)? LastDesktopRightClick { get; private set; }

    public DesktopSelection(Dispatcher dispatcher, Action<Native.POINT, Native.POINT, bool> onSelect, Action<bool> onDelete,
                            Func<IntPtr, bool> isFolderWindow)
    {
        _dispatcher = dispatcher;
        _onSelect = onSelect;
        _onDelete = onDelete;
        _isFolderWindow = isFolderWindow;
        _mouseProc = MouseProc;
        _keyProc = KeyProc;
        var module = Native.GetModuleHandle(null);
        _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyHook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == Native.WM_LBUTTONDOWN)
            {
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                _tracking = IsDesktopAt(info.pt);
                if (_tracking)
                {
                    _down = info.pt;
                    _ctrl = Native.CtrlDown;
                }
            }
            else if (msg == Native.WM_LBUTTONUP && _tracking)
            {
                _tracking = false;
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                var down = _down;
                bool ctrl = _ctrl;
                _dispatcher.BeginInvoke(() => _onSelect(down, info.pt, ctrl));
            }
            else if (msg == Native.WM_RBUTTONDOWN)
            {
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if (IsDesktopAt(info.pt)) LastDesktopRightClick = (info.pt, DateTime.Now);
            }
        }
        return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Só reage ao Delete quando quem está em foco é a área de trabalho ou uma pasta do Popfold
        // (em qualquer outro programa a tecla é dele). A tecla segue para o Explorer normalmente:
        // ícones do Windows que estiverem selecionados junto também vão para a Lixeira.
        if (nCode >= 0 && wParam.ToInt32() == WM_KEYDOWN && Marshal.ReadInt32(lParam) == VK_DELETE
            && (IsDesktop(Native.GetForegroundWindow()) || _isFolderWindow(Native.GetForegroundWindow())))
        {
            bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
            _dispatcher.BeginInvoke(() => _onDelete(shift));
        }
        return Native.CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private static bool IsDesktopAt(Native.POINT p) => IsDesktop(Native.WindowFromPoint(p));

    private static bool IsDesktop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var cls = Native.ClassOf(Native.GetAncestor(hwnd, 2 /* GA_ROOT */));
        return cls is "Progman" or "WorkerW";
    }

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        if (_keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
    }
}
