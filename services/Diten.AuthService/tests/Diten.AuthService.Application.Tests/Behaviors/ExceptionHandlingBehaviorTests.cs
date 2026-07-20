using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Behaviors;

// FIX-PASSWORD-POLICY-ERROR-SURFACE — a handler-thrown FluentValidation failure (e.g. the tenant password policy)
// must surface as a 400 with its SPECIFIC messages, not be swallowed into the generic 500. Other exceptions stay 500.
public sealed class ExceptionHandlingBehaviorTests
{
    private sealed record TestRequest;

    private static ExceptionHandlingBehavior<TestRequest, Response<string>> Behavior() =>
        new(NullLogger<ExceptionHandlingBehavior<TestRequest, Response<string>>>.Instance);

    [Fact]
    public async Task Validation_exception_becomes_400_with_joined_specific_messages()
    {
        RequestHandlerDelegate<Response<string>> next = () => throw new ValidationException(new[]
        {
            new ValidationFailure("Password", "Password must be at least 12 characters."),
            new ValidationFailure("Password", "Password must contain at least one special character.")
        });

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        var joined = string.Join(" ", result.Errors);
        Assert.Contains("at least 12 characters", joined);
        Assert.Contains("special character", joined);
        Assert.DoesNotContain("unexpected error", joined);
    }

    [Fact]
    public async Task Generic_exception_stays_500_generic()
    {
        RequestHandlerDelegate<Response<string>> next = () => throw new InvalidOperationException("boom internal");

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(500, result.StatusCode);
        Assert.Contains("An unexpected error occurred.", result.Errors);
        Assert.DoesNotContain(result.Errors, e => e.Contains("boom internal")); // internal detail not leaked
    }

    [Fact]
    public async Task No_exception_passes_the_result_through_untouched()
    {
        var expected = Response<string>.Success("ok", 200);
        RequestHandlerDelegate<Response<string>> next = () => Task.FromResult(expected);

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Validation_exception_with_no_messages_falls_back_to_exception_message()
    {
        RequestHandlerDelegate<Response<string>> next = () => throw new ValidationException("policy rejected");

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.Errors, e => e.Contains("policy rejected"));
    }

    [Fact]
    public async Task Coded_password_failures_serialize_errorCodes_alongside_the_english_detail()
    {
        RequestHandlerDelegate<Response<string>> next = () => throw new ValidationException(new[]
        {
            new ValidationFailure("Password", "Password must be at least 10 characters.")
            {
                ErrorCode = PasswordErrorCodes.TooShort,
                CustomState = new Dictionary<string, string> { ["minLength"] = "10" }
            },
            new ValidationFailure("Password", "Password must contain at least one uppercase letter.")
            {
                ErrorCode = PasswordErrorCodes.NeedsUppercase
            }
        });

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);

        // machine-readable codes + params
        Assert.Collection(result.ErrorCodes,
            e =>
            {
                Assert.Equal(PasswordErrorCodes.TooShort, e.Code);
                Assert.NotNull(e.Params);
                Assert.Equal("10", e.Params!["minLength"]);
            },
            e =>
            {
                Assert.Equal(PasswordErrorCodes.NeedsUppercase, e.Code);
                Assert.True(e.Params is null || e.Params.Count == 0);
            });

        // English fallback `detail` still present for back-compat / logging
        var joined = string.Join(" ", result.Errors);
        Assert.Contains("at least 10 characters", joined);
        Assert.Contains("uppercase letter", joined);
    }

    [Fact]
    public async Task Non_password_validation_failures_carry_no_errorCodes()
    {
        RequestHandlerDelegate<Response<string>> next = () => throw new ValidationException(new[]
        {
            // FluentValidation default error code — must NOT leak as a machine-readable code.
            new ValidationFailure("Email", "Email is required.") { ErrorCode = "NotEmptyValidator" }
        });

        var result = await Behavior().Handle(new TestRequest(), next, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(result.ErrorCodes);
        Assert.Contains(result.Errors, e => e.Contains("Email is required."));
    }
}
