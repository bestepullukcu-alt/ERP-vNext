using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

internal static class PayrollIntegrationReferenceGuard
{
    public static async Task<Response<NoContent>> ValidateRunReferencesAsync(
        PayrollIntegrationRunRequest request,
        IPayrollSourceRepository payrollSources,
        ITimeAttendanceProviderRepository timeAttendanceProviders,
        IHrisSourceRepository hrisSources,
        CancellationToken ct)
    {
        if (await payrollSources.GetExternalSystemProfileByIdAsync(request.PayrollSourceProfileId, ct) == null)
        {
            return Response<NoContent>.Fail("MOD-0279 payroll source reference not found for current tenant.", 404);
        }

        if (request.TimeAttendanceProviderProfileId.HasValue
            && await timeAttendanceProviders.GetProviderProfileByIdAsync(request.TimeAttendanceProviderProfileId.Value, ct) == null)
        {
            return Response<NoContent>.Fail("MOD-0280 time-attendance provider reference not found for current tenant.", 404);
        }

        if (request.HrisSourceProfileId.HasValue
            && await hrisSources.GetSourceProfileByIdAsync(request.HrisSourceProfileId.Value, ct) == null)
        {
            return Response<NoContent>.Fail("MOD-0251 HRIS source reference not found for current tenant.", 404);
        }

        return Response<NoContent>.Success(204);
    }

    public static async Task<Response<NoContent>> ValidateSourceLinkAsync(
        PayrollIntegrationSourceLinkRequest request,
        IPayrollSourceRepository payrollSources,
        ITimeAttendanceProviderRepository timeAttendanceProviders,
        IHrisSourceRepository hrisSources,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions,
        CancellationToken ct)
    {
        if (request.LinkState == PayrollIntegrationLinkState.Deferred)
        {
            return Response<NoContent>.Success(204);
        }

        return request.SourceType switch
        {
            PayrollIntegrationSourceType.PayrollSource when await payrollSources.GetExternalSystemProfileByIdAsync(request.SourceReferenceId, ct) != null => Response<NoContent>.Success(204),
            PayrollIntegrationSourceType.TimeAttendanceProvider when await timeAttendanceProviders.GetProviderProfileByIdAsync(request.SourceReferenceId, ct) != null => Response<NoContent>.Success(204),
            PayrollIntegrationSourceType.HrisSource when await hrisSources.GetSourceProfileByIdAsync(request.SourceReferenceId, ct) != null => Response<NoContent>.Success(204),
            PayrollIntegrationSourceType.OrganizationDirectory when await organizationUnits.GetByIdAsync(request.SourceReferenceId, ct) != null => Response<NoContent>.Success(204),
            PayrollIntegrationSourceType.PositionDirectory when await positions.GetByIdAsync(request.SourceReferenceId, ct) != null => Response<NoContent>.Success(204),
            PayrollIntegrationSourceType.PersonDirectory => Response<NoContent>.Fail("MOD-0288 Person reference validation is not available in this service.", 404),
            _ => Response<NoContent>.Fail("Source reference not found for current tenant.", 404)
        };
    }

    public static Response<NoContent> RejectBlindApprovedState(PayrollIntegrationControlState state)
    {
        return state is PayrollIntegrationControlState.Governed or PayrollIntegrationControlState.Approved or PayrollIntegrationControlState.Mapped
            ? Response<NoContent>.Fail("Blind references cannot be marked Governed, Approved, or Mapped.", 409)
            : Response<NoContent>.Success(204);
    }
}
