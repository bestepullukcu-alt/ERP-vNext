using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>RECALL <c>getSignableSubject</c> (R4) — the controller has checked the key and set the company scope from the record.</summary>
public sealed record GetRecallSignableSubjectQuery(string SubjectType, Guid SubjectId) : IRequest<Response<RecallSignableSubjectDto>>;

/// <summary>
/// The MOD-0022 subject provider answer (pack §8.8): <c>rc-v1</c> of a case of the caller's company, <c>rs-v1</c> of a settings change the caller
/// prepared. <c>recall.drill-run</c> (<c>dr-v1</c>) answers 404 until R5 brings the drill run. Every refusal is one 404 (no oracle). The fields
/// are exactly those <see cref="RecallSignableSubjects"/> hashes for the verification (CT decision F3-A).
/// </summary>
public sealed class GetRecallSignableSubjectHandler : IRequestHandler<GetRecallSignableSubjectQuery, Response<RecallSignableSubjectDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallSubjectFacts _facts;
    private readonly RecallSupport _support;

    public GetRecallSignableSubjectHandler(IRecallRepository repository, RecallSubjectFacts facts, RecallSupport support)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<RecallSignableSubjectDto>> Handle(GetRecallSignableSubjectQuery request, CancellationToken ct)
    {
        if (request.SubjectType == RecallSignableActions.CaseSubjectType && await _repository.FindCaseAsync(request.SubjectId, ct) is { } c)
        {
            return Response<RecallSignableSubjectDto>.Success(RecallSignableSubjects.CaseSubject(c, await _facts.FactsAsync(c, _support.Now(), ct)));
        }

        if (request.SubjectType == RecallSignableActions.SettingsChangeSubjectType
            && await _repository.FindSettingsChangeAsync(request.SubjectId, ct) is { } change && change.SetBy == _support.UserId)
        {
            return Response<RecallSignableSubjectDto>.Success(RecallSignableSubjects.SettingsChangeSubject(change));
        }

        return Response<RecallSignableSubjectDto>.Fail(RecallErrorCodes.SubjectNotFound, "Signable subject not found.", 404);
    }
}
