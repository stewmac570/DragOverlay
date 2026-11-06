@echo off
rem Records the Castle Link and shows each tune change as you write it.
rem Double-click it and say Yes to the Windows prompt.
net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
for /f %%t in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HHmmss"') do set STAMP=%%t
set FOLDER=%~dp0..\..\Tests\castle-link-captures\live_%STAMP%
"C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap2 -A -o - | python -I "%~dp0castle_tune_live.py" "%FOLDER%"
pause
