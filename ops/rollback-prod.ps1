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

$pointer = Join-Path $BackupDir "LATEST_PROD_BACKUP.txt"
if (!(Test-Path $pointer)) { throw "No pointer file found: $pointer" }

$stamp = (Get-Content $pointer -Raw).Trim()
if (-not $stamp) { throw "Pointer file is empty." }

$imageTar = Join-Path $BackupDir "prod-image-$stamp.tar"
if (!(Test-Path $imageTar)) { throw "Missing image tar: $imageTar" }

Say "Stopping current BLUE gateway (prod on 88)"
DC -f docker-compose.apps.yml -f blue.yml down

Say "Loading backup image tar: $imageTar"
docker load -i $imageTar | Out-Null

$rollbackTag = "hr_portal_gateway:prod-backup-$stamp"
Say "Tagging rollback image -> hr_portal_gateway:blue"
docker tag $rollbackTag hr_portal_gateway:blue

Say "Starting BLUE gateway on :88"
DC -f docker-compose.apps.yml -f blue.yml up -d

Start-Sleep -Seconds 3
$r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "http://localhost:88/"
Say "ROLLBACK OK: HTTP $($r.StatusCode)"
Say "LIVE URL: http://portal.tv5.com.ph:88"
