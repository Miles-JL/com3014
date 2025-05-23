try {
    $response = Invoke-WebRequest -Uri "http://localhost:5250/health" -UseBasicParsing -ErrorAction Stop
    Write-Host "Health Check Response:"
    Write-Host "Status Code: $($response.StatusCode)"
    Write-Host "Content: $($response.Content)"
} catch {
    Write-Host "Error accessing health endpoint:"
    Write-Host $_.Exception.Message
}

# Check if port 5250 is in use
Write-Host "\nChecking if port 5250 is in use..."
$portInUse = Test-NetConnection -ComputerName localhost -Port 5250 -InformationLevel Quiet
if ($portInUse) {
    Write-Host "Port 5250 is in use."
} else {
    Write-Host "Port 5250 is not in use."
}
