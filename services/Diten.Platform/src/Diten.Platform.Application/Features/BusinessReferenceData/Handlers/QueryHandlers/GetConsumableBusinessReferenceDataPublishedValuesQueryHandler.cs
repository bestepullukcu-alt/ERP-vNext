using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — answers what the Platform consumer path answers an administrator today, for any tenant user.
///
/// <para><b>Which tenant a set is read in</b> is decided by the set's OWN BRD metadata, never by the client and never by a
/// code list of "global sets":</para>
/// <list type="bullet">
///   <item>The caller's tenant holds the set → its <c>ScopeType</c> governs. <c>global</c> → read there with no scope key;
///   any other scope → read there with <c>scope_key</c> = the server-resolved caller tenant. This is exactly the
///   consumer read the CRM validator sends with an administrator's token today (scoped first, keyless for a global set).</item>
///   <item>The caller's tenant does not hold the set → the reference tenant's metadata is read. Only a <c>global</c> set is
///   served from there (the same reference tenant the Legal Entity stopgap reads). A tenant-scoped set of the reference
///   tenant — or of any other tenant — is NEVER returned: that is another tenant's data.</item>
/// </list>
/// <para>Every "nothing to serve" outcome (set missing, retired, no published or no effective version, scope mismatch) is
/// one answer: 404 <c>reference_set_not_published</c>. Never a 500.</para>
/// </summary>
public sealed class GetConsumableBusinessReferenceDataPublishedValuesQueryHandler
    : IRequestHandler<GetConsumableBusinessReferenceDataPublishedValuesQuery, Response<BusinessReferenceDataPublishedValuesModel>>
{
    public const string NotAccessible = "reference_set_not_tenant_accessible";
    public const string NotPublished = "reference_set_not_published";
    public const string TenantContextRequired = "tenant_context_required";
    public const string ReferenceTenantMisconfigured = "reference_tenant_misconfigured";

    private const string GlobalScope = "global";

    // The consumer service's coded "nothing to serve" outcomes (BusinessReferenceDataConsumerQueryService.ResolveVersionAsync).
    private static readonly HashSet<string> NotPublishedSignals = new(StringComparer.Ordinal)
    {
        "reference_data_set_not_found",
        "reference_data_set_retired",
        "no_published_version",
        "scope_not_found",
        "effective_version_not_found",
        "published_version_not_found",
        "unsupported_scope_type",
        "scope_key_required",
        "scope_key_not_allowed_for_global"
    };

    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IBusinessReferenceDataConsumerQueryService _consumer;
    private readonly ITenantContext _tenantContext;
    private readonly BusinessReferenceDataConsumableSetsOptions _options;

    public GetConsumableBusinessReferenceDataPublishedValuesQueryHandler(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataConsumerQueryService consumer,
        ITenantContext tenantContext,
        IOptions<BusinessReferenceDataConsumableSetsOptions> options)
    {
        _repository = repository;
        _consumer = consumer;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Response<BusinessReferenceDataPublishedValuesModel>> Handle(
        GetConsumableBusinessReferenceDataPublishedValuesQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CallerTenantId is not { } callerTenantId || callerTenantId == Guid.Empty)
        {
            return Fail(TenantContextRequired, 400);
        }

        if (!_options.TryResolve(request.SetCode, out var setCode))
        {
            return Fail(NotAccessible, 404);
        }

        try
        {
            var callerSet = await ReadSetAsync(callerTenantId, setCode, ct);
            if (callerSet is not null)
            {
                var scopeKey = IsGlobal(callerSet.ScopeType) ? null : callerTenantId.ToString();
                return Success(await ReadValuesAsync(callerTenantId, setCode, scopeKey, ct));
            }

            if (request.ReferenceTenantId is not { } referenceTenantId || referenceTenantId == Guid.Empty)
            {
                return Fail(ReferenceTenantMisconfigured, 500);
            }

            if (referenceTenantId == callerTenantId)
            {
                return Fail(NotPublished, 404);
            }

            var referenceSet = await ReadSetAsync(referenceTenantId, setCode, ct);
            if (referenceSet is null || !IsGlobal(referenceSet.ScopeType))
            {
                // A reference-tenant set that is not global belongs to the reference tenant alone.
                return Fail(NotPublished, 404);
            }

            return Success(await ReadValuesAsync(referenceTenantId, setCode, null, ct));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException
                                   && NotPublishedSignals.Contains(ex.Message))
        {
            return Fail(NotPublished, 404);
        }
    }

    private async Task<Domain.Entities.BusinessReferenceDataSet?> ReadSetAsync(
        Guid tenantId, string setCode, CancellationToken ct)
    {
        using (TenantScope.Begin(_tenantContext, tenantId))
        {
            return await _repository.GetSetByCodeAsync(setCode, ct);
        }
    }

    private async Task<BusinessReferenceDataPublishedValuesModel> ReadValuesAsync(
        Guid tenantId, string setCode, string? scopeKey, CancellationToken ct)
    {
        using (TenantScope.Begin(_tenantContext, tenantId))
        {
            return await _consumer.GetPublishedValuesAsync(setCode, scopeKey, ct);
        }
    }

    private static bool IsGlobal(string? scopeType)
        => string.Equals(scopeType?.Trim(), GlobalScope, StringComparison.OrdinalIgnoreCase);

    private static Response<BusinessReferenceDataPublishedValuesModel> Success(BusinessReferenceDataPublishedValuesModel model)
        => Response<BusinessReferenceDataPublishedValuesModel>.Success(model);

    private static Response<BusinessReferenceDataPublishedValuesModel> Fail(string code, int statusCode)
        => Response<BusinessReferenceDataPublishedValuesModel>.Fail(code, statusCode, code);
}
