using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, Response<Guid>>
{
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITenantContext _tenantContext;
    private readonly IInProcessRecordCodeAllocator _recordCodes;
    private readonly IPositionCodeHistoryRepository _codeHistory;
    private readonly ILogger<CreatePositionCommandHandler> _logger;

    public CreatePositionCommandHandler(
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ITenantContext tenantContext,
        IInProcessRecordCodeAllocator recordCodes,
        IPositionCodeHistoryRepository codeHistory,
        ILogger<CreatePositionCommandHandler> logger)
    {
        _positions = positions;
        _organizationUnits = organizationUnits;
        _tenantContext = tenantContext;
        _recordCodes = recordCodes;
        _codeHistory = codeHistory;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreatePositionCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        // MOD-0040-FU02 DL-2 (§8.15) — the code is system-assigned; a filled one is refused here (envelope + Failed row).
        if (!string.IsNullOrWhiteSpace(request.Request.Code))
        {
            return Response<Guid>.Fail(
                "The Position code is assigned by the system; send no code.", 400, RecordCodeReasonCodes.SystemAssigned);
        }

        var referenceCheck = await PositionReferenceGuard.ValidateAsync(_positions, _organizationUnits, null, request.Request.OrganizationUnitId, request.Request.ReportsToPositionId, ct);
        if (!referenceCheck.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceCheck.Errors, referenceCheck.StatusCode);
        }

        /*
         * MOD-0040-FU02 DL-2 (§8.14, K-15, Q-5, K-17 F-2 b) — the position's company is its unit's company AT CREATION, read
         * a second time after the guard (the guard returns no unit and stays as it is). A unit deleted or archived between
         * the two reads answers what the guard answers; nothing is allocated. The user's own context company is NOT the
         * position's company. A later move to another company's unit never changes the code (RCS-001 §5.2).
         */
        var organizationUnit = await _organizationUnits.GetByIdAsync(request.Request.OrganizationUnitId, ct);
        if (organizationUnit == null || organizationUnit.IsArchived)
        {
            return Response<Guid>.Fail("Organization Unit not found.", 404);
        }

        var id = Guid.NewGuid();
        var allocation = await InProcessRecordCodeCollisionSkip.AllocateAsync(
            _recordCodes, "POS", organizationUnit.LegalEntityId, id, _codeHistory.ExistsByCodeIncludingDeletedAsync, _logger, ct);
        if (!allocation.IsAllocated)
        {
            return Response<Guid>.Fail("The Position code could not be assigned.", allocation.StatusCode, allocation.ReasonCode);
        }

        var entity = new Position
        {
            Id = id,
            TenantId = tenantId,
            Code = allocation.Code!,
            Name = request.Request.Name.Trim(),
            OrganizationUnitId = request.Request.OrganizationUnitId,
            ReportsToPositionId = request.Request.ReportsToPositionId
        };
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        // A write that fails after the allocation leaves the code a gap (RCS-001 §5.8): logged, and rethrown unchanged.
        try
        {
            await _positions.CreateAsync(entity, ct);
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
}
