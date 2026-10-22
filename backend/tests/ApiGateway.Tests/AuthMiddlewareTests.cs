using ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;

namespace Backend.Unit.Tests;

public class AuthMiddlewareTests
{
    private static HttpRequest Request(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context.Request;
    }

    [Theory]
    [InlineData("GET", "/api/v1/clusters")]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("POST", "/API/V1/clusters")]
    [InlineData("GET", "/api/v1/draft/status")]
    public void AdminApi_RequiresAuthentication(string method, string path)
    {
        Assert.True(AuthMiddleware.RequiresAuthentication(Request(method, path)));
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/login/")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("OPTIONS", "/api/v1/clusters")]
    [InlineData("GET", "/healthz")]
    [InlineData("GET", "/dashboard")]
    [InlineData("GET", "/api/v10/anything")]
    public void AnonymousOrNonAdminPaths_DoNotRequireAuthentication(string method, string path)
    {
        Assert.False(AuthMiddleware.RequiresAuthentication(Request(method, path)));
    }
}
