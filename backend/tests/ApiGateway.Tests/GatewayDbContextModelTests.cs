using ApiGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ApiGateway.Tests;

/// <summary>以 Npgsql provider 建立模型並產生 DDL，不需實際資料庫連線</summary>
public class GatewayDbContextModelTests
{
    private static GatewayDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GatewayDbContext>().UseNpgsql("Host=localhost").Options, TimeProvider.System);

    [Fact]
    public void Model_GeneratesPostgresDdl()
    {
        using var db = CreateContext();

        var script = db.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE \"YarpClusters\"", script);
        Assert.Contains("cidr[]", script);
        Assert.Contains("jsonb", script);
        Assert.Contains("WHERE \"Status\" = 'Active'", script);
    }

    [Fact]
    public void EveryTableAndColumn_HasComment()
    {
        using var db = CreateContext();

        var model = db.GetService<IDesignTimeModel>().Model;
        var missing = model.GetEntityTypes()
            .SelectMany(t => new[] { t.GetComment() is null ? t.GetTableName() : null }
                .Concat(t.GetProperties().Where(p => p.GetComment() is null).Select(p => $"{t.GetTableName()}.{p.Name}")))
            .Where(x => x is not null)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Model_HasAllSchemaTables()
    {
        using var db = CreateContext();

        Assert.Equal(27, db.Model.GetEntityTypes().Count());
    }
}
