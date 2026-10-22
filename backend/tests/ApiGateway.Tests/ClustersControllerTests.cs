using ApiGateway.Controllers;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ApiGateway.Tests;

public class ClustersControllerTests
{
    private const long OperatorId = 1;

    private readonly Mock<IClusterService> _service = new();
    private readonly ClustersController _sut;

    public ClustersControllerTests()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[AuthRules.AuthUserItemKey] =
            new AuthTokenPayload(OperatorId, "admin", RoleCodes.Admin, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        _sut = new ClustersController(_service.Object) { ControllerContext = new ControllerContext { HttpContext = httpContext } };
    }

    private static ClusterSaveRequest Request() => new("G", null, null, null, 100, null, false, true, null);

    private static ClusterDto Dto() => new(1, "G", "RoundRobin", "2.0", "RequestVersionOrLower", 100, null, false, true, null, DateTimeOffset.UtcNow);

    private static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode,
        StatusCodeResult s => s.StatusCode,
        _ => null,
    };

    [Fact]
    public async Task Create_Success_Returns201AndPassesOperator()
    {
        _service.Setup(x => x.CreateAsync(OperatorId, It.IsAny<ClusterSaveRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto());

        Assert.Equal(StatusCodes.Status201Created, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Create_ArgumentException_Returns400()
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ClusterSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(ErrorCodes.ClustersKeyInvalid));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Create_Forbidden_Returns403()
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ClusterSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Create_KeyExists_Returns409()
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ClusterSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(ErrorCodes.ClustersKeyExists));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Get_NotFound_Returns404()
    {
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(await _sut.Get(99)));
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        _service.Setup(x => x.DeleteAsync(OperatorId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(await _sut.Delete(1)));
    }

    [Fact]
    public async Task Delete_InUse_Returns409()
    {
        _service.Setup(x => x.DeleteAsync(It.IsAny<long?>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(ErrorCodes.ClustersInUse));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(await _sut.Delete(1)));
    }

    [Fact]
    public async Task UnexpectedInvalidOperation_IsNotSwallowed()
    {
        _service.Setup(x => x.DeleteAsync(It.IsAny<long?>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("something else"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Delete(1));
    }
}
