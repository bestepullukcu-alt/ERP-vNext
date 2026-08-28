using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Domain.Entities;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Services;

/// <summary>
/// Turns an estimate into its HTTP answer, identically for the saved and the preview surfaces.
/// <para>An unresolved estimate is <b>503</b>, not 200: the caller asked a question the platform could not answer right
/// now, and a 200 would let a UI render "—" as though it were the number. The body still carries the resolution and the
/// reason codes, so the UI can explain WHY rather than showing a generic error.</para>
/// </summary>
public static class CycleCapacityCalculationResponse
{
    public static Response<CycleCapacityCalculationDto> From(
        CapacityEntity capacity, CycleCapacityEstimator.Result estimate)
    {
        var dto = CycleCapacityMapper.ToCalculation(
            capacity, estimate.CalendarLegalEntityId, estimate.Calculation);

        if (string.Equals(dto.Resolution, CycleCapacityResolutions.Resolved, StringComparison.Ordinal))
        {
            return Response<CycleCapacityCalculationDto>.Success(dto);
        }

        return new Response<CycleCapacityCalculationDto>
        {
            Data = dto,
            Errors = new List<string> { dto.Reason }.Concat(dto.ReasonCodes).ToList(),
            StatusCode = 503,
            IsSuccessful = false
        };
    }
}
