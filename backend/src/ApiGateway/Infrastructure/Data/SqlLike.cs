namespace ApiGateway.Infrastructure.Data;

/// <summary>LIKE / ILIKE 關鍵字跳脫，避免使用者輸入的 % _ 被當成萬用字元</summary>
public static class SqlLike
{
    public const string EscapeCharacter = "\\";

    public static string Escape(string value) =>
        value
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
}
