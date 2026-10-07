# Install and update only on a disposable Windows CI runner.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $IsWindows) { throw 'Use a disposable Windows GitHub Actions runner.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installed = Join-Path $env:LOCALAPPDATA 'DokhuStudio.Unfold.Updates'
if (Test-Path $installed) { throw 'Refusing to replace an existing installation.' }
$evidence = Join-Path $root 'artifacts/validation/published-windows-update'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$scratch = Join-Path $env:RUNNER_TEMP ('unfold-update-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$env:UNFOLD_DATA_DIR = Join-Path $scratch 'profile'
New-Item -ItemType Directory -Path $env:UNFOLD_DATA_DIR | Out-Null
$sentinel = Join-Path $env:UNFOLD_DATA_DIR 'preserved-user-data.txt'
Set-Content $sentinel 'Update must preserve user settings, history and custom pets.'
$sentinelHash = (Get-FileHash $sentinel -Algorithm SHA256).Hash
$zip = Join-Path $scratch 'previous.zip'
Invoke-WebRequest 'https://github.com/C-Caffe1ne/Unfold/releases/download/v1.1.0-beta/Unfold-v1.1.0-beta-win-x64-installer.zip' -OutFile $zip
if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'f528a895196ce4175cc8c1e5abc61403f4f9dedf71173ab23c2987703c82cc21') { throw 'Previous installer ZIP changed.' }
Expand-Archive $zip (Join-Path $scratch 'previous')
$installer = Join-Path $scratch 'previous/Unfold-v1.1.0-beta-win-x64-setup.exe'
$process = Start-Process $installer -ArgumentList '--silent' -PassThru
if (-not $process.WaitForExit(120000) -or $process.ExitCode -ne 0) { throw 'Previous installer failed.' }
$probe = Join-Path $scratch 'probe'
New-Item -ItemType Directory -Path $probe | Out-Null
$appUpdates = [Security.SecurityElement]::Escape((Join-Path $root 'src/Unfold.Desktop/AppUpdates.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NoWarn>CA1416</NoWarn></PropertyGroup>
  <ItemGroup><PackageReference Include="Velopack" Version="1.2.0"/><Compile Include="$appUpdates" Link="AppUpdates.cs"/></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $probe 'Probe.csproj')
Copy-Item (Join-Path $PSScriptRoot 'verification/PublishedWindowsUpdateProbe.cs') (Join-Path $probe 'Program.cs')
dotnet build (Join-Path $probe 'Probe.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Update probe build failed.' }
dotnet (Join-Path $probe 'bin/Release/net10.0/Probe.dll') $installed $evidence
if ($LASTEXITCODE -ne 0) { throw 'Update probe failed.' }
$smokePath = Join-Path $env:UNFOLD_DATA_DIR 'verification/smoke.json'
$deadline = [DateTime]::UtcNow.AddSeconds(180)
while (-not (Test-Path $smokePath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Seconds 2 }
if (-not (Test-Path $smokePath)) { throw 'Updated app did not restart and produce smoke.json.' }
$smoke = Get-Content -Raw $smokePath | ConvertFrom-Json
$metadata = [xml](Get-Content -Raw (Join-Path $installed 'current/sq.version'))
$ns = [Xml.XmlNamespaceManager]::new($metadata.NameTable)
$ns.AddNamespace('n', 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd')
$version = $metadata.SelectSingleNode('/n:package/n:metadata/n:version', $ns).InnerText
if ($version -ne '1.1.1-beta' -or -not $smoke.success -or $smoke.imageFiles -ne 100 -or $smoke.timerRefinements.version -ne 'Beta v1.1.1') { throw 'Updated version or native restart diagnostic failed.' }
if ((Get-FileHash $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw 'Update changed existing user data.' }
Copy-Item (Join-Path $env:UNFOLD_DATA_DIR 'verification') (Join-Path $evidence 'verification') -Recurse
@{ success = $true; runtime = 'win-x64'; fromVersion = '1.1.0-beta'; version = $version; realAppUpdatesPrepareApply = $true; applicationReplaced = $true; restartedWithDiagnosticArgument = $true; userDataPreserved = $true; smokeSuccess = $smoke.success; smokeImages = $smoke.imageFiles } | ConvertTo-Json | Set-Content (Join-Path $evidence 'result.json')
Write-Output 'PASS: public 1.1.0 -> 1.1.1, real Update.exe apply/restart, 100 native diagnostic captures, preserved user data.'
