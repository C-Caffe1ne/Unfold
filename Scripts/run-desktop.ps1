$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$originalDataDirectory = $env:UNFOLD_DATA_DIR
if ([string]::IsNullOrWhiteSpace($env:UNFOLD_DATA_DIR)) {
    $env:UNFOLD_DATA_DIR = Join-Path ([IO.Path]::GetTempPath()) ('Unfold-dev-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($env:UNFOLD_DATA_DIR) | Out-Null
}
Write-Host "Unfold source: $projectRoot"
Write-Host "Development data: $env:UNFOLD_DATA_DIR"
$runExitCode = 1
try {
    dotnet run --project (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj') -c Release -- @args
    $runExitCode = $LASTEXITCODE
}
finally {
    $env:UNFOLD_DATA_DIR = $originalDataDirectory
}
exit $runExitCode
