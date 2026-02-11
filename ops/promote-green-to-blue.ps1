param(
  [string]$ProjectDir = "C:\HR_Portal"
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

Say "Verify GREEN is healthy"
$r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "http://localhost:188/"
Say "GREEN OK: HTTP $($r.StatusCode)"

Say "Stop BLUE gateway (free port 88)"
DC -f docker-compose.apps.yml -f blue.yml down

Say "Tag GREEN image as BLUE"
docker image inspect hr_portal_gateway:green *> $null
docker tag hr_portal_gateway:green hr_portal_gateway:blue

Say "Start BLUE gateway on :88"
DC -f docker-compose.apps.yml -f blue.yml up -d

Say "Smoke test BLUE"
Start-Sleep -Seconds 3
$r2 = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "http://localhost:88/"
Say "BLUE OK: HTTP $($r2.StatusCode)"
Say "LIVE URL: http://portal.tv5.com.ph:88"
