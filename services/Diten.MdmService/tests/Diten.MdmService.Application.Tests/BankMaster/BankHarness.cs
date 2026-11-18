using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.RecordCodes;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>
/// MOD-0289 DL-1 — the one place the bank tests build their store: the test-owned replica-set mongod
/// (<see cref="MdmTestMongo"/>, never the shared server), ONE fixed database; every test isolates by its own random tenant.
/// The handlers are the production ones over the production repositories; only the code service is the test's own fake.
/// </summary>
internal sealed class BankHarness
{
    public const string DatabaseName = "diten_mdm_mod0289_dl1_itest";

    private static readonly Lazy<IMongoDatabase> SharedDatabase = new(() => Open(null));

    public static IMongoDatabase Database => SharedDatabase.Value;

    public static IMongoDatabase Open(string? applicationName)
    {
        var settings = MongoClientSettings.FromConnectionString(MdmTestMongo.ConnectionString);
#pragma warning disable CS0618 // the production Guid representation
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        if (applicationName is not null) settings.ApplicationName = applicationName;
        return new MongoClient(settings).GetDatabase(DatabaseName);
    }

    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid Actor { get; } = Guid.NewGuid();
    public Guid Correlation { get; } = Guid.NewGuid();
    public TenantContext Tenant { get; } = new();
    public FakeRecordCodeAllocator Codes { get; } = new();
    public IBankRepository Banks { get; }
    public IBankBranchRepository Branches { get; }

    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public BankHarness(Guid? tenantId = null, IMongoDatabase? database = null, TimeProvider? clock = null)
    {
        if (tenantId is { } fixedTenant) TenantId = fixedTenant;
        Tenant.SetTenant(TenantId);
        _correlation = new FixedCorrelation(Correlation);
        _clock = clock ?? TimeProvider.System;
        var db = database ?? Database;
        Banks = new BankRepository(db, Tenant);
        Branches = new BankBranchRepository(db, Tenant);
    }

    /// <summary>A second tenant over the same store — the foreign caller of the isolation tests.</summary>
    public BankHarness Foreign() => new();

    public IMongoCollection<Bank> BankCollection => Database.GetCollection<Bank>(BankRepository.CollectionName);
    public IMongoCollection<BankBranch> BranchCollection => Database.GetCollection<BankBranch>(BankBranchRepository.CollectionName);

    public CreateBankHandler CreateBank(IRecordCodeAllocator? allocator) => new(Banks, Tenant, _correlation, allocator, _clock);
    public CreateBankHandler CreateBank() => CreateBank(Codes);
    public UpdateBankHandler UpdateBank() => new(Banks, Tenant, _correlation, _clock);
    public ActivateBankHandler ActivateBank() => new(Banks, Tenant, _correlation, _clock);
    public DeactivateBankHandler DeactivateBank() => new(Banks, Tenant, _correlation, _clock);
    public DeleteBankHandler DeleteBank() => new(Banks, Tenant, _correlation, _clock);
    public BulkDeleteBankHandler BulkDeleteBank() => new(Banks, Tenant, _correlation, _clock);
    public CreateBankBranchHandler CreateBranch() => new(Banks, Branches, Tenant, _correlation, _clock);
    public UpdateBankBranchHandler UpdateBranch() => new(Banks, Branches, Tenant, _correlation, _clock);
    public ActivateBankBranchHandler ActivateBranch() => new(Branches, Tenant, _correlation, _clock);
    public DeactivateBankBranchHandler DeactivateBranch() => new(Branches, Tenant, _correlation, _clock);
    public DeleteBankBranchHandler DeleteBranch() => new(Branches, Tenant, _correlation, _clock);
    public GetBankListHandler List() => new(Banks);
    public GetBankByIdHandler GetBank() => new(Banks);
    public GetBankBranchListHandler ListBranches() => new(Banks, Branches);
    public GetBankBranchByIdHandler GetBranch() => new(Branches);

    public static BankWriteRequest Bank(
        string name = "Test Bank", string country = "TR", string? bic = null, string? national = null, bool? includesBranch = null) =>
        new() { Name = name, CountryCode = country, Bic = bic, NationalBankCode = national, DomesticKeyIncludesBranch = includesBranch };

    public static BankBranchWriteRequest Branch(string code = "001", string name = "Main Branch", string? bic = null, string? city = null) =>
        new() { BranchCode = code, Name = name, Bic = bic, City = city };

    public async Task<BankDetailDto> NewBankAsync(
        string name = "Test Bank", string country = "TR", string? bic = null, string? national = null, bool activate = false)
    {
        var created = await CreateBank().Handle(new CreateBankCommand(Bank(name, country, bic, national), Actor), default);
        Assert.True(created.IsSuccessful, string.Join(",", created.Errors));
        var bank = created.Data!;
        if (!activate) return bank;
        var activated = await ActivateBank().Handle(new ActivateBankCommand(bank.Id, bank.Version, Actor), default);
        Assert.True(activated.IsSuccessful, string.Join(",", activated.Errors));
        return activated.Data!;
    }

    public async Task<BankBranchDto> NewBranchAsync(Guid bankId, string code = "001", string? bic = null, bool activate = false)
    {
        var created = await CreateBranch().Handle(new CreateBankBranchCommand(bankId, Branch(code, bic: bic), Actor), default);
        Assert.True(created.IsSuccessful, string.Join(",", created.Errors));
        var branch = created.Data!;
        if (!activate) return branch;
        var activated = await ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, branch.Version, Actor), default);
        Assert.True(activated.IsSuccessful, string.Join(",", activated.Errors));
        return activated.Data!;
    }

    public async Task<Bank> StoredBankAsync(Guid id) =>
        await BankCollection.Find(x => x.Id == id && x.TenantId == TenantId).SingleAsync();

    public async Task<BankBranch> StoredBranchAsync(Guid id) =>
        await BranchCollection.Find(x => x.Id == id && x.TenantId == TenantId).SingleAsync();

    /// <summary>Makes the audit-intent temporal cutover active in the test database (delivery refuses to run before it).</summary>
    public static async Task EnsureCutoverAsync()
    {
        var states = Database.GetCollection<AuditIntentTemporalMigrationState>(AuditIntentTemporalMigrationRepository.StateCollectionName);
        if (await states.CountDocumentsAsync(FilterDefinition<AuditIntentTemporalMigrationState>.Empty) > 0) return;
        var now = DateTimeOffset.UtcNow;
        try
        {
            await states.InsertOneAsync(new AuditIntentTemporalMigrationState
            {
                Phase = AuditIntentTemporalMigrationState.Phases.CutoverActive,
                CollectionOrdinal = 8,
                CollectionBoundaries = Enumerable.Range(0, 8).Select(index => new AuditIntentTemporalCollectionBoundary
                {
                    CollectionOrdinal = index,
                    UpperAggregateId = Guid.Empty
                }).ToList(),
                UpdatedAtUtcTicks = now.UtcTicks,
                CompletedAtUtcTicks = now.UtcTicks,
                ActivationVersion = AuditIntentTemporalStorage.CurrentVersion,
                IndexEvidenceFingerprint = AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint
            });
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // another test class made it first — the state is a singleton
        }
    }

    private sealed record FixedCorrelation(Guid CorrelationId) : IRequestCorrelation;
}

/// <summary>The test's own code service: a counter per instance, every call recorded, the answer replaceable.</summary>
internal sealed class FakeRecordCodeAllocator : IRecordCodeAllocator
{
    private long _sequence;

    public sealed record Call(RecordCodePrefixScope Scope, Guid? LegalEntityId, string RecordType, string IdempotencyKey);

    public List<Call> Calls { get; } = [];

    /// <summary>When set, answers instead of allocating.</summary>
    public Func<RecordCodeAllocationResult>? Answer { get; set; }

    public Task<RecordCodeAllocationResult> AllocateAsync(
        RecordCodePrefixScope prefixScope, Guid? legalEntityId, string recordType, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new Call(prefixScope, legalEntityId, recordType, idempotencyKey));
        if (Answer is { } answer) return Task.FromResult(answer());
        var number = Interlocked.Increment(ref _sequence);
        return Task.FromResult(RecordCodeAllocationResult.Allocated($"GMG-BNK-{number:D6}", number));
    }
}
