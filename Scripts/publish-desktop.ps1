param(
    [ValidateSet('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')]
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
dotnet run --project (Join-Path $projectRoot 'tools/Unfold.MediaSetup') -- $Runtime $projectRoot
if ($LASTEXITCODE -ne 0) { throw "Media tool preparation failed ($LASTEXITCODE)." }
$artifactRoot = Join-Path $projectRoot 'artifacts'
$csprojPath = Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj'
# The csproj <Version> is the single source of truth so the archive name can
# never drift out of sync with the version actually baked into the build.
$version = [regex]::Match((Get-Content -Raw -LiteralPath $csprojPath), '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { throw "Could not read <Version> from $csprojPath" }
$packageDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot $Runtime))
if (-not $packageDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish target must stay inside this project artifacts directory.'
}
if (Test-Path -LiteralPath $packageDirectory) {
    if ((Get-Item -LiteralPath $packageDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing linked publish directory.' }
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
# Windows builds nest the runtime payload under app/ so the portable folder's
# top level only shows a launcher and a readme, not ~230 loose runtime DLLs.
$publishDirectory = if ($Runtime.StartsWith('win-')) { Join-Path $packageDirectory 'app' } else { $packageDirectory }
dotnet publish (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj') `
    -c Release -r $Runtime --self-contained true `
    -p:PublishReadyToRun=true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/cross-platform.md') -Destination (Join-Path $publishDirectory 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $publishDirectory
if ($Runtime.StartsWith('win-')) {
    $launcher = Join-Path $packageDirectory 'Unfold.cmd'
    Set-Content -LiteralPath $launcher -Encoding UTF8 -Value @'
@echo off
cd /d "%~dp0app"
start "" "Unfold.exe"
'@
    $readme = Join-Path $packageDirectory 'README.txt'
    Set-Content -LiteralPath $readme -Encoding UTF8 -Value @'
Unfold 실행 방법
================

1. "Unfold.cmd" 파일을 더블클릭하세요.
2. 처음 실행 시 Windows SmartScreen 경고가 뜨면 "추가 정보" -> "실행"을
   눌러 계속 진행하세요. (서명되지 않은 로컬 빌드입니다.)

앱은 실행 후 시스템 트레이에 남습니다. 트레이 아이콘 메뉴의 "Unfold 종료"로
완전히 끌 수 있습니다.

app 폴더의 파일은 프로그램 실행에 필요한 구성 요소입니다. 옮기거나 지우지
말고 이 폴더 전체를 그대로 유지해 주세요. 자세한 안내는 app\README.md를
확인하세요.
'@
    $archive = Join-Path $artifactRoot "Unfold-v$version-$Runtime.zip"
    Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archive -Force
    Write-Output "Portable app: $packageDirectory"
    Write-Output "Launcher: $launcher"
    Write-Output "Archive: $archive"
} else {
    Write-Output "macOS executable: $(Join-Path $publishDirectory 'Unfold')"
    Write-Output 'Use Scripts/make-macos-bundle.sh on macOS to create an application bundle.'
}
