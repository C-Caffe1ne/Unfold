$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
dotnet run --project (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj') -- @args
exit $LASTEXITCODE
