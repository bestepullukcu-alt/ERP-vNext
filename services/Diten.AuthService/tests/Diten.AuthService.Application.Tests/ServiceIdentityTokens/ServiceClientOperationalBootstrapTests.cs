using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

[Collection(OperationalMongoCollection.Name)]
public sealed class ServiceClientOperationalBootstrapTests(OperationalMongoFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData("Production", "Production")]
    [InlineData("Development", "Production")]
    [InlineData("development", "Development")]
    [InlineData("", "Development")]
    public async Task Release_child_rejects_non_exact_process_environment_before_composition(string dotnet, string aspnet)
    {
        var start = ChildStart([ServiceClientOperationalProvisioningRunner.Mode]);
        start.Environment["DOTNET_ENVIRONMENT"] = dotnet;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = aspnet;
        var result = await RunChild(start, "{}");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("environment", result.Stdout);
        Assert.DoesNotContain("Now listening", result.Stdout);
    }

    [Theory]
    [InlineData("--SERVICE-CLIENT-OPERATIONAL-RUN")]
    [InlineData("--service-client-operational-unknown")]
    public async Task Release_child_captures_malformed_family_before_normal_startup(string command)
    {
        var result = await RunChild(ChildStart([command]), "{}");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("contract", result.Stdout);
        Assert.DoesNotContain("Now listening", result.Stdout);
    }

    public static IEnumerable<object[]> MixedSelectors()
    {
        const string credentials = "--service-client-operational-run";
        const string entitlements = "--run-product-identity-entitlement-reconciliation";
        foreach (var pair in new[]
        {
            new[] { credentials, entitlements }, new[] { entitlements, credentials },
            new[] { credentials.ToUpperInvariant(), entitlements }, new[] { entitlements, credentials.ToUpperInvariant() },
            new[] { credentials, entitlements.ToUpperInvariant() }, new[] { entitlements.ToUpperInvariant(), credentials },
            new[] { credentials, credentials, entitlements }, new[] { entitlements, entitlements, credentials },
            new[] { "--service-client-operational-unknown", entitlements },
            new[] { "--run-product-identity-entitlement-reconciliation-unknown", credentials }
        }) yield return [pair];
    }

    [Theory]
    [MemberData(nameof(MixedSelectors))]
    public async Task Release_child_mixed_operational_selectors_reject_before_host_without_secret_or_database_mutation(string[] selectors)
    {
        await using var scope = await fixture.ScopeAsync(output);
        var actor = new OperationalActorHarness();
        await using var actorRows = await ActorRows.InsertAsync(fixture.Database, actor, output);
        var request = scope.Request("read-identity");
        var token = actor.Token();
        var start = ChildStart(selectors);
        Configure(start, OperationalTestData.Options(request), actor);
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["Eventing__Transport"] = "InMemory";
        start.Environment["DITEN_ENTITLEMENT_OPERATOR_ACCESS_TOKEN"] = token;
        var oplog = fixture.Database.Client.GetDatabase("local").GetCollection<BsonDocument>("oplog.rs");
        var before = (await oplog.Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("$natural", -1)).Limit(1).SingleAsync())["ts"];
        var collections = await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync();

        var result = await RunChild(start, ServiceClientOperationalProvisioningRunnerTests.Envelope(request, token));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("contract", result.Stdout);
        Assert.DoesNotContain("Now listening", result.Stdout + result.Stderr);
        Assert.DoesNotContain("Application started", result.Stdout + result.Stderr);
        Assert.False((result.Stdout + result.Stderr).Contains(token, StringComparison.Ordinal), "Operator token must not escape the child.");
        Assert.DoesNotContain(OperationalTestData.Marker, result.Stdout + result.Stderr);
        Assert.Empty(await oplog.Find(new BsonDocument { { "ts", new BsonDocument("$gt", before) },
            { "ns", new BsonRegularExpression("^" + OperationalMongoFixture.DatabaseName + "\\.") } }).ToListAsync());
        Assert.Equal(collections.Order(StringComparer.Ordinal), (await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync()).Order(StringComparer.Ordinal));
        Assert.Equal(0, await scope.Journal.CountDocumentsAsync(x => x.TargetId == scope.Identity.Id));
        Assert.Equal(0, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
        await actorRows.AssertUnchangedAsync();
    }

    [Fact]
    public async Task Real_Release_child_rotates_through_actual_pipe_without_normal_startup_or_unrelated_writes()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var actor = new OperationalActorHarness();
        await using var actorRows = await ActorRows.InsertAsync(fixture.Database, actor, output);
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var request = scope.Request() with { InheritedPipeHandle = pipe.GetClientHandleAsString() };
        var token = actor.Token();
        var options = OperationalTestData.Options(request);
        var start = ChildStart([ServiceClientOperationalProvisioningRunner.Mode]);
        Configure(start, options, actor);
        var oplog = fixture.Database.Client.GetDatabase("local").GetCollection<BsonDocument>("oplog.rs");
        var before = (await oplog.Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("$natural", -1)).Limit(1).SingleAsync())["ts"];
        var collectionNames = await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync();
        var result = await RunChild(start, ServiceClientOperationalProvisioningRunnerTests.Envelope(request, token), pipe.DisposeLocalCopyOfClientHandle);
        Assert.False(result.Stdout.Contains(token, StringComparison.Ordinal) || result.Stderr.Contains(token, StringComparison.Ordinal), "Child output must not disclose operator JWT.");
        Assert.DoesNotContain(OperationalTestData.Marker, result.Stdout + result.Stderr);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("completed", result.Stdout);
        using var reader = new StreamReader(pipe);
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var secretEnvelope = JsonDocument.Parse(line!);
        var secret = secretEnvelope.RootElement.GetProperty("Secret").GetString()!;
        Assert.False(result.Stdout.Contains(secret, StringComparison.Ordinal) || result.Stderr.Contains(secret, StringComparison.Ordinal), "Secret must exist only in the actual inherited pipe.");
        var identity = await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync();
        Assert.True(new ServiceClientCredentialVerifier().Verify(identity, secret, DateTimeOffset.UtcNow), "Pipe credential must match committed identity hash.");
        Assert.Equal(1, identity.OperationalVersion);
        var operation = await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync();
        Assert.Equal(ServiceClientOperationalProvisioningState.Completed, operation.State);
        Assert.Equal(ServiceClientOperationalProvisioningCheckpoint.EvidenceRecorded, operation.Checkpoint);
        var afterNames = await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync();
        Assert.Equal(collectionNames.OrderBy(x => x, StringComparer.Ordinal), afterNames.OrderBy(x => x, StringComparer.Ordinal));
        var writes = await oplog.Find(new BsonDocument { { "ts", new BsonDocument("$gt", before) }, { "ns", new BsonRegularExpression("^" + OperationalMongoFixture.DatabaseName + "\\.") } }).ToListAsync();
        Assert.NotEmpty(writes);
        Assert.All(writes, write =>
        {
            Assert.NotEqual("c", write["op"].AsString);
            Assert.Contains(write["ns"].AsString, new[] { OperationalMongoFixture.DatabaseName + ".serviceClientIdentities", OperationalMongoFixture.DatabaseName + "." + OperationalMongoFixture.Journal });
        });
        await actorRows.AssertUnchangedAsync();
        output.WriteLine("Release child CLI exited 0; actual inherited pipe verified; no normal host, index DDL, catalog/grant/user/role writes; no plaintext secret in stdout/stderr/journal.");

        // Replay is a new isolated process, records the old outcome, and cannot reissue a secret.
        using var replayPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var replayRequest = request with { InheritedPipeHandle = replayPipe.GetClientHandleAsString() };
        var replayStart = ChildStart([ServiceClientOperationalProvisioningRunner.Mode]);
        Configure(replayStart, options, actor);
        var replay = await RunChild(replayStart, ServiceClientOperationalProvisioningRunnerTests.Envelope(replayRequest, token), replayPipe.DisposeLocalCopyOfClientHandle);
        Assert.Equal(0, replay.ExitCode);
        Assert.Contains("not-reissued", replay.Stdout);
        Assert.Equal(1, await scope.Journal.CountDocumentsAsync(x => x.CommandId == request.CommandId));
        Assert.Equal(1, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
    }

    [Fact]
    public async Task Release_child_current_revoked_grant_denies_before_credential_or_journal_mutation()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var actor = new OperationalActorHarness();
        await using var rows = await ActorRows.InsertAsync(fixture.Database, actor, output);
        await fixture.Database.GetCollection<RolePermission>("rolePermissions").UpdateOneAsync(x => x.Id == actor.Grant.Id && x.TenantId == OperationalTestData.PlatformTenant,
            Builders<RolePermission>.Update.Set(x => x.IsDeleted, true));
        var request = scope.Request("read-identity");
        var start = ChildStart([ServiceClientOperationalProvisioningRunner.Mode]);
        Configure(start, OperationalTestData.Options(request), actor);
        var result = await RunChild(start, ServiceClientOperationalProvisioningRunnerTests.Envelope(request, actor.Token()));
        Assert.Equal(3, result.ExitCode);
        Assert.Equal(0, await scope.Journal.CountDocumentsAsync(x => x.TargetId == scope.Identity.Id));
        Assert.Equal(0, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
    }

    private void Configure(ProcessStartInfo start, ServiceClientOperationalProvisioningOptions options, OperationalActorHarness actor)
    {
        start.Environment["MongoDbSettings__ConnectionString"] = fixture.ConnectionString;
        start.Environment["MongoDbSettings__DatabaseName"] = OperationalMongoFixture.DatabaseName;
        start.Environment["JwtSettings__Secret"] = Encoding.UTF8.GetString(actor.Key);
        start.Environment["JwtSettings__Issuer"] = "fixture-issuer";
        start.Environment["JwtSettings__Audience"] = "fixture-audience";
        foreach (var property in typeof(ServiceClientOperationalProvisioningOptions).GetProperties())
        {
            var value = property.GetValue(options);
            if (value is not null) start.Environment[ServiceClientOperationalProvisioningOptions.SectionName + "__" + property.Name] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static ProcessStartInfo ChildStart(string[] arguments)
    {
        var api = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.Api.dll");
        Assert.True(File.Exists(api) && File.Exists(Path.ChangeExtension(api, ".runtimeconfig.json")), "Matching isolated Release API artifacts are required.");
        var start = new ProcessStartInfo(@"C:\Program Files\dotnet\dotnet.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory
        };
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "PATH", "TEMP", "TMP", "USERPROFILE", "COMSPEC" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.ArgumentList.Add(api);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private async Task<ChildResult> RunChild(ProcessStartInfo start, string input, Action? afterStart = null)
    {
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Owned CLI child did not start.");
        afterStart?.Invoke();
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        try
        {
            await child.StandardInput.WriteAsync(input); child.StandardInput.Close();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            output.WriteLine($"Owned Release CLI PID={child.Id}; exit={child.ExitCode}; closed={child.HasExited}.");
            return new(child.ExitCode, await stdout, await stderr);
        }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
    }
    private sealed record ChildResult(int ExitCode, string Stdout, string Stderr);

    private sealed class ActorRows(IMongoDatabase database, ITestOutputHelper output) : IAsyncDisposable
    {
        private readonly List<(string Collection, Guid Id, string Digest)> _rows = [];
        internal static async Task<ActorRows> InsertAsync(IMongoDatabase database, OperationalActorHarness actor, ITestOutputHelper output)
        {
            var rows = new ActorRows(database, output);
            var membership = new UserRole(actor.User.Id, actor.Role.Id, OperationalTestData.PlatformTenant, "fixture");
            await rows.Insert("users", actor.User); await rows.Insert("roles", actor.Role);
            await rows.Insert("permissions", actor.Permission); await rows.Insert("rolePermissions", actor.Grant);
            await rows.Insert("userRoles", membership); return rows;
        }
        private async Task Insert<T>(string collection, T value)
        {
            var document = value.ToBsonDocument();
            await database.GetCollection<BsonDocument>(collection).InsertOneAsync(document);
            _rows.Add((collection, document["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard), Digest(document)));
        }
        internal async Task AssertUnchangedAsync()
        {
            foreach (var row in _rows)
            {
                var current = await database.GetCollection<BsonDocument>(row.Collection).Find(new BsonDocument("_id", new BsonBinaryData(row.Id, GuidRepresentation.Standard))).SingleAsync();
                Assert.Equal(row.Digest, Digest(current));
            }
        }
        public async ValueTask DisposeAsync()
        {
            foreach (var row in _rows)
            {
                var filter = new BsonDocument("_id", new BsonBinaryData(row.Id, GuidRepresentation.Standard));
                if (row.Collection != "permissions") filter.Add("TenantId", new BsonBinaryData(OperationalTestData.PlatformTenant, GuidRepresentation.Standard));
                var collection = database.GetCollection<BsonDocument>(row.Collection);
                Assert.True((await collection.DeleteOneAsync(filter)).IsAcknowledged);
                Assert.Equal(0, await collection.CountDocumentsAsync(filter));
            }
            output.WriteLine("Fixture-owned platform actor/current-grant rows cleanup acknowledged; remaining=0.");
        }
        private static string Digest(BsonDocument value) => Convert.ToHexString(SHA256.HashData(value.ToBson()));
    }
}
