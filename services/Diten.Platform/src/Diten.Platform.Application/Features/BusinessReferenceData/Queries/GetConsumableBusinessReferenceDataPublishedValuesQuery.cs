using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Queries;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — published values of an allow-listed reference set for a tenant user, read by the set's own
/// scope. Both tenant ids are SERVER-RESOLVED: <paramref name="CallerTenantId"/> from the validated token (null when the
/// caller is not a tenant user), <paramref name="ReferenceTenantId"/> from <c>BusinessReferenceData:CatalogLoad:TenantId</c>
/// (null when misconfigured). Nothing here comes from the request body or a client <c>scope_key</c>.
/// </summary>
public sealed record GetConsumableBusinessReferenceDataPublishedValuesQuery(
    string SetCode,
    Guid? CallerTenantId,
    Guid? ReferenceTenantId,
    Guid? VerifiedReferenceTenantId = null) : IRequest<Response<BusinessReferenceDataPublishedValuesModel>>, IBusinessReferenceDataRequest;
// WP-VW-W4 (W4-FIX-d, G-2) — VerifiedReferenceTenantId: BusinessReferenceData:Provider:ReferenceTenantId, the reserved tenant
// the VERIFIED catalogs (market, uom, pack-applicability) are provisioned in. Null = not configured ⇒ the old behaviour.
