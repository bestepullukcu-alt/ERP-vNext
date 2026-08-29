namespace Diten.AuthService.Application.DTOs;

// MaskedName / MaskedEmail are privacy-preserving identity hints (e.g. "D***a", "a***@diten.com") returned to a caller
// that may reference — but is not permitted to fully read — a user. They are optional so existing 2-argument
// constructions keep compiling.
public sealed record TenantUserLookupValidationDto(
    Guid UserId,
    bool Referenceable,
    string? MaskedName = null,
    string? MaskedEmail = null);
