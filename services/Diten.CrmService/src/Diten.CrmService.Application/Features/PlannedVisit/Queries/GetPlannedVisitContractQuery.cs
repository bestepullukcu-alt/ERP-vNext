using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Queries;

/// <summary>Publishes the feature flags, in-domain vocabulary, supported filters, limits, error codes, permissions and
/// limitations, so a contract-driven UI needs no hardcoded list anywhere.</summary>
public sealed record GetPlannedVisitContractQuery : IRequest<Response<PlannedVisitContractDto>>;
