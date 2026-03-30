@echo off
setlocal enabledelayedexpansion

set PROJECT_ROOT=%~dp0..
set OUTPUT_DIR=%~dp0publish

echo ========================================
echo RcloneHelper Publish Script
echo ========================================

if exist "%OUTPUT_DIR%" (
    echo Cleaning old publish files...
    rmdir /s /q "%OUTPUT_DIR%"
)

mkdir "%OUTPUT_DIR%"

echo.
echo Publishing application...
echo Output: %OUTPUT_DIR%
echo.

dotnet publish "%PROJECT_ROOT%\RcloneHelper\RcloneHelper.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o "%OUTPUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Publish completed!
echo Output: %OUTPUT_DIR%
echo ========================================
echo.

dir "%OUTPUT_DIR%" /b

pause
