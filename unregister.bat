@echo off
cd /d "%~dp0"
echo === Unregister DeepModel ===

set DLL=%CD%\bin\Debug\net48\DeepModel.dll
set GUID={D9E2F1A4-8B7C-6E5D-3F2A-1C0B9D8E7F6A}

reg delete "HKCU\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
reg delete "HKLM\Software\SolidWorks\AddIns\%GUID%" /f 2>nul
if exist "%DLL%" C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /unregister "%DLL%" 2>nul
del /f "%LOCALAPPDATA%\DeepModel_Pipe.log" 2>nul
echo Done.
pause
