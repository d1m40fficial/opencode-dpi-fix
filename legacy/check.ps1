<#
.SYNOPSIS
    Проверяет, доходят ли opencode.ai, models.dev и registry.npmjs.org.

.DESCRIPTION
    Для каждого хоста показывает: во что резолвится имя, устанавливается ли TCP
    на 443 и что отвечает HTTPS. Ничего не меняет, права администратора не нужны.

    Если хост резолвится в 8.47.69.5 и HTTPS отвечает 200/401/403 — всё работает.
    Если резолвится в 172.65.90.x / 104.26.x и HTTPS висит — записи в hosts нет
    либо она не применилась.
#>
[CmdletBinding()]
param(
    [string[]]$Domains = @('opencode.ai', 'models.dev', 'registry.npmjs.org')
)

function Test-Url([string]$url) {
    $started = Get-Date
    try {
        $r = Invoke-WebRequest -Uri $url -Method Head -TimeoutSec 15 -UseBasicParsing `
             -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' -ErrorAction Stop
        return "HTTP $($r.StatusCode) за $([int]((Get-Date)-$started).TotalSeconds)с"
    } catch {
        $code = $null
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        if ($code) { return "HTTP $code за $([int]((Get-Date)-$started).TotalSeconds)с" }
        return "ОШИБКА за $([int]((Get-Date)-$started).TotalSeconds)с: $($_.Exception.Message)"
    }
}

$urls = @{
    'opencode.ai'        = 'https://opencode.ai/inference/v1/models'
    'models.dev'         = 'https://models.dev/api.json'
    'registry.npmjs.org' = 'https://registry.npmjs.org/-/ping'
}

Write-Host ("{0,-22} {1,-30} {2,-10} {3}" -f 'домен', 'резолв', 'tcp443', 'https')
Write-Host ('-' * 100)

foreach ($d in $Domains) {
    $ips = @()
    try { $ips = (Resolve-DnsName $d -Type A -ErrorAction Stop | Where-Object IPAddress).IPAddress } catch { }
    if (-not $ips) { $ips = @('нет ответа') }
    $tcp = (Test-NetConnection -ComputerName $d -Port 443 -WarningAction SilentlyContinue).TcpTestSucceeded
    $https = if ($urls.ContainsKey($d)) { Test-Url $urls[$d] } else { '-' }
    Write-Host ("{0,-22} {1,-30} {2,-10} {3}" -f $d, (($ips | Select-Object -First 2) -join ','), $tcp, $https)
}

Write-Host ''
Write-Host 'IPv6 для этих имён:'
foreach ($d in $Domains) {
    $v6 = @()
    try { $v6 = (Resolve-DnsName $d -Type AAAA -ErrorAction Stop | Where-Object IPAddress).IPAddress } catch { }
    Write-Host ("  {0,-22} {1}" -f $d, $(if ($v6) { ($v6 | Select-Object -First 2) -join ',' } else { 'нет AAAA' }))
}

Write-Host ''
Write-Host 'Если IPv6 есть, а сайт не открывается - запустите 1-установить.cmd -BlockIPv6'
Write-Host 'и проверьте снова.'
