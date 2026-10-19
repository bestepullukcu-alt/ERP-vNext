using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;

public sealed class DispatchProductAbbreviationWorkItemActionHandler
    : IRequestHandler<DispatchProductAbbreviationWorkItemActionCommand,
        ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>>
{
    private readonly IMediator _mediator;
    private readonly IProductAbbreviationRegisterRepository _register;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IProductAbbreviationActorContext _actor;
    private readonly IProductAbbreviationHistoryRepository _history;

    public DispatchProductAbbreviationWorkItemActionHandler(
        IMediator mediator,
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IProductAbbreviationActorContext actor,
        IProductAbbreviationHistoryRepository history)
    {
        _mediator = mediator;
        _register = register;
        _globalProducts = globalProducts;
        _actor = actor;
        _history = history;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>> Handle(
        DispatchProductAbbreviationWorkItemActionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.HasUnmappedFields
            || request.ItemId == Guid.Empty
            || !string.Equals(
                request.ProviderCode,
                ProductAbbreviationWorkItemContract.ProviderCode,
                StringComparison.Ordinal)
            || !ProductAbbreviationWorkItemContract.ActionCodes.Contains(request.ActionCode)
            || request.ExpectedVersion is null or < 0)
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }

        var reason = NormalizeReason(request.Reason ?? request.Note);
        if (request.ActionCode == "reject" && string.IsNullOrWhiteSpace(reason))
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }
        if (reason?.Length > 512)
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }

        var authorization = new ProductAbbreviationAuthorization(_actor).Demand(PermissionFor(request.ActionCode));
        if (!authorization.Succeeded) return Fail(authorization.StatusCode, authorization.ErrorCode!);
        var entry = await _register.GetByIdAsync(request.ItemId, cancellationToken);
        if (entry is null)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }
        var scope = await _scopeGuard.ResolveContextAsync(PermissionFor(request.ActionCode), cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.StatusCode, scope.FailureCode!);
        }
        if (scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
            && await _globalProducts.GetByIdAsync(entry.GlobalProductId, cancellationToken) is null)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context, entry.GlobalProductId, cancellationToken);
        if (!decision.Allowed)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }

        if (await TryRecognizeTerminalReplayAsync(entry, request, reason, cancellationToken))
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Success(
                new(request.ItemId.ToString("D"), ProductAbbreviationWorkItemContract.ProviderCode, request.ActionCode));
        }

        var workKind = ResolveWorkKind(entry);
        if (workKind is null || request.ActionCode == "cancel" && workKind == WorkKind.Retirement)
            return Fail(409, "CONCURRENCY_CONFLICT");

        var requestIdentity = workKind == WorkKind.Retirement
            ? entry.RetirementRequestId!
            : entry.Id.ToString("D");
        var operationKey = BuildOperationKey(
            request.ItemId,
            WorkKindCode(workKind.Value),
            requestIdentity,
            request.ActionCode,
            request.ExpectedVersion.Value,
            reason);
        var former = workKind == WorkKind.Correction
            ? await _register.GetByIdAsync(entry.ReplacesEntryId!.Value, cancellationToken)
            : null;
        if (workKind == WorkKind.Correction
            && (former is null
                || former.GlobalProductId != entry.GlobalProductId
                || former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE))
        {
            return Fail(409, "CONCURRENCY_CONFLICT");
        }

        Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto> response =
            (workKind.Value, request.ActionCode) switch
        {
            (WorkKind.Initial or WorkKind.Correction, "approve") => await _mediator.Send(
                new ApproveProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    ExpectedFormerVersion: former?.Version,
                    reason),
                cancellationToken),
            (WorkKind.Initial or WorkKind.Correction, "reject") => await _mediator.Send(
                new RejectProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    reason!),
                cancellationToken),
            (WorkKind.Initial or WorkKind.Correction, "cancel") => await _mediator.Send(
                new CancelProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    reason),
                cancellationToken),
            (WorkKind.Retirement, "approve") => await _mediator.Send(
                new ApproveProductAbbreviationRetirementCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    entry.RetirementRequestId!,
                    operationKey,
                    reason),
                cancellationToken),
            (WorkKind.Retirement, "reject") => await _mediator.Send(
                new RejectProductAbbreviationRetirementCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    entry.RetirementRequestId!,
                    operationKey,
                    reason!),
                cancellationToken),
            _ => throw new InvalidOperationException("Validated action code was not dispatchable.")
        };

        if (!response.IsSuccessful)
        {
            return Fail(
                response.StatusCode,
                response.Errors.FirstOrDefault() ?? "WORK_ITEM_ACTION_FAILED");
        }

        return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Success(
            new(
                request.ItemId.ToString("D"),
                ProductAbbreviationWorkItemContract.ProviderCode,
                request.ActionCode));
    }

    public static string BuildOperationKey(
        Guid itemId,
        string workKind,
        string requestIdentity,
        string actionCode,
        int expectedVersion,
        string? reason)
    {
        reason = NormalizeReason(reason);
        var canonicalReason = reason is null
            ? "null"
            : $"value:{Encoding.UTF8.GetByteCount(reason).ToString(CultureInfo.InvariantCulture)}:{reason}";
        var canonicalPayload = string.Join(
            '\n',
            $"provider={ProductAbbreviationWorkItemContract.ProviderCode}",
            $"itemId={itemId:D}",
            $"workKind={workKind}",
            $"requestIdentity={Encoding.UTF8.GetByteCount(requestIdentity).ToString(CultureInfo.InvariantCulture)}:{requestIdentity}",
            $"actionCode={actionCode}",
            $"expectedVersion={expectedVersion.ToString(CultureInfo.InvariantCulture)}",
            $"reason={canonicalReason}",
            string.Empty);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
        return $"abb-wc:{itemId:N}:{workKind}:{actionCode}:v{expectedVersion.ToString(CultureInfo.InvariantCulture)}:{payloadHash}";
    }

    public static string BuildOperationKey(Guid itemId, string actionCode, int expectedVersion, string? reason)
        => BuildOperationKey(
            itemId,
            WorkKindCode(WorkKind.Initial),
            itemId.ToString("D"),
            actionCode,
            expectedVersion,
            reason);

    private static string? NormalizeReason(string? reason)
        => reason?.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static string PermissionFor(string actionCode) => actionCode switch
    {
        "approve" => ProductAbbreviationPermissions.Approve,
        "reject" => ProductAbbreviationPermissions.Reject,
        "cancel" => ProductAbbreviationPermissions.Cancel,
        _ => throw new InvalidOperationException("Validated action code has no permission mapping.")
    };

    private static WorkKind? ResolveWorkKind(Diten.MdmService.Domain.Entities.ProductAbbreviationRegisterEntry entry)
        => entry.LifecycleStatus switch
        {
            ProductAbbreviationLifecycleStatus.REQUESTED when entry.ReplacesEntryId.HasValue => WorkKind.Correction,
            ProductAbbreviationLifecycleStatus.REQUESTED => WorkKind.Initial,
            ProductAbbreviationLifecycleStatus.ACTIVE when !string.IsNullOrWhiteSpace(entry.RetirementRequestId)
                => WorkKind.Retirement,
            ProductAbbreviationLifecycleStatus.ACTIVE or ProductAbbreviationLifecycleStatus.REJECTED
                or ProductAbbreviationLifecycleStatus.CANCELLED when entry.ReplacesEntryId.HasValue
                => WorkKind.Correction,
            ProductAbbreviationLifecycleStatus.ACTIVE or ProductAbbreviationLifecycleStatus.REJECTED
                or ProductAbbreviationLifecycleStatus.CANCELLED => WorkKind.Initial,
            _ => null
        };

    private async Task<bool> TryRecognizeTerminalReplayAsync(
        Diten.MdmService.Domain.Entities.ProductAbbreviationRegisterEntry entry,
        DispatchProductAbbreviationWorkItemActionCommand request,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (entry.Version != request.ExpectedVersion + 1
            || !string.Equals(NormalizeReason(entry.LastDecisionReason), reason, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entry.LastDecisionIdempotencyKey)
            || entry.LastDecisionByCanonicalSubjectId != _actor.CanonicalHumanSubjectId)
        {
            return false;
        }

        var key = entry.LastDecisionIdempotencyKey;
        var retirementMarker = $"abb-wc:{entry.Id:N}:retirement:";
        var kind = key.StartsWith(retirementMarker, StringComparison.Ordinal)
            ? WorkKind.Retirement
            : entry.ReplacesEntryId.HasValue
                ? WorkKind.Correction
                : WorkKind.Initial;

        var stateMatches = (kind, request.ActionCode, entry.LifecycleStatus) switch
        {
            (WorkKind.Initial or WorkKind.Correction, "approve", ProductAbbreviationLifecycleStatus.ACTIVE) => true,
            (WorkKind.Initial or WorkKind.Correction, "reject", ProductAbbreviationLifecycleStatus.REJECTED) => true,
            (WorkKind.Initial or WorkKind.Correction, "cancel", ProductAbbreviationLifecycleStatus.CANCELLED) => true,
            (WorkKind.Retirement, "approve", ProductAbbreviationLifecycleStatus.RETIRED) => true,
            (WorkKind.Retirement, "reject", ProductAbbreviationLifecycleStatus.ACTIVE) => true,
            _ => false
        };
        if (!stateMatches)
        {
            return false;
        }

        var histories = await _history.GetForRegisterEntryAsync(entry.Id, cancellationToken);
        var identities = kind == WorkKind.Retirement
            ? histories.Where(x => x.EventType == ProductAbbreviationHistoryEventType.RETIREMENT_REQUESTED)
                .Select(x => x.IdempotencyKey).Distinct(StringComparer.Ordinal).ToArray()
            : new[] { entry.Id.ToString("D") };
        var operationKeys = identities.Select(identity => BuildOperationKey(entry.Id, WorkKindCode(kind), identity,
            request.ActionCode, request.ExpectedVersion!.Value, reason))
            .Where(candidate => key == candidate || kind == WorkKind.Correction && request.ActionCode == "approve"
                && key == candidate + ":replacement").Take(2).ToArray();
        if (operationKeys.Length != 1) return false;
        var matching = histories.Where(x => x.IdempotencyKey == operationKeys[0]
            && x.CanonicalHumanSubjectId == _actor.CanonicalHumanSubjectId
            && x.AfterStatus == entry.LifecycleStatus && NormalizeReason(x.Reason) == reason).Take(2).ToArray();
        if (matching.Length != 1) return false;
        if (kind == WorkKind.Correction && request.ActionCode == "approve")
        {
            var former = await _register.GetByIdAsync(entry.ReplacesEntryId!.Value, cancellationToken);
            if (former?.LifecycleStatus != ProductAbbreviationLifecycleStatus.RETIRED) return false;
            var formerEvidence = (await _history.GetForRegisterEntryAsync(former.Id, cancellationToken))
                .Where(x => x.IdempotencyKey == operationKeys[0] + ":former"
                    && x.CanonicalHumanSubjectId == _actor.CanonicalHumanSubjectId).Take(2).ToArray();
            if (formerEvidence.Length != 1 || !await _register.AppendAuditIntentIfAbsentAsync(former.Id,
                    ProductAbbreviationAuditIntentFactory.Create(former, formerEvidence[0]), cancellationToken)) return false;
        }
        return await _register.AppendAuditIntentIfAbsentAsync(entry.Id,
            ProductAbbreviationAuditIntentFactory.Create(entry, matching[0]), cancellationToken);
    }

    private static string WorkKindCode(WorkKind kind) => kind switch
    {
        WorkKind.Initial => "allocation",
        WorkKind.Correction => "correction",
        WorkKind.Retirement => "retirement",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private enum WorkKind
    {
        Initial,
        Correction,
        Retirement
    }

    private static ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse> Fail(
        int statusCode,
        string reasonCode)
        => ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Fail(
            statusCode,
            reasonCode);
}
