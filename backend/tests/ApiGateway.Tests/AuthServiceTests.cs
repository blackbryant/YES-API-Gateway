using ApiGateway.Infrastructure.Auth;
using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApiGateway.Tests;

public class AuthServiceTests
{
    private const string Password = "P@ssw0rd!";

    private readonly Mock<IAppUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.Setup(x => x.CreateAccessToken(It.IsAny<AppUser>())).Returns(("token", _time.GetUtcNow().AddHours(1)));
        _sut = new AuthService(_users.Object, _unitOfWork.Object, _hasher, _tokens.Object, _audit.Object, _time, NullLogger<AuthService>.Instance);
    }

    private AppUser CreateUser(Action<AppUser>? configure = null)
    {
        var user = new AppUser { Id = 7, Username = "alice", DisplayName = "Alice", Role = RoleCodes.Admin, IsActive = true };
        user.PasswordHash = _hasher.HashPassword(user, Password);
        configure?.Invoke(user);
        _users.Setup(x => x.GetByUsernameForUpdateAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("alice", null)]
    [InlineData("   ", Password)]
    public async Task Login_MissingCredentials_ThrowsArgument(string? username, string? password)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.LoginAsync(new LoginRequest(username, password)));
        Assert.Equal(ErrorCodes.AuthCredentialsRequired, ex.Message);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSessionAndResetsFailures()
    {
        var user = CreateUser(u => u.FailedLoginCount = 3);

        var result = await _sut.LoginAsync(new LoginRequest("alice", Password));

        Assert.Equal("alice", result.Session.Username);
        Assert.Equal(RoleCodes.Admin, result.Session.Role);
        Assert.Equal("token", result.AccessToken);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(_time.GetUtcNow(), user.LastLoginAt);
        _audit.Verify(x => x.Record(AuditActions.Login, It.IsAny<string>(), "alice", 7, null, null), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsInvalidCredentials()
    {
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest("nobody", Password)));

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, ex.Message);
        _audit.Verify(x => x.Record(AuditActions.LoginFailed, It.IsAny<string>(), "nobody", null, null, null), Times.Once);
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsInvalidCredentials()
    {
        CreateUser(u => u.IsActive = false);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest("alice", Password)));

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, ex.Message);
    }

    [Fact]
    public async Task Login_WrongPassword_IncrementsFailedCount()
    {
        var user = CreateUser(u => u.FailedLoginCount = 1);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest("alice", "wrong")));

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, ex.Message);
        Assert.Equal(2, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_FifthWrongPassword_LocksAccount()
    {
        var user = CreateUser(u => u.FailedLoginCount = AuthRules.MaxFailedLoginCount - 1);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest("alice", "wrong")));

        Assert.Equal(ErrorCodes.AuthAccountLocked, ex.Message);
        Assert.Equal(_time.GetUtcNow().AddMinutes(AuthRules.LockoutMinutes), user.LockoutEndAt);
    }

    [Fact]
    public async Task Login_WhileLocked_ThrowsLockedEvenWithCorrectPassword()
    {
        CreateUser(u => u.LockoutEndAt = _time.GetUtcNow().AddMinutes(1));

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest("alice", Password)));

        Assert.Equal(ErrorCodes.AuthAccountLocked, ex.Message);
        _tokens.Verify(x => x.CreateAccessToken(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Login_AfterLockoutExpired_Succeeds()
    {
        var user = CreateUser(u => u.LockoutEndAt = _time.GetUtcNow().AddMinutes(-1));

        await _sut.LoginAsync(new LoginRequest("alice", Password));

        Assert.Null(user.LockoutEndAt);
    }
}
