using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class UpdateTenantModuleEntitlementExpiryCommandHandler : IRequestHandler<UpdateTenantModuleEntitlementExpiryCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTenantModuleEntitlementExpiryCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IModuleCatalogRepository moduleRepository,
        IPlatformTransactionExecutor transactions,
        IEntitlementStateVersionRepository versions,
        ITransactionalIntegrationEventWriter events,
        ITransactionalAuditOutboxWriter audit,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
        _transactions = transactions;
        _versions = versions;
        _events = events;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTenantModuleEntitlementExpiryCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return TenantModuleEntitlementCommandSupport.NotFound();
        }

        // BL-500 — the list's own rule: a new date is offered on an Addon/Trial/override row of a module the catalogue
        // still knows. The date itself is required and in the future (the validator): this action never clears it.
        var refusal = await TenantModuleEntitlementActionGate.RefuseStoredAsync(
            _moduleRepository, entitlement, TenantModuleEntitlementRowActions.ExtendExpiry, ct);
        if (refusal is not null)
        {
            return refusal;
        }

        try
        {
            var expiryChanged = entitlement.ExpiryDateUtc != request.Request.ExpiryDateUtc;
            var reasonChanged = !string.IsNullOrWhiteSpace(request.Request.Reason)
                                && !string.Equals(entitlement.Reason, request.Request.Reason, StringComparison.Ordinal);
            if (!expiryChanged && !reasonChanged)
            {
                // FIX2 — "nothing to change" is only true of the row the screen saw: a stale version is answered as
                // stale here too, not as a quiet 204 over a row somebody else has changed since.
                return request.Request.RowVersion is { Length: > 0 } version && version.AsSpan().SequenceEqual(entitlement.RowVersion)
                    ? Response<NoContent>.Success(204)
                    : TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
            }
            var auditBefore = PhysicalEntitlementAuditIntent.StateOf(entitlement);
            entitlement.ExpiryDateUtc = request.Request.ExpiryDateUtc;
            if (!string.IsNullOrWhiteSpace(request.Request.Reason))
            {
                entitlement.Reason = request.Request.Reason;
            }

            var auditIntentId = Guid.NewGuid();
            await _transactions.ExecuteAsync(async (session, transactionCt) =>
            {
                var eventId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

                await _repository.UpdateAsync(session, entitlement, request.Request.RowVersion, transactionCt);
                await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, entitlement.ModuleCode, transactionCt);
                await _events.EnqueueAsync(
                    session,
                    new TenantEntitlementExpiryUpdatedV1(
                        eventId,
                        occurredAtUtc,
                        request.TenantId,
                        correlationId,
                        actorId,
                        entitlement.ModuleCode),
                    new EventPublishOptions
                    {
                        EventId = eventId,
                        CorrelationId = correlationId,
                        TenantId = request.TenantId,
                        Producer = "Diten.Platform",
                        OccurredAtUtc = occurredAtUtc
                    },
                    transactionCt);
                await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, correlationId,
                    auditIntentId, nameof(UpdateTenantModuleEntitlementExpiryCommand), AuditOperation.Update,
                    entitlement.Id, entitlement.ModuleCode, transactionCt,
                    auditBefore, PhysicalEntitlementAuditIntent.StateOf(entitlement));
                return true;
            }, ct);

            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementTenantMismatchException)
        {
            return TenantModuleEntitlementCommandSupport.NotFound();
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
