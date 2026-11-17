using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreateOrganizationUnitCommandHandler : IRequestHandler<CreateOrganizationUnitCommand, Response<Guid>>
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly IOrganizationReportingGraphRepository _graph;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;
    private readonly ITenantContext _tenantContext;
    private readonly IInProcessRecordCodeAllocator _recordCodes;
    private readonly IOrganizationUnitCodeHistoryRepository _codeHistory;
    private readonly ILogger<CreateOrganizationUnitCommandHandler> _logger;

    public CreateOrganizationUnitCommandHandler(
        IOrganizationUnitRepository repository,
        IOrganizationReportingGraphRepository graph,
        ILegalEntityReferenceValidator legalEntityValidator,
        ITenantContext tenantContext,
        IInProcessRecordCodeAllocator recordCodes,
        IOrganizationUnitCodeHistoryRepository codeHistory,
        ILogger<CreateOrganizationUnitCommandHandler> logger)
    {
        _repository = repository;
        _graph = graph;
        _legalEntityValidator = legalEntityValidator;
        _tenantContext = tenantContext;
        _recordCodes = recordCodes;
        _codeHistory = codeHistory;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateOrganizationUnitCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        /*
         * MOD-0040-FU02 DL-2 (§8.15) — the code is SYSTEM-ASSIGNED. A filled one is refused here, in the handler, so the
         * answer is the envelope with its reason_code and the central audit row says Failed; ignoring it silently would lose
         * what the user typed without a trace. Blank (absent, empty, whitespace only) means "assign one".
         */
        if (!string.IsNullOrWhiteSpace(request.Request.Code))
        {
            return Response<Guid>.Fail(
                "The Organization Unit code is assigned by the system; send no code.", 400, RecordCodeReasonCodes.SystemAssigned);
        }

        var legalEntity = await _legalEntityValidator.ValidateAsync(request.Request.LegalEntityId, ct);
        if (!legalEntity.IsSuccessful || legalEntity.Data?.Referenceable != true)
        {
            return Response<Guid>.Fail("Legal Entity is not referenceable.", 404);
        }

        if (request.Request.ParentOrganizationUnitId.HasValue)
        {
            var parentCheck = await ValidateParentAsync(null, request.Request.ParentOrganizationUnitId.Value, request.Request.LegalEntityId, ct);
            if (!parentCheck.IsSuccessful)
            {
                return Response<Guid>.Fail(parentCheck.Errors, parentCheck.StatusCode);
            }
        }

        /*
         * MOD-0288-FU02 — the ADMINISTRATIVE line, validated by the same rules as the functional one and
         * carrying no extra ones. In particular it may point at the SAME unit as the functional parent: one
         * unit genuinely holding both responsibilities for another is a real structure, and only the SYSTEM
         * is forbidden from producing that pair. Nothing below copies one line into the other.
         */
        if (request.Request.AdministrativeParentOrganizationUnitId.HasValue)
        {
            var administrativeCheck = await ValidateParentAsync(
                null, request.Request.AdministrativeParentOrganizationUnitId.Value, request.Request.LegalEntityId, ct);
            if (!administrativeCheck.IsSuccessful)
            {
                return Response<Guid>.Fail(administrativeCheck.Errors, administrativeCheck.StatusCode);
            }
        }

        /*
         * MOD-0040-FU02 DL-2 (§8.14) — validation first (a refused create spends no number), then the allocation, then the
         * record. The record id is made BEFORE the allocation: it is the allocation's idempotency key, so the ledger row and
         * its audit row point at this record. A refused allocation answers as it was answered — no record, no fallback code.
         */
        var id = Guid.NewGuid();
        var allocation = await InProcessRecordCodeCollisionSkip.AllocateAsync(
            _recordCodes, "ORG", request.Request.LegalEntityId, id, _codeHistory.ExistsByCodeIncludingDeletedAsync, _logger, ct);
        if (!allocation.IsAllocated)
        {
            return Response<Guid>.Fail(
                "The Organization Unit code could not be assigned.", allocation.StatusCode, allocation.ReasonCode);
        }

        var entity = new OrganizationUnit
        {
            Id = id,
            TenantId = tenantId,
            Code = allocation.Code!,
            Name = request.Request.Name.Trim(),
            LegalEntityId = request.Request.LegalEntityId,
            ParentOrganizationUnitId = request.Request.ParentOrganizationUnitId,
            AdministrativeParentOrganizationUnitId = request.Request.AdministrativeParentOrganizationUnitId
        };
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        // A write that fails after the allocation leaves the code a gap (RCS-001 §5.8): logged, and rethrown unchanged.
        try
        {
            await _repository.CreateAsync(entity, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Record code {Code} was allocated for {RecordId} in tenant {TenantId} but the record was not written; the number stays a gap.",
                entity.Code, entity.Id, tenantId);
            throw;
        }

        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<Response<NoContent>> ValidateParentAsync(Guid? currentId, Guid parentId, Guid legalEntityId, CancellationToken ct)
    {
        if (currentId.HasValue && currentId.Value == parentId)
        {
            return Response<NoContent>.Fail("Organization Unit cannot be its own parent.", 409);
        }

        var parent = await _repository.GetByIdAsync(parentId, ct);
        if (parent == null || parent.IsArchived)
        {
            return Response<NoContent>.Fail("Parent Organization Unit not found.", 404);
        }

        if (parent.LegalEntityId != legalEntityId)
        {
            return Response<NoContent>.Fail("Parent Organization Unit must belong to the same Legal Entity.", 409);
        }

        /*
         * ⚠ A CREATE CANNOT CLOSE A CYCLE, and the guard says so itself by short-circuiting on a null
         * currentId. A brand-new unit's id is unknown to every other writer, so nothing points at it and no
         * path returns to it. That is also why creates skip the structure-token guard: they would pay for a
         * race they cannot lose.
         */
        return await OrganizationUnitCycleGuard.EnsureNoCycleAsync(_graph, currentId, parentId, ct);
    }
}
