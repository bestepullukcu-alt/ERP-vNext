using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.CommandHandlers;

/// <summary>
/// <c>bulkDeleteLocationRoles</c> (pack §8.1 "Toplu komutlar", WMS draft §4 rule 5). The key is looked up among the caller's key events of THIS
/// operation only (a single <c>deleteLocationRole</c> under the same key is another request — independent review 11 F-2): found with the same
/// fingerprint → replay (a row that carries the key answers <c>Deleted</c> with its current version, another row <c>Conflict</c>, an unknown
/// one <c>NotFound</c>); other fingerprint → 400. Otherwise every row is soft-deleted at its own version in ONE transaction, each with its
/// <c>Deleted</c> key event; a row that is gone answers <c>NotFound</c>, a stale one <c>Conflict</c>. The central audit event is enlisted only
/// when at least one row was deleted (no row → no record, no key).
/// </summary>
public sealed class BulkDeleteWarehouseLocationRoleHandler : IRequestHandler<BulkDeleteWarehouseLocationRoleCommand, Response<BulkDeleteLocationRolesResultDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly LocationRoleChecks _checks;
    private readonly WarehouseSupport _support;

    public BulkDeleteWarehouseLocationRoleHandler(IWarehouseLocationRoleRepository repository, LocationRoleChecks checks, WarehouseSupport support)
    {
        _repository = repository;
        _checks = checks;
        _support = support;
    }

    public async Task<Response<BulkDeleteLocationRolesResultDto>> Handle(BulkDeleteWarehouseLocationRoleCommand request, CancellationToken ct)
    {
        if (request.Items.Count is 0 or > WarehouseContract.MaxBulkItems)
        {
            return WarehouseSupport.Invalid<BulkDeleteLocationRolesResultDto>($"items must hold 1-{WarehouseContract.MaxBulkItems} rows.");
        }

        if (request.Items.Select(i => i.RoleId).Distinct().Count() != request.Items.Count)
        {
            return WarehouseSupport.Invalid<BulkDeleteLocationRolesResultDto>("items must not repeat a roleId.");
        }

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpBulkDeleteLocationRoles),
            ("items", WarehouseSupport.List(request.Items.Select(i => $"{i.RoleId:D}:{i.Version}").OrderBy(i => i, StringComparer.Ordinal))));
        if (await ReplayAsync(request, fingerprint, ct) is { } replay) return replay;

        var live = (await _repository.FindManyAsync(request.Items.Select(i => i.RoleId).ToList(), ct)).ToDictionary(r => r.Id);
        var busy = new HashSet<Guid>();
        foreach (var role in live.Values)
        {
            if (await _checks.OpenTasksAsync<bool>(role.LocationId, ct) is not null) busy.Add(role.Id); // DL-2 / AC-58: the row answers Conflict
        }
        var now = _support.Now();
        var rows = new Dictionary<Guid, BulkDeleteLocationRoleRowDto>();

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await _repository.InTransactionAsync(async (session, tx) =>
            {
                rows.Clear(); // the driver may re-run the body after a transient conflict
                var deleted = 0;
                foreach (var item in request.Items)
                {
                    if (!live.TryGetValue(item.RoleId, out var stored))
                    {
                        rows[item.RoleId] = new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowNotFound);
                        continue;
                    }

                    if (stored.Version != item.Version || busy.Contains(stored.Id))
                    {
                        rows[item.RoleId] = new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowConflict);
                        continue;
                    }

                    var role = Deleted(stored, item.Version, now, _support.UserId);
                    if (!await session.ReplaceRoleAsync(role, item.Version, tx))
                    {
                        rows[item.RoleId] = new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowConflict);
                        continue;
                    }

                    await session.InsertEventAsync(DeleteWarehouseLocationRoleHandler.DeletedEvent(_support, role, WarehouseLocationRoleSnapshot.Of(stored), now,
                        request.IdempotencyKey, WarehouseContract.OpBulkDeleteLocationRoles, fingerprint), tx);
                    rows[item.RoleId] = new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowDeleted, role.Version);
                    deleted++;
                }

                return deleted == 0 ? WarehouseWriteOutcome.None : new WarehouseWriteOutcome(deleted, null);
            }, ct);
        }
        catch (WarehouseLocationRoleDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected == 0 && await ReplayAsync(request, fingerprint, ct) is { } twin) return twin;

        return Response<BulkDeleteLocationRolesResultDto>.Success(new BulkDeleteLocationRolesResultDto(
            request.Items.Select(i => rows.TryGetValue(i.RoleId, out var row) ? row : new BulkDeleteLocationRoleRowDto(i.RoleId.ToString("D"), WarehouseContract.RowConflict)).ToList()));
    }

    private static WarehouseLocationRole Deleted(WarehouseLocationRole stored, int version, DateTimeOffset now, Guid by)
    {
        var copy = stored.Copy();
        copy.IsDeleted = true;
        copy.DeletedAt = now;
        copy.UpdatedAt = now;
        copy.UpdatedBy = by;
        copy.Version = version + 1;
        return copy;
    }

    private async Task<Response<BulkDeleteLocationRolesResultDto>?> ReplayAsync(BulkDeleteWarehouseLocationRoleCommand request, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindBulkKeyEventsAsync(_support.UserId, WarehouseContract.OpBulkDeleteLocationRoles, request.IdempotencyKey, ct);
        if (earlier.Count == 0) return null;
        if (earlier.Any(e => e.RequestFingerprint != fingerprint)) return WarehouseSupport.KeyReused<BulkDeleteLocationRolesResultDto>();

        var written = earlier.Select(e => e.RoleId).ToHashSet();
        var rows = new List<BulkDeleteLocationRoleRowDto>();
        foreach (var item in request.Items)
        {
            var current = await _repository.FindIncludingDeletedAsync(item.RoleId, ct);
            rows.Add(current is null
                ? new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowNotFound)
                : written.Contains(item.RoleId)
                    ? new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowDeleted, current.Version)
                    : new BulkDeleteLocationRoleRowDto(item.RoleId.ToString("D"), WarehouseContract.RowConflict));
        }

        return Response<BulkDeleteLocationRolesResultDto>.Success(new BulkDeleteLocationRolesResultDto(rows));
    }
}
