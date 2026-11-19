@echo off
setlocal
cd /d "%~dp0"
set "PALACE_ROUTE_GAME=%~dp0Builds\Level1Bedchamber\AloysiusLevel1.exe"
if not exist "%PALACE_ROUTE_GAME%" goto missing
start "" "%PALACE_ROUTE_GAME%" -palace-route -screen-fullscreen 0 -screen-width 1440 -screen-height 810 -logFile "%~dp0Logs\PalaceRoutePlayer.log"
exit /b 0
:missing
echo Palace route build is not available yet.
pause
exit /b 1
