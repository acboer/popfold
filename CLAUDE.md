# Popfold

WPF/.NET 10 app for Windows: Android/iOS-style folders on the desktop and taskbar. Full history, architecture,
release process and pitfalls: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) (in Portuguese). Read it before
touching shell integration, the taskbar code, the installer or the release workflow.

The user speaks Portuguese; the app UI is English + Portuguese (`L.cs`), the GitHub page and release notes are English.

## Rules

- **Never launch Popfold (or any build of it) from Claude.** The Claude desktop sandbox redirects %AppData% and HKCU
  writes, so the user's folders end up in a private copy and "vanish" after a reboot. Ask the user to open it from
  the Start menu or File Explorer. Killing it or taking screenshots is fine.
- Ask before restarting Explorer.
- Don't edit source files with PowerShell 5 `Get-Content`/`Set-Content` (corrupts UTF-8); use the Edit tool or sed.
- Simulated clicks don't drive the Win11 taskbar or XAML context menus: ask the user to click.
- Screenshots for docs show only Popfold: minimize other windows, crop tight, mask other taskbar icons.
- Commit/push only when asked. Releases: bump `<Version>` in `Popfold.csproj`, add `docs/release-notes-<ver>.md`,
  push tag `v<ver>`; GitHub Actions builds and publishes the installer.

## Commands

```powershell
dotnet build -c Release
.\tools\build-installer.ps1      # → dist\Popfold-Setup-<version>.exe (needs Inno Setup 6)
```
