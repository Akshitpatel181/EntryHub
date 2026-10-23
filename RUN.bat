@echo off
cd /d "%~dp0"
echo ============================================
echo   EntryHub -  starting
echo ============================================
dotnet --version >nul 2>&1
if errorlevel 1 (
  echo .NET SDK not found. Install it from https://dotnet.microsoft.com/download
  pause
  exit /b 1
)
echo Trusting the HTTPS dev certificate (click Yes if Windows asks)...
dotnet dev-certs https --trust
dotnet restore VisitorCheckIn.slnx
echo.
echo Site will open at  https://localhost:7080   (admin@entryhub.com / Newpass123)
echo Keep this window open. Press Ctrl+C to stop.
dotnet run --project VisitorCheckIn\VisitorCheckIn.csproj --launch-profile VisitorCheckIn
pause
