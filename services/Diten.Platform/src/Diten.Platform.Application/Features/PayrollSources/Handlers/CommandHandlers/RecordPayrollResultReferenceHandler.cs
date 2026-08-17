using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class RecordPayrollResultReferenceHandler : IRequestHandler<RecordPayrollResultReferenceCommand, Response<Guid>>
{
    private readonly IPayrollSourceRepository _repository;

    public RecordPayrollResultReferenceHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollResultReferenceCommand request, CancellationToken ct)
    {
        var source = await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<Guid>.Fail("Payroll source not found.", 404);
        }

        if (await _repository.GetCycleReferenceByIdAsync(source.Id, request.Request.PayrollCycleReferenceId, ct) == null)
        {
            return Response<Guid>.Fail("Payroll cycle reference not found.", 404);
        }

        var result = new PayrollResultReference
        {
            TenantId = source.TenantId,
            PayrollExternalSystemProfileId = source.Id,
            PayrollCycleReferenceId = request.Request.PayrollCycleReferenceId,
            ExternalPayrollResultId = request.Request.ExternalPayrollResultId.Trim(),
            ResultVersion = request.Request.ResultVersion.Trim(),
            ResultState = request.Request.ResultState,
            PublishedAt = request.Request.PublishedAt,
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateResultReferenceAsync(result, ct);
        return Response<Guid>.Success(result.Id, 201);
    }
}
