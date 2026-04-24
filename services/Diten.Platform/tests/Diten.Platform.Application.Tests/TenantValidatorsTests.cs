using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Validators;
using FluentValidation;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class TenantValidatorsTests
{
    [Fact]
    public void RegisterTenantValidator_ShouldFail_WhenNameOrDomainMissing()
    {
        var validator = new RegisterTenantCommandValidator();
        var result = validator.Validate(new RegisterTenantCommand("", ""));

        Assert.False(result.IsValid);
    }
}
