using ApiGateway.Controllers;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ApiGateway.Tests;

public class ServicesControllerTests
{
    private const long OperatorId = 1;

    private readonly Mock<IServiceService> _service = new();
    private readonly ServicesController _sut;

    public ServicesControllerTests()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[AuthRules.AuthUserItemKey] =
            new AuthTokenPayload(OperatorId, "admin", RoleCodes.Admin, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        _sut = new ServicesController(_service.Object) { ControllerContext = new ControllerContext { HttpContext = httpContext } };
    }

    private static ServiceSaveRequest Request() => new("orders-api", "訂單", null, true, null, [], RoutingMode.Single, null, null);

    private static ServiceDetailDto Dto() =>
        new(1, "orders-api", "訂單", [], true, null, [], RoutingMode.Single, null, [], null, DateTimeOffset.UtcNow);

    private static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode,
        StatusCodeResult s => s.StatusCode,
        _ => null,
    };

    [Fact]
    public async Task Create_Success_Returns201AndPassesOperator()
    {
        _service.Setup(x => x.CreateAsync(OperatorId, It.IsAny<ServiceSaveRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto());

        Assert.Equal(StatusCodes.Status201Created, StatusOf(await _sut.Create(Request())));
    }

    [Theory]
    [InlineData(ErrorCodes.ServicesKeyInvalid)]
    [InlineData(ErrorCodes.RoutesPathInvalid)]
    [InlineData(ErrorCodes.ExperimentsInvalid)]
    public async Task Create_ArgumentException_Returns400(string code)
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ServiceSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(code));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Create_Forbidden_Returns403()
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ServiceSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Create_KeyExists_Returns409()
    {
        _service.Setup(x => x.CreateAsync(It.IsAny<long?>(), It.IsAny<ServiceSaveRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(ErrorCodes.ServicesKeyExists));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(await _sut.Create(Request())));
    }

    [Fact]
    public async Task Get_NotFound_Returns404()
    {
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(await _sut.Get(99)));
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(await _sut.Update(99, Request())));
    }

    [Fact]
    public async Task SetActive_Success_Returns204()
    {
        _service.Setup(x => x.SetActiveAsync(OperatorId, 1, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(await _sut.SetActive(1, new ServiceActiveRequest(false))));
    }

    [Fact]
    public async Task Delete_InUse_Returns409()
    {
        _service.Setup(x => x.DeleteAsync(It.IsAny<long?>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(ErrorCodes.ClustersInUse));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(await _sut.Delete(1)));
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        _service.Setup(x => x.DeleteAsync(OperatorId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(await _sut.Delete(1)));
    }
}
