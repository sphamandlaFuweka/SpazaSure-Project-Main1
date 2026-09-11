# SpazaSure Backend - Start All Services
# Resolves relative to this script's own location, so it works on any machine
# regardless of where the repo is cloned.
$base = $PSScriptRoot

Write-Host "Starting SpazaSure Backend Services..." -ForegroundColor Cyan

# Kill any old instances first
Get-Process | Where-Object { $_.Name -like "*dotnet*" } | ForEach-Object {
    $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine
    if ($cmd -like "*SpazaSure*") {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        Write-Host "Killed old process: $($_.Id)" -ForegroundColor Yellow
    }
}
Start-Sleep -Seconds 2

# Define all services: Name, Port, Path
$services = @(
    @{ Name="AuthService";          Port=5001; Path="src\Services\SpazaSure.AuthService" },
    @{ Name="ProductService";       Port=5002; Path="src\Services\SpazaSure.ProductService" },
    @{ Name="OrderService";         Port=5003; Path="src\Services\SpazaSure.OrderService" },
    @{ Name="AnalyticsService";     Port=5004; Path="src\Services\SpazaSure.AnalyticsService" },
    @{ Name="UserService";          Port=5005; Path="src\Services\SpazaSure.UserService" },
    @{ Name="ComplianceService";    Port=5006; Path="src\Services\SpazaSure.ComplianceService" },
    @{ Name="PaymentService";       Port=5038; Path="src\Services\SpazaSure.PaymentService" },
    @{ Name="DeliveryService";      Port=5141; Path="src\Services\SpazaSure.DeliveryService" },
    @{ Name="NotificationService";  Port=5184; Path="src\Services\SpazaSure.NotificationService" },
    @{ Name="VerificationService";  Port=5000; Path="src\Services\SpazaSure.VerificationService" }
)

foreach ($svc in $services) {
    $fullPath = Join-Path $base $svc.Path
    Write-Host "Starting $($svc.Name) on port $($svc.Port)..." -ForegroundColor Green
    Start-Process "dotnet" -ArgumentList "run --launch-profile http --no-build" -WorkingDirectory $fullPath -WindowStyle Normal
    Start-Sleep -Milliseconds 500
}

# Start Gateway last
Write-Host "Starting Gateway on port 5181..." -ForegroundColor Magenta
$gatewayPath = Join-Path $base "src\Gateway\SpazaSure.Gateway"
Start-Process "dotnet" -ArgumentList "run --launch-profile http --no-build" -WorkingDirectory $gatewayPath -WindowStyle Normal

Write-Host ""
Write-Host "All services started!" -ForegroundColor Cyan
Write-Host "Gateway Swagger UI: http://localhost:5181/swagger" -ForegroundColor White
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
