using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Queries;

/// <summary>Publishes the FU02 contract — vocabulary, feature flags, limits, error codes, permissions + the load-bearing
/// limitations — so a contract-driven UI needs no hardcoded vocabulary or ceiling.</summary>
public sealed record GetVisitReportContractQuery : IRequest<Response<VisitReportContractDto>>;
