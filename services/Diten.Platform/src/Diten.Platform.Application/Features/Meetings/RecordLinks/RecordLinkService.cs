using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <inheritdoc cref="IRecordLinkService"/>
public sealed class RecordLinkService : IRecordLinkService
{
    private readonly IRecordLinkRepository _links;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public RecordLinkService(
        IRecordLinkRepository links, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _links = links;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public Task<RecordLink> AddLinkAsync(
        RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType,
        string? idempotencyKey = null, bool createdAfterMinutesPublished = false, CancellationToken ct = default)
    {
        /*
         * K11 — idempotent: the SAME (source, target, linkType) triple returns the EXISTING row, never a
         * second one, whether that resolves as a plain find or as the loser of a genuine insert race
         * (FindOrCreateAsync's own doc-comment). No error either way — a resubmitted request is not a fault.
         */
        var link = new RecordLink
        {
            TenantId = _tenantContext.TenantId,
            SourceModuleCode = source.ModuleCode,
            SourceRecordId = source.RecordId,
            TargetModuleCode = target.ModuleCode,
            TargetRecordId = target.RecordId,
            LinkType = linkType,
            IdempotencyKey = idempotencyKey,
            CreatedByUserId = _currentUser.UserId,
            CreatedBy = _currentUser.ActorName,
            CreatedAfterMinutesPublished = createdAfterMinutesPublished
        };

        return _links.FindOrCreateAsync(link, ct);
    }

    public Task RemoveLinkAsync(Guid linkId, CancellationToken ct = default) => _links.DeleteAsync(linkId, ct);

    public Task<RecordLink?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _links.FindByIdempotencyKeyAsync(idempotencyKey, ct);

    public Task<RecordLink?> FindLinkAsync(
        RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType, CancellationToken ct = default)
        => _links.FindAsync(source.ModuleCode, source.RecordId, target.ModuleCode, target.RecordId, linkType, ct);

    public Task<IReadOnlyList<RecordLink>> ListBySourceAsync(
        IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default)
        => _links.ListBySourceAsync(sourceRecordIds, ct);

    public Task<IReadOnlyList<RecordLink>> ListByTargetAsync(
        IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default)
        => _links.ListByTargetAsync(targetRecordIds, ct);
}
