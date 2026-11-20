using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Application.Features.BankMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>MOD-0289 DL-1 — the field rules (no store): BIC ISO 9362 + country, branch BIC, no echo in messages, search length.</summary>
public sealed class BankValidationTests
{
    private static IReadOnlyList<string> Errors(BankWriteRequest request) =>
        new BankWriteRequestValidator().Validate(request).Errors.Select(e => e.ErrorMessage).ToList();

    [Theory]
    [InlineData("ABCDTR22", "TR", true)]
    [InlineData("ABCDTR22XXX", "TR", true)]
    [InlineData("abcd tr22", "TR", true)] // blanks dropped, upper-cased
    [InlineData("ABCDDE22", "TR", false)] // characters 5-6 are the country
    [InlineData("ABCDTR2", "TR", false)] // 7 characters
    [InlineData("ABCDTR22X", "TR", false)] // 9 characters
    [InlineData("ABCD1R22", "TR", false)] // a digit where the country letters are
    [InlineData("1BCDTR22", "TR", false)] // a digit in the institution code
    [InlineData("ABCDTR2!", "TR", false)]
    public void Mod0289Dl1_AC02_Bank_BIC_follows_ISO_9362_and_carries_the_banks_country(string bic, string country, bool valid)
    {
        var errors = Errors(BankHarness.Bank(country: country, bic: bic));
        Assert.Equal(valid, errors.Count == 0);
        if (!valid) Assert.Contains(BankValidationMessages.BicInvalid, errors);
    }

    [Theory]
    [InlineData("T", false)]
    [InlineData("TUR", false)]
    [InlineData("t1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("tr", true)] // normalized to TR
    public void Mod0289Dl1_AC02_Bank_country_is_an_ISO_alpha2_format(string? country, bool valid) =>
        Assert.Equal(valid, Errors(BankHarness.Bank(country: country!)).Count == 0);

    [Fact]
    public void Mod0289Dl1_AC02_Bank_name_is_required_trimmed_and_at_most_200_without_control_characters()
    {
        Assert.Empty(Errors(BankHarness.Bank(name: new string('N', 200))));
        Assert.Contains(BankValidationMessages.NameInvalid, Errors(BankHarness.Bank(name: new string('N', 201))));
        Assert.Contains(BankValidationMessages.NameInvalid, Errors(BankHarness.Bank(name: "   ")));
        Assert.Contains(BankValidationMessages.NameInvalid, Errors(BankHarness.Bank(name: "Bad\u0007Name")));
    }

    [Theory]
    [InlineData(" 12-34 ab ", "1234AB", true)]
    [InlineData("A/B", null, false)]
    [InlineData("ÜBER", null, false)]
    public void Mod0289Dl1_AC02_National_bank_code_is_normalized_then_alphanumeric(string raw, string? normalized, bool valid)
    {
        var errors = Errors(BankHarness.Bank(national: raw));
        Assert.Equal(valid, errors.Count == 0);
        if (valid) Assert.Equal(normalized, BankRules.NormalizeCode(raw));
    }

    [Fact]
    public void Mod0289Dl1_AC02_National_code_is_at_most_35_characters()
    {
        Assert.Empty(Errors(BankHarness.Bank(national: new string('A', 35))));
        Assert.Contains(BankValidationMessages.NationalCodeInvalid, Errors(BankHarness.Bank(national: new string('A', 36))));
    }

    [Theory]
    [InlineData("ABCDTR22XXX", true)]
    [InlineData("ABCDTR22", false)] // a branch BIC is the 11-character form
    [InlineData("ABCDTR2XXXX", true)]
    [InlineData("ABCD1R22XXX", false)]
    public void Mod0289Dl1_AC02_Branch_BIC_is_the_11_character_ISO_form(string bic, bool valid)
    {
        var errors = new BankBranchWriteRequestValidator().Validate(BankHarness.Branch(bic: bic)).Errors;
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void Mod0289Dl1_AC02_Branch_BIC_first_8_characters_equal_the_banks_BIC_and_its_country_the_banks()
    {
        var bank = Bank.CreateDraft(Guid.NewGuid(), "GMG-BNK-1", "B", "TR", "ABCDTR22", null, false, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Null(BankBranchRules.Judge(bank, BankHarness.Branch(bic: "ABCDTR22123")));
        Assert.Equal(BankErrorCodes.ValidationFailed, BankBranchRules.Judge(bank, BankHarness.Branch(bic: "ZZZZTR22123")));
        // A bank without a BIC does not constrain the first 8; the country still must match the bank's.
        var noBic = Bank.CreateDraft(Guid.NewGuid(), "GMG-BNK-2", "B", "TR", null, null, false, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Null(BankBranchRules.Judge(noBic, BankHarness.Branch(bic: "ZZZZTR22123")));
        Assert.Equal(BankErrorCodes.ValidationFailed, BankBranchRules.Judge(noBic, BankHarness.Branch(bic: "ZZZZDE22123")));
        Assert.Equal(BankErrorCodes.ValidationFailed,
            BankBranchRules.Judge(bank, BankHarness.Branch() with { CountryCode = "DE" }));
    }

    [Fact]
    public void Mod0289Dl1_AC01_A_code_or_a_tenant_in_the_request_is_refused_not_ignored()
    {
        Assert.Equal([BankErrorCodes.CodeForbidden], Errors(BankHarness.Bank() with { Code = "BNK-0001" }));
        Assert.Contains(BankValidationMessages.TenantForbidden, Errors(BankHarness.Bank() with { TenantId = Guid.NewGuid() }));
        Assert.Contains(BankValidationMessages.TenantForbidden,
            new BankBranchWriteRequestValidator().Validate(BankHarness.Branch() with { TenantId = Guid.NewGuid() }).Errors.Select(e => e.ErrorMessage));
        Assert.Contains(BankValidationMessages.TenantForbidden,
            new GetBankListValidator().Validate(new GetBankListQuery { TenantId = Guid.NewGuid() }).Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Mod0289Dl1_AC01_Validator_messages_carry_a_fixed_key_and_never_echo_what_was_sent()
    {
        var sent = "ZZ-SECRET-BIC";
        var errors = Errors(BankHarness.Bank(name: "", country: "XX", bic: sent, national: "bad value!") with { Code = "BNK-SENT-CODE" });
        Assert.NotEmpty(errors);
        Assert.All(errors, message =>
        {
            Assert.DoesNotContain("{PropertyValue}", message);
            Assert.DoesNotContain("{", message);
            Assert.DoesNotContain(sent, message);
            Assert.DoesNotContain("BNK-SENT-CODE", message);
            Assert.StartsWith("MDM_BANK_", message);
        });
    }

    [Fact]
    public void Mod0289Dl1_AC59_Search_text_is_at_most_100_characters_after_trimming()
    {
        var validator = new GetBankListValidator();
        Assert.True(validator.Validate(new GetBankListQuery { Search = new string('a', 100) }).IsValid);
        Assert.True(validator.Validate(new GetBankListQuery { Search = "  " + new string('a', 100) + "  " }).IsValid);
        var tooLong = validator.Validate(new GetBankListQuery { Search = new string('a', 101) });
        Assert.Equal([BankValidationMessages.SearchTooLong], tooLong.Errors.Select(e => e.ErrorMessage));
        Assert.False(new GetBankBranchListValidator().Validate(new GetBankBranchListQuery { Search = new string('a', 101) }).IsValid);
    }

    [Fact]
    public void Mod0289Dl1_AC05_The_state_table_allows_exactly_the_documented_moves()
    {
        Bank At(BankLifecycleStatus status, DateTimeOffset? activatedAt = null)
        {
            var bank = Bank.CreateDraft(Guid.NewGuid(), "C", "N", "TR", null, null, false, Guid.NewGuid(), DateTimeOffset.UtcNow);
            bank.Status = status;
            bank.ActivatedAt = activatedAt;
            return bank;
        }

        var draft = At(BankLifecycleStatus.Draft);
        Assert.True(draft.CanUpdate);
        Assert.False(draft.UpdateIsLimited);
        Assert.True(draft.CanActivate);
        Assert.False(draft.CanDeactivate);
        Assert.True(draft.CanDelete);
        var active = At(BankLifecycleStatus.Active, DateTimeOffset.UtcNow);
        Assert.True(active.CanUpdate);
        Assert.True(active.UpdateIsLimited);
        Assert.False(active.CanActivate);
        Assert.True(active.CanDeactivate);
        Assert.False(active.CanDelete);
        var inactive = At(BankLifecycleStatus.Inactive, DateTimeOffset.UtcNow);
        Assert.False(inactive.CanUpdate);
        Assert.True(inactive.CanActivate);
        Assert.False(inactive.CanDeactivate);
        Assert.False(inactive.CanDelete);
        // A Draft that was activated once and is a Draft again cannot happen — and even if stored so, it is not deletable.
        Assert.False(At(BankLifecycleStatus.Draft, DateTimeOffset.UtcNow).CanDelete);
    }
}
