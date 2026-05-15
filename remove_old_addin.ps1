<#
  One-shot script to completely remove old "SwCubeAddIn" add-in.
  Run as Administrator.
  Use -WhatIf to preview without making changes.
#>
param([switch]$WhatIf)

$oldGuid = "B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C"
$oldDll = Join-Path $PSScriptRoot "bin\Debug\net48\SwCubeAddIn.dll"
$ErrorActionPreference = "SilentlyContinue"

function Remove-Key {
    param([string]$Path)
    if (Test-Path $Path) {
        Write-Host "  DELETE: $Path"
        if (-not $WhatIf) { Remove-Item -Path $Path -Force -Recurse }
    }
}

Write-Host "=== Deep Clean Old AddIn (SwCubeAddIn) ==="
Write-Host "GUID: $oldGuid"
Write-Host ""

# 1. COM unregister
Write-Host "[1/5] COM unregister..."
if (Test-Path $oldDll) {
    $regasm = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    if (-not $WhatIf) { & $regasm /unregister $oldDll 2>&1 | Out-Null }
    Write-Host "  regasm /unregister done"
} else {
    Write-Host "  DLL not found, skipping"
}

# 2. Known registry keys
Write-Host "[2/5] Removing known SW AddIn registry keys..."
$addinPaths = @(
    "HKCU:\Software\SolidWorks\AddIns\{$oldGuid}",
    "HKLM:\Software\SolidWorks\AddIns\{$oldGuid}",
    "HKLM:\Software\SolidWorks\SOLIDWORKS 2023\AddIns\{$oldGuid}",
    "HKLM:\Software\WOW6432Node\SolidWorks\AddIns\{$oldGuid}"
)
foreach ($p in $addinPaths) { Remove-Key $p }

# 3. CLSID
Write-Host "[3/5] Removing CLSID..."
Remove-Key "HKCR:\CLSID\{$oldGuid}"
Remove-Key "HKCR:\Wow6432Node\CLSID\{$oldGuid}"

# 4. Deep search for any remaining references
Write-Host "[4/5] Deep registry search..."
$searchRoots = @(
    "HKCU:\Software\SolidWorks",
    "HKLM:\Software\SolidWorks",
    "HKLM:\Software\WOW6432Node\SolidWorks"
)
$searchTerms = @($oldGuid, "SwCubeAddIn", "CubeAddIn")

foreach ($root in $searchRoots) {
    if (-not (Test-Path $root)) { continue }
    $items = Get-ChildItem -Path $root -Recurse -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        $name = $item.PSChildName
        $match = $false
        foreach ($t in $searchTerms) {
            if ($name -like "*$t*") { $match = $true; break }
        }
        if ($match) {
            $path = $item.PSPath
            Write-Host "  DELETE: $path"
            if (-not $WhatIf) { Remove-Item -Path $path -Force -Recurse }
        }
    }
}

# 5. Log files
Write-Host "[5/5] Cleaning log files..."
$logs = @(
    "$env:LOCALAPPDATA\SwCubeAddIn.log",
    "$env:LOCALAPPDATA\SwCubeAddIn_Pipe.log"
)
foreach ($l in $logs) {
    if (Test-Path $l) {
        Write-Host "  DELETE: $l"
        if (-not $WhatIf) { Remove-Item $l -Force }
    }
}

Write-Host ""
Write-Host "=== Cleanup complete ==="
Write-Host "You may manually delete the old DLL: $oldDll"

if ($WhatIf) {
    Write-Host ""
    Write-Host "** WHATIF mode - no changes made. Run without -WhatIf to execute. **"
}
