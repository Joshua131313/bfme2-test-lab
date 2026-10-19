$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'version.txt') -Raw).Trim()
$out = Join-Path $root "build\BFME2-Test-Lab-$version"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /out:"$out\BFME2-Test-Lab.exe" /win32manifest:"$root\src\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$root\src\Program.cs" "$root\src\AppUpdate.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath "$root\src\Apply-Package.ps1","$root\src\baseline.json","$root\src\Install-App.ps1","$root\src\Install.cmd","$root\README.txt" -Destination $out -Force
$package = Join-Path (Split-Path $root -Parent) 'dist\BFME2-1.00-Hybrid-Test04.zip'
Copy-Item -LiteralPath $package -Destination "$out\BFME2-TestPackage.zip" -Force
Write-Output "Built $out"
