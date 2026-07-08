using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class SearchPersonReferencesQueryHandler : IRequestHandler<SearchPersonReferencesQuery, Response<PersonReferenceSearchResultDto>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly IPersonReferenceRepository _repository;

    public SearchPersonReferencesQueryHandler(IPersonReferenceRepository repository) => _repository = repository;

    public async Task<Response<PersonReferenceSearchResultDto>> Handle(SearchPersonReferencesQuery request, CancellationToken ct)
    {
        var status = ParseStatus(request.Status);
        if (!status.IsSuccessful)
        {
            return Response<PersonReferenceSearchResultDto>.Fail(status.Errors, status.StatusCode);
        }

        var page = request.Page <= 0 ? DefaultPage : request.Page;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);
        var skip = (page - 1) * pageSize;
        IReadOnlyList<PersonReference> items;
        try
        {
            items = await _repository.SearchAsync(request.Query, status.Data, skip, pageSize, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Response<PersonReferenceSearchResultDto>.Fail("Person reference repository unavailable.", 503);
        }

        var dto = new PersonReferenceSearchResultDto(items.Select(TenantOrganizationMapper.ToDto).ToList(), page, pageSize);

        return Response<PersonReferenceSearchResultDto>.Success(dto);
    }

    private static Response<PersonReferenceStatus?> ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Response<PersonReferenceStatus?>.Success(null);
        }

        return Enum.TryParse<PersonReferenceStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? Response<PersonReferenceStatus?>.Success(parsed)
            : Response<PersonReferenceStatus?>.Fail("Invalid person reference status.", 400);
    }
}
