# Run only on a disposable Windows CI host; preserve all real installations and startup entries.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [regex]::Match((Get-Content -Raw (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj')), '<Version>([^<]+)</Version>').Groups[1].Value
$installer = Join-Path $projectRoot "artifacts/Unfold-v$version-win-x64-setup.exe"
$packageId = 'DokhuStudio.Unfold.Updates'
$installationRoot = Join-Path $env:LOCALAPPDATA $packageId
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$packageId"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Test-Path $installationRoot) -or (Test-Path $uninstallKey)) { throw 'Refusing to replace an existing updater installation.' }
if (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs/Unfold')) { throw 'Use a disposable host without a legacy Unfold installation.' }
if ((Get-ItemProperty $runKey -Name Unfold -ErrorAction SilentlyContinue).Unfold) { throw 'Refusing to change an existing Unfold startup entry.' }
$verificationRoot = Join-Path $projectRoot 'artifacts/validation/windows-installer'
New-Item -ItemType Directory -Force -Path $verificationRoot | Out-Null
$previousDataDirectory = $env:UNFOLD_DATA_DIR
$env:UNFOLD_DATA_DIR = Join-Path ([IO.Path]::GetTempPath()) ('Unfold-installer-qa-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $env:UNFOLD_DATA_DIR | Out-Null
$sentinel = Join-Path $env:UNFOLD_DATA_DIR 'preserved-user-data.txt'
Set-Content -LiteralPath $sentinel -Value 'Settings/history/custom pets must survive install and uninstall.'
$sentinelHash = (Get-FileHash $sentinel -Algorithm SHA256).Hash
$checks = [Collections.Generic.List[string]]::new()
function Run-Installer {
    $process = Start-Process -FilePath $installer -ArgumentList '--silent' -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'Installer timeout.' }
    if ($process.ExitCode -ne 0) { throw "Installer failed ($($process.ExitCode))." }
}
try {
    Run-Installer
    $checks.Add('silent-install')
    if (-not (Test-Path $uninstallKey)) { throw 'Uninstall registration is missing.' }
    if (-not (Test-Path (Join-Path $installationRoot 'Unfold.exe')) -or -not (Test-Path (Join-Path $installationRoot 'Update.exe'))) { throw 'Stable launcher or updater is missing.' }
    if (-not (Test-Path (Join-Path $installationRoot 'current/sq.version'))) { throw 'Update installation metadata is missing.' }
    $portable = Join-Path $verificationRoot 'portable'
    Expand-Archive -LiteralPath (Join-Path $projectRoot "artifacts/Unfold-v$version-win-x64.zip") -DestinationPath $portable
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $portable 'current') -Recurse -File) {
        $relative = $file.FullName.Substring((Join-Path $portable 'current').Length + 1)
        $installed = Join-Path (Join-Path $installationRoot 'current') $relative
        if ((Get-FileHash $installed -Algorithm SHA256).Hash -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash) { throw "Installed payload mismatch: $relative" }
    }
    $checks.Add('installed-payload-and-updater-metadata')
    $versionOutput = Join-Path $verificationRoot 'version.txt'
    $process = Start-Process -FilePath (Join-Path $installationRoot 'current/Unfold.exe') -ArgumentList '--version' -RedirectStandardOutput $versionOutput -PassThru -Wait
    if ($process.ExitCode -ne 0 -or (Get-Content -Raw $versionOutput) -notmatch [regex]::Escape($version)) { throw 'Installed application version command failed.' }
    $checks.Add('installed-executable-version')
    if ((Get-ItemProperty $runKey -Name Unfold -ErrorAction SilentlyContinue).Unfold) { throw 'Installer enabled login launch without user consent.' }
    $checks.Add('installation-preserves-login-opt-out')
    Run-Installer
    $checks.Add('reinstall')
    if (-not (Test-Path $runKey)) { New-Item -Path $runKey -Force | Out-Null }
    New-ItemProperty -Path $runKey -Name Unfold -Value ('"' + (Join-Path $installationRoot 'Unfold.exe') + '" --background') -PropertyType String -Force | Out-Null
    $process = Start-Process -FilePath (Join-Path $installationRoot 'Update.exe') -ArgumentList '--uninstall','--silent' -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'Uninstall timeout.' }
    if ($process.ExitCode -ne 0) { throw "Uninstaller failed ($($process.ExitCode))." }
    if ((Test-Path (Join-Path $installationRoot 'current/Unfold.exe')) -or (Test-Path $uninstallKey)) { throw 'Uninstall left app files/registration.' }
    if ((Get-FileHash $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw 'Installer/uninstaller changed user data.' }
    if ((Get-ItemProperty $runKey -Name Unfold -ErrorAction SilentlyContinue).Unfold) { throw 'Uninstaller left its login startup entry.' }
    $checks.Add('uninstall-with-preserved-user-data-and-startup-removal')
    @{ success = $true; version = $version; checks = $checks; userData = $env:UNFOLD_DATA_DIR } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $verificationRoot 'result.json')
} finally {
    $env:UNFOLD_DATA_DIR = $previousDataDirectory
}
