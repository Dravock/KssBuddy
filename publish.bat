@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo KSS Buddy wird als EXE gebaut ...
echo.
rem App beenden, falls sie noch laeuft (sonst ist die EXE gesperrt)
taskkill /F /IM KssBuddy.exe >nul 2>&1

dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0dist"
if errorlevel 1 (
    echo.
    echo FEHLER beim Bauen. Bitte die Meldung oben kopieren.
    pause
    exit /b 1
)

echo.
echo Fertig: %~dp0dist\KssBuddy.exe
explorer "%~dp0dist"
pause
