# menu.ps1
function Show-Menu {
    param (
        [string]$Title = '.NET 與前端專案管理選單'
    )
    Clear-Host
    Write-Host "================ $Title ================" -ForegroundColor Cyan
    Write-Host "1. 啟動 API (dotnet watch run)" -ForegroundColor Green
    Write-Host "2. 執行單元測試 (dotnet test)" -ForegroundColor Yellow
    Write-Host "3. 啟動前端 (npm run dev)" -ForegroundColor Magenta
    Write-Host "4. 設定環境變數為 Development (ASPNETCORE_ENVIRONMENT)" -ForegroundColor Blue
    Write-Host "5. 啟動前端 (對外開放，可指定 PORT)" -ForegroundColor Magenta
    Write-Host "6. 打包前端並列出產出的 JS 檔案" -ForegroundColor Magenta
    Write-Host "0. 離開" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Cyan
}

# 本機設定（連線字串等含密碼的環境變數）放在 menu.local.ps1，不納入版控
if (Test-Path "$PSScriptRoot\menu.local.ps1") { . "$PSScriptRoot\menu.local.ps1" }

do {
    Show-Menu
    $choice = Read-Host "請輸入選項代號"
    
    switch ($choice) {
        '1' {
            Write-Host "`n正在啟動 API 專案 (Environment: Development)..." -ForegroundColor Green
            $env:ASPNETCORE_ENVIRONMENT="Development"
            $apiProject = "backend/src/ApiGateway/ApiGateway.csproj"
            if (Test-Path $apiProject) {
                dotnet watch run --project $apiProject
            } else {
                Write-Host "找不到 $apiProject，後端 C# 專案尚未建立。" -ForegroundColor Red
            }
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '2' {
            Write-Host "`n正在執行單元測試..." -ForegroundColor Yellow
            $testProject = "backend/tests/Backend.Unit.Tests/Backend.Unit.Tests.csproj"
            if (Test-Path $testProject) {
                dotnet test $testProject
            } else {
                Write-Host "找不到 $testProject，後端 C# 測試專案尚未建立。" -ForegroundColor Red
            }
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '3' {
            Write-Host "`n正在啟動前端專案 (frontend)..." -ForegroundColor Magenta
            # 切換到 frontend 資料夾執行，執行完或離開後切回原本根目錄
            Push-Location frontend
            npm run dev
            Pop-Location
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '4' {
            $env:ASPNETCORE_ENVIRONMENT="Development"
            Write-Host "`n已將 ASPNETCORE_ENVIRONMENT 設定為 Development（僅限本次 PowerShell 視窗有效）" -ForegroundColor Blue
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '5' {
            $port = Read-Host "請輸入對外開放的 PORT (直接按 Enter 使用預設 5180)"
            if ([string]::IsNullOrWhiteSpace($port)) {
                $port = "5180"
            }
            Write-Host "`n正在啟動前端專案 (frontend)，對外開放於 0.0.0.0:$port ..." -ForegroundColor Magenta
            Push-Location frontend
            npm run dev -- --host 0.0.0.0 --port $port
            Pop-Location
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '6' {
            Write-Host "`n正在打包前端專案 (npm run build)..." -ForegroundColor Magenta
            Push-Location frontend
            npm run build
            Pop-Location
            $jsFiles = Get-ChildItem -Path "frontend/dist/assets" -Filter "*.js" -ErrorAction SilentlyContinue
            if ($jsFiles) {
                Write-Host "`n產出的 JS 檔案：" -ForegroundColor Cyan
                foreach ($file in $jsFiles) {
                    $sizeKb = [math]::Round($file.Length / 1KB, 2)
                    Write-Host "  $($file.Name)  ($sizeKb KB)" -ForegroundColor Green
                }

                $apiProjectDir = "backend/src/ApiGateway"
                $wwwrootPath = "$apiProjectDir/wwwroot"
                if (-not (Test-Path $apiProjectDir)) {
                    Write-Host "`n找不到 $apiProjectDir，後端 C# 專案尚未建立，略過複製到 wwwroot。" -ForegroundColor Yellow
                } else {
                    Write-Host "`n正在複製打包結果到 $wwwrootPath （全部覆蓋）..." -ForegroundColor Magenta
                    if (Test-Path $wwwrootPath) {
                        # 保留 .gitkeep（wwwroot 目錄本身納入版控）
                        Get-ChildItem -Path $wwwrootPath -Force | Where-Object { $_.Name -ne '.gitkeep' } | Remove-Item -Recurse -Force
                    } else {
                        New-Item -ItemType Directory -Path $wwwrootPath -Force | Out-Null
                    }
                    Copy-Item -Path "frontend/dist/*" -Destination $wwwrootPath -Recurse -Force
                    Write-Host "已完成複製到 $wwwrootPath" -ForegroundColor Green
                }
            } else {
                Write-Host "`n找不到打包後的 JS 檔案，請確認打包是否成功。未複製到 wwwroot。" -ForegroundColor Red
            }
            Read-Host "`n按 Enter 鍵返回選單..."
        }
        '0' {
            Write-Host "`n再見！" -ForegroundColor Cyan
            break
        }
        default {
            Write-Host "`n無效的選擇，請重新輸入。" -ForegroundColor Red
            Start-Sleep -Seconds 1.5
        }
    }
} while ($choice -ne '0')
