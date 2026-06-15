using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ProvisionBusinessReferenceDataEvidenceFixtureCommandHandler : IRequestHandler<ProvisionBusinessReferenceDataEvidenceFixtureCommand, Response<BusinessReferenceDataEvidenceFixtureProvisionModel>>
{
    private const string FixturePrefix = "FX15F";
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public ProvisionBusinessReferenceDataEvidenceFixtureCommandHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataEvidenceFixtureProvisionModel>> Handle(ProvisionBusinessReferenceDataEvidenceFixtureCommand request, CancellationToken ct)
    {
        var fixtureCode = NormalizeRequired(request.FixtureCode, "fixture_code_required").ToUpperInvariant();
        if (!fixtureCode.StartsWith(FixturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("fixture_code_not_allowed");
        }

        var requirementCode = string.IsNullOrWhiteSpace(request.RequirementCode)
            ? "FX15F_REQ_SATISFIED"
            : NormalizeRequired(request.RequirementCode, "requirement_code_required").ToUpperInvariant();
        var valueCode = string.IsNullOrWhiteSpace(request.ValueCode)
            ? $"{fixtureCode}_VALUE_A"
            : NormalizeRequired(request.ValueCode, "value_code_required").ToUpperInvariant();
        var valueLabel = string.IsNullOrWhiteSpace(request.ValueLabel)
            ? $"{fixtureCode} Evidence Fixture Value A"
            : NormalizeRequired(request.ValueLabel, "value_label_required");

        var setCode = string.IsNullOrWhiteSpace(request.SetCode)
            ? $"{fixtureCode}_BusinessReferenceData_EVID_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
            : NormalizeRequired(request.SetCode, "set_code_required").ToUpperInvariant();
        if (!setCode.StartsWith(fixtureCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("set_code_must_match_fixture_code");
        }

        var set = await EnsureSetAsync(setCode, request.SetName, request.ActorId, request.CorrelationId, ct);
        var version = await EnsureDraftVersionAsync(set, request.ActorId, request.CorrelationId, ct);

        version.RequiresEvidence = true;
        version.RequiresApproval = true;
        version.EvidenceRequirementCode = requirementCode;
        version.EvidenceAttached = true;
        version.ApprovedAt = DateTimeOffset.UtcNow;
        version.EvidenceLinkId = null;
        version.EvidenceEvaluationId = null;
        version.EvidenceDocumentVersionId = null;
        version.EvidenceDecisionCode = null;
        version.EvidenceReasonCode = null;
        version.LastEvidenceRef = null;
        version.BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Draft;
        version.BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.NotStarted;
        version.IsEditable = true;
        version.IsImmutable = false;
        version.SubmittedAt = null;
        version.SubmittedBy = null;
        version.DecisionAt = null;
        version.DecisionBy = null;
        version.WorkflowInstanceId = null;
        version.WorkflowTemplateCode = null;
        version.WorkflowState = null;
        UpsertFixtureValue(version, valueCode, valueLabel);
        version.UpdatedBy = request.ActorId;
        version.LastCorrelationId = request.CorrelationId;

        var expectedToken = version.ConcurrencyToken;
        var updated = await _repository.UpdateVersionAsync(version, expectedToken, ct);
        if (!updated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return Response<BusinessReferenceDataEvidenceFixtureProvisionModel>.Success(new BusinessReferenceDataEvidenceFixtureProvisionModel(
            fixtureCode,
            set.BusinessReferenceDataSetId,
            set.SetCode,
            set.RowVersion,
            version.BusinessReferenceDataVersionId,
            version.ConcurrencyToken,
            requirementCode,
            valueCode,
            version.RequiresEvidence,
            version.RequiresApproval));
    }

    private async Task<BusinessReferenceDataSet> EnsureSetAsync(
        string setCode,
        string? setName,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        var existing = await _repository.GetSetByCodeAsync(setCode, ct);
        if (existing is not null)
        {
            if (existing.Status == BusinessReferenceDataSetStatus.Retired)
            {
                var rowVersion = existing.RowVersion;
                existing.Status = BusinessReferenceDataSetStatus.Active;
                existing.UpdatedBy = actorId;
                existing.LastCorrelationId = correlationId;
                var restored = await _repository.UpdateSetAsync(existing, rowVersion, ct);
                if (!restored)
                {
                    throw new InvalidOperationException("concurrency_conflict");
                }
            }

            return existing;
        }

        var entity = new BusinessReferenceDataSet
        {
            TenantId = Guid.Empty,
            BusinessReferenceDataSetId = Guid.NewGuid(),
            SetCode = setCode,
            Name = string.IsNullOrWhiteSpace(setName) ? setCode : setName.Trim(),
            ScopeType = "tenant",
            Description = "FX15F fixture-owned BusinessReferenceData evidence-required runtime proof set.",
            Status = BusinessReferenceDataSetStatus.Active,
            LastCorrelationId = correlationId,
            CreatedBy = actorId
        };

        await _repository.CreateSetAsync(entity, ct);
        return entity;
    }

    private async Task<BusinessReferenceDataVersion> EnsureDraftVersionAsync(
        BusinessReferenceDataSet set,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        if (set.ActiveDraftVersionId.HasValue)
        {
            var currentDraft = await _repository.GetVersionByIdAsync(set.ActiveDraftVersionId.Value, ct);
            if (currentDraft is not null
                && currentDraft.Status == BusinessReferenceDataVersionStatus.Draft
                && !currentDraft.IsDeleted)
            {
                return currentDraft;
            }
        }

        var nextVersionNo = await _repository.GetNextVersionNumberAsync(set.BusinessReferenceDataSetId, ct);
        var version = new BusinessReferenceDataVersion
        {
            TenantId = set.TenantId,
            BusinessReferenceDataVersionId = Guid.NewGuid(),
            BusinessReferenceDataSetId = set.BusinessReferenceDataSetId,
            VersionNumber = nextVersionNo,
            Status = BusinessReferenceDataVersionStatus.Draft,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsImmutable = false,
            CopyActor = actorId,
            CopiedAt = DateTimeOffset.UtcNow,
            LastCorrelationId = correlationId,
            CreatedBy = actorId,
            BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Draft,
            BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.NotStarted,
            IsEditable = true
        };

        await _repository.CreateVersionAsync(version, ct);
        var previousRowVersion = set.RowVersion;
        set.ActiveDraftVersionId = version.BusinessReferenceDataVersionId;
        set.UpdatedBy = actorId;
        set.LastCorrelationId = correlationId;
        var setUpdated = await _repository.UpdateSetAsync(set, previousRowVersion, ct);
        if (!setUpdated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return version;
    }

    private static void UpsertFixtureValue(BusinessReferenceDataVersion version, string valueCode, string valueLabel)
    {
        var existing = version.Values.FirstOrDefault(v => string.Equals(v.ValueCode, valueCode, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var sortOrder = version.Values.Count == 0 ? 10 : version.Values.Max(v => v.SortOrder) + 10;
            version.Values.Add(new BusinessReferenceDataValue
            {
                ValueCode = valueCode,
                DisplayName = valueLabel,
                Description = "FX15F synthetic evidence-required fixture value.",
                IsDeprecated = false,
                SortOrder = sortOrder,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fixture_code"] = valueCode.Split('_')[0],
                    ["fixture_owned"] = "true",
                    ["fixture_scope"] = "BusinessReferenceData_evidence_runtime_proof"
                }
            });
        }
        else
        {
            existing.DisplayName = valueLabel;
            existing.Description = "FX15F synthetic evidence-required fixture value.";
            existing.IsDeprecated = false;
            existing.Attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            existing.Attributes["fixture_owned"] = "true";
            existing.Attributes["fixture_scope"] = "BusinessReferenceData_evidence_runtime_proof";
        }

        version.DeprecatedValuesEffectiveCount = version.Values.Count(v => v.IsDeprecated);
    }

    private static string NormalizeRequired(string? value, string errorCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorCode);
        }

        return normalized;
    }
}
