using System.Text.Json.Serialization;

namespace ApiGateway.Models.Responses;

/// <summary>分頁回應：{ "success": true, "data": [...], "pageIndex", "pageSize", "totalCount", "totalPages" }</summary>
public sealed class PagedResponse<T>
{
    public PagedResponse(IReadOnlyList<T> data, int pageIndex, int pageSize, int totalCount)
    {
        Data = data;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    [JsonPropertyOrder(-1)]
    public bool Success => true;

    public IReadOnlyList<T> Data { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
