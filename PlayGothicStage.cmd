@echo off
setlocal
cd /d "%~dp0"
set "GOTHIC_GAME=%~dp0Builds\GothicBride2D\AloysiusGothic.exe"
if not exist "%GOTHIC_GAME%" goto missing
start "" "%GOTHIC_GAME%" -screen-fullscreen 0 -screen-width 1440 -screen-height 810
exit /b 0
:missing
echo Game build was not found: Builds\GothicBride2D\AloysiusGothic.exe
pause
exit /b 1
