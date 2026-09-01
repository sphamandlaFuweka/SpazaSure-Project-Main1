#!/usr/bin/env bash
# SpazaSure Backend - Start All Services (macOS/Linux)
# Mirrors StartAll.ps1 for developers not on Windows.
#
# Usage:
#   ./start-all.sh
#
# Requires: .NET SDK 8/9, a running Postgres instance matching the
# connection string in each service's appsettings.json (see README).

set -euo pipefail

# Resolve relative to this script's own location, so it works regardless of
# where the repo is cloned on this machine.
BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

declare -a SERVICES=(
  "AuthService:5001:src/Services/SpazaSure.AuthService"
  "ProductService:5002:src/Services/SpazaSure.ProductService"
  "OrderService:5003:src/Services/SpazaSure.OrderService"
  "AnalyticsService:5004:src/Services/SpazaSure.AnalyticsService"
  "UserService:5005:src/Services/SpazaSure.UserService"
  "ComplianceService:5006:src/Services/SpazaSure.ComplianceService"
  "PaymentService:5038:src/Services/SpazaSure.PaymentService"
  "DeliveryService:5141:src/Services/SpazaSure.DeliveryService"
  "NotificationService:5184:src/Services/SpazaSure.NotificationService"
  "VerificationService:5000:src/Services/SpazaSure.VerificationService"
)

# Kill any old instances first (best-effort; ignores failures if none running)
pkill -f "SpazaSure.*\.dll" 2>/dev/null || true
sleep 1

PIDS=()

for entry in "${SERVICES[@]}"; do
  IFS=":" read -r name port path <<< "$entry"
  full_path="$BASE_DIR/$path"
  echo -e "\033[32mStarting $name on port $port...\033[0m"
  (cd "$full_path" && dotnet run --launch-profile http --no-build > "/tmp/spazasure-$name.log" 2>&1 &)
  sleep 0.5
done

echo -e "\033[35mStarting Gateway on port 5181...\033[0m"
(cd "$BASE_DIR/src/Gateway/SpazaSure.Gateway" && dotnet run --launch-profile http --no-build > "/tmp/spazasure-Gateway.log" 2>&1 &)

echo ""
echo -e "\033[36mAll services starting! (logs in /tmp/spazasure-*.log)\033[0m"
echo "Gateway Swagger UI: http://localhost:5181/swagger"
echo ""
echo "To stop everything: pkill -f 'SpazaSure.*\.dll'"
