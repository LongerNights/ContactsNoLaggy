# Closes Resonite gracefully (never kills it), builds + installs the mod into rml_mods, relaunches in desktop mode.
param([switch]$NoLaunch, [string]$LaunchArgs = '-Screen', [int]$CloseTimeoutSeconds = 90)
$ErrorActionPreference = 'Stop'
$dir = 'E:\Program Files (x86)\Steam\steamapps\common\Resonite'
$names = 'Resonite', 'Renderite.Host', 'Renderite.Renderer'
function Get-Reso { Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.Name } }

foreach ($p in @(Get-Reso)) { if ($p.MainWindowHandle -ne 0) { [void]$p.CloseMainWindow() } }
$deadline = (Get-Date).AddSeconds($CloseTimeoutSeconds)
while ((Get-Reso) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
if (Get-Reso) { throw "Resonite did not exit within $CloseTimeoutSeconds s. Close it by hand and rerun." }

dotnet build (Join-Path (Split-Path -Parent $PSScriptRoot) 'src') -c Release -p:InstallMod=true
if ($LASTEXITCODE -ne 0) { throw 'Build/install failed.' }

if (-not $NoLaunch) { Start-Process -FilePath "$dir\Resonite.exe" -ArgumentList $LaunchArgs -WorkingDirectory $dir }
