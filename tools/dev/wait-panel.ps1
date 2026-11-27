# Espera o painel aberto pela barra de tarefas aparecer (até 5 min) e captura a área dele.
param([string]$Out)
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class WP {
 public delegate bool EP(IntPtr h, IntPtr l);
 [DllImport("user32.dll")] static extern bool EnumWindows(EP f, IntPtr l);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
 public struct RECT { public int L,T,R,B; }
 public static int[] Panel(uint pid){ int[] o=null; EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p); RECT r; GetWindowRect(h,out r);
   if(p==pid && IsWindowVisible(h) && r.R-r.L>250 && r.B>800) o=new[]{r.L,r.T,r.R,r.B}; return true;}, IntPtr.Zero); return o; }
}
'@
$deadline = (Get-Date).AddMinutes(5)
while ((Get-Date) -lt $deadline) {
    $p = Get-Process Popfold -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) {
        $r = [WP]::Panel($p.Id)
        if ($r) {
            Start-Sleep -Milliseconds 700   # deixa a animação terminar
            $k = 1.275
            $x = [int](($r[0] - 10) * $k); $y = [int](($r[1] - 10) * $k)
            $w = [int](($r[2] - $r[0] + 20) * $k); $h = [int](1152 * $k) - $y
            & "$PSScriptRoot\cap.ps1" -X $x -Y $y -W $w -H $h -Out $Out
            "captured $($r -join ',') -> $x,$y ${w}x$h"
            exit 0
        }
    }
    Start-Sleep -Milliseconds 150
}
'timeout'
