namespace Diten.AuthService.Application.DTOs;

/// <summary>
/// WP-INFRA-AUTH-DISPLAY-LABEL-01 — a display/history label for one tenant user, for a consumer (PPM) that needs to
/// show "who" against an id it already holds. This is NOT eligibility, activity, or authorization evidence: an
/// inactive account still answers 200 with whatever label it has (see <see cref="GetAccountAssertionQuery"/> for the
/// activity fact, and <see cref="LookupUsersQuery"/> for the reference-picker search — those are the endpoints that
/// answer other questions). AuthService does not truncate the label and applies no length limit (no write-side limit
/// on first/last name either — BL-376): a consumer that needs a shorter string applies its own truncation. AuthService
/// encodes no consumer's policy here.
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="DisplayLabel">"First Last" (collapsing a missing half); null when there is no name at all.</param>
/// <param name="LabelState"><see cref="UserDisplayLabelStates.Named"/> or <see cref="UserDisplayLabelStates.Unnamed"/>.</param>
public sealed record UserDisplayLabelDto(Guid UserId, string? DisplayLabel, string LabelState);

public static class UserDisplayLabelStates
{
    public const string Named = "Named";
    public const string Unnamed = "Unnamed";
}
