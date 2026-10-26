# 模擬環境

以 Docker 啟動 .NET 閘道與兩台測試後端（backend-a / backend-b），依 `yes-specs/001-02-dynamic-routing-ab-test/quickstart.md` 驗證端對端行為。閘道必需 PostgreSQL，因此一併啟動。

| 容器 | 說明 | 網路位址（`sim` 172.31.0.0/24） | 本機埠 |
|---|---|---|---|
| `gateway` | .NET 閘道；管理介面（js / css / icon）已打包在 `wwwroot/admin` | `172.31.0.10`，網域 `api.example.local` | `8080` |
| `backend-a` | REST 測試服務（主機 A） | `172.31.0.11:9001` | `9001` |
| `backend-b` | REST 測試服務（主機 B） | `172.31.0.12:9002` | `9002` |
| `postgres` | 閘道設定資料庫 | `172.31.0.5:5432` | `5433` |

閘道對外網址為 <http://api.example.local:8080>，本機需在 `C:\Windows\System32\drivers\etc\hosts` 加入 `127.0.0.1  api.example.local`（需系統管理員權限）。網段若與公司 VPN 或內網衝突，修改 `docker-compose.yml` 的 `subnet` 與各 `ipv4_address`。

閘道映像（`simulation/Dockerfile`）只建置 .NET，不在 Docker 內建置前端；管理介面需先以 `scripts/build-frontend.ps1` 輸出到 `backend/src/ApiGateway/wwwroot/admin`，再由 `dotnet publish` 一起打包。

## 啟動

於 repo 根目錄執行：

```powershell
Copy-Item simulation/.env.example simulation/.env
.\simulation\scripts\build-frontend.ps1                         # 管理介面 → backend/src/ApiGateway/wwwroot/admin
docker compose -f simulation/docker-compose.yml up -d --build
docker compose -f simulation/docker-compose.yml logs -f gateway
```

- 管理介面：<http://api.example.local:8080/admin/>（`/` 會導向這裡），帳號密碼見 `simulation/.env` 的 `ADMIN_USERNAME` / `ADMIN_PASSWORD`。
- `/admin` 以外沒有命中入口的路徑一律回 404；看到 404 而不是預期的後端回應，代表入口沒有命中（檢查路徑與網域）。
- Swagger：<http://api.example.local:8080/swagger>（`GATEWAY_ENVIRONMENT=Development` 時）。
- 修改後端程式後重新建置：`docker compose -f simulation/docker-compose.yml up -d --build gateway`。
- 修改前端後：先重跑 `build-frontend.ps1`，再執行上一行重新建置閘道。
- 從舊版（gateway-1、client-a 等容器）升級：先 `docker compose -f simulation/docker-compose.yml --profile multi-node down --remove-orphans` 再啟動，資料庫資料會保留。

## 入口與主機怎麼填

| 欄位 | 填什麼 | 範例 |
|---|---|---|
| 入口網域 | 用戶端連入**閘道**時網址上的網域；留空表示所有網域 | 留空、`api.example.local:8080` |
| 主機位址 | 閘道轉送的**後端**位址，各以固定 IP 當作一台主機 | `http://172.31.0.11:9001`（A）、`http://172.31.0.12:9002`（B） |

網域比對的是請求的 `Host` 標頭（含埠，預設埠 80 / 443 除外）。瀏覽器打 `http://api.example.local:8080` 時 Host 為 `api.example.local:8080`；入口網域填 `api.example.local:8080` 後，改用 `http://localhost:8080` 就不會命中。不要把後端位址（如 `172.31.0.11:9001`）填進入口網域，發佈時會出現警告。

容器內的 `localhost` 是容器自己，不是本機。要連本機上的服務時，主機位址填 `http://host.docker.internal:<埠>`。

本機 `dotnet run` 也可以只用這裡的資料庫與測試後端（主機位址改填 `http://localhost:9001`、`9002`）：

```powershell
docker compose -f simulation/docker-compose.yml up -d postgres backend-a backend-b
# backend/.env：POSTGRES_HOST=localhost、POSTGRES_PORT=5433，其餘與 simulation/.env 相同
```

## REST 測試（backend-a / backend-b）

backend-a、backend-b 是 `rest-backend/server.js`（Node.js 內建模組，掛載進 `node:22.12.0-alpine`，修改後 `docker compose -f simulation/docker-compose.yml restart backend-a backend-b` 即生效）。回應一律是 JSON，包含：

- `service`：哪一台後端回應（A/B 測試依此判斷組別）
- `request`：後端**實際收到**的方法、路徑、查詢參數、body，以及閘道轉送時改寫的 `host`、`x-forwarded-for`（用戶端 IP）、`x-forwarded-host`（用戶端使用的網域）

### 資源

| 方法 | 路徑 | body | 成功 | 錯誤 |
|---|---|---|---|---|
| GET | `/items` | — | 200 列表 | — |
| GET | `/items/{id}` | — | 200 | 404 |
| POST | `/items` | `{ "name": "kiwi", "price": 40, "tags": ["fruit"] }` | 201 + `Location` | 400 欄位錯誤 |
| PUT | `/items/{id}` | 完整欄位（整筆取代） | 200 | 400、404 |
| PATCH | `/items/{id}` | 任一欄位，例如 `{ "price": 50 }` | 200 | 400、404 |
| DELETE | `/items/{id}` | — | 200（回傳被刪除的資料） | 404 |
| POST | `/reset` | — | 200，還原初始 3 筆資料 | — |
| 其他方法 / 路徑 | — | — | 405（`/items` 不支援的方法，附 `Allow`）；其他路徑為回顯模式 200 | — |

只比對路徑**結尾**的 `/items` 或 `/items/{id}`，所以入口有沒有勾「移除路徑前綴」都能用。看 `request.path` 就知道前綴有沒有被移除。

初始資料：`1 apple 30 [fruit, red]`、`2 banana 15 [fruit, yellow]`、`3 carrot 20 [vegetable, orange]`。

### URL 參數

| 類型 | 範例 | 說明 |
|---|---|---|
| 路徑參數 | `/items/2` | 取 id=2 |
| 關鍵字 | `/items?q=an` | name 包含 `an`（banana） |
| 可重複參數 | `/items?tag=fruit&tag=red` | 同時具備兩個標籤（apple） |
| 範圍 | `/items?minPrice=15&maxPrice=25` | 價格介於 15～25 |
| 排序 | `/items?sort=price`、`/items?sort=-price` | 由小到大 / 由大到小（`id`、`name`、`price`） |
| 分頁 | `/items?page=2&size=1` | 第 2 頁、每頁 1 筆；回應附 `page`、`size`、`total` |
| 組合 | `/items?tag=fruit&sort=-price&page=1&size=1` | 最貴的水果 |
| 編碼 | `/items?q=%E8%98%8B` | 中文等特殊字元需 URL 編碼（瀏覽器會自動處理） |

### 範例

以下假設入口為路徑 `/backend-a/{**catch-all}`、方法勾選 GET / POST / PUT / PATCH / DELETE、網域留空、主機 `http://172.31.0.11:9001`。

瀏覽器可以直接測 GET：

- <http://api.example.local:8080/backend-a/items?tag=fruit&sort=-price>
- <http://api.example.local:8080/backend-a/items/2>
- <http://api.example.local:8080/backend-a/anything/else?x=1&x=2>（回顯模式）

PowerShell（`Invoke-RestMethod`，可避免 Windows PowerShell 5.1 中 `curl.exe` 的 JSON 引號問題）：

```powershell
$G = 'http://api.example.local:8080/backend-a'
$json = @{ 'Content-Type' = 'application/json' }

Invoke-RestMethod "$G/items?q=an&sort=-price"                                                              # GET 列表
Invoke-RestMethod "$G/items/1"                                                                             # GET 單筆
Invoke-RestMethod "$G/items" -Method Post -Headers $json -Body '{"name":"kiwi","price":40,"tags":["fruit"]}'  # POST → id 4
Invoke-RestMethod "$G/items/4" -Method Put -Headers $json -Body '{"name":"kiwi","price":45}'                # PUT
Invoke-RestMethod "$G/items/4" -Method Patch -Headers $json -Body '{"price":50}'                            # PATCH
Invoke-RestMethod "$G/items/4" -Method Delete                                                              # DELETE
Invoke-RestMethod "$G/reset" -Method Post                                                                  # 還原資料

(Invoke-RestMethod "$G/items/1").request | ConvertTo-Json -Depth 5                                         # 看後端實際收到什麼
```

驗證入口的方法限制（G-11）：入口只勾 GET、POST 並發佈後，`Invoke-RestMethod "$G/items/1" -Method Delete` 應該回 **405**，而且由閘道直接回應，請求不會到達後端（後端 log 看不到這筆）：

```powershell
docker compose -f simulation/docker-compose.yml logs -f backend-a     # 後端收到的每筆請求
```

## 測試腳本

`scripts/send-requests.ps1` 從本機連續送出請求並依回應內容（`-By Body`，預設，以 `service` 欄位分組）或狀態碼（`-By Status`）統計：

| 參數 | 說明 |
|---|---|
| `-Header @{ 'X-Exp-Group' = 'B' }` | 附加標頭 |
| `-KeepCookie` | 保留閘道回傳的 Cookie（驗證黏著） |
| `-DelayMs 20` | 每次請求間隔 |

## 驗證步驟（對應 quickstart）

### §3 靜態路由

```powershell
curl.exe -i http://api.example.local:8080/health                    # 200（G-01）
docker compose -f simulation/docker-compose.yml stop postgres
curl.exe -i http://api.example.local:8080/health                    # 仍 200（G-02）
docker compose -f simulation/docker-compose.yml start postgres
```

### §4 新增服務並發佈

服務「訂單 API」：入口 `/api/orders/{**catch-all}`、方法 GET/POST、移除路徑前綴 `/api/orders`、主機 `http://172.31.0.11:9001`，然後發佈。

```powershell
curl.exe http://api.example.local:8080/api/orders/items             # backend-a（G-10、G-15）
curl.exe -i -X DELETE http://api.example.local:8080/api/orders/items/1   # 405（G-11）
```

移除路徑前綴：回應 `request.path` 應為 `/items`，不含 `/api/orders`。

熱更新（G-20）：一個視窗持續送請求，另一邊把主機改為 `http://172.31.0.12:9002` 並發佈。

```powershell
.\simulation\scripts\send-requests.ps1 -Url http://api.example.local:8080/api/orders/ -Count 2000 -By Status   # 預期全為 200
docker compose -f simulation/docker-compose.yml ps gateway                                            # 容器未重啟
```

### §6 A/B 測試

分流改為 A/B：A 組 `http://172.31.0.11:9001` 90%、B 組 `http://172.31.0.12:9002` 10%，黏著 7 天，強制分組標頭 `X-Exp-Group`，然後發佈。

```powershell
.\simulation\scripts\send-requests.ps1 -Url http://api.example.local:8080/api/orders/ -Count 1000                         # backend-b 約 70～130（G-30）
.\simulation\scripts\send-requests.ps1 -Url http://api.example.local:8080/api/orders/ -Count 20 -Header @{ 'X-Exp-Group' = 'B' }   # 全為 backend-b（G-32）
.\simulation\scripts\send-requests.ps1 -Url http://api.example.local:8080/api/orders/ -Count 50 -KeepCookie              # 全落在同一組（黏著）
curl.exe -i -b ".yes.exp.orders-api=B" http://api.example.local:8080/api/orders/                                         # backend-b，且無 Set-Cookie（G-31）
```

組別不會出現在回應標頭（G-50），請以回應內容（`service` 欄位）判斷。

### §7 排程與自動回退

設定開始時間為 2 分鐘後、回退條件 2% / 1 分鐘 / 最低 100 筆，並發佈。轉為「執行中」後停掉 B 組後端模擬故障，持續送請求：

```powershell
docker compose -f simulation/docker-compose.yml stop backend-b
.\simulation\scripts\send-requests.ps1 -Url http://api.example.local:8080/api/orders/ -Count 3000 -DelayMs 20 -By Status   # 1 分鐘內錯誤停止出現（G-42）
docker compose -f simulation/docker-compose.yml start backend-b
```

## 清除

```powershell
docker compose -f simulation/docker-compose.yml down       # 保留資料庫
docker compose -f simulation/docker-compose.yml down -v    # 連同資料庫一起刪除
```
