using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Npgsql;

namespace ApiGateway.Infrastructure.Configuration;

/// <summary>
/// 讀取 backend/.env 的帳號密碼並對應為設定鍵。同名的系統環境變數優先於檔案（docker --env-file）；
/// 原生設定鍵環境變數（如 Jwt__SigningKey）又優先於 .env。
/// </summary>
public static class DotEnvConfiguration
{
    private const string FileName = ".env";
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5432;

    private static readonly string[] KnownKeys =
    [
        "POSTGRES_HOST", "POSTGRES_PORT", "POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD",
        "AUTH_TOKEN_SECRET", "ADMIN_USERNAME", "ADMIN_PASSWORD",
    ];

    public static void AddDotEnv(this ConfigurationManager configuration, string contentRootPath)
    {
        var values = FindFile(contentRootPath) is { } path ? Parse(File.ReadAllLines(path)) : new(StringComparer.Ordinal);
        foreach (var key in KnownKeys)
        {
            if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } value)
            {
                values[key] = value;
            }
        }

        var settings = Map(values);
        if (settings.Count == 0)
        {
            return;
        }

        // 插在 appsettings*.json（含 user secrets）之後、環境變數與命令列之前
        var sources = configuration.Sources;
        var index = sources.Count;
        while (index > 0 && sources[index - 1] is not JsonConfigurationSource)
        {
            index--;
        }

        sources.Insert(index, new MemoryConfigurationSource { InitialData = settings });
    }

    /// <summary>自 content root 往上層尋找 .env（backend/src/ApiGateway → backend）</summary>
    private static string? FindFile(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var separator = line.IndexOf('=');
            if (line.Length == 0 || line.StartsWith('#') || separator <= 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            values[line[..separator].Trim()] = value;
        }

        return values;
    }

    private static Dictionary<string, string?> Map(Dictionary<string, string> env)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (env.TryGetValue("POSTGRES_USER", out var user))
        {
            settings["ConnectionStrings:GatewayDb"] = new NpgsqlConnectionStringBuilder
            {
                Host = env.GetValueOrDefault("POSTGRES_HOST", DefaultHost),
                Port = int.TryParse(env.GetValueOrDefault("POSTGRES_PORT"), out var port) ? port : DefaultPort,
                Database = env.GetValueOrDefault("POSTGRES_DB"),
                Username = user,
                Password = env.GetValueOrDefault("POSTGRES_PASSWORD"),
            }.ConnectionString;
        }

        AddIfPresent(env, settings, "AUTH_TOKEN_SECRET", "Jwt:SigningKey");
        AddIfPresent(env, settings, "ADMIN_USERNAME", "Database:SeedAdminUsername");
        AddIfPresent(env, settings, "ADMIN_PASSWORD", "Database:SeedAdminPassword");
        return settings;
    }

    private static void AddIfPresent(Dictionary<string, string> env, Dictionary<string, string?> settings, string envKey, string configKey)
    {
        if (env.TryGetValue(envKey, out var value) && value.Length > 0)
        {
            settings[configKey] = value;
        }
    }
}
