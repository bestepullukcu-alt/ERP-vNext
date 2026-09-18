using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>Soft delete tek supplier (public SupplierId). Hard delete YOK (MOD-0140 §8).</summary>
public sealed record DeleteSupplierCommand(string SupplierId) : IRequest<Response<bool>>;
