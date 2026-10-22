namespace ApiGateway.Infrastructure.Auth;

/// <summary>appsettings「Jwt」區段；SigningKey 為機密，以環境變數 Jwt__SigningKey 注入</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "yes-api-gateway";

    public string Audience { get; set; } = "yes-admin";

    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>HS256 共用密鑰（UTF-8 至少 32 bytes）。Development 未設定時使用暫時金鑰</summary>
    public string? SigningKey { get; set; }
}
