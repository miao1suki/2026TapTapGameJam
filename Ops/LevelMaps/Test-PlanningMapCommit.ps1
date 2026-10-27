param([switch]$AllTracked)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$root = & git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Run inside the 2026TapTap repository.' }
Push-Location $root
try {
    $scenes = if ($AllTracked) { & git -c core.quotepath=false ls-files -- '*.unity' }
        else { & git -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR -- '*.unity' }
    $failures = [System.Collections.Generic.List[string]]::new()
    $indexFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    & git -c core.quotepath=false ls-files | ForEach-Object { [void]$indexFiles.Add($_) }
    foreach ($scene in $scenes) {
        if (-not $indexFiles.Contains("$scene.meta")) { $failures.Add("Missing Unity metadata: $scene.meta") }
        $unstaged = & git diff --name-only -- $scene
        if ($unstaged) { $failures.Add("Scene has unstaged changes: $scene") }
    }
    $newAssets = & git -c core.quotepath=false diff --cached --name-only --diff-filter=A -- '*.asset' '*.prefab' '*.png' '*.psd' '*.mat' '*.controller' '*.playable'
    foreach ($asset in $newAssets) {
        if (-not $indexFiles.Contains("$asset.meta")) { $failures.Add("Missing Unity metadata: $asset.meta") }
    }
    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host 'Stage the Scene, its .meta, GridArt Map/ownership assets and every new dependency together.'
        exit 1
    }
    Write-Host 'Scene level delivery check passed. Legacy planning JSON is not required.'
} finally { Pop-Location }
