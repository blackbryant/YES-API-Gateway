# 連續送出請求並統計結果，用於 A/B 分配比例、熱更新不中斷、自動回退等驗證
#
# 從本機送出（經由對本機開放的埠）：
#   .\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 1000
#   .\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 2000 -By Status
#   .\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Header @{ 'X-Exp-Group' = 'B' }
param(
    [Parameter(Mandatory)] [string]$Url,
    [int]$Count = 1000,
    # Body：依回應內容分組（假後端回應自己的名稱）；Status：依 HTTP 狀態碼分組
    [ValidateSet('Body', 'Status')] [string]$By = 'Body',
    [hashtable]$Header = @{},
    # 帶上閘道回傳的黏著 Cookie，驗證同一用戶端固定在同一組
    [switch]$KeepCookie,
    [int]$DelayMs = 0
)

function Invoke-FromHost {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    for ($i = 1; $i -le $Count; $i++) {
        $request = @{ Uri = $Url; Headers = $Header; UseBasicParsing = $true; ErrorAction = 'Stop' }
        if ($KeepCookie) { $request.WebSession = $session }

        try {
            $response = Invoke-WebRequest @request
            if ($By -eq 'Status') { [int]$response.StatusCode } else { "$($response.Content)".Trim() }
        } catch {
            $status = $_.Exception.Response.StatusCode
            if ($null -ne $status) { if ($By -eq 'Status') { [int]$status } else { "HTTP $([int]$status)" } }
            else { "ERROR: $($_.Exception.Message)" }
        }

        if ($i % 100 -eq 0) { Write-Progress -Activity $Url -Status "$i / $Count" -PercentComplete ($i * 100 / $Count) }
        if ($DelayMs -gt 0) { Start-Sleep -Milliseconds $DelayMs }
    }
    Write-Progress -Activity $Url -Completed
}

$results = Invoke-FromHost

# REST 測試後端回 JSON（每次內容不同），改以 service 欄位分組；其他回應維持原文字
$labels = $results | ForEach-Object {
    $label = $_
    if ($By -eq 'Body' -and "$_".StartsWith('{')) {
        try { $service = ("$_" | ConvertFrom-Json).service; if ($service) { $label = $service } } catch { }
    }
    $label
}

$labels | Group-Object | Sort-Object Count -Descending |
    Select-Object Name, Count, @{ Name = 'Percent'; Expression = { '{0:N1}%' -f ($_.Count * 100 / $Count) } } |
    Format-Table -AutoSize
