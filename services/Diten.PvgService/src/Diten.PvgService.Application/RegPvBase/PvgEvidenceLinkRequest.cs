using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgEvidenceLinkRequest(
    PvgIntakeOperation Operation,
    string? TenantId,
    string? CaseId,
    string? ActorId,
    string? EvidenceReference,
    string? EvidenceText);
