using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgFieldSecurityRequest(
    PvgIntakeOperation Operation,
    string Surface,
    string FieldName,
    string? TenantId,
    string? ActorId,
    string? RawFieldValue,
    string? FreeText);
