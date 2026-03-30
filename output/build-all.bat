@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0

echo ========================================
echo RcloneHelper Build All Script
echo ========================================
echo.

echo [Step 1/2] Publishing application...
echo.
call "%SCRIPT_DIR%publish.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Publish step failed!
    exit /b 1
)

echo.

echo [Step 2/2] Building installer...
echo.
call "%SCRIPT_DIR%build-installer.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Installer step failed!
    exit /b 1
)

echo.
echo ========================================
echo Build completed!
echo Output: %SCRIPT_DIR%
echo ========================================
echo.

dir "%SCRIPT_DIR%*.exe" /b 2>nul

pause
