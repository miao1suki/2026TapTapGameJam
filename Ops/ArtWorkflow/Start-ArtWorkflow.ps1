param([switch]$ImportWorkflows, [switch]$CheckOnly, [switch]$OfflineCheck)
$ErrorActionPreference = 'Stop'
$taskRoot = Join-Path $env:LOCALAPPDATA 'XingZhiCai/ArtWorkflow'
$nodeExe = (Get-Command node -ErrorAction Stop).Source
$entry = Join-Path $taskRoot 'node_modules/n8n/bin/n8n'
if (!(Test-Path -LiteralPath $entry)) { throw '请先运行 Install-ArtWorkflow.ps1。' }
$env:N8N_USER_FOLDER = Join-Path $taskRoot 'data'
$env:N8N_LISTEN_ADDRESS = '127.0.0.1'
$env:N8N_HOST = 'localhost'
$env:N8N_PORT = '5791'
$env:N8N_PROTOCOL = 'http'
$env:N8N_EDITOR_BASE_URL = 'http://localhost:5791/'
$env:WEBHOOK_URL = 'http://localhost:5791/'
$env:N8N_SECURE_COOKIE = 'false'
$env:N8N_DIAGNOSTICS_ENABLED = 'false'
$env:N8N_VERSION_NOTIFICATIONS_ENABLED = 'false'
$env:N8N_TEMPLATES_ENABLED = 'false'
$env:N8N_PERSONALIZATION_ENABLED = 'false'
$env:N8N_COMMUNITY_PACKAGES_ENABLED = 'false'
$env:N8N_COMMUNITY_PACKAGES_PREVENT_LOADING = 'true'
$env:N8N_PYTHON_ENABLED = 'false'
$env:N8N_RUNNERS_BROKER_PORT = '5792'
$env:N8N_RUNNERS_BROKER_LISTEN_ADDRESS = '127.0.0.1'
$env:EXECUTIONS_DATA_SAVE_ON_ERROR = 'none'
$env:EXECUTIONS_DATA_SAVE_ON_SUCCESS = 'none'
$env:EXECUTIONS_DATA_SAVE_ON_PROGRESS = 'false'
$env:EXECUTIONS_DATA_SAVE_MANUAL_EXECUTIONS = 'false'
$env:N8N_ENCRYPTION_KEY_FILE = Join-Path $taskRoot 'n8n-key.local'
New-Item -ItemType Directory -Force -Path $env:N8N_USER_FOLDER | Out-Null
if (!(Test-Path -LiteralPath $env:N8N_ENCRYPTION_KEY_FILE)) {
    $taskKey = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($taskKey)
    [IO.File]::WriteAllText($env:N8N_ENCRYPTION_KEY_FILE, [Convert]::ToBase64String($taskKey))
    $taskIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $env:N8N_ENCRYPTION_KEY_FILE /inheritance:r /grant:r ($taskIdentity + ':F') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '本机密钥文件 ACL 设置失败。' }
}
if ($CheckOnly) { & $nodeExe $entry --version; exit $LASTEXITCODE }
if ($OfflineCheck) { & $nodeExe $entry execute --id=xzc-offline-check-v1 --rawOutput; exit $LASTEXITCODE }
if ($ImportWorkflows) {
    & $nodeExe (Join-Path $PSScriptRoot 'build-workflows.mjs')
    if ($LASTEXITCODE -ne 0) { throw '生成工作流失败。' }
    $workflowDirectory = Join-Path $PSScriptRoot 'workflows'
    & $nodeExe $entry import:workflow --separate "--input=$workflowDirectory"
    exit $LASTEXITCODE
}
Write-Host '打开 http://localhost:5791/；首次使用请创建本机账户。按 Ctrl+C 停止。'
& $nodeExe $entry start
exit $LASTEXITCODE
