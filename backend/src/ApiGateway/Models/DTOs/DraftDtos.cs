namespace ApiGateway.Models.DTOs;

/// <summary>頂部列草稿狀態（對應前端 DraftStatus）</summary>
public sealed record DraftStatusDto(string? ActiveVersion, int PendingChanges);
