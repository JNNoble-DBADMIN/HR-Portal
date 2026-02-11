param(
  [string]$ProjectDir = "C:\HR_Portal"
)

$ErrorActionPreference = "Stop"
Set-Location $ProjectDir

function Say($m){ Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $m" }

Say "Ensure stable image exists"
docker image inspect hr_portal_gateway:stable *> $null

Say "Stop BLUE"
docker-compose -f docker-compose.apps.yml -f docker-compose.blue.yml down

Say "Retag stable as blue"
docker tag hr_portal_gateway:stable hr_portal_gateway:blue

Say "Start BLUE from stable"
docker-compose -f docker-compose.apps.yml -f docker-compose.blue.yml up -d

Say "Smoke test BLUE"
Start-Sleep -Seconds 3
$r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "http://localhost:88/"
Say "ROLLBACK OK ✅ HTTP $($r.StatusCode)"
Say "LIVE URL restored: http://portal.tv5.com.ph:88"
