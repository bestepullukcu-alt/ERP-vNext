using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 (P1a) — the attribute catalog now says WHERE a legitimate value comes from, so the criteria editor can
/// offer a dropdown instead of a text box.
/// <para>Two properties matter and both are tested here. First, <b>anti-drift</b>: every reference set code is the very
/// constant the Account / Contact / link write path validates against, so the editor can never offer a value that path
/// would reject. Second, <b>the metadata is DESCRIPTIVE</b>: it narrows nothing. The predicate shape, the validator and
/// the resolver behave exactly as they did before, and a hand-typed value is still accepted.</para>
/// </summary>
public sealed class SegmentAttributeValueSourceTests
{
    private static SegmentAttributeDefinition Attr(string code) => SegmentAttributeCatalog.Find(code)!;

    [Fact]
    public void Every_attribute_declares_a_value_source_and_populates_only_the_field_its_kind_implies()
    {
        Assert.All(SegmentAttributeCatalog.All, a =>
        {
            var source = a.ValueSource;
            Assert.NotNull(source);
            Assert.Contains(source.Kind, SegmentAttributeValueSource.AllKinds);

            switch (source.Kind)
            {
                case SegmentAttributeValueSource.KindReferenceSet:
                    Assert.False(string.IsNullOrWhiteSpace(source.ReferenceSetCode));
                    Assert.Empty(source.AllowedValues);
                    Assert.Null(source.EntityKind);
                    break;
                case SegmentAttributeValueSource.KindEnum:
                    Assert.NotEmpty(source.AllowedValues);
                    Assert.Null(source.ReferenceSetCode);
                    Assert.Null(source.EntityKind);
                    break;
                case SegmentAttributeValueSource.KindEntityPicker:
                    Assert.False(string.IsNullOrWhiteSpace(source.EntityKind));
                    Assert.Null(source.ReferenceSetCode);
                    Assert.Empty(source.AllowedValues);
                    break;
                default:
                    Assert.Null(source.ReferenceSetCode);
                    Assert.Null(source.EntityKind);
                    Assert.Empty(source.AllowedValues);
                    break;
            }
        });
    }

    [Fact]
    public void Reference_set_codes_are_the_write_path_constants_so_the_editor_cannot_offer_a_rejected_value()
    {
        // Account (MOD-0149)
        Assert.Equal(AccountReferenceValidation.AccountTypeSet, Attr(SegmentAttributeCatalog.AccountType).ValueSource.ReferenceSetCode);
        Assert.Equal(AccountReferenceValidation.AccountCategorySet, Attr(SegmentAttributeCatalog.AccountCategory).ValueSource.ReferenceSetCode);
        Assert.Equal(AccountReferenceValidation.AccountStatusSet, Attr(SegmentAttributeCatalog.AccountStatus).ValueSource.ReferenceSetCode);

        // Contact (MOD-0150)
        Assert.Equal(ContactReferenceValidation.ContactTypeSet, Attr(SegmentAttributeCatalog.ContactType).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactReferenceValidation.ContactStatusSet, Attr(SegmentAttributeCatalog.ContactStatus).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactReferenceValidation.GenderSet, Attr(SegmentAttributeCatalog.ContactGender).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactReferenceValidation.MedicalSpecialtySet, Attr(SegmentAttributeCatalog.ContactSpecialty).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactReferenceValidation.ProfessionalTitleSet, Attr(SegmentAttributeCatalog.ContactProfessionalTitle).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactReferenceValidation.DepartmentTypeSet, Attr(SegmentAttributeCatalog.ContactDepartment).ValueSource.ReferenceSetCode);
        Assert.Equal(ContactWorkbookSchema.PreferredLanguageSet, Attr(SegmentAttributeCatalog.ContactPreferredLanguage).ValueSource.ReferenceSetCode);

        // Location sets are shared between the two masters, so both sides must point at the same set.
        foreach (var pair in new[]
                 {
                     (SegmentAttributeCatalog.AccountCountry, SegmentAttributeCatalog.ContactCountry, ContactReferenceValidation.CountrySet),
                     (SegmentAttributeCatalog.AccountCity, SegmentAttributeCatalog.ContactCity, ContactReferenceValidation.CitySet),
                     (SegmentAttributeCatalog.AccountDistrict, SegmentAttributeCatalog.ContactDistrict, ContactReferenceValidation.DistrictSet)
                 })
        {
            Assert.Equal(pair.Item3, Attr(pair.Item1).ValueSource.ReferenceSetCode);
            Assert.Equal(pair.Item3, Attr(pair.Item2).ValueSource.ReferenceSetCode);
        }

        // The link role, and the linked account type, reuse their own write-path sets. The catalog binds to the
        // link handler's own (internal) constant; the test compares against its PUBLIC twin in the workbook schema,
        // which declares the same set - so a drift on either side still fails here.
        Assert.Equal(ContactWorkbookSchema.ContactRoleSet, Attr(SegmentAttributeCatalog.ContactAccountRole).ValueSource.ReferenceSetCode);
        Assert.Equal(AccountReferenceValidation.AccountTypeSet, Attr(SegmentAttributeCatalog.ContactAccountType).ValueSource.ReferenceSetCode);
    }

    [Fact]
    public void The_consent_verdict_enum_is_the_MOD_0164_vocabulary_verbatim()
    {
        var source = Attr(SegmentAttributeCatalog.ConsentEligibility).ValueSource;

        Assert.Equal(SegmentAttributeValueSource.KindEnum, source.Kind);
        Assert.Equal(
            new[]
            {
                ConsentEligibilityStatus.Allowed,
                ConsentEligibilityStatus.Blocked,
                ConsentEligibilityStatus.Unknown,
                ConsentEligibilityStatus.NotApplicable
            },
            source.AllowedValues);
    }

    [Theory]
    [InlineData(SegmentAttributeCatalog.AccountParentAccount, SegmentAttributeValueSource.EntityAccount)]
    [InlineData(SegmentAttributeCatalog.TerritoryNode, SegmentAttributeValueSource.EntityTerritoryNode)]
    [InlineData(SegmentAttributeCatalog.TerritoryModel, SegmentAttributeValueSource.EntityTerritoryModel)]
    [InlineData(SegmentAttributeCatalog.ConceptAffinity, SegmentAttributeValueSource.EntityGlobalProduct)]
    [InlineData(SegmentAttributeCatalog.ConsentScopeProduct, SegmentAttributeValueSource.EntityMdmProduct)]
    [InlineData(SegmentAttributeCatalog.ConsentScopeBrand, SegmentAttributeValueSource.EntityMdmBrand)]
    public void Id_valued_attributes_name_the_picker_that_already_exists(string attributeCode, string entityKind)
    {
        var source = Attr(attributeCode).ValueSource;
        Assert.Equal(SegmentAttributeValueSource.KindEntityPicker, source.Kind);
        Assert.Equal(entityKind, source.EntityKind);
    }

    [Fact]
    public void Genuinely_open_values_stay_free_text_rather_than_getting_an_invented_set()
    {
        // There is no MOD-0048 set behind a tenant-authored AccountAttributeValue, and pretending otherwise would
        // create a second source of truth (F-TIER). Dates and bools need no list at all: the ValueType says it.
        foreach (var code in new[]
                 {
                     SegmentAttributeCatalog.AccountAttribute,
                     SegmentAttributeCatalog.AccountCreatedAt,
                     SegmentAttributeCatalog.ContactCreatedAt,
                     SegmentAttributeCatalog.ContactIsPrimary,
                     SegmentAttributeCatalog.TerritoryHasCoverage
                 })
        {
            Assert.Equal(SegmentAttributeValueSource.KindFreeText, Attr(code).ValueSource.Kind);
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // The metadata is DESCRIPTIVE: these two tests are what keep P1a from quietly becoming a contract change.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_value_outside_the_declared_reference_set_is_STILL_accepted_by_the_validator()
    {
        // The editor offers published values; the runtime keeps accepting a hand-typed one. If this ever fails, the
        // value source has stopped being a hint and become a restriction the criteria contract never agreed to.
        var criteria = SegmentMapper.ToCriteria(new[]
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "a-code-no-set-has-ever-published" })
        });

        Assert.Null(SegmentValidation.ValidateCriteria(
            SegmentTypes.Dynamic, SegmentSubjectTypes.Contact, criteria));
    }

    [Fact]
    public void The_persisted_predicate_shape_is_unchanged_by_the_new_metadata()
    {
        var node = SegmentMapper.ToCriteria(new[]
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.In, SegmentValueTypes.String,
                new[] { "cardiology", "oncology" })
        }).Single();

        // A multi-select simply fills the SAME Values array - there is no new field, and no wrapper object.
        Assert.Equal(SegmentCriteriaNodeKinds.Predicate, node.NodeKind);
        Assert.Equal(SegmentAttributeCatalog.ContactSpecialty, node.AttributeCode);
        Assert.Equal(SegmentOperators.In, node.Operator);
        Assert.Equal(new[] { "cardiology", "oncology" }, node.Values);
        Assert.Equal(SegmentValueTypes.String, node.ValueType);
        Assert.Empty(node.Parameters);
        Assert.False(node.Negate);

        var properties = typeof(SegmentCriteriaNode).GetProperties()
            .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                "AttributeCode", "GroupOperator", "Label", "Negate", "NodeId", "NodeKind", "Operator",
                "Parameters", "ParentNodeId", "SortOrder", "ValueType", "Values"
            },
            properties);
    }
}
