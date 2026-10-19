$ErrorActionPreference = 'Stop'
try {
    $destination = Join-Path $env:LOCALAPPDATA 'Programs\BFME2TestLab'
    $names = @('BFME2-Test-Lab.exe','Apply-Package.ps1','baseline.json','BFME2-TestPackage.zip','README.txt')
    foreach ($name in $names) {
        if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name) -PathType Leaf)) { throw "Extract the entire ZIP first. Missing $name" }
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($name in $names) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $destination $name) -Force
    }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
        $shortcut = $shell.CreateShortcut((Join-Path $folder 'BFME2 Test Lab.lnk'))
        $shortcut.TargetPath = Join-Path $destination 'BFME2-Test-Lab.exe'
        $shortcut.WorkingDirectory = $destination
        $shortcut.Description = 'Install, restore and update BFME2 test builds'
        $shortcut.Save()
    }
    Write-Host "Installed to $destination"
    Write-Host 'Open BFME2 Test Lab from your desktop or Start menu.'
    exit 0
} catch { Write-Host "ERROR: $($_.Exception.Message)"; exit 1 }
