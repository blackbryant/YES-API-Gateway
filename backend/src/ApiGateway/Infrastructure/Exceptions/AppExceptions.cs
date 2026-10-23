namespace ApiGateway.Infrastructure.Exceptions;

/// <summary>登入失敗；Message 為 ErrorCodes（AUTH_INVALID_CREDENTIALS → 401、AUTH_ACCOUNT_LOCKED → 423）</summary>
public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string errorCode)
        : base(errorCode)
    {
    }
}

/// <summary>多欄位驗證錯誤；由 ErrorHandlingMiddleware 轉成 400，Errors 放在回應 details</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string errorCode, IReadOnlyDictionary<string, string[]> errors)
        : base(errorCode)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
