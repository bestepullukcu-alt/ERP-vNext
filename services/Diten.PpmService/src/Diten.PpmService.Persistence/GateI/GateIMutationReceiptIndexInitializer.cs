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


public sealed class GateIMutationReceiptIndexInitializer(IMongoDatabase database)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var receipts = database.GetCollection<GateIMutationReceiptDocument>(
            PpmCollectionNames.GateIMutationReceipts);
        var keys = Builders<GateIMutationReceiptDocument>.IndexKeys
            .Ascending(item => item.TenantId)
            .Ascending(item => item.OperationId)
            .Ascending(item => item.IdempotencyKey);
        await receipts.Indexes.CreateOneAsync(
            new CreateIndexModel<GateIMutationReceiptDocument>(
                keys,
                new CreateIndexOptions
                {
                    Name = "ux_ppm_gate_i_receipt_scope",
                    Unique = true
                }),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
