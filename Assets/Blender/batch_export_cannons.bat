@echo off
REM batch_export_cannons.bat
REM Run this script to generate and export all cannon types from Blender to Unity
REM Usage: Double-click this file or run from command line

echo ================================================
echo Napoleonic Cannon Generator for Unity
echo ================================================
echo.

set BLENDER_PATH="E:\SteamLibrary\steamapps\common\Blender\5.0\blender.exe"
set SCRIPT_PATH="E:\FPSLowPoly\Assets\Blender\napoleonic_cannon_generator.py"
set UNITY_PATH="E:\FPSLowPoly"

echo Checking Blender installation...
if not exist %BLENDER_PATH% (
    echo ERROR: Blender not found at %BLENDER_PATH%
    echo Please update BLENDER_PATH in this script
    pause
    exit /b 1
)

echo Blender found: %BLENDER_PATH%
echo.

echo Creating export directory...
if not exist "%UNITY_PATH%\Assets\Models\Cannons" (
    mkdir "%UNITY_PATH%\Assets\Models\Cannons"
    echo Created: %UNITY_PATH%\Assets\Models\Cannons
)

echo.
echo ================================================
echo Generating 12-Pounder Cannon (Heavy Field Artillery)
echo ================================================
echo.

%BLENDER_PATH% --background --python %SCRIPT_PATH%

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Cannon generation failed!
    pause
    exit /b 1
)

echo.
echo ================================================
echo SUCCESS! Cannons exported to:
echo %UNITY_PATH%\Assets\Models\Cannons\
echo ================================================
echo.
echo Next steps:
echo 1. Open Unity project at %UNITY_PATH%
echo 2. The cannons will be auto-imported with correct settings
echo 3. Go to "Napoleonic Wars -^> Cannons -^> Create Cannon Prefabs" in Unity menu
echo.

pause
