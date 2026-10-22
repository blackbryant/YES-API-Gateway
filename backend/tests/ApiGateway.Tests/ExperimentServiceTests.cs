using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Interfaces;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApiGateway.Tests;

public class ExperimentServiceTests
{
    private const long AdminId = 1;
    private const long ReaderId = 2;
    private const string Key = "orders-api-ab";

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private static readonly ExperimentDefinition Published = new()
    {
        Key = Key,
        Status = ExperimentConfigStatus.Running,
        Variants =
        [
            new VariantDefinition { Key = "A", Weight = 90, IsControl = true },
            new VariantDefinition { Key = "B", Weight = 10 },
        ],
    };

    private readonly Mock<IAccessControlService> _access = new();
    private readonly Mock<IExperimentStateReader> _states = new();
    private readonly Mock<IExperimentGatewayControl> _gateway = new();
    private readonly Mock<IExperimentRuntimeRepository> _runtime = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExperimentService _sut;

    public ExperimentServiceTests()
    {
        var admin = new AppUser { Id = AdminId, Username = "admin", Role = RoleCodes.Admin, IsActive = true };
        _access.Setup(x => x.EnsureAdminAsync(AdminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _access.Setup(x => x.EnsureAdminAsync(ReaderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));
        _access.Setup(x => x.EnsureSignedInAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _services.Setup(x => x.GetExperimentOwnersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExperimentOwner(Key, 10, "訂單 API")]);
        _states.SetupGet(x => x.PublishedExperiments).Returns([Published]);

        _sut = new ExperimentService(_access.Object, _states.Object, _gateway.Object, _runtime.Object, _services.Object,
            _audit.Object, _unitOfWork.Object, new FakeTimeProvider(Now));
    }

    private void State(ExperimentStatus status, bool autoRolledBack = false, ExperimentDefinition? published = null) =>
        _states.Setup(x => x.Get(Key)).Returns(new ExperimentState(status, autoRolledBack, autoRolledBack ? Now : null, published ?? Published));

    [Fact]
    public async Task RunAction_ByReader_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RunActionAsync(ReaderId, Key, ExperimentRuntimeAction.Pause));
    }

    [Fact]
    public async Task RunAction_NotPublished_ReturnsNull()
    {
        _states.Setup(x => x.Get(Key)).Returns(new ExperimentState(ExperimentStatus.Draft, false, null, null));

        Assert.Null(await _sut.RunActionAsync(AdminId, Key, ExperimentRuntimeAction.Pause));
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pause_CreatesOverrideAuditsAndAppliesLocally()
    {
        State(ExperimentStatus.Running);
        YarpExperimentRuntime? added = null;
        _runtime.Setup(x => x.Add(It.IsAny<YarpExperimentRuntime>())).Callback<YarpExperimentRuntime>(r => added = r);

        var result = await _sut.RunActionAsync(AdminId, Key, ExperimentRuntimeAction.Pause);

        Assert.NotNull(result);
        Assert.Equal(ExperimentOverrideStatus.Paused, added!.OverrideStatus);
        Assert.Equal(AdminId, added.UpdatedBy);
        _audit.Verify(x => x.Record(AuditActions.ExperimentPause, It.IsAny<string>(), Key, AdminId, It.IsAny<object>(), It.IsAny<object>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(x => x.ApplyOverride(Key, It.Is<ExperimentRuntimeOverride>(o => o.OverrideStatus == ExperimentOverrideStatus.Paused)), Times.Once);
    }

    [Fact]
    public async Task Resume_ClearsOverrideAndAutoRollbackAndResetsMetrics()
    {
        State(ExperimentStatus.Paused, autoRolledBack: true);
        var row = new YarpExperimentRuntime
        {
            ExperimentKey = Key,
            OverrideStatus = ExperimentOverrideStatus.Paused,
            IsAutoRolledBack = true,
            TriggeredMetric = "{}",
            TriggeredAt = Now,
        };
        _runtime.Setup(x => x.GetForUpdateAsync(Key, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await _sut.RunActionAsync(AdminId, Key, ExperimentRuntimeAction.Resume);

        Assert.Null(row.OverrideStatus);
        Assert.False(row.IsAutoRolledBack);
        Assert.Null(row.TriggeredMetric);
        _gateway.Verify(x => x.ApplyOverride(Key, null), Times.Once);
        _gateway.Verify(x => x.ResetMetrics(Key), Times.Once);
    }

    [Fact]
    public async Task End_SetsEnded()
    {
        State(ExperimentStatus.Running);
        var row = new YarpExperimentRuntime { ExperimentKey = Key };
        _runtime.Setup(x => x.GetForUpdateAsync(Key, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await _sut.RunActionAsync(AdminId, Key, ExperimentRuntimeAction.End);

        Assert.Equal(ExperimentOverrideStatus.Ended, row.OverrideStatus);
        _audit.Verify(x => x.Record(AuditActions.ExperimentEnd, It.IsAny<string>(), Key, AdminId, It.IsAny<object>(), It.IsAny<object>()), Times.Once);
    }

    [Theory]
    [InlineData(ExperimentStatus.Ended, ExperimentRuntimeAction.Resume)]
    [InlineData(ExperimentStatus.Ended, ExperimentRuntimeAction.End)]
    [InlineData(ExperimentStatus.Running, ExperimentRuntimeAction.Resume)]
    [InlineData(ExperimentStatus.Paused, ExperimentRuntimeAction.Pause)]
    public async Task InvalidTransition_ThrowsActionInvalid(ExperimentStatus status, ExperimentRuntimeAction action)
    {
        State(status);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.RunActionAsync(AdminId, Key, action));

        Assert.Equal(ErrorCodes.ExperimentsActionInvalid, ex.Message);
    }

    [Fact]
    public async Task RunAction_DoesNotCreateVersionOrMarkDraft()
    {
        State(ExperimentStatus.Running);

        await _sut.RunActionAsync(AdminId, Key, ExperimentRuntimeAction.Pause);

        // 只寫執行期表（IExperimentRuntimeRepository）與稽核，沒有草稿或版本相關依賴
        _runtime.Verify(x => x.Add(It.IsAny<YarpExperimentRuntime>()), Times.Once);
    }

    [Theory]
    [InlineData(ExperimentStatus.Running, true)]
    [InlineData(ExperimentStatus.Scheduled, true)]
    [InlineData(ExperimentStatus.Paused, true)]
    [InlineData(ExperimentStatus.Ended, false)]
    [InlineData(ExperimentStatus.Draft, false)]
    public async Task Summary_ListsActiveStatesWithServiceName(ExperimentStatus status, bool listed)
    {
        State(status);

        var result = await _sut.GetSummaryAsync(AdminId);

        if (listed)
        {
            var item = Assert.Single(result);
            Assert.Equal("訂單 API", item.ServiceName);
            Assert.Equal(10, item.ServiceId);
            Assert.Equal(status, item.EffectiveStatus);
            Assert.Equal([("A", 90, true), ("B", 10, false)], item.Variants.Select(v => (v.VariantKey, v.Percent, v.IsControl)));
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task Attention_ListsAutoRolledBackExperiments()
    {
        State(ExperimentStatus.Paused, autoRolledBack: true);

        var item = Assert.Single(await _sut.GetAttentionItemsAsync(AdminId));

        Assert.Equal("experimentAutoRolledBack", item.Type);
        Assert.Equal("critical", item.Severity);
        Assert.Equal("/services/10", item.Target);
        Assert.Equal("訂單 API", item.Params["service"]);
    }
}
