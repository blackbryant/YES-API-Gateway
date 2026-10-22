namespace ApiGateway.Tests.Infrastructure;

/// <summary>需要實際 PostgreSQL 的測試；未設定環境變數 GATEWAY_TEST_DB 時略過</summary>
public sealed class DbFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "GATEWAY_TEST_DB";

    public DbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"{ConnectionStringVariable} not set";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
