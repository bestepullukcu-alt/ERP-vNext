namespace Diten.AuthService.Application.DTOs;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the account FACT AuthService asserts about one tenant user, for a consumer
/// (PPM) that decides eligibility itself. Nothing here is a verdict: <c>Active</c> and <c>AccountKind</c> are
/// observations, and "may this account own a portfolio" stays the consumer's rule (same tenant + active + Human).
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="Active">Whether the account is currently active (an inactive account still answers 200).</param>
/// <param name="AccountKind">The enum NAME — <c>Unknown</c>, <c>Human</c> or <c>Service</c>; never the number.</param>
/// <param name="AssertedAt">When AuthService produced this assertion (UTC).</param>
/// <param name="UserUpdatedAt">The user record's last update stamp, so a consumer can detect staleness.</param>
public sealed record AccountAssertionDto(
    Guid UserId,
    bool Active,
    string AccountKind,
    DateTimeOffset AssertedAt,
    DateTimeOffset? UserUpdatedAt);
