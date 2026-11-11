using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Expiry.Queries;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>The read side shared by the exception handlers: a case + its events + its warehouse, as the wire detail.</summary>
public sealed class ExpiryReads
{
    private readonly IExpiryRepository _repository;
    private readonly ILocationRepository _locations;
    private readonly ExpirySupport _support;

    public ExpiryReads(IExpiryRepository repository, ILocationRepository locations, ExpirySupport support)
    {
        _repository = repository;
        _locations = locations;
        _support = support;
    }

    public async Task<IReadOnlyDictionary<Guid, Location>> WarehousesAsync(IEnumerable<FefoExceptionCase> cases, CancellationToken ct)
    {
        var ids = cases.Select(c => Guid.TryParse(c.WarehouseId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        return ids.Count == 0 ? new Dictionary<Guid, Location>() : (await _locations.FindManyAsync(ids, ct)).ToDictionary(l => l.Id);
    }

    public async Task<ExpiryExceptionDetailDto> DetailAsync(FefoExceptionCase exceptionCase, CancellationToken ct)
        => ExpiryMapping.Detail(exceptionCase, await _repository.EventsAsync(exceptionCase.Id, ct), await WarehousesAsync([exceptionCase], ct), _support.Now());

    public async Task<Response<ExpiryExceptionDetailDto>> CurrentAsync(Guid caseId, CancellationToken ct)
        => await _repository.FindCaseAsync(caseId, ct) is { } c
            ? Response<ExpiryExceptionDetailDto>.Success(await DetailAsync(c, ct))
            : ExpirySupport.CaseNotFound<ExpiryExceptionDetailDto>();
}

/// <summary>§4.6 getException — scoped to the caller's company (another company's id → the controller answers 403).</summary>
public sealed class GetExpiryExceptionHandler : IRequestHandler<GetExpiryExceptionQuery, Response<ExpiryExceptionDetailDto>>
{
    private readonly ExpiryReads _reads;

    public GetExpiryExceptionHandler(ExpiryReads reads) => _reads = reads;

    public Task<Response<ExpiryExceptionDetailDto>> Handle(GetExpiryExceptionQuery request, CancellationToken ct)
        => ExpirySupport.TryParseId(request.ExceptionId, out var id)
            ? _reads.CurrentAsync(id, ct)
            : Task.FromResult(ExpirySupport.CaseNotFound<ExpiryExceptionDetailDto>());
}

/// <summary>
/// §4.5 listExceptions — filters type, status (repeatable), late, warehouseId, assignedToMe, q (number / lot number); sort
/// <c>dueAtUtc</c> (default) or <c>-openedAtUtc</c>; paging 1–200.
/// </summary>
public sealed class ListExpiryExceptionsHandler : IRequestHandler<ListExpiryExceptionsQuery, Response<ExpiryExceptionPageDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpiryReads _reads;
    private readonly ExpirySupport _support;

    public ListExpiryExceptionsHandler(IExpiryRepository repository, ExpiryReads reads, ExpirySupport support)
    {
        _repository = repository;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<ExpiryExceptionPageDto>> Handle(ListExpiryExceptionsQuery request, CancellationToken ct)
    {
        ExpiryExceptionType? type = null;
        if (request.Type is not null)
        {
            if (!ExpiryWire.TryParseType(request.Type, out var parsed)) return ExpirySupport.Invalid<ExpiryExceptionPageDto>("Unknown type.");
            type = parsed;
        }

        var statuses = new List<ExpiryExceptionStatus>();
        foreach (var raw in request.Status)
        {
            if (!ExpiryWire.TryParseName<ExpiryExceptionStatus>(raw, out var status)) return ExpirySupport.Invalid<ExpiryExceptionPageDto>("Unknown status.");
            statuses.Add(status);
        }

        if (request.Late is not (null or "true" or "false") || request.AssignedToMe is not (null or "true" or "false"))
        {
            return ExpirySupport.Invalid<ExpiryExceptionPageDto>("late and assignedToMe must be true or false.");
        }

        if (request.WarehouseId is not null && !ExpirySupport.TryParseId(request.WarehouseId, out _))
        {
            return ExpirySupport.Invalid<ExpiryExceptionPageDto>("warehouseId must be a canonical UUID.");
        }

        if (request.Sort is not (null or "dueAtUtc" or "-openedAtUtc"))
        {
            return ExpirySupport.Invalid<ExpiryExceptionPageDto>("sort must be dueAtUtc or -openedAtUtc.");
        }

        if (request.Q is { Length: > 100 })
        {
            return ExpirySupport.Invalid<ExpiryExceptionPageDto>("q must be at most 100 characters.");
        }

        if (ExpiryPaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return ExpirySupport.Invalid<ExpiryExceptionPageDto>($"page must be ≥ 1 and pageSize 1-{ExpiryContract.MaxPageSize}.");
        }

        var now = _support.Now();
        var result = await _repository.ListCasesAsync(new ExpiryExceptionQuery(
            type, statuses.Count == 0 ? null : statuses, request.Late is null ? null : request.Late == "true", now, request.WarehouseId,
            request.AssignedToMe == "true" ? _support.UserId : null, string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            request.Sort != "-openedAtUtc", paging.Page, paging.PageSize), ct);
        var warehouses = await _reads.WarehousesAsync(result.Items, ct);
        return Response<ExpiryExceptionPageDto>.Success(new ExpiryExceptionPageDto(
            result.Items.Select(c => ExpiryMapping.Ref(c, warehouses, now)).ToList(), result.Total, paging.Page, paging.PageSize));
    }
}
