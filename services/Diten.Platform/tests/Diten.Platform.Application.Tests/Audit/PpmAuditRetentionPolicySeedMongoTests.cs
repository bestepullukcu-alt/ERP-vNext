using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Settings;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// The seed itself is intentionally exercised without applying a schema profile. The disposable
/// Mongo process and its database are test-owned and removed by this fixture; production startup
/// never creates an unrelated database or applies a schema as part of this test.
/// </summary>
public sealed class PpmAuditRetentionPolicySeedMongoTests : IAsyncLifetime
{
    private const string MongoExecutable = "/opt/homebrew/bin/mongod";
    private static readonly HashSet<int> ProtectedPorts = [27017, 27018, 27019, 27020, 27021];
    private readonly AuditRetentionSeedOptions _options = new()
    {
        MinimumRetentionDays = 30,
        DefaultRetentionDays = 365,
        MaximumRetentionDays = 2555,
        HotStorageDays = 90,
        AllowTenantOverride = true,
        ColdStoragePrepared = true
    };

    private Process? _process;
    private string? _temporaryRoot;
    private int _port;
    private IMongoClient? _client;
    private IMongoDatabase? _database;

    public async Task InitializeAsync()
    {
        if (!File.Exists(MongoExecutable))
        {
            throw new FileNotFoundException("The test-owned mongod executable is unavailable.", MongoExecutable);
        }

        _port = SelectAllowedPort();
        _temporaryRoot = Path.Combine(Path.GetTempPath(), $"diten-platform-audit-seed-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(_temporaryRoot, "data");
        Directory.CreateDirectory(dataDirectory);

        var startInfo = new ProcessStartInfo(MongoExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 {
                     "--bind_ip", "127.0.0.1",
                     "--port", _port.ToString(CultureInfo.InvariantCulture),
                     "--dbpath", dataDirectory,
                     "--logpath", Path.Combine(_temporaryRoot, "mongod.log"),
                     "--pidfilepath", Path.Combine(_temporaryRoot, "mongod.pid"),
                     "--nounixsocket"
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Disposable audit-seed mongod did not start.");
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        _client = new MongoClient($"mongodb://127.0.0.1:{_port}/?directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPingAsync(_client);
        _database = _client.GetDatabase($"diten_platform_audit_seed_{Guid.NewGuid():N}");

        var policies = _database.GetCollection<AuditEventRetentionPolicy>("audit_event_retention_policies");
        var baseline = Enum.GetValues<AuditCategory>()
            .Where(category => category is not AuditCategory.Unknown and not AuditCategory.PortfolioDelivery)
            .Select(CreatePolicy)
            .ToList();
        await policies.InsertManyAsync(baseline);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_client is not null && _database is not null)
            {
                await _client.DropDatabaseAsync(_database.DatabaseNamespace.DatabaseName);
            }
        }
        catch (MongoException)
        {
            // Process cleanup remains authoritative if test-owned Mongo is already unavailable.
        }

        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }

            _process.Dispose();
        }

        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }

        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
        {
            throw new InvalidOperationException("Disposable audit-seed Mongo temporary residue remains.");
        }

        if (_port != 0 && !CanBind(_port))
        {
            throw new InvalidOperationException("Disposable audit-seed Mongo listener residue remains.");
        }
    }

    [Fact]
    public async Task Inserts_only_missing_portfolio_delivery_policy_and_is_idempotent()
    {
        var database = Assert.IsAssignableFrom<IMongoDatabase>(_database);
        var policies = database.GetCollection<AuditEventRetentionPolicy>("audit_event_retention_policies");
        var before = await policies.Find(FilterDefinition<AuditEventRetentionPolicy>.Empty).ToListAsync();

        await AuditRetentionPolicySeed.EnsureSeededAsync(database, _options);

        var afterFirstSeed = await policies.Find(FilterDefinition<AuditEventRetentionPolicy>.Empty).ToListAsync();
        var portfolioPolicy = Assert.Single(afterFirstSeed, policy =>
            policy.Category == AuditCategory.PortfolioDelivery
            && policy.PlanTierCode == AuditEventRetentionPolicy.DefaultPlanTierCode
            && !policy.IsDeleted);
        Assert.Equal(before.Count + 1, afterFirstSeed.Count);
        Assert.Equal(_options.DefaultRetentionDays, portfolioPolicy.DefaultRetentionDays);
        Assert.Equal(_options.MinimumRetentionDays, portfolioPolicy.MinimumRetentionDays);
        Assert.Equal(_options.MaximumRetentionDays, portfolioPolicy.MaximumRetentionDays);
        Assert.Equal(_options.HotStorageDays, portfolioPolicy.HotStorageDays);
        Assert.All(before, existing => Assert.Contains(afterFirstSeed, candidate =>
            candidate.Id == existing.Id
            && candidate.Category == existing.Category
            && candidate.DefaultRetentionDays == existing.DefaultRetentionDays
            && candidate.MinimumRetentionDays == existing.MinimumRetentionDays
            && candidate.MaximumRetentionDays == existing.MaximumRetentionDays
            && candidate.HotStorageDays == existing.HotStorageDays));

        await AuditRetentionPolicySeed.EnsureSeededAsync(database, _options);

        var afterSecondSeed = await policies.Find(FilterDefinition<AuditEventRetentionPolicy>.Empty).ToListAsync();
        Assert.Equal(afterFirstSeed.Count, afterSecondSeed.Count);
        Assert.Single(afterSecondSeed, policy => policy.Category == AuditCategory.PortfolioDelivery);
    }

    private AuditEventRetentionPolicy CreatePolicy(AuditCategory category) => new()
    {
        Category = category,
        PlanTierCode = AuditEventRetentionPolicy.DefaultPlanTierCode,
        MinimumRetentionDays = _options.MinimumRetentionDays,
        DefaultRetentionDays = _options.DefaultRetentionDays,
        MaximumRetentionDays = _options.MaximumRetentionDays,
        HotStorageDays = _options.HotStorageDays,
        AllowTenantOverride = _options.AllowTenantOverride,
        ColdStoragePrepared = _options.ColdStoragePrepared,
        IsActive = true
    };

    private static int SelectAllowedPort()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022 && !ProtectedPorts.Contains(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("The operating system did not provide an allowed Mongo test port.");
    }

    private static async Task WaitForPingAsync(IMongoClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                last = exception;
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException("Disposable audit-seed Mongo did not accept connections.", last);
    }

    private static bool CanBind(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
