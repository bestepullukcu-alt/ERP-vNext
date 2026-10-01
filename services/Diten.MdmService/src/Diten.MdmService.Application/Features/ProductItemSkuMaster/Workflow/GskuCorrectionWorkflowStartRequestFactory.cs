using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GskuCorrectionStartConfiguration(Guid? TemplateId, string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds, string ReasonCode, bool CommentRequired,
    bool EvidenceRequired, TimeSpan? DueAfter, Guid? IdentityTemplateId, string? IdentityTemplateCode);
public sealed record GskuCorrectionStartPlan(GskuCorrectionWorkflowOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class GskuCorrectionWorkflowStartRequestFactory(
    GskuCorrectionStartConfiguration configuration, TimeProvider timeProvider)
{
    public const string ObjectType = "GskuCorrection";
    public GskuCorrectionStartPlan Create(Gsku gsku, Guid globalProductId, Guid operationId,
        Guid makerSubjectId, decimal quantity, string uomCode,
        VerifiedGskuReferenceResolveResult references)
    {
        EnsureConfiguration();
        var (applicability, uom) = ResolveSelections(references, uomCode);
        if (gsku.Id == Guid.Empty || gsku.TenantId == Guid.Empty || globalProductId == Guid.Empty
            || gsku.IsDeleted || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || gsku.ActiveLifecycleOperation is not null || gsku.ChildCreationAdmissions.Count != 0
            || gsku.RetirementOperationId.HasValue
            || operationId == Guid.Empty || makerSubjectId == Guid.Empty || quantity <= 0
            || !Exact(uomCode, 16) || applicability is null || uom is null
            || quantity == gsku.PackQuantity && uomCode == gsku.PackUomCode)
            throw new InvalidOperationException("GSKU_CORRECTION_FACTS_INVALID");
        var now = timeProvider.GetUtcNow();
        var dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : (DateTimeOffset?)null;
        var candidates = configuration.CandidatePrincipalIds.Order().ToArray();
        var key = $"gsku-correction:{gsku.TenantId:D}:{operationId:D}";
        var fingerprint = Hash(string.Join('\n', "contract=gsku-correction-v1", gsku.TenantId,
            gsku.Id, gsku.ProductDefinitionRevisionId, globalProductId, gsku.Version, operationId,
            makerSubjectId, quantity, uomCode, Selection(applicability), Selection(uom),
            configuration.TemplateId, configuration.TemplateCode, string.Join(',', candidates),
            configuration.ReasonCode, configuration.CommentRequired, configuration.EvidenceRequired,
            configuration.DueAfter?.TotalSeconds, dueAt?.UtcTicks, ObjectType, gsku.CanonicalCode, key));
        var operation = new GskuCorrectionWorkflowOperation
        {
            Id = operationId, TenantId = gsku.TenantId, OperationId = operationId, GskuId = gsku.Id,
            ProductDefinitionRevisionId = gsku.ProductDefinitionRevisionId, GlobalProductId = globalProductId,
            BaseGskuVersion = gsku.Version, MakerSubjectId = makerSubjectId,
            ProposedPackQuantity = quantity, ProposedPackUomCode = uomCode,
            ProposedPackApplicabilitySelection = applicability, ProposedPackUomSelection = uom,
            WorkflowTemplateId = configuration.TemplateId, WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. candidates], ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired, EvidenceRequired = configuration.EvidenceRequired,
            ConfiguredDueAfterSeconds = configuration.DueAfter.HasValue
                ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            DueAtUtcTicksV1 = dueAt?.UtcTicks, ObjectType = ObjectType,
            ObjectId = operationId.ToString("D"), ObjectRef = gsku.CanonicalCode,
            StartIdempotencyKey = key, OperationFingerprint = fingerprint,
            Checkpoint = GskuCorrectionWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        var request = new ProductIdentityWorkflowStartRequest(configuration.TemplateId,
            configuration.TemplateCode, ObjectType, operation.ObjectId, gsku.CanonicalCode,
            candidates.Select(x => x.ToString("D")).ToArray(), configuration.ReasonCode, key,
            configuration.CommentRequired, configuration.EvidenceRequired, dueAt);
        return new(operation, request);
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(GskuCorrectionWorkflowOperation x) => new(
        x.WorkflowTemplateId, x.WorkflowTemplateCode, x.ObjectType, x.ObjectId, x.ObjectRef,
        x.CandidatePrincipalIds.Select(v => v.ToString("D")).ToArray(), x.ReasonCode,
        x.StartIdempotencyKey, x.CommentRequired, x.EvidenceRequired,
        x.DueAtUtcTicksV1.HasValue ? new(x.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);

    public bool MatchesConfiguration(GskuCorrectionWorkflowOperation x) =>
        x.WorkflowTemplateId == configuration.TemplateId && x.WorkflowTemplateCode == configuration.TemplateCode
        && x.CandidatePrincipalIds.Order().SequenceEqual(configuration.CandidatePrincipalIds.Order())
        && x.ReasonCode == configuration.ReasonCode && x.CommentRequired == configuration.CommentRequired
        && x.EvidenceRequired == configuration.EvidenceRequired;

    public static string ComputeFingerprint(GskuCorrectionWorkflowOperation x) => Hash(string.Join('\n',
        "contract=gsku-correction-v1", x.TenantId, x.GskuId, x.ProductDefinitionRevisionId,
        x.GlobalProductId, x.BaseGskuVersion, x.OperationId, x.MakerSubjectId, x.ProposedPackQuantity,
        x.ProposedPackUomCode, Selection(x.ProposedPackApplicabilitySelection),
        Selection(x.ProposedPackUomSelection), x.WorkflowTemplateId, x.WorkflowTemplateCode,
        string.Join(',', x.CandidatePrincipalIds.Order()), x.ReasonCode, x.CommentRequired,
        x.EvidenceRequired, x.ConfiguredDueAfterSeconds, x.DueAtUtcTicksV1, x.ObjectType,
        x.ObjectRef, x.StartIdempotencyKey));

    public static (ReferenceCatalogSelection? Applicability, ReferenceCatalogSelection? Uom) ResolveSelections(
        VerifiedGskuReferenceResolveResult result, string uomCode) =>
        (ExactSelection(result, "pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            ExactSelection(result, "uom", uomCode));

    private void EnsureConfiguration()
    {
        if ((configuration.TemplateId.HasValue == !string.IsNullOrWhiteSpace(configuration.TemplateCode))
            || configuration.TemplateId == Guid.Empty || configuration.CandidatePrincipalIds.Count is < 1 or > 100
            || configuration.CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || configuration.CandidatePrincipalIds.Distinct().Count() != configuration.CandidatePrincipalIds.Count
            || !Exact(configuration.ReasonCode, 128)
            || configuration.DueAfter is { } due && (due < TimeSpan.FromMinutes(1) || due > TimeSpan.FromDays(30))
            || configuration.TemplateId.HasValue && configuration.IdentityTemplateId == configuration.TemplateId
            || configuration.TemplateCode is not null && string.Equals(configuration.TemplateCode,
                configuration.IdentityTemplateCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GSKU_CORRECTION_CONFIGURATION_INVALID");
    }

    private static ReferenceCatalogSelection? ExactSelection(VerifiedGskuReferenceResolveResult result,
        string setCode, string valueCode)
    {
        if (!result.IsSuccessful || result.Selections.Count != 2) return null;
        var x = result.Selections.SingleOrDefault(v => v.SetCode == setCode && v.ValueCode == valueCode);
        return x is null || x.CatalogVersionId == Guid.Empty || x.CatalogVersionNumber <= 0
            || x.ResolutionMode != "LATEST" || x.IsRetired || !x.SelectableForNew || x.ResolvedAtUtc.Offset != TimeSpan.Zero
            ? null : new() { SetCode = x.SetCode, ValueCode = x.ValueCode, CatalogVersionId = x.CatalogVersionId,
                CatalogVersionNumber = x.CatalogVersionNumber, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = x.ResolvedAtUtc };
    }
    private static string Selection(ReferenceCatalogSelection x) => string.Join(':', x.SetCode, x.ValueCode,
        x.CatalogVersionId, x.CatalogVersionNumber, x.ResolutionMode, x.ResolvedAtUtc.UtcTicks);
    private static bool Exact(string? x, int max) => x is { Length: > 0 } && x.Length <= max
        && x == x.Trim() && !x.Any(char.IsControl);
    private static string Hash(string x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x))).ToLowerInvariant();
}
