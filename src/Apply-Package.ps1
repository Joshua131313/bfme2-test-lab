param(
    [ValidateSet('Validate', 'Install', 'Restore')]
    [string]$Action = 'Validate',
    [string]$GameDirectory = '',
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [Parameter(Mandatory=$true)][string]$CacheDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-BfmeFolder([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    return ((Test-Path -LiteralPath (Join-Path $Path 'game.dat') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $Path 'INI.big') -PathType Leaf))
}

function Resolve-BfmeFolder([string]$Requested) {
    if ([string]::IsNullOrWhiteSpace($Requested)) {
        $candidates = @()
        $programRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { $_ } | Select-Object -Unique
        foreach ($programRoot in $programRoots) {
            foreach ($name in @('The Battle for Middle-earth (tm) II', 'The Battle for Middle-earth II')) {
                $candidate = Join-Path (Join-Path $programRoot 'Electronic Arts') $name
                if (Test-BfmeFolder $candidate) { $candidates += $candidate }
            }
        }
        $candidates = @($candidates | Select-Object -Unique)
        if ($candidates.Count -eq 1) { $Requested = $candidates[0] }
        else {
            if ($candidates.Count -gt 1) {
                Write-Host 'More than one BFME2 folder was found:'
                foreach ($candidate in $candidates) { Write-Host "  $candidate" }
            } else { Write-Host 'BFME2 was not found in the usual installation folders.' }
            $Requested = Read-Host 'Paste the full BFME2 folder path containing game.dat and INI.big'
        }
    }
    $Requested = $Requested.Trim().Trim('"')
    if (-not (Test-BfmeFolder $Requested)) {
        throw "BFME2 files were not found in '$Requested'. Supply the folder containing both game.dat and INI.big. No game files changed."
    }
    return (Resolve-Path -LiteralPath $Requested).ProviderPath
}

function Get-BytesHash([byte[]]$Bytes) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-PathHash([string]$Path) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

function Convert-HexBytes([string]$Hex) {
    if ($Hex.Length % 2 -ne 0 -or $Hex -match '[^0-9a-fA-F]') { throw 'Invalid patch byte encoding.' }
    $result = New-Object byte[] ($Hex.Length / 2)
    for ($i = 0; $i -lt $result.Length; $i++) { $result[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16) }
    return ,$result
}

function Get-PatchedBytes([byte[]]$Original, $Record) {
    if ((Get-BytesHash $Original) -ne $Record.original_sha256) { throw 'Original verification failed.' }
    [byte[]]$result = [IO.File]::ReadAllBytes((Join-Path (Join-Path $PackageDirectory 'payload') $Record.name))
    if ($result.Length -ne $Record.patched_size -or (Get-BytesHash $result) -ne $Record.patched_sha256) { throw 'Payload verification failed.' }
    return ,$result
}

$staged = New-Object 'System.Collections.Generic.List[string]'
$committed = New-Object 'System.Collections.Generic.List[object]'
try {
    $manifest = Get-Content -LiteralPath (Join-Path $PackageDirectory 'patch.json') -Raw | ConvertFrom-Json
    if ($manifest.format -ne 2 -or -not $manifest.release.StartsWith('BFME2 1.00 ')) { throw 'Unexpected patch manifest.' }
    $names = @($manifest.files | ForEach-Object { $_.name })
    if ($names.Count -ne 3 -or ($names -join '|') -cne 'game.dat|INI.big|lotrbfme2.exe') { throw 'Unexpected patch file list.' }
    $trusted = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'baseline.json') -Raw | ConvertFrom-Json
    if (@($manifest.baseline).Count -ne @($trusted.baseline).Count) { throw 'Incomplete baseline.' }
    for ($i = 0; $i -lt 3; $i++) {
        if ($manifest.files[$i].original_sha256 -ne $trusted.files[$i].original_sha256 -or $manifest.files[$i].original_size -ne $trusted.files[$i].original_size) { throw 'Unsupported original version.' }
        if ($manifest.files[$i].patched_size -lt 1 -or $manifest.files[$i].patched_size -gt 100000000 -or $manifest.files[$i].patched_sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid payload record.' }
    }
    for ($i = 0; $i -lt @($trusted.baseline).Count; $i++) {
        if ($manifest.baseline[$i].name -cne $trusted.baseline[$i].name -or $manifest.baseline[$i].sha256 -ne $trusted.baseline[$i].sha256) { throw 'Baseline differs from original 1.00.' }
    }
    $knownHashes = @{}
    foreach ($fileName in $names) { $knownHashes[$fileName] = @() }
    foreach ($cached in @(Get-ChildItem -LiteralPath $CacheDirectory -Directory -ErrorAction SilentlyContinue)) {
        $cachedManifest = Join-Path $cached.FullName 'patch.json'
        if (-not (Test-Path -LiteralPath $cachedManifest)) { continue }
        try {
            $previous = Get-Content -LiteralPath $cachedManifest -Raw | ConvertFrom-Json
            if ($previous.format -ne 2 -or @($previous.files).Count -ne 3) { continue }
            for ($i = 0; $i -lt 3; $i++) {
                $f = $previous.files[$i]
                if ($f.name -cne $names[$i] -or $f.original_sha256 -ne $trusted.files[$i].original_sha256) { continue }
                $previousPayload = Join-Path (Join-Path $cached.FullName 'payload') $f.name
                if ((Test-Path -LiteralPath $previousPayload) -and (Get-PathHash $previousPayload) -eq $f.patched_sha256) { $knownHashes[$f.name] += $f.patched_sha256 }
            }
        } catch { continue }
    }
    $root = Resolve-BfmeFolder $GameDirectory
    Write-Host "Game folder: $root"
    $backupRoot = Join-Path $root '_BFME2_TestLab_Backup'
    if ($Action -ne 'Restore') {
        foreach ($base in $manifest.baseline) {
            if ([IO.Path]::IsPathRooted($base.name) -or $base.name -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Unsafe baseline path.' }
            $basePath = Join-Path $root $base.name
            if ($base.name -ceq 'Maps.big') {
                if (-not (Test-Path -LiteralPath $basePath -PathType Leaf)) { throw 'Maps.big is missing. Repair the game installation first.' }
                $mapStream = [IO.File]::OpenRead($basePath)
                try {
                    $header = New-Object byte[] 16
                    if ($mapStream.Read($header, 0, 16) -ne 16) { throw 'Maps.big is truncated. Repair the game installation first.' }
                    $magic = [Text.Encoding]::ASCII.GetString($header, 0, 4)
                    if ($magic -ne 'BIGF' -and $magic -ne 'BIG4') { throw 'Maps.big is not a recognized BIG archive. Repair the game installation first.' }
                    [long]$indexEnd = ([long]$header[12] * 16777216) + ([long]$header[13] * 65536) + ([long]$header[14] * 256) + $header[15]
                    if ($indexEnd -lt 16 -or $indexEnd -gt $mapStream.Length) { throw 'Maps.big has an invalid archive header. Repair the game installation first.' }
                } finally { $mapStream.Dispose() }
                if ((Get-PathHash $basePath) -ne $base.sha256) {
                    Write-Warning 'Maps.big differs from the reference 1.00 copy. Keeping your maps unchanged; map-specific gameplay and multiplayer compatibility are not verified.'
                }
                continue
            }
            if (-not (Test-Path -LiteralPath $basePath) -or (Get-PathHash $basePath) -ne $base.sha256) {
                throw "Base data is not verified 1.00: $($base.name). Select 1.00 in the switcher first."
            }
        }
        $overrides = @(Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Name -match '(?i)(BT2DC.*\.big$|^_patch.*\.big$)' })
        if ($overrides.Count -gt 0) { throw 'Later-patch override archives are present. Select 1.00 in the switcher first.' }
    }
    $items = @()
    foreach ($record in $manifest.files) {
        $path = Join-Path $root $record.name
        [byte[]]$current = [System.IO.File]::ReadAllBytes($path)
        $currentHash = Get-BytesHash $current
        if ($currentHash -ne $record.original_sha256 -and $currentHash -ne $record.patched_sha256 -and $knownHashes[$record.name] -notcontains $currentHash) {
            throw "Unsupported or changed $($record.name). Select the original 1.00 version in your patch switcher first. No game files were changed."
        }
        $isPatched = $currentHash -ne $record.original_sha256
        Write-Host "$($record.name): $(if ($isPatched) { if ($currentHash -eq $record.patched_sha256) { $manifest.release } else { 'recognized previous test build' } } else { 'verified original 1.00' })"
        $items += [pscustomobject]@{ Record=$record; Path=$path; Current=$current; Hash=$currentHash; IsPatched=$isPatched; Backup=(Join-Path $backupRoot ($record.name + '.original')) }
    }
    if ($Action -eq 'Validate') { Write-Host 'Validation complete. No files changed.'; exit 0 }
    if (@(Get-Process -Name 'game', 'lotrbfme2' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Close BFME2 and its launcher before installing or restoring this test patch.'
    }
    if ($Action -eq 'Restore' -and @($items | Where-Object { $_.IsPatched }).Count -eq 0) {
        Write-Host 'All files are already original 1.00. Nothing to restore.'; exit 0
    }
    # Validate every backup and construct every result before writing either game file.
    $plans = @()
    foreach ($item in $items) {
        [byte[]]$original = $null
        if (Test-Path -LiteralPath $item.Backup) {
            $original = [System.IO.File]::ReadAllBytes($item.Backup)
            if ((Get-BytesHash $original) -ne $item.Record.original_sha256) { throw "Backup verification failed: $($item.Record.name)" }
        } elseif ($item.IsPatched) {
            foreach ($oldBackupName in @('_BFME2_100_Hybrid04_Backup','_BFME2_100_Hybrid03_Backup','_BFME2_100_Hybrid02_Backup','_BFME2_100_Test01_Backup')) {
                $candidate = Join-Path (Join-Path $root $oldBackupName) ($item.Record.name + '.original')
                if ((Test-Path -LiteralPath $candidate) -and (Get-PathHash $candidate) -eq $item.Record.original_sha256) { $original = [IO.File]::ReadAllBytes($candidate); break }
            }
            if ($null -eq $original) { throw "Verified original backup is missing for $($item.Record.name). No game files changed." }
        } else { $original = $item.Current }
        if ($Action -eq 'Install') { [byte[]]$desired = Get-PatchedBytes $original $item.Record }
        else { [byte[]]$desired = $original }
        $plans += [pscustomobject]@{ Item=$item; Original=$original; Desired=$desired; DesiredHash=(Get-BytesHash $desired); Stage=$null }
    }
    if ($Action -eq 'Install') {
        [void][System.IO.Directory]::CreateDirectory($backupRoot)
        foreach ($plan in $plans) {
            if (-not (Test-Path -LiteralPath $plan.Item.Backup)) {
                $stream = [System.IO.File]::Open($plan.Item.Backup, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
                try { $stream.Write($plan.Original, 0, $plan.Original.Length) } finally { $stream.Dispose() }
            }
            if ((Get-PathHash $plan.Item.Backup) -ne $plan.Item.Record.original_sha256) { throw 'Backup verification failed.' }
        }
    }
    foreach ($plan in $plans) {
        if ($plan.DesiredHash -eq $plan.Item.Hash) { continue }
        $plan.Stage = Join-Path $root ('.bfme-testlab-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        $staged.Add($plan.Stage)
        [System.IO.File]::WriteAllBytes($plan.Stage, $plan.Desired)
        if ((Get-PathHash $plan.Stage) -ne $plan.DesiredHash) { throw 'Staged file verification failed.' }
    }
    foreach ($plan in $plans) {
        if ((Get-PathHash $plan.Item.Path) -ne $plan.Item.Hash) { throw 'A game file changed during preparation. Close the patch switcher and try again.' }
    }
    foreach ($plan in $plans) {
        if ($null -eq $plan.Stage) { continue }
        # Windows PowerShell can bind $null to an empty path for this overload.
        # Use an explicit temporary previous-file path instead.
        $previous = Join-Path $root ('.bfme-testlab-previous-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        $staged.Add($previous)
        [System.IO.File]::Replace($plan.Stage, $plan.Item.Path, $previous)
        $committed.Add($plan)
        if ((Get-PathHash $plan.Item.Path) -ne $plan.DesiredHash) { throw 'Installed file verification failed.' }
    }
    if ($Action -eq 'Install') {
        Write-Host "$($manifest.release) installed. Start the game using your usual shortcut."
        Write-Host 'Experimental 1.09v3 engine with 1.00 data. See README for compatibility differences.'
    } else { Write-Host 'Original 1.00 engine, launcher and INI restored and verified.' }
    Write-Host "Original backups retained in: $backupRoot"
    exit 0
} catch {
    $message = $_.Exception.Message
    foreach ($plan in $committed) {
        try {
            $rollback = Join-Path $root ('.bfme-testlab-rollback-' + [Guid]::NewGuid().ToString('N') + '.tmp')
            $staged.Add($rollback)
            [System.IO.File]::WriteAllBytes($rollback, $plan.Item.Current)
            $replaced = $rollback + '.previous'
            $staged.Add($replaced)
            [System.IO.File]::Replace($rollback, $plan.Item.Path, $replaced)
            Write-Host "Rolled back $($plan.Item.Record.name)."
        } catch { Write-Warning "Automatic rollback failed. Original files remain in $backupRoot. $($_.Exception.Message)" }
    }
    Write-Host "ERROR: $message" -ForegroundColor Red
    Write-Host 'For Program Files installations, right-click Install.cmd or Restore.cmd and choose Run as administrator.'
    exit 1
} finally {
    foreach ($path in $staged) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
    }
}
