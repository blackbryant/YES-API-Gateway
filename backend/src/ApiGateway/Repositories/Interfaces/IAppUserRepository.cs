using ApiGateway.Models.Entities;

namespace ApiGateway.Repositories.Interfaces;

public interface IAppUserRepository
{
    /// <summary>唯讀查詢（不追蹤）</summary>
    Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>取得追蹤中的 Entity，修改後由 Service 呼叫 IUnitOfWork.SaveChangesAsync</summary>
    Task<AppUser?> GetByUsernameForUpdateAsync(string username, CancellationToken ct = default);

    Task<bool> AnyAsync(CancellationToken ct = default);

    void Add(AppUser user);
}
