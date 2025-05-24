# Clear contents of cdn-service/storage directory
$storagePath = "services/cdn-service/storage"
if (Test-Path $storagePath) {
    Remove-Item -Path "$storagePath\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Cleared contents of $storagePath" -ForegroundColor DarkYellow
}

$services = @(
    @{ Name = "auth-service"; Path = "services/auth-service" },
    @{ Name = "user-service"; Path = "services/user-service" },
    @{ Name = "chatroom-service"; Path = "services/chatroom-service" },
    @{ Name = "message-service"; Path = "services/message-service" }
)

function Reset-Migrations {
    param (
        [string]$ServiceName,
        [string]$ServicePath
    )

    Write-Host "`n=== Processing $ServiceName ===" -ForegroundColor Cyan
    Push-Location $ServicePath

    try {
        # Delete 'Migrations' folder (case-insensitive)
        $folders = @("Migrations", "migrations")
        foreach ($folder in $folders) {
            if (Test-Path $folder) {
                Remove-Item -Recurse -Force $folder
                Write-Host "Deleted '$folder' folder for $ServiceName." -ForegroundColor DarkYellow
            }
        }

        dotnet ef database drop -f
        Write-Host "Dropped database for $ServiceName." -ForegroundColor DarkGray

        dotnet ef migrations add Init
        Write-Host "Added Init migration for $ServiceName." -ForegroundColor Green

        dotnet ef database update
        Write-Host "Updated database for $ServiceName." -ForegroundColor Green
    }
    catch {
        Write-Host "Error while processing ${ServiceName}: $($_.Exception.Message)" -ForegroundColor Red
    }
    finally {
        Pop-Location
    }
}

foreach ($service in $services) {
    Reset-Migrations -ServiceName $service.Name -ServicePath $service.Path
}

Write-Host "`nAll services cleaned and database reset complete." -ForegroundColor Yellow
