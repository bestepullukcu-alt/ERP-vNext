namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeLifecycleResult(InitiativeV2Dto Initiative, InitiativeClosureDto? Closure,
    IReadOnlyList<string> Warnings);
