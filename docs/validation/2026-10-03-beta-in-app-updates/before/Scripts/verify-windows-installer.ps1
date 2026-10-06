# Intended for a disposable Windows CI host. Never replace an existing user installation.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [regex]::Match((Get-Content -Raw (Join-Path $projectRoot 'src/Unfold.Desktop/Unfold.Desktop.csproj')), '<Version>([^<]+)</Version>').Groups[1].Value
$installer = Join-Path $projectRoot "artifacts/Unfold-v$version-win-x64-setup.exe"
$installationRoot = Join-Path $env:LOCALAPPDATA 'Programs/Unfold'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DokhuStudio.Unfold'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Test-Path $installationRoot) { throw 'Refusing to replace an existing Windows installation.' }
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
    $process = Start-Process -FilePath $installer -ArgumentList '/S' -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed ($($process.ExitCode))." }
}
try {
    Run-Installer
    $checks.Add('silent-install')
    $registry = Get-ItemProperty $uninstallKey
    if ($registry.DisplayVersion -ne $version -or $registry.InstallLocation -ne $installationRoot) { throw 'Uninstall registration/version mismatch.' }
    if (-not (Test-Path (Join-Path $env:APPDATA 'Microsoft/Windows/Start Menu/Programs/Unfold/Unfold.lnk'))) { throw 'Start menu shortcut is missing.' }
    $manifest = Get-Content -Raw (Join-Path $projectRoot "artifacts/Unfold-v$version-win-x64-installer-inputs.json") | ConvertFrom-Json
    foreach ($file in $manifest.payload.PSObject.Properties) {
        $installedFile = Join-Path (Join-Path $installationRoot 'app') $file.Name
        if ((Get-FileHash $installedFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.Value) { throw "Installed payload mismatch: $($file.Name)" }
    }
    $checks.Add('installed-payload-and-shortcut')
    $versionOutput = Join-Path $verificationRoot 'version.txt'
    $process = Start-Process -FilePath (Join-Path $installationRoot 'app/Unfold.exe') -ArgumentList '--version' -RedirectStandardOutput $versionOutput -PassThru -Wait
    if ($process.ExitCode -ne 0 -or (Get-Content -Raw $versionOutput) -notmatch [regex]::Escape($version)) { throw 'Installed application version command failed.' }
    $checks.Add('installed-executable-version')
    $lock = [IO.FileStream]::new((Join-Path $env:UNFOLD_DATA_DIR '.instance.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $blocked = Start-Process -FilePath $installer -ArgumentList '/S' -PassThru -Wait
        if ($blocked.ExitCode -eq 0) { throw 'Installer replaced an app while its exclusive instance lock was held.' }
    } finally { $lock.Dispose() }
    $checks.Add('running-app-lock-refusal')
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty $runKey -Name Unfold -Value '"C:\OldPortable\Unfold.exe" --background'
    Run-Installer
    $expectedRun = '"' + (Join-Path $installationRoot 'app\Unfold.exe') + '" --background'
    if ((Get-ItemProperty $runKey -Name Unfold).Unfold -ne $expectedRun) { throw 'Enabled login launch did not migrate to the installed path.' }
    $checks.Add('reinstall-and-startup-migration')
    # Do not pass _?= here: the ordinary TEMP uninstall stub must also work.
    $process = Start-Process -FilePath (Join-Path $installationRoot 'Uninstall.exe') -ArgumentList '/S' -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Uninstaller failed ($($process.ExitCode))." }
    if ((Test-Path (Join-Path $installationRoot 'app/Unfold.exe')) -or (Test-Path $uninstallKey)) { throw 'Uninstall left app files/registration.' }
    if ((Get-ItemProperty $runKey -Name Unfold -ErrorAction SilentlyContinue).Unfold) { throw 'Uninstall left its login launch entry.' }
    if ((Get-FileHash $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw 'Installer/uninstaller changed user data.' }
    $checks.Add('uninstall-with-preserved-user-data')
    @{ success = $true; version = $version; checks = $checks; userData = $env:UNFOLD_DATA_DIR } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $verificationRoot 'result.json')
} finally {
    $env:UNFOLD_DATA_DIR = $previousDataDirectory
}
