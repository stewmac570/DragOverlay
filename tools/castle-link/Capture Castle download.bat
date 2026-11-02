@echo off
rem Records the USB conversation while Castle Link 2 downloads a log.
rem Double-click it and say Yes to the Windows prompt.
net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0capture-castle-download.ps1"
pause
