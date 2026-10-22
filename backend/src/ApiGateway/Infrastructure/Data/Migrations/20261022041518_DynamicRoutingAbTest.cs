using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiGateway.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DynamicRoutingAbTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "YarpExperiments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "設定狀態（Draft / Running），有效狀態由執行期計算",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "實驗狀態（Draft, Scheduled, Running, Paused, Ended）");

            migrationBuilder.AddColumn<long>(
                name: "ServiceId",
                table: "YarpExperiments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "所屬服務（YarpServices.Id）");

            migrationBuilder.AlterColumn<string>(
                name: "ClusterKey",
                table: "YarpClusters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "系統產生 ULID，即 YARP 的 ClusterId",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "群組名稱，即 YARP 的 ClusterId");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "YarpClusters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                comment: "畫面顯示名稱");

            migrationBuilder.AddColumn<long>(
                name: "OwnerServiceId",
                table: "YarpClusters",
                type: "bigint",
                nullable: true,
                comment: "由服務自動建立時的所屬服務（YarpServices.Id）");

            // 既有群組以 ClusterKey 回填顯示名稱
            migrationBuilder.Sql("UPDATE \"YarpClusters\" SET \"DisplayName\" = \"ClusterKey\" WHERE \"DisplayName\" = '';");

            // 既有實驗依引用它的服務 / 路由回填所屬服務；無法對應者為孤兒資料，刪除後才能建立外鍵
            migrationBuilder.Sql("""
                UPDATE "YarpExperiments" e SET "ServiceId" = s."Id"
                FROM "YarpServices" s WHERE s."DefaultExperimentId" = e."Id" AND e."ServiceId" = 0;
                UPDATE "YarpExperiments" e SET "ServiceId" = r."ServiceId"
                FROM "YarpRoutes" r WHERE r."ExperimentId" = e."Id" AND e."ServiceId" = 0;
                DELETE FROM "YarpExperiments" WHERE "ServiceId" = 0;
                """);

            migrationBuilder.CreateTable(
                name: "YarpExperimentRuntime",
                columns: table => new
                {
                    ExperimentKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "實驗名稱（對應生效版本中的 ExperimentKey）"),
                    OverrideStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "覆寫狀態（Paused / Ended；空值代表無覆寫）"),
                    IsAutoRolledBack = table.Column<bool>(type: "boolean", nullable: false, comment: "是否由自動回退造成的暫停"),
                    TriggeredMetric = table.Column<string>(type: "jsonb", nullable: true, comment: "自動回退觸發時的指標"),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "自動回退觸發時間"),
                    TriggeredByNode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "觸發自動回退的閘道節點"),
                    LastScheduleEvent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "已記錄的排程事件（Started / Ended），避免多節點重複寫稽核"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "手動操作者（AppUsers.Id），系統操作為空值"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "最後更新時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpExperimentRuntime", x => x.ExperimentKey);
                    table.ForeignKey(
                        name: "FK_YarpExperimentRuntime_AppUsers_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "實驗執行期狀態表：手動暫停、結束與自動回退的覆寫，不納入設定版本");

            migrationBuilder.CreateTable(
                name: "YarpGatewayNodes",
                columns: table => new
                {
                    NodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "節點識別（Gateway:NodeId，未設定時為機器名稱）"),
                    LoadedVersionNo = table.Column<string>(type: "char(10)", nullable: true, comment: "目前載入的版本號"),
                    LoadedChecksum = table.Column<string>(type: "char(64)", nullable: true, comment: "目前載入版本的校驗碼"),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "最後心跳時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarpGatewayNodes", x => x.NodeId);
                },
                comment: "閘道節點表：各節點心跳與目前載入的設定版本，用於判斷同步狀態");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperiments_ServiceId",
                table: "YarpExperiments",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpClusters_OwnerServiceId",
                table: "YarpClusters",
                column: "OwnerServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_YarpExperimentRuntime_UpdatedBy",
                table: "YarpExperimentRuntime",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_YarpClusters_YarpServices_OwnerServiceId",
                table: "YarpClusters",
                column: "OwnerServiceId",
                principalTable: "YarpServices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_YarpExperiments_YarpServices_ServiceId",
                table: "YarpExperiments",
                column: "ServiceId",
                principalTable: "YarpServices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YarpClusters_YarpServices_OwnerServiceId",
                table: "YarpClusters");

            migrationBuilder.DropForeignKey(
                name: "FK_YarpExperiments_YarpServices_ServiceId",
                table: "YarpExperiments");

            migrationBuilder.DropTable(
                name: "YarpExperimentRuntime");

            migrationBuilder.DropTable(
                name: "YarpGatewayNodes");

            migrationBuilder.DropIndex(
                name: "IX_YarpExperiments_ServiceId",
                table: "YarpExperiments");

            migrationBuilder.DropIndex(
                name: "IX_YarpClusters_OwnerServiceId",
                table: "YarpClusters");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "YarpExperiments");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "YarpClusters");

            migrationBuilder.DropColumn(
                name: "OwnerServiceId",
                table: "YarpClusters");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "YarpExperiments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "實驗狀態（Draft, Scheduled, Running, Paused, Ended）",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "設定狀態（Draft / Running），有效狀態由執行期計算");

            migrationBuilder.AlterColumn<string>(
                name: "ClusterKey",
                table: "YarpClusters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "群組名稱，即 YARP 的 ClusterId",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "系統產生 ULID，即 YARP 的 ClusterId");
        }
    }
}
