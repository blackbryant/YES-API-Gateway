using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Repositories.Implementations;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly GatewayDbContext _db;

    public AppUserRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AppUser?> GetByUsernameForUpdateAsync(string username, CancellationToken ct = default) =>
        _db.AppUsers.FirstOrDefaultAsync(x => x.Username == username, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default) => _db.AppUsers.AnyAsync(ct);

    public void Add(AppUser user) => _db.AppUsers.Add(user);
}
