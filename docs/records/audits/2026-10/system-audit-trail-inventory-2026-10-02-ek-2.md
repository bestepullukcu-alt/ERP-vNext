# Denetim kaydı envanteri — Ek 2 (2026-10-02, gözden geçirme düzeltmeleri sonrası)

P-2026-10-02-03-FIX1. Ana belgeyi ve Ek 1'i **düzeltmez**; ölçüm kuralları sıkılaştıktan sonraki durumu kaydeder.

## 1. Ne değişti

| # | değişiklik | sayıya etkisi |
|---|---|---|
| 1 | Yazıcı izinde kanıt artık bir **çağrıdır** (`Tip.Metot`), tipi anmak değil | Denetlenen hiçbir komut nottan düşmedi. "Aday izi olan" 178 → 148: CRM'de 26 handler yayıncıyı anıyor ama `PublishAsync` çağırmıyor; Platform'da 4 |
| 2 | Adı `Command` ile biten tür `.Queries` ad alanında olsa da komuttur | Bugün böyle bir tür yok (0) |
| 3 | Sorgu işleyicisi depoya yazıyorsa listelenir | Platform'da 3 — hiçbir kayıtları yok |
| 4 | K2 sınıfında denetlenen ama kapalı-başarısız olmayan komutlar listelenir | Auth 8 · Platform 192 |
| 5 | Defterdeki her sayı ve kabul edilmiş her iz testte sabit | — |

## 2. Envanter (testin ürettiği tablo, 35/35 yeşil)

| servis | yazma komutu | a | b | c | istisna | borç | borçta aday izi olan | K2 borcu | yazan sorgu | handler bulunamayan | MediatR dışı |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Diten.AuthService | 26 | 0 | 8 | 0 | 0 | 18 | 10 | 8 | 0 | 0 | 0 |
| Diten.CrmService | 188 | 0 | 0 | 0 | 0 | 188 | 63 | 0 | 0 | 0 | 0 |
| Diten.DevEnablementService | 8 | 0 | 0 | 0 | 0 | 8 | 0 | 0 | 0 | 0 | 0 |
| Diten.EnterpriseStrategyService | 36 | 0 | 0 | 0 | 0 | 36 | 20 | 0 | 0 | 0 | 0 |
| Diten.HcmService | 8 | 0 | 0 | 0 | 0 | 8 | 4 | 0 | 0 | 0 | 0 |
| Diten.HumanCapitalService | 70 | 0 | 0 | 0 | 0 | 70 | 0 | 0 | 0 | 0 | 0 |
| Diten.ManagementGovernanceService | 23 | 0 | 0 | 0 | 0 | 23 | 0 | 0 | 0 | 12 | 0 |
| Diten.MdmService | 27 | 0 | 6 | 8 | 0 | 13 | 0 | 0 | 0 | 0 | 0 |
| Diten.Platform | 464 | 280 | 0 | 15 | 5 | 164 | 26 | 192 | 3 | 0 | 0 |
| Diten.PpmService | 26 | 0 | 0 | 0 | 0 | 26 | 25 | 0 | 0 | 0 | 0 |
| Diten.ProcurementService | 35 | 0 | 0 | 0 | 0 | 35 | 0 | 0 | 0 | 0 | 0 |
| Diten.PvgService | 13 | 0 | 0 | 0 | 0 | 13 | 0 | 0 | 0 | 13 | 13 |
| Diten.TalentEcosystemService | 106 | 0 | 0 | 0 | 0 | 106 | 0 | 0 | 0 | 0 | 0 |
| **TOPLAM** | 1030 | 280 | 14 | 23 | 5 | 708 | 148 | 200 | 3 | 25 | 13 |

Ek 1'e göre: komut · a · b · c · istisna · borç **aynı**. Aday 178 → 148. K2 borcu 0 → 200 (yeni bölüm). Yazan sorgu 0 → 3 (yeni bölüm).

## 3. Yazan sorgular (Platform)

| sorgu | yazdığı |
|---|---|
| `GetTenantAdminUsersQuery` | ilk yönetici kullanıcı kaydını kiracıya ekleyip kiracıyı günceller (`EnsureInitialAdminUser` + `_repository.UpdateAsync`, `GetTenantAdminUsersQueryHandler.cs:24-28`) |
| `GetTenantLoginSettingsQuery` | yoksa varsayılan giriş ayarlarını oluşturur (`_settingsRepository.CreateAsync`, `GetTenantLoginSettingsQueryHandler.cs:38`) |
| `GetTenantUsersSummaryQuery` | aynı ilk yönetici yazımı (`GetTenantUsersSummaryQueryHandler.cs:28-32`) |

Üçü de kiracı / kullanıcı verisine dokunuyor; `AuditBehavior` sorguyu denetlemediği için hiçbirinin kaydı yok.
Algılayıcı dardır (`…Repository / …Store / …Collection` adlı alana yazma çağrısı); bu üçünün dışında eşleşme olmadı —
yani ölçülen yanlış alarm 0, ama servis üzerinden yazan bir sorgu görülmez.

## 4. K2 borcu nasıl çıkarıldı

- **Auth (8):** yol `b` ile denetlenen sekiz kullanıcı yaşam döngüsü komutu.
- **Platform (192):** özellik klasörüne göre — `PlatformAdministrators`, `PlatformAccount` (kimlik) · `Tenants` (kiracı durumu) ·
  `DocumentManagement*`, `DocumentRepository` (GxP). Komut tek tek okunmadı. Organizasyon, abonelik özellikleri, kotalar,
  referans veri, zaman kaydı, görevler, iş akışı, bildirimler sınıfa **alınmadı**; sınır Control Tower'ın (kural §10 K10).

Borçtaki (denetlenmeyen) K2 sınıfı komutlar bu listede değildir; onlar zaten `## Bilinen borç`'tadır
(ör. Auth rol / izin komutları, `SuspendTenantCommand`).
