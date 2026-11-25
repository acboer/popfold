# Gera o instalador do Popfold: publica o app (com o .NET embutido) e compila installer\Popfold.iss.
# Saída: dist\Popfold-Setup-<versão>.exe
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
          "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado (https://jrsoftware.org/isinfo.php).' }

$publish = Join-Path $root 'publish\win-x64'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'Popfold.csproj') -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou.' }

& $iscc /Q (Join-Path $root 'installer\Popfold.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup falhou.' }
Get-ChildItem (Join-Path $root 'dist') -Filter 'Popfold-Setup-*.exe' | Sort-Object LastWriteTime | Select-Object -Last 1 |
    ForEach-Object { '{0}  ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB) }
