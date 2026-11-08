param([Parameter(ValueFromRemainingArguments = $true)][string[]]$ClaudeArgs)
$ErrorActionPreference = 'Stop'
$taskRoot = Join-Path $env:LOCALAPPDATA 'XingZhiCai/ArtWorkflow'
$entry = Join-Path $taskRoot 'node_modules/@anthropic-ai/claude-code/cli.js'
$manifest = Join-Path $taskRoot 'node_modules/@anthropic-ai/claude-code/package.json'
if (!(Test-Path -LiteralPath $manifest)) { throw '请先运行 Install-ArtWorkflow.ps1。' }
if ((Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version -ne '2.1.34') { throw '客户端版本不是固定的 2.1.34，已停止。' }
$env:DISABLE_AUTOUPDATER = '1'
$taskBash = @((Join-Path $taskRoot 'PortableGit/bin/bash.exe'), 'C:/Program Files/Git/bin/bash.exe', 'C:/Program Files (x86)/Git/bin/bash.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($taskBash) { $env:CLAUDE_CODE_GIT_BASH_PATH = $taskBash }
& node $entry @ClaudeArgs
exit $LASTEXITCODE
