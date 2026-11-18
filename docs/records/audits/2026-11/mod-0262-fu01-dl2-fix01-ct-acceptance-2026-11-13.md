# MOD-0262-FU01 DL-2 FIX-01 — CT kabul kaydı — 2026-11-13

- CT: MVP-1 CT (oturum `ca7382cf`); doğrulama ayrı worktree'de alt ajanla (`agent-af6d2d326e9d0d1a2`, detached `333e91559`).
- Hedef: `333e91559` — test(platform): MOD-0262-FU01 DL-2 FIX-01 — tenant predicate on the real Mongo repository, two tenants. Öncesinde `origin/main` (#419) merge'ü `b76b4a51e`.
- Önceki kayıt: `mod-0262-fu01-dl2-ct-acceptance-2026-11-12.md` (KOŞULLU; ORTA-1: Mongo kiracı yüklemi testi yok, A3 sabotajı yeşil kalıyordu).
- Sonuç: **VERIFIED — ORTA-1 kapandı.**
- Ortam: `DITEN_TEST_MONGOD` = Program Files mongod 9.0; `INVENTORY_TEST_MONGO` tanımsız; 27017 kullanılmadı; AccountKindAcceptanceHost.Tests koşulmadı.

## Diff

- Yalnız `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumerMongoTests.cs` (+87). Üretim kodu değişmedi. `Skip` yok.
- `FIX01_another_tenant_cannot_compensate_or_read_tenant_As_objects_through_the_production_repository`: gerçek HTTP host + gerçek Mongo; ikinci kiracı ayrı `MongoIntegrationHarness`. A iki nesne (biri telafi edilmiş, biri canlı) yükler; B'nin 6 denemesi (compensate / meta / content) 404, compensate `NOT_FOUND_NON_LEAKAGE`. A'nın satırları (`IsDeleted`, `UpdatedAt`, `Checksum`), sağlayıcı ve dosya değişmez; B satır sayısı 0; 6 `trusted-consumer.denied` denetim kaydı; A kendi içeriğini okur.
- `FIX01_the_production_read_that_includes_compensated_rows_is_bound_to_the_ambient_tenant`: üretim `RepositoryObjectRepository`, aynı veritabanında iki `TenantContext`; B bağlamında `GetByContentIdIncludingCompensatedAsync` null, A bağlamında satır döner.

## Testler

| Koşu | Sonuç |
|---|---|
| İki FIX01 testi (filtreli) | 2/2 geçti |
| `Diten.Platform.Application.Tests` (filtresiz, tamamı) | 9304 geçti / 19 kırmızı / 9323 (17 dk 25 sn) |

19 kırmızının hiçbiri DocumentRepository'de değil; hepsi bilinen Mongo ortam kırmızısı ve önceki CT kaydıyla (`fbb36feef`, 9302 / 19) aynı küme, fark yalnız +2 geçen test:
- `AuditOutboxTemporalStorageMigrationMongoTests` (8), `PpmAuditRetentionPolicySeedMongoTests` (1), `BusinessReferenceDataVerifiedMarketOperationalMongoTests` (7), `VerifiedMarketOperationalConfiguredReferenceTenantMongoTests` (1), `PlatformMongoTestLockTests` (1), `PlatformTestMongoTests` (1). Adlar ham çıktıda (scratchpad `full333.trx`).

## A3 sabotajı

- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RepositoryObjectRepository.cs:42` (`GetByContentIdIncludingCompensatedAsync`): `Builders<RepositoryObject>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),` kaldırıldı.
- DocumentRepository filtresi: 173 geçti, **2 kırmızı — tam olarak iki FIX01 testi.** Önceki kayıtta bu sabotaj yeşil kalıyordu.
- Geri alındı (sha256 eşit, `git diff` / `git status` boş); yeniden derleme sonrası iki test 2/2 yeşil.

## Ölçülmeyenler

- TenantArchitecture, Gateway, BackgroundJobs takımları koşulmadı (yalnız Platform test projesi); geliştiricinin TenantArchitecture 224/229 (5 `DocsPathGuardTests`) bildirimi doğrulanmadı.
- `333e91559^` üzerinde ayrı koşu yapılmadı (üretim kodu aynı; kırmızı kümesi önceki CT ölçümüyle aynı).

## Açık kalanlar (bu düzeltmenin kapsamı dışında)

- AC-21 (paket r9) ve AC-25 (FU) — önceki kayıttaki gibi açık.
- Hat: `333e91559` MVP-1 hat ucunda; CT zincirine yeniden merge ve yeni ff etiketi gerekli.
- Push / PR yapılmadı; yalnız sahip onayıyla.
