using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Queries;

/// <summary>GET /api/inventory/transactions (contract <c>getTransactions</c>). Raw query strings; scope comes from the JWT.</summary>
public sealed record GetTransactionsQuery(
    string? ItemId,
    string? LotId,
    string? LocationId,
    string? From,
    string? To,
    string? Page,
    string? PageSize) : IRequest<Response<TransactionPageDto>>;
