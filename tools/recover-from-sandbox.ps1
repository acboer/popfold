# Recupera as pastas do Popfold que ficaram presas na área isolada de outro aplicativo.
#
# Quando o Popfold é aberto de dentro de um app empacotado (como o app do Claude), o Windows desvia o que
# ele grava em %AppData% para %LocalAppData%\Packages\<app>\LocalCache\Roaming. Este script copia esses
# dados de volta para o lugar real e reativa o "Iniciar com o Windows".
#
# Rode pelo Explorador de Arquivos: botão direito neste arquivo → "Executar com o PowerShell".
# (Não rode de dentro do app do Claude: ele também está na área isolada.)

$ErrorActionPreference = 'Stop'
$real = Join-Path $env:APPDATA 'Popfold'
$exe = Resolve-Path (Join-Path $PSScriptRoot '..\bin\Release\net10.0-windows\Popfold.exe') -ErrorAction SilentlyContinue

function Fail($msg) { Write-Host "`n$msg" -ForegroundColor Red; Read-Host 'Enter para sair'; exit 1 }

# Este script também não pode estar numa área isolada.
New-Item -ItemType Directory -Force $real | Out-Null
$probe = 'probe-' + [guid]::NewGuid().ToString('N')
New-Item -ItemType File (Join-Path $real $probe) | Out-Null
$isolated = Get-ChildItem "$env:LOCALAPPDATA\Packages\*\LocalCache\Roaming\Popfold\$probe" -ErrorAction SilentlyContinue
Remove-Item (Join-Path $real $probe)
if ($isolated) { Fail 'Este PowerShell está dentro de outro aplicativo. Abra pelo Explorador de Arquivos (botão direito → Executar com o PowerShell).' }

# A cópia isolada mais recente.
$copies = Get-ChildItem "$env:LOCALAPPDATA\Packages\*\LocalCache\Roaming\Popfold\config.json" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending
if (-not $copies) { Fail 'Nenhuma cópia isolada do Popfold encontrada. Nada a recuperar.' }
$source = $copies[0].Directory.FullName
Write-Host "Cópia encontrada: $source ($($copies[0].LastWriteTime))"

Write-Host 'Fechando o Popfold...'
Get-Process Popfold -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Guarda os dados atuais antes de substituir.
$backup = "$real-antes-da-recuperacao-$(Get-Date -Format yyyyMMdd-HHmmss)"
if (Test-Path (Join-Path $real 'config.json')) {
    Copy-Item $real $backup -Recurse
    Write-Host "Dados atuais guardados em: $backup"
}

Copy-Item (Join-Path $source '*') $real -Recurse -Force
Write-Host 'Pastas, posições e atalhos copiados.' -ForegroundColor Green

# Renomeia a cópia isolada para ela não "esconder" os dados reais de quem estiver na área isolada.
Rename-Item $source "Popfold-recuperado-$(Get-Date -Format yyyyMMdd-HHmmss)"

if ($exe) {
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Popfold' -Value "`"$exe`""
    Write-Host 'Iniciar com o Windows: ativado.'
    Start-Process $exe
    Write-Host 'Popfold aberto.' -ForegroundColor Green
}
else { Write-Host 'Popfold.exe não encontrado; abra o Popfold manualmente.' -ForegroundColor Yellow }

Read-Host "`nPronto. Enter para fechar"
