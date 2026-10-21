using System.Reflection;
using System.Text.Json;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Audit;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Persistence.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using TerritoryFakes = Diten.CrmService.Application.Tests.Territory;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-CRM-MOBILE-REQ CRM-ACC-EDIT — MOD-0149 §19 on CRM's REAL pipeline (validators → handler, audit forwarding): an edit
/// needs the version it read (400 without, 409 on a stale one, the record unchanged; a concurrent second edit loses); an
/// omitted logo is kept, <c>removeLogo</c> removes it; the fields the command does not carry stay; every 400 / 409 carries
/// its machine code (Create and Update alike); another tenant / a deleted account is 404; the edit is audited once.
/// </summary>
public sealed class AccountEditTests : IDisposable
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-4000-8000-0000000000ac");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-4000-8000-0000000000ac");
    private const string Logo = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly CopyingAccounts _accounts = new();
    private readonly MemoryExternalRefs _externalRefs = new();
    private readonly CapturingAuditPlatform _audit = new();
    private readonly ServiceProvider _provider;

    public AccountEditTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformService:BaseUrl"] = "http://platform.test:5057", ["AuthService:InternalApiKey"] = "acc-edit-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddCrmAuditForwarding(configuration);
        services.AddHttpClient(Diten.BuildingBlocks.Auditing.PlatformAuditForwarder.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _audit);
        services.AddSingleton<ITenantContext>(TerritoryFakes.TenantFactory.Tenant(Tenant));
        services.AddSingleton<IActorContext>(new SwitchActor("user-editor"));
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<IAccountExternalReferenceRepository>(_externalRefs);
        services.AddSingleton<IAccountAttributeValueRepository>(new NoAttributes());
        services.AddSingleton<IAccountCodeGenerator>(new SequentialCodes());
        services.AddSingleton<IAccountAuditPublisher>(new NoopAccountAudit());
        services.AddSingleton<IReferenceDataValidator>(new TerritoryFakes.FakeReferenceValidator()
            .Publish("account-type", "pharmacy", "hospital").Publish("account-status", "active", "passive").Publish("account-category", "a", "b"));
        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    private async Task<T> Send<T>(IRequest<T> request)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private Account Seed(Guid? tenant = null, int version = 0, string? logo = Logo)
    {
        var account = new Account
        {
            TenantId = tenant ?? Tenant, AccountName = "Eczane Şifa", AccountCode = "ACC-2026-000042", AccountType = "pharmacy", Status = "active",
            ParentAccountId = Guid.NewGuid(), CityRef = "istanbul", LogoDataUri = logo, Version = version,
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        };
        _accounts.Items.Add(account);
        return account;
    }

    private static UpdateAccountCommand Edit(Guid id, int? expectedVersion, string name = "Eczane Şifa Yeni", string? logo = null, bool removeLogo = false,
        string type = "pharmacy", string status = "active")
        => new(id, name, type, null, status, "tr", "istanbul", null, "Bağdat Cad. 2", 40.9, 29.1, "Ali", "+905551112233", "ali@sifa.test",
            "not", logo, expectedVersion, removeLogo);

    private static string Code<T>(Response<T> r) => r.Errors is { Count: >= 2 } e ? e[1] : string.Empty;

    // ── the version (§19: 409 + reload, no silent overwrite) ─────────────────────────────────────────────────────

    [Fact]
    public async Task E1_an_edit_with_the_version_it_read_writes_once_and_advances_the_version()
    {
        var account = Seed(version: 3);

        var ok = await Send(Edit(account.Id, 3));
        var detail = await Send(new GetAccountByIdQuery(account.Id));

        Assert.True(ok.IsSuccessful, string.Join(" | ", ok.Errors ?? []));
        Assert.Equal(("Eczane Şifa Yeni", 4), (detail.Data!.AccountName, detail.Data.Version));
    }

    [Fact]
    public async Task E2_a_stale_version_is_409_account_version_conflict_and_the_record_is_unchanged()
    {
        var account = Seed(version: 5);
        var before = JsonSerializer.Serialize(_accounts.Peek(account.Id));

        var stale = await Send(Edit(account.Id, 4));

        Assert.Equal((409, AccountErrors.VersionConflict), (stale.StatusCode, Code(stale)));
        Assert.Equal(before, JsonSerializer.Serialize(_accounts.Peek(account.Id)));
    }

    [Fact]
    public async Task E3_without_expectedVersion_the_edit_is_400_and_nothing_changes()
    {
        var account = Seed();

        var missing = await Send(Edit(account.Id, null));

        Assert.Equal((400, AccountErrors.ExpectedVersionRequired), (missing.StatusCode, Code(missing)));
        Assert.Equal("Eczane Şifa", _accounts.Peek(account.Id).AccountName);
    }

    [Fact]
    public async Task E4_two_concurrent_edits_of_the_same_version_have_one_winner()
    {
        var account = Seed(version: 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Send(Edit(account.Id, 1, name: "Ad " + i))));

        Assert.Single(results, r => r.IsSuccessful);
        Assert.All(results.Where(r => !r.IsSuccessful), r => Assert.Equal((409, AccountErrors.VersionConflict), (r.StatusCode, Code(r))));
        Assert.Equal(2, _accounts.Peek(account.Id).Version);
    }

    [Fact]
    public async Task E5_another_tenant_s_or_a_deleted_account_is_404()
    {
        var foreign = Seed(OtherTenant);
        var deleted = Seed();
        _accounts.Items.Single(a => a.Id == deleted.Id).IsDeleted = true;

        var r1 = await Send(Edit(foreign.Id, 0));
        var r2 = await Send(Edit(deleted.Id, 0));

        Assert.Equal((404, AccountErrors.NotFound), (r1.StatusCode, Code(r1)));
        Assert.Equal((404, AccountErrors.NotFound), (r2.StatusCode, Code(r2)));
    }

    [Fact]
    public async Task E6_a_hierarchy_write_between_the_read_and_the_edit_makes_the_edit_stale_instead_of_undoing_it()
    {
        var account = Seed();
        var read = (await Send(new GetAccountByIdQuery(account.Id))).Data!;
        var linked = _accounts.Peek(account.Id);
        linked.ParentAccountId = Guid.NewGuid();
        await _accounts.UpdateAsync(linked, default);                               // e.g. LinkParentAccount: an unconditional write

        var edit = await Send(Edit(account.Id, read.Version));

        Assert.Equal(409, edit.StatusCode);
        Assert.Equal(linked.ParentAccountId, _accounts.Peek(account.Id).ParentAccountId);
    }

    // ── the logo and the fields the command does not carry ───────────────────────────────────────────────────────

    [Fact]
    public async Task L1_an_omitted_logo_is_kept_a_new_one_replaces_it_and_removeLogo_removes_it()
    {
        var account = Seed();

        await Send(Edit(account.Id, 0));
        Assert.Equal(Logo, _accounts.Peek(account.Id).LogoDataUri);
        await Send(Edit(account.Id, 1, logo: Logo.Replace("iVBOR", "iVBOX")));
        Assert.StartsWith("data:image/png;base64,iVBOX", _accounts.Peek(account.Id).LogoDataUri);
        await Send(Edit(account.Id, 2, removeLogo: true));
        Assert.Null(_accounts.Peek(account.Id).LogoDataUri);

        var both = await Send(Edit(account.Id, 3, logo: Logo, removeLogo: true));
        Assert.Equal((400, AccountErrors.LogoConflict), (both.StatusCode, Code(both)));
    }

    [Fact]
    public async Task L2_the_fields_the_update_does_not_carry_stay_as_stored()
    {
        // UpdateAccountCommand does not carry: AccountCode, ParentAccountId, CreatedAt, TenantId, IsDeleted / DeletedAt, Id;
        // the external references and attributes are other collections (their own endpoints)
        var account = Seed();
        var external = new AccountExternalReference { TenantId = Tenant, AccountId = account.Id, SourceSystem = "OldCRM", ExternalId = "WP-9" };
        _externalRefs.Items.Add(external);
        var before = _accounts.Peek(account.Id);

        await Send(Edit(account.Id, 0));
        var after = _accounts.Peek(account.Id);

        Assert.Equal((before.AccountCode, before.ParentAccountId, before.CreatedAt, before.TenantId, before.IsDeleted, before.DeletedAt, before.Id),
            (after.AccountCode, after.ParentAccountId, after.CreatedAt, after.TenantId, after.IsDeleted, after.DeletedAt, after.Id));
        Assert.Equal(new[] { "WP-9" }, _externalRefs.Items.Select(r => r.ExternalId));
        Assert.Equal(("Bağdat Cad. 2", "ali@sifa.test", 40.9), (after.AddressLine, after.ResponsiblePersonEmail, after.Latitude));
    }

    [Fact]
    public void L3_the_update_command_carries_exactly_the_editable_fields()
    {
        var carried = typeof(UpdateAccountCommand).GetProperties().Select(p => p.Name).ToHashSet();
        var stored = typeof(Account).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Equal(new[] { "AccountCode", "CreatedAt", "DeletedAt", "IsDeleted", "ParentAccountId", "TenantId", "UpdatedAt", "Version" },
            stored.Except(carried).OrderBy(n => n, StringComparer.Ordinal));
    }

    // ── machine codes (Create + Update alike) ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("name", AccountErrors.NameRequired)]
    [InlineData("type", AccountErrors.TypeRequired)]
    [InlineData("type-invalid", AccountErrors.TypeInvalid)]
    [InlineData("status-invalid", AccountErrors.StatusInvalid)]
    [InlineData("email", AccountErrors.EmailInvalid)]
    [InlineData("logo", AccountErrors.LogoInvalid)]
    public async Task C1_a_bad_field_is_400_with_its_code_on_update_and_on_create(string what, string code)
    {
        var account = Seed();
        var update = what switch
        {
            "name" => Edit(account.Id, 0, name: " "),
            "type" => Edit(account.Id, 0, type: ""),
            "type-invalid" => Edit(account.Id, 0, type: "garage"),
            "status-invalid" => Edit(account.Id, 0, status: "lost"),
            "email" => Edit(account.Id, 0) with { ResponsiblePersonEmail = "not-an-email" },
            _ => Edit(account.Id, 0, logo: "data:text/plain;base64,AAAA")
        };
        var create = new CreateAccountCommand(update.AccountName, null, update.AccountType, null, null, update.Status, null, null, null, null, null, null,
            null, null, update.ResponsiblePersonEmail, null, null, update.LogoDataUri);

        var u = await Send(update);
        var c = await Send(create);

        Assert.Equal((400, code), (u.StatusCode, Code(u)));
        Assert.Equal((400, code), (c.StatusCode, Code(c)));
        Assert.Equal(2, u.Errors!.Count % 2 == 0 ? 2 : u.Errors.Count);              // [message, code] pairs
        Assert.Equal("Eczane Şifa", _accounts.Peek(account.Id).AccountName);
    }

    [Fact]
    public async Task C2_create_conflicts_answer_account_code_taken_and_external_reference_taken()
    {
        Seed();
        _externalRefs.Items.Add(new AccountExternalReference { TenantId = Tenant, AccountId = Guid.NewGuid(), SourceSystem = "default", ExternalId = "WP-1" });

        var code = await Send(new CreateAccountCommand("Yeni", "ACC-2026-000042", "pharmacy", null, null, "active", null, null, null, null, null, null, null,
            null, null, null, null));
        var external = await Send(new CreateAccountCommand("Yeni", null, "pharmacy", null, null, "active", null, null, null, null, null, null, null, null,
            null, null, new ExternalReferenceInput("WP-1", null, null, null)));

        Assert.Equal((409, AccountErrors.CodeTaken), (code.StatusCode, Code(code)));
        Assert.Equal((409, AccountErrors.ExternalReferenceTaken), (external.StatusCode, Code(external)));
        Assert.Single(_accounts.Items);
    }

    [Fact]
    public async Task C3_a_validator_without_machine_codes_keeps_the_messages_only_envelope_and_a_coded_one_answers_pairs()
    {
        var plain = new Diten.CrmService.Application.Behaviors.ValidationBehavior<Probe, Response<bool>>([new PlainValidator()]);
        var coded = new Diten.CrmService.Application.Behaviors.ValidationBehavior<Probe, Response<bool>>([new CodedValidator()]);

        var a = await plain.Handle(new Probe(""), () => Task.FromResult(Response<bool>.Success(true)), default);
        var b = await coded.Handle(new Probe(""), () => Task.FromResult(Response<bool>.Success(true)), default);

        Assert.Equal(new[] { "Name is required." }, a.Errors);                     // every other module: unchanged
        Assert.Equal(new[] { "Name is required.", "probe_name_required" }, b.Errors);
    }

    public sealed record Probe(string Name) : IRequest<Response<bool>>;

    private sealed class PlainValidator : FluentValidation.AbstractValidator<Probe>
    {
        public PlainValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class CodedValidator : FluentValidation.AbstractValidator<Probe>
    {
        public CodedValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.").WithErrorCode("probe_name_required");
    }

    // ── audit + permission ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A1_create_and_update_are_audited_once_each_with_MOD_0149_and_no_logo_bytes()
    {
        var account = Seed();
        await Send(Edit(account.Id, 0, logo: Logo));
        await Send(new GetAccountByIdQuery(account.Id));
        await Send(new CreateAccountCommand("Yeni", null, "pharmacy", null, null, "active", null, null, null, null, null, null, null, null, null, null, null,
            Logo));

        Assert.Equal(new[] { "UpdateAccountCommand", "CreateAccountCommand" },
            _audit.Bodies.Select(b => JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString()));
        Assert.All(_audit.Bodies, b =>
        {
            Assert.Contains("MOD-0149", b);
            Assert.DoesNotContain("iVBORw0KGgo", b);
        });
    }

    [Fact]
    public void A2_the_edit_endpoint_is_still_PUT_accounts_id_behind_crm_account_update()
    {
        var update = typeof(AccountController).GetMethod(nameof(AccountController.Update))!;

        Assert.Equal("crm.account.update", Assert.Single(update.GetCustomAttributes<HasPermissionAttribute>()).Permission);
        Assert.Equal("{id:guid}", update.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPutAttribute>()!.Template);
    }

    // ═══ the real mongod (skipped without CRM_TEST_MONGO_URI; never the dev database) ═══════════════════════════

    [Fact]
    public async Task M1_on_a_real_mongod_the_version_filter_is_atomic_and_a_script_loaded_account_without_Version_is_version_0()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return;
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "ACC_EDIT_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            var repo = new AccountRepository(db);
            var account = new Account { TenantId = Tenant, AccountName = "A", AccountCode = "ACC-1", AccountType = "pharmacy", Status = "active", Version = 7 };
            await repo.InsertAsync(account, default);

            // ten concurrent conditional writes of version 7: exactly one lands
            var wins = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
            {
                var copy = await repo.GetByIdAsync(Tenant, account.Id, default);
                copy!.AccountName = "W" + i;
                return await repo.ReplaceIfVersionAsync(copy, 7, default);
            }));
            Assert.Single(wins, w => w);
            Assert.Equal(8, (await repo.GetByIdAsync(Tenant, account.Id, default))!.Version);

            // another tenant / a deleted account never matches
            var foreign = await repo.GetByIdAsync(Tenant, account.Id, default);
            foreign!.TenantId = OtherTenant;
            Assert.False(await repo.ReplaceIfVersionAsync(foreign, 8, default));

            // the unconditional write advances the version too
            var current = (await repo.GetByIdAsync(Tenant, account.Id, default))!;
            await repo.UpdateAsync(current, default);
            Assert.Equal(9, (await repo.GetByIdAsync(Tenant, account.Id, default))!.Version);

            // a script-loaded document with no Version and no IsDeleted field: read as 0, edited with expectedVersion 0
            var legacyId = Guid.NewGuid();
            await db.GetCollection<BsonDocument>("accounts").InsertOneAsync(new BsonDocument
            {
                ["_id"] = legacyId.ToString("D"), ["TenantId"] = Tenant.ToString("D"), ["AccountName"] = "Eski", ["AccountCode"] = "ACC-L",
                ["AccountType"] = "pharmacy", ["Status"] = "active", ["CreatedAt"] = new BsonArray { 0L, 0 }
            });
            var legacy = await repo.GetByIdAsync(Tenant, legacyId, default);
            Assert.Equal(0, legacy!.Version);
            legacy.AccountName = "Eski Yeni";
            Assert.True(await repo.ReplaceIfVersionAsync(legacy, 0, default));
            Assert.False(await repo.ReplaceIfVersionAsync(legacy, 0, default));      // now version 1
            Assert.Equal(1, (await db.GetCollection<BsonDocument>("accounts").Find(new BsonDocument("_id", legacyId.ToString("D"))).SingleAsync())["Version"].AsInt32);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    // ── doubles ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A database-like account store: deep copies, the version filter atomic under a lock.</summary>
    private sealed class CopyingAccounts : IAccountRepository
    {
        private readonly object _gate = new();
        public List<Account> Items { get; } = new();

        private static Account Copy(Account a) => JsonSerializer.Deserialize<Account>(JsonSerializer.Serialize(a))!;

        public Account Peek(Guid id)
        {
            lock (_gate) return Copy(Items.Single(a => a.Id == id));
        }

        public async Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            Account? found;
            lock (_gate) found = Items.Where(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted).Select(Copy).FirstOrDefault();
            await Task.Delay(20, ct); // a real database round trip: concurrent edits all read before any of them writes
            return found;
        }

        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct)
        {
            lock (_gate) return Task.FromResult(Items.Any(a => a.TenantId == tenantId && !a.IsDeleted && a.AccountCode == accountCode && a.Id != excludeId));
        }

        public Task InsertAsync(Account account, CancellationToken ct)
        {
            lock (_gate) Items.Add(Copy(account));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Account account, CancellationToken ct)
        {
            lock (_gate)
            {
                account.Version++;
                Items[Items.FindIndex(a => a.Id == account.Id && a.TenantId == account.TenantId)] = Copy(account);
            }

            return Task.CompletedTask;
        }

        public Task<bool> ReplaceIfVersionAsync(Account account, int expectedVersion, CancellationToken ct)
        {
            lock (_gate)
            {
                var index = Items.FindIndex(a => a.Id == account.Id && a.TenantId == account.TenantId && !a.IsDeleted && a.Version == expectedVersion);
                if (index < 0)
                {
                    return Task.FromResult(false);
                }

                account.Version = expectedVersion + 1;
                Items[index] = Copy(account);
                return Task.FromResult(true);
            }
        }

        public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotImplementedException();

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid tenantId, string? search, int page, int pageSize,
            string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
            IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotImplementedException();

        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class MemoryExternalRefs : IAccountExternalReferenceRepository
    {
        public List<AccountExternalReference> Items { get; } = new();

        public Task<bool> ExistsBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken ct)
            => Task.FromResult(Items.Any(r => r.TenantId == tenantId && r.SourceSystem == sourceSystem && r.ExternalId == externalId && r.Id != excludeId));

        public Task<IReadOnlyList<AccountExternalReference>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountExternalReference>>(Items.Where(r => r.TenantId == tenantId && r.AccountId == accountId).ToList());

        public Task InsertAsync(AccountExternalReference reference, CancellationToken ct)
        {
            Items.Add(reference);
            return Task.CompletedTask;
        }
    }

    private sealed class NoAttributes : IAccountAttributeValueRepository
    {
        public Task<IReadOnlyList<AccountAttributeValue>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountAttributeValue>>(Array.Empty<AccountAttributeValue>());

        public Task UpsertAsync(AccountAttributeValue attribute, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class SequentialCodes : IAccountCodeGenerator
    {
        private int _n;
        public Task<string> GenerateAsync(Guid tenantId, CancellationToken ct) => Task.FromResult($"ACC-2026-{Interlocked.Increment(ref _n) + 100:000000}");
    }

    private sealed class NoopAccountAudit : IAccountAuditPublisher
    {
        public Task PublishAsync(string eventName, Guid tenantId, Guid accountId, string? detail, CancellationToken ct) => Task.CompletedTask;
    }
}
