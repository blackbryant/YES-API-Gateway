using System.Security.Cryptography;

namespace ApiGateway.Infrastructure.Ids;

/// <summary>系統識別碼產生器（ClusterId 等，FR-02-05）</summary>
public interface ISystemIdGenerator
{
    /// <summary>26 字元 ULID：前 10 碼毫秒時間戳、後 16 碼隨機數（Crockford Base32）</summary>
    string NewUlid();
}

/// <summary>
/// ULID 產生器（research R2）。同一毫秒內遞增隨機部分（monotonic），保證唯一且可依字典序排序。
/// </summary>
public sealed class UlidGenerator : ISystemIdGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int TimeLength = 10;
    private const int RandomLength = 16;
    private const int RandomBytes = 10;
    private const int BitsPerChar = 5;

    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private readonly byte[] _lastRandom = new byte[RandomBytes];
    private long _lastTimestamp = -1;

    public UlidGenerator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string NewUlid()
    {
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        Span<byte> random = stackalloc byte[RandomBytes];

        lock (_lock)
        {
            if (timestamp <= _lastTimestamp)
            {
                // 同一毫秒（或時鐘回撥）：沿用上一個時間戳並將隨機部分 +1
                timestamp = _lastTimestamp;
                Increment(_lastRandom);
            }
            else
            {
                RandomNumberGenerator.Fill(_lastRandom);
                _lastTimestamp = timestamp;
            }

            _lastRandom.CopyTo(random);
        }

        return string.Create(TimeLength + RandomLength, (timestamp, random.ToArray()), static (chars, state) =>
        {
            var (time, bytes) = state;
            for (var i = TimeLength - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(time & 0x1F)];
                time >>= BitsPerChar;
            }

            // 80 bits → 16 字元
            var value = new System.Numerics.BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            for (var i = TimeLength + RandomLength - 1; i >= TimeLength; i--)
            {
                chars[i] = Alphabet[(int)(value & 0x1F)];
                value >>= BitsPerChar;
            }
        });
    }

    /// <summary>解出前 10 碼的毫秒時間戳</summary>
    public static long DecodeTimestamp(string ulid)
    {
        long value = 0;
        for (var i = 0; i < TimeLength; i++)
        {
            value = (value << BitsPerChar) | (uint)Alphabet.IndexOf(ulid[i], StringComparison.Ordinal);
        }

        return value;
    }

    private static void Increment(byte[] bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (++bytes[i] != 0)
            {
                return;
            }
        }
    }
}
