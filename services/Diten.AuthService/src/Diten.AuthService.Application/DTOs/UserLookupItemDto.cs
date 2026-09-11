namespace Diten.AuthService.Application.DTOs;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — one row of the user lookup a reference picker consumes: the id and a display
/// label (first name + last name) and NOTHING else. No email, no roles, no status, no kind — the lookup answers
/// "which person is this", not "tell me about this person". Adding a field here widens what
/// <c>auth.users.lookup</c> discloses; do not.
/// </summary>
public sealed record UserLookupItemDto(Guid UserId, string DisplayLabel);
