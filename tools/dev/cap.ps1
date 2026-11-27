# Captura uma área da tela (pixels físicos) num PNG. -ShowDesktop esconde as janelas antes.
param([int]$X, [int]$Y, [int]$W, [int]$H, [string]$Out, [switch]$ShowDesktop)
$src = @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class Cap {
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d,int x,int y,int w,int h,IntPtr s,int sx,int sy,int rop);
 public static void Grab(int x,int y,int w,int h,string path){ var bmp=new Bitmap(w,h); using(var g=Graphics.FromImage(bmp)){ var hdc=g.GetHdc(); var sdc=GetDC(IntPtr.Zero); BitBlt(hdc,0,0,w,h,sdc,x,y,0x00CC0020|0x40000000); ReleaseDC(IntPtr.Zero,sdc); g.ReleaseHdc(hdc);} bmp.Save(path); bmp.Dispose(); }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
if ($ShowDesktop) { $shell = New-Object -ComObject Shell.Application; $shell.ToggleDesktop(); Start-Sleep -Milliseconds 1200 }
[Cap]::Grab($X, $Y, $W, $H, $Out)
if ($ShowDesktop) { $shell.ToggleDesktop() }
