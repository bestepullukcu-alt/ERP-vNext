using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.CommandHandlers;

/// <summary>⛔ Compensation only (DCP-008 AD-6) — not a purge path.</summary>
public sealed class CompensateRepositoryObjectHandler
    : IRequestHandler<CompensateRepositoryObjectCommand, Response<NoContent>>
{
    private readonly DocumentRepositoryService _service;

    public CompensateRepositoryObjectHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<NoContent>> Handle(CompensateRepositoryObjectCommand request, CancellationToken cancellationToken) =>
        _service.CompensateAsync(request.ContentId, request.CorrelationId, cancellationToken);
}
