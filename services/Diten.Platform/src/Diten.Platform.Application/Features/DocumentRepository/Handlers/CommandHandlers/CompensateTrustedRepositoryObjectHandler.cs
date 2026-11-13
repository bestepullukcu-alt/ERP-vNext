using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.CommandHandlers;

/// <summary>
/// ⛔ MOD-0262-FU01 DL-2 — trusted consumer compensation only (DCP-008 AD-6), not a purge path. The handler reads the row
/// again and decides on its own read (§8.1 "Telafi tekrarı"); the provider delete follows only a won mark.
/// </summary>
public sealed class CompensateTrustedRepositoryObjectHandler
    : IRequestHandler<CompensateTrustedRepositoryObjectCommand, Response<NoContent>>
{
    private readonly DocumentRepositoryService _service;

    public CompensateTrustedRepositoryObjectHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<NoContent>> Handle(CompensateTrustedRepositoryObjectCommand request, CancellationToken cancellationToken) =>
        _service.CompensateForTrustedConsumerAsync(
            request.ContentId, request.Consumer, request.CorrelationId.ToString("D"), cancellationToken);
}
