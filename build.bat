@echo off
cd /d "%~dp0"

echo =============================================================
echo   1. Force-Killing any running Tangerine instances...
echo =============================================================
powershell -Command "Get-Process -Name 'TangerineGlass','TangerineWin' -ErrorAction SilentlyContinue | Stop-Process -Force" >nul 2>&1
timeout /t 1 /nobreak >nul

echo.
echo =============================================================
echo   2. Removing old CyberSec files if present...
echo =============================================================
if exist "CyberSecurityToolkit.cs" del /f /q "CyberSecurityToolkit.cs"
if exist "CyberSecWindow.xaml" del /f /q "CyberSecWindow.xaml"
if exist "CyberSecWindow.xaml.cs" del /f /q "CyberSecWindow.xaml.cs"
if exist "PasswordDialog.xaml" del /f /q "PasswordDialog.xaml"
if exist "PasswordDialog.xaml.cs" del /f /q "PasswordDialog.xaml.cs"
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

echo.
echo =============================================================
echo   3. Building Tangerine v17 Clean Edition...
echo =============================================================
dotnet build -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo =========================================================
    echo   [ERROR] Build failed! Read the error message above!
    echo =========================================================
    pause
    exit /b
)

echo.
echo =============================================================
echo   4. Launching TangerineGlass.exe...
echo =============================================================
start "" "bin\Release\net8.0-windows\TangerineGlass.exe"
echo.
echo [SUCCESS] Tangerine v17 Clean Edition is now running!
timeout /t 2 /nobreak >nul
