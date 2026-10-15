param([switch]$AllTracked)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$root = & git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Run inside the 2026TapTap repository.' }
Push-Location $root
try {
    $scenes = if ($AllTracked) {
        & git -c core.quotepath=false ls-files -- '*.unity'
    } else {
        & git -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR -- '*.unity'
    }
    $failures = [System.Collections.Generic.List[string]]::new()
    $indexFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    & git -c core.quotepath=false ls-files | ForEach-Object { [void]$indexFiles.Add($_) }
    foreach ($scene in $scenes) {
        $text = (& git show ":$scene") -join "`n"
        if ($text -notmatch '(?m)^  planningJson: .+') { continue }
        if ($text -match '(?m)^  planningJson: (?:\x27\x27|\x22\x22|\{\})\s*$') { continue }
        $map = $scene.Substring(0, $scene.Length - 6) + '.planning.json'
        if (-not $indexFiles.Contains($map)) { $failures.Add("Missing editable map in commit: $map"); continue }
        if (-not $indexFiles.Contains("$map.meta")) { $failures.Add("Missing Unity metadata: $map.meta") }
        try {
            $json = (& git show ":$map") -join "`n"
            $doc = ConvertFrom-Json -InputObject $json
            if ($null -eq $doc.rooms) { throw 'No rooms field' }
        } catch { $failures.Add("Invalid editable map: $map") }
        $unstaged = & git diff --name-only -- $map
        if ($unstaged) { $failures.Add("Map has unstaged changes; stage Scene, JSON and metadata together: $map") }
    }
    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host 'Save the Scene in Unity to create its .planning.json, then stage the paired files.'
        exit 1
    }
    Write-Host 'Planning map commit check passed.'
} finally { Pop-Location }
