using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;
using Diten.InventoryService.Domain.Traceability.Repositories;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// validateTrace (TRACE v1, typed results v1.1): one result per requested lot id (<c>lotId</c>) and serial id
/// (<c>serialId</c>), in request order. <c>exists</c> = in the caller's tenant; <c>usable</c> = a Draft / Released lot,
/// or an Active serial of such a lot (a Consumed serial or one of a Blocked / Expired lot is not usable).
/// </summary>
public sealed class ValidateTraceHandler : IRequestHandler<ValidateTraceQuery, Response<ValidateResponseDto>>
{
    public const int MaxIds = 500;

    private readonly ILotRepository _lots;
    private readonly ISerialRepository _serials;

    public ValidateTraceHandler(ILotRepository lots, ISerialRepository serials)
    {
        _lots = lots;
        _serials = serials;
    }

    public async Task<Response<ValidateResponseDto>> Handle(ValidateTraceQuery request, CancellationToken cancellationToken)
    {
        if (request.LotIds.Count + request.SerialIds.Count > MaxIds)
            return TraceWriteSupport.Invalid<ValidateResponseDto>($"At most {MaxIds} ids per request.");

        var lotIds = request.LotIds.Select(Parse).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var serialIds = request.SerialIds.Select(Parse).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var serials = (await _serials.FindManyAsync(serialIds, cancellationToken)).ToDictionary(s => s.Id);
        var lots = (await _lots.FindManyAsync(lotIds.Concat(serials.Values.Where(s => s.LotId.HasValue).Select(s => s.LotId!.Value)).Distinct().ToList(), cancellationToken))
            .ToDictionary(l => l.Id);

        var results = new List<ValidateResultDto>();
        foreach (var raw in request.LotIds)
        {
            var lot = Parse(raw) is { } id && lots.TryGetValue(id, out var found) ? found : null;
            results.Add(lot is null
                ? new ValidateResultDto { LotId = raw, Exists = false }
                : new ValidateResultDto
                {
                    LotId = raw,
                    Exists = true,
                    Status = lot.Status.ToString(),
                    SkuId = TraceMapping.Id(lot.SkuId),
                    SkuLevel = WireNames.ToWire(lot.SkuLevel),
                    Usable = LotStatusRules.IsUsable(lot.Status)
                });
        }

        foreach (var raw in request.SerialIds)
        {
            var serial = Parse(raw) is { } id && serials.TryGetValue(id, out var found) ? found : null;
            if (serial is null)
            {
                results.Add(new ValidateResultDto { SerialId = raw, Exists = false });
                continue;
            }

            var lot = serial.LotId is { } lotId && lots.TryGetValue(lotId, out var l) ? l : null;
            var status = TraceMapping.WireSerialStatus(serial.Status, lot?.Status);
            results.Add(new ValidateResultDto
            {
                SerialId = raw,
                Exists = true,
                Status = status,
                SkuId = TraceMapping.Id(serial.SkuId),
                SkuLevel = WireNames.ToWire(serial.SkuLevel),
                Usable = status == "Active"
            });
        }

        return Response<ValidateResponseDto>.Success(new ValidateResponseDto { Results = results });
    }

    private static Guid? Parse(string raw) => TraceWriteSupport.TryParseId(raw, out var id) ? id : null;
}
