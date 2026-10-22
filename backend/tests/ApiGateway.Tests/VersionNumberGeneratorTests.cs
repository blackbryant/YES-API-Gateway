using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Services.Implementations.Publishing;

namespace ApiGateway.Tests;

public class VersionNumberGeneratorTests
{
    // 2026-10-22 16:30 UTC = 2026-10-23 00:30 台北
    private static readonly DateTimeOffset LateUtc = new(2026, 10, 22, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MorningUtc = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DatePrefix_UsesTaipeiDate()
    {
        Assert.Equal("20261023", VersionNumberGenerator.DatePrefix(LateUtc, PublishRules.TimeZoneId));
        Assert.Equal("20261022", VersionNumberGenerator.DatePrefix(MorningUtc, PublishRules.TimeZoneId));
    }

    [Fact]
    public void Next_FirstOfDay_Is01()
    {
        Assert.Equal("2026102201", VersionNumberGenerator.Next(MorningUtc, PublishRules.TimeZoneId, null));
    }

    [Fact]
    public void Next_IncrementsSequence()
    {
        Assert.Equal("2026102208", VersionNumberGenerator.Next(MorningUtc, PublishRules.TimeZoneId, "2026102207"));
    }

    [Fact]
    public void Next_PreviousDayMax_StartsFrom01()
    {
        Assert.Equal("2026102301", VersionNumberGenerator.Next(LateUtc, PublishRules.TimeZoneId, "2026102299"));
    }

    [Fact]
    public void Next_DailyLimit_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VersionNumberGenerator.Next(MorningUtc, PublishRules.TimeZoneId, "2026102299"));

        Assert.Equal(ErrorCodes.PublishDailyLimit, ex.Message);
    }
}
