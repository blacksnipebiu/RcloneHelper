@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..
set OUTPUT_DIR=%SCRIPT_DIR%publish
set ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe

echo ========================================
echo RcloneHelper Lite Build Script
echo Builds installer (RcloneHelper only)
echo ========================================
echo.

:: Clean old publish files
if exist "%OUTPUT_DIR%" (
    echo Cleaning old publish files...
    rmdir /s /q "%OUTPUT_DIR%"
)
mkdir "%OUTPUT_DIR%"

echo [Step 1/2] Publishing application...
echo.

dotnet publish "%PROJECT_ROOT%\RcloneHelper\RcloneHelper.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o "%OUTPUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo.
echo Removing PDB files...
del /q "%OUTPUT_DIR%\*.pdb" 2>nul

echo.
echo [Step 2/2] Building lite installer...
echo.

if not exist "%ISCC_PATH%" (
    echo [ERROR] Inno Setup not found!
    echo Download from: https://jrsoftware.org/isdownload.php
    pause
    exit /b 1
)

"%ISCC_PATH%" "%SCRIPT_DIR%setup-lite.iss"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Installer build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed!
echo Output: %SCRIPT_DIR%
echo ========================================
echo.

dir "%SCRIPT_DIR%*.exe" /b 2>nul | findstr /v "build-full build-lite"

pause