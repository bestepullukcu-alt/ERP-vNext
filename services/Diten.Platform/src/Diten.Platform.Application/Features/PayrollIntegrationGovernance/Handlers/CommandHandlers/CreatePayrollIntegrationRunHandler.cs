using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class CreatePayrollIntegrationRunHandler : IRequestHandler<CreatePayrollIntegrationRunCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;
    private readonly IPayrollSourceRepository _payrollSources;
    private readonly ITimeAttendanceProviderRepository _timeAttendanceProviders;
    private readonly IHrisSourceRepository _hrisSources;
    private readonly ITenantContext _tenantContext;

    public CreatePayrollIntegrationRunHandler(
        IPayrollIntegrationGovernanceRepository repository,
        IPayrollSourceRepository payrollSources,
        ITimeAttendanceProviderRepository timeAttendanceProviders,
        IHrisSourceRepository hrisSources,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _payrollSources = payrollSources;
        _timeAttendanceProviders = timeAttendanceProviders;
        _hrisSources = hrisSources;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePayrollIntegrationRunCommand request, CancellationToken ct)
    {
        if (request.Request.ContractVersion.Contains("stale", StringComparison.OrdinalIgnoreCase))
        {
            return Response<Guid>.Fail("Stale contract version is not accepted.", 409);
        }

        var referenceValidation = await PayrollIntegrationReferenceGuard.ValidateRunReferencesAsync(request.Request, _payrollSources, _timeAttendanceProviders, _hrisSources, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        var runCode = PayrollIntegrationGovernanceCodeNormalizer.Normalize(request.Request.RunCode);
        var idempotencyKey = request.Request.IdempotencyKey.Trim();
        if (await _repository.ExistsActiveRunCodeAsync(runCode, null, ct))
        {
            return Response<Guid>.Fail("A payroll integration run with this code already exists.", 409);
        }

        if (await _repository.ExistsActiveRunIdempotencyKeyAsync(idempotencyKey, null, ct))
        {
            return Response<Guid>.Fail("A payroll integration run with this idempotency key already exists.", 409);
        }

        var run = new PayrollIntegrationRun
        {
            TenantId = _tenantContext.TenantId,
            RunCode = runCode,
            PayrollSourceProfileId = request.Request.PayrollSourceProfileId,
            TimeAttendanceProviderProfileId = request.Request.TimeAttendanceProviderProfileId,
            HrisSourceProfileId = request.Request.HrisSourceProfileId,
            ContractVersion = request.Request.ContractVersion.Trim(),
            RunType = request.Request.RunType,
            Status = request.Request.Status,
            RequestedByActorId = request.Request.RequestedByActorId,
            CorrelationId = request.Request.CorrelationId.Trim(),
            IdempotencyKey = idempotencyKey,
            StartedAt = request.Request.StartedAt,
            CompletedAt = request.Request.CompletedAt,
            Summary = string.IsNullOrWhiteSpace(request.Request.Summary) ? null : request.Request.Summary.Trim()
        };

        await _repository.CreateRunAsync(run, ct);
        return Response<Guid>.Success(run.Id, 201);
    }
}
