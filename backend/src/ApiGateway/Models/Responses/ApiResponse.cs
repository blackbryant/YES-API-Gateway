using System.Text.Json.Serialization;

namespace ApiGateway.Models.Responses;

/// <summary>成功回應：{ "success": true, "data": ... }</summary>
public sealed class ApiResponse<T>
{
    public ApiResponse(T data)
    {
        Data = data;
    }

    [JsonPropertyOrder(-1)]
    public bool Success => true;

    public T Data { get; }
}

/// <summary>失敗回應：{ "success": false, "code": ..., "message": ..., "details": ... }</summary>
public sealed class ApiErrorResponse
{
    public ApiErrorResponse(string code, string message, object? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    [JsonPropertyOrder(-1)]
    public bool Success => false;

    /// <summary>錯誤碼，取自 ErrorCodes</summary>
    public string Code { get; }

    /// <summary>英文除錯訊息；畫面文字由前端依 Code 查 i18n</summary>
    public string Message { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new(data);

    public static ApiErrorResponse Fail(string code, string message, object? details = null) => new(code, message, details);
}
