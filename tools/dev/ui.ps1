# Move o mouse, clica ou envia teclas. Não funciona na barra de tarefas nem nos menus XAML do Windows 11.
param([ValidateSet('click','rclick','move','key')][string]$Action, [int]$X, [int]$Y, [string]$Keys)
if (-not ('UiM' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class UiM {
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(int f,int x,int y,int d,IntPtr e);
}
'@
}
switch ($Action) {
  'move'   { [void][UiM]::SetCursorPos($X, $Y) }
  'click'  { [void][UiM]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 250; [UiM]::mouse_event(2,0,0,0,[IntPtr]::Zero); Start-Sleep -Milliseconds 60; [UiM]::mouse_event(4,0,0,0,[IntPtr]::Zero) }
  'rclick' { [void][UiM]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 250; [UiM]::mouse_event(8,0,0,0,[IntPtr]::Zero); Start-Sleep -Milliseconds 60; [UiM]::mouse_event(0x10,0,0,0,[IntPtr]::Zero) }
  'key'    { Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait($Keys) }
}
