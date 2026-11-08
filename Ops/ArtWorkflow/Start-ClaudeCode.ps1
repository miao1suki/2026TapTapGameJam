param([Parameter(ValueFromRemainingArguments = $true)][string[]]$ClaudeArgs)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-UserLoopbackProxy.ps1')
$taskRoot = Join-Path $env:LOCALAPPDATA 'XingZhiCai/ArtWorkflow'
$entry = Join-Path $taskRoot 'node_modules/@anthropic-ai/claude-code/cli.js'
$manifest = Join-Path $taskRoot 'node_modules/@anthropic-ai/claude-code/package.json'
if (!(Test-Path -LiteralPath $manifest)) { throw '请先运行 Install-ArtWorkflow.ps1。' }
if ((Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version -ne '2.1.34') { throw '客户端版本不是固定的 2.1.34，已停止。' }
$env:DISABLE_AUTOUPDATER = '1'
$env:CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC = '1'
$env:CLAUDE_CODE_MAX_RETRIES = '0'
$taskBash = @((Join-Path $taskRoot 'PortableGit/bin/bash.exe'), 'C:/Program Files/Git/bin/bash.exe', 'C:/Program Files (x86)/Git/bin/bash.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($taskBash) { $env:CLAUDE_CODE_GIT_BASH_PATH = $taskBash }
$localOnly = $ClaudeArgs.Count -eq 1 -and $ClaudeArgs[0] -in @('--help','-h','--version','-v')
if (!$localOnly) {
    $profilePath = Join-Path $taskRoot 'claude-provider.local.json'
    $tokenPath = Join-Path $taskRoot 'claude-token.dpapi'
    if (!(Test-Path -LiteralPath $profilePath) -or !(Test-Path -LiteralPath $tokenPath)) { throw '请先运行 Configure-ClaudeCode.ps1；不会回退到官方 API 或 n8n Anthropic 凭据。' }
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    if ($profile.baseUrl -ne 'https://sui-xiang.com' -or $profile.authMode -ne 'ANTHROPIC_AUTH_TOKEN') { throw '请重新核对本机供应商配置。' }
    $secureToken = (Get-Content -LiteralPath $tokenPath -Raw).Trim() | ConvertTo-SecureString
    $tokenPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    try { $env:ANTHROPIC_AUTH_TOKEN = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPtr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPtr); $secureToken.Dispose() }
    $env:ANTHROPIC_BASE_URL = $profile.baseUrl
    $env:ANTHROPIC_MODEL = $profile.model
    # Avoid inherited official credentials changing the selected authentication mode.
    Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue
}
try {
    if ($ClaudeArgs -contains 'stream-json') { $input | & node $entry @ClaudeArgs }
    else { & node $entry @ClaudeArgs }
    $clientExit = $LASTEXITCODE
} finally {
    if (!$localOnly) { Remove-Item Env:ANTHROPIC_AUTH_TOKEN -ErrorAction SilentlyContinue }
}
exit $clientExit
