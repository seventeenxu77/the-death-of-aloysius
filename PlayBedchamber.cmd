@echo off
setlocal
cd /d "%~dp0"
set "BEDCHAMBER_GAME=%~dp0Builds\Level1Bedchamber\AloysiusLevel1.exe"
if not exist "%BEDCHAMBER_GAME%" goto missing
start "" "%BEDCHAMBER_GAME%" -bedchamber -screen-fullscreen 0 -screen-width 1440 -screen-height 810 -logFile "%~dp0Logs\BedchamberPlayer.log"
exit /b 0
:missing
echo Bedchamber build is not available yet.
pause
exit /b 1
