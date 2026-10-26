using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>FEFO-BUNDLE <c>getSignableSubject</c> (F3) — the controller has checked the key and set the company scope from the record.</summary>
public sealed record GetExpirySignableSubjectQuery(string SubjectType, Guid SubjectId) : IRequest<Response<ExpirySignableSubjectDto>>;

/// <summary>
/// The MOD-0022 subject provider answer (pack §8.6): <c>fx-v1</c> of a case of the caller's company, <c>do-v1</c> of an override the caller
/// prepared. Every refusal is one 404 (no oracle). The fields are those <see cref="ExpirySignableSubjects"/> hashes for verification.
/// </summary>
public sealed class GetExpirySignableSubjectHandler : IRequestHandler<GetExpirySignableSubjectQuery, Response<ExpirySignableSubjectDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpirySupport _support;

    public GetExpirySignableSubjectHandler(IExpiryRepository repository, ExpirySupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<ExpirySignableSubjectDto>> Handle(GetExpirySignableSubjectQuery request, CancellationToken ct)
    {
        if (request.SubjectType == ExpirySignableActions.ExceptionSubjectType
            && await _repository.FindCaseAsync(request.SubjectId, ct) is { } c)
        {
            return Response<ExpirySignableSubjectDto>.Success(ExpirySignableSubjects.ExceptionSubject(c));
        }

        if (request.SubjectType == ExpirySignableActions.DateOverrideSubjectType
            && await _repository.FindDateOverrideAsync(request.SubjectId, ct) is { } o && o.CreatedBy == _support.UserId)
        {
            return Response<ExpirySignableSubjectDto>.Success(ExpirySignableSubjects.DateOverrideSubject(o));
        }

        return Response<ExpirySignableSubjectDto>.Fail(ExpiryErrorCodes.ExceptionNotFound, "Signable subject not found.", 404);
    }
}
