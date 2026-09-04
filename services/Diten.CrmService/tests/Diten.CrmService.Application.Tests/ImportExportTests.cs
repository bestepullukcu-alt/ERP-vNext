using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.ImportExport.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;

namespace Diten.CrmService.Application.Tests;

/// <summary>MOD-0150 FU06 — import (dry-run + actual), reference validation, conflict rows, export/template.</summary>
public sealed class ImportExportTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    // ---------------- Contact import ----------------

    [Fact]
    public async Task ContactImport_DryRun_Validates_Without_Persisting()
    {
        var contacts = new FakeContactRepo();
        var handler = new ImportContactsHandler(Tenant(TenantA), contacts, new FakeContactRefRepo(), new FakeValidator(), new NoopContactAudit());
        var cmd = new ImportContactsCommand(DryRun: true, new[]
        {
            new ContactImportRow(null, null, "Ada", "Lovelace", null, "doctor", null, null, null, null, "ada@example.com", "active", null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.True(res.IsSuccessful);
        Assert.True(res.Data!.DryRun);
        Assert.Equal(1, res.Data.ValidRows);
        Assert.Equal(0, res.Data.CreatedRows);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task ContactImport_Actual_Creates_Contact_And_ExternalReference()
    {
        var contacts = new FakeContactRepo();
        var refs = new FakeContactRefRepo();
        var handler = new ImportContactsHandler(Tenant(TenantA), contacts, refs, new FakeValidator(), new NoopContactAudit());
        var cmd = new ImportContactsCommand(DryRun: false, new[]
        {
            new ContactImportRow("legacy-crm", "EXT-1", "Ada", "Lovelace", null, "doctor", null, null, null, null, null, "active", null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.CreatedRows);
        Assert.Single(contacts.Items);
        Assert.Single(refs.Items);
        Assert.Equal("legacy-crm", refs.Items[0].SourceSystem);
        Assert.Equal(contacts.Items[0].Id, refs.Items[0].ContactId);
    }

    [Fact]
    public async Task ContactImport_InvalidReference_Yields_Error_Row()
    {
        var contacts = new FakeContactRepo();
        var validator = new FakeValidator { Status = ReferenceValidationStatus.InvalidValue };
        var handler = new ImportContactsHandler(Tenant(TenantA), contacts, new FakeContactRefRepo(), validator, new NoopContactAudit());
        var cmd = new ImportContactsCommand(DryRun: false, new[]
        {
            new ContactImportRow(null, null, "Ada", null, null, "not-a-type", null, null, null, null, null, "nope", null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(0, res.Data!.ValidRows);
        Assert.Equal(1, res.Data.InvalidRows);
        Assert.Empty(contacts.Items);
        Assert.Contains(res.Data.Errors, e => e.Code == "invalid_reference");
    }

    [Fact]
    public async Task ContactImport_DuplicateExternalReference_Yields_Conflict_Row()
    {
        var contacts = new FakeContactRepo();
        var refs = new FakeContactRefRepo();
        refs.Items.Add(new ContactExternalReference { TenantId = TenantA, ContactId = Guid.NewGuid(), SourceSystem = "legacy-crm", ExternalId = "EXT-1" });
        var handler = new ImportContactsHandler(Tenant(TenantA), contacts, refs, new FakeValidator(), new NoopContactAudit());
        var cmd = new ImportContactsCommand(DryRun: false, new[]
        {
            new ContactImportRow("legacy-crm", "EXT-1", "Ada", null, null, "doctor", null, null, null, null, null, "active", null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.ConflictRows);
        Assert.Empty(contacts.Items);
        Assert.Contains(res.Data.Errors, e => e.Code == "conflict");
    }

    [Fact]
    public async Task ContactExport_Emits_Header_And_Rows()
    {
        var contacts = new FakeContactRepo();
        contacts.Items.Add(new Contact { TenantId = TenantA, FirstName = "Ada", LastName = "Lovelace", DisplayName = "Ada Lovelace", ContactType = "doctor", Status = "active" });
        var handler = new ExportContactsHandler(Tenant(TenantA), contacts, new NoopContactAudit());

        var res = await handler.Handle(new ExportContactsQuery(), default);
        var lines = res.Data!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("ExternalSourceSystem,ExternalId,FirstName", lines[0]);
        Assert.Contains("Ada", lines[1]);
    }

    // ---------------- AccountContact import ----------------

    [Fact]
    public async Task AccountContactImport_Resolves_By_Code_And_External_Then_Creates()
    {
        var accounts = new FakeAccountRepo();
        var account = new Account { TenantId = TenantA, AccountName = "Hospital", AccountCode = "ACC-A", AccountType = "hospital", Status = "active" };
        accounts.Items.Add(account);
        var contacts = new FakeContactRepo();
        var contact = new Contact { TenantId = TenantA, DisplayName = "Ada", FirstName = "Ada", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(contact);
        var refs = new FakeContactRefRepo();
        refs.Items.Add(new ContactExternalReference { TenantId = TenantA, ContactId = contact.Id, SourceSystem = "legacy-crm", ExternalId = "EXT-1" });
        var links = new FakeLinkRepo();

        var handler = new ImportAccountContactsHandler(Tenant(TenantA), accounts, contacts, refs, links, new FakeValidator(), new NoopContactAudit());
        var cmd = new ImportAccountContactsCommand(DryRun: false, new[]
        {
            new AccountContactImportRow("ACC-A", null, "legacy-crm", "EXT-1", null, "physician", true, null, null, null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.CreatedRows);
        Assert.Single(links.Items);
        Assert.Equal(account.Id, links.Items[0].AccountId);
        Assert.Equal(contact.Id, links.Items[0].ContactId);
    }

    [Fact]
    public async Task AccountContactImport_UnknownAccountCode_Yields_NotFound_Error()
    {
        var handler = new ImportAccountContactsHandler(
            Tenant(TenantA), new FakeAccountRepo(), new FakeContactRepo(), new FakeContactRefRepo(), new FakeLinkRepo(), new FakeValidator(), new NoopContactAudit());
        var cmd = new ImportAccountContactsCommand(DryRun: true, new[]
        {
            new AccountContactImportRow("MISSING", null, null, null, Guid.NewGuid(), "physician", false, null, null, null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.InvalidRows);
        Assert.Contains(res.Data.Errors, e => e.Code == "not_found");
    }

    // ---------------- AccountRelationship import ----------------

    [Fact]
    public async Task RelationshipImport_SelfLink_Blocked_When_Not_Allowed()
    {
        var accounts = new FakeAccountRepo();
        var a = new Account { TenantId = TenantA, AccountName = "H", AccountCode = "ACC-A", AccountType = "hospital", Status = "active" };
        accounts.Items.Add(a);
        var handler = new ImportAccountRelationshipsHandler(
            Tenant(TenantA), accounts, new FakeRelRepo(), new FakeValidator(), new FakeMetadataReader(), new NoopContactAudit());
        var cmd = new ImportAccountRelationshipsCommand(DryRun: true, new[]
        {
            new AccountRelationshipImportRow("ACC-A", null, "ACC-A", null, "refers-to", "active", null, null, null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.InvalidRows);
        Assert.Contains(res.Data.Errors, e => e.Message.Contains("self-relationship", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RelationshipImport_Bidirectional_ReversePair_Is_Conflict()
    {
        var accounts = new FakeAccountRepo();
        var a = new Account { TenantId = TenantA, AccountName = "A", AccountCode = "ACC-A", AccountType = "hospital", Status = "active" };
        var b = new Account { TenantId = TenantA, AccountName = "B", AccountCode = "ACC-B", AccountType = "pharmacy", Status = "active" };
        accounts.Items.Add(a);
        accounts.Items.Add(b);
        var rels = new FakeRelRepo();
        rels.Items.Add(new DomainRel { TenantId = TenantA, SourceAccountId = b.Id, TargetAccountId = a.Id, RelationshipType = "same-network", Direction = "bidirectional", Status = "active" });

        var handler = new ImportAccountRelationshipsHandler(
            Tenant(TenantA), accounts, rels, new FakeValidator(), new FakeMetadataReader(), new NoopContactAudit());
        var cmd = new ImportAccountRelationshipsCommand(DryRun: false, new[]
        {
            new AccountRelationshipImportRow("ACC-A", null, "ACC-B", null, "same-network", "active", null, null, null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.ConflictRows);
        Assert.Single(rels.Items); // no new row inserted
    }

    [Fact]
    public async Task RelationshipImport_Actual_Creates_With_Derived_Direction()
    {
        var accounts = new FakeAccountRepo();
        var a = new Account { TenantId = TenantA, AccountName = "A", AccountCode = "ACC-A", AccountType = "hospital", Status = "active" };
        var b = new Account { TenantId = TenantA, AccountName = "B", AccountCode = "ACC-B", AccountType = "pharmacy", Status = "active" };
        accounts.Items.Add(a);
        accounts.Items.Add(b);
        var rels = new FakeRelRepo();
        var handler = new ImportAccountRelationshipsHandler(
            Tenant(TenantA), accounts, rels, new FakeValidator(), new FakeMetadataReader(), new NoopContactAudit());
        var cmd = new ImportAccountRelationshipsCommand(DryRun: false, new[]
        {
            new AccountRelationshipImportRow(null, a.Id, null, b.Id, "same-network", "active", null, null, null)
        });

        var res = await handler.Handle(cmd, default);

        Assert.Equal(1, res.Data!.CreatedRows);
        Assert.Single(rels.Items);
        Assert.Equal("bidirectional", rels.Items[0].Direction);
    }

    // ---------------- fakes ----------------

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));
        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.AccountCode == code && !a.IsDeleted));
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Account>)new List<Account>(), 0L, 0L));
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<Account>)new List<Account>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(Account a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task UpdateAsync(Account a, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = new();
        public Task<Contact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));
    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Contact>)new List<Contact>(), 0L, 0L));
        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Contact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList());
        public Task InsertAsync(Contact c, CancellationToken ct) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Contact c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRefRepo : IContactExternalReferenceRepository
    {
        public List<ContactExternalReference> Items { get; } = new();
        public Task<bool> ExistsBySourceExternalAsync(Guid t, string source, string externalId, Guid? ex, CancellationToken ct)
            => Task.FromResult(Items.Any(r => r.TenantId == t && !r.IsDeleted && r.SourceSystem == source && r.ExternalId == externalId && r.Id != ex));
        public Task<ContactExternalReference?> GetBySourceExternalAsync(Guid t, string source, string externalId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && !r.IsDeleted && r.SourceSystem == source && r.ExternalId == externalId));
        public Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid t, Guid contactId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.Where(r => r.TenantId == t && r.ContactId == contactId).ToList());
        public Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.Where(r => r.TenantId == t && !r.IsDeleted).ToList());
        public Task InsertAsync(ContactExternalReference reference, CancellationToken ct) { Items.Add(reference); return Task.CompletedTask; }
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == t && l.Id == id && !l.IsDeleted));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(Items.Any(l => l.TenantId == t && !l.IsDeleted && l.AccountId == a && l.ContactId == c && l.RoleCode == r && l.Id != ex));
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(Items.Any(l => l.TenantId == t && !l.IsDeleted && l.AccountId == a && l.RoleCode == r && l.IsPrimary && l.Id != ex));
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.AccountId == a).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.ContactId == c).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted).ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) { Items.Add(l); return Task.CompletedTask; }
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRelRepo : IAccountRelationshipRepository
    {
        public List<DomainRel> Items { get; } = new();
        public Task<DomainRel?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));
        public Task<bool> ExistsActivePairAsync(Guid t, Guid s, Guid tg, string type, bool includeReverse, Guid? ex, CancellationToken ct)
            => Task.FromResult(Items.Any(r => r.TenantId == t && !r.IsDeleted && r.RelationshipType == type && r.Id != ex
                && ((r.SourceAccountId == s && r.TargetAccountId == tg) || (includeReverse && r.SourceAccountId == tg && r.TargetAccountId == s))));
        public Task<IReadOnlyList<DomainRel>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainRel>)Items.Where(r => r.TenantId == t && !r.IsDeleted && (r.SourceAccountId == a || r.TargetAccountId == a)).ToList());
        public Task<IReadOnlyList<DomainRel>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainRel>)Items.Where(r => r.TenantId == t && !r.IsDeleted).ToList());
        public Task InsertAsync(DomainRel r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
        public Task UpdateAsync(DomainRel r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeValidator : IReferenceDataValidator
    {
        public ReferenceValidationStatus Status { get; set; } = ReferenceValidationStatus.Valid;
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(Status, setCode, value));
    }

    private sealed class FakeMetadataReader : IReferenceMetadataReader
    {
        private readonly Dictionary<string, Dictionary<string, string>> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["same-network"] = new() { ["direction"] = "bidirectional", ["inverseLabelCode"] = "same-network", ["selfAllowed"] = "false" },
            ["refers-to"] = new() { ["direction"] = "directional", ["inverseLabelCode"] = "referred-by", ["selfAllowed"] = "false" },
        };
        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(_map.TryGetValue(value, out var m) ? (IReadOnlyDictionary<string, string>?)m : null);
    }

    private sealed class NoopContactAudit : IContactAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, Guid c, string? d, CancellationToken ct) => Task.CompletedTask;
    }
}
