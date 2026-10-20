[CmdletBinding()]
param(
    [string]$AdbPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe',
    [string]$Serial,
    [ValidateRange(0, 2147483647)]
    [int]$AppProcessId = 0
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) {
    throw 'ADB not found. Supply -AdbPath pointing to Android SDK platform-tools/adb.exe.'
}

$deviceArgs = @()
if ($Serial) { $deviceArgs = @('-s', $Serial) }
$deviceState = & $AdbPath @deviceArgs get-state 2>&1
if ($LASTEXITCODE -ne 0 -or (($deviceState -join '').Trim() -ne 'device')) {
    throw 'No authorized Android device. Connect USB, enable USB debugging, accept the phone authorization dialog, then retry. Use -Serial when multiple devices are connected.'
}

# Do not clear logcat: the rare error may already be in its ring buffer.
# ErrLog is an Android log tag, not a file/menu named Errorlog.
$processArgs = @()
if ($AppProcessId -gt 0) { $processArgs = @('--pid=' + $AppProcessId) }
# Success callbacks use Unity's Info level. Keep them so a corrected credential
# can be verified, while the sanitization below excludes unrelated Unity logs.
$lines = & $AdbPath @deviceArgs logcat -d -v threadtime @processArgs 'ErrLog:V' 'TapSDK:W' 'TapTap:W' 'TapTapSdk:W' 'TapTapSDK:W' 'TapInitializer:W' 'TapTapCore:W' 'TapTapLogin:W' 'Unity:I' '*:S' 2>&1
if ($LASTEXITCODE -ne 0) { throw 'ADB logcat failed. Check USB debugging authorization.' }

$safeLines = foreach ($line in $lines) {
    $value = [string]$line
    if ($value -match '\sUnity\s*:' -and $value -notmatch '\[TapTapAchievementBridge\]') { continue }
    # SDK replies can contain credentials or account data. Never print raw logs.
    $value = [regex]::Replace($value, '(?i)(https?://)[^\s"''<>]+', '[url redacted]')
    $value = [regex]::Replace($value, '(?i)(bearer\s+)\S+', '$1[redacted]')
    $value = [regex]::Replace($value, '(?i)((?:client[_-]?token|access[_-]?token|refresh[_-]?token|id[_-]?token|token|authorization|server[_-]?secret|client[_-]?secret|secret|open[_-]?id|union[_-]?id|account[_-]?id|session[_-]?id|password|cookie|mac|signature)\s*["'']?\s*[:=]\s*["'']?)[^\s,"'';}]+', '$1[redacted]')
    $value = [regex]::Replace($value, '[A-Za-z0-9_+/=-]{40,}', '[long value redacted]')
    $value
}

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outputDirectory = Join-Path $projectRoot 'Library/AndroidDiagnostics'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$outputPath = Join-Path $outputDirectory ('TapTap-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
$header = @(
    'TapTap diagnostic capture (filtered and best-effort redacted; review before sharing)',
    ('Captured UTC: ' + [DateTime]::UtcNow.ToString('O')),
    ('Application PID filter: ' + $(if ($AppProcessId -gt 0) { [string]$AppProcessId } else { 'none' })),
    'Expected standalone Android package: com.miao1suki.shapeofcolor',
    'Known release certificate MD5: 2D9E5B4A9D63EB90D63EA84789C0C333',
    'Record separately whether the issue happened in TapTap instant-play/container or the installed APK.',
    'No log buffer was cleared; no app was started, stopped, installed or uninstalled.',
    ''
)
[IO.File]::WriteAllLines($outputPath, [string[]]($header + @($safeLines)), [Text.UTF8Encoding]::new($false))
Write-Output ('Saved filtered diagnostics: ' + $outputPath)
Write-Output 'Review this file locally before sending it. No raw log output or credentials were printed.'
