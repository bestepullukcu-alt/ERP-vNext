using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.RecordCodes.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.RecordCodes.Handlers.QueryHandlers;

/// <summary>
/// The route tenant is checked in the platform context, then the setting is read INSIDE that tenant's scope — the store
/// refuses the platform context (pack §4 "Yönetici ucu").
/// </summary>
public sealed class GetRecordCodeGroupPrefixHandler : IRequestHandler<GetRecordCodeGroupPrefixQuery, Response<RecordCodeGroupPrefixDto>>
{
    private readonly ITenantRegistryRepository _tenants;
    private readonly IRecordCodeGroupPrefixRepository _groupPrefixes;
    private readonly ITenantContext _tenantContext;
    private readonly IOptions<DocumentCodingOptions> _documentCoding;

    public GetRecordCodeGroupPrefixHandler(
        ITenantRegistryRepository tenants,
        IRecordCodeGroupPrefixRepository groupPrefixes,
        ITenantContext tenantContext,
        IOptions<DocumentCodingOptions> documentCoding)
    {
        _tenants = tenants;
        _groupPrefixes = groupPrefixes;
        _tenantContext = tenantContext;
        _documentCoding = documentCoding;
    }

    public async Task<Response<RecordCodeGroupPrefixDto>> Handle(GetRecordCodeGroupPrefixQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || await _tenants.GetByIdAsync(request.TenantId, cancellationToken) is null)
        {
            return Response<RecordCodeGroupPrefixDto>.Fail(
                "The tenant was not found.", 404, RecordCodeReasonCodes.TenantNotFound);
        }

        using (TenantScope.Begin(_tenantContext, request.TenantId))
        {
            var setting = await _groupPrefixes.GetAsync(request.TenantId, cancellationToken);
            return setting is null
                ? Response<RecordCodeGroupPrefixDto>.Fail(
                    "The tenant has no group-prefix setting.", 404, RecordCodeReasonCodes.GroupPrefixMissing)
                : Response<RecordCodeGroupPrefixDto>.Success(new RecordCodeGroupPrefixDto(
                    setting.TenantId, setting.GroupPrefix, setting.AlignedAtUtc, setting.LockedAtUtc,
                    _documentCoding.Value.OrgPrefix));
        }
    }
}
