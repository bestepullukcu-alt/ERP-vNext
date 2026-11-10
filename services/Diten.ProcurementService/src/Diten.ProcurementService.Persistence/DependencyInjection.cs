using Diten.BuildingBlocks.Security.Secrets;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Application.Interfaces;
using Diten.ProcurementService.Domain.Repositories;
using Diten.ProcurementService.Persistence.Repositories;
using Diten.ProcurementService.Persistence.Suppliers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "Procurement.Persistence", [
            new("Mongo:ConnectionString", "Procurement.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // V3 GUID subtype-4 (MOD-0140 §8) — Guid'ler standart (subtype 4) UUID olarak temsil edilir. TryRegister: a second host
        // in the same process (the real-host tests start several) must not fail on an already registered serializer.
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing in appsettings.json.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing in appsettings.json.");

        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(clientSettings);
        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        // MOD-0140 DL-2: okuma anahtarı Suppliers:Model (varsayılan PerLegalEntity; tanınmayan değer açılışı durdurur).
        var supplierModel = SupplierModelSwitch.FromConfiguration(configuration);
        services.AddSingleton(supplierModel);

        // MOD-0140 DL-3 (K-3): the switch SELECTS the supplier store. PerLegalEntity (default, roll-back) → today's per-company
        // `suppliers` through SupplierRepository; TenantShared → supplier_masters + supplier_company_relationships, and the other
        // Procurement modules' code lookups (ISupplierRepository) read the same model through a read-only adapter (XMC-001).
        services.AddScoped<SupplierRepository>();
        switch (supplierModel.Model)
        {
            case SupplierModel.TenantShared:
                services.AddScoped<ISupplierStore, TenantSharedSupplierStore>();
                services.AddScoped<ISupplierRepository, TenantSharedSupplierLookup>();
                break;
            default:
                services.AddScoped<ISupplierRepository>(sp => sp.GetRequiredService<SupplierRepository>());
                services.AddScoped<ISupplierStore>(sp => new PerLegalEntitySupplierStore(sp.GetRequiredService<SupplierRepository>()));
                break;
        }
        services.AddScoped<IRfxRepository, RfxRepository>();
        services.AddScoped<IRequisitionRepository, RequisitionRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IGrnRepository, GrnRepository>();
        services.AddScoped<IInvoiceMatchRepository, InvoiceMatchRepository>();
        services.AddScoped<IContractingRepository, ContractingRepository>();

        // PRODUCT-MASTER (MOD-0290) consume seam — progressive integration (0290 bu dilimde bağlı değil).
        // Varsayılan permissive (hard-fail etmez); gerçek 0290 gateway'i bağlanınca bu kayıt değiştirilir.
        services.AddSingleton<IProductReferenceValidator, PermissiveProductReferenceValidator>();

        // INVENTORY (MOD-0173) posting seam — progressive integration (0173 bu dilimde bağlı değil). Varsayılan
        // MockInventoryPostingClient deterministik transactionId üretir (çalışan 0173 GEREKMEZ); gerçek HTTP client
        // sonraki wiring — bu kayıt değiştirilir, GRN kodu DEĞİŞMEZ (contract sınırı, AD-2/§8). Shadow stock YOK:
        // stok yalnız INVENTORY POST /movements ile artar; GRN yalnız dönen inventoryTransactionId referansını saklar.
        services.AddSingleton<IInventoryPostingClient, MockInventoryPostingClient>();

        // TOLERANCE POLICY (MOD-0143) seam — 3-way match toleransı POLICY-DRIVEN (ASSUMPTION-P2P-01). Varsayılan
        // ZeroToleranceMatchPolicy: eşikler (0) BU SEAM'DE yaşar, match mantığında DEĞİL; herhangi bir sapma →
        // exception (fail-closed). Gerçek Finance/EA tolerans policy servisi bağlanınca yalnız bu kayıt değişir,
        // runThreeWayMatch kodu DEĞİŞMEZ. IProductReferenceValidator / IInventoryPostingClient deseninin aynısı.
        services.AddSingleton<IMatchTolerancePolicy, ZeroToleranceMatchPolicy>();

        services.AddScoped<IModuleSeedDataInitializer, NoOpModuleSeedDataInitializer>();

        // Best-effort index bootstrap (idempotent; Mongo down ise startup'ı çökertmez).
        services.AddHostedService<MongoIndexInitializerHostedService>();

        return services;
    }
}
