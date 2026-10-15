extern alias FirstAdminTool;

using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Command = FirstAdminTool::Diten.AuthService.FirstPlatformAdministrator.FirstPlatformAdministratorCommand;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — the first platform administrator command, run in-process (its real
/// <c>RunAsync</c>) and as a process, against a NON-Development Auth host on a test-owned mongod: the Auth half goes to the
/// host's own database, the Platform half to a database next to it, and the host's platform status check reads that
/// Platform record — so the link the command hands over is redeemed at Auth's real set-password door.
/// </summary>
public sealed class FirstPlatformAdministratorCommandTests : IClassFixture<FirstPlatformAdministratorCommandTests.BootstrapHost>
{
    public const string PlatformDatabaseName = "first_admin_platform";
    private const string Email = "first.admin@bootstrap.test";
    private const string NewPassword = "Bootstrap!Pass123";
    private const string FrontendRoot = "https://console.bootstrap.test";

    private readonly BootstrapHost _host;

    public FirstPlatformAdministratorCommandTests(BootstrapHost host) => _host = host;

    public sealed class BootstrapHost : AccountKindAcceptance.AuthTestHost
    {
        protected override string HostEnvironmentName => "Production";

        public IMongoDatabase PlatformDatabase => Database.Client.GetDatabase(PlatformDatabaseName);

        public string JwtSecret => GeneratedJwtSecretForLeakGuardOnly ?? throw new InvalidOperationException("host not started");

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddSingleton<IPlatformAdministratorStatusClient>(sp => new RecordStatusClient(() => PlatformDatabase));
            services.AddScoped<ITenantLoginSettingsClient, PlainSettings>();
        }
    }

    [Fact]
    public async Task The_command_creates_one_administrator_whose_one_time_link_works_once_and_a_second_run_changes_nothing()
    {
        await ResetAsync();
        var linkFile = NewLinkPath();
        var (exit, output, error) = await RunAsync(linkFile);

        Assert.True(exit == Command.Created, error);
        var link = (await File.ReadAllTextAsync(linkFile)).Trim();
        var token = Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
        Assert.StartsWith(FrontendRoot + "/platform/reset-password?email=", link);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(linkFile));
        }

        // Nothing secret in what the command said: not the link, not its token, not a hash, no password.
        foreach (var said in new[] { output, error })
        {
            Assert.DoesNotContain(token, said);
            Assert.DoesNotContain("token=", said);
            Assert.DoesNotContain("$2", said);
            Assert.DoesNotContain(_host.JwtSecret, said);
        }

        // One id, both sides; the Auth account is an inactive invitation holding only the link's hash.
        var account = await _host.Database.GetCollection<User>("users").Find(u => u.Email == Email).SingleAsync();
        var record = await PlatformRecordAsync();
        Assert.Equal(account.Id, record["_id"].AsGuid);
        Assert.Contains(account.Id.ToString("D"), output);
        Assert.Equal(PlatformSystemTenantId, account.TenantId);
        Assert.Equal("platform_admin", account.PlatformActorType);
        Assert.False(account.IsActive);
        Assert.True(account.IsInvitationPending());
        Assert.NotEqual(token, account.PasswordResetTokenHash);
        Assert.Equal((int)PlatformStatus.Active, record["Status"].AsInt32);
        Assert.Equal(new BsonArray { (int)PlatformRole.SuperAdmin }, record["Roles"].AsBsonArray);
        var audit = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs").Find(a => a.UserId == account.Id).SingleAsync();
        Assert.DoesNotContain(Email, audit.Metadata);

        // The link sets the password and opens the account — once.
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemAsync(token)).StatusCode);
        Assert.True((await _host.Database.GetCollection<User>("users").Find(u => u.Id == account.Id).SingleAsync()).IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(token)).StatusCode);

        // A second run (same address, or another one) changes nothing on either side and hands over no link.
        var authBefore = await RawAuthAccountsAsync();
        var platformBefore = await _host.PlatformDatabase.GetCollection<BsonDocument>(PlatformCollection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (var email in new[] { Email, "second.admin@bootstrap.test" })
        {
            var secondLink = NewLinkPath();
            var (secondExit, _, secondError) = await RunAsync(secondLink, email);
            Assert.Equal(Command.Refused, secondExit);
            Assert.Contains("Nothing was changed", secondError);
            Assert.False(File.Exists(secondLink));
        }

        Assert.Equal(authBefore, await RawAuthAccountsAsync());
        Assert.Equal(platformBefore, await _host.PlatformDatabase.GetCollection<BsonDocument>(PlatformCollection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
    }

    [Fact]
    public async Task A_record_that_appears_between_the_two_writes_stops_the_command_part_way_and_names_the_account()
    {
        await ResetAsync();
        var (exit, _, error) = await RunAsync(NewLinkPath(), beforePlatformWrite: () =>
            _host.PlatformDatabase.GetCollection<BsonDocument>(PlatformCollection).InsertOneAsync(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
                ["NormalizedEmail"] = "someone.else@bootstrap.test",
                ["IsDeleted"] = false
            }));

        Assert.Equal(Command.PartWay, exit);
        var account = await _host.Database.GetCollection<User>("users").Find(u => u.Email == Email).SingleAsync();
        Assert.Contains(account.Id.ToString("D"), error);
        Assert.Equal(1, await _host.PlatformDatabase.GetCollection<BsonDocument>(PlatformCollection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [InlineData("--email", "not-an-address", "--frontend-base-url", FrontendRoot)]
    [InlineData("--email", Email, "--frontend-base-url", "http://console.bootstrap.test")]
    [InlineData("--email", Email, "--frontend-base-url", "https://console.bootstrap.test/?token=x")]
    public async Task Bad_arguments_change_nothing(string a, string b, string c, string d)
    {
        await ResetAsync();
        var linkFile = NewLinkPath();
        var output = new StringWriter();
        var error = new StringWriter();

        var environment = Environment();
        var exit = await Command.RunAsync([a, b, c, d, "--link-file", linkFile], name => environment.GetValueOrDefault(name), output, error);

        Assert.Equal(Command.UsageError, exit);
        Assert.False(File.Exists(linkFile));
        Assert.Equal(0, await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.TenantId == PlatformSystemTenantId));
    }

    [Fact]
    public async Task An_existing_link_file_is_refused_before_anything_is_written()
    {
        await ResetAsync();
        var linkFile = NewLinkPath();
        await File.WriteAllTextAsync(linkFile, "someone else's file");

        var (exit, _, _) = await RunAsync(linkFile);

        Assert.Equal(Command.UsageError, exit);
        Assert.Equal("someone else's file", await File.ReadAllTextAsync(linkFile));
        Assert.Equal(0, await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.Email == Email));
    }

    // As an operator runs it: the tool's own executable, the environment set, the output read. A connection string the
    // driver quotes in its message is never printed (K4 of the marker tool, the same check).
    [Fact]
    public async Task The_process_prints_neither_the_link_nor_a_broken_connection_string()
    {
        await ResetAsync();
        var dll = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.FirstPlatformAdministrator.dll");
        Assert.True(File.Exists(dll), dll);

        var linkFile = NewLinkPath();
        var (exit, stdout, stderr) = await RunProcessAsync(dll, linkFile, Environment());
        Assert.True(exit == Command.Created, stderr);
        var token = (await File.ReadAllTextAsync(linkFile)).Trim().Split("token=")[1];
        Assert.DoesNotContain(token, stdout + stderr);
        Assert.DoesNotContain(Uri.UnescapeDataString(token), stdout + stderr);

        var marker = string.Concat("do-not", "-print-", "b00t");
        var broken = Environment();
        broken[Command.AuthConnectionVariable] = string.Concat("mongodb", "://", "localhost", "/?", "replicaSet", "=", marker, "&", "connect", "=", marker);
        Assert.Contains(marker, Record.Exception(() => MongoClientSettings.FromConnectionString(broken[Command.AuthConnectionVariable]))?.Message ?? string.Empty);
        var (brokenExit, brokenOut, brokenErr) = await RunProcessAsync(dll, NewLinkPath(), broken);
        Assert.Equal(Command.Failure, brokenExit);
        Assert.DoesNotContain(marker, brokenOut + brokenErr, StringComparison.OrdinalIgnoreCase);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly Guid PlatformSystemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // Platform's names and stored values (PlatformCollections.PlatformAdministrators, AdministratorStatus, AdministratorRole);
    // that Platform itself reads the record as an active super administrator is measured in Platform's own tests
    // (PlatformAdministratorSeedMeasurementTests).
    private const string PlatformCollection = "platform_administrators";
    private enum PlatformStatus { Active = 1 }
    private enum PlatformRole { SuperAdmin = 1 }

    private async Task ResetAsync()
    {
        await _host.Database.GetCollection<User>("users").DeleteManyAsync(u => u.TenantId == PlatformSystemTenantId);
        await _host.Database.Client.DropDatabaseAsync(PlatformDatabaseName);
    }

    private Dictionary<string, string?> Environment() => new(StringComparer.Ordinal)
    {
        [Command.AuthConnectionVariable] = _host.ConnectionString,
        [Command.AuthDatabaseVariable] = _host.Database.DatabaseNamespace.DatabaseName,
        [Command.PlatformConnectionVariable] = _host.ConnectionString,
        [Command.PlatformDatabaseVariable] = PlatformDatabaseName,
        [Command.JwtSecretVariable] = _host.JwtSecret
    };

    private async Task<(int Exit, string Output, string Error)> RunAsync(
        string linkFile, string email = Email, Func<Task>? beforePlatformWrite = null)
    {
        var environment = Environment();
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await Command.RunAsync(
            ["--email", email, "--frontend-base-url", FrontendRoot, "--link-file", linkFile, "--display-name", "First Admin"],
            name => environment.GetValueOrDefault(name), output, error, beforePlatformWrite);
        return (exit, output.ToString(), error.ToString());
    }

    private static async Task<(int Exit, string Output, string Error)> RunProcessAsync(string dll, string linkFile, Dictionary<string, string?> environment)
    {
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[] { dll, "--email", Email, "--frontend-base-url", FrontendRoot, "--link-file", linkFile })
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000));
        return (process.ExitCode, await stdout, await stderr);
    }

    private static string NewLinkPath() => Path.Combine(Path.GetTempPath(), $"first-admin-link-{Guid.NewGuid():N}.txt");

    private Task<BsonDocument> PlatformRecordAsync() =>
        _host.PlatformDatabase.GetCollection<BsonDocument>(PlatformCollection).Find(new BsonDocument("NormalizedEmail", Email)).SingleAsync();

    private Task<List<BsonDocument>> RawAuthAccountsAsync() =>
        _host.Database.GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(PlatformSystemTenantId, GuidRepresentation.Standard)))
            .ToListAsync();

    private async Task<HttpResponseMessage> RedeemAsync(string token)
    {
        using var client = _host.Client();
        return await client.PostAsJsonAsync("api/platform-auth/reset-password", new { email = Email, token, newPassword = NewPassword });
    }

    /// <summary>Platform's status answer, read from the record the command wrote (Active and not deleted).</summary>
    private sealed class RecordStatusClient(Func<IMongoDatabase> platform) : IPlatformAdministratorStatusClient
    {
        public async Task<bool> IsActiveAsync(string email, CancellationToken ct)
        {
            var record = await platform().GetCollection<BsonDocument>(PlatformCollection)
                .Find(new BsonDocument("NormalizedEmail", email.Trim().ToLowerInvariant())).FirstOrDefaultAsync(ct);
            return record is not null && record["Status"].AsInt32 == (int)PlatformStatus.Active && !record["IsDeleted"].AsBoolean;
        }

        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;

        public async Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct) =>
            await IsActiveAsync(email, ct)
                ? PlatformAdministratorRecordAnswer.ConfirmedAs(PlatformAdministratorRecordAnswer.PlatformAdminActorType, ["SuperAdmin"])
                : PlatformAdministratorRecordAnswer.Refused;
    }

    private sealed class PlainSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new TenantLoginSettingsSnapshot(
            tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
            PasswordMinLength: 8, PasswordRequireUppercase: true, PasswordRequireLowercase: true, PasswordRequireDigit: true,
            PasswordRequireSpecialChar: true, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
            MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }
}
