using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Commands;

/// <summary>Retires a capacity. A SOFT archive: the inputs an old estimate was made from stay readable, and there is
/// no delete endpoint anywhere in this feature.</summary>
public sealed record ArchiveCycleCapacityCommand(
    Guid CycleCapacityId, int? ExpectedVersion) : IRequest<Response<bool>>;
