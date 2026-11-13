# MOD-0181 DL-3 — CT kabulü (onay, tolerans, dört göz, COUNT_GAIN / COUNT_LOSS, kurtarma, yeniden sayım, ayarlar)

- Tarih: 2026-11-10. CT: MVP-5 WMS CT (oturum `ca7382cf`). Talimat: WP-0181-DL3-01. Sahip: Beste.
- Hedef: hatta `65af0e638..5ab424fc0` — `2d42a5380` (taslak §4.3 bağlayıcı), `d3728e393` (backend), `65b968b64` (Role Permissions sözcükleri,
  7 dil), `5ab424fc0` (Gateway rota sayısı 60 → 69). Geliştirici raporu (son kısmı): 32 sabotaj kırmızı, bayt-eşit geri; push / PR yok.
- Ölçülen ağaç: `57a68a460` = `5ab424fc0` + CT zinciri (WMS-BUNDLE dondurma, K-2, WP-MOD0178-ANTIGRAVITY-ROWS) çakışmasız yeniden uygulandı.
- Sonuç: **VERIFIED** — ENGEL yok; 2 ORTA sahip kararıyla DL-4'e; DÜŞÜK'ler aşağıda.

## Tam takımlar (CT, süzgeçsiz; `INVENTORY_TEST_MONGO` yok, 27017 kullanılmadı; Platform için `DITEN_TEST_MONGOD`)
| Takım | Sonuç |
|---|---|
| Inventory | **2081 / 0 / 5 atlandı** |
| Gateway | **371 / 0** |
| TenantArchitecture | 222 / 5 — `DocsPathGuardTests` ×5 (main tabanı) |
| Tenancy | 3 / 0 |
| Platform.Application | 9140 / 19 — hepsi Mongo ortam testi (indeks / mongod; aynı 19 DL-3'süz ağaçta da, WP koşusu) |
| Web | 4403 / 2 — `CapacityPlansPageTests`, `SandopPlansPageTests` (bilinen CRLF kırmızıları) |

## Bağımsız inceleme (salt-okunur alt ajan, kod okuması, koşu yok)
AC-21…AC-34, AC-47, AC-48, AC-51, AC-52 (b)(c) DL-3 yarısı, `[HasPermission]` (9 eylem, `cycle-count.*`), hata kodları, §21 olay / merkezi
denetim: **MET** (dosya:satır kanıtıyla). AC-49: **PARTIAL** (ORTA-1). AC-48 gerçek seri stokla — DL-2 notu kapandı.

## CT sabotajları (`CycleCountDecisionRules.cs`, süzgeç `~CycleCount`, her biri bayt-eşit geri)
| # | Sabotaj | Sonuç |
|---|---|---|
| CT-1 | dört göz: gönderen onaylayabilir (`|| approver == count.SubmittedBy` silindi) | **kırmızı** — RulesTests AC22, DecisionTests AC22 (2/161) |
| CT-2 | tolerans: yüzde ayağı silindi | **kırmızı** — RulesTests AC21 satırı, DecisionTests AC21 (2/161) |
| CT-3 | tolerans sınırı `<=` → `<` | **yeşil** (161/161) — DÜŞÜK-1'i ölçümle doğrular |

## Bulgular ve kararlar
- **ORTA-1 → DL-4 (sahip «Tamam», 2026-11-10):** AC-49 "ApiPermissions tam olarak `settings.read` / `.configure`" — bugün manifest 14
  anahtarın hepsini API izni sayıyor (DL-4 ekranlarına kadar zorunlu sıra), test manifest yerine kendi sabitini ölçüyor. DL-4 talimatına:
  manifestin kendisinde eşitlik testi.
- **ORTA-2 → DL-4, paket değişikliği (sahip «Ekle», 2026-11-10):** `ResolveCountAdjustmentIntegrityHandler.cs:107-119` AcceptHolder
  karşılaştırması tür / miktar / bin / lot / durum / SourceLineId — **SKU / kalem yok** (paket lafzına uygun). Senaryo: aynı anahtar, aynı bin ve
  miktarla başka SKU'lu elle `COUNT_GAIN` → IntegrityError → AcceptHolder 200 `Posted`, sayılan SKU'nun stoğu hiç düzelmez. Karar:
  karşılaştırmaya SKU ve kalem eklenir; paket metni revizyonu + kod + test DL-4'te.
- **DÜŞÜK-1:** tolerans sınırı testsiz (CT-3 yeşil); tutucu yön (satır onaya düşer). DL-4 talimatına sınır satırı testi.
- **DÜŞÜK-2:** MaxRecounts "bütün istek" karışık derinlikle test edilmemiş (kod doğru).
- **DÜŞÜK-3:** `Perm.Entity.VARIANCES` en / tr Title Case değil ("Count variances", "Sayım farkları").
- **DÜŞÜK-4:** §19.4 performans testi her koşuda ortak test defterine 120 bin belge yazıyor; sınır (< 30 s) gevşek.
- **DÜŞÜK-5:** `IPostingKeyLookup` yalnız `AddWarehouse` ile kayıtlı (gizli DI bağımlılığı; yorumda yazılı).
- Bilgi: dört göz son kaydedeni (`CountedBy` üzerine yazılır) ölçer — paket lafzı. Süpürücü kiracılar arası tarar, sızıntı yok.

## Modül dışı dokunuşlar (hepsi eklemeli)
1. `ExpiryQuarantineTypeExemptionTests.cs` — `Mod0181StockDoor` (pack §5 dikişi; `CycleCountPostingEngine.cs:113`); 0175 / 0178 satırları aynen.
2. `Mod0178WarehouseRouteTests.cs` 60 → 69 — **sahip teyidi** (Beste «Evet», 2026-11-10, "69 yap"). `ocelot.json` değişmedi.
3. `LedgerImmutabilityArchitectureTests.cs`, `SingleSystemOfRecordArchitectureTests.cs` (`+wms_count_settings`), `CentralAuditDeclarationTests.cs` (109 → 116).
4. Governance Role Permissions: `RoleAssignmentsIndex.{7}.resx` (`ActionVerb_Post`, `ActionVerb_Recount`), `_IndexL10n.cshtml`, `perm-label.js`.
   Commit sahip onayı anıyor ("Köprüye eklemeli yaz") — **teyit bekleniyor**; `wms_count_settings` onayı da teyit bekleniyor.
5. `SharedResource.{7}.resx` — `Perm.Entity.VARIANCES` (1 anahtar, 7 dil, gerçek çeviri).

VERIFIED
