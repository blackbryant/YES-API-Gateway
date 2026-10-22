using ApiGateway.Infrastructure.Ids;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApiGateway.Tests;

public class ClusterServiceTests
{
    private const long AdminId = 1;
    private const long ReaderId = 2;

    private readonly Mock<IAccessControlService> _access = new();
    private readonly Mock<IClusterRepository> _clusters = new();
    private readonly Mock<IConfigDraftRepository> _drafts = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private const string GeneratedKey = "01JC0000000000000000000001";
    private readonly Mock<ISystemIdGenerator> _ids = new();
    private readonly ClusterService _sut;

    public ClusterServiceTests()
    {
        var admin = new AppUser { Id = AdminId, Username = "admin", Role = RoleCodes.Admin, IsActive = true };
        _access.Setup(x => x.EnsureAdminAsync(AdminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _access.Setup(x => x.EnsureAdminAsync(ReaderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));
        _ids.Setup(x => x.NewUlid()).Returns(GeneratedKey);
        _access.Setup(x => x.EnsureSignedInAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        _sut = new ClusterService(_access.Object, _clusters.Object, _drafts.Object, _audit.Object, _unitOfWork.Object,
            _ids.Object, new FakeTimeProvider(), NullLogger<ClusterService>.Instance);
    }

    private static ClusterSaveRequest ValidRequest(string displayName = "支付群組") =>
        new(displayName, "RoundRobin", "2.0", "RequestVersionOrLower", 100, null, false, true, "支付");

    [Fact]
    public async Task Create_ByReader_ThrowsForbidden()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CreateAsync(ReaderId, ValidRequest()));

        Assert.Equal(ErrorCodes.Forbidden, ex.Message);
        _clusters.Verify(x => x.Add(It.IsAny<YarpCluster>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidDisplayName_ThrowsArgument(string displayName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, ValidRequest(displayName)));
        Assert.Equal(ErrorCodes.ClustersDisplayNameInvalid, ex.Message);
    }

    [Fact]
    public async Task Create_UnknownLoadBalancingPolicy_ThrowsArgument()
    {
        var request = ValidRequest() with { LoadBalancingPolicy = "Sticky" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, request));

        Assert.Equal(ErrorCodes.ClustersLoadBalancingPolicyInvalid, ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ClusterRules.MaxActivityTimeoutSec + 1)]
    public async Task Create_ActivityTimeoutOutOfRange_ThrowsArgument(int seconds)
    {
        var request = ValidRequest() with { ActivityTimeoutSec = seconds };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, request));

        Assert.Equal(ErrorCodes.ClustersActivityTimeoutInvalid, ex.Message);
    }

    [Fact]
    public async Task Create_Valid_AddsMarksDraftAuditsAndSavesOnce()
    {
        YarpCluster? added = null;
        _clusters.Setup(x => x.Add(It.IsAny<YarpCluster>())).Callback<YarpCluster>(c => added = c);

        var result = await _sut.CreateAsync(AdminId, ValidRequest(" 支付群組 "));

        Assert.NotNull(added);
        Assert.Equal(GeneratedKey, result.ClusterKey);
        Assert.Equal("支付群組", result.DisplayName);
        Assert.Equal(AdminId, added.UpdatedBy);
        _drafts.Verify(x => x.MarkDirtyAsync(AdminId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(x => x.Record(AuditActions.Create, "YarpClusters", GeneratedKey, AdminId, null, It.IsAny<object>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_NullOptionalFields_UsesDefaults()
    {
        var request = new ClusterSaveRequest("Group", null, null, null, ClusterRules.DefaultActivityTimeoutSec, null, false, true, null);

        var result = await _sut.CreateAsync(AdminId, request);

        Assert.Equal(ClusterRules.DefaultLoadBalancingPolicy, result.LoadBalancingPolicy);
        Assert.Equal(ClusterRules.DefaultHttpVersion, result.HttpVersion);
        Assert.Equal(ClusterRules.DefaultHttpVersionPolicy, result.HttpVersionPolicy);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(AdminId, 99, ValidRequest());

        Assert.Null(result);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_InUse_ThrowsConflict()
    {
        _clusters.Setup(x => x.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new YarpCluster { Id = 5, ClusterKey = "G" });
        _clusters.Setup(x => x.IsInUseAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(AdminId, 5));

        Assert.Equal(ErrorCodes.ClustersInUse, ex.Message);
        _clusters.Verify(x => x.Remove(It.IsAny<YarpCluster>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Unused_RemovesAndSaves()
    {
        var cluster = new YarpCluster { Id = 5, ClusterKey = "G" };
        _clusters.Setup(x => x.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cluster);

        Assert.True(await _sut.DeleteAsync(AdminId, 5));

        _clusters.Verify(x => x.Remove(cluster), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, PagingRules.MaxPageSize + 1)]
    public async Task Search_InvalidPaging_ThrowsArgument(int pageIndex, int pageSize)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(AdminId, new ClusterQuery(null, pageIndex, pageSize)));
        Assert.Equal(ErrorCodes.PagingInvalid, ex.Message);
    }

    [Fact]
    public async Task Search_ComputesSkipAndTotalPages()
    {
        _clusters.Setup(x => x.SearchAsync("pay", 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new YarpCluster { Id = 1, ClusterKey = "Payment" }], 41));

        var result = await _sut.SearchAsync(AdminId, new ClusterQuery("pay", 2, 20));

        Assert.Single(result.Data);
        Assert.Equal(41, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }
}
