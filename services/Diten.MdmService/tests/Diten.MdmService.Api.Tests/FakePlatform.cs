using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// The Platform workflow engine as MDM sees it over its client contract — in memory, per test process. A started
/// instance waits until a test decides it (<see cref="Approve"/>), exactly like a task in the Task Center. Start is
/// idempotent by key (a replay returns the same instance), cancellation moves the versions on, and the evidence echoes
/// what the start recorded, so MDM's own exactness checks run unchanged.
/// </summary>
public sealed class FakeWorkflowEngine : IProductIdentityWorkflowClient
{
    public sealed class Instance
    {
        public required Guid Id { get; init; }
        public required Guid TemplateId { get; init; }
        public required Guid TemplateVersionId { get; init; }
        public required Guid TaskId { get; init; }
        public required string ObjectType { get; init; }
        public required string ObjectId { get; init; }
        public required string ObjectRef { get; init; }
        public required Guid Maker { get; init; }
        public required string Key { get; init; }
        public required long Sequence { get; init; }
        public int InstanceVersion { get; set; } = 1;
        public int TaskVersion { get; set; } = 1;
        public Guid? ApprovedBy { get; set; }
        public bool Cancelled { get; set; }
        public DateTimeOffset? DecidedAt { get; set; }
        public bool ClosedWithoutDecision { get; set; }
        public bool EvidenceInconsistent { get; set; }
    }

    private readonly ConcurrentDictionary<string, Instance> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Instance> _byId = new();

    /// <summary>The outcome the next start answers with (once), instead of starting.</summary>
    public ProductIdentityWorkflowTransportOutcome? NextStartFails { get; set; }

    /// <summary>FIX3 S3: the next start throws before it reaches the engine (a request cut off half-way).</summary>
    public bool NextStartThrows { get; set; }

    /// <summary>FIX4 item 12: the next terminal-evidence read throws (a connection broken half-way).</summary>
    public bool NextTerminalEvidenceThrows { get; set; }

    /// <summary>The next start answers this (once) — after creating the instance first when <c>Created</c>, the way an
    /// engine answers when the answer is lost or unreadable but the work was done.</summary>
    public (ProductIdentityWorkflowTransportOutcome Outcome, string Code, bool Created)? NextStartFailure { get; set; }

    /// <summary>While set, every start lookup answers this (the engine cannot say what it holds).</summary>
    public ProductIdentityWorkflowTransportOutcome? LookupFailure { get; set; }

    /// <summary>The outcome the next cancellation answers with (once), instead of cancelling.</summary>
    public ProductIdentityWorkflowTransportOutcome? NextCancelFails { get; set; }

    /// <summary>An administrator closes the approval without a decision (cancelled in the Task Center).</summary>
    public void CloseWithoutDecision(Instance instance)
    {
        instance.ClosedWithoutDecision = true;
        instance.InstanceVersion++;
        instance.TaskVersion++;
    }

    public int StartCalls;
    private long _sequence;

    public IReadOnlyList<Instance> Instances(string objectType) =>
        _byId.Values.Where(x => x.ObjectType == objectType).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();

    /// <summary>The single undecided instance of this type for this object — what the approver sees in the Task Center.</summary>
    public Instance Open(string objectType, string? objectId = null) => _byId.Values.Single(x =>
        x.ObjectType == objectType && (objectId is null || x.ObjectId == objectId)
        && x.ApprovedBy is null && !x.Cancelled);

    /// <summary>The most recently started undecided instance of this type.</summary>
    public Instance Latest(string objectType) => _byId.Values
        .Where(x => x.ObjectType == objectType && x.ApprovedBy is null && !x.Cancelled)
        .MaxBy(x => x.Sequence) ?? throw new InvalidOperationException($"No open {objectType} instance.");

    public void Approve(Instance instance, Guid approver)
    {
        instance.ApprovedBy = approver;
        instance.DecidedAt = DateTimeOffset.UtcNow;
        instance.InstanceVersion++;
        instance.TaskVersion++;
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
        Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref StartCalls);
        if (NextStartThrows)
        {
            NextStartThrows = false;
            throw new IOException("the maker's request was cut off before the start was sent");
        }
        if (NextStartFails is { } failure)
        {
            NextStartFails = null;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                failure, "FAKE_ENGINE_START_REFUSED"));
        }
        var scripted = NextStartFailure;
        NextStartFailure = null;
        if (scripted is { Created: false } refused)
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                refused.Outcome, refused.Code));

        var replay = _byKey.TryGetValue(request.IdempotencyKey, out var existing);
        var instance = existing ?? _byKey.GetOrAdd(request.IdempotencyKey, key =>
        {
            var created = new Instance
            {
                Id = Guid.NewGuid(),
                TemplateId = request.TemplateId ?? Derived("template:" + request.TemplateCode),
                TemplateVersionId = Derived("template-version:" + (request.TemplateId?.ToString() ?? request.TemplateCode)),
                TaskId = Guid.NewGuid(),
                ObjectType = request.ObjectType,
                ObjectId = request.ObjectId,
                ObjectRef = request.ObjectRef ?? request.ObjectId,
                Maker = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(delegatedUserToken).Subject),
                Key = key,
                Sequence = Interlocked.Increment(ref _sequence)
            };
            _byId[created.Id] = created;
            return created;
        });
        if (scripted is { Created: true } lost)
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                lost.Outcome, lost.Code));
        return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
            StartResult(instance, replay)));
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
        Guid tenantId, ProductIdentityWorkflowStartResultRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(LookupFailure is { } lookupFailure
            ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(lookupFailure, "FAKE_ENGINE_LOOKUP_FAILED")
            : _byKey.TryGetValue(request.IdempotencyKey, out var instance)
                        && instance.ObjectType == request.ExpectedObjectType
                        && instance.ObjectId == request.ExpectedObjectId
            ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult(instance, true))
            : ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND_NON_LEAKAGE"));

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
        Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (NextTerminalEvidenceThrows)
        {
            NextTerminalEvidenceThrows = false;
            throw new IOException("the engine connection broke half-way");
        }
        if (!_byId.TryGetValue(request.WorkflowInstanceId, out var instance))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"));
        // FIX4 (CT decision 3 corrected): what Platform answers TODAY for an approval closed without a decision — the
        // same "inconsistent" conflict as for contradictory facts (BL-547 will give it its own reason). Every code this
        // fake answers is one Platform's own source defines (FakePlatformCodeGuardTests).
        if (instance.ClosedWithoutDecision || instance.EvidenceInconsistent)
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT"));
        if (instance.ApprovedBy is not { } approver)
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL"));
        return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(new(
            instance.Id, instance.TaskId, instance.TemplateId, instance.TemplateVersionId, instance.ObjectType,
            instance.ObjectId, instance.ObjectRef, "Approve", approver.ToString("D"), "APPROVED",
            instance.DecidedAt!.Value, 2, "Approved", "Completed", "http-host")));
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>>
        GetCancellationPreflightAsync(Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!_byId.TryGetValue(request.WorkflowInstanceId, out var instance))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"));
        return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
            instance.Id, instance.TaskId, instance.ObjectType, instance.ObjectId, instance.ObjectRef,
            instance.InstanceVersion, instance.TaskVersion,
            instance.Cancelled ? "Cancelled" : instance.ApprovedBy is null ? "Active" : "Completed",
            instance.Cancelled ? "Cancelled" : instance.ApprovedBy is null ? "WaitingApproval" : "Approved")));
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>> CancelAsync(
        Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
        CancellationToken cancellationToken = default)
    {
        if (NextCancelFails is { } cancelFailure)
        {
            NextCancelFails = null;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
                cancelFailure, "FAKE_ENGINE_CANCEL_FAILED"));
        }
        if (!_byId.TryGetValue(request.WorkflowInstanceId, out var instance))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"));
        if (instance.ApprovedBy is not null
            || !instance.Cancelled && (request.ExpectedWorkflowInstanceVersion != instance.InstanceVersion
                                       || request.ExpectedApprovalTaskVersion != instance.TaskVersion))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT"));
        var replay = instance.Cancelled;
        if (!replay)
        {
            instance.Cancelled = true;
            instance.DecidedAt = DateTimeOffset.UtcNow;
            instance.InstanceVersion++;
            instance.TaskVersion++;
        }
        return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Success(new(
            instance.Id, instance.TaskId, instance.TemplateId, instance.TemplateVersionId, instance.ObjectType,
            instance.ObjectId, instance.ObjectRef, "Cancel", request.ExpectedMakerSubjectId, request.ReasonCode,
            request.Comment, instance.DecidedAt!.Value, 3, Derived("cancel-log:" + instance.Id), "Cancelled",
            "Cancelled", instance.InstanceVersion, instance.TaskVersion, replay, "http-host")));
    }

    private static ProductIdentityWorkflowStartResult StartResult(Instance instance, bool replay) => new(
        instance.Id, instance.TemplateId, instance.TemplateVersionId, instance.TaskId,
        Derived("snapshot:" + instance.Id), Derived("start-log:" + instance.Id), instance.ObjectRef, "Running",
        "Approval", "Review", DateTimeOffset.UtcNow, null, replay, "http-host");

    private static Guid Derived(string seed) => new(SHA256.HashData(Encoding.UTF8.GetBytes(seed)).AsSpan(0, 16));
}

/// <summary>Platform's verified reference data: C62 (each, whole) and KGM (three decimals), both selectable, version 1.</summary>
public sealed class FakeReferenceData : IVerifiedGskuReferenceResolver, IWorkflowVerifiedGskuReferenceResolver
{
    public static readonly DateTimeOffset ResolvedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(VerifiedGskuUomEnumerationResult.Success([
            new VerifiedGskuUom("C62", "Each", 1, 0),
            new VerifiedGskuUom("KGM", "Kilogram", 2, 3)]));

    public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        string packApplicabilityValueCode, string uomValueCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Resolve(packApplicabilityValueCode, uomValueCode));

    public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId, string packApplicabilityValueCode, string uomValueCode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Resolve(packApplicabilityValueCode, uomValueCode));

    private static VerifiedGskuReferenceResolveResult Resolve(string pack, string uom) =>
        uom is not ("C62" or "KGM")
            ? VerifiedGskuReferenceResolveResult.Fail(400, "PACK_UOM_INVALID")
            : VerifiedGskuReferenceResolveResult.Success([
                new VerifiedGskuReferenceSelection("pack-applicability", pack,
                    Guid.Parse("a0000000-0000-0000-0000-000000010001"), 1, "LATEST", ResolvedAt, false, true),
                new VerifiedGskuReferenceSelection("uom", uom,
                    Guid.Parse("a0000000-0000-0000-0000-000000010002"), 1, "LATEST", ResolvedAt, false, true)]);
}

/// <summary>The transport is configured — the real check needs Auth and Platform URLs a test host has no use for.</summary>
public sealed class ReadyTransport : IProductIdentityWorkflowTransportReadiness
{
    public string? FindConfigurationFault() => null;
}

/// <summary>Central audit forwarding is best effort in production; here it is recorded and goes nowhere.</summary>
public sealed class RecordingAuditForwarder : IPlatformAuditForwarder
{
    public ConcurrentQueue<AuditForwardRequest> Forwarded { get; } = new();

    public Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default)
    {
        Forwarded.Enqueue(request);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Platform's verified-GSKU resolve endpoint, on the wire. FIX2 item 2: the approval-time check uses the REAL resolver
/// client (PlatformWorkflowVerifiedGskuResolverClient) against this answer, so what a retired or withdrawn value does
/// is what production does — FIX1's test passed on a fake that returned a retired selection as a success.
/// </summary>
public sealed class FakePlatformReferenceEndpoint : HttpMessageHandler
{
    private static readonly DateTimeOffset ResolvedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Values Platform now reports as retired (no longer selectable for new use).</summary>
    public ConcurrentDictionary<string, bool> Retired { get; } = new(StringComparer.Ordinal);

    /// <summary>FIX3 S9: values Platform reports as not selectable for new use, though not retired.</summary>
    public ConcurrentDictionary<string, bool> NotSelectable { get; } = new(StringComparer.Ordinal);

    /// <summary>FIX3 (CT decision 1): from this call on (Calls counts every call), every value is not selectable.</summary>
    public int? NotSelectableFromCall { get; set; }

    /// <summary>FIX3 S6: while set, Platform answers with this status and a failure envelope naming this reason.</summary>
    public (HttpStatusCode Status, string Reason)? FailEnvelope { get; set; }

    public int Calls;

    /// <summary>While set, Platform answers with this status and no envelope (an outage, a refused service identity).</summary>
    public HttpStatusCode? FailWith { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref Calls);
        var everythingWithdrawn = NotSelectableFromCall is { } from && call >= from;
        if (FailWith is { } status)
            return new HttpResponseMessage(status) { Content = new StringContent("platform unavailable", Encoding.UTF8, "text/plain") };
        if (FailEnvelope is { } refusal)
            return new HttpResponseMessage(refusal.Status)
            {
                Content = new StringContent(new JsonObject
                {
                    ["data"] = null, ["statusCode"] = (int)refusal.Status, ["isSuccessful"] = false,
                    ["errors"] = new JsonArray(refusal.Reason), ["reason_code"] = refusal.Reason, ["correlation_id"] = null
                }.ToJsonString(), Encoding.UTF8, "application/json")
            };
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        var selections = new JsonArray();
        foreach (var item in body["selections"]!.AsArray())
        {
            var set = item!["set_code"]!.GetValue<string>();
            var value = item["value_code"]!.GetValue<string>();
            var retired = Retired.ContainsKey(value);
            selections.Add(new JsonObject
            {
                ["set_code"] = set,
                ["value_code"] = value,
                ["catalog_version_id"] = (set == "uom" ? "b0000000-0000-0000-0000-000000000002" : "b0000000-0000-0000-0000-000000000001"),
                ["catalog_version_number"] = 1,
                ["resolution_mode"] = "LATEST",
                ["resolved_at_utc"] = ResolvedAt.ToString("O"),
                ["is_retired"] = retired,
                ["selectable_for_new"] = !retired && !NotSelectable.ContainsKey(value) && !everythingWithdrawn
            });
        }
        var envelope = new JsonObject
        {
            ["data"] = new JsonObject { ["selections"] = selections },
            ["statusCode"] = 200,
            ["isSuccessful"] = true,
            ["errors"] = new JsonArray(),
            ["reason_code"] = null,
            ["correlation_id"] = null
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(envelope.ToJsonString(), Encoding.UTF8, "application/json")
        };
    }
}

/// <summary>The service token the resolver presents — issued here, in this process, for the test host only.</summary>
public sealed class FakeServiceIdentity : IWorkflowVerifiedMarketServiceIdentityProvider
{
    public Task<WorkflowVerifiedMarketServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkflowVerifiedMarketServiceIdentity("http-host-service-token", DateTimeOffset.UtcNow.AddMinutes(5)));
}
