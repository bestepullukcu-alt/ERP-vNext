using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Api.ModuleRegistration;
using Diten.ProcurementService.Api.Tests.Suppliers.Contract;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using MediatR;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers.CentralAudit;

/// <summary>
/// MOD-0140 DL-3 (§16 Revizyon 4/6, §17 "Koruma testi", §21) — measured on the PRODUCTION assembly and sources, Inventory's
/// <c>CentralAuditDeclarationTests</c> narrowed to <c>Features.Supplier</c> and the two supplier stores (MOD-0141…0145 are not
/// counted, XMC-001): the marked set is exactly §21's eight commands, the unmarked set exactly {SubmitOnboardingCase} until
/// DL-4, the values are Platform enum names under the manifest's module code, and every <c>WithTransactionAsync(</c>
/// callback of the two stores enlists the central-audit event — a fixed, non-zero count, so an empty scan cannot pass.
/// </summary>
public sealed class SupplierCentralAuditDeclarationTests
{
    private static readonly Type[] WriteCommands = typeof(CreateSupplierCommand).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
                    && (t.Namespace ?? string.Empty).StartsWith("Diten.ProcurementService.Application.Features.Supplier", StringComparison.Ordinal)
                    && t.GetInterfaces().Any(i => i == typeof(IBaseRequest))
                    && !(t.Name.EndsWith("Query", StringComparison.Ordinal) || (t.Namespace ?? string.Empty).Contains(".Queries", StringComparison.Ordinal)))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void The_marked_set_is_exactly_the_eight_DL3_commands_and_only_SubmitOnboardingCase_is_unmarked()
    {
        var marked = WriteCommands.Where(t => typeof(ICentralAuditDeclaration).IsAssignableFrom(t)).Select(t => t.Name).ToArray();
        var unmarked = WriteCommands.Where(t => !typeof(ICentralAuditDeclaration).IsAssignableFrom(t)).Select(t => t.Name).ToArray();

        Assert.Equal(
            [
                "BulkDeleteSupplierCommand", "ChangeRelationshipStatusCommand", "CreateSupplierCommand", "DeleteSupplierCommand",
                "LinkSupplierToCompanyCommand", "SetRelationshipPaymentTermsCommand", "UpdateSupplierCommand", "UpdateSupplierMasterCommand"
            ],
            marked);
        Assert.Equal(["SubmitOnboardingCaseCommand"], unmarked);     // DL-4 marks it; then this set is empty
        Assert.Equal(9, WriteCommands.Length);
        Assert.DoesNotContain(WriteCommands, t => typeof(ICentralAuditOutsideTransaction).IsAssignableFrom(t));
    }

    [Fact]
    public void Category_and_operation_are_platform_enum_names_under_the_manifest_module_code()
    {
        var module = new SupplierOnboardingManifestProvider().GetManifest().ModuleCode;
        var expected = new Dictionary<string, (string Operation, string Entity)>
        {
            ["CreateSupplierCommand"] = ("Create", "Supplier"),
            ["UpdateSupplierCommand"] = ("Update", "SupplierCompanyRelationship"),
            ["UpdateSupplierMasterCommand"] = ("Update", "Supplier"),
            ["LinkSupplierToCompanyCommand"] = ("Assign", "SupplierCompanyRelationship"),
            ["ChangeRelationshipStatusCommand"] = ("LifecycleTransition", "SupplierCompanyRelationship"),
            ["SetRelationshipPaymentTermsCommand"] = ("Update", "SupplierCompanyRelationship"),
            ["DeleteSupplierCommand"] = ("Delete", "SupplierCompanyRelationship"),
            ["BulkDeleteSupplierCommand"] = ("Delete", "SupplierCompanyRelationship")
        };

        foreach (var type in WriteCommands.Where(t => typeof(ICentralAuditDeclaration).IsAssignableFrom(t)))
        {
            var metadata = ((ICentralAuditDeclaration)RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;
            Assert.True(CentralAuditPlatformValues.IsKnown(metadata), $"{type.Name}: {metadata.Category} / {metadata.Operation}");
            Assert.Equal("MasterData", metadata.Category);
            Assert.Equal(module, metadata.SourceModule);
            Assert.Equal(expected[type.Name], (metadata.Operation, metadata.EntityType));
        }
    }

    [Fact]
    public void Every_transaction_of_the_two_supplier_stores_enlists_the_central_audit_event()
    {
        var persistence = Path.Combine(SupplierContractChecks.RepoRoot, "services", "Diten.ProcurementService", "src", "Diten.ProcurementService.Persistence");
        var stores = new[]
        {
            Path.Combine(persistence, "Repositories", "SupplierRepository.cs"),
            Path.Combine(persistence, "Suppliers", "TenantSharedSupplierStore.cs")
        };
        var failures = new List<string>();
        var seen = 0;

        foreach (var file in stores)
        {
            var text = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", string.Empty);
            Assert.DoesNotContain("StartTransaction(", text, StringComparison.Ordinal);
            var starts = Regex.Matches(text, @"WithTransactionAsync\(").Select(m => m.Index).ToList();
            for (var i = 0; i < starts.Count; i++)
            {
                seen++;
                var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
                var window = text[starts[i]..Math.Min(end, starts[i] + 1200)];
                if (!window.Contains("CentralAuditTransaction.EnlistAsync(s", StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetFileName(file)}: a WithTransactionAsync callback does not enlist the central-audit event");
                }
            }
        }

        // One transaction helper per store (every write of the store goes through it). Measured in the DL-3 PR; a store that
        // gains or loses a transaction updates this number in the same commit.
        Assert.Equal(2, seen);
        Assert.Empty(failures);
    }
}
