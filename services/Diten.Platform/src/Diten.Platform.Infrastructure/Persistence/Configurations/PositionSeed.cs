using Diten.Platform.Infrastructure.Persistence.Schema;
using System;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class PositionSeed
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tenant97c5Id = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var orgCol = database.GetCollection<OrganizationUnit>("organizationUnits");
        var posCol = database.GetCollection<Position>(PlatformCollections.Positions);

        // Seed for DefaultTenantId and Tenant97c5Id
        await SeedForTenantAsync(orgCol, posCol, DefaultTenantId, ct);
        await SeedForTenantAsync(orgCol, posCol, Tenant97c5Id, ct);
    }

    private static async Task SeedForTenantAsync(
        IMongoCollection<OrganizationUnit> orgCol,
        IMongoCollection<Position> posCol,
        Guid tenantId,
        CancellationToken ct)
    {
        var positionCount = await posCol.CountDocumentsAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken: ct);
        if (positionCount == 0)
        {
            // We need an OrganizationUnit first.
            var orgUnit = await orgCol.Find(x => x.TenantId == tenantId && !x.IsDeleted).FirstOrDefaultAsync(ct);
            if (orgUnit == null)
            {
                orgUnit = new OrganizationUnit
                {
                    TenantId = tenantId,
                    Code = "HEADQUARTERS",
                    Name = "Headquarters",
                    LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    IsArchived = false,
                    CreatedBy = "system",
                };
                await orgCol.InsertOneAsync(orgUnit, cancellationToken: ct);
            }

            var mockPositions = new[]
            {
                new Position { TenantId = tenantId, Code = "CEO", Name = "Chief Executive Officer", OrganizationUnitId = orgUnit.Id, CreatedBy = "system" },
                new Position { TenantId = tenantId, Code = "CTO", Name = "Chief Technology Officer", OrganizationUnitId = orgUnit.Id, CreatedBy = "system" },
                new Position { TenantId = tenantId, Code = "HR_MGR", Name = "HR Manager", OrganizationUnitId = orgUnit.Id, CreatedBy = "system" },
                new Position { TenantId = tenantId, Code = "DEV_LEAD", Name = "Development Team Lead", OrganizationUnitId = orgUnit.Id, CreatedBy = "system" },
                new Position { TenantId = tenantId, Code = "DEV_ENG", Name = "Software Engineer", OrganizationUnitId = orgUnit.Id, CreatedBy = "system" }
            };

            await posCol.InsertManyAsync(mockPositions, cancellationToken: ct);
            Console.WriteLine($"Seeded 5 positions for tenant {tenantId}.");
        }
    }
}
