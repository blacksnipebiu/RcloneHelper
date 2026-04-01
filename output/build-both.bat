@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0

echo ========================================
echo RcloneHelper Build Script
echo ========================================
echo This will build BOTH installers:
echo   1. Full installer (with WinFsp + rclone)
echo   2. Lite installer (RcloneHelper only)
echo ========================================
echo.

echo [Step 1/3] Publishing application...
echo.
call "%SCRIPT_DIR%publish.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Publish step failed!
    exit /b 1
)

echo.

echo [Step 2/3] Building FULL installer (with WinFsp + rclone)...
echo.
call "%SCRIPT_DIR%build-installer.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Full installer step failed!
    exit /b 1
)

echo.

echo [Step 3/3] Building LITE installer (RcloneHelper only)...
echo.
call "%SCRIPT_DIR%build-installer-lite.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Lite installer step failed!
    exit /b 1
)

echo.
echo ========================================
echo Build completed!
echo Output: %SCRIPT_DIR%
echo.
echo Generated files:
echo   - RcloneHelperv1.0.0.exe (Full with WinFsp + rclone)
echo   - RcloneHelperv1.0.0-Lite.exe (RcloneHelper only)
echo ========================================
echo.

dir "%SCRIPT_DIR%*.exe" /b 2>nul

pause