namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeClassificationResult(InitiativeAuthorityDisposition Disposition,
    IReadOnlyList<InitiativeContractOption> Options);
