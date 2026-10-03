# Scope / Phase1.5 — DRAFT, approval pending

Bu yeni prospective liste mevcut kaynaklardan çıkarıldı; eski12path listesi kurtarılmış veya doğrulanmış değildir.
11 path:4 mevcut implementation,2 yeni feature-local read dosyası,2 test,3 probe. Enum read-result dosyasında tutulur;
ayrı provenance dosyası gerekmiyor. Probe ayrımı HTTP capture / process restart / evidence verifier sorumluluklarına dayanır.
Yalnız ShipmentRepository IShipmentRepository'yi implemente ediyor; mevcut SourceIntake/command/list callers korunur.

| Exact path | Durum | Gerekçe |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Shipments/IShipmentRepository.cs` | existing | Feature-specific GetDetailAsync; mevcut Get/Query/Mutate imzaları korunur |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Shipments/ShipmentDetailReadResult.cs` | new | Read-only sonuç + raw-state enum aynı dosyada; tarihsel provenance kanıtı değildir |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Shipments/ShipmentRepository.cs` | existing | Mevcut serializer ile render edilmiş scoped tek BSON read |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Shipments/ShipmentDetailMaterializer.cs` | new | Raw presence/value yakalama ve detached read-only materialization |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Shipments/Handlers/QueryHandlers/GetShipmentByIdHandler.cs` | existing | Yeni detail sonucu projection’a taşıma; mevcut404 korunur |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Shipments/ShipmentProjection.cs` | existing | Yalnız Detail explicit UUID/null; Summary/Mutation korunur |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ShipmentRootStorageTests.cs` | new | Raw BSON ayrımı ve projection negatif kontrolleri |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ShipmentRootHttpTests.cs` | new | HTTP/JWT/tenant/LE ve no-write integration testleri |
| `services/Diten.SupplyChainService/tests/root_uptake/runtime_probe.py` | new | Gerçek API bytes ve raw DB evidence |
| `services/Diten.SupplyChainService/tests/root_uptake/restart_probe.py` | new | Aynı veriyle iki gerçek API process restart |
| `services/Diten.SupplyChainService/tests/root_uptake/verify_evidence.py` | new | Manifest, scope ve başarısız ölçümü fail-closed doğrulama |

## Dar tasarım
GetDetailAsync mevcut db/client/sce_shipments üzerinde tek raw document getirir. ShipmentRepository.Scoped tenant+LE+
!IsDeleted filtresi ile mapped Id aynı registered serializer üzerinden render edilir. Yeni client, unscoped fallback,
ikinci root query veya query sonrasında başka sürümden root alma yoktur.
ShipmentDetailReadResult persistent entity değildir. RawRootState=Missing/ExplicitNull/InvalidStoredValue/PresentStoredUuid
ve nullable UUID değerini taşır. PresentStoredUuid historical-authority beyanı değildir. Raw state typed materialization'dan
önce yakalanır. Nil UUID PresentStoredUuid kalır; Guid.Empty sentinel olarak kullanılmaz.
Null/invalid root yalnız detached BSON kopyasında materialization için çıkarılabilir; typed default projection'a root olarak
geçemez. Detached view hiçbir mutation/replacement/receipt/event yoluna verilmez. Uygulama raw BSON'u değiştirmez.
Projection yalnız result state/value üzerinden lifecycleCorrelationId'yi UUID string veya explicit JSON null olarak ekler.
RequestContext correlation root kaynağı değildir. İlgisiz bozuk alanlar için genel tolerant deserializer eklenmez.

## Phase1.5 önerisi
| Kontrol | Delta |
|---|---|
| Entity/base/storage | Shipment, EntityBase, BSON key/type, serializers değişmez; read-only result eklenir |
| Naming | lifecycleCorrelationId yalnız detail wire alanı; CorrelationId storage adı korunur |
| Repository | Specific interface read eklemesi; shared repository veya mutation genişlemesi yok |
| CQRS | Mevcut query/handler; yeni endpoint/command/controller yok |
| Composition | Mevcut DI yeterli; Program.cs/DI yetkisi istenmiyor |
| Contract | Optional nullable UUID compatibility hedefi; upgraded producer daima emit eder; contractVersion v1 korunur |
| Security | Mevcut shipments.read, tenant/LE/soft-delete, header/auth precedence değişmez |
| UI/Golden/lookup | Backend-only delta; UI/form/lookup yok, N/A |
| Evidence | acceptance.md; fresh scoped implementation ve bağımsız verification gerekir |

Protected: prospective-paths.txt dışındaki tüm runtime/test dosyaları; özellikle Shipment/EntityBase, command handlers,
SourceIntake, Program.cs, controller/middleware, DI/serializers, mevcut tests/evidence; canonical/annex, pack, governance,
guard, gateway, diğer modüller, git. Interface implementor/caller inventory dispatch öncesinde tekrar kontrol edilir.
Gerekli ek dosya veya shared değişiklik bulunursa exact scope revision önerilir; sessizce genişletilmez.
