# 模擬環境（001-02 靜態 / 動態路由與 A/B 測試）

以 Docker 網路建立閘道、PostgreSQL 與假後端，依 `yes-specs/001-02-dynamic-routing-ab-test/quickstart.md` 驗證端對端行為。網路分層、固定 IP 與網域別名模擬實際部署：用戶端只能經由閘道存取後端。

```
   edge 172.30.0.0/24（對外）                       internal 172.31.0.0/24（內部，internal: true）
  ┌───────────────────────────────────┐            ┌──────────────────────────────────────────────┐
  │ client-a        172.30.0.101      │            │ backend-a       172.31.0.11:9001  REST 測試  │
  │ client-b        172.30.0.102      │            │ backend-b       172.31.0.12:9002  REST 測試  │
  │                                   │            │ backend-error   172.31.0.13:9003  固定 500    │
  │ gateway-1       172.30.0.10 ──────┼────────────┼ 172.31.0.10     backend-whoami 172.31.0.14:9004│
  │   api.example.local               │            │ gateway-2       172.31.0.20（選用）           │
  │   api-test.example.local          │            │ postgres        172.31.0.5                    │
  │ gateway-2       172.30.0.20       │            └──────────────────────────────────────────────┘
  │   gw2.example.local（選用）       │              ops 172.29.0.0/24：只負責把 postgres 與假後端
  └───────────────────────────────────┘              的埠開放給本機，用戶端不在此網路

  本機：localhost:8080 → gateway-1、8081 → gateway-2、5433 → postgres、9001～9004 → 假後端
```

| 從哪裡 | 可以連到 | 連不到 |
|---|---|---|
| `client-a` / `client-b` | 閘道（`api.example.local:8080` 等網域別名、`gateway-1:8080`、`172.30.0.10:8080`） | 後端、資料庫 |
| 閘道 | 後端（`backend-a:9001`、`172.31.0.11:9001`…）、資料庫 | — |
| 本機 | `localhost:8080`、`8081`、`5433`、`9001`～`9004` | 容器 IP |

網段若與公司 VPN 或內網衝突，修改 `docker-compose.yml` 的 `subnet` 與各 `ipv4_address`。

## 啟動

於 repo 根目錄執行：

```powershell
Copy-Item simulation/.env.example simulation/.env
docker compose -f simulation/docker-compose.yml up -d --build
docker compose -f simulation/docker-compose.yml logs -f gateway-1
```

- 管理介面：<http://localhost:8080/admin/>（`/` 會導向這裡），帳號密碼見 `simulation/.env` 的 `ADMIN_USERNAME` / `ADMIN_PASSWORD`。
- `/admin` 以外沒有命中入口的路徑一律回 404；看到 404 而不是預期的後端回應，代表入口沒有命中（檢查路徑與網域）。
- Swagger：<http://localhost:8080/swagger>（`GATEWAY_ENVIRONMENT=Development` 時）。
- 修改後端程式後重新建置：`docker compose -f simulation/docker-compose.yml up -d --build gateway-1`。
- 從舊版（單一 `gw-net` 網路）升級：先 `docker compose -f simulation/docker-compose.yml down --remove-orphans` 再啟動，資料庫資料會保留。

## 入口與主機怎麼填

| 欄位 | 填什麼 | 範例 |
|---|---|---|
| 入口網域 | 用戶端連入**閘道**時網址上的網域；留空表示所有網域 | 留空、`api.example.local:8080`、`localhost:8080` |
| 主機位址 | 閘道轉送的**後端**位址 | `http://backend-a:9001` 或 `http://172.31.0.11:9001` |

網域比對的是請求的 `Host` 標頭（含埠，預設埠 80 / 443 除外）。瀏覽器從本機打 `http://localhost:8080` 時 Host 為 `localhost:8080`；`client-a` 打 `http://api.example.local:8080` 時 Host 為 `api.example.local:8080`。不要把後端位址（如 `backend-a:9001`）填進入口網域，發佈時會出現警告。

依網域分流範例：入口 A 網域 `api.example.local:8080` → `backend-a`，入口 B 網域 `api-test.example.local:8080` → `backend-b`，路徑相同。

```powershell
.\simulation\scripts\send-requests.ps1 -From client-a -Url http://api.example.local:8080/api/orders/ -Count 10        # backend-a
.\simulation\scripts\send-requests.ps1 -From client-a -Url http://api-test.example.local:8080/api/orders/ -Count 10   # backend-b
.\simulation\scripts\send-requests.ps1 -From client-a -Url http://gateway-1:8080/api/orders/ -Count 10 -By Status     # 404（G-12，網域不符）
```

容器內的 `localhost` 是容器自己，不是本機。要連本機上的服務時，主機位址填 `http://host.docker.internal:<埠>`（閘道需能連外，目前 gateway 掛在 edge 網路上即可）。

本機 `dotnet run` 也可以只用這裡的資料庫與假後端（主機位址改填 `http://localhost:9001` ～ `9004`）：

```powershell
docker compose -f simulation/docker-compose.yml up -d postgres backend-a backend-b backend-error backend-whoami
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

以下假設入口為路徑 `/backend-a/{**catch-all}`、方法勾選 GET / POST / PUT / PATCH / DELETE、網域留空、主機 `http://backend-a:9001`。

瀏覽器可以直接測 GET：

- <http://localhost:8080/backend-a/items?tag=fruit&sort=-price>
- <http://localhost:8080/backend-a/items/2>
- <http://localhost:8080/backend-a/anything/else?x=1&x=2>（回顯模式）

PowerShell（`Invoke-RestMethod`，可避免 Windows PowerShell 5.1 中 `curl.exe` 的 JSON 引號問題）：

```powershell
$G = 'http://localhost:8080/backend-a'
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

從用戶端容器（`sh` 內引號正常，可直接用 curl）：

```powershell
docker compose -f simulation/docker-compose.yml exec client-a sh -c 'curl -s -X PATCH http://api.example.local:8080/backend-a/items/1 -H "Content-Type: application/json" -d "{\"price\":99}"'
```

驗證入口的方法限制（G-11）：入口只勾 GET、POST 並發佈後，`Invoke-RestMethod "$G/items/1" -Method Delete` 應該回 **405**，而且由閘道直接回應，請求不會到達後端（後端 log 看不到這筆）：

```powershell
docker compose -f simulation/docker-compose.yml logs -f backend-a     # 後端收到的每筆請求
```

## 測試腳本

`scripts/send-requests.ps1` 連續送出請求並依回應內容（`-By Body`，預設）或狀態碼（`-By Status`）統計：

| 參數 | 說明 |
|---|---|
| `-From client-a` / `client-b` | 從 edge 網路內的用戶端容器送出，可使用網域別名，來源 IP 為該用戶端的固定 IP；未指定時從本機送出 |
| `-Header @{ 'X-Exp-Group' = 'B' }` | 附加標頭 |
| `-KeepCookie` | 保留閘道回傳的 Cookie（驗證黏著） |
| `-DelayMs 20` | 每次請求間隔 |

從瀏覽器或本機送出時，閘道看到的來源 IP 是 Docker 的閘道位址，不是本機 IP；與來源 IP 有關的測試（日後的 IP 白名單 / 封鎖）請用 `-From`。

## 驗證步驟（對應 quickstart）

以下 `-Url` 以容器內閘道為例（`http://localhost:8080`）。

### §3 靜態路由

```powershell
curl.exe -i http://localhost:8080/health                    # 200（G-01）
docker compose -f simulation/docker-compose.yml stop postgres
curl.exe -i http://localhost:8080/health                    # 仍 200（G-02）
docker compose -f simulation/docker-compose.yml start postgres
```

### §4 新增服務並發佈

服務「訂單 API」：入口 `/api/orders/{**catch-all}`、方法 GET/POST、移除路徑前綴 `/api/orders`、主機 `http://backend-a:9001`，然後發佈。

```powershell
curl.exe http://localhost:8080/api/orders/                  # backend-a（G-10、G-15）
curl.exe -i -X DELETE http://localhost:8080/api/orders/1    # 405（G-11）
```

移除路徑前綴：將主機暫時改為 `http://backend-whoami:9004` 並發佈，`curl.exe http://localhost:8080/api/orders/abc` 回應的 `GET /abc` 應該不含 `/api/orders`。

熱更新（G-20）：一個視窗持續送請求，另一邊把主機改為 `http://backend-b:9002` 並發佈。

```powershell
.\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 2000 -By Status   # 預期全為 200
docker compose -f simulation/docker-compose.yml ps gateway-1                                          # 容器未重啟
```

### §6 A/B 測試

分流改為 A/B：A 組 `http://backend-a:9001` 90%、B 組 `http://backend-b:9002` 10%，黏著 7 天，強制分組標頭 `X-Exp-Group`，然後發佈。

```powershell
.\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 1000                         # backend-b 約 70～130（G-30）
.\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 20 -Header @{ 'X-Exp-Group' = 'B' }   # 全為 backend-b（G-32）
.\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 50 -KeepCookie              # 全落在同一組（黏著）
curl.exe -i -b ".yes.exp.orders-api=B" http://localhost:8080/api/orders/                                         # backend-b，且無 Set-Cookie（G-31）
```

組別不會出現在回應標頭（G-50），請以回應內容（假後端名稱）判斷。

### §7 排程與自動回退

設定開始時間為 2 分鐘後、回退條件 2% / 1 分鐘 / 最低 100 筆，並發佈。轉為「執行中」後，把 B 組主機改為 `http://backend-error:9003` 並發佈，持續送請求：

```powershell
.\simulation\scripts\send-requests.ps1 -Url http://localhost:8080/api/orders/ -Count 3000 -DelayMs 20   # 1 分鐘內 HTTP 500 停止出現（G-42）
```

也可以直接停掉 B 組後端模擬故障：`docker compose -f simulation/docker-compose.yml stop backend-b`。

### §9 多節點

```powershell
docker compose -f simulation/docker-compose.yml --profile multi-node up -d gateway-2
```

在 <http://localhost:8080/admin/> 發佈後：

- `curl.exe http://localhost:8081/api/orders/` 在 5 秒內轉送新設定（G-22）。
- `GET /api/v1/gateway-nodes` 中 `gw-1`、`gw-2` 皆 `synced: true`。

### 網路隔離

```powershell
.\simulation\scripts\send-requests.ps1 -From client-a -Url http://backend-a:9001/ -Count 3       # ERROR: 無法連線（用戶端不能繞過閘道）
.\simulation\scripts\send-requests.ps1 -From client-a -Url http://172.31.0.11:9001/ -Count 3     # 同上
docker compose -f simulation/docker-compose.yml exec client-a curl -s http://api.example.local:8080/health   # Healthy
```

## 清除

```powershell
docker compose -f simulation/docker-compose.yml --profile multi-node down       # 保留資料庫
docker compose -f simulation/docker-compose.yml --profile multi-node down -v    # 連同資料庫一起刪除
```
