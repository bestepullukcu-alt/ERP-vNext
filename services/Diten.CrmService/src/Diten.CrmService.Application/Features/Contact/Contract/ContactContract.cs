using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContactAvailability;
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Contract;

public sealed record GetContactContractQuery : IRequest<Response<ContactContractDto>>;

/// <summary>One required MOD-0048 set and whether the operator has published it.</summary>
public sealed record ContactReferenceSetReadiness(string SetCode, bool Ready, int ValueCount);

/// <summary>
/// MOD-0150 capability flags. The availability flags mean "availability / visit preference MASTER DATA is
/// supported" — they do NOT imply visit planning, route planning or frequency support. No
/// <c>supportsVisitPlanning</c> / <c>supportsRoutePlanning</c> / <c>supportsVisitFrequency</c> flag exists here:
/// those capabilities belong to MOD-0155 and MOD-0165/MOD-0167, and advertising them would claim ownership
/// MOD-0150 does not have.
/// </summary>
public sealed record ContactFeatureFlags(
    bool SupportsContactAvailability,
    bool SupportsAccountContactLinkAvailability,
    bool SupportsVisitPreference,
    bool SupportsAvailabilityExceptions);

public sealed record ContactContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    ContactFeatureFlags Features,
    IReadOnlyList<ContactReferenceSetReadiness> RequiredReferenceSets,
    IReadOnlyList<string> MissingRequiredReferenceSets,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// MOD-0150 contract surface (MOD-0149 / MOD-0151 parity). Reports the shipped capability flags, the MOD-0048
/// reference readiness the availability write path depends on, and the honest limitations of this FU.
/// </summary>
public sealed class GetContactContractHandler : IRequestHandler<GetContactContractQuery, Response<ContactContractDto>>
{
    public const string ModuleId = "MOD-0150";
    public const string ModuleName = "Contact & Relationship Management";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU01-contact-foundation-backend-only; FU02-contact-frontend-compact; FU03-account-contact-links; " +
        "FU04-account-relationships; FU05-consent-preference-seam; FU06-import-export-audit; " +
        "FU-contact-availability-visit-preference";

    private static readonly IReadOnlyList<string> ContractPermissions = new[]
    {
        "crm.contact.read",
        "crm.contact.create",
        "crm.contact.update",
        "crm.account-contact.read",
        "crm.account-contact.manage",
        ContactAvailabilityPermissions.Read,
        ContactAvailabilityPermissions.Manage
    };

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "availability is AccountContactLink-scoped master data; there is no availability field on the Contact master",
        "the same contact linked to several accounts carries an independent schedule per link",
        "a date-specific exception overrides the weekly pattern for that date",
        "missing availability is reported as 'unknown', never as 'unavailable'",
        "AppointmentRequired produces a warning/reason; it never drops a candidate",
        "the avoid window is a stronger constraint inside the available window, not the inverse of the preferred window",
        "availability windows are local wall-clock times of the account location; MOD-0150 owns no timezone master",
        "availability is never hard-deleted: closing is deactivate/archive",
        "the lookup returns rows and reason codes only — no ordering, no distance, no score, no plan",
        "visit planning, route planning and route optimization are MOD-0155; MOD-0150 produces none of them",
        "visit frequency / call-cycle policy is produced by MOD-0165/MOD-0167 and consumed by MOD-0155 — not owned here",
        "last visit date, visit history and due/overdue are MOD-0155; MOD-0150 never writes them",
        "availability endpoints run on the documented crm.contact.read / crm.contact.update fallback until the " +
        "MOD-0150-FU-RBAC catalog alignment lands"
    };

    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _catalog;

    public GetContactContractHandler(ITenantContext tenant, IReferenceDataCatalogReader catalog)
    {
        _tenant = tenant;
        _catalog = catalog;
    }

    public async Task<Response<ContactContractDto>> Handle(GetContactContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContactContractDto>.Fail("Tenant context is required.", 400);
        }

        var readiness = new List<ContactReferenceSetReadiness>();
        foreach (var setCode in ContactAvailabilityReferenceSets.All)
        {
            try
            {
                var snapshot = await _catalog.GetPublishedValuesAsync(setCode, cancellationToken);
                readiness.Add(new ContactReferenceSetReadiness(setCode, snapshot.IsPublished, snapshot.Values.Count));
            }
            catch (Exception)
            {
                // An unreachable reference service is reported as NOT ready — never silently as ready.
                readiness.Add(new ContactReferenceSetReadiness(setCode, false, 0));
            }
        }

        var missing = readiness.Where(r => !r.Ready).Select(r => r.SetCode).ToList();

        var dto = new ContactContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            missing.Count == 0,
            new ContactFeatureFlags(true, true, true, true),
            readiness,
            missing,
            ContractPermissions,
            CurrentLimitations);

        return Response<ContactContractDto>.Success(dto);
    }
}
