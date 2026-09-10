<#
    Compile QR Code Scanner et dépose l'exe final dans dist\.

        .\build.ps1              # compilation Release
        .\build.ps1 -Test        # + vérifie que l'exe décode réellement un QR code
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\QRCodeScanner\QRCodeScanner.csproj'
$output = Join-Path $root "src\QRCodeScanner\bin\$Configuration\QRCodeScanner.exe"
$dist = Join-Path $root 'dist'

dotnet build $project -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué." }

if ($Test) {
    $sample = Join-Path $env:TEMP 'qrcodescanner-selftest.png'
    $result = Join-Path $env:TEMP 'qrcodescanner-selftest.txt'
    $expected = 'https://github.com/Mushurelie/QRCodeScanner'

    # Les applications GUI ne bloquent pas le shell : il faut attendre explicitement.
    Start-Process $output -ArgumentList '--make-qr', $expected, $sample -Wait -NoNewWindow
    Start-Process $output -ArgumentList '--scan-file', $sample -Wait -NoNewWindow -RedirectStandardOutput $result

    $decoded = (Get-Content $result -Raw).Trim()
    Remove-Item $sample, $result -ErrorAction SilentlyContinue

    if ($decoded -ne $expected) {
        throw "Test raté : l'exe a décodé « $decoded » au lieu de « $expected »."
    }
    Write-Output "Test OK : QR code encodé puis relu correctement."
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $output $dist -Force

$exe = Get-Item (Join-Path $dist 'QRCodeScanner.exe')
Write-Output ("Prêt : {0} ({1:N0} Ko)" -f $exe.FullName, ($exe.Length / 1KB))
