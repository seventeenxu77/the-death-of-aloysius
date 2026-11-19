@echo off
setlocal
cd /d "%~dp0"
set "LEVEL1_GAME=%~dp0Builds\Level1\AloysiusLevel1.exe"
if exist "%~dp0Builds\Level1Bedchamber\AloysiusLevel1.exe" set "LEVEL1_GAME=%~dp0Builds\Level1Bedchamber\AloysiusLevel1.exe"
if not exist "%LEVEL1_GAME%" goto missing
start "" "%LEVEL1_GAME%" -screen-fullscreen 0 -screen-width 1440 -screen-height 810 -logFile "%~dp0Logs\Level1\Player.log"
exit /b 0
:missing
echo Level1 build was not found: Builds\Level1\AloysiusLevel1.exe
pause
exit /b 1
