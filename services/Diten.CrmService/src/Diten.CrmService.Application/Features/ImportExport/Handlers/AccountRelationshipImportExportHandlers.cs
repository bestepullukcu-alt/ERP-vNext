using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;

namespace Diten.CrmService.Application.Features.ImportExport.Handlers;

public sealed class ImportAccountRelationshipsHandler : IRequestHandler<ImportAccountRelationshipsCommand, Response<ImportResultDto>>
{
    private const string TypeSet = "account-relationship-type";
    private const string StatusSet = "account-relationship-status";
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IReferenceMetadataReader _metadataReader;
    private readonly IContactAuditPublisher _audit;

    public ImportAccountRelationshipsHandler(
        ITenantContext tenant, IAccountRepository accounts, IAccountRelationshipRepository relationships,
        IReferenceDataValidator referenceValidator, IReferenceMetadataReader metadataReader, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _relationships = relationships;
        _referenceValidator = referenceValidator;
        _metadataReader = metadataReader;
        _audit = audit;
    }

    public async Task<Response<ImportResultDto>> Handle(ImportAccountRelationshipsCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ImportResultDto>.Fail("Tenant context is required.", 400);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var cache = new ReferenceCache(_referenceValidator);
        var metadataCache = new Dictionary<string, RelationshipTypeMetadata>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ImportRowErrorDto>();
        int valid = 0, created = 0, conflicts = 0;

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNo = i + 1;
            var rowErrors = new List<ImportRowErrorDto>();

            var sourceId = await ResolveAccountAsync(tenantId, row.SourceAccountId, row.SourceAccountCode, rowNo, "Source", rowErrors, cancellationToken);
            var targetId = await ResolveAccountAsync(tenantId, row.TargetAccountId, row.TargetAccountCode, rowNo, "Target", rowErrors, cancellationToken);

            await CheckRefAsync(cache, rowNo, "RelationshipType", TypeSet, row.RelationshipType, rowErrors, cancellationToken);
            await CheckRefAsync(cache, rowNo, "Status", StatusSet, row.Status, rowErrors, cancellationToken);

            if (row.ValidFrom is { } f && row.ValidTo is { } t && f > t)
            {
                rowErrors.Add(new(rowNo, "Validity", "invalid", "ValidFrom must be on or before ValidTo.", "error"));
            }

            RelationshipTypeMetadata? metadata = null;
            if (!string.IsNullOrWhiteSpace(row.RelationshipType) && rowErrors.All(e => e.Field != "RelationshipType"))
            {
                var type = row.RelationshipType!.Trim();
                if (!metadataCache.TryGetValue(type, out var md))
                {
                    md = RelationshipTypeMetadata.Parse(await _metadataReader.GetValueAttributesAsync(TypeSet, type, cancellationToken));
                    metadataCache[type] = md;
                }
                metadata = md;
            }

            var isConflict = false;
            if (sourceId is { } sid && targetId is { } tid && metadata is { } md2 && rowErrors.Count == 0)
            {
                var type = row.RelationshipType!.Trim();
                if (sid == tid && !md2.SelfAllowed)
                {
                    rowErrors.Add(new(rowNo, "Target", "invalid", "A self-relationship is not allowed for this relationship type.", "error"));
                }
                else if (await _relationships.ExistsActivePairAsync(tenantId, sid, tid, type, md2.IsBidirectional, excludeId: null, cancellationToken))
                {
                    rowErrors.Add(new(rowNo, "RelationshipType", "conflict", "An active relationship of this type already exists between the accounts.", "conflict"));
                    isConflict = true;
                }
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                if (isConflict) conflicts++;
                continue;
            }

            valid++;
            if (request.DryRun)
            {
                continue;
            }

            await _relationships.InsertAsync(new DomainRel
            {
                TenantId = tenantId,
                SourceAccountId = sourceId!.Value,
                TargetAccountId = targetId!.Value,
                RelationshipType = row.RelationshipType!.Trim(),
                Direction = metadata!.DirectionCode,
                Status = row.Status!.Trim(),
                ValidFrom = row.ValidFrom,
                ValidTo = row.ValidTo,
                Notes = row.Notes?.Trim()
            }, cancellationToken);
            created++;
        }

        var invalid = request.Rows.Count - valid;
        await _audit.PublishAsync("crm.account-relationship.imported", tenantId, Guid.Empty,
            $"corr={correlationId} dryRun={request.DryRun} total={request.Rows.Count} created={created}", cancellationToken);
        return Response<ImportResultDto>.Success(new ImportResultDto(correlationId, request.DryRun, request.Rows.Count, valid, invalid, created, 0, 0, conflicts, errors));
    }

    private async Task<Guid?> ResolveAccountAsync(Guid tenantId, Guid? accountId, string? accountCode, int rowNo, string label, List<ImportRowErrorDto> errors, CancellationToken ct)
    {
        if (accountId is { } id)
        {
            if (await _accounts.GetByIdAsync(tenantId, id, ct) is not null) return id;
            errors.Add(new(rowNo, $"{label}AccountId", "not_found", $"{label} account not found.", "error"));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            var account = await _accounts.GetByCodeAsync(tenantId, accountCode.Trim(), ct);
            if (account is not null) return account.Id;
            errors.Add(new(rowNo, $"{label}AccountCode", "not_found", $"{label} account with code '{accountCode}' not found.", "error"));
            return null;
        }

        errors.Add(new(rowNo, $"{label}Account", "required", $"{label}AccountId or {label}AccountCode is required.", "error"));
        return null;
    }

    private static async Task CheckRefAsync(ReferenceCache cache, int rowNo, string field, string setCode, string? value, List<ImportRowErrorDto> errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new(rowNo, field, "required", $"'{setCode}' is required.", "error"));
            return;
        }

        switch (await cache.StatusAsync(setCode, value, ct))
        {
            case ReferenceValidationStatus.InvalidValue:
                errors.Add(new(rowNo, field, "invalid_reference", $"'{value}' is not a valid published value of reference set '{setCode}'.", "error"));
                break;
            case ReferenceValidationStatus.SetMissing:
                errors.Add(new(rowNo, field, "set_missing", $"Required reference set '{setCode}' is not published yet.", "error"));
                break;
        }
    }
}

public sealed class ExportAccountRelationshipsHandler : IRequestHandler<ExportAccountRelationshipsQuery, Response<string>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IContactAuditPublisher _audit;

    public ExportAccountRelationshipsHandler(ITenantContext tenant, IAccountRelationshipRepository relationships, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _relationships = relationships;
        _audit = audit;
    }

    public async Task<Response<string>> Handle(ExportAccountRelationshipsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<string>.Fail("Tenant context is required.", 400);
        }

        var relationships = await _relationships.ListAllAsync(tenantId, cancellationToken);
        var rows = relationships.Select(r => (IReadOnlyList<object?>)new object?[]
        {
            string.Empty, r.SourceAccountId, string.Empty, r.TargetAccountId, r.RelationshipType, r.Status, r.ValidFrom, r.ValidTo, r.Notes
        });

        await _audit.PublishAsync("crm.account-relationship.exported", tenantId, Guid.Empty, $"count={relationships.Count}", cancellationToken);
        return Response<string>.Success(Csv.Build(ImportTemplates.AccountRelationshipHeader, rows));
    }
}
