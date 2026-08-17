using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.CommandHandlers;

public sealed class UpdateAuditRetentionHandler
    : IRequestHandler<UpdateAuditRetentionCommand, Response<AuditRetentionPolicyDto>>
{
    private readonly IAuditRetentionPolicyRepository _repository;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateAuditRetentionHandler(
        IAuditRetentionPolicyRepository repository,
        IAuditMetaAuditWriter metaAuditWriter,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _metaAuditWriter = metaAuditWriter;
        _currentUserContext = currentUserContext;
    }

    public async Task<Response<AuditRetentionPolicyDto>> Handle(UpdateAuditRetentionCommand request, CancellationToken ct)
    {
        if (!AuditFilterParser.TryParseCategory(request.Request.Category, out var category, out var error) || category is null)
        {
            return Response<AuditRetentionPolicyDto>.Fail(error ?? "Retention category is invalid.", 400);
        }

        var planTierCode = request.Request.PlanTierCode.Trim();
        var existing = await _repository.GetActivePolicyByIdAsync(request.Request.PolicyId, ct);
        if (existing is null)
        {
            return Response<AuditRetentionPolicyDto>.Fail("Audit retention policy was not found. Reload before saving.", 404);
        }

        if (existing.Category != category.Value
            || !string.Equals(existing.PlanTierCode, planTierCode, StringComparison.OrdinalIgnoreCase))
        {
            return Response<AuditRetentionPolicyDto>.Fail("Audit retention policy id, category, and plan tier do not match. Reload before saving.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUserContext.ActorName;
        var policy = new AuditEventRetentionPolicy
        {
            Id = existing.Id,
            CreatedAt = existing.CreatedAt,
            CreatedBy = existing.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = actor,
            Category = existing.Category,
            PlanTierCode = existing.PlanTierCode,
            MinimumRetentionDays = request.Request.MinimumRetentionDays,
            DefaultRetentionDays = request.Request.DefaultRetentionDays,
            MaximumRetentionDays = request.Request.MaximumRetentionDays,
            HotStorageDays = request.Request.HotStorageDays,
            ColdStoragePrepared = request.Request.ColdStoragePrepared,
            AllowTenantOverride = request.Request.AllowTenantOverride,
            IsActive = request.Request.IsActive
        };

        try
        {
            policy.Validate();
            var updated = await _repository.UpdateAsync(policy, ct);
            if (!updated)
            {
                return Response<AuditRetentionPolicyDto>.Fail("Audit retention policy changed while you were editing. Reload before saving.", 409);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Response<AuditRetentionPolicyDto>.Fail(ex.Message, 400);
        }

        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.UpdateAuditRetentionCommand",
            AuditCategory.PlatformConfiguration,
            AuditOperation.Update,
            AuditOutcome.Succeeded,
            "AuditEventRetentionPolicy",
            policy.Id,
            AuditTenantIds.PlatformSystemTenantId,
            new Dictionary<string, object?>
            {
                ["policyId"] = policy.Id,
                ["category"] = policy.Category.ToString(),
                ["planTierCode"] = policy.PlanTierCode,
                ["minimumRetentionDays"] = policy.MinimumRetentionDays,
                ["defaultRetentionDays"] = policy.DefaultRetentionDays,
                ["maximumRetentionDays"] = policy.MaximumRetentionDays,
                ["hotStorageDays"] = policy.HotStorageDays,
                ["allowTenantOverride"] = policy.AllowTenantOverride,
                ["isActive"] = policy.IsActive
            }), ct);

        return Response<AuditRetentionPolicyDto>.Success(AuditEventMapper.ToRetentionDto(policy));
    }
}
