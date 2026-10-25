using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Repositories.Implementations;

public sealed class ServiceRepository : IServiceRepository
{
    private readonly GatewayDbContext _db;

    public ServiceRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<ServiceListRow> Items, int TotalCount)> SearchAsync(
        string? keyword, string? tag, ServiceSortField sortBy, bool sortDesc, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Services.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{SqlLike.Escape(keyword.Trim())}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.DisplayName, pattern, SqlLike.EscapeCharacter)
                || EF.Functions.ILike(s.ServiceKey, pattern, SqlLike.EscapeCharacter)
                || _db.Routes.Any(r => r.ServiceId == s.Id
                    && (EF.Functions.ILike(r.MatchPath, pattern, SqlLike.EscapeCharacter)
                        || r.MatchHosts.Any(h => EF.Functions.ILike(h, pattern, SqlLike.EscapeCharacter)))));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalized = tag.Trim();
            query = query.Where(s => s.Tags.Contains(normalized));
        }

        var total = await query.CountAsync(ct);
        var rows = query.Select(s => new
        {
            Service = s,
            RouteCount = _db.Routes.Count(r => r.ServiceId == s.Id),
            // 排序用：入口網域中字母順序最前者（網域已於儲存時轉小寫）；沒有網域時為 null
            FirstHost = _db.Routes.Where(r => r.ServiceId == s.Id).SelectMany(r => r.MatchHosts).Min(),
        });

        // 依指定欄位排序後再以名稱、Id 為次序，確保分頁穩定；網域排序時沒有網域的服務不論升降冪都排最後
        var ordered = sortBy switch
        {
            ServiceSortField.ServiceKey => sortDesc ? rows.OrderByDescending(x => x.Service.ServiceKey) : rows.OrderBy(x => x.Service.ServiceKey),
            ServiceSortField.Host => sortDesc
                ? rows.OrderBy(x => x.FirstHost == null).ThenByDescending(x => x.FirstHost)
                : rows.OrderBy(x => x.FirstHost == null).ThenBy(x => x.FirstHost),
            _ => sortDesc ? rows.OrderByDescending(x => x.Service.DisplayName) : rows.OrderBy(x => x.Service.DisplayName),
        };
        var page = await ordered
            .ThenBy(x => x.Service.DisplayName)
            .ThenBy(x => x.Service.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        // 網域與路徑彙整：一次取回本頁所有入口（不逐筆查詢）
        var ids = page.Select(x => x.Service.Id).ToList();
        var routes = await _db.Routes.AsNoTracking()
            .Where(r => ids.Contains(r.ServiceId))
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Id)
            .Select(r => new { r.ServiceId, r.MatchHosts, r.MatchPath })
            .ToListAsync(ct);
        var routesByService = routes.ToLookup(x => x.ServiceId);
        string[] HostsOf(long id) => routesByService[id].SelectMany(x => x.MatchHosts).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] PathsOf(long id) => routesByService[id].Select(x => x.MatchPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return (page.Select(x => new ServiceListRow(x.Service, x.RouteCount, HostsOf(x.Service.Id), PathsOf(x.Service.Id))).ToList(), total);
    }

    public Task<bool> ExistsByKeyAsync(string serviceKey, long? excludeId, CancellationToken ct = default) =>
        _db.Services.AnyAsync(x => x.ServiceKey == serviceKey && (excludeId == null || x.Id != excludeId), ct);

    public async Task<ServiceAggregate?> GetAggregateAsync(long id, bool tracking, CancellationToken ct = default)
    {
        var service = await Query<YarpService>(tracking).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null)
        {
            return null;
        }

        var routes = await Query<YarpRoute>(tracking).Where(x => x.ServiceId == id).OrderBy(x => x.Order).ThenBy(x => x.Id).ToListAsync(ct);
        var routeIds = routes.Select(r => r.Id).ToList();
        var transforms = await Query<YarpRouteTransform>(tracking).Where(x => routeIds.Contains(x.RouteId)).OrderBy(x => x.SortOrder).ToListAsync(ct);

        var experiment = await Query<YarpExperiment>(tracking).FirstOrDefaultAsync(x => x.ServiceId == id, ct);
        var variants = experiment is null
            ? []
            : await Query<YarpExperimentVariant>(tracking).Where(x => x.ExperimentId == experiment.Id).OrderBy(x => x.VariantKey).ToListAsync(ct);
        var rules = experiment is null
            ? []
            : await Query<YarpExperimentRule>(tracking).Where(x => x.ExperimentId == experiment.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);

        var referencedClusterIds = variants.Select(v => v.ClusterId).ToList();
        if (service.DefaultClusterId is { } defaultClusterId)
        {
            referencedClusterIds.Add(defaultClusterId);
        }

        var clusters = await Query<YarpCluster>(tracking)
            .Where(x => x.OwnerServiceId == id || referencedClusterIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var clusterIds = clusters.Select(c => c.Id).ToList();
        var destinations = await Query<YarpDestination>(tracking).Where(x => clusterIds.Contains(x.ClusterId)).OrderBy(x => x.Id).ToListAsync(ct);

        return new ServiceAggregate(service, routes, transforms, clusters, destinations, experiment, variants, rules);
    }

    public async Task<bool> IsClusterReferencedOutsideAsync(IReadOnlyCollection<long> clusterIds, long serviceId, CancellationToken ct = default)
    {
        if (clusterIds.Count == 0)
        {
            return false;
        }

        return await _db.Services.AnyAsync(s => s.Id != serviceId && s.DefaultClusterId != null && clusterIds.Contains(s.DefaultClusterId.Value), ct)
            || await _db.Routes.AnyAsync(r => r.ServiceId != serviceId && r.ClusterId != null && clusterIds.Contains(r.ClusterId.Value), ct)
            || await _db.ExperimentVariants.AnyAsync(
                v => clusterIds.Contains(v.ClusterId) && _db.Experiments.Any(e => e.Id == v.ExperimentId && e.ServiceId != serviceId), ct)
            || await _db.ApiVersions.AnyAsync(a => clusterIds.Contains(a.ClusterId), ct);
    }

    public async Task<IReadOnlyList<ExperimentOwner>> GetExperimentOwnersAsync(IReadOnlyCollection<string> experimentKeys, CancellationToken ct = default) =>
        await _db.Experiments.AsNoTracking()
            .Where(e => experimentKeys.Contains(e.ExperimentKey))
            .Join(_db.Services.AsNoTracking(), e => e.ServiceId, s => s.Id, (e, s) => new ExperimentOwner(e.ExperimentKey, s.Id, s.DisplayName))
            .ToListAsync(ct);

    public void Add(object entity) => _db.Add(entity);

    public void Remove(object entity) => _db.Remove(entity);

    private IQueryable<T> Query<T>(bool tracking)
        where T : class =>
        tracking ? _db.Set<T>() : _db.Set<T>().AsNoTracking();
}
