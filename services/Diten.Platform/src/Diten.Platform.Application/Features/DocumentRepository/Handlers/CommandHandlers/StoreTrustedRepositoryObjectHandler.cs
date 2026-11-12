using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.CommandHandlers;

/// <summary>
/// MOD-0262-FU01 DL-2 — trusted consumer upload. A FAILED service result is classified here, inside the command, so the
/// one <c>AuditBehavior</c> record and the client see the same outcome (§8.1). Order (r6, tur 3 F-1):
/// <list type="number">
/// <item><b>Token first.</b> The request was aborted → this is a cancellation whatever the provider answered (a broken
/// connection often surfaces as an <c>IOException</c> the provider turned into 503). Thrown before <c>AuditBehavior</c>
/// writes, so no 503 record appears; the executor's cancellation branch writes the one record.</item>
/// <item><b>Then the counting stream's flags</b> (<see cref="ITrustedUploadLimitSignal"/>): the file passed the trusted
/// limit → 413; the multipart body went on after the file section → 400.</item>
/// <item><b>Then the service result</b>, unchanged (provider 400, a genuine 503).</item>
/// </list>
/// A successful result is never re-classified: the row is written.
/// </summary>
public sealed class StoreTrustedRepositoryObjectHandler
    : IRequestHandler<StoreTrustedRepositoryObjectCommand, Response<RepositoryObjectModel>>
{
    private readonly DocumentRepositoryService _service;

    public StoreTrustedRepositoryObjectHandler(DocumentRepositoryService service) => _service = service;

    public async Task<Response<RepositoryObjectModel>> Handle(
        StoreTrustedRepositoryObjectCommand request, CancellationToken cancellationToken)
    {
        var correlationId = request.CorrelationId.ToString("D");
        var result = await _service.StoreForTrustedConsumerAsync(request.Input, request.Consumer, correlationId, cancellationToken);
        if (result.IsSuccessful)
        {
            return result;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (request.Input.Content is ITrustedUploadLimitSignal { LimitExceeded: true })
        {
            return Response<RepositoryObjectModel>.Fail(
                "The file exceeds the trusted consumer upload limit.", 413,
                DocumentRepositoryReasonCodes.TrustedConsumerTooLarge, correlationId);
        }

        if (request.Input.Content is ITrustedUploadLimitSignal { FormatViolated: true })
        {
            return Response<RepositoryObjectModel>.Fail(
                "The multipart body continues after the file section.", 400,
                DocumentRepositoryReasonCodes.ValidationFailed, correlationId);
        }

        return result;
    }
}
