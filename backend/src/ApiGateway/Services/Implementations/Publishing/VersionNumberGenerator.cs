using System.Globalization;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>版本號：YYYYMMDD（依 PublishRules.TimeZoneId）+ 當日兩碼流水號</summary>
public static class VersionNumberGenerator
{
    private const int SequenceDigits = 2;

    public static string DatePrefix(DateTimeOffset now, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId))
            .ToString(PublishRules.VersionDateFormat, CultureInfo.InvariantCulture);

    /// <param name="maxOfDay">當日目前最大版本號；非當日的值會被忽略</param>
    public static string Next(DateTimeOffset now, string timeZoneId, string? maxOfDay)
    {
        var prefix = DatePrefix(now, timeZoneId);
        var sequence = 1;
        if (maxOfDay is not null && maxOfDay.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(maxOfDay.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var current))
        {
            if (current >= PublishRules.MaxVersionsPerDay)
            {
                throw new InvalidOperationException(ErrorCodes.PublishDailyLimit);
            }

            sequence = current + 1;
        }

        return prefix + sequence.ToString(new string('0', SequenceDigits), CultureInfo.InvariantCulture);
    }
}
