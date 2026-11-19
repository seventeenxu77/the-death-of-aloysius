@echo off
cd /d "%~dp0"
start "" "%~dp0Builds\SerpentChapter\AloysiusLevel1.exe" -boss -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -logFile "%~dp0Logs\SerpentChapter.log"
