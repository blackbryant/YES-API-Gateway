using ApiGateway.Gateway;
using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using ApiGateway.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApiGateway.Tests;

public class PublishServiceTests
{
    private const long AdminId = 1;
    private const long ReaderId = 2;

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAccessControlService> _access = new();
    private readonly Mock<IConfigSnapshotRepository> _snapshots = new();
    private readonly Mock<IConfigVersionRepository> _versions = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPublishComposer> _composer = new();
    private readonly Mock<IGatewayConfigApplier> _applier = new();
    private readonly YarpConfigDraft _draft = new() { Id = 1, IsDirty = true, ChangeCount = 3, LastEditedAt = Now.AddMinutes(-5) };
    private readonly SnapshotFixture _fixture = new();
    private readonly List<YarpConfigVersion> _added = [];
    private readonly PublishService _sut;

    public PublishServiceTests()
    {
        var admin = new AppUser { Id = AdminId, Username = "admin", Role = RoleCodes.Admin, IsActive = true };
        _access.Setup(x => x.EnsureAdminAsync(AdminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _access.Setup(x => x.EnsureAdminAsync(ReaderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));

        var cluster = _fixture.AddCluster();
        _fixture.AddRoute(_fixture.AddService("orders-api", clusterId: cluster.Id), "/api/orders");

        _snapshots.Setup(x => x.TryLockDraftAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_draft);
        _snapshots.Setup(x => x.GetDraftAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_draft);
        _snapshots.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _fixture.Build());
        _unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IDbContextTransaction>());
        _versions.Setup(x => x.Add(It.IsAny<YarpConfigVersion>())).Callback<YarpConfigVersion>(_added.Add);

        _composer.Setup(x => x.ComposeAsync(It.IsAny<ConfigSnapshot>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigSnapshot s, DateTimeOffset _, CancellationToken _) =>
            {
                var doc = YarpConfigBuilder.Build(s);
                return new PublishComposition([], doc, YarpConfigBuilder.ComputeChecksum(doc));
            });

        _sut = new PublishService(_access.Object, _snapshots.Object, _versions.Object, _audit.Object, _unitOfWork.Object,
            _composer.Object, _applier.Object, new FakeTimeProvider(Now), NullLogger<PublishService>.Instance);
    }

    private PublishRequest ValidRequest() => new("上線訂單 API", PublishService.ToDraftToken(_draft));

    [Fact]
    public async Task Publish_ByReader_ThrowsForbidden()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.PublishAsync(ReaderId, ValidRequest()));

        Assert.Equal(ErrorCodes.Forbidden, ex.Message);
        _applier.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Publish_EmptyNote_ThrowsArgument(string note)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.PublishAsync(AdminId, ValidRequest() with { Note = note }));

        Assert.Equal(ErrorCodes.PublishNoteRequired, ex.Message);
    }

    [Fact]
    public async Task Publish_LockUnavailable_ThrowsInProgress()
    {
        _snapshots.Setup(x => x.TryLockDraftAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YarpConfigDraft?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PublishAsync(AdminId, ValidRequest()));

        Assert.Equal(ErrorCodes.PublishInProgress, ex.Message);
    }

    [Fact]
    public async Task Publish_StaleDraftToken_ThrowsDraftChanged()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.PublishAsync(AdminId, ValidRequest() with { DraftToken = "123" }));

        Assert.Equal(ErrorCodes.PublishDraftChanged, ex.Message);
    }

    [Fact]
    public async Task Publish_NoChanges_ThrowsArgument()
    {
        var snapshotJson = _fixture.Build().Serialize();
        _versions.Setup(x => x.GetActiveForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YarpConfigVersion { VersionNo = "2026102101", Status = ConfigVersionStatus.Active, Snapshot = snapshotJson });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.PublishAsync(AdminId, ValidRequest()));

        Assert.Equal(ErrorCodes.PublishNoChanges, ex.Message);
    }

    [Fact]
    public async Task Publish_ValidationIssues_ThrowsWithIssues()
    {
        var issue = new ValidationIssue(PublishIssueCodes.RouteNoTarget, 1, "Orders", "orders-api-1", null, null);
        _composer.Setup(x => x.ComposeAsync(It.IsAny<ConfigSnapshot>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishComposition([issue], null, null));

        var ex = await Assert.ThrowsAsync<PublishValidationException>(() => _sut.PublishAsync(AdminId, ValidRequest()));

        Assert.Equal(ErrorCodes.PublishValidationFailed, ex.Message);
        Assert.Equal([issue], ex.Issues);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _applier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Publish_Success_SwitchesVersionResetsDraftAndApplies()
    {
        var previous = new YarpConfigVersion { VersionNo = "2026102101", Status = ConfigVersionStatus.Active, Snapshot = "{}" };
        _versions.Setup(x => x.GetActiveForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        _versions.Setup(x => x.GetMaxVersionNoOfDayAsync("20261022", It.IsAny<CancellationToken>())).ReturnsAsync("2026102203");

        var result = await _sut.PublishAsync(AdminId, ValidRequest());

        Assert.Equal("2026102204", result.VersionNo);
        Assert.Equal(ConfigVersionStatus.History, previous.Status);
        var created = Assert.Single(_added);
        Assert.Equal(ConfigVersionStatus.Active, created.Status);
        Assert.Equal("2026102101", created.BasedOnVersionNo);
        Assert.Equal("上線訂單 API", created.Note);
        Assert.Equal(AdminId, created.PublishedBy);
        Assert.Equal(64, created.Checksum.Length);
        Assert.True(created.ChangeCount > 0);
        Assert.False(_draft.IsDirty);
        Assert.Equal(0, _draft.ChangeCount);
        Assert.Equal("2026102204", _draft.BaseVersionNo);
        _audit.Verify(x => x.Record(AuditActions.Publish, It.IsAny<string>(), "2026102204", AdminId, null, It.IsAny<object>()), Times.Once);
        _applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), "2026102204", created.Checksum), Times.Once);
    }

    [Fact]
    public async Task Publish_AppliesOnlyAfterSave()
    {
        var saved = false;
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => saved = true).ReturnsAsync(1);
        _applier.Setup(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => Assert.True(saved));

        await _sut.PublishAsync(AdminId, ValidRequest());

        _applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Publish_SaveFails_DoesNotApply()
    {
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("db"));

        await Assert.ThrowsAnyAsync<Exception>(() => _sut.PublishAsync(AdminId, ValidRequest()));

        _applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
