using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class RecordPayrollIntegrationSourceLinkHandler : IRequestHandler<RecordPayrollIntegrationSourceLinkCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;
    private readonly IPayrollSourceRepository _payrollSources;
    private readonly ITimeAttendanceProviderRepository _timeAttendanceProviders;
    private readonly IHrisSourceRepository _hrisSources;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;

    public RecordPayrollIntegrationSourceLinkHandler(IPayrollIntegrationGovernanceRepository repository, IPayrollSourceRepository payrollSources, ITimeAttendanceProviderRepository timeAttendanceProviders, IHrisSourceRepository hrisSources, IOrganizationUnitRepository organizationUnits, IPositionRepository positions)
    {
        _repository = repository;
        _payrollSources = payrollSources;
        _timeAttendanceProviders = timeAttendanceProviders;
        _hrisSources = hrisSources;
        _organizationUnits = organizationUnits;
        _positions = positions;
    }

    public async Task<Response<Guid>> Handle(RecordPayrollIntegrationSourceLinkCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var validation = await PayrollIntegrationReferenceGuard.ValidateSourceLinkAsync(request.Request, _payrollSources, _timeAttendanceProviders, _hrisSources, _organizationUnits, _positions, ct);
        if (!validation.IsSuccessful)
        {
            return Response<Guid>.Fail(validation.Errors, validation.StatusCode);
        }

        var link = new PayrollIntegrationSourceLink
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            SourceType = request.Request.SourceType,
            SourceReferenceId = request.Request.SourceReferenceId,
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            LinkState = request.Request.LinkState,
            ValidationMessage = string.IsNullOrWhiteSpace(request.Request.ValidationMessage) ? null : request.Request.ValidationMessage.Trim()
        };

        await _repository.CreateSourceLinkAsync(link, ct);
        return Response<Guid>.Success(link.Id, 201);
    }
}
