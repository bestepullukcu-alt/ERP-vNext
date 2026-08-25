using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataProviderOptionsTests
{
    [Fact]
    public void ReferenceTenantId_HasNoDefault()
    {
        var options = new BusinessReferenceDataProviderOptions();

        Assert.Null(options.ReferenceTenantId);
        Assert.False(options.TryGetReferenceTenantId(out var resolved));
        Assert.Equal(Guid.Empty, resolved);
    }

    [Fact]
    public void ReferenceTenantId_RejectsEmptyAndAcceptsNonEmptyGuid()
    {
        var empty = new BusinessReferenceDataProviderOptions { ReferenceTenantId = Guid.Empty };
        var expected = Guid.Parse("9302a46f-8136-458b-9e54-b2859405ae2e");
        var valid = new BusinessReferenceDataProviderOptions { ReferenceTenantId = expected };

        Assert.False(empty.TryGetReferenceTenantId(out _));
        Assert.True(valid.TryGetReferenceTenantId(out var resolved));
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Validator_ClassifiesMissingEmptyAndValidValuesWithoutStartupValidation()
    {
        var validator = new BusinessReferenceDataProviderOptionsValidator();

        Assert.True(validator.Validate(null, new BusinessReferenceDataProviderOptions()).Failed);
        Assert.True(validator.Validate(
            null,
            new BusinessReferenceDataProviderOptions { ReferenceTenantId = Guid.Empty }).Failed);
        Assert.True(validator.Validate(
            null,
            new BusinessReferenceDataProviderOptions { ReferenceTenantId = Guid.NewGuid() }).Succeeded);
    }

    [Fact]
    public void Binding_DoesNotFallBackToCatalogLoadTenant()
    {
        var catalogTenantId = Guid.Parse("12bb0fa1-eb2f-461a-a03f-d0838c14894f");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessReferenceData:CatalogLoad:TenantId"] = catalogTenantId.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.Configure<BusinessReferenceDataProviderOptions>(
            configuration.GetSection(BusinessReferenceDataProviderOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BusinessReferenceDataProviderOptions>>().Value;

        Assert.Null(options.ReferenceTenantId);
        Assert.False(options.TryGetReferenceTenantId(out _));
    }

    [Fact]
    public void Binding_InvalidGuidRemainsInvalidInsteadOfUsingFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId"] = "not-a-guid",
                ["BusinessReferenceData:CatalogLoad:TenantId"] = Guid.NewGuid().ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.Configure<BusinessReferenceDataProviderOptions>(
            configuration.GetSection(BusinessReferenceDataProviderOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IOptions<BusinessReferenceDataProviderOptions>>().Value);
    }
}
