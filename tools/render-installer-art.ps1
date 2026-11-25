# Renderiza a arte do instalador (branding\installer\*.html) em PNG e BMP usando o Edge headless.
# Gera cada imagem em 100% e 250% de escala (Inno Setup escolhe a certa conforme o DPI).
$ErrorActionPreference = 'Stop'
$dir = Resolve-Path "$PSScriptRoot\..\branding\installer"
$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
          "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge não encontrado." }
Add-Type -AssemblyName System.Drawing

$jobs = @(
    @{ Html = 'wizard-large.html'; W = 164; H = 314; Name = 'wizard-large' },
    @{ Html = 'wizard-small.html'; W = 55;  H = 55;  Name = 'wizard-small' }
)
foreach ($j in $jobs) {
    foreach ($scale in 1, 2.5) {
        $suffix = if ($scale -eq 1) { '' } else { '@2.5x' }
        $png = Join-Path $dir "$($j.Name)$suffix.png"
        $url = "file:///" + ((Join-Path $dir $j.Html) -replace '\\', '/')
        $profile = Join-Path $env:TEMP "popfold-edge-render"
        if (Test-Path $png) { Remove-Item $png }
        # Start-Process: o Edge escreve avisos inofensivos no stderr, que o PowerShell trataria como erro.
        $edgeArgs = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
            "--user-data-dir=`"$profile`"", "--force-device-scale-factor=$scale",
            "--window-size=$($j.W),$($j.H)", '--virtual-time-budget=6000', "--screenshot=`"$png`"", "`"$url`"")
        Start-Process -FilePath $edge -ArgumentList $edgeArgs -Wait -WindowStyle Hidden
        Start-Sleep -Milliseconds 300
        if (-not (Test-Path $png)) { throw "Falhou: $png" }
        # BMP 24 bits (formato clássico do Inno Setup)
        $img = [System.Drawing.Image]::FromFile($png)
        $bmp = New-Object System.Drawing.Bitmap $img.Width, $img.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp); $g.DrawImage($img, 0, 0, $img.Width, $img.Height); $g.Dispose()
        $bmp.Save(($png -replace '\.png$', '.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
        "{0}  {1}x{2}" -f (Split-Path $png -Leaf), $img.Width, $img.Height
        $bmp.Dispose(); $img.Dispose()
    }
}
