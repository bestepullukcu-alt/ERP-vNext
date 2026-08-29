using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public sealed class DwsDispatchHandler(IDwsLocalActionExecutor executor)
    : IRequestHandler<DwsDispatchRequest, Response<DwsLocalResult>>
{
    public async Task<Response<DwsLocalResult>> Handle(DwsDispatchRequest request, CancellationToken cancellationToken)
    {
        request.Contract.Validate();
        if (!string.Equals(request.Operation, request.Contract.GetType().Name, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.InvalidRequest);
        var result = await executor.ExecuteAsync(request, cancellationToken);
        return Response<DwsLocalResult>.Success(result, request.Contract is CreateStructureCommand ? 201 : 200);
    }
}
