# Fails when any tracked file or build output contains something shaped like an OpenAI API key.
# Release builds used to bake a live key into the app; this keeps one from coming back through
# source, a committed installer, or an MSBuild step. Only file names are printed, never a match.
param(
    # Untracked directories to scan as well, e.g. the build output.
    [string[]] $ExtraDirectories = @()
)

$ErrorActionPreference = 'Stop'

# The lookbehind keeps identifiers that merely contain "sk-" (e.g. "task-...") from matching.
$KeyPattern = [regex]'(?<![A-Za-z0-9_-])sk-(proj-)?[A-Za-z0-9_-]{20,}'
# Third-party NuGet binaries are not ours to police and make up a third of the checkout.
$ExcludedPrefix = 'packages/'

$Latin1 = [System.Text.Encoding]::GetEncoding(28591)
$Utf16 = [System.Text.Encoding]::Unicode

# .NET stores string literals as UTF-16 with no alignment guarantee, so binaries are decoded as
# UTF-16 from both byte offsets as well as byte-for-byte.
function Test-ContainsKey([byte[]] $Bytes) {
    if ($KeyPattern.IsMatch($Latin1.GetString($Bytes))) { return $true }
    if ($Bytes.Length -lt 2) { return $false }
    if ($KeyPattern.IsMatch($Utf16.GetString($Bytes, 0, $Bytes.Length))) { return $true }
    return $KeyPattern.IsMatch($Utf16.GetString($Bytes, 1, $Bytes.Length - 1))
}

function Assert-ScannerDetects {
    # Built at runtime so this script never contains a matching string itself.
    $sample = 'sk-' + 'proj-' + ('x' * 24)
    $candidates = @(
        $Latin1.GetBytes($sample),
        $Utf16.GetBytes($sample),
        [byte[]](@(0) + $Utf16.GetBytes($sample))
    )
    foreach ($bytes in $candidates) {
        if (-not (Test-ContainsKey $bytes)) { throw 'Secret scanner self-test failed: the key pattern no longer matches.' }
    }
}

Assert-ScannerDetects

$files = @(git ls-files -z | ForEach-Object { $_ -split "`0" } | Where-Object { $_ -and -not $_.StartsWith($ExcludedPrefix) })
foreach ($directory in $ExtraDirectories) {
    if (Test-Path $directory) {
        $files += @(Get-ChildItem -Path $directory -Recurse -File | ForEach-Object { $_.FullName })
    }
}

$offenders = @()
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
    if (Test-ContainsKey ([System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $file).Path))) {
        $offenders += $file
    }
}

Write-Host "Scanned $($files.Count) files for OpenAI API keys."
if ($offenders.Count -gt 0) {
    $offenders | ForEach-Object { Write-Host "::error file=$_::Contains a string shaped like an OpenAI API key. Remove it and revoke the key." }
    exit 1
}
Write-Host 'No OpenAI API keys found.'
