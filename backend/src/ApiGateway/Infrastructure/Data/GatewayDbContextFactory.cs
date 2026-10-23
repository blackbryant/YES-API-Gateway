using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApiGateway.Infrastructure.Data;

/// <summary>
/// 設計階段（dotnet ef migrations）使用的 DbContext 工廠。
/// 連線字串取自環境變數 ConnectionStrings__GatewayDb；產生 migration 不需要實際連線。
/// </summary>
public sealed class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    private const string FallbackConnectionString = "Host=localhost;Database=yes_gateway;Username=postgres";

    public GatewayDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__GatewayDb") ?? FallbackConnectionString;
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GatewayDbContext(options, TimeProvider.System);
    }
}
