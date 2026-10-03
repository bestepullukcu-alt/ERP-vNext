using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;

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
    }

    private readonly ConcurrentDictionary<string, Instance> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Instance> _byId = new();

    /// <summary>The outcome the next start answers with (once), instead of starting.</summary>
    public ProductIdentityWorkflowTransportOutcome? NextStartFails { get; set; }

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
        if (NextStartFails is { } failure)
        {
            NextStartFails = null;
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                failure, "FAKE_ENGINE_START_REFUSED"));
        }

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
        return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
            StartResult(instance, replay)));
    }

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
        Guid tenantId, ProductIdentityWorkflowStartResultRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byKey.TryGetValue(request.IdempotencyKey, out var instance)
                        && instance.ObjectType == request.ExpectedObjectType
                        && instance.ObjectId == request.ExpectedObjectId
            ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult(instance, true))
            : ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_START_NOT_FOUND"));

    public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
        Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_byId.TryGetValue(request.WorkflowInstanceId, out var instance))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"));
        if (instance.ApprovedBy is not { } approver)
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NonTerminal, "WORKFLOW_INSTANCE_NOT_TERMINAL"));
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
        if (!_byId.TryGetValue(request.WorkflowInstanceId, out var instance))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_NOT_FOUND"));
        if (instance.ApprovedBy is not null
            || !instance.Cancelled && (request.ExpectedWorkflowInstanceVersion != instance.InstanceVersion
                                       || request.ExpectedApprovalTaskVersion != instance.TaskVersion))
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_VERSION_CONFLICT"));
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
