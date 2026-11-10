using System.Security.Cryptography;
using System.Text;
using Xunit;
using static Diten.ProcurementService.Api.Tests.Suppliers.Contract.SupplierContractChecks;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Contract;

/// <summary>
/// MOD-0140 DL-1 (pack Revizyon 5, §16 / §17): SUPPLIER 1.3.0 is written contract-first, additive only against the frozen
/// 1.2.0 copy, and FROZEN. Each check is measured on the real file and, below, on the sabotages the pack names — a guard
/// that passes those would guard nothing.
/// </summary>
public sealed class SupplierContract130Tests
{
    /// <summary>SHA-256 of <c>supplier.openapi.yaml</c> 1.2.0 as frozen on main before DL-1 (the fixture is that file).</summary>
    private const string Frozen120Sha256 = "373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7";

    private static readonly string ContractText = ReadContract();
    private static readonly string Frozen120Text = Read(Frozen120RelativePath);
    private static readonly string PackText = Read(PackRelativePath);

    [Fact]
    public void The_fixture_is_the_frozen_1_2_0_file()
    {
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Frozen120Text))).ToLowerInvariant();
        Assert.Equal(Frozen120Sha256, sha);
        Assert.Equal("1.2.0", (string?)Parse(Frozen120Text)["info"]!["version"]);
    }

    [Fact]
    public void The_contract_is_1_3_0_and_frozen()
    {
        var info = Parse(ContractText)["info"]!;
        Assert.Equal("1.3.0", (string?)info["version"]);
        Assert.Equal("FROZEN", (string?)info["x-status"]);
        Assert.Equal("FROZEN", (string?)info["x-slice-status"]);
        Assert.Matches("^FROZEN [0-9]{4}-[0-9]{2}-[0-9]{2}$", (string?)info["x-producer-status"]);
        Assert.Equal("MOD-0140", (string?)info["x-owner"]);
        Assert.Equal("3.1.0", (string?)Parse(ContractText)["openapi"]);
    }

    [Fact]
    public void Only_the_closed_list_of_1_2_0_nodes_changed() =>
        Assert.Empty(WhitelistViolations(Parse(Frozen120Text), Parse(ContractText)));

    [Fact]
    public void The_1_2_0_description_is_a_prefix() =>
        Assert.Empty(DescriptionPrefixViolations(Parse(Frozen120Text), Parse(ContractText)));

    [Fact]
    public void Every_1_2_0_response_maps_to_the_same_response() =>
        Assert.Empty(ResponseMappingViolations(Parse(Frozen120Text), Parse(ContractText)));

    [Fact]
    public void Null_is_written_in_2020_12_form_exactly_where_the_pack_allows_it() =>
        Assert.Empty(NullSpellingViolations(Parse(ContractText)));

    [Fact]
    public void Only_the_two_known_1_2_0_example_violations_remain() =>
        Assert.Equal(KnownExampleViolations, ExampleViolations(Parse(ContractText)));

    [Fact]
    public void The_1_2_0_file_has_the_same_two_example_violations() =>
        Assert.Equal(KnownExampleViolations, ExampleViolations(Parse(Frozen120Text)));

    [Fact]
    public void The_s2_s6_surface_is_exactly_the_pack_s() =>
        Assert.Empty(SurfaceViolations(Parse(Frozen120Text), Parse(ContractText)));

    [Fact]
    public void The_contract_text_is_copied_from_the_pack() =>
        Assert.Empty(PackTextViolations(PackText, ContractText, Parse(ContractText)));

    // ------------------------------------------------------------------------------------------- sabotage (pack §17)
    [Fact]
    public void Sabotage_unknown_supplier_description_turns_the_response_mapping_red() =>
        Assert.NotEmpty(ResponseMappingViolations(Parse(Frozen120Text), Parse(Sabotage(
            "      description: Supplier bulunamadı\n", "      description: Supplier ya da legal entity bulunamadı\n"))));

    [Fact]
    public void Sabotage_unknown_supplier_example_turns_the_response_mapping_red() =>
        Assert.NotEmpty(ResponseMappingViolations(Parse(Frozen120Text), Parse(Sabotage(
            "example: { error: { code: UNKNOWN_SUPPLIER, message: \"supplier bulunamadı\", correlationId: req-1 }, contractVersion: v1 } } }\n    DuplicateSupplier:",
            "example: { error: { code: UNKNOWN_LEGAL_ENTITY, message: \"supplier bulunamadı\", correlationId: req-1 }, contractVersion: v1 } } }\n    DuplicateSupplier:"))));

    [Fact]
    public void Sabotage_submit_onboarding_403_as_legal_entity_lookup_turns_the_surface_red() =>
        Assert.NotEmpty(SurfaceViolations(Parse(Frozen120Text), Parse(Sabotage(
            "        '403': { $ref: '#/components/responses/OnboardingWorkflowForbidden' }\n",
            "        '403': { $ref: '#/components/responses/LegalEntityLookupForbidden' }\n"))));

    [Fact]
    public void Sabotage_required_supplier_id_on_error_turns_the_surface_and_whitelist_red()
    {
        var sabotaged = Parse(Sabotage(
            "        error: { type: object, properties: {",
            "        error: { type: object, required: [supplierId], properties: {"));
        Assert.NotEmpty(SurfaceViolations(Parse(Frozen120Text), sabotaged));
        Assert.NotEmpty(WhitelistViolations(Parse(Frozen120Text), sabotaged));
    }

    [Fact]
    public void Sabotage_status_as_allof_nullable_turns_the_null_spelling_red() =>
        Assert.NotEmpty(NullSpellingViolations(Parse(Sabotage(
            "    status: { oneOf: [{ $ref: '#/components/schemas/SupplierStatus' }, { type: \"null\" }], description: \"çağıran şirketin ilişki durumu; ilişki yoksa null\" }\n",
            "    status: { allOf: [{ $ref: '#/components/schemas/SupplierStatus' }], nullable: true, description: \"çağıran şirketin ilişki durumu; ilişki yoksa null\" }\n"))));

    [Fact]
    public void Sabotage_eligible_next_cursor_as_plain_string_turns_the_examples_red() =>
        Assert.NotEqual(KnownExampleViolations, ExampleViolations(Parse(Sabotage(
            "                  nextCursor: { type: [string, \"null\"] }\n",
            "                  nextCursor: { type: string }\n"))));

    [Fact]
    public void Sabotage_rewritten_tenant_sentence_turns_the_prefix_red() =>
        Assert.NotEmpty(DescriptionPrefixViolations(Parse(Frozen120Text), Parse(Sabotage(
            "    Tenant + Legal-Entity scoped; TenantId/LegalEntityId server-resolved (payload'da yok).\n",
            "    Tenant scoped; supplier identity tenant-wide, visibility through the caller's company.\n"))));

    [Fact]
    public void Sabotage_dropping_mod_0147_from_front_loaded_for_turns_the_whitelist_red() =>
        Assert.NotEmpty(WhitelistViolations(Parse(Frozen120Text), Parse(Sabotage(
            "  x-front-loaded-for: [MOD-0147, MOD-0148, MOD-0119, MOD-0289]\n",
            "  x-front-loaded-for: [MOD-0148, MOD-0119, MOD-0289]\n"))));

    [Fact]
    public void Sabotage_changing_a_1_2_0_value_in_the_validate_example_turns_the_whitelist_red() =>
        Assert.NotEmpty(WhitelistViolations(Parse(Frozen120Text), Parse(Sabotage(
            "                  - { supplierId: SUP-2001, known: true, status: Active,",
            "                  - { supplierId: SUP-2001, known: true, status: OnHold,"))));

    [Fact]
    public void Sabotage_a_new_required_list_on_a_1_2_0_schema_turns_the_whitelist_red() =>
        Assert.NotEmpty(WhitelistViolations(Parse(Frozen120Text), Parse(Sabotage(
            "    OnboardingCase:\n      type: object\n",
            "    OnboardingCase:\n      type: object\n      required: [supplierId]\n"))));

    [Fact]
    public void Sabotage_a_relationship_path_in_1_3_0_turns_the_surface_red() =>
        Assert.NotEmpty(SurfaceViolations(Parse(Frozen120Text), Parse(Sabotage(
            "  /{supplierId}/eligibility:\n",
            "  /{supplierId}/relationships:\n    get:\n      operationId: listSupplierRelationships\n      responses: { '200': { description: x } }\n  /{supplierId}/eligibility:\n"))));

    [Fact]
    public void Sabotage_a_drifted_supplier_eligibility_turns_the_pack_check_red()
    {
        var text = Sabotage(
            "    relationshipExists: { type: boolean, description: \"çağıran şirketle silinmemiş ilişki var mı\" }\n",
            "    relationshipExists: { type: [boolean, \"null\"], description: \"çağıran şirketle silinmemiş ilişki var mı\" }\n");
        Assert.NotEmpty(PackTextViolations(PackText, text, Parse(text)));
    }

    private static string Sabotage(string from, string to)
    {
        Assert.Equal(1, Count(ContractText, from));
        return ContractText.Replace(from, to, StringComparison.Ordinal);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
