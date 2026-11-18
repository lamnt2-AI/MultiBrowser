$publishDir = Join-Path $PSScriptRoot "MultiBrowser\bin\Release\net10.0-windows\publish"
Set-Location $publishDir

$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCmd) { $dotnet = $dotnetCmd.Source }
}

if (Test-Path $dotnet) {
    Start-Process -FilePath $dotnet -ArgumentList "MultiBrowser.dll" -WorkingDirectory $publishDir
} else {
    Start-Process -FilePath (Join-Path $publishDir "MultiBrowser.exe") -WorkingDirectory $publishDir
}
