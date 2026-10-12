using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>QUARANTINE-BUNDLE <c>getSignableSubject</c> — the decision as MOD-0022 hashes it (<c>qd-v1</c>).</summary>
public sealed record GetSignableSubjectQuery(Guid DecisionId) : IRequest<Response<SignableSubjectDto>>;

/// <summary>
/// Reads the decision in the caller's (already resolved) company scope and builds its <c>qd-v1</c> subject. Another company's
/// or an unknown decision is 404 (one answer, no oracle — ESIGN §6). The controller resolves the company before this runs.
/// </summary>
public sealed class GetSignableSubjectHandler : IRequestHandler<GetSignableSubjectQuery, Response<SignableSubjectDto>>
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineSignableSubjectBuilder _builder;

    public GetSignableSubjectHandler(IQuarantineDecisionRepository decisions, QuarantineSignableSubjectBuilder builder)
    {
        _decisions = decisions;
        _builder = builder;
    }

    public async Task<Response<SignableSubjectDto>> Handle(GetSignableSubjectQuery request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        return decision is null
            ? QuarantineSupport.Fail<SignableSubjectDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404)
            : Response<SignableSubjectDto>.Success(await _builder.BuildAsync(decision, ct));
    }
}
