@echo off
cd /d "%~dp0"
echo === FORCE CLEAN ALL CubeAddIn traces ===
set GUID={B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C}
echo.

echo [1] Unblock DLL...
powershell -Command "Unblock-File -Path '%CD%\bin\Debug\net48\SwCubeAddIn.dll'" 2>nul
echo Done.

echo [2] Delete HKCU AddIns key...
reg delete "HKCU\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
echo Done.

echo [3] Delete HKLM AddIns key...
reg delete "HKLM\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
reg delete "HKLM\Software\SolidWorks\SOLIDWORKS 2023\AddIns\%GUID%" /f 2>nul
echo Done.

echo [4] Search and destroy ALL SolidWorks references to this GUID...
for %%R in (
    "HKCU\Software\SolidWorks"
    "HKLM\Software\SolidWorks"
    "HKLM\Software\WOW6432Node\SolidWorks"
) do (
    reg query %%R /s /f "%GUID%" >nul 2>&1
    if not errorlevel 1 (
        echo Found in %%R - deleting specific keys...
        for /f "delims=" %%K in ('reg query %%R /s /f "%GUID%" /k 2^>nul ^| findstr /i "HKEY_"') do (
            echo   Deleting: %%K
            reg delete "%%K" /f 2>nul
        )
    )
)
echo Done.

echo [5] Also search for CubeAddIn by name...
for %%R in (
    "HKCU\Software\SolidWorks"
    "HKLM\Software\SolidWorks"
) do (
    for /f "delims=" %%K in ('reg query %%R /s /f "CubeAddIn" /k 2^>nul ^| findstr /i "HKEY_"') do (
        echo   Deleting: %%K
        reg delete "%%K" /f 2>nul
    )
)
echo Done.

echo.
echo === FORCE CLEAN complete. Now run register.bat ===
pause
