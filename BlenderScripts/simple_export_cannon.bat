@echo off
REM simple_export_cannon.bat
REM Export simple du canon vers E:\FPSLowPoly\BlenderScripts\

echo ================================================
echo Export Simple - Canon Napoleonien
echo ================================================
echo.

set BLENDER_PATH="E:\SteamLibrary\steamapps\common\Blender\5.0\blender.exe"
set SCRIPT_PATH="E:\FPSLowPoly\BlenderScripts\simple_export_cannon.py"

if not exist %BLENDER_PATH% (
    echo ERREUR: Blender pas trouve!
    echo Modifiez BLENDER_PATH dans ce script
    pause
    exit /b 1
)

echo Blender: %BLENDER_PATH%
echo Script: %SCRIPT_PATH%
echo.

mkdir "E:\FPSLowPoly\BlenderScripts" 2>nul

echo Export en cours...
%BLENDER_PATH% --background --python %SCRIPT_PATH%

if %errorlevel% neq 0 (
    echo.
    echo ERREUR lors de l'export!
    pause
    exit /b 1
)

echo.
echo ================================================
echo SUCCES! Canon exporte vers:
echo E:\FPSLowPoly\BlenderScripts\canon_12_pounder.fbx
echo ================================================
echo.

pause
