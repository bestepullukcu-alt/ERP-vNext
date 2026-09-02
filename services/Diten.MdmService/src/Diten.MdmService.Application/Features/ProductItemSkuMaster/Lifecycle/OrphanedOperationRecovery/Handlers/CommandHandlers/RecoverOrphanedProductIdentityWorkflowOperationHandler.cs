using System.Globalization;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Handlers.CommandHandlers;

public sealed class RecoverOrphanedProductIdentityWorkflowOperationHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityWorkflowOperationRecoveryRepository repository,
    IProductIdentityWorkflowClient workflowClient,
    TimeProvider timeProvider)
    : IRequestHandler<RecoverOrphanedProductIdentityWorkflowOperationCommand,
        Response<ProductIdentityWorkflowOperationRecoveryResult>>
{
    private const string ExactNotFoundCode = "NOT_FOUND_NON_LEAKAGE";

    public async Task<Response<ProductIdentityWorkflowOperationRecoveryResult>> Handle(
        RecoverOrphanedProductIdentityWorkflowOperationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!tenantContext.IsResolved || tenantContext.TenantId == Guid.Empty
            || !actorContext.TryResolveCanonicalHumanSubject(out var operatorSubjectId)
            || operatorSubjectId == Guid.Empty
            || !actorContext.HasPermission(ProductIdentityWorkflowOperationRecoveryPermissions.Recover))
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_FORBIDDEN", 403);
        }

        var request = command.Request;
        if (request is null) return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_INVALID", 400);
        var candidate = await repository.GetCandidateAsync(command.OperationId, cancellationToken);
        if (candidate is null) return Fail("PRODUCT_IDENTITY_WORKFLOW_OPERATION_NOT_FOUND", 404);
        var disposition = request.Action == ProductIdentityWorkflowOperationRecoveryAction.Supersede
            ? ProductIdentityWorkflowRecoveryDisposition.Superseded
            : ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart;
        var persisted = candidate.PersistedRecovery;
        var replay = persisted is not null;
        if (candidate.HasPersistedWorkflowEvidence
            || candidate.OriginalMakerSubjectId == operatorSubjectId
            || !ExactVersions(candidate.Scope, request.ExpectedTargetVersion)
            || !replay && (!candidate.IsPrepared
                || candidate.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.None
                || candidate.OperationVersion != request.ExpectedOperationVersion)
            || replay && (!ExactPersistedRecovery(persisted!, disposition, command, operatorSubjectId)
                || candidate.IsPrepared || candidate.RecoveryDisposition != disposition
                || candidate.OperationVersion <= 0
                || candidate.OperationVersion - 1 != request.ExpectedOperationVersion))
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_INELIGIBLE", 409);
        }
        if (request.Action == ProductIdentityWorkflowOperationRecoveryAction.Supersede
            && !actorContext.HasPermission(SubmitPermission(candidate.Scope.Family)))
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_SUBMIT_FORBIDDEN", 403);
        }

        var lookupRequest = new ProductIdentityWorkflowStartResultRequest(
            ObjectType(candidate.Scope.Family), ObjectId(candidate.Scope),
            candidate.OriginalMakerSubjectId, candidate.StartIdempotencyKey);
        var first = await workflowClient.GetStartResultAsync(
            tenantContext.TenantId, lookupRequest, cancellationToken);
        var firstFailure = ValidateNotFound(first);
        if (firstFailure is not null) return firstFailure;

        // The trusted lookup is deliberately repeated immediately before the transactional write.
        // A positive or ambiguous result on either observation fails closed.
        var second = await workflowClient.GetStartResultAsync(
            tenantContext.TenantId, lookupRequest, cancellationToken);
        var secondFailure = ValidateNotFound(second);
        if (secondFailure is not null) return secondFailure;

        var proofId = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.DeterministicGuid(
            $"workflow-not-found-v1|{tenantContext.TenantId:D}|{candidate.OperationId:D}|{command.CommandId:D}|{candidate.StartIdempotencyKey}");
        var proofFingerprint = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.Fingerprint(
            "workflow-not-found-proof-v1", tenantContext.TenantId.ToString("D"),
            candidate.OperationId.ToString("D"), ((int)candidate.Scope.Family).ToString(CultureInfo.InvariantCulture),
            lookupRequest.ExpectedObjectType, lookupRequest.ExpectedObjectId,
            lookupRequest.ExpectedMakerSubjectId.ToString("D"), lookupRequest.IdempotencyKey,
            candidate.OperationFingerprint, ExactNotFoundCode, proofId.ToString("D"));
        ProductIdentityWorkflowOperationRecoveryEvidence evidence;
        if (persisted is null)
        {
            var observedAt = timeProvider.GetUtcNow();
            if (observedAt.Offset != TimeSpan.Zero) observedAt = observedAt.ToUniversalTime();
            evidence = new(disposition, command.CommandId, operatorSubjectId, request.ReasonCode, request.Comment,
                proofId, proofFingerprint, observedAt.UtcTicks, observedAt.UtcTicks);
        }
        else
        {
            if (persisted.WorkflowNotFoundEvidenceId != proofId
                || !string.Equals(persisted.WorkflowNotFoundEvidenceFingerprint, proofFingerprint,
                    StringComparison.Ordinal))
            {
                return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);
            }
            evidence = new(persisted.Disposition, persisted.RecoveryCommandId, persisted.OperatorSubjectId,
                persisted.ReasonCode, persisted.Comment, persisted.WorkflowNotFoundEvidenceId,
                persisted.WorkflowNotFoundEvidenceFingerprint, persisted.WorkflowNotFoundObservedAtUtcTicksV1,
                persisted.RecoveredAtUtcTicksV1);
        }
        var successor = disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded
            ? CreateSuccessor(tenantContext.TenantId, candidate, command.CommandId, operatorSubjectId)
            : null;
        if (persisted is not null && !ExactPersistedSuccessor(persisted, successor))
            return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);
        var audit = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.Create(
            tenantContext.TenantId, candidate.Scope, evidence);
        var mutation = new ProductIdentityWorkflowOperationRecoveryMutation(
            candidate.OperationId, request.ExpectedOperationVersion, candidate.Scope,
            candidate.OriginalMakerSubjectId, candidate.StartIdempotencyKey,
            candidate.OperationFingerprint, candidate.LeaseOwner, candidate.LeaseUntilUtcTicksV1,
            candidate.LeaseGeneration, evidence, successor, audit);
        var write = await repository.RecoverAsync(mutation, cancellationToken);
        if (!replay && write.Status == ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict)
        {
            // One bounded retry closes the lost-CAS window between the trusted proof and the local write.
            // The winner's immutable snapshot is authoritative for timestamps; no synthetic replay time is used.
            var terminal = await repository.GetCandidateAsync(candidate.OperationId, cancellationToken);
            if (!ExactConcurrentWinner(candidate, terminal, disposition, command, operatorSubjectId))
                return Fail(write.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_RECOVERY_CONFLICT", 409);

            var winner = terminal!.PersistedRecovery!;
            if (winner.WorkflowNotFoundEvidenceId != proofId
                || !string.Equals(winner.WorkflowNotFoundEvidenceFingerprint, proofFingerprint,
                    StringComparison.Ordinal))
                return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);

            var winnerEvidence = new ProductIdentityWorkflowOperationRecoveryEvidence(
                winner.Disposition, winner.RecoveryCommandId, winner.OperatorSubjectId,
                winner.ReasonCode, winner.Comment, winner.WorkflowNotFoundEvidenceId,
                winner.WorkflowNotFoundEvidenceFingerprint, winner.WorkflowNotFoundObservedAtUtcTicksV1,
                winner.RecoveredAtUtcTicksV1);
            var winnerSuccessor = disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded
                ? CreateSuccessor(tenantContext.TenantId, candidate, command.CommandId, operatorSubjectId)
                : null;
            if (!ExactPersistedSuccessor(winner, winnerSuccessor))
                return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);
            var winnerAudit = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.Create(
                tenantContext.TenantId, candidate.Scope, winnerEvidence);
            var exactReplayMutation = new ProductIdentityWorkflowOperationRecoveryMutation(
                candidate.OperationId, request.ExpectedOperationVersion, candidate.Scope,
                candidate.OriginalMakerSubjectId, candidate.StartIdempotencyKey,
                candidate.OperationFingerprint, candidate.LeaseOwner, candidate.LeaseUntilUtcTicksV1,
                candidate.LeaseGeneration, winnerEvidence, winnerSuccessor, winnerAudit);
            write = await repository.RecoverAsync(exactReplayMutation, cancellationToken);
            if (write.Status != ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay)
                return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);
        }
        if (replay && write.Status != ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay)
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REPLAY_CONFLICT", 409);
        }
        if (!write.Succeeded || write.Scope is null || !write.OperationVersion.HasValue
            || !write.Disposition.HasValue)
        {
            return write.Status switch
            {
                ProductIdentityWorkflowOperationRecoveryWriteStatus.NotFound =>
                    Fail("PRODUCT_IDENTITY_WORKFLOW_OPERATION_NOT_FOUND", 404),
                ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict =>
                    Fail(write.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_RECOVERY_CONFLICT", 409),
                _ => Fail(write.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_RECOVERY_INELIGIBLE", 409)
            };
        }

        var resultScope = Versions(write.Scope);
        var resultSuccessor = write.Successor is null
            ? null
            : new ProductIdentityWorkflowOperationRecoverySuccessorResult(
                write.Successor.OperationId, Versions(write.Successor.Scope));
        return Response<ProductIdentityWorkflowOperationRecoveryResult>.Success(
            new(write.OperationId, write.Scope.Family, write.Disposition.Value,
                write.OperationVersion.Value, resultScope, resultSuccessor), 200);
    }

    private static Response<ProductIdentityWorkflowOperationRecoveryResult>? ValidateNotFound(
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result)
    {
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
            && result.Value is null
            && string.Equals(result.ErrorCode, ExactNotFoundCode, StringComparison.Ordinal)) return null;
        return result.Outcome switch
        {
            ProductIdentityWorkflowTransportOutcome.Timeout => Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_PROOF_TIMEOUT", 504),
            ProductIdentityWorkflowTransportOutcome.Success => Fail("PRODUCT_IDENTITY_WORKFLOW_ALREADY_STARTED", 409),
            ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.NonTerminal
                or ProductIdentityWorkflowTransportOutcome.Conflict =>
                Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_PROOF_AMBIGUOUS", 409),
            _ => Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_PROOF_UNAVAILABLE", 503)
        };
    }

    private static ProductIdentityWorkflowOperationRecoverySuccessor CreateSuccessor(
        Guid tenantId,
        ProductIdentityWorkflowOperationRecoveryCandidate candidate,
        Guid commandId,
        Guid operatorSubjectId)
    {
        var successorId = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.DeterministicGuid(
            $"product-identity-recovery-successor-v1|{tenantId:D}|{candidate.OperationId:D}|{commandId:D}|{(int)candidate.Scope.Family}");
        if (successorId == candidate.OperationId)
            throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_SUCCESSOR_IDENTITY_CONFLICT");
        var scope = Increment(candidate.Scope);
        var startKey = $"{StartKeyPrefix(candidate.Scope.Family)}:{tenantId:D}:{successorId:D}";
        var fingerprint = ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.Fingerprint(
            "product-identity-workflow-recovery-successor-v1", tenantId.ToString("D"),
            candidate.OperationId.ToString("D"), successorId.ToString("D"), commandId.ToString("D"),
            operatorSubjectId.ToString("D"), ((int)candidate.Scope.Family).ToString(CultureInfo.InvariantCulture),
            startKey, candidate.OperationFingerprint, ScopeFingerprint(scope));
        return new(successorId, startKey, fingerprint, operatorSubjectId, scope);
    }

    private static bool ExactPersistedRecovery(
        ProductIdentityWorkflowOperationPersistedRecoverySnapshot persisted,
        ProductIdentityWorkflowRecoveryDisposition disposition,
        RecoverOrphanedProductIdentityWorkflowOperationCommand command,
        Guid operatorSubjectId) =>
        persisted.Disposition == disposition
        && persisted.RecoveryCommandId == command.CommandId
        && persisted.OperatorSubjectId == operatorSubjectId
        && string.Equals(persisted.ReasonCode, command.Request.ReasonCode, StringComparison.Ordinal)
        && string.Equals(persisted.Comment, command.Request.Comment, StringComparison.Ordinal);

    private static bool ExactPersistedSuccessor(
        ProductIdentityWorkflowOperationPersistedRecoverySnapshot persisted,
        ProductIdentityWorkflowOperationRecoverySuccessor? successor) => successor is null
        ? persisted.SuccessorOperationId is null
            && persisted.SuccessorStartIdempotencyKey is null
            && persisted.SuccessorOperationFingerprint is null
        : persisted.SuccessorOperationId == successor.OperationId
            && string.Equals(persisted.SuccessorStartIdempotencyKey, successor.StartIdempotencyKey,
                StringComparison.Ordinal)
            && string.Equals(persisted.SuccessorOperationFingerprint, successor.OperationFingerprint,
                StringComparison.Ordinal);

    private static bool ExactConcurrentWinner(
        ProductIdentityWorkflowOperationRecoveryCandidate original,
        ProductIdentityWorkflowOperationRecoveryCandidate? terminal,
        ProductIdentityWorkflowRecoveryDisposition disposition,
        RecoverOrphanedProductIdentityWorkflowOperationCommand command,
        Guid operatorSubjectId) => terminal is not null
        && terminal.OperationId == original.OperationId
        && terminal.OperationVersion == original.OperationVersion + 1
        && Equals(terminal.Scope, original.Scope)
        && terminal.OriginalMakerSubjectId == original.OriginalMakerSubjectId
        && string.Equals(terminal.StartIdempotencyKey, original.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(terminal.OperationFingerprint, original.OperationFingerprint, StringComparison.Ordinal)
        && !terminal.IsPrepared
        && !terminal.HasPersistedWorkflowEvidence
        && terminal.RecoveryDisposition == disposition
        && terminal.PersistedRecovery is not null
        && ExactPersistedRecovery(terminal.PersistedRecovery, disposition, command, operatorSubjectId);

    private static ProductIdentityWorkflowOperationRecoveryScope Increment(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => new GlobalProductWorkflowRecoveryScope(
                item.GlobalProductId, checked(item.GlobalProductVersion + 1)),
            FirstGskuWorkflowRecoveryScope item => new FirstGskuWorkflowRecoveryScope(
                item.GlobalProductId, item.ProductDefinitionRevisionId, item.GskuId,
                checked(item.GskuVersion + 1), checked(item.ProductDefinitionRevisionVersion + 1)),
            LskuWorkflowRecoveryScope item => new LskuWorkflowRecoveryScope(
                item.LskuId, item.GskuId, item.ProductDefinitionRevisionId, item.MarketCode,
                checked(item.LskuVersion + 1)),
            FinishedGoodWorkflowRecoveryScope item => new FinishedGoodWorkflowRecoveryScope(
                item.FinishedGoodId, item.GskuId, item.ProductDefinitionRevisionId,
                checked(item.FinishedGoodVersion + 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    private static bool ExactVersions(ProductIdentityWorkflowOperationRecoveryScope scope,
        ProductIdentityWorkflowTargetVersions versions) => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => versions.PrimaryEntityVersion == item.GlobalProductVersion
                && versions.ProductDefinitionRevisionVersion is null,
            FirstGskuWorkflowRecoveryScope item => versions.PrimaryEntityVersion == item.GskuVersion
                && versions.ProductDefinitionRevisionVersion == item.ProductDefinitionRevisionVersion,
            LskuWorkflowRecoveryScope item => versions.PrimaryEntityVersion == item.LskuVersion
                && versions.ProductDefinitionRevisionVersion is null,
            FinishedGoodWorkflowRecoveryScope item => versions.PrimaryEntityVersion == item.FinishedGoodVersion
                && versions.ProductDefinitionRevisionVersion is null,
            _ => false
        };

    private static ProductIdentityWorkflowTargetVersions Versions(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => new(item.GlobalProductVersion),
            FirstGskuWorkflowRecoveryScope item => new(item.GskuVersion, item.ProductDefinitionRevisionVersion),
            LskuWorkflowRecoveryScope item => new(item.LskuVersion),
            FinishedGoodWorkflowRecoveryScope item => new(item.FinishedGoodVersion),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    private static string ObjectType(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => ProductIdentityWorkflowStartRequestFactory.GlobalProductObjectType,
        ProductIdentityWorkflowOperationFamily.FirstGsku => FirstGskuIdentityWorkflowStartRequestFactory.ObjectType,
        ProductIdentityWorkflowOperationFamily.Lsku => LskuIdentityWorkflowStartRequestFactory.LskuObjectType,
        ProductIdentityWorkflowOperationFamily.FinishedGood => FinishedGoodIdentityWorkflowStartRequestFactory.FinishedGoodObjectType,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string ObjectId(ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => item.GlobalProductId.ToString("D"),
        FirstGskuWorkflowRecoveryScope item => item.GskuId.ToString("D"),
        LskuWorkflowRecoveryScope item => item.LskuId.ToString("D"),
        FinishedGoodWorkflowRecoveryScope item => item.FinishedGoodId.ToString("D"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static string SubmitPermission(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => ProductIdentityLifecyclePermissions.GlobalProductSubmit,
        ProductIdentityWorkflowOperationFamily.FirstGsku => FirstGskuIdentityLifecyclePermissions.Submit,
        ProductIdentityWorkflowOperationFamily.Lsku => LskuIdentityLifecyclePermissions.Submit,
        ProductIdentityWorkflowOperationFamily.FinishedGood => FinishedGoodIdentityLifecyclePermissions.Submit,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string StartKeyPrefix(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => "global-product-identity",
        ProductIdentityWorkflowOperationFamily.FirstGsku => "first-gsku-identity",
        ProductIdentityWorkflowOperationFamily.Lsku => "lsku-identity",
        ProductIdentityWorkflowOperationFamily.FinishedGood => "finished-good-identity",
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string ScopeFingerprint(ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => $"{item.GlobalProductId:D}|{item.GlobalProductVersion}",
        FirstGskuWorkflowRecoveryScope item => $"{item.GlobalProductId:D}|{item.ProductDefinitionRevisionId:D}|{item.GskuId:D}|{item.GskuVersion}|{item.ProductDefinitionRevisionVersion}",
        LskuWorkflowRecoveryScope item => $"{item.LskuId:D}|{item.GskuId:D}|{item.ProductDefinitionRevisionId:D}|{item.MarketCode}|{item.LskuVersion}",
        FinishedGoodWorkflowRecoveryScope item => $"{item.FinishedGoodId:D}|{item.GskuId:D}|{item.ProductDefinitionRevisionId:D}|{item.FinishedGoodVersion}",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static Response<ProductIdentityWorkflowOperationRecoveryResult> Fail(string code, int statusCode) =>
        Response<ProductIdentityWorkflowOperationRecoveryResult>.Fail(code, statusCode);
}
