using ApiGateway.Infrastructure.Data;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ApiGateway.Repositories.Implementations;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly GatewayDbContext _db;

    public UnitOfWork(GatewayDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) => _db.Database.BeginTransactionAsync(ct);
}
