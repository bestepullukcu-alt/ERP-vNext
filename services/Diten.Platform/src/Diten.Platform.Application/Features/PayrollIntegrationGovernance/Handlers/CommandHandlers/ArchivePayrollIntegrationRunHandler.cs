using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class ArchivePayrollIntegrationRunHandler : IRequestHandler<ArchivePayrollIntegrationRunCommand, Response<NoContent>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public ArchivePayrollIntegrationRunHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ArchivePayrollIntegrationRunCommand request, CancellationToken ct) =>
        await _repository.ArchiveRunAsync(request.RunId, ct)
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("Payroll integration run not found.", 404);
}
