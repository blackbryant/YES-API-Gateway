using System.Globalization;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Infrastructure.Auth;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user);

    /// <summary>驗證 token；無效時回傳 null</summary>
    Task<AuthTokenPayload?> ValidateAsync(string token);
}

/// <summary>簽發與驗證管理介面 access token（HS256 JWT）</summary>
public sealed class TokenService : ITokenService
{
    private const string RoleClaim = "role";
    private const string UsernameClaim = "preferred_username";
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly JsonWebTokenHandler _handler = new();
    private readonly ISigningKeyProvider _keys;
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public TokenService(ISigningKeyProvider keys, IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _keys = keys;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(_keys.Key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(CultureInfo.InvariantCulture),
                [UsernameClaim] = user.Username,
                [RoleClaim] = user.Role,
            },
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }

    public async Task<AuthTokenPayload?> ValidateAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _keys.Key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = ClockSkew,
        });

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            return null;
        }

        if (!long.TryParse(jwt.Subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || !jwt.TryGetPayloadValue<string>(UsernameClaim, out var username)
            || !jwt.TryGetPayloadValue<string>(RoleClaim, out var role))
        {
            return null;
        }

        return new AuthTokenPayload(
            userId,
            username,
            role,
            new DateTimeOffset(jwt.IssuedAt, TimeSpan.Zero),
            new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
    }
}
