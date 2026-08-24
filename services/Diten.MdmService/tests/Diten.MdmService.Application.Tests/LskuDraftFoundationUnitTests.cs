using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuDraftFoundationUnitTests
{
    [Fact]
    public void Request_and_business_dto_expose_no_server_owned_or_prohibited_fields()
    {
        Assert.Equal(
            ["GskuId", "IdempotencyKey", "MarketCode", "UnmappedFields"],
            typeof(ProductItemSkuMasterModels.CreateLskuDraftRequest)
                .GetProperties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));

        var dtoProperties = typeof(ProductItemSkuMasterModels.LskuDraftDto)
            .GetProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("TenantId", dtoProperties);
        Assert.DoesNotContain("ReferenceTenantId", dtoProperties);
        Assert.DoesNotContain("Credential", dtoProperties);
        Assert.DoesNotContain("PublicationEvidence", dtoProperties);
        Assert.DoesNotContain("LegalEntityId", dtoProperties);
        Assert.DoesNotContain("FinishedGoodId", dtoProperties);
    }

    [Theory]
    [InlineData("TR", true)]
    [InlineData("US", true)]
    [InlineData("tr", false)]
    [InlineData("Tr", false)]
    [InlineData(" TR", false)]
    [InlineData("TR ", false)]
    [InlineData("T", false)]
    [InlineData("TUR", false)]
    [InlineData("1R", false)]
    [InlineData("İR", false)]
    public void Market_code_uses_exact_iso_alpha2_grammar_without_normalization(string value, bool valid)
    {
        var result = new CreateLskuDraftValidator().Validate(new CreateLskuDraftCommand(new()
        {
            GskuId = Guid.NewGuid(),
            MarketCode = value,
            IdempotencyKey = "command"
        }));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("CanonicalCode")]
    [InlineData("CodeReservationId")]
    [InlineData("ReferenceTenantId")]
    [InlineData("CredentialSecret")]
    [InlineData("PublicationEvidence")]
    [InlineData("CatalogVersionId")]
    [InlineData("CatalogVersionNumber")]
    [InlineData("ResolutionMode")]
    [InlineData("ResolvedAtUtc")]
    [InlineData("LegalEntityId")]
    [InlineData("MarketTradeName")]
    [InlineData("FinishedGoodId")]
    [InlineData("MarketSupplyAssignmentId")]
    [InlineData("ArtworkId")]
    [InlineData("PackagingLevelCode")]
    [InlineData("ManufacturerId")]
    [InlineData("SiteId")]
    [InlineData("Gtin")]
    [InlineData("CompositionId")]
    public void Client_authored_evidence_and_prohibited_families_fail_before_handling(string field)
    {
        var result = new CreateLskuDraftValidator().Validate(new CreateLskuDraftCommand(new()
        {
            GskuId = Guid.NewGuid(),
            MarketCode = "TR",
            IdempotencyKey = "command",
            UnmappedFields = new Dictionary<string, JsonElement>
            {
                [field] = JsonDocument.Parse("null").RootElement.Clone()
            }
        }));

        Assert.Contains(result.Errors, error => error.ErrorMessage == "LSKU_FIELD_FORBIDDEN");
    }

    [Fact]
    public void Lsku_identity_schema_and_append_only_enums_are_locked()
    {
        var properties = typeof(Lsku).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("GskuId", properties);
        Assert.Contains("MarketCode", properties);
        Assert.Contains("MarketSelection", properties);
        Assert.DoesNotContain("LegalEntityId", properties);
        Assert.DoesNotContain("FinishedGoodId", properties);
        Assert.DoesNotContain("MarketTradeName", properties);
        Assert.Equal(6, (int)AuditAggregateType.Lsku);
        Assert.Equal(10, (int)ProductAuditOperation.LskuDraftCreated);
        Assert.DoesNotContain(typeof(ILskuRepository).GetMethods(), method =>
            method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Retire", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Only_explicit_persistence_classification_is_ambiguous()
    {
        var commandConflict = new LskuCreateResult(
            false,
            null,
            "LSKU_DUPLICATE_CONFLICT",
            ConflictKind: LskuCreateConflictKind.CommandOrPayload);
        var identityKeyConflict = new LskuCreateResult(
            false,
            null,
            "LSKU_IDENTITY_KEY_CONFLICT",
            ConflictKind: LskuCreateConflictKind.IdentityKey);

        Assert.False(commandConflict.WriteOutcomeAmbiguous);
        Assert.Equal(LskuCreateConflictKind.CommandOrPayload, commandConflict.ConflictKind);
        Assert.False(identityKeyConflict.WriteOutcomeAmbiguous);
        Assert.Equal(LskuCreateConflictKind.IdentityKey, identityKeyConflict.ConflictKind);
        Assert.True(new LskuCreateResult(
            false,
            null,
            "LSKU_WRITE_OUTCOME_AMBIGUOUS",
            WriteOutcomeAmbiguous: true).WriteOutcomeAmbiguous);
    }

    [Fact]
    public void Resolver_contract_exposes_only_market_code_and_cancellation()
    {
        var method = typeof(IVerifiedMarketReferenceResolver)
            .GetMethod(nameof(IVerifiedMarketReferenceResolver.ResolveLatestAsync));
        Assert.NotNull(method);
        Assert.Equal(
            [typeof(string), typeof(CancellationToken)],
            method.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(
            ["CatalogVersionId", "CatalogVersionNumber", "ResolutionMode", "ResolvedAtUtc", "SetCode", "ValueCode"],
            typeof(VerifiedMarketReferenceSelection).GetProperties()
                .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
    }
}
