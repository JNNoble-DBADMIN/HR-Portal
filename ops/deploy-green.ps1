param(
  [string]$ProjectDir = "C:\HR_Portal",
  [string]$Branch = "main"
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

Say "Fetch latest code"
git fetch origin
git checkout $Branch
git pull origin $Branch

# Tag current BLUE image as stable (optional but recommended)
$blueId = (DC -f docker-compose.apps.yml -f blue.yml ps -q gateway-blue) | Select-Object -First 1
if ($blueId) {
  $blueImage = (docker inspect $blueId --format "{{.Config.Image}}")
  if ($blueImage) {
    Say "Tagging current BLUE image as STABLE"
    docker tag $blueImage hr_portal_gateway:stable
  }
} else {
  Say "WARN: gateway-blue not running; skipping stable tag."
}

Say "Build GREEN image"
docker build -t hr_portal_gateway:green .

Say "Start shared apps (app1/app2)"
DC -f docker-compose.apps.yml up -d

Say "Start GREEN gateway on :188"
DC -f docker-compose.apps.yml -f green.yml up -d

Say "Smoke test GREEN"
Start-Sleep -Seconds 3
$r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "http://localhost:188/"
Say "GREEN OK: HTTP $($r.StatusCode)"
Say "GREEN URL: http://portal.tv5.com.ph:188"
