namespace Diten.AuthService.Application.DTOs;

public sealed record TenantUserLookupValidationDto(Guid UserId, bool Referenceable);
