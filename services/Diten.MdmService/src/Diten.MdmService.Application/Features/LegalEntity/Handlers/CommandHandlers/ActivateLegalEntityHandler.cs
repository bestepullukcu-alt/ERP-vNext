using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class ActivateLegalEntityHandler : IRequestHandler<Commands.ActivateLegalEntityCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;

    public ActivateLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.ActivateLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        // Faz 1 — MANUAL activation. Allowed from ANY non-active state: Draft/InReview/Approved (initial activation)
        // AND Suspended (resume) AND Archived (restore) → all transition to Active. Only an already-Active entity is
        // rejected. Evidence/approval gating (Approved→Active requires EvidenceStatus==Verified) is DEFERRED
        // (MOD-0023/0031): STUB, manual allowed. TODO(MOD-0023): enforce evidence completion before activation.
        if (entity.OperationalStatus == LegalEntityOperationalStatus.Active)
        {
            return Response<NoContent>.Fail("Legal Entity is already active.", 409);
        }

        entity.OperationalStatus = LegalEntityOperationalStatus.Active;
        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
