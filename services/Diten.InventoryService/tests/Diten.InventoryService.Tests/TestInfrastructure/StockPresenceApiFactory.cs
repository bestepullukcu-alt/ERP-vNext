using System.Collections.Concurrent;
using System.Security.Cryptography;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// WP-0173-PM2 — the real host with two configured service keys (<c>Inventory:InternalServiceKeys</c>, in-memory, minted per
/// test process — never a checked-in value):
/// <list type="bullet">
/// <item><c>ditenmdmservice</c> with TWO secrets (rotation overlap) opening <c>getStockPresence</c>;</item>
/// <item><c>otherservice</c> — a VALID key that opens another operation only (the "not authorised for this endpoint" case).</item>
/// </list>
/// Every log line of the host is captured in <see cref="Logs"/> (secret scan). Host environment "Testing": not Development, so
/// the https rule is on — <see cref="CreatePresenceClient"/> talks https unless asked otherwise.
/// </summary>
public class StockPresenceApiFactory : InventoryApiFactory
{
    public const string Mdm = "ditenmdmservice";
    public const string Other = "otherservice";

    public static readonly string MdmSecret1 = NewSecret();
    public static readonly string MdmSecret2 = NewSecret();
    public static readonly string OtherSecret = NewSecret();

    public CapturingLoggerProvider Logs { get; } = new();

    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static IEnumerable<string> AllSecrets => [MdmSecret1, MdmSecret2, OtherSecret];

    protected virtual IEnumerable<string> MdmSecrets => [MdmSecret1, MdmSecret2];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Inventory:InternalServiceKeys:0:Identifier"] = Mdm,
                ["Inventory:InternalServiceKeys:0:Endpoints:0"] = "getStockPresence",
                ["Inventory:InternalServiceKeys:1:Identifier"] = Other,
                ["Inventory:InternalServiceKeys:1:Secrets:0"] = OtherSecret,
                ["Inventory:InternalServiceKeys:1:Endpoints:0"] = "getSomethingElse"
            };
            var i = 0;
            foreach (var secret in MdmSecrets) settings[$"Inventory:InternalServiceKeys:0:Secrets:{i++}"] = secret;
            config.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);
            ConfigureMore(services);
        });
    }

    protected virtual void ConfigureMore(IServiceCollection services)
    {
    }

    /// <summary>A contract-validating client on https (or http, for the transport rule).</summary>
    public HttpClient CreatePresenceClient(bool https = true)
        => CreateDefaultClient(new Uri(https ? "https://localhost" : "http://localhost"), new ContractValidatingHandler());
}

/// <summary>The same keys after the rotation: <c>ditenmdmservice</c> keeps only its second secret — the first one left the list.</summary>
public sealed class RotatedStockPresenceApiFactory : StockPresenceApiFactory
{
    protected override IEnumerable<string> MdmSecrets => [MdmSecret2];
}

/// <summary>The store cannot be read (the presence reader throws) — measures the 503 <c>INVENTORY_UNAVAILABLE</c> path.</summary>
public sealed class UnreadableStockPresenceApiFactory : StockPresenceApiFactory
{
    protected override void ConfigureMore(IServiceCollection services)
        => services.AddScoped<IStockPresenceReader, ThrowingStockPresenceReader>();

    private sealed class ThrowingStockPresenceReader : IStockPresenceReader
    {
        public Task<bool> HasNonZeroStockAsync(Guid tenantId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default)
            => throw new TimeoutException("store unreachable (test)");
    }
}

/// <summary>Collects every formatted log line (message + exception) of the host, for the "secret never logged" scan.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Capturing(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Capturing(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => owner.Lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
    }
}
