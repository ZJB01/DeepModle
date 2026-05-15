@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion
echo === Register CubeAddIn ===
echo.

set DLL=%CD%\bin\Debug\net48\SwCubeAddIn.dll
set GUID={B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C}

REM Check admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ========================================
    echo NOT running as Administrator!
    echo COM registration WILL fail.
    echo Please right-click this file ^> Run as Administrator
    echo ========================================
    pause
    exit /b 1
)

REM Verify DLL
if not exist "%DLL%" (
    echo ERROR: DLL not found: %DLL%
    pause
    exit /b 1
)
echo DLL: %DLL%
echo.

REM 1. COM Registration
echo [1/2] COM registration...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /codebase "%DLL%"
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo regasm FAILED with code %errorlevel%
    echo ========================================
    pause
    exit /b 1
)
echo COM registration OK.

REM Verify CLSID was written
echo Verifying CLSID...
reg query "HKCR\CLSID\%GUID%" >nul 2>&1
if %errorlevel% neq 0 (
    reg query "HKCR\Wow6432Node\CLSID\%GUID%" >nul 2>&1
    if %errorlevel% neq 0 (
        echo WARNING: CLSID not found in registry. COM registration may be incomplete.
    )
)
echo.

REM 2. SolidWorks registry (both HKCU and HKLM)
echo [2/2] SolidWorks registry keys...
for %%H in (HKCU HKLM) do (
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /f >nul 2>&1
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /ve /d "1" /f >nul 2>&1
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /v "Title" /t REG_SZ /d "CubeAddIn" /f >nul 2>&1
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /v "Description" /t REG_SZ /d "Generate a 100mm cube" /f >nul 2>&1
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /v "CLSID" /t REG_SZ /d "%GUID%" /f >nul 2>&1
    reg add "%%H\Software\SolidWorks\AddIns\%GUID%" /v "LoadBehavior" /t REG_DWORD /d 3 /f >nul 2>&1
)

reg query "HKCU\Software\SolidWorks\AddIns\%GUID%" >nul 2>&1
echo Final HKCU registry:
reg query "HKCU\Software\SolidWorks\AddIns\%GUID%"
echo.

echo === SUCCESS ===
echo Log will be at: %%LOCALAPPDATA%%\SwCubeAddIn.log
echo.
echo 1. Close SolidWorks if running
echo 2. Start SolidWorks
echo 3. Open a PART document
echo 4. Check log file
pause
