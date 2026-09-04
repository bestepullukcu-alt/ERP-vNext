using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship.Commands;
using Diten.CrmService.Application.Features.AccountRelationship.Handlers;
using Diten.CrmService.Application.Features.AccountRelationship.Queries;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;

namespace Diten.CrmService.Application.Tests;

public sealed class AccountRelationshipTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeAccountRepo Accounts { get; } = new();
        public FakeRelRepo Rels { get; } = new();
        public FakeValidator Validator { get; } = new();
        public FakeMetadataReader Metadata { get; } = new();
        public Account A { get; }
        public Account B { get; }

        public Fixture(Guid tenant)
        {
            A = new Account { TenantId = tenant, AccountName = "Hospital", AccountCode = "ACC-A", AccountType = "hospital", Status = "active" };
            B = new Account { TenantId = tenant, AccountName = "Pharmacy", AccountCode = "ACC-B", AccountType = "pharmacy", Status = "active" };
            Accounts.Items.Add(A);
            Accounts.Items.Add(B);
        }

        public CreateAccountRelationshipHandler Create(Guid tenant) =>
            new(Tenant(tenant), Accounts, Rels, Validator, Metadata, new NoopAudit());
    }

    private static CreateAccountRelationshipCommand Cmd(Guid src, Guid tgt, string type = "refers-to", string status = "active") =>
        new(src, tgt, type, status, null, null, "note");

    [Fact]
    public async Task Create_Directional_Success_And_DirectionDerived()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("outbound", f.Rels.Items.Single().Direction);
    }

    [Fact]
    public async Task Create_Bidirectional_DirectionIsBidirectional()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "same-network"), default);
        Assert.Equal("bidirectional", f.Rels.Items.Single().Direction);
    }

    [Fact]
    public async Task Create_Missing_Source_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create(TenantA).Handle(Cmd(Guid.NewGuid(), f.B.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Create_Missing_Target_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Create_SoftDeleted_Target_Returns_404()
    {
        var f = new Fixture(TenantA);
        f.B.IsDeleted = true;
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Create_Invalid_Type_Returns_400()
    {
        var f = new Fixture(TenantA);
        f.Validator.TypeStatus = ReferenceValidationStatus.InvalidValue;
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "not-real"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Invalid_Status_Returns_400()
    {
        var f = new Fixture(TenantA);
        f.Validator.StatusStatus = ReferenceValidationStatus.InvalidValue;
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, status: "not-real"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Missing_Set_Returns_400()
    {
        var f = new Fixture(TenantA);
        f.Validator.TypeStatus = ReferenceValidationStatus.SetMissing;
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_SelfLink_Default_Forbidden_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.A.Id, "refers-to"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_SelfLink_Allowed_When_Metadata_SelfAllowed()
    {
        var f = new Fixture(TenantA);
        f.Metadata.Set("self-ok", direction: "directional", inverse: "self-ok", selfAllowed: true);
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.A.Id, "self-ok"), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Create_Duplicate_Directional_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        var r = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Create_Bidirectional_Reverse_Duplicate_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "same-network"), default);
        var r = await f.Create(TenantA).Handle(Cmd(f.B.Id, f.A.Id, "same-network"), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Create_Directional_Reverse_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        var r = await f.Create(TenantA).Handle(Cmd(f.B.Id, f.A.Id, "refers-to"), default);
        Assert.True(r.IsSuccessful); // directional: reverse is a distinct relationship
    }

    [Fact]
    public async Task List_For_Source_Shows_Direct()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "served-by"), default);
        var h = new ListRelationshipsForAccountHandler(Tenant(TenantA), f.Accounts, f.Rels, f.Metadata);
        var r = await h.Handle(new ListRelationshipsForAccountQuery(f.A.Id), default);
        var row = Assert.Single(r.Data!);
        Assert.Equal("direct", row.DisplayDirection);
        Assert.Equal("served-by", row.EffectiveLabelCode);
        Assert.Equal(f.B.Id, row.RelatedAccountId);
    }

    [Fact]
    public async Task List_For_Target_Shows_Inverse()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "served-by"), default);
        var h = new ListRelationshipsForAccountHandler(Tenant(TenantA), f.Accounts, f.Rels, f.Metadata);
        var r = await h.Handle(new ListRelationshipsForAccountQuery(f.B.Id), default);
        var row = Assert.Single(r.Data!);
        Assert.Equal("inverse", row.DisplayDirection);
        Assert.Equal("serves", row.EffectiveLabelCode); // inverseLabelCode of served-by
        Assert.Equal(f.A.Id, row.RelatedAccountId);
    }

    [Fact]
    public async Task Delete_SoftDeletes_And_List_Excludes()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id), default);
        var id = f.Rels.Items[0].Id;
        var del = new DeleteAccountRelationshipHandler(Tenant(TenantA), f.Rels, new NoopAudit());
        await del.Handle(new DeleteAccountRelationshipCommand(f.A.Id, id), default);
        Assert.True(f.Rels.Items[0].IsDeleted);
        var h = new ListRelationshipsForAccountHandler(Tenant(TenantA), f.Accounts, f.Rels, f.Metadata);
        var r = await h.Handle(new ListRelationshipsForAccountQuery(f.A.Id), default);
        Assert.Empty(r.Data!);
    }

    [Fact]
    public async Task Get_CrossTenant_Returns_404()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id), default);
        var id = f.Rels.Items[0].Id;
        var h = new GetAccountRelationshipByIdHandler(Tenant(TenantB), f.Rels);
        var r = await h.Handle(new GetAccountRelationshipByIdQuery(f.A.Id, id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task End_Relationship_Then_Recreate_Same_Pair_Allowed_History_Preserved()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        var relId = f.Rels.Items[0].Id;

        // End the relationship (historical lifecycle): Status=ended + ValidTo, NOT deleted.
        var upd = new UpdateAccountRelationshipHandler(Tenant(TenantA), f.Rels, f.Validator, f.Metadata, new NoopAudit());
        var er = await upd.Handle(new UpdateAccountRelationshipCommand(f.A.Id, relId, "refers-to", "ended", null, DateTimeOffset.UtcNow, "affiliation changed"), default);
        Assert.True(er.IsSuccessful);
        Assert.Equal("ended", f.Rels.Items[0].Status);
        Assert.False(f.Rels.Items[0].IsDeleted); // history preserved

        // Recreate the SAME pair/type → allowed because the old one is ended.
        var rr = await f.Create(TenantA).Handle(Cmd(f.A.Id, f.B.Id, "refers-to"), default);
        Assert.Equal(201, rr.StatusCode);
        Assert.Equal(2, f.Rels.Items.Count);
    }

    [Fact]
    public async Task Create_ValidFrom_After_ValidTo_Returns_400()
    {
        var f = new Fixture(TenantA);
        var cmd = new CreateAccountRelationshipCommand(f.A.Id, f.B.Id, "refers-to", "active", DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow, "n");
        var r = await f.Create(TenantA).Handle(cmd, default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---- fakes ----

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

    private sealed class FakeRelRepo : IAccountRelationshipRepository
    {
        public List<DomainRel> Items { get; } = new();
        public Task<DomainRel?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));
        public Task<IReadOnlyList<DomainRel>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainRel>)Items.Where(r => r.TenantId == t && !r.IsDeleted).ToList());
        public Task<bool> ExistsActivePairAsync(Guid t, Guid s, Guid tg, string type, bool includeReverse, Guid? ex, CancellationToken ct)
            => Task.FromResult(Items.Any(r => r.TenantId == t && !r.IsDeleted && !RelationshipLifecycle.IsClosed(r.Status) && r.RelationshipType == type && r.Id != ex
                && ((r.SourceAccountId == s && r.TargetAccountId == tg) || (includeReverse && r.SourceAccountId == tg && r.TargetAccountId == s))));
        public Task<IReadOnlyList<DomainRel>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainRel>)Items.Where(r => r.TenantId == t && !r.IsDeleted && (r.SourceAccountId == a || r.TargetAccountId == a)).ToList());
        public Task InsertAsync(DomainRel r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
        public Task UpdateAsync(DomainRel r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeValidator : IReferenceDataValidator
    {
        public ReferenceValidationStatus TypeStatus { get; set; } = ReferenceValidationStatus.Valid;
        public ReferenceValidationStatus StatusStatus { get; set; } = ReferenceValidationStatus.Valid;
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
        {
            var s = setCode.Contains("status") ? StatusStatus : TypeStatus;
            return Task.FromResult(new ReferenceValidationResult(s, setCode, value));
        }
    }

    private sealed class FakeMetadataReader : IReferenceMetadataReader
    {
        private readonly Dictionary<string, Dictionary<string, string>> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["associated-with"] = new() { ["direction"] = "bidirectional", ["inverseLabelCode"] = "associated-with", ["selfAllowed"] = "false" },
            ["same-network"] = new() { ["direction"] = "bidirectional", ["inverseLabelCode"] = "same-network", ["selfAllowed"] = "false" },
            ["nearby"] = new() { ["direction"] = "bidirectional", ["inverseLabelCode"] = "nearby", ["selfAllowed"] = "false" },
            ["refers-to"] = new() { ["direction"] = "directional", ["inverseLabelCode"] = "referred-by", ["selfAllowed"] = "false" },
            ["served-by"] = new() { ["direction"] = "directional", ["inverseLabelCode"] = "serves", ["selfAllowed"] = "false" },
            ["preferred-pharmacy"] = new() { ["direction"] = "directional", ["inverseLabelCode"] = "preferred-by", ["selfAllowed"] = "false" },
        };

        public void Set(string type, string direction, string inverse, bool selfAllowed)
            => _map[type] = new() { ["direction"] = direction, ["inverseLabelCode"] = inverse, ["selfAllowed"] = selfAllowed ? "true" : "false" };

        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(_map.TryGetValue(value, out var m) ? (IReadOnlyDictionary<string, string>?)m : null);
    }

    private sealed class NoopAudit : IContactAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, Guid c, string? d, CancellationToken ct) => Task.CompletedTask;
    }
}
