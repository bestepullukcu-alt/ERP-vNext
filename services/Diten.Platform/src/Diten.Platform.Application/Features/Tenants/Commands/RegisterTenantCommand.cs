using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities;
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
    InitialAdminInfo? InitialAdmin = null) : IRequest<Response<Guid>>;
