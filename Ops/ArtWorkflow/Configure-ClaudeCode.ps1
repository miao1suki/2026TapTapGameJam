param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = Join-Path $env:LOCALAPPDATA 'XingZhiCai/ArtWorkflow'
$profilePath = Join-Path $taskRoot 'claude-provider.local.json'
$tokenPath = Join-Path $taskRoot 'claude-token.dpapi'
if ($CheckOnly) {
    if (!(Test-Path -LiteralPath $profilePath) -or !(Test-Path -LiteralPath $tokenPath)) { throw 'Claude Code 尚未完成本机配置；请运行 Configure-ClaudeCode.ps1，在密码框填入供应商 Key。' }
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    if ($profile.baseUrl -ne 'https://sui-xiang.com' -or $profile.authMode -ne 'ANTHROPIC_AUTH_TOKEN') { throw '供应商配置不符合本次接入约定。' }
    Write-Host '本机供应商配置与加密令牌文件存在；没有解密令牌或发送模型请求。'
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$dialog = New-Object Windows.Forms.Form
$dialog.Text = '形之彩 · Claude Code 本机配置（不发请求）'
$dialog.Size = New-Object Drawing.Size(590, 320)
$dialog.StartPosition = 'CenterScreen'
$dialog.TopMost = $true
$dialog.ShowInTaskbar = $true
$dialog.Add_Shown({ $dialog.Activate() })
$dialog.FormBorderStyle = 'FixedDialog'
$dialog.MaximizeBox = $false
$labels = @('供应商：https://sui-xiang.com', '客户端：2.1.34；仅手动复审，不自动调用', '模型（供应商文档示例，可按分组修改）', '供应商 Key（仅在本机加密保存，不进入聊天或 Git）')
for ($i=0; $i -lt $labels.Count; $i++) {
    $label = New-Object Windows.Forms.Label
    $label.Text = $labels[$i]
    $label.AutoSize = $true
    $label.Location = New-Object Drawing.Point(18, (16 + @(0,28,64,130)[$i]))
    $dialog.Controls.Add($label)
}
$model = New-Object Windows.Forms.TextBox
$model.Location = New-Object Drawing.Point(20,105)
$model.Size = New-Object Drawing.Size(530,25)
$model.Text = 'claude-sonnet-5-5'
$dialog.Controls.Add($model)
$token = New-Object Windows.Forms.TextBox
$token.Location = New-Object Drawing.Point(20,173)
$token.Size = New-Object Drawing.Size(530,25)
$token.UseSystemPasswordChar = $true
$dialog.Controls.Add($token)
$save = New-Object Windows.Forms.Button
$save.Text = '加密保存（不测试）'
$save.Location = New-Object Drawing.Point(338,222)
$save.Size = New-Object Drawing.Size(210,32)
$dialog.Controls.Add($save)
$save.Add_Click({
    if ([string]::IsNullOrWhiteSpace($token.Text) -or $model.Text -notmatch '^[a-zA-Z0-9_.:-]{1,100}$') {
        [Windows.Forms.MessageBox]::Show('请填入 Key，并核对模型名称。') | Out-Null
        return
    }
    try {
        New-Item -ItemType Directory -Force -Path $taskRoot | Out-Null
        $secureToken = ConvertTo-SecureString -String $token.Text.Trim() -AsPlainText -Force
        $secureToken | ConvertFrom-SecureString | Set-Content -LiteralPath $tokenPath -Encoding utf8
        $taskIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        & icacls.exe $tokenPath /inheritance:r /grant:r ($taskIdentity + ':F') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '令牌文件 ACL 设置失败。' }
        @{baseUrl='https://sui-xiang.com';authMode='ANTHROPIC_AUTH_TOKEN';model=$model.Text.Trim()} | ConvertTo-Json | Set-Content -LiteralPath $profilePath -Encoding utf8
        $token.Clear()
        $dialog.DialogResult = [Windows.Forms.DialogResult]::OK
        $dialog.Close()
    } catch { [Windows.Forms.MessageBox]::Show('保存失败，请检查本机文件权限。未发送网络请求。') | Out-Null }
})
$dialog.AcceptButton = $save
try { $result = $dialog.ShowDialog() } finally { $token.Clear(); $dialog.Dispose() }
if ($result -eq [Windows.Forms.DialogResult]::OK) { Write-Host 'Claude Code 本机配置已加密保存；尚未调用模型。' } else { Write-Host '未完成配置；Claude 复审保持不可用。' }
