# Registra "Novo > Popfold" para a máquina toda (HKLM). Precisa rodar como administrador.
# O Windows 11 ignora entradas do menu Novo registradas só para o usuário (HKCU).
param([Parameter(Mandatory)][string]$Exe, [switch]$Remove)

$ext = 'HKLM:\SOFTWARE\Classes\.popfold'
$prog = 'HKLM:\SOFTWARE\Classes\Popfold.Folder'

if ($Remove) {
    Remove-Item $ext -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $prog -Recurse -Force -ErrorAction SilentlyContinue
    return
}

# Modelo oculto: a cópia que o Explorer cria também fica oculta e não "pisca" na área de trabalho.
$template = Join-Path (Split-Path $Exe) 'Popfold.template'
if (-not (Test-Path -LiteralPath $template)) { New-Item -ItemType File -Path $template -Force | Out-Null }
(Get-Item -LiteralPath $template -Force).Attributes = 'Hidden, System'

New-Item -Path $ext -Force | Out-Null
Set-ItemProperty -Path $ext -Name '(default)' -Value 'Popfold.Folder'
Remove-Item "$ext\ShellNew" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path "$ext\ShellNew" -Force | Out-Null
New-ItemProperty -Path "$ext\ShellNew" -Name 'FileName' -Value $template -PropertyType String -Force | Out-Null

New-Item -Path $prog -Force | Out-Null
Set-ItemProperty -Path $prog -Name '(default)' -Value 'Popfold'
New-Item -Path "$prog\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$prog\DefaultIcon" -Name '(default)' -Value "$Exe,0"
New-Item -Path "$prog\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$prog\shell\open\command" -Name '(default)' -Value "`"$Exe`" --new `"%1`""
