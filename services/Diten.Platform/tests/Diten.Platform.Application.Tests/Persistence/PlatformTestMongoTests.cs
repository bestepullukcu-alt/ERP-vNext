using Diten.Platform.Tests.Shared;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// WP-CT-LANE-MAIN-SYNC-01 (CT decision C) — the ONE source of the Platform tests' Mongo, on fakes: a given lane URI
/// (main's Q131a check, PlatformMongoTestConnectionTests holds its details), else a mongod this process starts from
/// DITEN_TEST_MONGOD (Auth's rule, BL-570), else red by name — never 27017. And the live answer of this run.
/// </summary>
[Trait("Category", "Mongo")]
public sealed class PlatformTestMongoTests
{
    private const string Installed = "/usr/test/bin/mongod";
    private static readonly Func<string, bool> OnlyInstalled = path => path == Installed;

    private static Task<string> Address(string? uri, string? mongod, Action<string>? started = null) =>
        PlatformTestMongo.AddressAsync(
            PlatformTestMongo.Choose(uri, mongod, windows: false, OnlyInstalled),
            executable => { started?.Invoke(executable); return Task.FromResult("mongodb://127.0.0.1:40123/?replicaSet=rs0&directConnection=true"); });

    [Fact]
    public async Task A_given_lane_uri_is_used_as_given_and_nothing_is_started()
    {
        string? started = null;
        var address = await Address("mongodb://127.0.0.1:27118/?directConnection=true", Installed, e => started = e);

        Assert.Equal("mongodb://127.0.0.1:27118/?directConnection=true", address);
        Assert.Null(started);
    }

    // Main's Q131a rule decides a given URI — a protected port, a remote host, credentials, an empty value: red.
    [Theory]
    [InlineData("mongodb://127.0.0.1:27017")]
    [InlineData("mongodb://localhost")]
    [InlineData("mongodb://10.0.0.5:27118")]
    [InlineData("mongodb://user:pass@127.0.0.1:27118")]
    [InlineData("")]
    public async Task A_given_lane_uri_that_breaks_the_rule_is_red_and_starts_nothing(string uri)
    {
        string? started = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Address(uri, Installed, e => started = e));
        Assert.Null(started);
    }

    [Fact]
    public async Task Without_a_uri_an_installed_mongod_is_started_by_this_process()
    {
        string? started = null;
        var address = await Address(null, Installed, e => started = e);

        Assert.Equal(Installed, started);
        Assert.Equal(40123, MongoUrl.Create(address).Server.Port);
    }

    // FIX4 rule (Auth): an EMPTY DITEN_TEST_MONGOD is "given" — invalid, red; never "not given".
    [Theory]
    [InlineData("")]
    [InlineData("mongod")]
    [InlineData("bin/mongod")]
    [InlineData("/usr/missing/mongod")]
    public async Task A_given_mongod_variable_that_names_no_installed_mongod_is_red_and_starts_nothing(string mongod)
    {
        string? started = null;
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Address(null, mongod, e => started = e));

        Assert.StartsWith(PlatformTestMongo.InvalidVariable + ":", failure.Message);
        Assert.Null(started);
    }

    // Neither: red by name (the Platform harness never skipped) — and NEVER an address, least of all the shared 27017.
    [Fact]
    public async Task Neither_variable_is_red_by_name_and_never_falls_back_to_the_shared_mongo()
    {
        string? started = null;
        string? address = null;
        var failure = await Record.ExceptionAsync(async () => address = await Address(null, null, e => started = e));

        Assert.Null(address);
        Assert.Null(started);
        Assert.StartsWith(PlatformTestMongo.Unavailable + ":", Assert.IsType<InvalidOperationException>(failure).Message);
    }

    [Fact]
    public void The_windows_default_install_counts_on_windows_only()
    {
        Func<string, bool> windowsInstall = path => path == PlatformTestMongo.WindowsDefaultMongod;
        Assert.Equal(PlatformTestMongo.WindowsDefaultMongod, PlatformTestMongo.Choose(null, null, windows: true, windowsInstall).Executable);
        Assert.NotNull(PlatformTestMongo.Choose(null, null, windows: false, windowsInstall).Failure);
    }

    // The live answer of THIS run: a lane URI or an owned mongod — loopback, outside the protected band.
    [Fact]
    public async Task The_address_in_this_run_is_never_the_shared_mongo()
    {
        // BL-395: every file that resolves the Mongo address takes the machine-wide lock first (PlatformMongoTestLockTests).
        await PlatformMongoTestLock.EnsureHeldAsync();
        var url = MongoUrl.Create(await PlatformMongoTestConnection.RequireConnectionStringAsync());

        Assert.Contains(url.Server.Host, new[] { "127.0.0.1", "localhost", "::1", "[::1]" });
        Assert.False(url.Server.Port is >= 27017 and <= 27021, $"port {url.Server.Port}");
    }
}
