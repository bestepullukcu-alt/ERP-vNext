using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;

public sealed class GetAuditRetentionPoliciesHandler
    : IRequestHandler<GetAuditRetentionPoliciesQuery, Response<IReadOnlyList<AuditRetentionPolicyDto>>>
{
    private readonly IAuditRetentionPolicyRepository _repository;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;

    public GetAuditRetentionPoliciesHandler(
        IAuditRetentionPolicyRepository repository,
        IAuditMetaAuditWriter metaAuditWriter)
    {
        _repository = repository;
        _metaAuditWriter = metaAuditWriter;
    }

    public async Task<Response<IReadOnlyList<AuditRetentionPolicyDto>>> Handle(
        GetAuditRetentionPoliciesQuery request,
        CancellationToken ct)
    {
        var policies = await _repository.GetActivePoliciesAsync(ct);
        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.GetAuditRetentionPoliciesQuery",
            AuditCategory.PlatformConfiguration,
            AuditOperation.Execute,
            AuditOutcome.Succeeded,
            "AuditEventRetentionPolicy",
            null,
            AuditTenantIds.PlatformSystemTenantId,
            new Dictionary<string, object?>
            {
                ["resultCount"] = policies.Count
            }), ct);

        return Response<IReadOnlyList<AuditRetentionPolicyDto>>.Success(
            policies.Select(AuditEventMapper.ToRetentionDto).ToList());
    }
}
