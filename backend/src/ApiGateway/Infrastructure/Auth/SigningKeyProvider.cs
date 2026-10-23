using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Infrastructure.Auth;

public interface ISigningKeyProvider
{
    SymmetricSecurityKey Key { get; }
}

/// <summary>HS256 簽章金鑰（Singleton）</summary>
public sealed class SigningKeyProvider : ISigningKeyProvider
{
    /// <summary>HS256 金鑰長度下限（256 bits）</summary>
    public const int MinKeyBytes = 32;

    public SigningKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<SigningKeyProvider> logger)
    {
        var secret = options.Value.SigningKey;
        byte[] keyBytes;

        if (!string.IsNullOrWhiteSpace(secret))
        {
            keyBytes = Encoding.UTF8.GetBytes(secret);
            if (keyBytes.Length < MinKeyBytes)
            {
                throw new InvalidOperationException($"Jwt:SigningKey must be at least {MinKeyBytes} bytes.");
            }
        }
        else if (environment.IsDevelopment())
        {
            keyBytes = RandomNumberGenerator.GetBytes(MinKeyBytes);
            logger.LogWarning("Security: Jwt:SigningKey 未設定，使用暫時金鑰；重新啟動後既有 token 將失效");
        }
        else
        {
            throw new InvalidOperationException("Jwt:SigningKey is required outside Development.");
        }

        Key = new SymmetricSecurityKey(keyBytes);
    }

    public SymmetricSecurityKey Key { get; }
}
