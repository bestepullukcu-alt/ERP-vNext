using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;

public sealed class LocalTestMod0117ContextAdapter : IMod0117ContextValidationAdapter
{
    public Task ValidateAsync(ExternalContextReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = new ExternalContextReference(reference.ContractName, reference.ContractVersion, reference.ContextKind, reference.ContextId);
        return Task.CompletedTask;
    }
}

public sealed class LocalTestFu16AuthorizationAdapter : IFu16DwsAuthorizationAdapter
{
    public Task AuthorizeAsync(Guid tenantId, Guid actorId, string operation, string permission, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty || actorId == Guid.Empty) throw new DwsValidationException(DwsErrors.AuthenticationRequired);
        if (!string.Equals(DwsAuthorizationManifest.RequireExact(operation), permission, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.PermissionDenied);
        return Task.CompletedTask;
    }
}

public sealed record DwsSimulatedAudit(Guid TenantId, Guid ActorId, string Operation);

public sealed class LocalTestDwsAuditSimulator : IDwsAuditSimulator
{
    private readonly ConcurrentQueue<DwsSimulatedAudit> _records = new();
    public IReadOnlyCollection<DwsSimulatedAudit> Records => _records.ToArray();
    public Task RecordAsync(Guid tenantId, Guid actorId, string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(new(tenantId, actorId, operation));
        return Task.CompletedTask;
    }
}

public sealed class DwsMongoLocalActionExecutor(
    DwsMongoAtomicWriter writer,
    IMod0117ContextValidationAdapter contexts,
    IFu16DwsAuthorizationAdapter authorization,
    IDwsAuditSimulator audit) : IDwsLocalActionExecutor
{
    public async Task<DwsLocalResult> ExecuteAsync(DwsDispatchRequest request, CancellationToken cancellationToken)
    {
        var permission = DwsAuthorizationManifest.RequireExact(request.Operation);
        await authorization.AuthorizeAsync(request.Context.TenantId, request.Context.ActorId, request.Operation, permission, cancellationToken);
        if (request.Contract is CreateStructureCommand create) await contexts.ValidateAsync(create.ExternalContextReference, cancellationToken);

        if (request.Operation.EndsWith("Query", StringComparison.Ordinal))
            return new(request.Operation, "validated", Correlation(request.Context, request.Operation));

        var familyName = request.Operation[..^"Command".Length];
        var family = DwsPersistenceOwnershipManifest.Transactions.Single(value => value.Name == familyName);
        var payloadHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request.Contract, request.Contract.GetType()))).ToLowerInvariant();
        var correlation = Correlation(request.Context, request.Operation);
        var structureId = StructureId(request);
        var expectedVersion = ExpectedVersion(request.Contract);
        var participants = family.BusinessCollections
            .Select(alias => Participant(alias, request, structureId, expectedVersion, payloadHash, correlation))
            .Concat(DwsTransactionFamily.TechnicalParticipants.Select(alias => Participant(alias, request, structureId, 0, payloadHash, correlation)))
            .ToArray();
        await writer.ExecuteAsync(new(request.Context.TenantId, familyName, request.Context.IdempotencyKey, payloadHash, participants), cancellationToken: cancellationToken);
        await audit.RecordAsync(request.Context.TenantId, request.Context.ActorId, request.Operation, cancellationToken);
        return new(request.Operation, "succeeded", correlation);
    }

    private static DwsMongoParticipant Participant(string alias, DwsDispatchRequest request, Guid structureId, int expectedVersion, string payloadHash, string correlation)
    {
        var version = alias is "receipts" or "audit-intents" or "outbox" ? 0 : request.Contract is CreateStructureCommand ? 0 : expectedVersion;
        var values = new BsonDocument("Value", request.Operation)
        {
            ["CorrelationId"] = correlation,
            ["StructureDefinitionId"] = new BsonBinaryData(structureId, GuidRepresentation.Standard)
        };
        if (alias == "receipts")
        {
            values["CommandFamily"] = request.Operation[..^"Command".Length];
            values["IdempotencyKey"] = request.Context.IdempotencyKey;
            values["RequestPayloadHash"] = payloadHash;
        }
        return new(alias, DeterministicGuid(request.Context.TenantId, request.Context.IdempotencyKey, alias), version, values);
    }

    private static Guid StructureId(DwsDispatchRequest request) => request.Contract switch
    {
        CreateStructureCommand => DeterministicGuid(request.Context.TenantId, request.Context.IdempotencyKey, "structure"),
        UpdateStructureMetadataCommand value => value.StructureDefinitionId,
        AddStructureNodeCommand value => value.StructureDefinitionId,
        MoveStructureNodeCommand value => value.StructureDefinitionId,
        ReorderStructureNodeCommand value => value.StructureDefinitionId,
        RemoveStructureNodeCommand value => value.StructureDefinitionId,
        AddStructuralDependencyCommand value => value.StructureDefinitionId,
        RemoveStructuralDependencyCommand value => value.StructureDefinitionId,
        CreateStructureBaselineCommand value => value.StructureDefinitionId,
        CreateNextStructureRevisionCommand value => value.StructureDefinitionId,
        _ => throw new DwsValidationException(DwsErrors.InvalidRequest)
    };

    private static int ExpectedVersion(IDwsRequestContract contract) => contract switch
    {
        UpdateStructureMetadataCommand value => value.ExpectedRevisionVersion,
        AddStructureNodeCommand value => value.ExpectedRevisionVersion,
        MoveStructureNodeCommand value => value.ExpectedRevisionVersion,
        ReorderStructureNodeCommand value => value.ExpectedRevisionVersion,
        RemoveStructureNodeCommand value => value.ExpectedRevisionVersion,
        AddStructuralDependencyCommand value => value.ExpectedRevisionVersion,
        RemoveStructuralDependencyCommand value => value.ExpectedRevisionVersion,
        CreateStructureBaselineCommand value => value.ExpectedRevisionVersion,
        CreateNextStructureRevisionCommand value => value.ExpectedDefinitionVersion,
        _ => 0
    };

    private static string Correlation(DwsTrustedContext context, string operation) => DeterministicGuid(context.TenantId, context.IdempotencyKey, operation).ToString("D");
    private static Guid DeterministicGuid(Guid tenantId, string key, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:D}|{key}|{discriminator}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

public static class DwsLocalTestComposition
{
    public static IServiceCollection AddDwsLocalTestInfrastructure(this IServiceCollection services, IMongoClient client, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(client);
        services.AddSingleton(new DwsMongoContext(client, databaseName));
        services.AddSingleton<DwsMongoIndexInitializer>();
        services.AddSingleton<DwsMongoAtomicWriter>();
        services.AddSingleton<IMod0117ContextValidationAdapter, LocalTestMod0117ContextAdapter>();
        services.AddSingleton<IFu16DwsAuthorizationAdapter, LocalTestFu16AuthorizationAdapter>();
        services.AddSingleton<IDwsAuditSimulator, LocalTestDwsAuditSimulator>();
        services.AddScoped<IDwsLocalActionExecutor, DwsMongoLocalActionExecutor>();
        services.AddSingleton<DwsLocalMod0117Fixture>();
        services.AddSingleton<IMod0117DwsContextValidator, LocalTestMod0117FunctionalContextValidator>();
        services.AddSingleton<DwsLocalFu16Fixture>();
        services.AddSingleton<IFu16DwsFunctionalAuthorization, LocalTestFu16FunctionalAuthorization>();
        services.AddScoped<IDwsLocalAuditObserver, LocalTestDwsAuditObserver>();
        return services;
    }
}
