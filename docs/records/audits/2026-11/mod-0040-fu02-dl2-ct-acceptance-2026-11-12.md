# MOD-0040-FU02 DL-2 — CT kabulü (ORG / POS kayıt kodları süreç içi porttan)

- Tarih: 2026-11-12. CT: MVP-1 CT (oturum `ca7382cf`). Talimat: WP-0040-FU02-DL2-01. Sahip: Beste.
- Paket: r13 (sha256 `325e0624…`), onaylayan Ali (GMG-ERP) «onayla» 2026-11-11, PR #402 main'de (`c3737ae1c`).
- Hedef: hatta `809aeb504` (46 dosya; ebeveyn `2e5104d83`). Geliştiricinin ölçtüğü commit `7502bd8c0` (detached), hatta cherry-pick;
  46 dosyanın içeriği aynı (geliştirici beyanı).
- Başlama kapısı: `pack_approval.py check` 4 ENGEL; üçü "GitHub'a sorulamadı", dördüncüsü ("inceleme dosyası güvenilmez") üçüncünün
  (`reviews.tsv 568e8905877b`, bu makinede `gh` yok) sonucu — CT kabul ediyor (ortam kaynaklı, CI'da append-only yeşil).
- Sonuç: **KOŞULLU** — kod doğru ve pakete uygun; iki test boşluğu (ORTA-1, ORTA-2) kısa bir test talimatıyla kapanınca VERIFIED. ENGEL yok.
  Canlıya alma ayrıca A-18'e (MDM kalıcı kısa kod kilidi) bağlı.

## Tam takımlar (CT, `809aeb504`, süzgeçsiz; `DITEN_TEST_MONGOD` = mongod 9.0; 27017 ve AccountKindAcceptanceHost.Tests yok)
| Takım | Sonuç |
|---|---|
| Platform.Application | 9188 / 19 — main'in Mongo ortam kırmızıları (AuditOutboxTemporalStorageMigration ×8, BusinessReferenceData VerifiedMarket ×7, VerifiedMarketOperationalConfiguredReferenceTenant, PpmAuditRetentionPolicySeed, PlatformMongoTestLock, PlatformTestMongo) |
| Platform BackgroundJobs | 92 / 1 — `PlatformApiStartupExecutionModeTests.Actual_api_host…` "Build the Platform Release API" (CT ağacında Release derleme yok; ortam) |
| Platform CompositionGuard / Eventing | 12 / 0 · 65 / 0 (3 atlanan, tabanla aynı) |
| Inventory | 2107 / 0 |
| Gateway | 371 / 0 |
| TenantArchitecture | 222 / 5 — `DocsPathGuardTests` ×5 (main tabanı); `AuditTrailStandardTests` yeşil |
| Web | 4517 / 3 — CapacityPlans, SandopPlans `…no_loading_title`, `ReturnDetailsBehaviorTests.DetailsJs_shows_names…` (geliştirici tabanında da kırmızı) |
| vitest | 10 dosyada 13 kırmızı — geliştiricinin tabanıyla aynı liste (dialog / wcn ×6, governance-users ×4, global-products, gsku, platform-administrators) |

AC-61: `PlatformContainerValidationTests` yeşil (F-14); DL-1 kayıt kodu testleri yeşil.

## CT sabotajları (her biri bayt-eşit geri; `git status` temiz)
| # | Sabotaj | Sonuç |
|---|---|---|
| CT-1 | Port: token kiracısı eşitliği kaldırıldı | **kırmızı** — `InProcessRecordCodeAllocatorTests.AC57_a_contradicting_tenant_context…` |
| CT-2 | Çakışma atlaması 3 → 4 deneme | **kırmızı** — `AC55_a_legacy_code_on_the_counter_s_path…` |
| CT-3 | ORG güncelleme: değişmez kod denetimi kaldırıldı | **kırmızı** — `AC53_update_cannot_change…` |
| CT-4 | ORG kod geçmişi (Mongo): silinmişler süzüldü | **kırmızı** — `AC55_…soft_deleted` |
| CT-5 | POS oluşturma: arşivlenmiş birim ikinci okumada kabul | **kırmızı** — `AC51_a_unit_gone_or_archived…(Archived)` |
| CT-8 | POS kod geçmişi (Mongo): silinmişler süzüldü | **YEŞİL** — hiçbir test kırmızı değil (ORTA-1) |

Geliştiricinin S-74…S-95 sabotaj tablosu (her biri kırmızı, bayt-eşit geri) oturum raporunda; CT ayrıca koşmadı.

## Bağımsız inceleme (salt-okunur alt ajan)
MET: AC-48, 49, 50, 51, 52, 53, 56, 57, 58, 59, 60. AC-55 yalnız ORG. Değişmemesi gerekenler (`AllocateRecordCodeHandler`, sözleşme 1.0.0,
`.antigravity`, gateway, `_Layout`, Building.Blocks, `RuleAheadOfCatalogue` CNT/PUT/PCK/OUT/PKG, `NamedException` CTR→AGR) diff'te yok.
46 dosyanın hepsi §5 DL-2 listesinde (XMC-001 liste dışı dosya yok). Q-3 A kararı 7 dil (formlar kiracı kabuğunda) — talimattaki "2 dil"
CT'nin hatası, kod doğru; 14 anahtar × 7 dil gerçek çeviri, `en`-eşit değer yok.

## Bulgular
- **ORTA-1:** POS çakışma araması (`PositionRepository.ExistsByCodeIncludingDeletedAsync`) hiçbir testle ölçülmüyor: CT-8 yeşil kaldı.
  Silinmiş eski bir POS kodu yeniden verilebilir hâle gelse fark edilmez (RCS-001 §5.8). Paket AC-55'i yalnız ORG için yazmış; kural iki tür
  için de geçerli.
- **ORTA-2:** AC-54 kısmi: askıdaki kiracı ve 503 vakalarında "kayıt yok" + ret / merkezi `Failed` satırı ölçülmüyor; (b)(c)(d) yalnız ORG.
- **DÜŞÜK-1:** `InProcessRecordCodeRequest.Attempt = 1` (§3 imzasına ekleme) — `{taban}:{n}` anahtarı için gerekli; paket bir sonraki
  revizyonda §3'ü hizalar.
- **DÜŞÜK-2:** §3'te adı geçmeyen `InProcessRecordCodeCollisionSkip` sınıfı ve katalogdaki `Platform` sabiti — davranışsal değil.
- **DÜŞÜK-3:** HTTP test ana makinesi depo / dar arayüz kayıtlarını kendisi yapıyor; üretim Infrastructure DI satırlarını yalnız kapsayıcı
  doğrulaması ölçüyor.
- **DÜŞÜK-4 (çıkarım):** Düzenleme formu kodu `trim` ile geri gönderiyor; baş / son boşluklu saklı kod (normalleştiriciyi atlayan bir yolla)
  her kayıtta 409 alır. Olasılık düşük.
- **DÜŞÜK-5:** MOD-0288 testlerinde §5'in saydığı satırların ötesinde kod semantiği değişiklikleri (`OrganizationMatrixReportingTests :36-102`,
  `OrganizationEnterpriseFieldsTests :31, :62`, `TenantOrganizationRulesTests` altı istek + altı test yeniden adlandırma) — gerekçeli (dolu
  kod artık 400), XMC onayı kapsamında; CT kabul ediyor.
- **Açık:** AC-55 eski kod sayımı yalnız dev veritabanında (desene uyan 0; DL-6 tetiklenmiyor) — canlı ölçülmedi. AC-60 tarayıcı ölçümü
  yapılmadı (giriş sahipte). Sürüm notu §8.16 (SN-1…SN-7) CT yayımlayacak. Canlıya alma: A-18.

KOŞULLU
