# start-frontend.ps1
# This script navigates to the frontend directory, installs dependencies, and starts the development server.

$frontendPath = "$PSScriptRoot/frontend"

Write-Host "Navigating to frontend directory..." -ForegroundColor Cyan
Push-Location $frontendPath

try {
    Write-Host "Installing dependencies..." -ForegroundColor Green
    npm install

    Write-Host "Starting the development server..." -ForegroundColor Green
    npm start
} catch {
    Write-Host "An error occurred: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Pop-Location
}
