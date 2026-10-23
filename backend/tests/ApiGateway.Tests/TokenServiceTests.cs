using ApiGateway.Infrastructure.Auth;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiGateway.Tests;

public class TokenServiceTests
{
    private static TokenService CreateSut(TimeProvider? timeProvider = null)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new JwtOptions());
        var keys = new SigningKeyProvider(options, environment.Object, NullLogger<SigningKeyProvider>.Instance);
        return new TokenService(keys, options, timeProvider ?? TimeProvider.System);
    }

    [Fact]
    public async Task CreateThenValidate_RoundTripsClaims()
    {
        var sut = CreateSut();
        var (token, _) = sut.CreateAccessToken(new AppUser { Id = 42, Username = "alice", Role = RoleCodes.User });

        var payload = await sut.ValidateAsync(token);

        Assert.NotNull(payload);
        Assert.Equal(42, payload.UserId);
        Assert.Equal("alice", payload.Username);
        Assert.Equal(RoleCodes.User, payload.Role);
    }

    [Fact]
    public async Task Validate_TokenFromAnotherKey_ReturnsNull()
    {
        var (token, _) = CreateSut().CreateAccessToken(new AppUser { Id = 1, Username = "a", Role = RoleCodes.Admin });

        Assert.Null(await CreateSut().ValidateAsync(token));
    }

    [Fact]
    public async Task Validate_Garbage_ReturnsNull()
    {
        Assert.Null(await CreateSut().ValidateAsync("not-a-jwt"));
    }
}
