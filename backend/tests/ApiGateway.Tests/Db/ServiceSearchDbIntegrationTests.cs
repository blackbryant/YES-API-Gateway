using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Implementations;
using ApiGateway.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Tests.Db;

/// <summary>
/// 服務列表排序（需實際 PostgreSQL，GATEWAY_TEST_DB）：網域排序使用陣列欄位的子查詢，只能在真實資料庫驗證 SQL 轉譯。
/// </summary>
public sealed class ServiceSearchDbIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;

    public ServiceSearchDbIntegrationTests()
    {
        var baseConnection = DbFactAttribute.ConnectionString ?? string.Empty;
        _connectionString = baseConnection.Length == 0
            ? string.Empty
            : new Npgsql.NpgsqlConnectionStringBuilder(baseConnection) { Database = $"yes_gw_test_{Guid.NewGuid():N}" }.ConnectionString;
    }

    private GatewayDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<GatewayDbContext>().UseNpgsql(_connectionString).Options, TimeProvider.System);

    public async Task InitializeAsync()
    {
        if (_connectionString.Length == 0)
        {
            return;
        }

        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        // 名稱、代碼、網域三種排序結果各不相同
        AddService(db, "b-orders", "訂單", [("/orders/{**catch-all}", ["shop.example.com"]), ("/v2/orders/{**catch-all}", ["api.example.com"])]);
        AddService(db, "c-members", "會員", [("/members/{**catch-all}", ["zeta.example.com"])]);
        AddService(db, "a-reports", "報表", [("/reports/{**catch-all}", [])]);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString.Length == 0)
        {
            return;
        }

        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
    }

    private static void AddService(GatewayDbContext db, string key, string name, (string Path, string[] Hosts)[] routes)
    {
        var service = new YarpService { ServiceKey = key, DisplayName = name, IsActive = true };
        db.Services.Add(service);
        db.SaveChanges();
        var seq = 1;
        foreach (var (path, hosts) in routes)
        {
            db.Routes.Add(new YarpRoute { ServiceId = service.Id, RouteKey = $"{key}-{seq}", MatchPath = path, MatchHosts = hosts, Order = RouteRules.DefaultOrder + seq, IsActive = true });
            seq++;
        }
    }

    private async Task<string[]> KeysAsync(ServiceSortField sortBy, bool sortDesc)
    {
        await using var db = CreateDb();
        var (items, _) = await new ServiceRepository(db).SearchAsync(null, null, sortBy, sortDesc, 0, 20);
        return items.Select(x => x.Service.ServiceKey).ToArray();
    }

    [DbTheory]
    [InlineData(ServiceSortField.ServiceKey, false, new[] { "a-reports", "b-orders", "c-members" })]
    [InlineData(ServiceSortField.ServiceKey, true, new[] { "c-members", "b-orders", "a-reports" })]
    // 依各服務最前面的網域：b-orders 為 api.example.com；沒有網域的 a-reports 升降冪都排最後
    [InlineData(ServiceSortField.Host, false, new[] { "b-orders", "c-members", "a-reports" })]
    [InlineData(ServiceSortField.Host, true, new[] { "c-members", "b-orders", "a-reports" })]
    public async Task Search_SortsByField(ServiceSortField sortBy, bool sortDesc, string[] expected)
    {
        Assert.Equal(expected, await KeysAsync(sortBy, sortDesc));
    }

    [DbFact]
    public async Task Search_ReturnsPathsInRouteOrderAndSortedHosts()
    {
        await using var db = CreateDb();
        var (items, _) = await new ServiceRepository(db).SearchAsync("orders", null, ServiceSortField.Name, false, 0, 20);

        var row = Assert.Single(items);
        Assert.Equal(["/orders/{**catch-all}", "/v2/orders/{**catch-all}"], row.Paths);
        Assert.Equal(["api.example.com", "shop.example.com"], row.Hosts);
    }
}
