[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$LegacyRoot,

    [Parameter(Mandatory = $true)]
    [string]$SecureRoot,

    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

function Get-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }

    return [IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Get-StoredFileName {
    param([Parameter(Mandatory = $true)][string]$StoredPath)

    $normalized = $StoredPath.Trim().Replace('\', '/')
    $legacyPrefix = '/uploads/pharmacy/'
    if ($normalized.StartsWith($legacyPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring($legacyPrefix.Length)
    }

    if ([string]::IsNullOrWhiteSpace($normalized) -or
        $normalized.Contains('/') -or
        $normalized -in @('.', '..') -or
        [IO.Path]::GetFileName($normalized) -ne $normalized) {
        throw "Unsupported stored pharmacy path '$StoredPath'."
    }

    return $normalized
}

function Write-MigrationLog {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][string]$StoredPath,
        [Parameter(Mandatory = $true)][string]$Message
    )

    [pscustomobject]@{
        TimestampUtc = [DateTime]::UtcNow.ToString('O')
        Status = $Status
        StoredPath = $StoredPath
        Message = $Message
    } | ConvertTo-Json -Compress
}

$manifest = Get-AbsolutePath $ManifestPath
$legacy = Get-AbsolutePath $LegacyRoot
$secure = Get-AbsolutePath $SecureRoot

if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw "Manifest file '$manifest' does not exist."
}

if ([StringComparer]::OrdinalIgnoreCase.Equals($legacy, $secure)) {
    throw 'LegacyRoot and SecureRoot must be different directories.'
}

if ($Apply -and -not (Test-Path -LiteralPath $secure -PathType Container)) {
    [IO.Directory]::CreateDirectory($secure) | Out-Null
}

$counts = [ordered]@{
    Copied = 0
    WouldCopy = 0
    Skipped = 0
    Missing = 0
    Failed = 0
}
$seenFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

foreach ($storedPath in Get-Content -LiteralPath $manifest -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($storedPath)) {
        continue
    }

    try {
        $fileName = Get-StoredFileName $storedPath
        if (-not $seenFileNames.Add($fileName)) {
            $counts.Skipped++
            Write-MigrationLog 'SKIPPED' $storedPath 'Duplicate manifest entry.'
            continue
        }

        $source = [IO.Path]::GetFullPath((Join-Path $legacy $fileName))
        $destination = [IO.Path]::GetFullPath((Join-Path $secure $fileName))

        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            $counts.Missing++
            Write-MigrationLog 'MISSING' $storedPath "Legacy source '$source' was not found."
            continue
        }

        $sourceItem = Get-Item -LiteralPath $source -Force
        if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Legacy source '$source' is a reparse point and will not be copied."
        }

        if (Test-Path -LiteralPath $destination) {
            $counts.Skipped++
            Write-MigrationLog 'SKIPPED' $storedPath "Destination '$destination' already exists; it was not overwritten."
            continue
        }

        if (-not $Apply) {
            $counts.WouldCopy++
            Write-MigrationLog 'DRY-RUN' $storedPath "Would copy '$source' to '$destination'."
            continue
        }

        [IO.File]::Copy($source, $destination, $false)
        $counts.Copied++
        Write-MigrationLog 'COPIED' $storedPath "Copied to '$destination' without changing the stored filename."
    }
    catch {
        $counts.Failed++
        Write-MigrationLog 'FAILED' $storedPath $_.Exception.Message
    }
}

[pscustomobject]@{
    Mode = if ($Apply) { 'APPLY' } else { 'DRY-RUN' }
    Copied = $counts.Copied
    WouldCopy = $counts.WouldCopy
    Skipped = $counts.Skipped
    Missing = $counts.Missing
    Failed = $counts.Failed
} | ConvertTo-Json -Compress

if ($counts.Failed -gt 0) {
    exit 1
}
