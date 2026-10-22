namespace ApiGateway.Tests.Infrastructure;

/// <summary>
/// 啟動 WebApplicationFactory 的測試共用此集合且不平行執行：
/// Program.cs 以靜態 Serilog bootstrap logger 啟動，多個主機同時啟動會發生 "The logger is already frozen"。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebHostCollection
{
    public const string Name = "WebHost";
}
