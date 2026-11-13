using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Commands;

/// <summary>
/// MOD-0262-FU01 — store one binary in the Internal Document Repository.
/// <para>Audited via <see cref="IAuditableCommand"/> so every store reaches MOD-0021.</para>
/// <para>MOD-0262-FU08: the success record names the stored object — <c>EntityId = ContentId</c> and the §21 after-state —
/// from the successful response (<see cref="IAuditResponseStateProvider{TResponse}"/>); the metadata stays as it was.</para>
/// </summary>
public sealed record StoreRepositoryObjectCommand(StoreRepositoryObjectInput Input, string CorrelationId)
    : IRequest<Response<RepositoryObjectModel>>, IAuditableCommand, IAuditMetadataProvider,
        IAuditResponseStateProvider<Response<RepositoryObjectModel>>
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "RepositoryObject",
        SourceModule: "MOD-0262-FU01");

    public AuditResponseState? GetAuditResponseState(Response<RepositoryObjectModel> response) =>
        RepositoryObjectAuditState.For(response.Data, trustedConsumerModule: null);
}
