@echo off
setlocal EnableDelayedExpansion

set PROJECT_FOLDER=%~dp0..\PostmanOpenAPIConverter
set PROJECT=%PROJECT_FOLDER%\PostmanOpenAPIConverter.csproj
set SCRIPT=%~dp0p2oa.nsi

where makensis >nul 2>&1
if errorlevel 1 (
    echo ERROR: makensis not found. Install NSIS from https://nsis.sourceforge.io
    exit /b 1
)

echo.
echo [1/2] Publishing p2oa...
echo.

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained -p:DebugType=none -p:DebugSymbols=false -o:"%PROJECT_FOLDER%\bin\publish"

if errorlevel 1 (
    echo.
    echo ERROR: dotnet publish failed.
    exit /b 1
)

echo.
echo [2/2] Building installer...
echo.

makensis "%SCRIPT%"

if errorlevel 1 (
    echo.
    echo ERROR: makensis failed.
    exit /b 1
)

echo.
echo Done. Installer: %~dp0p2oa-setup.exe
echo.
