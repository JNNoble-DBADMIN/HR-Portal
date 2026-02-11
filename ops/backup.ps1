param(
  [string]$ProjectDir = "C:\HR_Portal",
  [string]$BackupDir  = "C:\HR_Portal\Backup"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

Write-Host "Backing up source to zip..."
$zip = Join-Path $BackupDir ("HR_Portal_source_{0}.zip" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
Compress-Archive -Path (Join-Path $ProjectDir "*") -DestinationPath $zip -Force

Write-Host "Exporting images (if present)..."
docker image inspect hr_portal_gateway:blue  *> $null 2>$null
if ($LASTEXITCODE -eq 0) { docker save -o (Join-Path $BackupDir "gateway_blue.tar")  hr_portal_gateway:blue }

docker image inspect hr_portal_gateway:green *> $null 2>$null
if ($LASTEXITCODE -eq 0) { docker save -o (Join-Path $BackupDir "gateway_green.tar") hr_portal_gateway:green }

docker image inspect hr_portal_gateway:stable *> $null 2>$null
if ($LASTEXITCODE -eq 0) { docker save -o (Join-Path $BackupDir "gateway_stable.tar") hr_portal_gateway:stable }

Write-Host "Backup complete:"
Write-Host " - $zip"
Write-Host " - $BackupDir\gateway_*.tar (if images existed)"
