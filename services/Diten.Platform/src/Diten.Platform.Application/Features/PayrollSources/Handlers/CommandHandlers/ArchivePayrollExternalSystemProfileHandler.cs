using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class ArchivePayrollExternalSystemProfileHandler : IRequestHandler<ArchivePayrollExternalSystemProfileCommand, Response<NoContent>>
{
    private readonly IPayrollSourceRepository _repository;

    public ArchivePayrollExternalSystemProfileHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ArchivePayrollExternalSystemProfileCommand request, CancellationToken ct)
    {
        if (!await _repository.ArchiveExternalSystemProfileAsync(request.Id, ct))
        {
            return Response<NoContent>.Fail("Payroll source not found.", 404);
        }

        return Response<NoContent>.Success(204);
    }
}
