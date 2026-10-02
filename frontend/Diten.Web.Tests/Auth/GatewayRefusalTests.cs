using Diten.Web.Services.Auth;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// WP-USERS-ERROR-CODES-01, acceptance round 2 (item 13) — <see cref="GatewayRefusal.Read"/>, the one reader of a
/// refused hop: every code is kept, and a refusal that names NO code is always at least one failure — an empty body,
/// an HTML error page or a ProblemDetails without codes must never read as "nothing went wrong".
/// </summary>
public sealed class GatewayRefusalTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("""{"title":"Server error","status":500,"detail":"An unexpected error occurred.","traceId":"t"}""")]
    [InlineData("""{"title":"Forbidden","status":403}""")]
    [InlineData("""{"data":null,"statusCode":400,"isSuccessful":false,"errors":[],"errorCodes":[]}""")]
    [InlineData("""{"isSuccessful":false,"errorCodes":[{"code":""},{"nope":1},"text"]}""")]
    public void A_refusal_that_names_no_code_is_one_uncoded_failure_never_zero(string? raw)
    {
        var refusal = GatewayRefusal.Read(raw);

        Assert.Empty(refusal.Codes);
        Assert.Equal(1, refusal.FailureCount);
        Assert.True(refusal.HasUncoded);
    }

    [Fact]
    public void An_envelope_with_one_error_and_its_code_is_fully_coded()
    {
        var refusal = GatewayRefusal.Read("""{"isSuccessful":false,"statusCode":409,"errors":["Email is already in use."],"errorCodes":[{"code":"USER_EMAIL_TAKEN"}]}""");

        Assert.Equal(["USER_EMAIL_TAKEN"], refusal.Codes.Select(c => c.Code));
        Assert.Null(refusal.Codes[0].Params);
        Assert.False(refusal.HasUncoded);
    }

    [Fact]
    public void An_envelope_with_more_errors_than_codes_has_an_uncoded_failure()
    {
        var refusal = GatewayRefusal.Read("""{"isSuccessful":false,"errors":["one","two"],"errorCodes":[{"code":"USER_NOT_FOUND"}]}""");

        Assert.Equal(2, refusal.FailureCount);
        Assert.True(refusal.HasUncoded);
    }

    [Fact]
    public void A_validators_body_counts_one_failure_per_Severity_line_and_keeps_every_code_with_its_params()
    {
        const string body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- Email: A valid email address is required. Severity: Error\n -- NewPassword: Password can be at most 128 characters. Severity: Error\n -- Token: Reset token is required. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128","ignored":5}},{"code":"password.reset_token_required","params":null}]}""";

        var refusal = GatewayRefusal.Read(body);

        Assert.Equal(["password.too_long", "password.reset_token_required"], refusal.Codes.Select(c => c.Code));
        Assert.Equal("128", Assert.Single(refusal.Codes[0].Params!).Value); // string params only
        Assert.Null(refusal.Codes[1].Params);
        Assert.Equal(3, refusal.FailureCount);
        Assert.True(refusal.HasUncoded); // three failures, two codes
    }

    [Fact]
    public void A_validators_body_whose_every_failure_is_coded_has_nothing_uncoded()
    {
        var refusal = GatewayRefusal.Read("""{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- FirstName: Ad boş bırakılamaz. Severity: Error","traceId":"t","errorCodes":[{"code":"USER_FIRST_NAME_REQUIRED","params":null}]}""");

        Assert.Equal(1, refusal.FailureCount);
        Assert.False(refusal.HasUncoded);
    }
}
