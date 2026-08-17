using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class RecordHrisSyncCheckpointHandler : IRequestHandler<RecordHrisSyncCheckpointCommand, Response<Guid>>
{
    private readonly IHrisSourceRepository _repository;

    public RecordHrisSyncCheckpointHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordHrisSyncCheckpointCommand request, CancellationToken ct)
    {
        var source = await _repository.GetSourceProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<Guid>.Fail("HRIS source not found.", 404);
        }

        var checkpoint = new HrisSyncCheckpoint
        {
            TenantId = source.TenantId,
            SourceProfileId = source.Id,
            SyncRunId = request.Request.SyncRunId.Trim(),
            SyncMode = request.Request.SyncMode,
            StartedAt = request.Request.StartedAt,
            CompletedAt = request.Request.CompletedAt,
            Status = request.Request.Status,
            CursorReference = NormalizeOptional(request.Request.CursorReference),
            RecordsSeen = request.Request.RecordsSeen,
            RecordsAccepted = request.Request.RecordsAccepted,
            RecordsRejected = request.Request.RecordsRejected,
            ErrorSummary = NormalizeOptional(request.Request.ErrorSummary)
        };

        await _repository.CreateSyncCheckpointAsync(checkpoint, ct);
        source.LastSyncCheckpointId = checkpoint.Id;
        await _repository.UpdateSourceProfileAsync(source, ct);

        return Response<Guid>.Success(checkpoint.Id, 201);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
