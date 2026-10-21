#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
echo "=== Visitor Check-In: starting ==="
command -v dotnet >/dev/null || { echo ".NET SDK not found: https://dotnet.microsoft.com/download"; exit 1; }
dotnet dev-certs https --trust || true
dotnet restore VisitorCheckIn.slnx
echo "Site: https://localhost:7080   (admin@example.com / ChangeMe123)"
dotnet run --project VisitorCheckIn/VisitorCheckIn.csproj --launch-profile VisitorCheckIn
