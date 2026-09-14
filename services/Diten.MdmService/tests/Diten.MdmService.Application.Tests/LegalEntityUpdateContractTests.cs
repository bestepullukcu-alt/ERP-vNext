using Diten.MdmService.Application.Features.LegalEntity;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityUpdateContractTests
{
    [Fact]
    public void LegalEntityWriteRequest_ExposesNullableExpectedVersion()
    {
        var property = typeof(LegalEntityWriteRequest).GetProperty("ExpectedVersion");

        Assert.NotNull(property);
        Assert.Equal(typeof(int?), property!.PropertyType);
    }

    [Fact]
    public void LegalEntityRepository_ExposesDedicatedEditableVersionCas()
    {
        var method = typeof(ILegalEntityRepository).GetMethod("UpdateEditableFieldsAsync");

        Assert.NotNull(method);
        Assert.Equal(
            [typeof(LegalEntity), typeof(int), typeof(CancellationToken)],
            method!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
    }

    [Fact]
    public void UpdateHandler_UsesDedicatedCasAndDoesNotUseGenericWholeDocumentUpdate()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs");

        Assert.Contains("UpdateEditableFieldsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_repository.UpdateAsync(", source, StringComparison.Ordinal);
        Assert.Contains("409", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditableCas_UsesExactFieldWhitelistAndNeverDelegatesToWholeDocumentBaseUpdate()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs");
        var methodStart = source.IndexOf("public async Task<bool> UpdateEditableFieldsAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("public async Task<IReadOnlyList<LegalEntity>> GetReferenceableAsync", StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];

        var expectedSetFields = new[]
        {
            "Code", "LegalName", "DisplayName", "LegalFormCode", "OrganizationRoleCode",
            "RegistrationNumber", "TaxId", "VatNumber", "PlaceOfIncorporation", "IncorporationDate",
            "DissolutionDate", "CountryCode", "StatutoryStatus", "ParentLegalEntityId", "OwnershipPercent",
            "ControlTypeCode", "FiscalYearVariant", "AccountingStandardCode", "TaxRegimeCode",
            "BaseCurrencyCode", "RegisteredAddressJson", "CorrespondenceAddressJson", "OfficialEmail",
            "OfficialPhone", "Website", "CompletenessScore", "UpdatedAt"
        };
        var actualSetFields = Regex.Matches(method, @"\.Set\(entity => entity\.(\w+)")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(expectedSetFields, actualSetFields);
        Assert.Contains("Eq(entity => entity.TenantId, TenantId)", method, StringComparison.Ordinal);
        Assert.Contains("Eq(entity => entity.Id, proposed.Id)", method, StringComparison.Ordinal);
        Assert.Contains("Eq(entity => entity.IsDeleted, false)", method, StringComparison.Ordinal);
        Assert.Contains("Eq(entity => entity.Version, expectedVersion)", method, StringComparison.Ordinal);
        Assert.Contains(".Inc(entity => entity.Version, 1)", method, StringComparison.Ordinal);
        Assert.Contains("Collection.UpdateOneAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceOneAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateAsync(proposed", method, StringComparison.Ordinal);

        var forbiddenFields = new[]
        {
            "ApprovalStatus", "ReviewDueUtc", "SourceSystem", "LegacyCode", "EvidenceStatus",
            "OperationalStatus", "IsDeleted", "DeletedAt", "TenantId", "Id", "CreatedAt"
        };
        Assert.Empty(actualSetFields.Intersect(forbiddenFields, StringComparer.Ordinal));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory!.FullName,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
