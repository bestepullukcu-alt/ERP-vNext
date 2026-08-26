using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataPublishService
{
    Task<BusinessReferenceDataVersionDetailModel> PublishAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default);

    Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default);

    Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedMarketAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default);

    Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedMarketAsync(
        Guid versionId, string actorId, string correlationId, string idempotencyKey, string publishMode,
        DateTimeOffset? publishAt, string? expectedConcurrencyToken, bool overrideAction, string? overrideReason,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,
        VerifiedMarketOperationalFacts facts, CancellationToken ct = default);

    Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts,
        CancellationToken ct = default);
}

public interface IBusinessReferenceDataPublishCheckpointObserver
{
    Task OnCheckpointPersistedAsync(
        BusinessReferenceDataPublishOperation operation,
        CancellationToken ct = default);
}
