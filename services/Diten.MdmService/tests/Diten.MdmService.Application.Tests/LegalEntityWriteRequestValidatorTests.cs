using Diten.MdmService.Application.Features.LegalEntity.Validators;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityWriteRequestValidatorTests
{
    private readonly LegalEntityWriteRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_code_fails()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(code: ""));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"line1\":\"x\",\"city\":\"y\"}")]          // missing country
    [InlineData("{\"line1\":\"x\",\"city\":\"\",\"country\":\"TR\"}")] // empty city
    [InlineData("[]")]                                          // not an object
    public void Invalid_registered_address_json_fails(string addressJson)
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(registeredAddressJson: addressJson));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Branch_without_parent_fails()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(
            organizationRoleCode: "BRANCH", parentLegalEntityId: null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(Features.LegalEntity.LegalEntityWriteRequest.ParentLegalEntityId));
    }

    [Fact]
    public void Branch_with_parent_passes()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(
            organizationRoleCode: "BRANCH", parentLegalEntityId: Guid.NewGuid()));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Legal_entity_role_does_not_require_parent()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(
            organizationRoleCode: "LEGALENTITY", parentLegalEntityId: null));
        Assert.True(result.IsValid);
    }

    // İŞ1 — the wizard defers OrganizationRole and OMITS it (null). The request must validate (the mapping defaults
    // it to LEGALENTITY). OrganizationRoleCode being non-nullable was making ASP.NET reject this with a 400 before
    // it ever reached the validator; nullable + this test lock in that an omitted role is accepted.
    [Fact]
    public void Omitted_organization_role_passes()
    {
        var result = _validator.Validate(LegalEntityTestData.ValidRequest(
            organizationRoleCode: null, parentLegalEntityId: null, registeredAddressJson: null));
        Assert.True(result.IsValid);
    }
}
