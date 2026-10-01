using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests;

// WP-CL-BE-4 — the direct approve commands/handlers are gone from production: a claim or a country version is approved
// ONLY by a MOD-0023 round outcome (ClaimReviewOutcomeApplier). Older tests that need an approved record reach it the
// production way through these TEST-ONLY shims: open a round (as submit-review would), then apply "approved" through
// the real applier. These records are not production commands.

internal sealed record ApproveClaimCommand(Guid ClaimId);

internal sealed record ApproveClaimCountryVersionCommand(Guid CountryVersionId);

/// <summary>Approves a claim through the production outcome applier (round opened, then "approved" applied).</summary>
internal sealed class WorkflowApproveClaimShim(
    ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository versions,
    IContentCompositionAuditPublisher? audit)
{
    public async Task<Response<bool>> Handle(ApproveClaimCommand command, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId || await claims.GetByIdAsync(tenantId, command.ClaimId, ct) is not { } claim)
        {
            return Response<bool>.Fail("Claim not found.", 404);
        }

        if (claim.IsApproved())
        {
            return Response<bool>.Success(true);
        }

        var instanceId = Guid.NewGuid();
        claim.ReviewRounds.Add(new ClaimReviewRound
        {
            WorkflowInstanceId = instanceId, RoundNo = ClaimReviewRules.NextRoundNo(claim.ReviewRounds),
            SubmittedAt = DateTimeOffset.UtcNow
        });
        claim.Status = ClaimStatuses.InReview;
        var result = await new ClaimReviewOutcomeApplier(claims, versions, audit).ApplyAsync(tenantId,
            ClaimReviewRules.ClaimObjectType, claim.Id, instanceId, ClaimReviewOutcomes.Approved, "approver-1", "APPROVED",
            DateTimeOffset.UtcNow, ct);
        return result == ClaimReviewApplyResult.Applied
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(result.ToString(), 409);
    }
}

/// <summary>Approves a country version through the production outcome applier.</summary>
internal sealed class WorkflowApproveCountryVersionShim(
    ITenantContext tenant, IClaimRepository claims, IClaimCountryVersionRepository versions,
    IContentCompositionAuditPublisher? audit)
{
    public async Task<Response<bool>> Handle(ApproveClaimCountryVersionCommand command, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId
            || await versions.GetByIdAsync(tenantId, command.CountryVersionId, ct) is not { } version)
        {
            return Response<bool>.Fail("Country version not found.", 404);
        }

        if (version.Status == ClaimStatuses.Approved)
        {
            return Response<bool>.Success(true);
        }

        var instanceId = Guid.NewGuid();
        version.ReviewRounds.Add(new ClaimReviewRound
        {
            WorkflowInstanceId = instanceId, RoundNo = ClaimReviewRules.NextRoundNo(version.ReviewRounds),
            SubmittedAt = DateTimeOffset.UtcNow
        });
        version.Status = ClaimStatuses.InReview;
        var result = await new ClaimReviewOutcomeApplier(claims, versions, audit).ApplyAsync(tenantId,
            ClaimReviewRules.CountryVersionObjectType, version.Id, instanceId, ClaimReviewOutcomes.Approved, "approver-1",
            "APPROVED", DateTimeOffset.UtcNow, ct);
        return result == ClaimReviewApplyResult.Applied
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(result.ToString(), 409);
    }
}

/// <summary>A country-version repository with nothing in it (for claim-only fixtures).</summary>
internal sealed class EmptyCountryVersionRepo : IClaimCountryVersionRepository
{
    public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<ClaimCountryVersion?>(null);
    public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct) => Empty();
    public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct) => Empty();
    public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct) => Empty();
    public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;

    private static Task<IReadOnlyList<ClaimCountryVersion>> Empty() =>
        Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Array.Empty<ClaimCountryVersion>());
}

/// <summary>Scriptable MOD-0023 fake: records every start / cancel, answers by-objects and the task list.</summary>
internal sealed class FakeClaimWorkflowClient : IClaimWorkflowClient
{
    private readonly Dictionary<Guid, (string ObjectType, string ObjectId)> _owners = new();

    public List<ClaimWorkflowStartRequest> Starts { get; } = new();
    public List<(Guid TaskId, string Actor, string Reason, string Key)> Cancels { get; } = new();
    public List<(string ObjectType, IReadOnlyList<string> Ids)> ByObjectsCalls { get; } = new();
    public ClaimWorkflowCallOutcome StartOutcome { get; set; } = ClaimWorkflowCallOutcome.Ok;
    public ClaimWorkflowCallOutcome CancelOutcome { get; set; } = ClaimWorkflowCallOutcome.Ok;
    public bool Unreachable { get; set; }
    public bool Throws { get; set; }
    public Dictionary<Guid, ClaimWorkflowInstanceState> Instances { get; } = new();
    public List<ClaimWorkflowTaskState> Tasks { get; } = new();
    public Guid LastInstanceId { get; private set; }

    public Task<ClaimWorkflowStartResult> StartAsync(ClaimWorkflowStartRequest request, CancellationToken ct)
    {
        Starts.Add(request);
        if (StartOutcome != ClaimWorkflowCallOutcome.Ok)
        {
            return Task.FromResult(new ClaimWorkflowStartResult(StartOutcome, null, "fake refusal"));
        }

        LastInstanceId = Guid.NewGuid();
        _owners[LastInstanceId] = (request.ObjectType, request.ObjectId);
        Instances[LastInstanceId] = new ClaimWorkflowInstanceState(LastInstanceId, "Active", null, null);
        Tasks.Add(new ClaimWorkflowTaskState(Guid.NewGuid(), LastInstanceId, "stage-1", "step-1", "WaitingApproval",
            "approver-1", null, null, null, null));
        return Task.FromResult(new ClaimWorkflowStartResult(ClaimWorkflowCallOutcome.Ok, LastInstanceId, null));
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>?> GetInstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken ct)
    {
        ByObjectsCalls.Add((objectType, objectIds));
        if (Throws)
        {
            throw new HttpRequestException("platform down");
        }

        if (Unreachable)
        {
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>?>(null);
        }

        IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>> byObject = objectIds.ToDictionary(
            id => id,
            id => (IReadOnlyList<ClaimWorkflowInstanceState>)Instances.Values
                .Where(i => _owners.TryGetValue(i.WorkflowInstanceId, out var o) && o.ObjectType == objectType
                            && string.Equals(o.ObjectId, id, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>?>(byObject);
    }

    public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetTasksAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>(Unreachable
            ? null
            : Tasks.Where(t => ids.Contains(t.WorkflowInstanceId)).ToList());

    public Task<ClaimWorkflowCallOutcome> CancelTaskAsync(Guid taskId, string actorId, string reasonCode,
        string idempotencyKey, CancellationToken ct)
    {
        Cancels.Add((taskId, actorId, reasonCode, idempotencyKey));
        return Task.FromResult(Unreachable ? ClaimWorkflowCallOutcome.Unavailable : CancelOutcome);
    }

    /// <summary>Marks an instance terminal in the fake (what MOD-0023 reports after a decision).</summary>
    public void Complete(Guid instanceId, string outcome) =>
        Instances[instanceId] = new ClaimWorkflowInstanceState(instanceId, "Completed", outcome, DateTimeOffset.UtcNow);
}

/// <summary>
/// WP-CL-BE-5 — MOD-0031 faked at the <see cref="IClaimEvidenceClient"/> seam. Mirrors the Platform read semantics
/// (object match; version filter only when given; active unless includeRemoved) and the computed document state per
/// document. <see cref="AnyObjectHasEvidence"/> answers every queried object with one active link (for older tests
/// that only need the submit rule satisfied).
/// </summary>
internal sealed class FakeClaimEvidenceClient : IClaimEvidenceClient
{
    public List<ClaimEvidenceLink> Links { get; } = new();
    public Dictionary<Guid, (bool Superseded, string State, DateTimeOffset? Due)> Documents { get; } = new();
    public bool AnyObjectHasEvidence { get; set; }
    public bool Unavailable { get; set; }
    public (int Status, string Reason)? RejectLinks { get; set; }
    public int QueryCalls { get; private set; }
    public List<ClaimEvidenceObjectRef> LinkedRefs { get; } = new();

    public ClaimEvidenceLink Seed(ClaimEvidenceObjectRef objectRef, Guid? documentId = null, string status = "active")
    {
        var link = New(objectRef, new ClaimEvidenceLinkInput("controlled", documentId ?? Guid.NewGuid(), Guid.NewGuid(),
            "smpc-pil", new ClaimEvidenceLocator("4.1", "4", null, "quote"), [])) with { Status = status };
        Links.Add(link);
        return link;
    }

    public Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> LinkAsync(
        ClaimEvidenceObjectRef objectRef, ClaimEvidenceLinkInput input, CancellationToken ct)
    {
        if (Unavailable)
        {
            return Task.FromResult(Fail(ClaimEvidenceCallOutcome.Unavailable, 503, null));
        }

        if (RejectLinks is { } reject)
        {
            return Task.FromResult(Fail(reject.Status == 403 ? ClaimEvidenceCallOutcome.Forbidden : ClaimEvidenceCallOutcome.Rejected,
                reject.Status, reject.Reason));
        }

        LinkedRefs.Add(objectRef);
        var link = New(objectRef, input);
        Links.Add(link);
        return Task.FromResult(Ok(link, 201));
    }

    public Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> GetAsync(Guid linkId, CancellationToken ct)
    {
        if (Unavailable)
        {
            return Task.FromResult(Fail(ClaimEvidenceCallOutcome.Unavailable, 503, null));
        }

        return Task.FromResult(Links.FirstOrDefault(l => l.LinkId == linkId) is { } link
            ? Ok(State(link), 200)
            : Fail(ClaimEvidenceCallOutcome.NotFound, 404, "link_not_found"));
    }

    public Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> RemoveAsync(Guid linkId, string? reason, CancellationToken ct)
    {
        if (Unavailable)
        {
            return Task.FromResult(Fail(ClaimEvidenceCallOutcome.Unavailable, 503, null));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(Fail(ClaimEvidenceCallOutcome.Rejected, 400, "removal_reason_required"));
        }

        var index = Links.FindIndex(l => l.LinkId == linkId);
        if (index < 0)
        {
            return Task.FromResult(Fail(ClaimEvidenceCallOutcome.NotFound, 404, "link_not_found"));
        }

        Links[index] = Links[index] with { Status = "removed", RemovedAt = DateTimeOffset.UtcNow, RemovalReason = reason };
        return Task.FromResult(Ok(Links[index], 200));
    }

    public Task<IReadOnlyList<ClaimEvidenceLink>?> QueryAsync(
        IReadOnlyList<ClaimEvidenceObjectRef> objects, bool includeRemoved, CancellationToken ct)
    {
        QueryCalls++;
        if (Unavailable)
        {
            return Task.FromResult<IReadOnlyList<ClaimEvidenceLink>?>(null);
        }

        var rows = Links.Where(l => (includeRemoved || l.IsActive) && objects.Any(o => o.Module == l.ObjectRef.Module
                && o.ObjectType == l.ObjectRef.ObjectType
                && string.Equals(o.ObjectId, l.ObjectRef.ObjectId, StringComparison.OrdinalIgnoreCase)
                && (o.ObjectVersion is null || o.ObjectVersion == l.ObjectRef.ObjectVersion)))
            .Select(State).ToList();
        if (AnyObjectHasEvidence)
        {
            rows.AddRange(objects.Select(o => New(o, new ClaimEvidenceLinkInput("controlled", Guid.NewGuid(), Guid.NewGuid(),
                "smpc-pil", new ClaimEvidenceLocator(null, null, null, "q"), []))));
        }

        return Task.FromResult<IReadOnlyList<ClaimEvidenceLink>?>(rows);
    }

    public Task<ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>> GetDocumentOptionsAsync(
        string? search, string? kind, CancellationToken ct)
        => Task.FromResult(Unavailable
            ? new ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>(ClaimEvidenceCallOutcome.Unavailable,
                null, 503, null, null)
            : new ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>(ClaimEvidenceCallOutcome.Ok,
                [new ClaimEvidenceDocumentOptionDto(kind ?? "controlled", Guid.NewGuid(), "SmPC " + search, "DOC-1", "Policy",
                    Guid.NewGuid(), "v2", "Active", null, null, null, null)], 200, null, null));

    private ClaimEvidenceLink State(ClaimEvidenceLink l) => Documents.TryGetValue(l.DocumentId, out var d)
        ? l with { IsSuperseded = d.Superseded, DocumentState = d.State, ReviewDueAt = d.Due }
        : l with { IsSuperseded = false, DocumentState = "effective", ReviewDueAt = null };

    private static ClaimEvidenceLink New(ClaimEvidenceObjectRef o, ClaimEvidenceLinkInput input) => new(
        Guid.NewGuid(), o, input.DocumentKind ?? "controlled", input.DocumentId, input.DocumentVersionId, "v1", "Doc",
        input.EvidenceTypeCode ?? "smpc-pil", input.Locator ?? new ClaimEvidenceLocator(null, null, null, "q"),
        input.SupportedSpans ?? [], "active", "tester", DateTimeOffset.UtcNow, null, null, null, input.DocumentVersionId,
        "v1", false, "effective", null);

    private static ClaimEvidenceCallResult<ClaimEvidenceLink> Ok(ClaimEvidenceLink link, int status) =>
        new(ClaimEvidenceCallOutcome.Ok, link, status, null, null);

    private static ClaimEvidenceCallResult<ClaimEvidenceLink> Fail(ClaimEvidenceCallOutcome outcome, int status, string? reason) =>
        new(outcome, null, status, reason, reason);
}
