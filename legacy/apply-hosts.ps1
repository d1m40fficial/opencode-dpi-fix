<#
.SYNOPSIS
    Уводит opencode.ai, models.dev и registry.npmjs.org на edge-адрес Cloudflare,
    где не действует правило DPI, режущее TLS ClientHello с post-quantum ключом.

.DESCRIPTION
    Правило блокировки срабатывает на содержимое рукопожатия (PQ key share,
    X25519MLKEM768, ~1.5 КБ), а не на имя в SNI и не на сам IP: классический
    X25519-hello на том же адресе проходит. Поэтому достаточно направить домены
    на edge-адрес, где правило не применено — проверено, что 8.47.69.5 отдаёт
    эти хосты даже с PQ-hello.

    Требуются права администратора: правится %SystemRoot%\System32\drivers\etc\hosts.
    Действие обратимо: -Revert возвращает файл к исходному состоянию.

.PARAMETER EdgeIPv4
    Куда направлять домены. По умолчанию 8.47.69.5. Резервный: 8.6.112.5.

.PARAMETER BlockIPv6
    Дополнительно прописать ::1 для этих доменов, чтобы браузер не пытался идти
    по IPv6, где блокировка тоже может действовать. Включать, только если сайт
    открывается медленно или не открывается при наличии IPv6 у провайдера.

.PARAMETER DryRun
    Показать результат, ничего не записывая.

.PARAMETER Revert
    Удалить записи, добавленные этим скриптом.

.EXAMPLE
    .\apply-hosts.ps1 -DryRun
.EXAMPLE
    .\apply-hosts.ps1
.EXAMPLE
    .\apply-hosts.ps1 -EdgeIPv4 8.6.112.5
.EXAMPLE
    .\apply-hosts.ps1 -Revert
#>
[CmdletBinding()]
param(
    [string]$EdgeIPv4 = '8.47.69.5',
    [switch]$BlockIPv6,
    [switch]$DryRun,
    [switch]$Revert,
    [string]$HostsPath = (Join-Path $env:SystemRoot 'System32\drivers\etc\hosts')
)

$ErrorActionPreference = 'Stop'

$marker  = '# --- DPI bypass: opencode / models.dev / npm ---'
$endTag  = '# --- end ---'
$domains = @('opencode.ai', 'models.dev', 'registry.npmjs.org')
$systemHosts = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal $id).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Path -LiteralPath $HostsPath)) { throw "Не найден файл hosts: $HostsPath" }

# --- пересобираем файл без прежнего блока (идемпотентно) ---
$kept = New-Object System.Collections.Generic.List[string]
$inside = $false
$removed = 0
foreach ($line in (Get-Content -LiteralPath $HostsPath)) {
    if ($line -eq $marker) { $inside = $true; $removed++; continue }
    if ($inside -and $line -eq $endTag) { $inside = $false; continue }
    if ($inside) { $removed++; continue }
    $kept.Add($line)
}
$lines = $kept.ToArray()
if ($removed -gt 0) { Write-Host "Удалён предыдущий блок ($removed строк)." }

# --- добавляем новый ---
if (-not $Revert) {
    $block = New-Object System.Collections.Generic.List[string]
    $block.Add($marker)
    $block.Add('# Правило DPI режет TLS ClientHello с PQ key share; этот edge не затронут.')
    foreach ($d in $domains) { $block.Add("$EdgeIPv4`t$d") }
    if ($BlockIPv6) {
        $block.Add('# IPv6 принудительно отключён для этих доменов (соединение отклоняется, браузер сразу берёт IPv4).')
        foreach ($d in $domains) { $block.Add("::1`t$d") }
    }
    $block.Add($endTag)
    $lines = @($lines) + $block.ToArray()
}

if ($DryRun) {
    Write-Host "`n--- [ПРОВЕРКА БЕЗ ЗАПИСИ] итоговый hosts ---"
    $lines | ForEach-Object { Write-Host $_ }
    Write-Host "--- ничего не записано ---"
    return
}

if (($HostsPath -ieq $systemHosts) -and -not (Test-Admin)) {
    throw "Нужны права администратора: запустите 1-установить.cmd (правый клик -> Запуск от имени администратора)."
}

$backup = "$HostsPath.bak-" + (Get-Date -Format 'yyyyMMdd-HHmmss')
Copy-Item -LiteralPath $HostsPath -Destination $backup -Force
Write-Host "Резервная копия: $backup"

Set-Content -LiteralPath $HostsPath -Value $lines -Encoding ASCII
Write-Host "hosts обновлён."

& ipconfig /flushdns | Out-Null
Write-Host "Кэш DNS сброшен.`n"

Write-Host "Проверка:"
foreach ($d in $domains) {
    $ips = @()
    try { $ips = (Resolve-DnsName $d -Type A -ErrorAction Stop | Where-Object IPAddress).IPAddress } catch { }
    $tcp = (Test-NetConnection -ComputerName $d -Port 443 -WarningAction SilentlyContinue).TcpTestSucceeded
    $ok = $ips -contains $EdgeIPv4
    "{0,-22} -> {1,-15} tcp443={2,-5} {3}" -f $d, ($ips -join ','), $tcp, $(if ($ok) { 'OK' } else { 'НЕ ПРИМЕНИЛОСЬ' })
}

Write-Host "`nГотово. Закройте и заново откройте браузер и приложение opencode."
