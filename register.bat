@echo off
cd /d "%~dp0"
echo === Register DeepModel AddIn ===

set DLL=%CD%\bin\Debug\net48\DeepModel.dll
set GUID={D9E2F1A4-8B7C-6E5D-3F2A-1C0B9D8E7F6A}

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Run as Administrator!
    pause & exit /b 1
)
if not exist "%DLL%" ( echo DLL not found: %DLL% & pause & exit /b 1 )

echo [1/2] COM register...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /codebase "%DLL%"
if %errorlevel% neq 0 ( echo FAILED & pause & exit /b 1 )

echo [2/2] SW registry...
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /f
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /ve /d "1" /f
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /v "Title" /t REG_SZ /d "DeepModel" /f
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /v "Description" /t REG_SZ /d "Parametric modeling add-in with Agent pipe" /f
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /v "CLSID" /t REG_SZ /d "%GUID%" /f
reg add "HKCU\Software\SolidWorks\AddIns\%GUID%" /v "LoadBehavior" /t REG_DWORD /d 3 /f
reg add "HKLM\Software\SolidWorks\AddIns\%GUID%" /f >nul 2>&1
reg add "HKLM\Software\SolidWorks\AddIns\%GUID%" /v "CLSID" /t REG_SZ /d "%GUID%" /f >nul 2>&1
reg add "HKLM\Software\SolidWorks\AddIns\%GUID%" /v "LoadBehavior" /t REG_DWORD /d 3 /f >nul 2>&1

echo Done. Restart SolidWorks.
pause
