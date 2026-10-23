using Microsoft.EntityFrameworkCore.Storage;

namespace ApiGateway.Repositories.Interfaces;

/// <summary>
/// 交易邊界。Repository 只「暫存」異動（Add / Remove / 修改追蹤中的 Entity），
/// 由 Service 呼叫 SaveChangesAsync 一次寫入（單次 SaveChanges 即為一個資料庫交易）。
/// 需跨多次 SaveChanges 或混用原生 SQL 時，才以 BeginTransactionAsync 開明確交易。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
