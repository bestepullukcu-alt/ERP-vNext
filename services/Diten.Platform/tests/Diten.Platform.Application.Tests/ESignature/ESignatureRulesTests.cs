using Diten.Platform.Domain.Entities.ESignature;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — the pure rules (pack §17 "Unit"): canonical JSON / digest GOLDEN VECTORS (the same values the frozen
/// esign.openapi.yaml carries), PNG byte validator, visual-mode matrix (4 × 3), re-authentication matrix, verdict
/// precedence, effective policy.
/// </summary>
public sealed class ESignatureRulesTests
{
    // ── golden vectors (ESIGN-BUNDLE §7; copied into esign.openapi.yaml x-golden-vectors) ─────────────────────────────

    /// <summary>Fields inserted OUT of ordinal order on purpose: the canonical form must sort them.</summary>
    internal static readonly Dictionary<string, string?> GoldenFields = new()
    {
        ["quantity"] = "1200",
        ["decision"] = "release",
        ["lotCode"] = "PRS-26A014",
        ["note"] = "Line 1\nLine \"2\"\t\\end",
        ["expiry"] = null
    };

    internal const string GoldenCanonicalSubject =
        "{\"module\":\"inventory\",\"subjectType\":\"inventory.quarantine-decision\",\"subjectId\":\"KRN-2026-0412\",\"fields\":{\"decision\":\"release\",\"expiry\":null,\"lotCode\":\"PRS-26A014\",\"note\":\"Line 1\\nLine \\\"2\\\"\\t\\\\end\",\"quantity\":\"1200\"}}";

    internal const string GoldenSubjectHash = "14c30d2865be9998c8504e38fee4ca4a69fcbe8c285f9a3d81a50e27141fd615";
    internal const string GoldenSignatureDigest = "6ebcfb6bcaa8fea17e37c004477d228f533b767761d0f08360e0adaa2b23c9f8";

    internal static SignatureDigestInput GoldenSignature(string subjectHash) => new(
        SignatureNumber: "ESIG-2026-004813",
        TenantId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Module: "inventory",
        SubjectType: "inventory.quarantine-decision",
        SubjectId: "KRN-2026-0412",
        SubjectHash: subjectHash,
        LegalEntityId: Guid.Parse("66666666-7777-8888-9999-000000000000"),
        ActionCode: "inventory.quarantine.release",
        Context: new Dictionary<string, string?> { ["workflowTaskId"] = "t-1", ["decision"] = "approve" },
        MeaningCode: "RELEASE",
        MeaningTextAtSign: "Serbest bırakma yetkisi",
        SignerUserId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        SignerNameAtSign: "Ayşe Demir",
        SignedAtUtc: new DateTime(2026, 10, 7, 9, 14, 22, DateTimeKind.Utc),
        ReauthKind: "password",
        PasswordVerifiedAtUtc: new DateTime(2026, 10, 7, 9, 13, 58, DateTimeKind.Utc),
        AppearanceType: "drawn",
        AppearanceSampleId: null,
        AppearanceImageHash: "0000000000000000000000000000000000000000000000000000000000000000",
        RequestId: null);

    [Fact]
    public void Canonical_subject_matches_the_golden_vector_byte_for_byte()
    {
        var canonical = ESignatureDigest.CanonicalSubject("inventory", "inventory.quarantine-decision", "KRN-2026-0412", GoldenFields);
        Assert.Equal(GoldenCanonicalSubject, canonical);
        Assert.Equal(GoldenSubjectHash, ESignatureDigest.SubjectHash("inventory", "inventory.quarantine-decision", "KRN-2026-0412", GoldenFields));
    }

    [Fact]
    public void Signature_digest_matches_the_golden_vector()
    {
        var input = GoldenSignature(GoldenSubjectHash);
        Assert.Equal(GoldenSignatureDigest, ESignatureDigest.SignatureDigest(input));

        // Every one of the 20 fields is inside the digest: change any one and the digest changes.
        var variants = new[]
        {
            input with { SignatureNumber = "ESIG-2026-004814" }, input with { TenantId = Guid.NewGuid() }, input with { Module = "other" },
            input with { SubjectType = "x" }, input with { SubjectId = "y" }, input with { SubjectHash = "z" }, input with { LegalEntityId = null },
            input with { ActionCode = null }, input with { Context = new Dictionary<string, string?> { ["decision"] = "reject" } },
            input with { MeaningCode = "APPROVE" }, input with { MeaningTextAtSign = "x" }, input with { SignerUserId = "x" },
            input with { SignerNameAtSign = "x" }, input with { SignedAtUtc = input.SignedAtUtc.AddSeconds(1) }, input with { ReauthKind = "window" },
            input with { PasswordVerifiedAtUtc = input.PasswordVerifiedAtUtc.AddSeconds(1) }, input with { AppearanceType = "none" },
            input with { AppearanceSampleId = Guid.NewGuid() }, input with { AppearanceImageHash = null }, input with { RequestId = Guid.NewGuid() }
        };
        Assert.Equal(20, variants.Length);
        Assert.All(variants, v => Assert.NotEqual(GoldenSignatureDigest, ESignatureDigest.SignatureDigest(v)));
    }

    [Fact]
    public void Insertion_order_of_fields_and_context_does_not_change_any_hash()
    {
        var reversed = GoldenFields.Reverse().ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(GoldenSubjectHash, ESignatureDigest.SubjectHash("inventory", "inventory.quarantine-decision", "KRN-2026-0412", reversed));
        var input = GoldenSignature(GoldenSubjectHash) with
        {
            Context = new Dictionary<string, string?> { ["decision"] = "approve", ["workflowTaskId"] = "t-1" }
        };
        Assert.Equal(GoldenSignatureDigest, ESignatureDigest.SignatureDigest(input));
    }

    // ── PNG bytes (V8–V10) ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Png_validator_checks_signature_ihdr_dimensions_and_size()
    {
        Assert.Equal(PngCheck.Ok, ESignaturePngValidator.Check(TestPng.Create(2000, 800)));
        Assert.Equal(PngCheck.NotPng, ESignaturePngValidator.Check(TestPng.Create(2001, 10)));
        Assert.Equal(PngCheck.NotPng, ESignaturePngValidator.Check(TestPng.Create(10, 801)));
        Assert.Equal(PngCheck.Empty, ESignaturePngValidator.Check([]));
        Assert.Equal(PngCheck.NotPng, ESignaturePngValidator.Check([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        var tooLarge = new byte[ESignaturePngValidator.MaxBytes + 1];
        TestPng.Create(10, 10).CopyTo(tooLarge, 0);
        Assert.Equal(PngCheck.TooLarge, ESignaturePngValidator.Check(tooLarge));
    }

    // ── visual mode × appearance (KS-8, 4 × 3) ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ESignatureVisualMode.None, ESignatureAppearanceType.None, true)]
    [InlineData(ESignatureVisualMode.None, ESignatureAppearanceType.Drawn, false)]
    [InlineData(ESignatureVisualMode.None, ESignatureAppearanceType.Sample, false)]
    [InlineData(ESignatureVisualMode.Optional, ESignatureAppearanceType.None, true)]
    [InlineData(ESignatureVisualMode.Optional, ESignatureAppearanceType.Drawn, true)]
    [InlineData(ESignatureVisualMode.Optional, ESignatureAppearanceType.Sample, true)]
    [InlineData(ESignatureVisualMode.SavedAccepted, ESignatureAppearanceType.None, false)]
    [InlineData(ESignatureVisualMode.SavedAccepted, ESignatureAppearanceType.Drawn, true)]
    [InlineData(ESignatureVisualMode.SavedAccepted, ESignatureAppearanceType.Sample, true)]
    [InlineData(ESignatureVisualMode.DrawRequired, ESignatureAppearanceType.None, false)]
    [InlineData(ESignatureVisualMode.DrawRequired, ESignatureAppearanceType.Drawn, true)]
    [InlineData(ESignatureVisualMode.DrawRequired, ESignatureAppearanceType.Sample, false)]
    public void Visual_mode_matrix(ESignatureVisualMode mode, ESignatureAppearanceType appearance, bool allowed) =>
        Assert.Equal(allowed, ESignatureRules.AppearanceAllowed(mode, appearance));

    // ── re-authentication kind × policy (KS-3 / KS-10) ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ESignatureReauthKind.Password, true, 15, 0, false, true)]
    [InlineData(ESignatureReauthKind.Password, true, 15, 120, true, true)]
    [InlineData(ESignatureReauthKind.Window, true, 15, 1, false, false)]   // "every signature"
    [InlineData(ESignatureReauthKind.Window, false, 15, 14, false, true)]  // inside the window
    [InlineData(ESignatureReauthKind.Window, false, 15, 16, false, false)] // stale window
    [InlineData(ESignatureReauthKind.Window, false, 15, 1, true, false)]   // sample / invalidate: password always
    public void Reauth_matrix(ESignatureReauthKind kind, bool every, int windowMinutes, int ageMinutes, bool passwordAlways, bool ok)
    {
        var now = DateTime.UtcNow;
        Assert.Equal(ok, ESignatureRules.ReauthSatisfies(kind, now.AddMinutes(-ageMinutes), now, every, windowMinutes, passwordAlways));
    }

    // ── verdict precedence (ESIGN-BUNDLE §2.6) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verdict_precedence_is_invalidated_tampered_unresolvable_changed_valid()
    {
        Assert.Equal(ESignatureVerificationStatus.Invalidated, ESignatureRules.Verdict(true, false, false, false, false));
        Assert.Equal(ESignatureVerificationStatus.Tampered, ESignatureRules.Verdict(false, false, true, false, false));
        Assert.Equal(ESignatureVerificationStatus.Tampered, ESignatureRules.Verdict(false, true, false, true, true));
        Assert.Equal(ESignatureVerificationStatus.SubjectUnresolvable, ESignatureRules.Verdict(false, true, true, false, false));
        Assert.Equal(ESignatureVerificationStatus.Changed, ESignatureRules.Verdict(false, true, true, true, false));
        Assert.Equal(ESignatureVerificationStatus.Valid, ESignatureRules.Verdict(false, true, true, true, true));
    }

    [Fact]
    public void The_company_override_is_the_effective_policy_when_present()
    {
        var tenant = new ESignaturePolicy { TenantId = Guid.NewGuid(), Scope = ESignaturePolicyScope.Tenant };
        var company = new ESignaturePolicy { TenantId = tenant.TenantId, Scope = ESignaturePolicyScope.LegalEntity, LegalEntityId = Guid.NewGuid() };
        Assert.Same(company, ESignatureRules.Effective(tenant, company));
        Assert.Same(tenant, ESignatureRules.Effective(tenant, null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("short", false)]
    [InlineData("   shortish   ", false)]
    [InlineData("0123456789", true)]
    public void Reasons_need_ten_characters_after_trimming(string? reason, bool ok) => Assert.Equal(ok, ESignatureRules.ReasonOk(reason));
}
