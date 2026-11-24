# Invoice Manager — install or update on Windows (no Docker, no .NET needed).
#   irm https://raw.githubusercontent.com/mgmehdiforme/local-invoice-manager/main/install/install.ps1 | iex
# Your data is kept on update (it lives in %LOCALAPPDATA%\LocalInvoiceManager\data, outside the app folder).
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = 'mgmehdiforme/local-invoice-manager'
$app = Join-Path $env:LOCALAPPDATA 'LocalInvoiceManager\app'
$url = "https://github.com/$repo/releases/latest/download/InvoiceManager-win-x64.zip"

Write-Host 'Downloading Invoice Manager...'
$tmp = Join-Path $env:TEMP ("lim-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Invoke-WebRequest -Uri $url -OutFile "$tmp\app.zip" -UseBasicParsing
    Expand-Archive -Path "$tmp\app.zip" -DestinationPath $tmp -Force

    Get-Process InvoiceManager -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    if (Test-Path $app) { Remove-Item $app -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path $app) | Out-Null
    Move-Item "$tmp\InvoiceManager" $app
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }

$exe = Join-Path $app 'InvoiceManager.exe'
$shell = New-Object -ComObject WScript.Shell
foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {
    $lnk = $shell.CreateShortcut((Join-Path $dir 'Invoice Manager.lnk'))
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $app
    $lnk.Description = 'Local invoices, quotations and payments'
    $lnk.Save()
}

Write-Host "Installed to $app"
Write-Host 'Shortcuts: Start menu and desktop ("Invoice Manager"). Starting now; your browser will open http://localhost:8090'
Start-Process $exe -WorkingDirectory $app
