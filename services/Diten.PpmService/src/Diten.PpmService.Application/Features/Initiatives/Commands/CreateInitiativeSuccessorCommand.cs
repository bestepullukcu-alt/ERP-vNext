using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record CreateInitiativeSuccessorCommand(Guid TerminalId, string Code, string Name,
    string? Description, Guid? PortfolioId, string? InitiativeTypeCode, string? PriorityCode,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, int ExpectedTerminalVersion)
    : IRequest<Response<InitiativeV2Dto>>;
