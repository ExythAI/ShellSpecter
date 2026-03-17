@echo off
title ShellSpecter Launcher
echo.
echo  ==============================
echo   ShellSpecter - System Observatory
echo  ==============================
echo.

:: Kill any stale instances
taskkill /IM ShellSpecter.Specter.exe /F >nul 2>&1

echo Starting Specter daemon (port 5050)...
start "Specter Daemon" dotnet run --project "%~dp0src\ShellSpecter.Specter"

timeout /t 4 /nobreak >nul

echo Starting Seer dashboard (port 5051)...
start "Seer Dashboard" dotnet run --project "%~dp0src\ShellSpecter.Seer" --urls "http://localhost:5051"

timeout /t 6 /nobreak >nul

echo.
echo Opening dashboard in browser...
start http://localhost:5051

echo.
echo Both services are running in separate windows.
echo Login with any username/password (mock mode).
echo Close the terminal windows to stop.
echo.
pause
