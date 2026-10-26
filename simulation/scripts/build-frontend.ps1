# 建置管理介面並輸出到後端 wwwroot/admin（js、css、icon、index.html），供 simulation/Dockerfile 打包
#   .\simulation\scripts\build-frontend.ps1
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$frontend = Join-Path $repoRoot 'frontend'
$outDir = Join-Path $repoRoot 'backend\src\ApiGateway\wwwroot\admin'

# 正式建置一律使用 api 資料來源
$env:VITE_DATA_SOURCE = 'api'
Push-Location $frontend
try {
    if (-not (Test-Path 'node_modules')) { npm ci; if ($LASTEXITCODE) { throw 'npm ci 失敗' } }
    npx tsc -b; if ($LASTEXITCODE) { throw '型別檢查失敗' }
    npx vite build --outDir $outDir --emptyOutDir; if ($LASTEXITCODE) { throw '前端建置失敗' }
} finally {
    Pop-Location
}

Write-Host "管理介面已輸出到 $outDir"
