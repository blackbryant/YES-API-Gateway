using ApiGateway.Controllers;
using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ApiGateway.Tests;

public class PublishControllerTests
{
    private const long OperatorId = 1;

    private readonly Mock<IPublishService> _service = new();
    private readonly PublishController _sut;

    public PublishControllerTests()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[AuthRules.AuthUserItemKey] =
            new AuthTokenPayload(OperatorId, "admin", RoleCodes.Admin, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));
        _sut = new PublishController(_service.Object) { ControllerContext = new ControllerContext { HttpContext = httpContext } };
    }

    private static readonly PublishRequest Request = new("note", "1");

    private static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode,
        StatusCodeResult s => s.StatusCode,
        _ => null,
    };

    private void Throws(Exception ex) =>
        _service.Setup(x => x.PublishAsync(It.IsAny<long?>(), It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(ex);

    [Fact]
    public async Task Publish_Success_Returns201()
    {
        _service.Setup(x => x.PublishAsync(OperatorId, Request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResultDto("2026102201", new string('a', 64), DateTimeOffset.UtcNow, 3));

        Assert.Equal(StatusCodes.Status201Created, StatusOf(await _sut.Publish(Request)));
    }

    [Fact]
    public async Task Publish_ValidationFailed_Returns400WithIssues()
    {
        var issue = new ValidationIssue(PublishIssueCodes.RouteConflict, 1, "Orders", "orders-api-1", null, null);
        Throws(new PublishValidationException([issue]));

        var result = Assert.IsType<BadRequestObjectResult>(await _sut.Publish(Request));

        var body = Assert.IsType<ApiErrorResponse>(result.Value);
        Assert.Equal(ErrorCodes.PublishValidationFailed, body.Code);
        Assert.Contains("ROUTE_CONFLICT", System.Text.Json.JsonSerializer.Serialize(body.Details));
    }

    [Theory]
    [InlineData(ErrorCodes.PublishInProgress)]
    [InlineData(ErrorCodes.PublishDraftChanged)]
    [InlineData(ErrorCodes.PublishDailyLimit)]
    public async Task Publish_Conflicts_Return409(string code)
    {
        Throws(new InvalidOperationException(code));

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(await _sut.Publish(Request)));
    }

    [Fact]
    public async Task Publish_StorageUnavailable_Returns503()
    {
        Throws(new InvalidOperationException(ErrorCodes.PublishStorageUnavailable));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusOf(await _sut.Publish(Request)));
    }

    [Fact]
    public async Task Publish_NoteRequired_Returns400()
    {
        Throws(new ArgumentException(ErrorCodes.PublishNoteRequired));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(await _sut.Publish(Request)));
    }

    [Fact]
    public async Task Preview_Forbidden_Returns403()
    {
        _service.Setup(x => x.GetPreviewAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(await _sut.Preview()));
    }
}
