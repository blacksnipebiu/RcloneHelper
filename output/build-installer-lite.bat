@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set ISS_FILE=%SCRIPT_DIR%setup-lite.iss
set ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe

echo ========================================
echo RcloneHelper Lite Installer Build Script
echo ========================================
echo This script builds a LITE installer that
echo does NOT include WinFsp and rclone.
echo ========================================
echo.

if not exist "%SCRIPT_DIR%publish\RcloneHelper.exe" (
    echo [ERROR] Publish files not found!
    echo Please run publish.bat first.
    pause
    exit /b 1
)

if not exist "%ISCC_PATH%" (
    echo [ERROR] Inno Setup not found!
    echo Download from: https://jrsoftware.org/isdownload.php
    pause
    exit /b 1
)

echo.
echo Building LITE installer...
echo Script: %ISS_FILE%
echo.

"%ISCC_PATH%" "%ISS_FILE%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Lite installer build completed!
echo Output: %SCRIPT_DIR%
echo ========================================
echo.

dir "%SCRIPT_DIR%*Lite*.exe" /b 2>nul

pause