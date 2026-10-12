using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>getSerial (TRACE v1): status Blocked is derived from the serial's lot; unknown → 404 <c>UNKNOWN_SERIAL</c>.</summary>
public sealed class GetSerialHandler : IRequestHandler<GetSerialQuery, Response<SerialRefDto>>
{
    private readonly ISerialRepository _serials;
    private readonly ILotRepository _lots;

    public GetSerialHandler(ISerialRepository serials, ILotRepository lots)
    {
        _serials = serials;
        _lots = lots;
    }

    public async Task<Response<SerialRefDto>> Handle(GetSerialQuery request, CancellationToken cancellationToken)
    {
        var serial = TraceWriteSupport.TryParseId(request.SerialId, out var serialId)
            ? await _serials.FindAsync(serialId, cancellationToken)
            : null;
        if (serial is null)
            return TraceWriteSupport.Fail<SerialRefDto>(TraceErrorCodes.UnknownSerial, "Serial not found.", 404);

        var lot = serial.LotId is { } lotId ? await _lots.FindAsync(lotId, cancellationToken) : null;
        return Response<SerialRefDto>.Success(TraceMapping.ToSerial(serial, lot));
    }
}
