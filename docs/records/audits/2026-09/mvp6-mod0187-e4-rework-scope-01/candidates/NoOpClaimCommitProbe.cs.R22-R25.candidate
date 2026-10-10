using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace Diten.SupplyChainService.Persistence.Features.Claims;
public sealed class NoOpClaimCommitProbe : IClaimCommitProbe
{
    public Guid NewId() => Guid.NewGuid();
    public Task AtAsync(string stage, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class EvidenceClaimCommitProbe : IClaimCommitProbe
{
    private static readonly HashSet<string> AllowedStages =
        ["aggregate", "receipt", "audit", "outbox", "beforeCommit", "afterCommit"];
    private readonly Guid? fixedId;
    private readonly string? faultStage;
    private readonly ILogger<EvidenceClaimCommitProbe> logger;

    private EvidenceClaimCommitProbe(Guid? fixedId, string? faultStage, ILogger<EvidenceClaimCommitProbe> logger)
    {
        this.fixedId = fixedId;
        this.faultStage = faultStage;
        this.logger = logger;
    }

    public static EvidenceClaimCommitProbe From(IConfiguration configuration, ILogger<EvidenceClaimCommitProbe> logger)
    {
        var fixedIdText = configuration["Claims:EvidenceProbe:FixedId"];
        var stage = configuration["Claims:EvidenceProbe:FaultStage"];
        Guid? fixedId = null;
        if (fixedIdText is not null)
        {
            if (!Guid.TryParseExact(fixedIdText, "D", out var parsed))
                throw new InvalidOperationException("Claims evidence probe FixedId must be a hyphenated UUID.");
            fixedId = parsed;
        }
        if (stage is not null && !AllowedStages.Contains(stage))
            throw new InvalidOperationException("Claims evidence probe FaultStage is not supported.");
        if (fixedId is null && stage is null)
            throw new InvalidOperationException("ClaimsEvidence requires an explicit FixedId or FaultStage.");
        return new EvidenceClaimCommitProbe(fixedId, stage, logger);
    }

    public Guid NewId() => fixedId ?? Guid.NewGuid();

    public Task AtAsync(string stage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Claims evidence probe reached {Stage}", stage);
        if (string.Equals(stage, faultStage, StringComparison.Ordinal))
            throw new TimeoutException("Claims evidence probe injected a bounded test failure.");
        return Task.CompletedTask;
    }
}
