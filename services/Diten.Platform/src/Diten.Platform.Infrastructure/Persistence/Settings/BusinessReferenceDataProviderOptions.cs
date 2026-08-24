using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Persistence.Settings;

public sealed class BusinessReferenceDataProviderOptions
{
    public const string SectionName = "BusinessReferenceData:Provider";

    public Guid? ReferenceTenantId { get; set; }

    public bool TryGetReferenceTenantId(out Guid referenceTenantId)
    {
        referenceTenantId = ReferenceTenantId.GetValueOrDefault();
        return ReferenceTenantId.HasValue && referenceTenantId != Guid.Empty;
    }
}

public readonly record struct BusinessReferenceDataProviderOptionsResolution(
    bool IsValid,
    Guid ReferenceTenantId)
{
    public const string InvalidReasonCode = "REFERENCE_PROVIDER_CONFIGURATION_INVALID";
}

public static class BusinessReferenceDataProviderOptionsResolver
{
    public static BusinessReferenceDataProviderOptionsResolution Resolve(
        IOptions<BusinessReferenceDataProviderOptions>? options)
    {
        try
        {
            return options?.Value.TryGetReferenceTenantId(out var referenceTenantId) == true
                ? new BusinessReferenceDataProviderOptionsResolution(true, referenceTenantId)
                : new BusinessReferenceDataProviderOptionsResolution(false, Guid.Empty);
        }
        catch (Exception exception) when (exception is OptionsValidationException
                                          or InvalidOperationException
                                          or FormatException)
        {
            return new BusinessReferenceDataProviderOptionsResolution(false, Guid.Empty);
        }
    }
}

public sealed class BusinessReferenceDataProviderOptionsValidator : IValidateOptions<BusinessReferenceDataProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, BusinessReferenceDataProviderOptions options)
    {
        return options.TryGetReferenceTenantId(out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"'{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId' must be a non-empty GUID.");
    }
}
