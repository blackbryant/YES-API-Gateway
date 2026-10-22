using ApiGateway.Infrastructure.Ids;
using Microsoft.Extensions.Time.Testing;

namespace ApiGateway.Tests;

public class UlidGeneratorTests
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 22, 8, 30, 0, 123, TimeSpan.Zero));

    [Fact]
    public void NewUlid_Has26CrockfordCharacters()
    {
        var id = new UlidGenerator(_time).NewUlid();

        Assert.Equal(26, id.Length);
        Assert.All(id, c => Assert.Contains(c, CrockfordAlphabet));
    }

    [Fact]
    public void NewUlid_First10CharactersEncodeTimestamp()
    {
        var id = new UlidGenerator(_time).NewUlid();

        Assert.Equal(_time.GetUtcNow().ToUnixTimeMilliseconds(), UlidGenerator.DecodeTimestamp(id));
    }

    [Fact]
    public void NewUlid_SameMillisecond_IsUniqueAndMonotonic()
    {
        var generator = new UlidGenerator(_time);

        var ids = Enumerable.Range(0, 10_000).Select(_ => generator.NewUlid()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        for (var i = 1; i < ids.Count; i++)
        {
            Assert.True(string.CompareOrdinal(ids[i - 1], ids[i]) < 0, $"{ids[i - 1]} !< {ids[i]}");
        }
    }

    [Fact]
    public void NewUlid_LaterTime_SortsAfter()
    {
        var generator = new UlidGenerator(_time);
        var first = generator.NewUlid();

        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(string.CompareOrdinal(first, generator.NewUlid()) < 0);
    }
}
