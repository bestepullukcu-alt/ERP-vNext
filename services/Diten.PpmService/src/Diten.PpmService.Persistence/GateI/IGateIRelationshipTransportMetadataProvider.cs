using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.GateI;


public interface IGateIRelationshipTransportMetadataProvider
{
    ValueTask<TrustedTransportMetadata> CreateAsync(
        EventMetadata metadata,
        ReadOnlyMemory<byte> canonicalPayloadUtf8,
        CancellationToken cancellationToken);
}
