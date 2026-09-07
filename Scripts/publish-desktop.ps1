param(
    [ValidateSet('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')]
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $projectRoot 'artifacts'
$publishDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot $Runtime))
if (-not $publishDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish target must stay inside this project artifacts directory.'
}
if (Test-Path -LiteralPath $publishDirectory) {
    if ((Get-Item -LiteralPath $publishDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing linked publish directory.' }
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
dotnet publish (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj') `
    -c Release -r $Runtime --self-contained true `
    -p:PublishReadyToRun=true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/cross-platform.md') -Destination (Join-Path $publishDirectory 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $publishDirectory
if ($Runtime.StartsWith('win-')) {
    $archive = Join-Path $artifactRoot "Unfold-$Runtime.zip"
    Compress-Archive -LiteralPath $publishDirectory -DestinationPath $archive -Force
    Write-Output "Portable app: $publishDirectory"
    Write-Output "Archive: $archive"
} else {
    Write-Output "macOS executable: $(Join-Path $publishDirectory 'Unfold')"
    Write-Output 'Use Scripts/make-macos-bundle.sh on macOS to create an application bundle.'
}
