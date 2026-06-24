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

        // Faz 1 — MANUAL activation from any pre-active governance state. Evidence/approval gating
        // (Approved→Active requires EvidenceStatus==Verified) is DEFERRED (MOD-0023/0031): STUB, manual allowed.
        // TODO(MOD-0023): enforce evidence completion before activation.
        if (entity.OperationalStatus is not (LegalEntityOperationalStatus.Draft
            or LegalEntityOperationalStatus.InReview
            or LegalEntityOperationalStatus.Approved))
        {
            return Response<NoContent>.Fail("Only draft, in-review or approved Legal Entities can be activated.", 409);
        }

        entity.OperationalStatus = LegalEntityOperationalStatus.Active;
        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
