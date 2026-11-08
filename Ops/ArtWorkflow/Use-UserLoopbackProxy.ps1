# Development tools only. Reuse an already-enabled local proxy; never edit OS settings.
$proxySettings = Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Internet Settings' -ErrorAction SilentlyContinue
if ($proxySettings.ProxyEnable -eq 1 -and $proxySettings.ProxyServer -match '^(127\.0\.0\.1|localhost):([0-9]{1,5})$') {
    $proxyPort = [int]$Matches[2]
    if ($proxyPort -gt 0 -and $proxyPort -le 65535) {
        $localProxy = 'http://' + $proxySettings.ProxyServer
        if (!$env:HTTPS_PROXY) { $env:HTTPS_PROXY = $localProxy }
        if (!$env:HTTP_PROXY) { $env:HTTP_PROXY = $localProxy }
    }
}
$env:NO_PROXY = (@($env:NO_PROXY,'localhost','127.0.0.1','::1') | Where-Object { $_ }) -join ','
