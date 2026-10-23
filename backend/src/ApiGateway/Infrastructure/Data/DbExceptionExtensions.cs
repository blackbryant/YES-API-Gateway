using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApiGateway.Infrastructure.Data;

public static class DbExceptionExtensions
{
    /// <summary>是否為 PostgreSQL 唯一鍵衝突（23505），用於將並發新增的重複鍵轉為 409</summary>
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
