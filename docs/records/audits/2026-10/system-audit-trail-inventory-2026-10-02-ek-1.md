# Denetim kaydı envanteri — Ek 1 (2026-10-02, CT kararları sonrası)

Bu ek, [system-audit-trail-inventory-2026-10-02.md](system-audit-trail-inventory-2026-10-02.md) belgesini **düzeltmez**
(kayıt düzeltilmez, K4); aynı gün alınan Control Tower kararlarından sonraki durumu kaydeder.

## 1. Alınan kararlar

| # | karar | sonucu |
|---|---|---|
| 1 | PvgService ve ManagementGovernanceService'te adı `Command` ile biten her tür yazma komutudur (yalnız bu iki servis) | PVG'nin 13 komutu borç defterine girdi. MG'nin 10 "MediatR dışı" türü, aynı adlı MediatR komutlarının sözleşme kaydı (`Modules/Dws/DwsContracts.cs`) çıktı; adları zaten borçtaydı, ikinci kez sayılmadı. |
| 2 | Beş İ1 istisnası | Onaylandı; değişiklik yok. |
| 3 | Üç eşdeğer iz + "kiracı tarafında okunabilir" tanımı | Onaylandı; tanım kuralın §5.c-3 maddesine yazıldı. |
| 4 | Yazma hatasında komut dursun mu (K2) | Açık karar olarak kaldı. |
| 5 | Platform işlem içi yol | Aşağıda §3 — **hiçbir komut borçtan çıkarılmadı.** |

## 2. Envanter (testin ürettiği tablo, 27/27 yeşil)

| servis | yazma komutu | a | b | c | istisna | borç | borcun içinde aday izi olan | handler bulunamayan | MediatR isteği olmayan *Command türü |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Diten.AuthService | 26 | 0 | 8 | 0 | 0 | 18 | 10 | 0 | 0 |
| Diten.CrmService | 188 | 0 | 0 | 0 | 0 | 188 | 89 | 0 | 0 |
| Diten.DevEnablementService | 8 | 0 | 0 | 0 | 0 | 8 | 0 | 0 | 0 |
| Diten.EnterpriseStrategyService | 36 | 0 | 0 | 0 | 0 | 36 | 20 | 0 | 0 |
| Diten.HcmService | 8 | 0 | 0 | 0 | 0 | 8 | 4 | 0 | 0 |
| Diten.HumanCapitalService | 70 | 0 | 0 | 0 | 0 | 70 | 0 | 0 | 0 |
| Diten.ManagementGovernanceService | 23 | 0 | 0 | 0 | 0 | 23 | 0 | 12 | 10 |
| Diten.MdmService | 27 | 0 | 6 | 8 | 0 | 13 | 0 | 0 | 0 |
| Diten.Platform | 464 | 280 | 0 | 15 | 5 | 164 | 30 | 0 | 0 |
| Diten.PpmService | 26 | 0 | 0 | 0 | 0 | 26 | 25 | 0 | 0 |
| Diten.ProcurementService | 35 | 0 | 0 | 0 | 0 | 35 | 0 | 0 | 0 |
| Diten.PvgService | 13 | 0 | 0 | 0 | 0 | 13 | 0 | 13 | 13 |
| Diten.TalentEcosystemService | 106 | 0 | 0 | 0 | 0 | 106 | 0 | 0 | 0 |
| **TOPLAM** | 1030 | 280 | 14 | 23 | 5 | 708 | 178 | 25 | 23 |

Ana belgeye göre fark: yazma komutu 1017 → 1030, borç 695 → 708 (ikisi de +13, PVG).

## 3. Platform işlem içi yol — dev veritabanı ölçümü

Salt-okunur, `diten_personalization_dev.audit_outbox`, 2026-10-02:

| anahtar öneki (yol) | durum | satır | `RequestType` |
|---|---|---:|---|
| `audit:` — `AuditBehavior` → `IAuditService` | Completed | 1461 | (tüm denetlenen komutlar) |
| `global-applicability:` — işlem içi | **DeadLetter** | 7 | `RegisterModuleManifestCommand` |
| `physical-entitlement:` — işlem içi | — | **0** | — |
| `tenant-subscription:` — işlem içi | — | **0** | — |

İşlem içi yoldan teslim edilmiş tek satır yok. `ITransactionOwnedAuditCommand` taşıyan 25 komut adından
`audit_outbox` / `audit_events` içinde satırı olanlar:

| komut | satır | yol | en son | işaretin eklenmesinden (d3098d729, 2026-08-31 12:22 UTC) |
|---|---:|---|---|---|
| `AddTenantModuleEntitlementCommand` | 17 teslim | `audit:` (eski yol) | 2026-08-30 21:13 UTC | **önce** |
| `AssignPlanToTenantCommand` | 10 teslim | `audit:` (eski yol) | 2026-07-10 | **önce** |
| `ActivateTenantSubscriptionCommand` | 1 teslim | `audit:` (eski yol) | 2026-07-08 | **önce** |
| `RegisterModuleManifestCommand` | 7 DeadLetter | işlem içi | 2026-09-04 … 2026-09-29 | sonra |
| diğer 21 komut | 0 | — | — | — |

**Sonuç.** Teslim edilen yetkilendirme ve abonelik satırları, bu komutlar `AuditBehavior` tarafından
denetlenirken yazılmış. İşaret eklendikten sonra o komutlardan dev'de hiç satır yok — ne teslim ne DeadLetter.
Yeni yolun çalıştığına dair tek kanıt manifest kaydıdır ve o yedide yedi başarısız. Kod okuması aynı sonucu
öngörüyor: üç çağıranın yükü de `TenantId` taşımıyor, eşleyici onu yükün içinde zorunlu tutuyor.

Bu yüzden **25 komutun hiçbiri borçtan çıkarılmadı.** Kanıt üretmek için: dev'de işaretli bir yetkilendirme
komutu çalıştırılır (ör. bir kiracıya modül eklenir) ve şu sorguyla satırın durumu okunur:

    db.audit_outbox.find({ IdempotencyKey: /^physical-entitlement:/ }, { RequestType: 1, Status: 1, LastError: 1 })

`Status: 3` gelirse defterde `platform-islem-ici` yolu `aday` → `a` yapılır ve test, o ize giden komutların borç
listesinden çıkarılmasını ister. `Status: 5` gelirse F1 doğrulanmıştır ve 2026-08-31'den beri yetkilendirme
değişiklikleri denetlenmiyor demektir.
