using System.Text.RegularExpressions;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;

namespace Diten.AuthService.Api.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX2 item 2 — the NAMED reason an operational command failed, for the operator (the live Windows
/// measurement rests on these names: OPERATION_ALREADY_RUNNING, OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT, …). Only a fixed
/// code is ever emitted: a secret channel's refusal reason, or an exception message that is entirely an UPPER_SNAKE code
/// (the shape every named refusal in these commands has). Anything else — a token, a path, a connection string, a
/// library's sentence — yields no reason. The exit-code contract does not change.
/// </summary>
public static partial class OperationalReason
{
    // FIX3: \z, not $ — $ also matches before a trailing \n, so "CODE\n" passed as a code.
    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{2,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    public static string? From(Exception exception) => exception switch
    {
        SecretChannelRejectedException rejected => CodeShape().IsMatch(rejected.Reason) ? rejected.Reason : null,
        _ when CodeShape().IsMatch(exception.Message) => exception.Message,
        _ => null
    };
}
