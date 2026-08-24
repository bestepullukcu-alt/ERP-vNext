using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class EnsureVerifiedGskuTenantAssignmentsHandler
    : IRequestHandler<EnsureVerifiedGskuTenantAssignmentsCommand, Response<NoContent>>
{
    private static readonly string[] RequiredSets = ["pack-applicability", "uom"];
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EnsureVerifiedGskuTenantAssignmentsHandler(
        IBusinessReferenceDataStewardshipRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(
        EnsureVerifiedGskuTenantAssignmentsCommand request,
        CancellationToken cancellationToken)
    {
        Guid referenceTenantId;
        try
        {
            referenceTenantId = _repository.GetRequiredReferenceTenantId();
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "REFERENCE_PROVIDER_CONFIGURATION_INVALID")
        {
            return Fail("REFERENCE_PROVIDER_CONFIGURATION_INVALID", 503);
        }

        if (request.ConsumerTenantId == Guid.Empty || request.ConsumerTenantId == referenceTenantId)
        {
            return Fail("REFERENCE_ASSIGNMENT_CONFLICT", 409);
        }

        using (TenantScope.Begin(_tenantContext, referenceTenantId))
        {
            var existing = new Dictionary<string, BusinessReferenceDataTenantAssignment?>(StringComparer.Ordinal);
            foreach (var setCode in RequiredSets)
            {
                var assignment = await _repository.GetTenantAssignmentForReconciliationAsync(
                    request.ConsumerTenantId,
                    setCode,
                    cancellationToken);
                if (assignment is not null
                    && (assignment.IsDeleted
                        || assignment.AssignmentStatus != BusinessReferenceDataTenantAssignmentStatus.ACTIVE
                        || assignment.TenantId != referenceTenantId
                        || assignment.ConsumerTenantId != request.ConsumerTenantId
                        || !string.Equals(assignment.SetCode, setCode, StringComparison.Ordinal)))
                {
                    return Fail("REFERENCE_ASSIGNMENT_CONFLICT", 409);
                }

                existing[setCode] = assignment;
            }

            foreach (var setCode in RequiredSets.Where(code => existing[code] is null))
            {
                var result = await _repository.EnsureActiveTenantAssignmentAsync(
                    request.ConsumerTenantId,
                    setCode,
                    request.ActorId,
                    cancellationToken);
                if (result.Outcome == BusinessReferenceDataTenantAssignmentReconciliationOutcome.Conflict)
                {
                    return Fail("REFERENCE_ASSIGNMENT_CONFLICT", 409);
                }
            }
        }

        return Response<NoContent>.Success(204);
    }

    private static Response<NoContent> Fail(string code, int statusCode) =>
        Response<NoContent>.Fail(code, statusCode, code);
}
