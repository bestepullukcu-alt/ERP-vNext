using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Queries;

/// <summary>listContracts (contract GET /api/contracts). Tenant+LE filtreli; opsiyonel supplierId + status + cursor.</summary>
public sealed record ListContractsQuery(
    string? SupplierId = null,
    ContractStatus? Status = null,
    string? Cursor = null) : IRequest<Response<ContractListResultDto>>;
