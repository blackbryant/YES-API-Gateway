namespace ApiGateway.Models.Constants;

/// <summary>管理介面登入與授權規則（憲章八：帳號保護）</summary>
public static class AuthRules
{
    /// <summary>存放 access token 的 HttpOnly Cookie 名稱</summary>
    public const string AccessTokenCookieName = "yes_access_token";

    /// <summary>AuthMiddleware 驗證成功後寫入 HttpContext.Items 的鍵</summary>
    public const string AuthUserItemKey = "AuthUser";

    /// <summary>連續登入失敗幾次鎖定帳號</summary>
    public const int MaxFailedLoginCount = 5;

    /// <summary>帳號鎖定時間（分鐘）</summary>
    public const int LockoutMinutes = 15;

    /// <summary>帳號長度上限（AppUsers.Username VARCHAR(50)）</summary>
    public const int UsernameMaxLength = 50;

    /// <summary>密碼長度上限，避免超長字串雜湊造成負擔</summary>
    public const int PasswordMaxLength = 256;

    /// <summary>Admin API 路由前綴；此前綴下預設全部需要登入</summary>
    public const string AdminApiPrefix = "/api/v1";

    /// <summary>Admin API 中不需登入的路徑（完全比對，不分大小寫）</summary>
    public static readonly IReadOnlyList<string> AnonymousPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/logout",
    ];
}
