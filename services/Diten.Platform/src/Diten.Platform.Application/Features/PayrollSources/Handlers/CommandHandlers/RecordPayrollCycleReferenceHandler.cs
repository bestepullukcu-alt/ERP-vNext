using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class RecordPayrollCycleReferenceHandler : IRequestHandler<RecordPayrollCycleReferenceCommand, Response<Guid>>
{
    private readonly IPayrollSourceRepository _repository;

    public RecordPayrollCycleReferenceHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollCycleReferenceCommand request, CancellationToken ct)
    {
        var source = await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<Guid>.Fail("Payroll source not found.", 404);
        }

        var cycle = new PayrollCycleReference
        {
            TenantId = source.TenantId,
            PayrollExternalSystemProfileId = source.Id,
            ExternalPayCycleId = request.Request.ExternalPayCycleId.Trim(),
            CycleCode = request.Request.CycleCode.Trim(),
            PeriodStart = request.Request.PeriodStart,
            PeriodEnd = request.Request.PeriodEnd,
            ProcessingState = request.Request.ProcessingState,
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateCycleReferenceAsync(cycle, ct);
        return Response<Guid>.Success(cycle.Id, 201);
    }
}
