# start-all-microservices.ps1
# Starts all microservices on specific ports in individual terminal windows

$basePath = "$PSScriptRoot/services"

$services = @(
    @{ name = "auth-service";         port = 5106 },
    @{ name = "user-service";         port = 5117 },
    @{ name = "chatroom-service";     port = 5262 },
    @{ name = "message-service";      port = 5199 },
    @{ name = "realtime-service";     port = 5200 },
    @{ name = "notification-service"; port = 5201 },
    @{ name = "api-gateway";      port = 5247 }
)


foreach ($svc in $services) {
    $path = Join-Path $basePath $svc.name
    $cmd = "dotnet run --urls=http://localhost:$($svc.port)"

    Write-Host "Starting $($svc.name) on port $($svc.port)..."

    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$path`"; $cmd"
}
