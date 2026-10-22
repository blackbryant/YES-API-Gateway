# YES API Gateway

## 部署前必要調整

### 1. 設定 `backend/.env`（帳號密碼與密鑰）

所有帳號密碼都放在 `backend/.env`，**不寫在 `appsettings*.json`**。`.env` 不納入版控，第一次請由範本複製：

```powershell
Copy-Item backend/.env.example backend/.env
```

| 變數 | 說明 |
|---|---|
| `POSTGRES_HOST` / `POSTGRES_PORT` | 資料庫主機與埠（預設 `localhost` / `5432`） |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | 資料庫名稱、帳號、密碼 |
| `AUTH_TOKEN_SECRET` | JWT（HS256）簽章密鑰，UTF-8 至少 32 bytes |
| `ADMIN_USERNAME` / `ADMIN_PASSWORD` | 初始管理者；僅在資料庫尚無任何使用者時建立，首次登入須變更密碼 |

**`AUTH_TOKEN_SECRET` 正式環境務必使用新產生的值**，外洩即可被偽造 token。多台實例必須使用同一把；更換後既有 token 全部失效（使用者需重新登入）。

```powershell
openssl rand -base64 48
```

讀取方式：後端啟動時自 content root 往上層尋找 `.env`。同名的系統環境變數優先於檔案，因此 Docker 可直接使用：

```powershell
docker run --env-file backend/.env ...
```

### 2. 建立資料庫（`backend/db/`）

兩支腳本皆可重複執行，帳號密碼請與 `backend/.env` 一致。

| 檔案 | 執行身分 | 內容 |
|---|---|---|
| `01-init-database.sql` | PostgreSQL 超級使用者 | 建立帳號（解除連線數限制）、建立資料庫並設為擁有者、授予 `public` schema 權限（PostgreSQL 15 起一般帳號預設不可建表） |
| `02-schema.sql` | 應用程式帳號 | 建立資料表（EF Core migrations 產生） |

```powershell
psql -h localhost -U postgres -v app_user=yesuser -v app_password='<密碼>' -v app_db=yes_gateway -f backend/db/01-init-database.sql
psql -h localhost -U yesuser -d yes_gateway -f backend/db/02-schema.sql
```

Development 環境（`Database:ApplyMigrationsOnStartup=true`）啟動時會自動套用 migration，只需執行 `01-init-database.sql`。新增 migration 後請重新產生 `02-schema.sql`：

```powershell
cd backend
dotnet ef migrations script --idempotent --project src/ApiGateway --output db/02-schema.sql
```
