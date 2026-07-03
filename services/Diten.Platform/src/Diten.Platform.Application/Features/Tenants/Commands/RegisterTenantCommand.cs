using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record RegisterTenantCommand(
    string Name,
    string Domain,
    string? Subdomain = null,
    string? Slug = null,
    string? DisplayName = null,
    string? Tier = null,
    string? Region = null,
    string? Environment = null,
    TenantType? TenantType = null,
    Guid? PlanId = null,

    // Legal & Company
    string? LegalName = null,
    string? TaxNumber = null,
    string? Country = null,
    string? Industry = null,

    // Contact
    string? ContactPerson = null,
    string? ContactEmail = null,
    string? ContactPhone = null,

    // Locale Defaults
    string? DefaultTimezone = null,
    string? DefaultLanguage = null,
    string? DefaultCurrency = null,

    // Initial Admin (invitation-based onboarding placeholder)
    InitialAdminInfo? InitialAdmin = null) : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Create, EntityType: "Tenant",
        IsPlatformGlobal: true, SourceModule: "tenant-registry",
        Metadata: new Dictionary<string, object?> { ["name"] = Name, ["slug"] = Slug, ["domain"] = Domain });
}
