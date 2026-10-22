using System.Text.Json;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>組成稽核日誌（含來源 IP）；呼叫端不得傳入含密碼雜湊或機密的物件</summary>
public sealed class AuditTrailService : IAuditTrailService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditLogRepository _auditLogs;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    public AuditTrailService(IAuditLogRepository auditLogs, IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
    {
        _auditLogs = auditLogs;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    public void Record(string action, string entityType, string? entityKey, long? userId, object? before = null, object? after = null)
    {
        _auditLogs.Add(new AuditLog
        {
            OccurredAt = _timeProvider.GetUtcNow(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityKey = entityKey,
            Before = before is null ? null : JsonSerializer.Serialize(before, SerializerOptions),
            After = after is null ? null : JsonSerializer.Serialize(after, SerializerOptions),
            ClientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress,
        });
    }
}
