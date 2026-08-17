using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class RecordPersonReferenceDirectoryHealthHandler : IRequestHandler<RecordPersonReferenceDirectoryHealthCommand, Response<Guid>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public RecordPersonReferenceDirectoryHealthHandler(IPersonReferenceDirectoryRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(RecordPersonReferenceDirectoryHealthCommand request, CancellationToken ct)
    {
        var entity = new PersonReferenceDirectoryHealthSnapshot
        {
            TenantId = TenantGuard.RequireTenant(_tenantContext),
            SnapshotKey = request.Request.SnapshotKey.Trim(),
            ProjectionCount = request.Request.ProjectionCount,
            ValidatedCount = request.Request.ValidatedCount,
            ConflictCount = request.Request.ConflictCount,
            LastCheckedAt = request.Request.LastCheckedAt,
            RedactedStatus = PersonReferenceDirectoryCodeNormalizer.NormalizeOptional(request.Request.RedactedStatus)
        };

        await _repository.CreateHealthSnapshotAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
