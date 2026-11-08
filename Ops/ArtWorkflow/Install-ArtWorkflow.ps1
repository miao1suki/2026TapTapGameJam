$ErrorActionPreference = 'Stop'
$taskRoot = Join-Path $env:LOCALAPPDATA 'XingZhiCai/ArtWorkflow'
New-Item -ItemType Directory -Force -Path $taskRoot | Out-Null
if (![Environment]::Is64BitOperatingSystem -or $env:OS -ne 'Windows_NT') { throw '此安装脚本只验收 Windows x64。' }
# Selected workflows need HTTP/Form/Code/Merge. Avoid running every third-party install hook.
& npm.cmd install --prefix $taskRoot --save-exact --no-audit --no-fund --ignore-scripts --omit=optional 'n8n@2.46.6' '@anthropic-ai/claude-code@2.1.34'
if ($LASTEXITCODE -ne 0) { throw '依赖安装失败，请保留 npm 错误日志排查。' }
$taskDownloads = Join-Path $taskRoot 'downloads'
New-Item -ItemType Directory -Force -Path $taskDownloads | Out-Null
function Get-CheckedAsset([string]$Url, [string]$File, [string]$Sha256) {
    if (!(Test-Path -LiteralPath $File) -or (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Sha256) {
        Invoke-WebRequest -Uri $Url -Headers @{Accept='application/octet-stream'} -OutFile $File -TimeoutSec 120
    }
    if ((Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Sha256) { throw '下载校验失败。' }
}
$taskSqlite = Join-Path $taskDownloads 'sqlite3-v5.1.7-napi-v6-win32-x64.tar.gz'
Get-CheckedAsset 'https://api.github.com/repos/TryGhost/node-sqlite3/releases/assets/143895615' $taskSqlite '7a55f7adb5fc3126156c56253511ec5ac38110531281ecbafbe71244555e93ce'
$taskEntries = @(tar.exe -tzf $taskSqlite)
if ($LASTEXITCODE -ne 0 -or $taskEntries.Count -ne 1 -or $taskEntries[0] -ne 'build/Release/node_sqlite3.node') { throw 'SQLite 压缩包内容不符合预期。' }
tar.exe -xzf $taskSqlite -C (Join-Path $taskRoot 'node_modules/sqlite3')
if ($LASTEXITCODE -ne 0) { throw 'SQLite 解压失败。' }
$taskBash = Join-Path $taskRoot 'PortableGit/bin/bash.exe'
if (!(Test-Path -LiteralPath $taskBash)) {
    $task7Zip = 'C:/Program Files/7-Zip/7z.exe'
    if (!(Test-Path -LiteralPath $task7Zip)) { throw '需先安装 7-Zip；本脚本仅解压 Portable Git，不执行其自解压安装器。' }
    $taskGitArchive = Join-Path $taskDownloads 'PortableGit-2.56.0.4-64-bit.7z.exe'
    Get-CheckedAsset 'https://api.github.com/repos/git-for-windows/git/releases/assets/677312658' $taskGitArchive 'b1f3e52216e2ed6882d1bebfd4d3fdf319bec40d2edbbf9fd26ec4fd3fce7ed1'
    $taskPaths = & $task7Zip l -ba -slt $taskGitArchive
    foreach ($taskLine in $taskPaths) {
        if ($taskLine.StartsWith('Path = ')) {
            $taskEntry = $taskLine.Substring(7)
            if ([IO.Path]::IsPathRooted($taskEntry) -or ($taskEntry -split '[\\/]' -contains '..')) { throw 'Portable Git 含越界路径。' }
        }
    }
    & $task7Zip x $taskGitArchive ('-o' + (Join-Path $taskRoot 'PortableGit')) -y -bd -bso0
    if ($LASTEXITCODE -ne 0) { throw 'Portable Git 解压失败。' }
}
& node -e "const s=require(process.argv[1]); const db=new s.Database(':memory:'); db.get('select 42 as value',(e,r)=>{if(e)throw e; console.log('SQLite native check:',r.value);db.close();});" (Join-Path $taskRoot 'node_modules/sqlite3')
if ($LASTEXITCODE -ne 0) { throw 'SQLite 原生模块验证失败。' }
Write-Host ('本地软件安装目录：' + $taskRoot)
& (Join-Path $PSScriptRoot 'Start-ArtWorkflow.ps1') -CheckOnly
