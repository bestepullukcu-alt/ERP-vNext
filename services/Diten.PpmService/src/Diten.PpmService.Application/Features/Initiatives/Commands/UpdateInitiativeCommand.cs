using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record UpdateInitiativeCommand(Guid Id, string Code, string Name, string? Description, Guid? PortfolioId,
    string? InitiativeTypeCode, int ExpectedVersion, string? PriorityCode = null,
    DateOnly? PlannedStartDate = null, DateOnly? PlannedEndDate = null) : IRequest<Response<InitiativeV2Dto>>;
