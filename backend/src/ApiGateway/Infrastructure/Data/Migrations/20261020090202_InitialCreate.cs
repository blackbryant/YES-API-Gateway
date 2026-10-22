using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApiGateway.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "登入帳號"),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "顯示名稱"),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "密碼雜湊（ASP.NET Core PasswordHasher）"),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "角色（admin / user）"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false, comment: "下次登入強制變更密碼"),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false, comment: "連續登入失敗次數"),
                    LockoutEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "鎖定解除時間"),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "最後登入時間"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                },
                comment: "帳號表：admin（可編輯、發佈）與 user（唯讀）");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "發生時間"),
                    UserId = table.Column<long>(type: "bigint", nullable: true, comment: "操作者（AppUsers.Id；系統動作為空值）"),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "動作（Create, Update, Delete, Publish, Rollback, Login, LoginFailed）"),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "資料表"),
                    EntityKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "資料名稱"),
                    Before = table.Column<string>(type: "jsonb", nullable: true, comment: "異動前內容"),
                    After = table.Column<string>(type: "jsonb", nullable: true, comment: "異動後內容"),
                    ClientIp = table.Column<IPAddress>(type: "inet", nullable: true, comment: "來源 IP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "稽核日誌表：設定異動、發佈、回滾與登入事件");

            migrationBuilder.CreateTable(
                name: "YarpAuthSchemes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchemeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "方案名稱"),
                    SchemeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "方案類型（JwtBearer, Cookie, ApiKey）"),
                    Authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "驗證伺服器"),
                    Audience = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "受眾"),
                    MetadataRefreshHours = table.Column<int>(type: "integer", nullable: false, comment: "JWKS 金鑰重新取得間隔（小時）"),
                    Options = table.Column<string>(type: "jsonb", nullable: false, comment: "其他參數"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpAuthSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpAuthSchemes_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "驗證方案表：定義閘道如何驗證身分");

            migrationBuilder.CreateTable(
                name: "YarpBlockedPaths",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PathPattern = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "路徑樣式"),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false, comment: "回應碼（404 或 403）"),
                    AllowedCidrs = table.Column<IPNetwork[]>(type: "cidr[]", nullable: false, comment: "例外來源"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpBlockedPaths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpBlockedPaths_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "隱藏內部端點表：符合規則的路徑在閘道直接回應");

            migrationBuilder.CreateTable(
                name: "YarpCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "SNI 網域（支援萬用字元）"),
                    Issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "發行者"),
                    Thumbprint = table.Column<string>(type: "char(40)", nullable: false, comment: "憑證指紋"),
                    NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "生效日"),
                    NotAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "到期日"),
                    StorageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "存放方式（KeyVault, CertStore, EncryptedBlob）"),
                    StorageRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Key Vault URI 或憑證存放區路徑"),
                    EncryptedPfx = table.Column<byte[]>(type: "bytea", nullable: true, comment: "加密後的憑證檔（僅 EncryptedBlob 使用）"),
                    BindEndpoints = table.Column<string[]>(type: "text[]", nullable: false, comment: "綁定的端點"),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false, comment: "是否自動續期"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpCertificates_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "TLS 憑證表：依 SNI 網域選擇憑證，私鑰不存明文");

            migrationBuilder.CreateTable(
                name: "YarpClusters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClusterKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "群組名稱，即 YARP 的 ClusterId"),
                    LoadBalancingPolicy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "負載平衡策略"),
                    HttpVersion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "對後端的 HTTP 版本（1.1 / 2.0 / 3.0）"),
                    HttpVersionPolicy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "版本協商方式"),
                    ActivityTimeoutSec = table.Column<int>(type: "integer", nullable: false, comment: "後端無回應即中斷的秒數"),
                    MaxConnectionsPerServer = table.Column<int>(type: "integer", nullable: true, comment: "每台後端最大連線數"),
                    AcceptAnyServerCertificate = table.Column<bool>(type: "boolean", nullable: false, comment: "略過後端憑證驗證（僅限測試環境）"),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false, comment: "自訂屬性"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpClusters_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "主機群組表：將相同功能的主機打包並定義負載平衡方式");

            migrationBuilder.CreateTable(
                name: "YarpConfigDraft",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false, comment: "固定為 1"),
                    BaseVersionNo = table.Column<string>(type: "char(10)", nullable: true, comment: "草稿的基礎版本"),
                    IsDirty = table.Column<bool>(type: "boolean", nullable: false, comment: "是否有未發佈變更"),
                    ChangeCount = table.Column<int>(type: "integer", nullable: false, comment: "未發佈變更筆數"),
                    LastEditedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後編輯者（AppUsers.Id）"),
                    LastEditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "最後編輯時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpConfigDraft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpConfigDraft_AppUsers_LastEditedBy",
                        column: x => x.LastEditedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "草稿狀態表：固定一列，記錄草稿基礎版本與未發佈變更數");

            migrationBuilder.CreateTable(
                name: "YarpConfigVersions",
                columns: table => new
                {
                    VersionNo = table.Column<string>(type: "char(10)", nullable: false, comment: "版本號（YYYYMMDD + 兩碼流水號）"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "版本狀態（Active / History / RolledBack），Active 同時只有一筆"),
                    BasedOnVersionNo = table.Column<string>(type: "char(10)", nullable: true, comment: "發佈當下的生效版本"),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false, comment: "所有設定表的完整內容（回滾時還原用）"),
                    YarpConfig = table.Column<string>(type: "jsonb", nullable: false, comment: "由快照產生的 YARP ReverseProxy 設定（閘道直接載入）"),
                    Checksum = table.Column<string>(type: "char(64)", nullable: false, comment: "YarpConfig 的 SHA-256"),
                    ChangeSummary = table.Column<string>(type: "jsonb", nullable: false, comment: "異動清單"),
                    ChangeCount = table.Column<int>(type: "integer", nullable: false, comment: "異動筆數"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "發佈說明"),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, comment: "是否鎖定（鎖定後不會被自動淘汰）"),
                    PublishedBy = table.Column<long>(type: "bigint", nullable: false, comment: "發佈者（AppUsers.Id）"),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "發佈時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpConfigVersions", x => x.VersionNo);
                    table.ForeignKey(
                        name: "FK_YarpConfigVersions_AppUsers_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "設定版本表：每次發佈產生一個版本，最多保留 20 版");

            migrationBuilder.CreateTable(
                name: "YarpCorsPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "CORS 策略名稱（內建 default、disable）"),
                    AllowedOrigins = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許的來源"),
                    AllowedMethods = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許的方法（* 代表全部）"),
                    AllowedHeaders = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許的標頭（* 代表全部）"),
                    ExposedHeaders = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許前端讀取的回應標頭"),
                    AllowCredentials = table.Column<bool>(type: "boolean", nullable: false, comment: "是否允許帶 Cookie（為 true 時來源不可為 *）"),
                    PreflightMaxAgeSec = table.Column<int>(type: "integer", nullable: false, comment: "預檢結果快取秒數"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為系統內建（不可刪除）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpCorsPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpCorsPolicies_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "跨來源資源共用策略表");

            migrationBuilder.CreateTable(
                name: "YarpExperiments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExperimentKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "實驗名稱"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "實驗狀態（Draft, Scheduled, Running, Paused, Ended）"),
                    StickyCookieName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "黏著 Cookie 名稱"),
                    StickyDays = table.Column<int>(type: "integer", nullable: false, comment: "黏著天數"),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "開始時間"),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "自動結束時間"),
                    AutoRollbackRule = table.Column<string>(type: "jsonb", nullable: true, comment: "自動回退條件"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpExperiments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpExperiments_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "A/B 實驗表：同一路由依比例、標頭或 Cookie 分流");

            migrationBuilder.CreateTable(
                name: "YarpGatewaySettings",
                columns: table => new
                {
                    Section = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "設定區段（Tls, HeaderHardening, Ddos, ApiVersioning, OpenTelemetry）"),
                    Settings = table.Column<string>(type: "jsonb", nullable: false, comment: "該區段的設定值"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpGatewaySettings", x => x.Section);
                    table.ForeignKey(
                        name: "FK_YarpGatewaySettings_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "全域設定表：以 JSONB 存放 TLS、DDoS、API 版本、OpenTelemetry 等設定");

            migrationBuilder.CreateTable(
                name: "YarpIpRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cidr = table.Column<IPNetwork>(type: "cidr", nullable: false, comment: "IP 範圍"),
                    RuleType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "規則類型（Block / Allow）"),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "來源（Manual / Auto）"),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "原因"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "自動解除時間"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpIpRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpIpRules_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "IP 黑白名單表：手動與 DDoS 自動封鎖的 IP 規則");

            migrationBuilder.CreateTable(
                name: "YarpOutputCachePolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "快取策略名稱"),
                    ExpireSec = table.Column<int>(type: "integer", nullable: false, comment: "快取存活秒數"),
                    VaryByQuery = table.Column<string[]>(type: "text[]", nullable: false, comment: "依這些 Query 參數分開快取"),
                    VaryByHeader = table.Column<string[]>(type: "text[]", nullable: false, comment: "依這些標頭分開快取"),
                    VaryByRouteValue = table.Column<string[]>(type: "text[]", nullable: false, comment: "依這些路由參數分開快取"),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false, comment: "清除快取時使用的標籤"),
                    NoStore = table.Column<bool>(type: "boolean", nullable: false, comment: "明確不快取"),
                    MaxBodySizeBytes = table.Column<long>(type: "bigint", nullable: true, comment: "回應超過此大小不快取"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為系統內建（不可刪除）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpOutputCachePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpOutputCachePolicies_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "輸出快取策略表：命中快取時不轉送到後端");

            migrationBuilder.CreateTable(
                name: "YarpRateLimitPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "限速策略名稱"),
                    LimitType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "限流演算法（FixedWindow, SlidingWindow, TokenBucket, Concurrency）"),
                    PermitLimit = table.Column<int>(type: "integer", nullable: true, comment: "時間區間內最大請求數"),
                    WindowSec = table.Column<int>(type: "integer", nullable: true, comment: "時間區間秒數"),
                    SegmentsPerWindow = table.Column<int>(type: "integer", nullable: true, comment: "滑動視窗分段數"),
                    TokenLimit = table.Column<int>(type: "integer", nullable: true, comment: "令牌桶容量"),
                    TokensPerPeriod = table.Column<int>(type: "integer", nullable: true, comment: "每次補充的令牌數"),
                    ReplenishmentPeriodSec = table.Column<int>(type: "integer", nullable: true, comment: "令牌補充間隔秒數"),
                    QueueLimit = table.Column<int>(type: "integer", nullable: false, comment: "排隊上限（0 代表不排隊）"),
                    QueueProcessingOrder = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "排隊處理順序（OldestFirst / NewestFirst）"),
                    PartitionBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "計數依據（ClientIp, User, ApiKey, Header, Global）"),
                    PartitionKeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "依 ApiKey / Header 時的標頭名稱，依 User 時的 Claim"),
                    RejectionStatusCode = table.Column<int>(type: "integer", nullable: false, comment: "超過上限的回應碼"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為系統內建（不可刪除）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpRateLimitPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpRateLimitPolicies_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "限速策略表：可被多條路由共用的限速規則");

            migrationBuilder.CreateTable(
                name: "YarpTimeoutPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "逾時策略名稱"),
                    TimeoutSec = table.Column<int>(type: "integer", nullable: false, comment: "逾時秒數"),
                    TimeoutStatusCode = table.Column<int>(type: "integer", nullable: false, comment: "逾時回應碼"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為系統內建（不可刪除）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpTimeoutPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpTimeoutPolicies_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "要求逾時策略表");

            migrationBuilder.CreateTable(
                name: "YarpAuthPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "授權策略名稱（內建 default、anonymous）"),
                    AuthSchemeId = table.Column<long>(type: "bigint", nullable: true, comment: "使用的驗證方案（YarpAuthSchemes.Id，空值代表匿名）"),
                    RequireAuthenticatedUser = table.Column<bool>(type: "boolean", nullable: false, comment: "是否需登入"),
                    RequiredRoles = table.Column<string[]>(type: "text[]", nullable: false, comment: "需具備的角色（任一即可）"),
                    RequiredClaims = table.Column<string>(type: "jsonb", nullable: false, comment: "需具備的 Claim"),
                    ForwardClaims = table.Column<string>(type: "jsonb", nullable: false, comment: "Claim 轉成標頭送後端"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為系統內建（不可刪除）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpAuthPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpAuthPolicies_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpAuthPolicies_YarpAuthSchemes_AuthSchemeId",
                        column: x => x.AuthSchemeId,
                        principalTable: "YarpAuthSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "授權策略表：未通過授權的請求不會到達後端");

            migrationBuilder.CreateTable(
                name: "YarpClusterHealthChecks",
                columns: table => new
                {
                    ClusterId = table.Column<long>(type: "bigint", nullable: false, comment: "對應 YarpClusters.Id"),
                    ActiveEnabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用主動健康檢查"),
                    ActivePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "檢查路徑"),
                    ActiveQuery = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "附加 Query 字串"),
                    ActiveIntervalSec = table.Column<int>(type: "integer", nullable: false, comment: "檢查間隔秒數"),
                    ActiveTimeoutSec = table.Column<int>(type: "integer", nullable: false, comment: "單次檢查逾時秒數"),
                    ActivePolicy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "判定策略"),
                    FailureThreshold = table.Column<int>(type: "integer", nullable: false, comment: "連續失敗幾次判定不健康"),
                    PassiveEnabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用被動健康檢查"),
                    PassivePolicy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "被動檢查策略"),
                    ReactivationPeriodSec = table.Column<int>(type: "integer", nullable: false, comment: "被隔離後多久恢復（秒）"),
                    FailureRateLimit = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false, comment: "失敗率門檻"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpClusterHealthChecks", x => x.ClusterId);
                    table.ForeignKey(
                        name: "FK_YarpClusterHealthChecks_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpClusterHealthChecks_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "群組健康檢查表：主動與被動健康檢查規則");

            migrationBuilder.CreateTable(
                name: "YarpClusterSessionAffinity",
                columns: table => new
                {
                    ClusterId = table.Column<long>(type: "bigint", nullable: false, comment: "對應 YarpClusters.Id"),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    Policy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "黏性方式（HashCookie, Cookie, ArrCookie, CustomHeader）"),
                    FailurePolicy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "原主機不可用時的處理（Redistribute, Return503Error）"),
                    AffinityKeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Cookie 或標頭名稱"),
                    CookiePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Cookie Path"),
                    CookieDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Cookie Domain（空值為請求網域）"),
                    CookieExpirationSec = table.Column<int>(type: "integer", nullable: true, comment: "Cookie 有效秒數"),
                    CookieMaxAgeSec = table.Column<int>(type: "integer", nullable: true, comment: "Cookie Max-Age"),
                    CookieHttpOnly = table.Column<bool>(type: "boolean", nullable: false, comment: "是否 HttpOnly"),
                    CookieIsEssential = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為必要 Cookie"),
                    CookieSameSite = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "SameSite 設定（Lax, Strict, None）"),
                    CookieSecurePolicy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Secure 設定（Always, SameAsRequest, None）"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpClusterSessionAffinity", x => x.ClusterId);
                    table.ForeignKey(
                        name: "FK_YarpClusterSessionAffinity_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpClusterSessionAffinity_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "會話黏性表：同一使用者固定送往同一台後端");

            migrationBuilder.CreateTable(
                name: "YarpDestinations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClusterId = table.Column<long>(type: "bigint", nullable: false, comment: "隸屬的群組（YarpClusters.Id）"),
                    DestinationKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "YARP 內部識別碼，與 ClusterId 組合唯一"),
                    Address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "協定 + IP + Port"),
                    HealthAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "健康檢查位址，空值時使用 Address"),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "轉送時覆寫的 Host 標頭"),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false, comment: "自訂屬性"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用（停用後不輸出到 YARP 設定）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpDestinations_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpDestinations_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "實體後端主機表：登記允許被代理的後端主機");

            migrationBuilder.CreateTable(
                name: "YarpExperimentVariants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExperimentId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬實驗（YarpExperiments.Id）"),
                    VariantKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "組別代號，與 ExperimentId 組合唯一"),
                    ClusterId = table.Column<long>(type: "bigint", nullable: false, comment: "導向的群組（YarpClusters.Id）"),
                    WeightPercent = table.Column<short>(type: "smallint", nullable: false, comment: "流量比例 0–100（同一實驗加總須為 100）"),
                    IsControl = table.Column<bool>(type: "boolean", nullable: false, comment: "是否為對照組"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpExperimentVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpExperimentVariants_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpExperimentVariants_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpExperimentVariants_YarpExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "YarpExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "實驗變體表：各組別指向一個群組並設定流量比例");

            migrationBuilder.CreateTable(
                name: "YarpServices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "服務代碼，產生 RouteId 時作為前綴"),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "畫面顯示名稱"),
                    DefaultClusterId = table.Column<long>(type: "bigint", nullable: true, comment: "預設導向的群組（YarpClusters.Id），與 DefaultExperimentId 二擇一"),
                    DefaultExperimentId = table.Column<long>(type: "bigint", nullable: true, comment: "預設導向的 A/B 實驗（YarpExperiments.Id）"),
                    AllowedListeners = table.Column<string[]>(type: "text[]", nullable: false, comment: "可進入的監聽端點（External、Internal）"),
                    RateLimitPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "預設限速策略"),
                    TimeoutPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "預設逾時策略"),
                    AuthPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "預設授權策略"),
                    CorsPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "預設 CORS 策略"),
                    OutputCachePolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "預設快取策略"),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false, comment: "分類標籤"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用（停用時所屬路由都不輸出）"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpServices_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpAuthPolicies_AuthPolicyId",
                        column: x => x.AuthPolicyId,
                        principalTable: "YarpAuthPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpClusters_DefaultClusterId",
                        column: x => x.DefaultClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpCorsPolicies_CorsPolicyId",
                        column: x => x.CorsPolicyId,
                        principalTable: "YarpCorsPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpExperiments_DefaultExperimentId",
                        column: x => x.DefaultExperimentId,
                        principalTable: "YarpExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpOutputCachePolicies_OutputCachePolicyId",
                        column: x => x.OutputCachePolicyId,
                        principalTable: "YarpOutputCachePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpRateLimitPolicies_RateLimitPolicyId",
                        column: x => x.RateLimitPolicyId,
                        principalTable: "YarpRateLimitPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpServices_YarpTimeoutPolicies_TimeoutPolicyId",
                        column: x => x.TimeoutPolicyId,
                        principalTable: "YarpTimeoutPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "服務表：一個服務等於一支對外 API，提供所屬路由共用的預設導向與策略");

            migrationBuilder.CreateTable(
                name: "YarpDestinationHealth",
                columns: table => new
                {
                    DestinationId = table.Column<long>(type: "bigint", nullable: false, comment: "對應 YarpDestinations.Id"),
                    GatewayNode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "回報的閘道節點"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "健康狀態（Healthy / Unhealthy / Unknown）"),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false, comment: "連續失敗次數"),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "最近檢查時間"),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "最近一次失敗原因")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpDestinationHealth", x => new { x.DestinationId, x.GatewayNode });
                    table.ForeignKey(
                        name: "FK_YarpDestinationHealth_YarpDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "YarpDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "主機健康狀態表：各閘道節點回寫的即時健康狀態，不納入設定版本");

            migrationBuilder.CreateTable(
                name: "YarpExperimentRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExperimentId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬實驗（YarpExperiments.Id）"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, comment: "比對順序"),
                    MatchType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "條件類型（Header, Cookie, Query, Default）"),
                    MatchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "標頭、Cookie 或參數名稱"),
                    MatchValue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "比對值（空值代表存在即符合）"),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "符合時的動作（ForceVariant, Sticky, Weighted）"),
                    VariantId = table.Column<long>(type: "bigint", nullable: true, comment: "強制指定的組別（YarpExperimentVariants.Id）"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpExperimentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpExperimentRules_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpExperimentRules_YarpExperimentVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "YarpExperimentVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpExperimentRules_YarpExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "YarpExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "實驗分配規則表：依序比對，先符合者生效");

            migrationBuilder.CreateTable(
                name: "YarpRoutes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬服務（YarpServices.Id）"),
                    RouteKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "路由名稱，即 YARP 的 RouteId"),
                    ClusterId = table.Column<long>(type: "bigint", nullable: true, comment: "流量導向的群組（空值沿用服務）"),
                    ExperimentId = table.Column<long>(type: "bigint", nullable: true, comment: "流量導向的 A/B 實驗，與 ClusterId 二擇一（空值沿用服務）"),
                    MatchPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "網址配對規則"),
                    MatchHosts = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許進來的 Domain"),
                    MatchMethods = table.Column<string[]>(type: "text[]", nullable: false, comment: "允許的 HTTP 方法（空陣列代表全部）"),
                    Order = table.Column<int>(type: "integer", nullable: false, comment: "優先級（數字越小越優先）"),
                    RateLimitPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "覆寫限速策略（空值沿用服務）"),
                    TimeoutPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "覆寫逾時策略（空值沿用服務）"),
                    TimeoutSecOverride = table.Column<int>(type: "integer", nullable: true, comment: "直接指定逾時秒數，優先於逾時策略"),
                    AuthPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "覆寫授權策略（空值沿用服務）"),
                    CorsPolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "覆寫 CORS 策略（空值沿用服務）"),
                    OutputCachePolicyId = table.Column<long>(type: "bigint", nullable: true, comment: "覆寫快取策略（空值沿用服務）"),
                    MaxRequestBodySize = table.Column<long>(type: "bigint", nullable: true, comment: "請求 Body 上限位元組（空值沿用全域設定）"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否啟用"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpAuthPolicies_AuthPolicyId",
                        column: x => x.AuthPolicyId,
                        principalTable: "YarpAuthPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpCorsPolicies_CorsPolicyId",
                        column: x => x.CorsPolicyId,
                        principalTable: "YarpCorsPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "YarpExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpOutputCachePolicies_OutputCachePolicyId",
                        column: x => x.OutputCachePolicyId,
                        principalTable: "YarpOutputCachePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpRateLimitPolicies_RateLimitPolicyId",
                        column: x => x.RateLimitPolicyId,
                        principalTable: "YarpRateLimitPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "YarpServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpRoutes_YarpTimeoutPolicies_TimeoutPolicyId",
                        column: x => x.TimeoutPolicyId,
                        principalTable: "YarpTimeoutPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "入口路由表：設定外部請求如何進來");

            migrationBuilder.CreateTable(
                name: "YarpApiVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬服務（YarpServices.Id）"),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "版本，與 ServiceId 組合唯一"),
                    PublicPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "對外路徑"),
                    BackendPathPrefix = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "轉送後端的路徑前綴"),
                    ClusterId = table.Column<long>(type: "bigint", nullable: false, comment: "目標群組（YarpClusters.Id）"),
                    RouteId = table.Column<long>(type: "bigint", nullable: true, comment: "產生的路由（YarpRoutes.Id）"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "版本狀態（Preview, Current, Deprecated, Disabled）"),
                    SunsetDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "淘汰日期（寫入 Sunset 回應標頭）"),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, comment: "未帶版本時導向此版本"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpApiVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpApiVersions_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpApiVersions_YarpClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "YarpClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YarpApiVersions_YarpRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "YarpRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpApiVersions_YarpServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "YarpServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "API 版本對應表：每一列發佈時於所屬服務下產生一條路由");

            migrationBuilder.CreateTable(
                name: "YarpRouteMatchConditions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬路由（YarpRoutes.Id）"),
                    ConditionType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "條件類型（Header / Query）"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "標頭或參數名稱"),
                    Values = table.Column<string[]>(type: "text[]", nullable: false, comment: "比對值"),
                    Mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "比對方式"),
                    IsCaseSensitive = table.Column<bool>(type: "boolean", nullable: false, comment: "是否區分大小寫"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, comment: "顯示順序"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpRouteMatchConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpRouteMatchConditions_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpRouteMatchConditions_YarpRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "YarpRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "路由比對條件表：依標頭或 Query 參數比對");

            migrationBuilder.CreateTable(
                name: "YarpRouteTransforms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "內部流水號")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteId = table.Column<long>(type: "bigint", nullable: false, comment: "所屬路由（YarpRoutes.Id）"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, comment: "執行順序"),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "轉換方向（Request / Response）"),
                    TransformType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "轉換種類"),
                    Definition = table.Column<string>(type: "jsonb", nullable: false, comment: "YARP 原生格式定義"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "建立時間"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "更新時間"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "最後異動者（AppUsers.Id）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpRouteTransforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarpRouteTransforms_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YarpRouteTransforms_YarpRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "YarpRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "請求與回應轉換表：轉送前後改寫路徑、標頭，依序套用");

            migrationBuilder.InsertData(
                table: "YarpConfigDraft",
                columns: new[] { "Id", "BaseVersionNo", "ChangeCount", "IsDirty", "LastEditedAt", "LastEditedBy" },
                values: new object[] { (short)1, null, 0, false, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Username",
                table: "AppUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpApiVersions_ClusterId",
                table: "YarpApiVersions",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpApiVersions_RouteId",
                table: "YarpApiVersions",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpApiVersions_ServiceId_Version",
                table: "YarpApiVersions",
                columns: new[] { "ServiceId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpApiVersions_UpdatedBy",
                table: "YarpApiVersions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpAuthPolicies_AuthSchemeId",
                table: "YarpAuthPolicies",
                column: "AuthSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpAuthPolicies_PolicyName",
                table: "YarpAuthPolicies",
                column: "PolicyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpAuthPolicies_UpdatedBy",
                table: "YarpAuthPolicies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpAuthSchemes_SchemeName",
                table: "YarpAuthSchemes",
                column: "SchemeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpAuthSchemes_UpdatedBy",
                table: "YarpAuthSchemes",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpBlockedPaths_PathPattern",
                table: "YarpBlockedPaths",
                column: "PathPattern",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpBlockedPaths_UpdatedBy",
                table: "YarpBlockedPaths",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpCertificates_Thumbprint",
                table: "YarpCertificates",
                column: "Thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpCertificates_UpdatedBy",
                table: "YarpCertificates",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpClusterHealthChecks_UpdatedBy",
                table: "YarpClusterHealthChecks",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpClusters_ClusterKey",
                table: "YarpClusters",
                column: "ClusterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpClusters_UpdatedBy",
                table: "YarpClusters",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpClusterSessionAffinity_UpdatedBy",
                table: "YarpClusterSessionAffinity",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpConfigDraft_LastEditedBy",
                table: "YarpConfigDraft",
                column: "LastEditedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpConfigVersions_PublishedBy",
                table: "YarpConfigVersions",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpConfigVersions_Status",
                table: "YarpConfigVersions",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_YarpCorsPolicies_PolicyName",
                table: "YarpCorsPolicies",
                column: "PolicyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpCorsPolicies_UpdatedBy",
                table: "YarpCorsPolicies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpDestinations_ClusterId_DestinationKey",
                table: "YarpDestinations",
                columns: new[] { "ClusterId", "DestinationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpDestinations_UpdatedBy",
                table: "YarpDestinations",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentRules_ExperimentId",
                table: "YarpExperimentRules",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentRules_UpdatedBy",
                table: "YarpExperimentRules",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentRules_VariantId",
                table: "YarpExperimentRules",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperiments_ExperimentKey",
                table: "YarpExperiments",
                column: "ExperimentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperiments_UpdatedBy",
                table: "YarpExperiments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentVariants_ClusterId",
                table: "YarpExperimentVariants",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentVariants_ExperimentId_VariantKey",
                table: "YarpExperimentVariants",
                columns: new[] { "ExperimentId", "VariantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentVariants_UpdatedBy",
                table: "YarpExperimentVariants",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpGatewaySettings_UpdatedBy",
                table: "YarpGatewaySettings",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpIpRules_UpdatedBy",
                table: "YarpIpRules",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpOutputCachePolicies_PolicyName",
                table: "YarpOutputCachePolicies",
                column: "PolicyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpOutputCachePolicies_UpdatedBy",
                table: "YarpOutputCachePolicies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRateLimitPolicies_PolicyName",
                table: "YarpRateLimitPolicies",
                column: "PolicyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpRateLimitPolicies_UpdatedBy",
                table: "YarpRateLimitPolicies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRouteMatchConditions_RouteId",
                table: "YarpRouteMatchConditions",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRouteMatchConditions_UpdatedBy",
                table: "YarpRouteMatchConditions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_AuthPolicyId",
                table: "YarpRoutes",
                column: "AuthPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_ClusterId",
                table: "YarpRoutes",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_CorsPolicyId",
                table: "YarpRoutes",
                column: "CorsPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_ExperimentId",
                table: "YarpRoutes",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_OutputCachePolicyId",
                table: "YarpRoutes",
                column: "OutputCachePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_RateLimitPolicyId",
                table: "YarpRoutes",
                column: "RateLimitPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_RouteKey",
                table: "YarpRoutes",
                column: "RouteKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_ServiceId",
                table: "YarpRoutes",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_TimeoutPolicyId",
                table: "YarpRoutes",
                column: "TimeoutPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRoutes_UpdatedBy",
                table: "YarpRoutes",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRouteTransforms_RouteId",
                table: "YarpRouteTransforms",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpRouteTransforms_UpdatedBy",
                table: "YarpRouteTransforms",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_AuthPolicyId",
                table: "YarpServices",
                column: "AuthPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_CorsPolicyId",
                table: "YarpServices",
                column: "CorsPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_DefaultClusterId",
                table: "YarpServices",
                column: "DefaultClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_DefaultExperimentId",
                table: "YarpServices",
                column: "DefaultExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_OutputCachePolicyId",
                table: "YarpServices",
                column: "OutputCachePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_RateLimitPolicyId",
                table: "YarpServices",
                column: "RateLimitPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_ServiceKey",
                table: "YarpServices",
                column: "ServiceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_TimeoutPolicyId",
                table: "YarpServices",
                column: "TimeoutPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpServices_UpdatedBy",
                table: "YarpServices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_YarpTimeoutPolicies_PolicyName",
                table: "YarpTimeoutPolicies",
                column: "PolicyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarpTimeoutPolicies_UpdatedBy",
                table: "YarpTimeoutPolicies",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "YarpApiVersions");

            migrationBuilder.DropTable(
                name: "YarpBlockedPaths");

            migrationBuilder.DropTable(
                name: "YarpCertificates");

            migrationBuilder.DropTable(
                name: "YarpClusterHealthChecks");

            migrationBuilder.DropTable(
                name: "YarpClusterSessionAffinity");

            migrationBuilder.DropTable(
                name: "YarpConfigDraft");

            migrationBuilder.DropTable(
                name: "YarpConfigVersions");

            migrationBuilder.DropTable(
                name: "YarpDestinationHealth");

            migrationBuilder.DropTable(
                name: "YarpExperimentRules");

            migrationBuilder.DropTable(
                name: "YarpGatewaySettings");

            migrationBuilder.DropTable(
                name: "YarpIpRules");

            migrationBuilder.DropTable(
                name: "YarpRouteMatchConditions");

            migrationBuilder.DropTable(
                name: "YarpRouteTransforms");

            migrationBuilder.DropTable(
                name: "YarpDestinations");

            migrationBuilder.DropTable(
                name: "YarpExperimentVariants");

            migrationBuilder.DropTable(
                name: "YarpRoutes");

            migrationBuilder.DropTable(
                name: "YarpServices");

            migrationBuilder.DropTable(
                name: "YarpAuthPolicies");

            migrationBuilder.DropTable(
                name: "YarpClusters");

            migrationBuilder.DropTable(
                name: "YarpCorsPolicies");

            migrationBuilder.DropTable(
                name: "YarpExperiments");

            migrationBuilder.DropTable(
                name: "YarpOutputCachePolicies");

            migrationBuilder.DropTable(
                name: "YarpRateLimitPolicies");

            migrationBuilder.DropTable(
                name: "YarpTimeoutPolicies");

            migrationBuilder.DropTable(
                name: "YarpAuthSchemes");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
