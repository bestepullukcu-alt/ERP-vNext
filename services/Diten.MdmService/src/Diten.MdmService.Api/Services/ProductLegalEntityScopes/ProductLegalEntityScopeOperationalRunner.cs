using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Diten.MdmService.Api.Services.ProductLegalEntityScopes;

public sealed class ProductLegalEntityScopeOperationalRunner
{
    public const string InspectAction = "Inspect";
    public const string BootstrapPreparationAction = "BootstrapPreparation";
    public const string ActivateEnforcedAction = "ActivateEnforced";
    public const string SuspendFailClosedAction = "SuspendFailClosed";
    private const int MaximumDiagnosticSampleSize =
        ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot;

    private readonly IHostEnvironment _environment;
    private readonly IOptions<ProductLegalEntityScopeOperationalOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public ProductLegalEntityScopeOperationalRunner(
        IHostEnvironment environment,
        IOptions<ProductLegalEntityScopeOperationalOptions> options,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _environment = environment;
        _options = options;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async Task<ProductLegalEntityScopeOperationalResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDevelopment(_environment);
        ProductLegalEntityScopeOperationalOptions configuredOptions;
        try
        {
            configuredOptions = _options.Value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID",
                exception);
        }
        var facts = ValidateConfiguration(configuredOptions);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(facts.TenantId);

        if (facts.Action is ActivateEnforcedAction or SuspendFailClosedAction)
            return await ExecuteTransitionAsync(scope.ServiceProvider, facts, cancellationToken);

        IProductLegalEntityScopeRolloutStateRepository? rolloutRepository = null;
        ProductLegalEntityScopeRolloutState? rollout;
        var wasCreated = false;
        if (string.Equals(facts.Action, BootstrapPreparationAction, StringComparison.Ordinal))
        {
            rolloutRepository = scope.ServiceProvider
                .GetRequiredService<IProductLegalEntityScopeRolloutStateRepository>();
            (rollout, wasCreated) = await BootstrapPreparationAsync(
                rolloutRepository,
                facts,
                cancellationToken);
        }
        else
        {
            rollout = null;
        }

        GlobalProductScopeCompletenessInventory completeness;
        ProductLegalEntityScopeOperationalReadiness readiness;
        try
        {
            var products = scope.ServiceProvider.GetRequiredService<IGlobalProductRepository>();
            completeness = await products.GetProductLegalEntityScopeCompletenessInventoryAsync(
                _timeProvider.GetUtcNow(),
                MaximumDiagnosticSampleSize,
                cancellationToken);
            var readinessRepository = scope.ServiceProvider
                .GetRequiredService<IProductLegalEntityScopeOperationalReadinessRepository>();
            readiness = await readinessRepository.InspectAsync(
                MaximumDiagnosticSampleSize,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "PRODUCT_SCOPE_OPERATIONAL_READINESS_UNAVAILABLE",
                exception);
        }
        ValidateCompleteness(completeness);

        if (string.Equals(facts.Action, InspectAction, StringComparison.Ordinal))
        {
            rollout = readiness.Rollout is null
                ? null
                : new ProductLegalEntityScopeRolloutState
                {
                    Id = readiness.Rollout.Id,
                    TenantId = facts.TenantId,
                    Mode = readiness.Rollout.Mode,
                    Version = readiness.Rollout.Version,
                    CreationCommandId = readiness.Rollout.CreationCommandId,
                    CreatedByActorId = readiness.Rollout.CreatedByActorId
                };
        }

        if (string.Equals(facts.Action, BootstrapPreparationAction, StringComparison.Ordinal))
        {
            var persisted = await rolloutRepository!.GetAsync(cancellationToken);
            if (!ExactPreparation(persisted, facts)
                || !ExactOperationalRollout(readiness.Rollout, persisted!))
            {
                throw new InvalidOperationException("PRODUCT_SCOPE_RECONCILIATION_REQUIRED");
            }

            rollout = persisted;
        }

        return new ProductLegalEntityScopeOperationalResult(
            facts.Action,
            wasCreated,
            rollout is null
                ? new ProductLegalEntityScopeRolloutOperationalFact(null, "Uninitialized", null, null, null)
                : new ProductLegalEntityScopeRolloutOperationalFact(
                    rollout.Id,
                    rollout.Mode.ToString(),
                    rollout.Version,
                    rollout.CreationCommandId,
                    rollout.CreatedByActorId),
            new ProductLegalEntityScopeCompletenessOperationalFact(
                completeness.EligibleGlobalProductCount,
                completeness.ConfiguredGlobalProductCount,
                completeness.MissingGlobalProductIds,
                completeness.EligibleGlobalProductCount - completeness.ConfiguredGlobalProductCount
                > completeness.MissingGlobalProductIds.Count),
            readiness);
    }

    public static void EnsureDevelopment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED");
        }
    }

    private async Task<(ProductLegalEntityScopeRolloutState State, bool WasCreated)> BootstrapPreparationAsync(
        IProductLegalEntityScopeRolloutStateRepository repository,
        ProductLegalEntityScopeOperationalFacts facts,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(cancellationToken);
        if (existing is not null)
        {
            if (!ExactPreparation(existing, facts))
            {
                throw new InvalidOperationException("PRODUCT_SCOPE_ROLLOUT_BOOTSTRAP_CONFLICT");
            }

            return (existing, false);
        }

        var requested = ProductLegalEntityScopeRolloutState.CreatePreparation(
            facts.TenantId,
            facts.CommandId!.Value,
            facts.ActorId!.Value,
            _timeProvider.GetUtcNow());
        var result = await repository.CreateAsync(requested, cancellationToken);
        if (result.Succeeded && ExactPreparation(result.State, facts))
        {
            // The rollout repository reports both a fresh insert and an exact
            // duplicate-key recovery as Succeeded.  The requested identity is
            // therefore the authoritative created-vs-replayed discriminator.
            return (result.State!, result.State!.Id == requested.Id);
        }
        if (result.WriteOutcomeAmbiguous)
        {
            var replay = await repository.GetByCreationCommandIdAsync(facts.CommandId.Value, cancellationToken);
            if (ExactPreparation(replay, facts))
            {
                return (replay!, false);
            }

            throw new InvalidOperationException("PRODUCT_SCOPE_RECONCILIATION_REQUIRED");
        }

        throw new InvalidOperationException("PRODUCT_SCOPE_ROLLOUT_BOOTSTRAP_CONFLICT");
    }

    private async Task<ProductLegalEntityScopeOperationalResult> ExecuteTransitionAsync(
        IServiceProvider services,
        ProductLegalEntityScopeOperationalFacts facts,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IProductLegalEntityScopeRolloutStateRepository>();
        var readinessRepository = services.GetRequiredService<IProductLegalEntityScopeOperationalReadinessRepository>();
        var products = services.GetRequiredService<IGlobalProductRepository>();
        var current = await repository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("PRODUCT_SCOPE_ROLLOUT_NOT_INITIALIZED");

        if (current.LastTransitionCommandId == facts.CommandId)
        {
            if (current.Id != facts.ExpectedRolloutStateId || current.Version != facts.ExpectedRolloutVersion + 1
                || current.LastTransitionActorId != facts.ActorId
                || !string.Equals(current.LastTransitionAction, facts.Action, StringComparison.Ordinal)
                || !string.Equals(current.LastTransitionReasonCode, facts.ReasonCode, StringComparison.Ordinal)
                || current.LastInventorySnapshot is null
                || !string.Equals(current.LastTransitionEvidenceHash, current.LastInventorySnapshot.SnapshotHash, StringComparison.Ordinal)
                || current.Mode != (facts.Action == ActivateEnforcedAction
                    ? ProductLegalEntityScopeRolloutMode.Enforced
                    : ProductLegalEntityScopeRolloutMode.FailClosedSuspended))
                throw new InvalidOperationException("PRODUCT_SCOPE_OPERATION_REPLAY_DRIFT");
            return await BuildTransitionResultAsync(current, products, readinessRepository, facts, cancellationToken);
        }

        var expectedMode = facts.Action == ActivateEnforcedAction
            ? ProductLegalEntityScopeRolloutMode.Preparation
            : ProductLegalEntityScopeRolloutMode.Enforced;
        var token = CreateFenceToken(facts);
        var fence = new ProductLegalEntityScopeActivationFence
        {
            Token = token, State = ProductLegalEntityScopeAdmissionState.Closing,
            Action = facts.Action, CommandId = facts.CommandId!.Value,
            ActorId = facts.ActorId!.Value, ReasonCode = facts.ReasonCode,
            AcquiredAtUtc = _timeProvider.GetUtcNow()
        };
        var acquired = await repository.AcquireFenceAsync(
            fence, facts.ExpectedRolloutStateId!.Value, facts.ExpectedRolloutVersion!.Value,
            expectedMode, cancellationToken);
        if (!acquired.Acquired)
            throw new InvalidOperationException(acquired.FailureCode ?? "PRODUCT_SCOPE_ACTIVATION_FENCE_CONFLICT");

        var firstRequest = SnapshotRequest(acquired.Rollout!, facts);
        var first = await readinessRepository.CaptureInventorySnapshotAsync(firstRequest, cancellationToken);
        var secondRequest = SnapshotRequest(acquired.Rollout!, facts);
        var second = await readinessRepository.CaptureInventorySnapshotAsync(secondRequest, cancellationToken);
        if (!string.Equals(first.StableFactsHash, second.StableFactsHash, StringComparison.Ordinal)
            || !await repository.BindFenceSnapshotsAsync(token, first, second, cancellationToken))
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_DELTA_DETECTED");

        ValidateBoundTransitionSnapshot(
            second,
            secondRequest,
            acquired.Fence!,
            _timeProvider.GetUtcNow(),
            requireActivationReadiness: facts.Action == ActivateEnforcedAction);

        var persisted = await repository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("PRODUCT_SCOPE_RECONCILIATION_REQUIRED");
        var requested = CopyForTransition(persisted);
        requested.Mode = facts.Action == ActivateEnforcedAction
            ? ProductLegalEntityScopeRolloutMode.Enforced
            : ProductLegalEntityScopeRolloutMode.FailClosedSuspended;
        requested.Version = persisted.Version + 1;
        requested.UpdatedAt = _timeProvider.GetUtcNow();
        requested.LastInventorySnapshot = second;
        requested.LastTransitionCommandId = facts.CommandId;
        requested.LastTransitionActorId = facts.ActorId;
        requested.LastTransitionAction = facts.Action;
        requested.LastTransitionReasonCode = facts.ReasonCode;
        requested.LastTransitionEvidenceHash = second.SnapshotHash;
        var transitionIntent = ProductLegalEntityScopeAuditIntentFactory.Create(
            requested,
            facts.Action == ActivateEnforcedAction
                ? ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated
                : ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended,
            persisted.Version, requested.Version, facts.CommandId.Value, facts.ActorId.Value,
            second.SnapshotHash, requested.UpdatedAt.Value);
        requested.LastTransitionIntentId = transitionIntent.IntentId;
        requested.AuditIntents.Add(transitionIntent);
        var committed = await repository.CommitTransitionAsync(token, requested, persisted.Version, cancellationToken);
        if (!committed.Succeeded)
            throw new InvalidOperationException(committed.WriteOutcomeAmbiguous
                ? "PRODUCT_SCOPE_RECONCILIATION_REQUIRED"
                : "PRODUCT_SCOPE_ROLLOUT_VERSION_CONFLICT");
        return await BuildTransitionResultAsync(committed.State!, products, readinessRepository, facts, cancellationToken);
    }

    private ProductLegalEntityScopeInventorySnapshotRequest SnapshotRequest(
        ProductLegalEntityScopeRolloutState rollout, ProductLegalEntityScopeOperationalFacts facts) => new(
        facts.TenantId, rollout.Id, rollout.Mode, rollout.Version, facts.Action,
        facts.CommandId!.Value, facts.ActorId!.Value, facts.ReasonCode, _timeProvider.GetUtcNow());

    private static string CreateFenceToken(ProductLegalEntityScopeOperationalFacts facts)
    {
        var payload = string.Join('\n',
            $"tenantId={facts.TenantId:D}",
            $"rolloutStateId={facts.ExpectedRolloutStateId!.Value:D}",
            $"expectedVersion={facts.ExpectedRolloutVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"action={facts.Action.ToUpperInvariant()}",
            $"commandId={facts.CommandId!.Value:D}",
            $"actorId={facts.ActorId!.Value:D}",
            $"reasonCode={facts.ReasonCode}") + "\n";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    internal static void ValidateBoundActivationSnapshot(
        ProductLegalEntityScopeInventorySnapshot snapshot,
        ProductLegalEntityScopeInventorySnapshotRequest expected,
        ProductLegalEntityScopeActivationFence fence,
        DateTimeOffset serverNowUtc)
        => ValidateBoundTransitionSnapshot(
            snapshot,
            expected,
            fence,
            serverNowUtc,
            requireActivationReadiness: true);

    private static void ValidateBoundTransitionSnapshot(
        ProductLegalEntityScopeInventorySnapshot snapshot,
        ProductLegalEntityScopeInventorySnapshotRequest expected,
        ProductLegalEntityScopeActivationFence fence,
        DateTimeOffset serverNowUtc,
        bool requireActivationReadiness)
    {
        snapshot.EnsureValid();
        var rawLines = snapshot.CanonicalPayload.Split('\n', StringSplitOptions.None);
        if (rawLines.Length < 2 || rawLines[^1].Length != 0
            || rawLines[..^1].Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        var records = rawLines[..^1].Select(line => line.Split('=', 2)).ToArray();
        var expectedKeys = ExpectedInventoryKeys();
        if (records.Length != expectedKeys.Count
            || !records.Select(parts => parts[0]).SequenceEqual(expectedKeys, StringComparer.Ordinal)
            || records.Any(parts => parts.Length != 2))
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        var facts = records.ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        if (!string.Equals(facts["schemaVersion"], "1", StringComparison.Ordinal)
            || !string.Equals(facts["tenantId"], expected.TenantId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(facts["rolloutStateId"], expected.RolloutStateId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(facts["rolloutMode"], ((int)expected.RolloutMode).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            || !string.Equals(facts["rolloutVersion"], expected.RolloutVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            || !string.Equals(facts["action"], expected.Action, StringComparison.Ordinal)
            || !string.Equals(facts["commandId"], expected.CommandId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(facts["actorId"], expected.ActorId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(facts["reasonCodeBase64"], Convert.ToBase64String(Encoding.UTF8.GetBytes(expected.ReasonCode)), StringComparison.Ordinal)
            || !long.TryParse(facts["observedUtcTicks"], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var observedTicks)
            || observedTicks != snapshot.ObservedAtUtc.UtcTicks
            || snapshot.ObservedAtUtc != expected.ObservedAtUtc
            || snapshot.ObservedAtUtc < fence.AcquiredAtUtc
            || snapshot.ObservedAtUtc > serverNowUtc)
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        static long Count(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var value)
            && long.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
                ? parsed : throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        static void Hash(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out var value) || value.Length != 64
                || !value.All(Uri.IsHexDigit)
                || !string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        }
        var eligible = Count(facts, "eligibleGlobalProductCount");
        var configured = Count(facts, "configuredGlobalProductCount");
        var missing = Count(facts, "missingGlobalProductCount");
        Hash(facts, "eligibleGlobalProductHash"); Hash(facts, "missingGlobalProductHash");
        Hash(facts, "policyInventoryHash"); Count(facts, "policyCount");
        foreach (var category in InventoryCategories)
        {
            var total = Count(facts, $"descendant.{category}.total");
            var orphan = Count(facts, $"descendant.{category}.orphan");
            if (orphan > total) throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
            Hash(facts, $"descendant.{category}.hash");
        }
        if (eligible != configured + missing)
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        if (requireActivationReadiness
            && (missing != 0
                || InventoryCategories.Any(category => Count(facts, $"descendant.{category}.orphan") != 0)))
            throw new InvalidOperationException("PRODUCT_SCOPE_ACTIVATION_READINESS_NOT_SATISFIED");
        foreach (var type in new[] { 7, 8 })
        {
        foreach (var state in new[] { "pending", "processing", "delivered", "deadLetter", "malformed", "unacknowledged" })
            if (requireActivationReadiness && Count(facts, $"audit.{type}.{state}") != 0)
                throw new InvalidOperationException("PRODUCT_SCOPE_ACTIVATION_READINESS_NOT_SATISFIED");
            else if (!requireActivationReadiness)
                Count(facts, $"audit.{type}.{state}");
            Count(facts, $"audit.{type}.receipt");
            Hash(facts, $"audit.{type}.hash");
        }
    }

    private static IReadOnlyList<string> ExpectedInventoryKeys()
    {
        var keys = new List<string>
        {
            "schemaVersion", "tenantId", "rolloutStateId", "rolloutMode", "rolloutVersion",
            "action", "commandId", "actorId", "reasonCodeBase64", "observedUtcTicks",
            "eligibleGlobalProductCount", "configuredGlobalProductCount", "missingGlobalProductCount",
            "eligibleGlobalProductHash", "missingGlobalProductHash", "policyCount", "policyInventoryHash"
        };
        foreach (var category in InventoryCategories)
        {
            keys.Add($"descendant.{category}.total"); keys.Add($"descendant.{category}.orphan");
            keys.Add($"descendant.{category}.hash");
        }
        foreach (var type in new[] { 7, 8 })
        foreach (var state in new[] { "pending", "processing", "delivered", "deadLetter", "malformed", "unacknowledged", "receipt", "hash" })
            keys.Add($"audit.{type}.{state}");
        return keys;
    }

    private static readonly string[] InventoryCategories =
    [
        "ProductDefinitionRevision", "Gsku", "Lsku", "FinishedGood",
        "ProductAbbreviationRegister", "ProductAbbreviationAllocationLedger", "ProductAbbreviationHistory"
    ];

    private static ProductLegalEntityScopeRolloutState CopyForTransition(ProductLegalEntityScopeRolloutState source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, Mode = source.Mode,
        CreationCommandId = source.CreationCommandId, CreatedByActorId = source.CreatedByActorId,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt, Version = source.Version,
        AuditIntents = [.. source.AuditIntents], AuditIntentReceipts = [.. source.AuditIntentReceipts],
        ActiveFence = source.ActiveFence, ActiveWriterLease = source.ActiveWriterLease,
        WriterLeaseGeneration = source.WriterLeaseGeneration, LastInventorySnapshot = source.LastInventorySnapshot,
        LastTransitionCommandId = source.LastTransitionCommandId, LastTransitionActorId = source.LastTransitionActorId,
        LastTransitionAction = source.LastTransitionAction, LastTransitionReasonCode = source.LastTransitionReasonCode,
        LastTransitionEvidenceHash = source.LastTransitionEvidenceHash
        , LastTransitionIntentId = source.LastTransitionIntentId
    };

    private async Task<ProductLegalEntityScopeOperationalResult> BuildTransitionResultAsync(
        ProductLegalEntityScopeRolloutState rollout,
        IGlobalProductRepository products,
        IProductLegalEntityScopeOperationalReadinessRepository readinessRepository,
        ProductLegalEntityScopeOperationalFacts facts,
        CancellationToken cancellationToken)
    {
        var completeness = await products.GetProductLegalEntityScopeCompletenessInventoryAsync(
            _timeProvider.GetUtcNow(), MaximumDiagnosticSampleSize, cancellationToken);
        var readiness = await readinessRepository.InspectAsync(MaximumDiagnosticSampleSize, cancellationToken);
        var auditPending = DetermineAuditPending(rollout, facts);
        return new(facts.Action, false,
            new(rollout.Id, rollout.Mode.ToString(), rollout.Version, rollout.CreationCommandId, rollout.CreatedByActorId),
            new(completeness.EligibleGlobalProductCount, completeness.ConfiguredGlobalProductCount,
                completeness.MissingGlobalProductIds,
                completeness.EligibleGlobalProductCount - completeness.ConfiguredGlobalProductCount
                    > completeness.MissingGlobalProductIds.Count), readiness, auditPending);
    }

    private bool DetermineAuditPending(
        ProductLegalEntityScopeRolloutState rollout,
        ProductLegalEntityScopeOperationalFacts facts)
    {
        var command = facts.CommandId!.Value.ToString("D");
        var operation = facts.Action == ActivateEnforcedAction
            ? ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated
            : ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended;
        if (!rollout.LastTransitionIntentId.HasValue || rollout.LastTransitionIntentId == Guid.Empty)
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATION_AUDIT_PROOF_INVALID");
        var intentId = rollout.LastTransitionIntentId.Value;
        var intents = rollout.AuditIntents.Where(intent => intent.IntentId == intentId).ToList();
        var receipts = rollout.AuditIntentReceipts.Where(receipt =>
            receipt.IntentId == intentId).ToList();
        if (intents.Count > 1 || receipts.Count > 1 || intents.Count == receipts.Count
            || intents.Any(intent => intent.Operation != operation
                || intent.TenantId != rollout.TenantId
                || intent.AggregateType != AuditAggregateType.ProductLegalEntityScopeRolloutState
                || intent.AggregateId != rollout.Id
                || !string.Equals(intent.CommandId, command, StringComparison.Ordinal)
                || !string.Equals(intent.IdempotencyKey, command, StringComparison.Ordinal)
                || !string.Equals(intent.EvidenceHash, rollout.LastTransitionEvidenceHash, StringComparison.Ordinal))
            || receipts.Any(receipt => receipt.TenantId != rollout.TenantId
                || !string.Equals(receipt.IdempotencyKey, command, StringComparison.Ordinal)
                || !string.Equals(receipt.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
                || receipt.CentralAcknowledgement.Length > 512
                || !string.Equals(receipt.CentralAcknowledgement, receipt.CentralAcknowledgement.Trim(), StringComparison.Ordinal)
                || receipt.CentralAcknowledgement.Any(char.IsControl)
                || !string.Equals(receipt.ContractVersion, AuditIntentDeliveryProcessor.RequiredContractVersion, StringComparison.Ordinal)
                || !string.Equals(receipt.CentralIdempotencyKey,
                    AuditIntentContract.BuildCentralIdempotencyKey(rollout.TenantId, intentId, AuditIntentDeliveryProcessor.RequiredContractVersion), StringComparison.Ordinal)
                || !string.Equals(receipt.CompactReceiptReference, receipt.CentralAcknowledgement, StringComparison.Ordinal)
                || receipt.AcknowledgedAt.Offset != TimeSpan.Zero
                || receipt.DeliveredAt.Offset != TimeSpan.Zero
                || receipt.CompactedAt.Offset != TimeSpan.Zero
                || receipt.AcknowledgedAt > receipt.DeliveredAt || receipt.DeliveredAt > receipt.CompactedAt
                || receipt.CompactedAt > _timeProvider.GetUtcNow()
                || !string.Equals(receipt.EvidenceHash, rollout.LastTransitionEvidenceHash, StringComparison.Ordinal)))
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATION_AUDIT_PROOF_INVALID");
        if (receipts.Count == 1) return false;
        if (intents.Count == 1) return true;
        throw new InvalidOperationException("PRODUCT_SCOPE_OPERATION_AUDIT_PROOF_MISSING");
    }

    private static ProductLegalEntityScopeOperationalFacts ValidateConfiguration(
        ProductLegalEntityScopeOperationalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        if (string.IsNullOrWhiteSpace(options.Action))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        if (!string.Equals(options.Action, InspectAction, StringComparison.Ordinal)
            && !string.Equals(options.Action, BootstrapPreparationAction, StringComparison.Ordinal)
            && !string.Equals(options.Action, ActivateEnforcedAction, StringComparison.Ordinal)
            && !string.Equals(options.Action, SuspendFailClosedAction, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ACTION_NOT_AUTHORIZED");
        }
        if (!options.TenantId.HasValue || options.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        if (string.Equals(options.Action, BootstrapPreparationAction, StringComparison.Ordinal)
            && (!options.ActorId.HasValue
                || options.ActorId == Guid.Empty
                || !options.CommandId.HasValue
                || options.CommandId == Guid.Empty))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }

        var transition = options.Action is ActivateEnforcedAction or SuspendFailClosedAction;
        if (transition && (!options.ActorId.HasValue || options.ActorId == Guid.Empty
            || !options.CommandId.HasValue || options.CommandId == Guid.Empty
            || !options.ExpectedRolloutStateId.HasValue || options.ExpectedRolloutStateId == Guid.Empty
            || !options.ExpectedRolloutVersion.HasValue || options.ExpectedRolloutVersion < 0
            || !ProductLegalEntityScopeActivationFence.Reason(options.ReasonCode)))
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");

        return new(
            options.Action,
            options.TenantId.Value,
            options.ActorId,
            options.CommandId,
            options.ExpectedRolloutStateId,
            options.ExpectedRolloutVersion,
            options.ReasonCode);
    }

    private static bool ExactPreparation(
        ProductLegalEntityScopeRolloutState? state,
        ProductLegalEntityScopeOperationalFacts facts)
    {
        if (state is null || state.Id == Guid.Empty || state.IsDeleted || state.Version != 0)
        {
            return false;
        }

        try
        {
            state.EnsureValid();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return state.TenantId == facts.TenantId
               && state.Mode == ProductLegalEntityScopeRolloutMode.Preparation
               && facts.ActorId.HasValue
               && state.CreatedByActorId == facts.ActorId.Value
               && facts.CommandId.HasValue
               && state.CreationCommandId == facts.CommandId.Value
               && state.AuditIntents is { Count: 0 }
               && state.AuditIntentReceipts is { Count: 0 };
    }

    private static bool ExactOperationalRollout(
        ProductLegalEntityScopeOperationalRolloutFact? fact,
        ProductLegalEntityScopeRolloutState state)
        => fact is not null
           && fact.Id == state.Id
           && fact.Mode == state.Mode
           && fact.Version == state.Version
           && fact.CreationCommandId == state.CreationCommandId
           && fact.CreatedByActorId == state.CreatedByActorId;

    private static void ValidateCompleteness(GlobalProductScopeCompletenessInventory completeness)
    {
        ArgumentNullException.ThrowIfNull(completeness);
        var missingCount = completeness.EligibleGlobalProductCount - completeness.ConfiguredGlobalProductCount;
        if (completeness.EligibleGlobalProductCount < 0
            || completeness.ConfiguredGlobalProductCount < 0
            || missingCount < 0
            || completeness.MissingGlobalProductIds.Count > MaximumDiagnosticSampleSize
            || completeness.MissingGlobalProductIds.Count > missingCount
            || completeness.MissingGlobalProductIds.Any(id => id == Guid.Empty)
            || completeness.MissingGlobalProductIds.Distinct().Count()
               != completeness.MissingGlobalProductIds.Count)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_READINESS_UNAVAILABLE");
        }
    }

    private sealed record ProductLegalEntityScopeOperationalFacts(
        string Action,
        Guid TenantId,
        Guid? ActorId,
        Guid? CommandId,
        Guid? ExpectedRolloutStateId = null,
        int? ExpectedRolloutVersion = null,
        string ReasonCode = "");
}

public sealed record ProductLegalEntityScopeOperationalResult(
    string Action,
    bool WasCreated,
    ProductLegalEntityScopeRolloutOperationalFact Rollout,
    ProductLegalEntityScopeCompletenessOperationalFact Completeness,
    ProductLegalEntityScopeOperationalReadiness Readiness,
    bool AuditPending = false);

public sealed record ProductLegalEntityScopeRolloutOperationalFact(
    Guid? Id,
    string Mode,
    int? Version,
    Guid? CreationCommandId,
    Guid? CreatedByActorId);

public sealed record ProductLegalEntityScopeCompletenessOperationalFact(
    long EligibleGlobalProductCount,
    long ConfiguredGlobalProductCount,
    IReadOnlyList<Guid> MissingGlobalProductIds,
    bool HasMore);

public static class ProductLegalEntityScopeOperationalCommandLine
{
    public const string RunArgument = "--run-product-legal-entity-scope-operational";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var materialized = arguments.ToArray();
        if (materialized.Any(argument =>
                argument.StartsWith("--run-product-legal-entity-scope-operational", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(argument, RunArgument, StringComparison.Ordinal)))
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ARGUMENT_INVALID");
        var count = materialized.Count(argument => string.Equals(argument, RunArgument, StringComparison.Ordinal));
        if (count > 1) throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ARGUMENT_DUPLICATE");
        return count == 1;
    }

    public static Task<ProductLegalEntityScopeOperationalResult> RunAsync(
        ProductLegalEntityScopeOperationalRunner runner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        cancellationToken.ThrowIfCancellationRequested();
        return runner.RunAsync(cancellationToken);
    }
}

public static class ProductLegalEntityScopeOperationalConfiguration
{
    public static void EnsureValid(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ProductLegalEntityScopeOperationalOptions.SectionName);
        if (!bool.TryParse(section[nameof(ProductLegalEntityScopeOperationalOptions.Enabled)], out var enabled)
            || !enabled)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }

        var action = section[nameof(ProductLegalEntityScopeOperationalOptions.Action)];
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        if (!string.Equals(action, ProductLegalEntityScopeOperationalRunner.InspectAction, StringComparison.Ordinal)
            && !string.Equals(
                action,
                ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
                StringComparison.Ordinal)
            && !string.Equals(action, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, StringComparison.Ordinal)
            && !string.Equals(action, ProductLegalEntityScopeOperationalRunner.SuspendFailClosedAction, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ACTION_NOT_AUTHORIZED");
        }

        var tenant = ParseOptionalIdentity(
            section[nameof(ProductLegalEntityScopeOperationalOptions.TenantId)]);
        var actor = ParseOptionalIdentity(
            section[nameof(ProductLegalEntityScopeOperationalOptions.ActorId)]);
        var command = ParseOptionalIdentity(
            section[nameof(ProductLegalEntityScopeOperationalOptions.CommandId)]);
        var expectedRollout = ParseOptionalIdentity(
            section[nameof(ProductLegalEntityScopeOperationalOptions.ExpectedRolloutStateId)]);
        var expectedVersionText = section[nameof(ProductLegalEntityScopeOperationalOptions.ExpectedRolloutVersion)];
        int? expectedVersion = int.TryParse(expectedVersionText, out var parsedVersion) && parsedVersion >= 0
            ? parsedVersion : null;
        var reason = section[nameof(ProductLegalEntityScopeOperationalOptions.ReasonCode)] ?? string.Empty;
        var transition = action is ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction
            or ProductLegalEntityScopeOperationalRunner.SuspendFailClosedAction;
        if (!tenant.HasValue
            || string.Equals(
                action,
                ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
                StringComparison.Ordinal)
            && (!actor.HasValue || !command.HasValue))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        if (transition && (!actor.HasValue || !command.HasValue || !expectedRollout.HasValue
            || !expectedVersion.HasValue || !ProductLegalEntityScopeActivationFence.Reason(reason)))
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
    }

    private static Guid? ParseOptionalIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID");
        }
        return parsed;
    }
}

public static class ProductLegalEntityScopeOperationalServiceRegistration
{
    public static IServiceCollection AddProductLegalEntityScopeOperational(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<ProductLegalEntityScopeOperationalOptions>(
            configuration.GetSection(ProductLegalEntityScopeOperationalOptions.SectionName));
        services.AddScoped<ProductLegalEntityScopeOperationalRunner>();
        return services;
    }
}
