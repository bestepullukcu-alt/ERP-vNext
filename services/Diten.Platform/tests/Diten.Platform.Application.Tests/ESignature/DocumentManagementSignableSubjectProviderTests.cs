using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;
using Diten.Platform.Application.Features.ESignature.Providers;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — the read-compatible Document Management provider: it reads FU23's OWN projection (no FU23 change), so a
/// MOD-0022 signature of a document-management subject hashes exactly the governance fields FU23 signs — and a change to
/// one of them makes the subject hash move.
/// </summary>
public sealed class DocumentManagementSignableSubjectProviderTests
{
    private readonly Mock<IDocumentLegalHoldRepository> _holds = new();

    [Fact]
    public async Task A_legal_hold_resolves_to_FU23s_fields_and_its_hash_follows_them()
    {
        var hold = new DocumentLegalHold
        {
            TenantId = Guid.NewGuid(),
            HoldKey = "LH-2026-0007",
            HoldTitle = "Audit \"Q3\" hold",
            EffectiveFrom = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)
        };
        _holds.Setup(r => r.GetByIdAsync(hold.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hold);

        var result = await Provider().ResolveAsync("legal-hold", hold.Id.ToString("D"), CancellationToken.None);

        Assert.Equal(SignableSubjectOutcome.Found, result.Outcome);
        var subject = result.Subject!;
        Assert.Null(subject.LegalEntityId); // Document Management applies no LegalEntity filter today (BL-432)
        Assert.Equal("LH-2026-0007", subject.Fields["holdKey"]);
        Assert.Equal("Audit \"Q3\" hold", subject.Fields["holdTitle"]);
        Assert.Equal("2026-09-01T00:00:00.0000000+00:00", subject.Fields["effectiveFrom"]);
        Assert.True(subject.Fields.ContainsKey("effectiveUntil") && subject.Fields["effectiveUntil"] is null);

        var before = ESignatureDigest.SubjectHash("document-management", "legal-hold", hold.Id.ToString("D"), subject.Fields);
        hold.HoldTitle = "Audit Q3 hold (extended)";
        var changed = (await Provider().ResolveAsync("legal-hold", hold.Id.ToString("D"), CancellationToken.None)).Subject!;
        Assert.NotEqual(before, ESignatureDigest.SubjectHash("document-management", "legal-hold", hold.Id.ToString("D"), changed.Fields));
    }

    [Theory]
    [InlineData("legal-hold", "not-a-guid")]
    [InlineData("unknown-type", "00000000-0000-0000-0000-000000000001")]
    [InlineData("other", "00000000-0000-0000-0000-000000000001")]                  // FU23 does not resolve "Other"
    [InlineData("approval-evidence", "00000000-0000-0000-0000-000000000001")]      // needs {subjectId}:{registerEntryId}
    [InlineData("legal-hold", "00000000-0000-0000-0000-000000000001")]             // not in this tenant / not found
    public async Task Anything_FU23_cannot_resolve_is_not_found(string subjectType, string subjectId)
    {
        var result = await Provider().ResolveAsync(subjectType, subjectId, CancellationToken.None);
        Assert.Equal(SignableSubjectOutcome.NotFound, result.Outcome);
    }

    [Theory]
    [InlineData(SignableSubjectType.ApprovalEvidence, "approval-evidence")]
    [InlineData(SignableSubjectType.CAPAAction, "capa-action")]
    [InlineData(SignableSubjectType.GDocPCorrectionRecord, "g-doc-p-correction-record")]
    [InlineData(SignableSubjectType.DocumentMasterRegisterEntry, "document-master-register-entry")]
    public void Wire_types_are_kebab_case_of_the_FU23_enum(SignableSubjectType type, string expected) =>
        Assert.Equal(expected, DocumentManagementSignableSubjectProvider.WireType(type));

    private DocumentManagementSignableSubjectProvider Provider() => new(new DocumentSignableSubjectResolver(
        Mock.Of<IDocumentApprovalEvidenceRepository>(), Mock.Of<IDocumentReleaseGateEvidenceRepository>(),
        Mock.Of<IDocumentTrainingAssignmentRepository>(), Mock.Of<IDocumentGDocPCorrectionRecordRepository>(),
        Mock.Of<IDocumentQualityEventRepository>(), Mock.Of<IDocumentDeviationRepository>(), Mock.Of<IDocumentCAPAActionRepository>(),
        Mock.Of<IDocumentRepositoryAssessmentRepository>(), _holds.Object, Mock.Of<IDocumentDispositionRequestRepository>(),
        Mock.Of<IDocumentTemporaryControlledIssueRepository>(), Mock.Of<IDocumentCopyWithdrawalPlanRepository>(),
        Mock.Of<IExternalDocumentImpactAssessmentRepository>(), Mock.Of<IDocumentMasterRegisterRepository>()));
}
