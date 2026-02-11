Set-Location C:\HR_Portal

Write-Host "1) Backup current gateway container -> image"
docker commit hr_portal-gateway-1 hr_portal_gateway_backup:working

Write-Host "2) Pull from GitHub"
git pull origin main

Write-Host "3) Rebuild + restart gateway"
docker-compose build gateway
docker-compose up -d gateway

Write-Host "Done."
