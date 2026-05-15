@echo off
cd /d "%~dp0"
echo === Unregister CubeAddIn ===

set GUID={B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C}
set DLL=%CD%\bin\Debug\net48\SwCubeAddIn.dll

echo [1/3] Removing SW registry keys...
reg delete "HKCU\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
reg delete "HKLM\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
echo Done.

echo [2/3] Unregistering COM...
if exist "%DLL%" C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /unregister "%DLL%" 2>nul
echo Done.

echo [3/3] Delete log...
del /f "%LOCALAPPDATA%\SwCubeAddIn.log" 2>nul
echo Done.

echo Complete.
pause
