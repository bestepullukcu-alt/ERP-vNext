using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>WP-CYC-UI-1 — what points at this period (capacity, campaigns, planning sessions, planned visits, monthly
/// demand). A READ: it creates and changes nothing.</summary>
public sealed record GetCyclePeriodUsageQuery(Guid CyclePeriodId) : IRequest<Response<CyclePeriodUsageDto>>;
