# MOD-0188 Demand v2 — PR #228 bağımsız CT kabul kararı (2026-11-13)

- Hedef: PR #228 (`feat(mod-0188): Demand v2 Superseded niyeti ve sayfalama sözleşmesi`, yazar ElikPenco, MVP-4), baş
  `f95fe4d2f50dfefcd5e408a4b1355f00553cc1e9`. Girdi: MVP-4'ün CT kabul ölçüm kaydı (PR yorumu `issuecomment-6820599826`).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0188-demand-planning.md` (`ready-for-dev`; PR paketi değiştirmez; AC
  metinleri :242-251).
- İnceleyen: merkez CT (Beste adına), bağımsız inceleyici alt ajan aea91df60c1a7f5a6 (claude-opus-5-5; kodu yazmadı). Salt okunur:
  `refs/ct/pr228` üzerinde `git show` / `git grep`; test koşulmadı (yerelde RabbitMQ yok).
- Ölçüm: `origin/main` `5b95e90ab`, merge-base `ce4358070` — PR main'in 73 commit gerisinde. CI: `f95fe4d2f` check-runs 39/39 `success`
  (GitHub API, CT 2026-11-13); `service-tests (planning-tests)` koşusu 2026-11-11 15:14-15:15 UTC, iş 130610056368.

## Karar: KOŞULLU — dar kabul

**Kabul edilen dar ölçümler (yalnız bunlar):**
- AC-D19 / AC-D25 atomiklik parçası: Superseded outbox niyeti Published ile aynı Mongo session / transaction'da; Superseded yazısı
  gerçek benzersiz indeksle çakıştırılınca geri alma gerçek Mongo testiyle ölçülür (`PublishedRevisionMongoIntegrationTests.cs:538-574`);
  replay'de ikinci outbox etkisi yok.
- AC-D23: HTTP manifest + bütün satır sayfalarından checksum, üretim yardımcısı çağrılmadan yeniden hesaplanır; ilk miktar bozulunca
  digest farkı (`RevisionHttpChecksumContractTests.cs:14-47`, CANONICAL-1).
- AC-D25 taşıyıcı parçası: taşıyıcı Production'da kapalı (`DemandRevisionEventTransport.cs:206-209`, Development / IntegrationTest dışında
  açılırsa başlangıçta hata); gerçek broker testi kuyruktan okunan `eventId` / `correlationId`'yi Mongo outbox alındısıyla eşler; CI
  RabbitMQ'yu digest ile pinli imajla başlatır, filtresiz koşar, Skipped = 0 ve proof JSON `cat` adımı zorunludur
  (`.github/workflows/service-tests.yml:38`, :268-280).

**Bu PR'dan çıkmayanlar (açık):** AC-D18 (yayın öncesi doğrulamaya yeni kod / test yok); AC-D24 "boş / gerçek sıfır / eksik / bilinmeyen"
ayrımı (yalnız çok sayfalı tutarlılık ölçülür); AC-D25 uçtan uca (broker testi gerçek yayından değil elle eklenmiş sentetik kayıtla,
`Payload="{}"`); AC-D27 (kısmi, değişiklik yok); Demand v2 dondurması; Superseded tarihsel içerik; canlı tüketici.

**Merge koşulları:** (1) main alınır (merge, 73 commit) ve CI yeniden yeşil; (2) planning-tests koşu logu ve `mod0188-rabbitmq-delivery-proof.json`
artefaktı CT'ye gösterilir (bağlantı); (3) aşağıdaki orta bulgular giderilir ya da PR kaydına "açık" olarak yazılır; (4) merge yalnız Ali /
CT onayıyla, merge commit ile.

## Bulgular

- F-1 orta — broker testi env yoksa sessizce geçer: `DemandRevisionRabbitMqIntegrationTests.cs:28-29` `MOD0188_TEST_RABBITMQ_ENABLED != "true"`
  → `return;` (Skip değil; Skipped = 0 kapısı yakalamaz; yalnız CI'daki `cat` adımı yakalar). Öneri: `Assert.Skip` ya da env yoksa fail.
- F-2 orta — broker kanıtı sentetik kayıtla (`:70-81`); gerçek yayının Published + Superseded niyetleri yalnız sahte taşıyıcıyla
  (`DemandRevisionOutboxDeliveryMongoTests.cs:66-103`). AC-D25 uçtan uca iddiası bu PR'dan çıkmaz.
- F-3 orta — rollback testi iddiadan dar: `DemandAuditEntry` Action="Superseded" (`PublishedRevisionMongoStore.cs:206-212`), eski
  `ManualDraftManifest` durumu / StateVersion ve yeni revision'ın `PublishedRevisionManifests` / Parts yokluğu sayılmıyor.
- F-4 orta — `Publish_AuditFailure_RollsBackStateSnapshotSlotAndOutbox` (`:576-620`) main'de zaten var, ilk yayını sınar; Superseded rollback
  kanıtı sayılmaz.
- F-5 düşük — CANONICAL-2 testinde (`RevisionHttpChecksumContractTests.cs:50-92`) bozma negatifi ve çok sayfa / `nextCursor` kontrolü yok.
- F-6 düşük — `contracts/drafts/demand-v2.openapi.yaml` x-implementation-gaps "actual RabbitMQ delivery … remain unproven" der; PR'ın
  AC-D25 iddiası ve `tests/MOD0188-rabbitmq-ci-evidence.md` ile çelişir.
- F-7 düşük — `ClaimForPublishAsync` (`DemandRevisionOutboxStore.cs:339-368`) DeliveryState / NextAttemptAt / DispatchedAt için indekssiz tarar.
- F-8 düşük — geçersiz correlation `HttpOutboxCorrelationReader.cs:88-89` publish transaction'ında 500'e döner.

Kiracı / yetki: yeni HTTP ucu yok; `RevisionReadController.cs` değişmedi, her eylemde `[HasPermission]`; outbox kayıtları TenantId /
LegalEntityId taşır, benzersiz indeks (TenantId, LegalEntityId, EventId); Superseded payload satır taşımaz.

## Ölçülmeyenler

CI koşu logu ve proof JSON içeriği (GitHub oturumu gerekir); testler koşulmadı (CANONICAL eşleşmesi, sabotaj 1-3 sonuçları);
`pack_approval.py check` ve Ali'nin CI dosyası onayı PR'da doğrulanmadı; `scripts/test-env/mod0188-ci-mongo.sh`, `check_trx_no_skipped.py`,
`ci-skip-allowlist.tsv` planning satırı, `EventOutboxPublisherProcessor` okunmadı.

MOD-0189 kodlaması bu karardan bağımsızdır; yeni bağımsız onay ister (MVP-4 beyanıyla aynı).

## Yeniden inceleme — baş 2744d5216 (2026-11-13, aynı gün)

- Hedef: PR #228 baş `2744d521697b52a2c56d739af79973ab03232e95`; `origin/main` `5b95e90ab` ile senkron (geride 0; main birleşmesi
  `be67ffd79`, merge commit; PlanningService altında main birleşmesinden değişiklik yok). Merge sonrası fark yalnız `demand-v2.openapi.yaml` ve
  üç test dosyası; `src/` değişmedi; `.github/workflows`'ta merge sonrası değişiklik yok.
- İnceleyen: bağımsız inceleyici alt ajan a09b02dc0fbfb31c8 (salt okunur, `refs/ct/pr228`); CI ölçümü CT (GitHub API).
- CI (CT ölçtü): `2744d5216` check-runs 39/39 `success`; çalıştırma 43409041189 `success`; iş 131830526628 `service-tests (planning-tests)`
  `success` — adımlar "Start RabbitMQ", "Start owned Mongo (Planning)", "Test" (proof JSON `cat` bu adımın içinde, dosya yoksa adım kırmızı),
  "Skipped = 0", "Upload test results" hepsi `success`; artefakt `test-results-planning-tests` 98.303 bayt, süresi dolmamış. Artefakt
  içeriği ve koşu logu CT tarafından açılmadı (GitHub oturumu gerekir).
- Bulgular: F-1 **kapandı** (env yoksa `throw`, `DemandRevisionRabbitMqIntegrationTests.cs:30-31`); F-2 **kapandı** (gerçek Approve →
  Publish → Publish akışı, gerçek publisher ve RabbitMQ; 3 olay, alındı eşleşmesi; düşük kalıntı N-2); F-3 **kapandı**
  (`PublishedRevisionMongoIntegrationTests.cs:545-608`); F-4 **kapandı** (`Publish_SupersededAuditFailure_RollsBackBothRevisionsAndOutbox`
  `:610-683`); F-5 **kapandı** (`RevisionHttpChecksumContractTests.cs:83-106`); F-6 **kapandı** (`demand-v2.openapi.yaml:76`); F-7 ve F-8
  açık (düşük, `src/` değişmedi). Yeni: N-1 düşük — yerelde Mongo var RabbitMQ yoksa planning takımı kırmızı (CI için doğru; yerel not);
  N-2 düşük — broker testi alındı ile mesaj CorrelationId'sinin birbirine eşitliğine bakar, verilen değere ve Superseded mesajının eski
  revision'a ait olduğuna bakmaz; N-3 bilgi — test `guest`/`guest` (RabbitMQ varsayılanı, yalnız 127.0.0.1), sır değil.
- Kiracı / yetki: yeni HTTP ucu yok; controller değişmedi; yeni test sorguları TenantId + LegalEntityId ile.

**Karar: merge'e hazır DAR KABUL.** Kabul edilen dar kapsam ilk karardakiyle aynı (AC-D19 / AC-D25 atomiklik, AC-D23, AC-D25 taşıyıcı
kısmı) ve F-2 kapandığı için AC-D25'in "gerçek yayın niyetlerinin gerçek broker'a teslimi" CI düzeyinde ölçülmüştür. Açık kalanlar
değişmedi: AC-D18, AC-D24 ayrımı, AC-D27, Demand v2 dondurması, Superseded tarihsel içerik, canlı tüketici; düşük F-7, F-8, N-1, N-2 PR
kaydında "açık" olarak kalır. Merge: Ali (GMG-ERP) onayı ve kararıyla, **merge commit** (squash değil); merge öncesi main yeniden geride
ise alınır ve CI yeniden yeşil olur. MOD-0189 kodlaması bu karardan bağımsızdır, yeni bağımsız paket onayı ister.

## Merge (2026-11-13)

- PR #228 baş `2744d5216` main'e **merge commit** `6f55974a8` ile alındı (Beste); CT `origin/main`'de ölçtü: `2744d5216` main'in atası.
- MVP-4 açık düşükleri sonraki MOD-0188 işine kaydetti: F-7 (claim taraması indeksi), F-8 (üretim `Program.cs` geçersiz correlation'da 400
  döndürür; HTTP test sunucusu bu korumayı kurmaz — 500 iddiası aynı hat üzerinden yeniden ölçülecek), N-1 (yerel RabbitMQ notu), N-2
  (correlation değeri ve Superseded olayının eski revision'a aitliği). Açık AC'ler değişmedi (AC-D18, AC-D24 ayrımı, AC-D27, Demand v2
  dondurması, Superseded tarihsel içerik, canlı tüketici). MOD-0189 kodlaması yeni bağımsız paket onayı bekliyor.

## PR #422 — #228'den kalan düşük bulgular (2026-11-13)

- Hedef: PR #422 baş `72d00448f4f6caf4e901e2ca31693970b8506ade` (ortak taban `6f55974a8`, main'in gerisinde değil); 5 dosya, +87.
  İnceleyen: bağımsız alt ajan ab79a2e160f88b272 (salt okunur, `refs/ct/pr422`); CI CT tarafından GitHub API ile ölçüldü.
- CI: check-runs 39/39 `success`; `service-tests (planning-tests)` `success` 2026-11-13 11:44 UTC — adımlar Start RabbitMQ, Start owned
  Mongo, Test, Skipped = 0, Upload test results hepsi `success`.
- F-7 **kapandı (çekinceli):** `DemandRevisionOutboxStore.cs:13` `ix_mod0188_outbox_undispatched` (tek alan `DispatchedAt`), `:42` Hint,
  `:55-73` tembel kurulum; bileşik indeks `DateTimeOffset` dizi saklaması yüzünden kullanılamadı (`:63-65`, doğru). `DeliveryState` /
  `NextAttemptAt` indekste değil. F-8 **kapandı:** `PlanningCorrelationProductionPipelineTests.cs:24` üretim `Program.cs` ile 400 / 401
  (`Program.cs:19-27` ara katman kimlik doğrulamadan önce). N-1 **kapandı:** `services/Diten.PlanningService/README.md:29-37`. N-2
  **kapandı:** `DemandRevisionRabbitMqIntegrationTests.cs:105`, `:109-113`.
- Yeni (düşük, sonraki iş): Y-1 dead-letter kayıtları `DispatchedAt` null kaldığı için indeks aralığında birikir (kısmi indeks önerisi);
  Y-2 indeksin sorguda kullanıldığını ölçen test yok (kurulum + Hint birlikte silinirse kırmızı yok); Y-3 ilk commit 5503b500e'nin
  `ix_mod0188_outbox_claim` bileşik indeksi kalıcı bir veritabanında kurulduysa kalır (CI'da sorun yok).
- PR açıklaması ve sabotaj kanıtı okunmadı (oturum gerekir).

**Karar: merge'e hazır.** Merge commit ile (squash değil); Y-1…Y-3 sonraki MOD-0188 işinde.

- **Merge:** PR #422 baş `72d00448f` main'e **merge commit** `8773562d4` ile alındı (Beste, 2026-11-13; CT origin/main'de ölçtü). Y-1…Y-3 sonraki MOD-0188 işine.
