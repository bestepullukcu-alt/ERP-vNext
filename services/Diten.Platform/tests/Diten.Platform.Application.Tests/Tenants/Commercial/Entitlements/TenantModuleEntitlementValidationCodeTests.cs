using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Validators;
using FluentValidation;
using FluentValidation.Results;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 FIX1 item 5 — the server side of the Modules tab's bridge. Every validator rule on the tab's five commands
/// answers with a curated code from <see cref="TenantModuleEntitlementRefusalCodes"/>; the screen's half
/// (<c>frontend/Diten.Web/tests/platform-tenant-modules-screen.test.js</c>) demands a sentence in en and tr for every
/// constant of that class. Together: no refusal of these endpoints — business or validation — reaches the reader as
/// the general sentence with the field's message lost.
/// </summary>
public sealed class TenantModuleEntitlementValidationCodeTests
{
    private static readonly IValidator[] Validators =
    [
        new AddTenantModuleEntitlementCommandValidator(),
        new DisableTenantModuleEntitlementCommandValidator(),
        new EnableTenantModuleEntitlementCommandValidator(),
        new UpdateTenantModuleEntitlementExpiryCommandValidator(),
        new RemoveTenantManualModuleOverrideCommandValidator()
    ];

    [Fact]
    public void Every_rule_on_the_modules_tab_commands_answers_with_a_code_the_screen_has_a_sentence_for()
    {
        var components = Validators
            .SelectMany(validator => validator.CreateDescriptor().Rules
                .SelectMany(rule => rule.Components.Select(component => (Validator: validator.GetType().Name, rule.PropertyName, component.ErrorCode))))
            .ToList();

        Assert.True(components.Count >= 19, $"Expected every rule to be measured, found {components.Count}.");
        Assert.All(components, component =>
        {
            Assert.False(string.IsNullOrWhiteSpace(component.ErrorCode), $"{component.Validator}.{component.PropertyName} has no curated code.");
            Assert.Contains(component.ErrorCode, TenantModuleEntitlementRefusalCodes.All);
        });
    }

    [Fact]
    public void A_curated_code_reaches_the_response_verbatim()
    {
        // GlobalExceptionHandler writes ValidationReasonCode.From(first failure) as reason_code.
        foreach (var code in TenantModuleEntitlementRefusalCodes.All)
        {
            Assert.Equal(code, ValidationReasonCode.From(new ValidationFailure("Request.Reason", "x") { ErrorCode = code }));
        }
    }
}
