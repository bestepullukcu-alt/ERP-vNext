using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Commands;

/// <summary>Soft delete tek GRN (public GrnId; ASSUMPTION-GRN-03). Yalnız Draft silinebilir (Posted → 409;
/// düzeltme reverse ile). Hard delete YOK.</summary>
public sealed record DeleteGrnCommand(string GrnId) : IRequest<Response<bool>>;
