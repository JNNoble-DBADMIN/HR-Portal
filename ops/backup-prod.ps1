param(
  [string]$ProjectDir = "C:\HR_Portal",
  [string]$BackupDir  = "C:\HR_Portal\Backup"
)

$ErrorActionPreference = "Stop"
Set-Location $ProjectDir

function Say($m){ Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $m" }

function DC {
  param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
  if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    & docker-compose @Args
  } else {
    & docker compose @Args
  }
}

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

# --- Detect current BLUE container (prod on :88) ---
$blueId = (DC -f docker-compose.apps.yml -f blue.yml ps -q gateway-blue) | Select-Object -First 1
if (-not $blueId) { throw "gateway-blue is not running. Start prod first using: docker-compose -f docker-compose.apps.yml -f blue.yml up -d" }

$prodImage = (docker inspect $blueId --format "{{.Config.Image}}")
if (-not $prodImage) { throw "Cannot detect prod image from container id: $blueId" }

Say "Backing up source folder to zip..."
$zipPath = Join-Path $BackupDir "prod-source-$stamp.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$ProjectDir\*" -DestinationPath $zipPath -Force
Say "Saved: $zipPath"

Say "Tagging current PROD image as rollback tag..."
$rollbackTag = "hr_portal_gateway:prod-backup-$stamp"
docker tag $prodImage $rollbackTag
Say "Tagged: $prodImage -> $rollbackTag"

Say "Saving Docker image tar..."
$imageTar = Join-Path $BackupDir "prod-image-$stamp.tar"
docker save -o $imageTar $rollbackTag
Say "Saved: $imageTar"

$pointer = Join-Path $BackupDir "LATEST_PROD_BACKUP.txt"
"$stamp" | Out-File -Encoding ascii -Force $pointer
Say "Updated pointer: $pointer"

Say "Backup complete."
