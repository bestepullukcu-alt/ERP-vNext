namespace Diten.AuthService.Domain.Enums;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — what KIND of account a tenant user record is, as a fact AuthService asserts
/// (owner decision 2026-09-11). Persisted as its number (Auth serializes enums as Int32), carried over the API as
/// its NAME.
///
/// <para><see cref="Unknown"/> is 0 on purpose: every user document written before this field existed deserializes
/// to Unknown without being rewritten, and every automatic creation path (self-service register, tenant-admin
/// invitation, platform-admin provisioning, the seed) leaves an account here explicitly. Nobody becomes
/// <see cref="Human"/> by structure — only a holder of <c>auth.users.account-kind.manage</c>, through
/// <c>SetAccountKindCommand</c> (or a permitted explicit kind on create), moves an account out of Unknown.</para>
///
/// <para>The decision "may this account own a portfolio" is NOT made here. AuthService reports the fact
/// (same tenant, active, kind); the consumer (PPM) applies its own rule to it.</para>
/// </summary>
public enum AccountKind
{
    /// <summary>Not yet classified. The default for every existing and every automatically created account.</summary>
    Unknown = 0,

    /// <summary>A person's account.</summary>
    Human = 1,

    /// <summary>A non-person (integration / service) account.</summary>
    Service = 2
}
