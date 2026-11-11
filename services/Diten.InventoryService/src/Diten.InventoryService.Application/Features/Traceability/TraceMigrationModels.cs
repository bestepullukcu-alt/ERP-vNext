using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Traceability;

// ── wire (trace.openapi.yaml §4.8 + 1.2.0) ───────────────────────────────────────────────────────────────

/// <summary>listManualLotGroups 200 item (<c>ManualLotGroup</c>).</summary>
public sealed class ManualLotGroupDto
{
    public string GroupId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public string TypedText { get; init; } = string.Empty;
    public int TransactionCount { get; init; }
    public string OnHand { get; init; } = "0";
    public string? BaseUomId { get; init; }
    public ManualLotSuggestionDto Suggestion { get; init; } = new();
}

public sealed class ManualLotSuggestionDto
{
    public string Kind { get; init; } = nameof(LotMatchKind.None);
    public string? LotId { get; init; }
    public string? GroupId { get; init; }
    public string Note { get; init; } = string.Empty;
}

/// <summary>listManualLotGroups 200 (<c>ManualLotGroupPage</c>); <see cref="Completed"/> only once the commit ran.</summary>
public sealed class ManualLotGroupPageDto
{
    public IReadOnlyList<ManualLotGroupDto> Items { get; init; } = [];
    public long Total { get; init; }
    public ManualLotGroupTotalsDto Totals { get; init; } = new();
    public MigrationResultDto? Completed { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class ManualLotGroupTotalsDto
{
    public int Groups { get; init; }
    public int Transactions { get; init; }
}

/// <summary>commitManualLotMigration 200 (<c>MigrationResult</c>).</summary>
public sealed class MigrationResultDto
{
    public string MigrationId { get; init; } = string.Empty;
    public string CompletedAt { get; init; } = string.Empty;
    public string CompletedBy { get; init; } = string.Empty;
    public int TransactionsLinked { get; init; }
    public int LotsLinked { get; init; }
    public int LotsCreated { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>getMovementLotLinks 200 (<c>MovementLotLinks</c>).</summary>
public sealed class MovementLotLinksDto
{
    public IReadOnlyList<MovementLotLinkDto> Items { get; init; } = [];
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class MovementLotLinkDto
{
    public string TransactionId { get; init; } = string.Empty;
    public string TypedText { get; init; } = string.Empty;
    public string LotId { get; init; } = string.Empty;
    public string MigrationId { get; init; } = string.Empty;
}

/// <summary>getManualLotMigrationStatus 200 (1.2.0, CT K4-1); <see cref="CompletedAt"/> only when closed.</summary>
public sealed class ManualLotMigrationStatusDto
{
    public bool ManualLotEntryClosed { get; init; }
    public string? CompletedAt { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public static class TraceMigrationMapping
{
    public static MigrationResultDto ToResult(ManualLotMigration migration) => new()
    {
        MigrationId = TraceMapping.Id(migration.Id),
        CompletedAt = TraceMapping.Wire(migration.CompletedAt) ?? string.Empty,
        CompletedBy = migration.CompletedBy is { } by ? TraceMapping.Id(by) : string.Empty,
        TransactionsLinked = migration.TransactionsLinked,
        LotsLinked = migration.LotsLinked,
        LotsCreated = migration.LotsCreated
    };

    public static ManualLotGroupDto ToGroup(TypedLotGroupStock row, LotMatchSuggestion suggestion) => new()
    {
        GroupId = row.Group.GroupId,
        ItemId = TraceMapping.Id(row.Group.ItemId),
        SkuId = TraceMapping.Id(row.Group.SkuId),
        SkuLevel = TraceMapping.Wire(row.Group.SkuLevel),
        TypedText = row.Group.TypedText,
        TransactionCount = row.Group.TransactionCount,
        OnHand = InventoryQuantity.ToWire(row.OnHand),
        BaseUomId = row.BaseUomId,
        Suggestion = new ManualLotSuggestionDto
        {
            Kind = suggestion.Kind.ToString(),
            LotId = suggestion.LotId is { } lot ? TraceMapping.Id(lot) : null,
            GroupId = suggestion.GroupId,
            Note = suggestion.Note
        }
    };
}

// ── requests ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>GET /api/trace/migration/manual-lots (<c>listManualLotGroups</c>). Raw query values.</summary>
public sealed record ListManualLotGroupsQuery(string? View, string? Page, string? PageSize) : IRequest<Response<ManualLotGroupPageDto>>;

/// <summary>GET /api/trace/migration/links (<c>getMovementLotLinks</c>).</summary>
public sealed record GetMovementLotLinksQuery(string? TransactionId, string? LotId) : IRequest<Response<MovementLotLinksDto>>;

/// <summary>GET /api/trace/migration/status (<c>getManualLotMigrationStatus</c>, 1.2.0).</summary>
public sealed record GetManualLotMigrationStatusQuery : IRequest<Response<ManualLotMigrationStatusDto>>;

/// <summary>POST /api/trace/migration/manual-lots/commit (<c>commitManualLotMigration</c>). Raw values; the handler parses.</summary>
public sealed record CommitManualLotMigrationCommand : IRequest<Response<MigrationResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLotSerial("Execute", "ManualLotMigration");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<MigrationResultDto>(response)?.MigrationId);

    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? Reason { get; init; }
    public int? ExpectedGroupCount { get; init; }
    public IReadOnlyList<MigrationDecisionRaw> Decisions { get; init; } = [];
}

/// <summary>One <c>MigrationDecision</c> as read from the body (raw strings; <see cref="NewLot"/> = <c>LotFields</c>).</summary>
public sealed record MigrationDecisionRaw(string? GroupId, string? Kind, string? LotId, string? TargetGroupId, MigrationNewLotRaw? NewLot);

public sealed record MigrationNewLotRaw(
    string? LotNumber,
    string? ManufacturerLot,
    string? SupplierLot,
    string? ManufacturingDate,
    string? ExpiryDate,
    string? RetestDate,
    string? CountryOfOrigin,
    string? CoaReference);
