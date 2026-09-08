using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Handlers;

public sealed class DeletePayBenchmarkingReadinessHandler : IRequestHandler<DeletePayBenchmarkingReadinessCommand, Response<bool>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeletePayBenchmarkingReadinessHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeletePayBenchmarkingReadinessCommand request, CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("PayBenchmarking readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.PayBenchmarkingReadinessState = PayBenchmarkingReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
