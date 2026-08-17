using Diten.Platform.Application.Features.Billing;
using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Application.Features.Billing.Validators;
using Xunit;

namespace Diten.Platform.Application.Tests.Billing;

public sealed class BillingRulesTests
{
    [Theory]
    [InlineData(10.125, 10.13)]
    [InlineData(10.124, 10.12)]
    [InlineData(10.115, 10.12)]
    public void Billing_money_uses_two_decimal_half_up_rounding(decimal input, decimal expected)
    {
        Assert.Equal(expected, BillingMoney.Round(input));
    }

    [Fact]
    public void Create_payment_validator_requires_positive_amount_and_idempotency_key()
    {
        var validator = new CreatePaymentCommandValidator();
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            new CreatePaymentRequest(string.Empty, 0, "USD", null));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("IdempotencyKey", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("AppliedAmount", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_refund_validator_rejects_negative_refund()
    {
        var validator = new CreateRefundCommandValidator();
        var command = new CreateRefundCommand(
            Guid.NewGuid(),
            new CreateRefundRequest(-1, "USD", "Correction"));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("RefundAmount", StringComparison.Ordinal));
    }
}
