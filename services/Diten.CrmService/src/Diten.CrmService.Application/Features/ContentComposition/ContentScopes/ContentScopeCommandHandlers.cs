using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>Shared SCMM-14 content-scope write validation. TenantId is claim-resolved; status is in-domain
/// (create/update carry only draft|active — archive is the dedicated command).</summary>
internal static class ContentScopeWrite
{
    public static (string? Error, int StatusCode) ValidateStructural(
        string scopeName, string? status, DateTimeOffset? periodFrom, DateTimeOffset? periodTo)
    {
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            return ("ScopeName is required.", 400);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && (!ContentScopeStatuses.IsValid(status)
                || string.Equals(status.Trim(), ContentScopeStatuses.Archived, StringComparison.OrdinalIgnoreCase)))
        {
            return ($"Status must be one of: {ContentScopeStatuses.Draft}, {ContentScopeStatuses.Active} "
                    + "(use the archive endpoint to archive).", 400);
        }

        if (periodFrom is { } from && periodTo is { } to && to < from)
        {
            return ("PeriodTo cannot be before PeriodFrom.", 400);
        }

        return (null, 0);
    }
}

public sealed class CreateContentScopeHandler : IRequestHandler<CreateContentScopeCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentScopeRepository _scopes;
    private readonly IContentCompositionAuditPublisher? _audit;

    public CreateContentScopeHandler(
        ITenantContext tenant, IActorContext actor, IContentScopeRepository scopes,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _scopes = scopes;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContentScopeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ScopeCode))
        {
            return Response<Guid>.Fail("ScopeCode is required.", 400);
        }

        var (error, statusCode) = ContentScopeWrite.ValidateStructural(
            request.ScopeName, request.Status, request.PeriodFrom, request.PeriodTo);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        var code = request.ScopeCode.Trim();
        if (await _scopes.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived content scope already uses ScopeCode '{code}' (contentScopeId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ContentScope
        {
            TenantId = tenantId,
            ScopeCode = code,
            ScopeName = request.ScopeName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ProductRefs = ContentScopeMapper.CleanRefs(request.ProductRefs),
            MarketRefs = ContentScopeMapper.CleanRefs(request.MarketRefs),
            AudienceRefs = ContentScopeMapper.CleanRefs(request.AudienceRefs),
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? null : request.Channel.Trim(),
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? null : request.LanguageCode.Trim(),
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            ScopeVersion = string.IsNullOrWhiteSpace(request.ScopeVersion) ? "1.0" : request.ScopeVersion.Trim(),
            Status = ContentScopeStatuses.Normalize(request.Status),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _scopes.InsertAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ContentScopeReasonCodes.Created, tenantId,
                ContentCompositionAuditEntities.ContentScope, entity.Id, entity.Version, entity.ScopeCode, cancellationToken);
        }

        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateContentScopeHandler : IRequestHandler<UpdateContentScopeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentScopeRepository _scopes;
    private readonly IContentCompositionAuditPublisher? _audit;

    public UpdateContentScopeHandler(
        ITenantContext tenant, IActorContext actor, IContentScopeRepository scopes,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _scopes = scopes;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateContentScopeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _scopes.GetByIdAsync(tenantId, request.ContentScopeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Content scope not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived content scope cannot be updated.", 409);
        }

        var (error, statusCode) = ContentScopeWrite.ValidateStructural(
            request.ScopeName, request.Status, request.PeriodFrom, request.PeriodTo);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        var now = DateTimeOffset.UtcNow;
        entity.ScopeName = request.ScopeName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.ProductRefs = ContentScopeMapper.CleanRefs(request.ProductRefs);
        entity.MarketRefs = ContentScopeMapper.CleanRefs(request.MarketRefs);
        entity.AudienceRefs = ContentScopeMapper.CleanRefs(request.AudienceRefs);
        entity.Channel = string.IsNullOrWhiteSpace(request.Channel) ? null : request.Channel.Trim();
        entity.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? null : request.LanguageCode.Trim();
        entity.PeriodFrom = request.PeriodFrom;
        entity.PeriodTo = request.PeriodTo;
        entity.Status = ContentScopeStatuses.Normalize(request.Status ?? entity.Status);
        if (!string.IsNullOrWhiteSpace(request.ScopeVersion))
        {
            entity.ScopeVersion = request.ScopeVersion.Trim();
        }

        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _scopes.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ContentScopeReasonCodes.Updated, tenantId,
                ContentCompositionAuditEntities.ContentScope, entity.Id, entity.Version, entity.ScopeCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveContentScopeHandler : IRequestHandler<ArchiveContentScopeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentScopeRepository _scopes;
    private readonly IContentCompositionAuditPublisher? _audit;

    public ArchiveContentScopeHandler(
        ITenantContext tenant, IActorContext actor, IContentScopeRepository scopes,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _scopes = scopes;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(ArchiveContentScopeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _scopes.GetByIdAsync(tenantId, request.ContentScopeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Content scope not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ContentScopeStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _scopes.UpdateAsync(entity, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ContentScopeReasonCodes.Archived, tenantId,
                ContentCompositionAuditEntities.ContentScope, entity.Id, entity.Version, entity.ScopeCode, cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}
