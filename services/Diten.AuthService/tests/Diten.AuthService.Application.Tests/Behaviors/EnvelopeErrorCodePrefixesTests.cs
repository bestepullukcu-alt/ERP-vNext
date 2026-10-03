using Diten.AuthService.Application.Common;
using FluentValidation.Results;

namespace Diten.AuthService.Application.Tests.Behaviors;

/// <summary>
/// WP-USERS-ERROR-CODES-01 — the one prefix list and the one extraction helper both doors use
/// (ExceptionHandlingBehavior and the Api's GlobalExceptionHandler), measured directly.
/// </summary>
public sealed class EnvelopeErrorCodePrefixesTests
{
    [Theory]
    [InlineData("password.too_short")]
    [InlineData("USER_NOT_FOUND")]
    [InlineData("ROLE_NAME_TAKEN")]
    public void Allows_a_code_that_starts_with_a_listed_prefix(string code)
        => Assert.True(EnvelopeErrorCodePrefixes.Allows(code));

    [Theory]
    [InlineData("USERNAME_X")]      // "USER" is not the prefix — "USER_" is
    [InlineData("user_x")]          // ordinal: case matters
    [InlineData("Password.x")]
    [InlineData("ROLEPLAY")]
    [InlineData("NotEmptyValidator")]
    [InlineData("X_USER_NOT_FOUND")] // a prefix, not a substring
    [InlineData("")]
    [InlineData(null)]
    public void Allows_nothing_else(string? code)
        => Assert.False(EnvelopeErrorCodePrefixes.Allows(code));

    [Fact]
    public void Extract_keeps_the_order_skips_what_is_not_allowed_and_carries_the_params()
    {
        var maxLength = new Dictionary<string, string> { ["maxLength"] = "128" };

        var codes = EnvelopeErrorCodePrefixes.Extract(
        [
            Failure("USER_LAST_NAME_REQUIRED"),
            Failure("NotEmptyValidator"),
            Failure("password.too_long", maxLength),
            Failure(null),
            Failure("ROLE_NAME_TAKEN"),
        ]);

        Assert.Equal(["USER_LAST_NAME_REQUIRED", "password.too_long", "ROLE_NAME_TAKEN"], codes.Select(c => c.Code));
        Assert.Null(codes[0].Params);
        Assert.Same(maxLength, codes[1].Params);
    }

    [Fact]
    public void Extract_reports_a_repeated_code_once_with_the_params_of_its_first_failure()
    {
        var first = new Dictionary<string, string> { ["minLength"] = "10" };
        var second = new Dictionary<string, string> { ["minLength"] = "99" };

        var codes = EnvelopeErrorCodePrefixes.Extract(
            [Failure("password.too_short", first), Failure("USER_EMAIL_INVALID"), Failure("password.too_short", second)]);

        Assert.Equal(["password.too_short", "USER_EMAIL_INVALID"], codes.Select(c => c.Code));
        Assert.Same(first, codes[0].Params);
    }

    [Fact]
    public void Extract_of_only_unlisted_codes_is_empty()
        => Assert.Empty(EnvelopeErrorCodePrefixes.Extract([Failure("NotEmptyValidator"), Failure("EmailValidator")]));

    private static ValidationFailure Failure(string? code, IReadOnlyDictionary<string, string>? state = null)
        => new("Property", "English fallback.") { ErrorCode = code, CustomState = state };
}
