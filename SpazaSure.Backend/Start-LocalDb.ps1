# SpazaSure Backend - start the local development PostgreSQL server.
# The server lives in %LOCALAPPDATA%\SpazaSure\pgdata (PostgreSQL 18, port 5434);
# its password is in %LOCALAPPDATA%\SpazaSure\postgres-password.txt and the
# services read their connection string from the user environment variable
# ConnectionStrings__DefaultConnection.

$data = Join-Path $env:LOCALAPPDATA "SpazaSure\pgdata"
$pgCtl = "C:\Program Files\PostgreSQL\18\bin\pg_ctl.exe"

if (-not (Test-Path $data)) {
    Write-Host "No local SpazaSure database found at $data" -ForegroundColor Yellow
    return
}

& $pgCtl status -D $data *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Local PostgreSQL already running on port 5434" -ForegroundColor Green
    return
}

Write-Host "Starting local PostgreSQL on port 5434..." -ForegroundColor Green
Start-Process $pgCtl -ArgumentList "start", "-D", "`"$data`"", "-l", "`"$(Join-Path $env:LOCALAPPDATA 'SpazaSure\postgres.log')`"", "-w" -WindowStyle Hidden -Wait
