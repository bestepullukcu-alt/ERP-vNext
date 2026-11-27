# MOD-0140 Tedarikçi Onboarding — sıradaki adım analizi (2026-11-27)

- **Tür:** tarihli analiz kaydı. Karar, kod ya da commit izni değildir.
- **Kaynaklar:** paket `execution/domains/procurement/module-packs/MOD-0140-supplier-onboarding.md` Revizyon 9.3 ("Teslim dilimleri",
  dilim notları, §18, §20, §21); devir notu `docs/records/audits/2026-11/mvp2-mvp3-ct-handoff-01/MVP-2-DEVIR.md` (2026-11-17);
  `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md`; `execution/approvals/approvals.tsv`.
- **Ölçüm tabanı:** `origin/main` `85e3b1eec` (2026-11-27 `git fetch`). Kod okundu, test koşulmadı.
- **Kısmi inceleme:** MOD-0023 ve Auth kodunda yalnız arama yapıldı; MOD-0147 / MOD-0148 / MOD-0119 / MOD-0289 paketlerinin tüketim
  maddeleri yalnız aranarak okundu.

## 1. Ölçülen durum

| Dilim | Durum (main) | Kanıt |
|---|---|---|
| DL-1 sözleşme 1.3.0 | main'de | devir notu §2 (#289) |
| DL-2 model + göç | main'de | devir notu §2 (#321) |
| DL-3 komutlar, sorgular, BL-501, sözleşme 1.4.0 | **main'de** | PR #459, merge `84052c60e`; `supplier.openapi.yaml:30-31` `version: 1.4.0`, `x-status: FROZEN` |
| Rev 9.3 onayı | main'de | `approvals.tsv:102` (Natig, HIGH, `6cb57a5a…`) |
| DL-3 ertelenen parça (kiracı geneli arama + `read-tenant-wide`) | yok | Auth `ExplicitGrantOnlyPermissions.cs`'te procurement anahtarı yok (yalnız `:42` iş raporu anahtarı) |
| DL-4 … DL-7 | yok | — |

DL-3 "canlı öncesi" kapıları (§18) de açık: yanıt zarfı ↔ sözleşme kararı (§20), Procurement Mongo'sunun her ortamda replika kümesi
kanıtı, `procurement.suppliers.eligibility.read`'in tüketici rollerine verilmesi.

## 2. Kalan dilimler ve ön koşulları

| Dilim | Ne | Ön koşul | Kimde | Bugün |
|---|---|---|---|---|
| DL-3 ertelenen | Kiracı geneli vergi no araması, `read-tenant-wide` | Auth `ExplicitGrantOnlyPermissions` kaydı main'de **ve** o Auth sürümü her ortamda önce dağıtılmış | Auth (Ali) — XMC-001 | kayıt yok |
| **DL-4** | Gerçek MOD-0023 onay akışı (`Approved` / `Rejected` yazan tek yol) + C2: başlık LE'sinin her istekte MDM'de doğrulanması | §20 **S-3**: MOD-0023 onboarding şablonu (`PROC-SUPPLIER-ONBOARDING` önerisi) ve atama kuralı; rollere `mdm.legal-entities.read`; C2 bildirimi MOD-0141…0145 sahiplerine | MOD-0023 sahibi (platform-shared-services; Ali) | **S-3 açık**, şablon kodu depoda hiç geçmiyor (ölçüldü); tarih yok |
| DL-5 | Ekranlar (7 dil): şirketin tedarikçileri, "mevcut tedarikçi bulundu → bağla", ilişki paneli | DL-3 uçları (var); mockup brief; manifest sayfa eylemleri (§20 Revizyon 6 sapması DL-5'e taşındı) | MOD-0140 sahibi | brief yok |
| **DL-6** | Procurement içi beş noktada (PO, fatura, RFx, teklif, sözleşme) `Blocked` / `OnHold` tedarikçiye yeni işlem reddi | DL-3 ilişki durumu (var: `Supplier.cs:9` `OnHold`); XMC-001 onayı 2026-10-23 (MOD-0141/0143/0144/0145) | MOD-0140 sahibi; dosyalar merkezin | **dış bağımlılık yok**; beş dosya main'de mevcut (ölçüldü) |
| DL-7 | `POST /validate` ve tekil uygunluk için servis token'ı, sözleşme 1.5.0 | §20 **S-5** (audience ve `service_name` listesi) + BL-652 Option 2 + Procurement → MDM servis kimliği | Auth sahibi (Ali) | **tarih yok** |

## 3. Tüketicilere bağı

- **MOD-0360-FU01 (DC-03 tedarikçi kullanıcı bağı):** başarılı bağ `onboardingStatus = Approved` ister; bu değeri yalnız **DL-4** yazar.
  Karar: test verisi dışarıdan yazılmaz, başarılı bağ ölçütü DL-4'e kadar açık (`dc03-…-01.md:104-109`). → DL-4 = S-3'e bağlı.
- **MOD-0147 (MVP-6):** uygunluğu kullanıcı token'ı + `eligibility.read` ile okur; üretim kullanımı zarf kararını ve rol atamasını bekler.
- **MOD-0148 (MVP-6, Tedarikçi Portalı):** servis token'ıyla okur → **DL-7**'ye bağlı; DL-7'nin tarihi yok. (Devir notu: MOD-0148 paketinde
  "1.4.0 DL-7" yazıyor, doğrusu 1.5.0.)
- **Treasury MOD-0119 / MOD-0289:** 1.3.0 / 1.4.0 şekline göre sahte istemciyle yazıyor; üretim tüketimi **zarf kararını** ve
  `eligibility.read` rol atamasını bekler. Servis bugün bütün yanıtları `data / statusCode / isSuccessful / errors` zarfıyla dönüyor,
  sözleşme zarfsız şekil yazıyor; okuyuculara iki şekli de okumaları önerildi (devir notu §5).

## 4. Sıradaki dilim ve merkezi ekibin ilk adımı

**Paketteki sıraya göre sıradaki dilim DL-4'tür, ama başlayamaz:** S-3 (MOD-0023 şablonu ve atama kuralı) açık ve tarihsiz.
DL-7 de S-5 / BL-652 nedeniyle tarihsiz. Bu ikisinin sahibi Ali'nin alanındadır (MOD-0023, Auth).

**Önerilen ilk adım (tek öneri): DL-6'nın iş paketi.** Gerekçe: (1) dış sahip beklemez — ilişki durumu DL-3 ile main'de, XMC-001
onayı 2026-10-23'te verildi; (2) küçük ve sınırlı — beş handler'a "Blocked / OnHold tedarikçiye yeni işlem yok" kontrolü, başka
davranış değişmez; (3) üretim riskini azaltır — bugün bloke edilen tedarikçiyle yeni PO / fatura / sözleşme açılabiliyor (ölçüldü:
`Features/PurchaseOrder`, `InvoiceMatch`, `Sourcing`, `Contract` altında `Blocked`, `OnHold` ya da uygunluk denetimi geçmiyor; canlı
denenmedi). Paket DL-6'yı DL-4 / DL-5'e bağlayan bir
ön koşul yazmıyor (§18'de DL-6 kapısı yok; ölçüldü).

**Paralel kağıt işi (kod değil):** MOD-0140 paket revizyonu taslağı — §20 "yanıt zarfı ↔ sözleşme" için tek öneri (karar sahibi
MOD-0140) ve devir notunda verilen söz: MOD-0360-FU01'in danışılan tüketicilere eklenmesi. Bu karar beş tüketicinin üretim kullanımını
açar.

**Ayrıca Ali'ye iletilecek iki bekleyen:** S-3 (DL-4'ün ve MOD-0360-FU01 başarılı bağının tek engeli) ve S-5 / BL-652 (DL-7, MOD-0148).

**Not:** DL-6 ve MOD-0142-FU01 DL-1 aynı serviste (Procurement) ama farklı dosyalardadır; iki dal ayrı PR olur, sırayla main'e girer.

## 5. Açık sahiplik / onay soruları

1. **MOD-0140'ın bugünkü sahibi ve karar sahibi kim?** Paket kararları "Cihan (K4)" diyor; devir notu (2026-11-17) Cihan'ın devrettiğini
   yazıyor, MOD-0142-FU01 paketi MVP-2 sahibi olarak Beste'yi yazıyor. Zarf kararı ve DL-6 / DL-5 iş paketlerinin sahibi kim?
2. **Yüksek riskli MOD-0140 revizyonlarının onaylayıcısı hâlâ Natig (K1, D3) mı, yoksa diğer paketlerdeki gibi Ali mi?**
3. **S-3** (MOD-0023 onboarding şablonu + onaylayıcı rolü) ve **S-5** (servis token'ı audience) için Ali'den tarih — TBD.
4. Devir notundaki "MOD-0140 çift uygulama (Platform.API) — asıl kayıt hangisi" sorusu CT'de açık; bu analizde ölçülmedi.
