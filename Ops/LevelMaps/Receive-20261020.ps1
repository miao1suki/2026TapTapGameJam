param([string]$Baseline = '1722c59')
$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$scenePath = 'Assets/_Project/Scenes/Playing/Gaming.unity'
$mapPath = 'Assets/_Project/Scenes/Playing/Gaming.planning.json'
$namedPath = 'Assets/_Project/Content/LevelMaps/正式关卡.json'
$utf8 = [Text.UTF8Encoding]::new($false)
function Documents([string]$text) {
    $result = @{}
    foreach ($match in [regex]::Matches($text, '(?ms)^--- !u!\d+ &(\d+)[^\n]*\n.*?(?=^--- |\z)')) {
        if ($result.ContainsKey($match.Groups[1].Value)) { throw 'Duplicate YAML id' }
        $result[$match.Groups[1].Value] = $match.Value
    }
    return $result
}
function Native([string]$path) { return Join-Path $root $path }
$before = ((git show "${Baseline}:$scenePath") -join "`n") + "`n"
$oldDocs = Documents $before
$scene = [IO.File]::ReadAllText((Native $scenePath)).Replace("`r`n", "`n")
$newDocs = Documents $scene
$mapText = [IO.File]::ReadAllText((Native $mapPath))
$map = $mapText | ConvertFrom-Json -Depth 100
$oldMap = (git show "${Baseline}:$mapPath") -join "`n" | ConvertFrom-Json -Depth 100
$start = ($oldMap.rooms.boxes | Where-Object id -eq 'box_gameplay_start_anchor')
if ($null -eq $start -or @($start).Count -ne 1) { throw 'Expected baseline Start' }
if ($map.rooms.boxes.id -contains $start.id) { throw 'Incoming map already has Start; inspect instead' }
$room = @($map.rooms | Where-Object id -eq $map.playerStartRoomId)
if ($room.Count -ne 1) { throw 'Missing player room' }
$room = $room[0]
$start.x = [Math]::Floor($map.playerStartLocal.x)
$start.y = [Math]::Floor($map.playerStartLocal.y)
$room.boxes += $start
$json = ConvertTo-Json $map -Depth 100
$signature = (ConvertTo-Json $start -Depth 100 -Compress) + '{"values":[]}'
# JSON double-quoted strings are a compatible subset of YAML double-quoted scalars.
$embedded = ConvertTo-Json $json -Compress
$scene = [regex]::Replace($scene, '(?ms)^  planningJson: ".*?"(?=\n  \w|\n--- )', { param($m) '  planningJson: ' + $embedded }, 1)
if ($scene -notmatch [regex]::Escape('box_gameplay_start_anchor')) { throw 'Embedded planning replacement failed' }

# Preserve only the existing art hierarchy, never stale unparented gameplay roots.
$artRoot = '753769110'
$queue = [Collections.Generic.Queue[string]]::new()
$queue.Enqueue($artRoot)
$artIds = [Collections.Generic.HashSet[string]]::new()
while ($queue.Count -gt 0) {
    $id = $queue.Dequeue()
    if (!$oldDocs.ContainsKey($id) -or !$artIds.Add($id)) { continue }
    $doc = $oldDocs[$id]
    foreach ($match in [regex]::Matches($doc, '(?m)^(?!  m_Father:).*?\{fileID: (\d+)\}')) {
        $ref = $match.Groups[1].Value
        if ($oldDocs.ContainsKey($ref)) { $queue.Enqueue($ref) }
    }
}
$added = [Text.StringBuilder]::new()
foreach ($id in $artIds) {
    if ($newDocs.ContainsKey($id)) { throw "Incoming Scene fileID overlaps preserved art: $id" }
    [void]$added.Append($oldDocs[$id])
}
# Reuse established Start prefab/GUIDs, but attach to the new author spawn room.
$roomGo = @($newDocs.Values | Where-Object { $_ -match '(?m)^GameObject:' -and $_ -match ('m_Name: .*\['+[regex]::Escape($room.id)+'\]') })
if ($roomGo.Count -ne 1) { throw 'Cannot identify new spawn room hierarchy' }
$roomTransform = [regex]::Match($roomGo[0], 'component: \{fileID: (\d+)\}').Groups[1].Value
$anchorIds = @('521496459','521496460','521496461','521496462','521496463')
foreach ($id in $anchorIds) {
    if ($newDocs.ContainsKey($id)) { throw "Incoming Scene fileID overlaps Start: $id" }
    $doc = $oldDocs[$id]
    $doc = $doc.Replace('m_TransformParent: {fileID: 1596880718}', "m_TransformParent: {fileID: $roomTransform}")
    $doc = $doc.Replace('regionId: mvmap14_white_only', 'regionId: '+$room.id)
    $doc = [regex]::Replace($doc, '(?m)^  cell: .+$', '  cell: {x: '+[Math]::Floor($map.playerStartWorld.x)+', y: '+[Math]::Floor($map.playerStartWorld.y)+'}')
    $doc = [regex]::Replace($doc, '(?m)^  planningLocalCell: .+$', '  planningLocalCell: {x: '+$start.x+', y: '+$start.y+'}')
    $doc = [regex]::Replace($doc, '(?ms)^  planningSignature: ".*?"(?=\n  \w|\n--- |\n\z)', { param($m) '  planningSignature: '+(ConvertTo-Json $signature -Compress) })
    foreach ($axis in @('x','y','z')) {
        $value = $map.playerStartWorld.$axis.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
        $pattern = '(?m)(propertyPath: m_LocalPosition\.'+$axis+'\n      value: )[^\n]+'
        $doc = [regex]::Replace($doc, $pattern, { param($m) $m.Groups[1].Value+$value })
    }
    [void]$added.Append($doc)
}
$parent = $newDocs[$roomTransform]
$newParent = $parent.Replace('  m_Children:', "  m_Children:`n  - {fileID: 521496460}")
$scene = $scene.Replace($parent,$newParent)
$roots = [regex]::Match($scene, '(?ms)^--- !u!1660057539 .*\z').Value
$newRoots = $roots.TrimEnd()+"`n  - {fileID: $artRoot}`n"
$scene = $scene.Replace($roots, $added.ToString()+$newRoots)
$finalDocs = Documents $scene
foreach ($id in $newDocs.Keys) {
    if ($id -in @($roomTransform,'9223372036854775807')) { continue }
    if ($newDocs[$id] -match '^--- !u!114' -and $newDocs[$id] -match '(?m)^  planningJson: ') { continue }
    if ($finalDocs[$id] -cne $newDocs[$id]) { throw "Author Scene document changed: $id" }
}
[IO.File]::WriteAllText((Native $scenePath), $scene, $utf8)
[IO.File]::WriteAllText((Native $mapPath), $json+"`n", $utf8)
[IO.File]::WriteAllText((Native $namedPath), $json+"`n", $utf8)
Write-Host ('Preserved author Scene documents='+$newDocs.Count+'; art documents='+$artIds.Count+'; map boxes='+$map.rooms.boxes.Count+'; Start exact='+$map.playerStartWorld)
