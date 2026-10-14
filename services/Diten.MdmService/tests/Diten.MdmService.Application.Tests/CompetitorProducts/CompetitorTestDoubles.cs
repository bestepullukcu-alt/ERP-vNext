using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Application.Features.CompetitorProducts;
using Diten.MdmService.Application.Tests.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Eventing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Tests.CompetitorProducts;

// WP-VW-W4 (W4-BE-b2) — in-memory repositories with the SAME contract as the Mongo ones (explicit tenant, CAS, a stored copy
// — never the caller's instance), a fake market catalog / BRD set, and one harness wiring the REAL service, the REAL b1
// flow / applier / consumer and the competitor approval target.

internal static class Copy
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = false };

    public static T Of<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}

public sealed class InMemoryCompetitorRepository : ICompetitorProductRepository
{
    private readonly Dictionary<Guid, CompetitorProduct> _rows = new();

    public IReadOnlyCollection<CompetitorProduct> Raw => _rows.Values;

    public Task<CompetitorProduct?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(_rows.TryGetValue(id, out var r) && r.TenantId == tenantId && tenantId != Guid.Empty ? Copy.Of(r) : null);

    public Task<IReadOnlyList<CompetitorProduct>> ListAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CompetitorProduct>>(_rows.Values.Where(r => r.TenantId == tenantId && tenantId != Guid.Empty).Select(Copy.Of).ToList());

    public Task InsertAsync(CompetitorProduct record, CancellationToken ct)
    {
        _rows.Add(record.Id, Copy.Of(record));
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(CompetitorProduct record, int expectedVersion, CancellationToken ct)
    {
        if (!_rows.TryGetValue(record.Id, out var stored) || stored.TenantId != record.TenantId || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        record.Version = expectedVersion + 1;
        _rows[record.Id] = Copy.Of(record);
        return Task.FromResult(true);
    }
}

public sealed class InMemoryDecisionRepository : ICompetitorProposalDecisionRepository, IWorkflowApprovalStore<CompetitorProposalDecision>
{
    private readonly Dictionary<Guid, CompetitorProposalDecision> _rows = new();

    public Task<CompetitorProposalDecision?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(_rows.TryGetValue(id, out var r) && r.TenantId == tenantId ? Copy.Of(r) : null);

    public Task<CompetitorProposalDecision?> GetByProposalAsync(Guid tenantId, Guid proposalId, CancellationToken ct)
        => Task.FromResult(_rows.Values.Where(r => r.TenantId == tenantId && r.ProposalId == proposalId).OrderByDescending(r => r.CreatedAt)
            .Select(Copy.Of).FirstOrDefault());

    public Task<IReadOnlyList<CompetitorProposalDecision>> ListByProposalsAsync(Guid tenantId, IReadOnlyCollection<Guid> proposalIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CompetitorProposalDecision>>(_rows.Values.Where(r => r.TenantId == tenantId && proposalIds.Contains(r.ProposalId))
            .Select(Copy.Of).ToList());

    public Task InsertAsync(CompetitorProposalDecision record, CancellationToken ct)
    {
        _rows.Add(record.Id, Copy.Of(record));
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(CompetitorProposalDecision record, int expectedVersion, CancellationToken ct)
    {
        if (!_rows.TryGetValue(record.Id, out var stored) || stored.TenantId != record.TenantId || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        record.Version = expectedVersion + 1;
        _rows[record.Id] = Copy.Of(record);
        return Task.FromResult(true);
    }
}

public sealed class FakeMarkets : IVerifiedMarketReferenceResolver
{
    public bool Down { get; set; }
    public HashSet<string> Known { get; } = ["TR", "AE", "AZ"];

    public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(string marketCode, CancellationToken cancellationToken = default)
        => Task.FromResult(Down
            ? VerifiedMarketReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
            : Known.Contains(marketCode)
                ? VerifiedMarketReferenceResolveResult.Success(new VerifiedMarketReferenceSelection("market", marketCode, Guid.NewGuid(), 1, "latest", DateTimeOffset.UtcNow))
                : VerifiedMarketReferenceResolveResult.Fail(404, "MARKET_NOT_FOUND"));
}

public sealed class FakeReferenceSets : IMdmReferenceValueReader
{
    public bool Down { get; set; }

    public Task<IReadOnlySet<string>?> ActiveCodesAsync(string setCode, CancellationToken ct)
        => Task.FromResult<IReadOnlySet<string>?>(Down || setCode != CompetitorReferenceSets.ReimbursementStatus
            ? null
            : new HashSet<string> { "reimbursed", "reimbursed_conditional", "not_reimbursed", "unknown" });
}

public sealed class FakePermissions : IMdmPermissionContext
{
    public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Has(string permission) => Keys.Contains(permission);
}

public sealed class FixedSettings : IMdmApprovalSettings
{
    public string CompetitorProposalTemplateCode => "MDM-COMP-PROPOSAL";
    public int ReconcileAfterSeconds => 120;
}

/// <summary>The real service + the real b1 flow / applier / consumer, over the in-memory stores.</summary>
public sealed class CompetitorHarness
{
    public static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    public static readonly Guid OtherTenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly Guid Tutukon = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid Uroflex = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    public InMemoryCompetitorRepository Products { get; } = new();
    public InMemoryDecisionRepository Decisions { get; } = new();
    public FakeWorkflowClient Workflow { get; } = new();
    public FixedActor Actor { get; } = new("u-manager");
    public FakePermissions Permissions { get; } = new();
    public FakeMarkets Markets { get; } = new();
    public FakeReferenceSets ReferenceSets { get; } = new();
    public TenantContext TenantContext { get; } = new();
    public WorkflowApprovalOutcomeApplier Applier { get; }
    public MdmWorkflowOutcomeConsumer Consumer { get; }
    public CompetitorProductService Service { get; }

    public CompetitorHarness()
    {
        TenantContext.SetTenant(Tenant);
        Permissions.Keys.UnionWith(CompetitorPermissions.All);
        Applier = new WorkflowApprovalOutcomeApplier([CompetitorProposalApproval.Target(Decisions, Products)]);
        var flow = new WorkflowApprovalFlow(Workflow, Actor, Applier);
        Consumer = new MdmWorkflowOutcomeConsumer(Applier, new InMemoryInbox(), NullLogger<MdmWorkflowOutcomeConsumer>.Instance);
        Service = new CompetitorProductService(Products, Decisions, Decisions, TenantContext, Actor, Permissions, Markets, ReferenceSets,
            flow, new WorkflowApprovalReconciler(Workflow, Applier, new FixedSettings()), new FixedSettings());
    }

    /// <summary>Acts as <paramref name="actor"/> holding only <paramref name="keys"/> (a rep: read + propose).</summary>
    public void As(string actor, params string[] keys)
    {
        Actor.ActorName = actor;
        Permissions.Keys.Clear();
        Permissions.Keys.UnionWith(keys);
    }

    public static CompetitorProductWriteRequest Write(string name, string company, params CompetitorCountryPriceRequest[] prices)
        => new(name, null, company, ["trimetazidine"], "modified-release-tablet", "35 mg", "C01EB15", [Tutukon], null, prices);

    public static CompetitorCountryPriceRequest Price(string market, decimal price, string currency = "TRY", string reimbursement = "reimbursed",
        string from = "2026-01-01", string? source = null)
        => new(market, price, currency, reimbursement, DateTime.Parse(from), source);

    public async Task<Guid> CreateAsync(string name, string company, params CompetitorCountryPriceRequest[] prices)
    {
        var result = await Service.CreateAsync(Write(name, company, prices), default);
        if (!result.IsSuccessful) throw new InvalidOperationException(string.Join(";", result.Errors));
        return result.Data;
    }

    public async Task<WorkflowApplyResult?> OutcomeAsync(Guid decisionId, Guid instanceId, string outcome, string? completedBy)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = Tenant, workflowInstanceId = instanceId, objectType = "mdm.competitor-proposal", objectId = decisionId.ToString("D"),
            outcome, completedAt = DateTimeOffset.UtcNow, completedBy, reasonCode = (string?)null
        });
        return await Consumer.ConsumeAsync(new Diten.Platform.Application.Contracts.Eventing.EventTransportMessage(Guid.NewGuid(),
            MdmWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(), null, Tenant, "platform", DateTimeOffset.UtcNow, payload));
    }
}
