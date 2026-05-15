$guid = "B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C"
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== Force clean CubeAddIn ==="

# 1. Unblock DLL
$dll = Join-Path $PSScriptRoot "bin\Debug\net48\SwCubeAddIn.dll"
if (Test-Path $dll) {
    Unblock-File -Path $dll
    Write-Host "[1] DLL unblocked"
}

# 2. Remove AddIns keys from all locations
$paths = @(
    "HKCU:\Software\SolidWorks\AddIns\{$guid}",
    "HKLM:\Software\SolidWorks\AddIns\{$guid}",
    "HKLM:\Software\SolidWorks\SOLIDWORKS 2023\AddIns\{$guid}",
    "HKLM:\Software\WOW6432Node\SolidWorks\AddIns\{$guid}"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Force -Recurse
        Write-Host "[2] Removed: $p"
    }
}

# 3. Deep search for any reference to this GUID in SolidWorks keys
Write-Host "[3] Searching for $guid in registry..."
$roots = @("HKCU:\Software\SolidWorks", "HKLM:\Software\SolidWorks", "HKLM:\Software\WOW6432Node\SolidWorks")
foreach ($root in $roots) {
    Get-ChildItem -Path $root -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.PSChildName -like "*$guid*" -or $_.PSChildName -like "*CubeAddIn*"
    } | ForEach-Object {
        $path = $_.PSPath
        Write-Host "  Found: $path"
        Remove-Item -Path $path -Force -Recurse
        Write-Host "  Deleted"
    }
}

# 4. Check for failed-load blacklist
$sw2023 = "HKCU:\Software\SolidWorks\SOLIDWORKS 2023"
if (Test-Path $sw2023) {
    Get-ChildItem -Path $sw2023 -Directory | ForEach-Object {
        $sub = $_.PSPath
        Get-ChildItem -Path $sub -Recurse -ErrorAction SilentlyContinue | Where-Object {
            $_.PSChildName -match $guid -or $_.PSChildName -match "CubeAddIn"
        } | ForEach-Object {
            Write-Host "  Found blacklist: $($_.PSPath)"
            Remove-Item -Path $_.PSPath -Force -Recurse
        }
    }
}

Write-Host ""
Write-Host "=== Clean complete. Now run register.bat ==="
pause
