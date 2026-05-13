using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class ImportInterfaceManifestRequestValidator : AbstractValidator<ImportInterfaceManifestRequest>
{
    public ImportInterfaceManifestRequestValidator()
    {
        RuleFor(x => x.Manifest).NotNull().WithMessage("Manifest is required.");
        RuleFor(x => x.Manifest.SourceService).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Manifest.SourceModuleCode).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Manifest.Interfaces).NotEmpty().WithMessage("Manifest must include at least one interface.");

        RuleForEach(x => x.Manifest.Interfaces).ChildRules(definition =>
        {
            definition.RuleFor(x => x.InterfaceCode)
                .NotEmpty()
                .Must(InterfaceCodeNormalizer.IsValid)
                .WithMessage("InterfaceCode must use {MODULE}.{RESOURCE}.{ACTION} format.");
            definition.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
            definition.RuleFor(x => x.OwnerModuleCode).NotEmpty().MaximumLength(80);
            definition.RuleFor(x => x.ProviderService).NotEmpty().MaximumLength(160);
            definition.RuleFor(x => x.Version).NotEmpty().Matches("^v[0-9]+$").WithMessage("Version must use vN format.");
            definition.RuleFor(x => x.Endpoints).NotEmpty().WithMessage("Interface must include at least one endpoint.");
            definition.RuleForEach(x => x.Endpoints).ChildRules(endpoint =>
            {
                endpoint.RuleFor(x => x)
                    .Must(x => EndpointKeyNormalizer.IsValid(x.HttpMethod, x.RoutePath, x.Version))
                    .WithMessage("Endpoint must produce a valid {HTTP_METHOD}:{NORMALIZED_ROUTE}:{VERSION} key.");
            });
        });

        RuleFor(x => x.Manifest)
            .Must(manifest =>
            {
                var keys = manifest.Interfaces
                    .SelectMany(x => x.Endpoints.Select(endpoint => EndpointKeyNormalizer.Create(endpoint.HttpMethod, endpoint.RoutePath, endpoint.Version)))
                    .ToList();
                return keys.Count == keys.Distinct(StringComparer.Ordinal).Count();
            })
            .WithMessage("Manifest contains duplicate EndpointKey values.");

        RuleFor(x => x.Manifest)
            .Must(manifest =>
            {
                var keys = manifest.Interfaces
                    .Select(x => $"{InterfaceCodeNormalizer.Normalize(x.InterfaceCode)}:{x.Version.Trim().ToLowerInvariant()}")
                    .ToList();
                return keys.Count == keys.Distinct(StringComparer.Ordinal).Count();
            })
            .WithMessage("Manifest contains duplicate InterfaceCode + Version values.");
    }
}
