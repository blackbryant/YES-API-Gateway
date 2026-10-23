using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;

namespace ApiGateway.Infrastructure.Http;

public static class HttpContextAuthExtensions
{
    /// <summary>AuthMiddleware 寫入的登入者；未登入（匿名端點）時為 null</summary>
    public static AuthTokenPayload? GetAuthUser(this HttpContext context) =>
        context.Items[AuthRules.AuthUserItemKey] as AuthTokenPayload;

    /// <summary>操作者 Id，傳給 Service 做權限判斷</summary>
    public static long? GetOperatorId(this HttpContext context) => context.GetAuthUser()?.UserId;
}
