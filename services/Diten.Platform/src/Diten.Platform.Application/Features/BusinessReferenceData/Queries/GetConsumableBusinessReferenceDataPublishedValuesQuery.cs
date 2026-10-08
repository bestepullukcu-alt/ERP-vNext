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
    Guid? ReferenceTenantId) : IRequest<Response<BusinessReferenceDataPublishedValuesModel>>, IBusinessReferenceDataRequest;
