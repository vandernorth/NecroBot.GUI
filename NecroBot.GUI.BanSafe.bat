@echo off

start NecroBot.GUI.exe

set hr=%time:~0,2%
if "%hr:~0,1%" equ " " set hr=0%hr:~1,1%

cd logs
if not exist Old_Logs_%date:~6,4%%date:~3,2%%date:~0,2%_%hr%.%time:~3,2%.%time:~6,2% (
  mkdir Old_Logs_%date:~6,4%%date:~3,2%%date:~0,2%_%hr%.%time:~3,2%.%time:~6,2%
)
cd %%~dp0\..\
move *.txt Old_Logs_%date:~6,4%%date:~3,2%%date:~0,2%_%hr%.%time:~3,2%.%time:~6,2%
:loop
for /f "delims=" %%a in ('type *.txt ^| find /C "CatchSuccess"') do @set /a catches=%%a
for /f "delims=" %%a in ('type *.txt ^| find /C "] Name: "') do @set /a stops=%%a
echo Pokemon: %catches%
echo PokeStop: %stops%
echo [%date%_%time%] Pokemon: %catches% PokeStop: %stops% >> CountSinceStart_%date:~6,4%%date:~3,2%%date:~0,2%_%hr%.txt

IF /I %catches% GEQ 950 (
  taskkill /im NecroBot.GUI.exe
  exit
)

IF /I %stops% GEQ 1900 (
  taskkill /im NecroBot.GUI.exe
  exit
)
timeout 10
cls
goto loop