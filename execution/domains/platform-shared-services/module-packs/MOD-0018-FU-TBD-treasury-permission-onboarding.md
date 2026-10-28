---
id: MOD-0018-FU-TBD
name: Treasury Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
owner: auth-owner (MOD-0018 sahibi inceler ve teyit eder) / yazar Treasury Intake CT
branch: TBD (FU numarası kararından sonra `feature/pss/mod-0018-fu<NN>-treasury-permission-onboarding`)
started: 2026-10-28
target: TBD
form_field_count: 0
parent: MOD-0018
consumer_module: MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289, MOD-0217
---

# MOD-0018-FU-TBD — Treasury Permission Onboarding

> **Taslak — kod başlatma izni değildir.** Bu paket izin kataloğu, rol, hak (grant), rol üyeliği, hak eşitlemesi
> (entitlement), token, manifest, gezinme, yapılandırma, gizli bilgi ya da kiracı verisi oluşturmaz. Kod yalnız paket
> `approved` / `ready-for-dev` olduktan ve CT-APR-01 onay satırı main'e girdikten sonra, Bölüm 5'teki tam yol listesiyle ve
> dilim sırasıyla yazılır. Platform ve MDM yollarına dokunmak ayrıca XMC-001 onayı ister (Bölüm 5, D63/2).
>
> **Yetki dayanağı.** MOD-0018 Auth eki Treasury tarafından yazılır; MOD-0018 sahibi inceler ve teyit eder (Y-15, Y-16;
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md`). Y-14 geçersizdir ve bu pakete
> işlenmedi. Modül tanımı biçimi değişmedi; bu paket mevcut MOD-0018 onboarding kalıbını (FU20 / FU22) izler (Y-16/2).
> **D53** aynen geçerlidir: bu paketin metni main'de = altı tüketici paketin paket kapısı (D53/1); bu paketin kodu main'de =
> tüketicilerin DL-1 kabul kapısı (D53/2). **D56** aynen geçerlidir.
>
> **Kaynak (tek doğruluk kaynağı, D62/1).** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` — main'de
> (`origin/main` @ `f81d34df7`, 2026-10-28 ölçümü): "D47 rol matrisi (CT04a; D49 ile güncellendi — işleyen tek matris)"
> bölümü, D54, D56, D57, D62, D63. **D64** (D63/3'ün yerine; waive-sod Auth veri yapısı bu pakette, DL-5) karar dosyasına
> yazıldı (tetik Y-17) ama 2026-10-28 ölçümünde **main'de değildir** (yalnız `chore/ct/treasury-intake-ct-05` dalında, yerel
> commit `4d8a4050a`); D62/1
> gereği DL-5'in paket kapısı D64'ün main'e girmesini bekler (Ready-for-dev Checklist). D46 tablosu tarihçedir
> ("YERİNE GEÇTİ — UYGULAMAYIN") ve kullanılmadı. Anahtar adları altı paketin Authorization Convention bölümleriyle
> mekanik olarak karşılaştırıldı (Bölüm 19).
>
> **Revizyon (2026-10-28, KARAR D69 — sözleşme sürümü onay anahtarı):** işleyen matrise yeni anahtar `mdm.contracts.versions.approve` ve yeni rol
> "Sözleşme onaylayıcı" (`mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve`; belge deposu izni yok — D56/2; açık atama
> değil) eklendi: toplam **117 anahtar, 26 rol**; DL-3 **18 anahtar, 4 rol, 1 açık atama** (rol 26 `ContractVersionApprover`, ad
> önerisi K-7). Yeni anahtarın eylemi `approve`'dur, `read` değil: Viewer dışlama listesi (18 `read` anahtarı) değişmez. Bölüm 8.6
> tüketici anahtarı satırında MOD-0217 "açık" → `mdm.contracts.versions.approve` (D69; MOD-0217 O-3 (b) kapandı). Değişen yerler: Bölüm 1, 2, 3, 4, 5.1, 7,
> 8.1, 8.2, 8.6, 11, 12, 13, 14.2, 16 (DL-3 başlığı; **AC-7, AC-18, AC-19 genişletildi**), 17, 18, 19 (mekanik uzlaştırma yeniden
> koşuldu), Teslim dilimleri DL-3 satırı, K-2. Rolün bağımlı MOD-0023 görev anahtarlarını (`platform.workflow.tasks.approve` /
> `.reject`) taşıyıp taşımayacağını D69 yazmadı — K-4'e eklendi (açık; K-A4 ona bağlı). D69 2026-10-28 ölçümünde **main'de değil**
> (çalışma ağacında, commit edilmemiş); D62/1 gereği DL-3'ün paket kapısı D69'un main'e girmesini bekler (Ready-for-dev
> Checklist). MOD-0018 sahibinin teyidi Auth ekiyle birlikte (D69). Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, D66/1 — onaylayıcı aday sorgusu):** MOD-0023 şablonları atamasız olduğundan (Y-12) onaylayıcı aday
> tabanı Auth'tan gelir: "bu izne sahip kullanıcılar" sorgusu bu pakete **yeni dilim DL-6** olarak eklendi (kapsam g; Bölüm 1,
> 2, 3, 5.4, 7, 8.6, 10, 12, 13, 14.4, 15, 17, 18, 21, Teslim dilimleri, Açık kararlar K-A1…K-A4; **yeni AC-35…AC-41**). D56/3'ün muafiyet aday listesi ayrı
> tanım olarak kalmaz: DL-6 sorgusunun `waive-sod` + parti biçimidir (Bölüm 8.5, 8.6 — tek aday sorgusu). **Neden DL-1 değil:**
> DL-1 kodu tüketicilerin DL-1 kabul kapısıdır (D53/2) ve iç uç servis kimliği kararını (K-A1 / K-W3) ister; DL-6 tüketicilerin
> onay dilimlerinden (MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3) önce main'de olmalıdır,
> DL-1'i bekletmez. Kaynak: karar dosyası D66 (2026-10-28 ölçümünde **main'de değil** — yalnız `chore/ct/treasury-intake-ct-05`
> dalında); D62/1 gereği DL-6'nın paket kapısı D66'nın main'e girmesini bekler. D66/1 MOD-0018 sahibinin teyidini ister
> (Ready-for-dev Checklist). Açık kalanlar K-A1…K-A4. Mevcut AC numaraları değişmedi.
>
> **DCP-002 kimlik durumu — FU numarası açık karar (K-1).** Ana kayıtta (`execution/registries/module-id-registry.md`) en
> büyük kayıtlı numara MOD-0018-FU22'dir; ancak FU23–FU26 paket dosyaları arşiv dallarında vardır
> (`origin/archive/pc2/feature/pss/mod-0018-fu24-fu26-governance-reconciliation` ağacında `MOD-0018-FU23…FU26-*.md`) ve
> FU23–FU25 kodda anılır (`SeedOwnedTenantScopeKeys.cs:7`, `InternalPermissionsController.cs:150`). Doğrulayıcı arşivi
> görmez: `verify_module_id.py . --check-id MOD-0018-FU23 --name "Treasury Permission Onboarding" --parent MOD-0018` → `OK`
> (yanlış olumlu). Aday `MOD-0018-FU27` için aynı komut `OK MOD-0018-FU27: proven against Blueprint/registry` (çıkış 0).
> `--discover "Treasury permission onboarding"` çıkış 3 ("inceleme gerekli"; Blueprint 0, registry 6, paket 8 bulgu;
> rapor-sha256 `959839624f40c2bfff71597652b244318421e166c958276b03e2f29dfcd6c045`). Auth kulesi numarayı kendisi vereceğini
> yazmıştı (Y-6, Y-10/1). Bu yüzden dosya adı ve `id` `FU-TBD`'dir; `FU-TBD` doğrulayıcıdan geçmez (beklenen, kapalı-başarısız).
> Numara verilince dosya adı, `id`, `branch` ve kayıt satırı birlikte yazılır.
>
> **Golden Reference kararı.** Arka uç yetki / katalog / onboarding işi. Razor ya da DataTable yüzeyi yoktur:
> `shell: none`, `golden_reference: none`, `form_field_count: 0` bilinçlidir. DL-5 Auth'ta kiracıya ait yeni bir kayıt türü
> açtığı için `entity_base: EntityBase` (Auth için somut taban sınıf).

## 1. Module Summary

Bu FU, Treasury'nin altı paketinin (MOD-0126 Ödeme ve onaylar, MOD-0119 Borç / AP, MOD-0120 Alacak / AR, MOD-0124 Nakit
pozisyonu, MOD-0289 Banka ve banka hesabı kaydı, MOD-0217 Sözleşme yaşam döngüsü) izinlerini Auth'a katar. Kapsam MOD-0018
sahibinin a–f listesidir (Y-10/1), D64 ile eklenen muafiyet ataması ve D66/1 ile eklenen onaylayıcı aday sorgusudur:

| | Kapsam | Dilim |
|---|---|---|
| a | 26 rol şablonu (D47 rol matrisi, D49 ve D69 ile güncel) — ad ve anahtar ataması matristen birebir | DL-1, DL-2, DL-3 |
| b | 117 modül izin anahtarı (D57/4 ve D69 sonrası) — sayıldı, matrisle uzlaştırıldı (Bölüm 19) | DL-1, DL-2, DL-3 |
| c | 8 "yalnız açık atama" (`ExplicitGrantOnlyPermissions`) anahtarı, `waive-sod` dahil | DL-1, DL-2, DL-3 |
| d | `waive-sod` atamasının parti bağı (D54) — **D64 ile bu paketin kapsamında**, ayrı dilim; muafiyet aday listesi (D56/3) D66/1 ile DL-6 aday sorgusunun `waive-sod` + parti biçimidir (parti bağını bu dilimin kaydı verir) | DL-5 |
| e | SEC-002 eki (D56/1): Platform izin listesi + MDM istemcisinin modül kodu | DL-4 |
| f | İki yeni ayar anahtarı `finance-treasury.payment-sod-settings.read` / `.update` (D57/4) | DL-1 |
| g | Onaylayıcı aday sorgusu — "bu izne sahip kullanıcılar" (iç uç, servis kimliğiyle; kiracı, yalnız etkin kullanıcı, açık atama anahtarında yalnız açık ataması olanlar; şirket kapsamı K-A2) — **D66/1** (D56/3 yönünde) | DL-6 |

Sözleşme rollerine belge deposu izni verilmez (D56/2). `mdm.contracts.*` kaydı MOD-0217 onayından sonra yapılır (Y-6,
Y-10/1) — bu yüzden ayrı dilim (DL-3) ve koşullu.

Anahtarlar mevcut yolla kaydolur (Y-16/4): tüketici servisin modül manifesti → Platform genel kataloğu → Auth eşitlemesi
(BL-592 deseni, `docs/guides/operations/post-deploy-steps.md:317-320`). Bu FU anahtarı **beyan etmez** (beyan tüketici
paketinin manifest adımıdır); Auth tarafında (1) açık atama listesini, (2) varsayılan rol tabanından dışlamayı, (3) modül
hakkıyla kurulan özel hak profilini ve 26 ayrılmış sistem rolünü, (4) muafiyet atama kaydını, (5) onaylayıcı aday sorgusunu
sahiplenir.

Neden özel profil gerekir (kod gerçeği, ölçüldü): genel eşitleme bir modül hakkıyla Admin'e modülün **bütün** anahtarlarını,
Viewer'a bütün `read` eylemli anahtarlarını verir (`EntitlementPermissionSyncService.cs:572-580`). Ayrıca varsayılan Viewer
tabanı, modül hakkı olmayan kiracılarda dahil, `read` eylemli her kiracı anahtarını verir
(`DefaultRolePermissionTemplate.cs:207-213`; dışlama yalnız `EntitlementOnlyViewerPermissions`, `:120-168`). Treasury'nin
117 anahtarından 18'i `read` eylemlidir (Bölüm 4). Genel davranış Treasury'de görev ayrılığını (SoD) çökertir — Admin aynı
partiyi hazırlayıp onaylama izinlerinin ikisini birden alır — ve finans / banka verisini her Viewer'a açar.

## 2. Ownership and Boundaries

**Bu FU / Auth sahibi (MOD-0018; yazar Treasury CT, inceleme ve teyit sahibin):**

- 8 anahtarın `ExplicitGrantOnlyPermissions` listesine girmesi.
- 18 `read` eylemli anahtarın (ve modül hakkı olmadan verilmemesi gereken diğerlerinin) varsayılan rol tabanından dışlanması.
- Treasury modül kodları için tam küme doğrulamalı, saf (persist etmeyen) hak profili ve 26 ayrılmış sistem rol şablonu.
- Kiracı + kaynak modül sınırlı, idempotent hak verme ve geri alma; kullanıcıya otomatik rol üyeliği yok.
- DL-5: `waive-sod` parti bağlı muafiyet atama kaydı, yazma ucu, "bu parti için geçerli atamalar" sorgu ucu, K2 denetim kaydı.
- DL-6 (D66/1): "bu izne sahip kullanıcılar" aday sorgusu — salt okuma iç uç; kiracı sınırlı, yalnız etkin kullanıcı, açık
  atama anahtarında yalnız açık atama; `waive-sod` için parti biçimi (D56/3 ile birleşik). Auth SoD kuralı uygulamaz.

**Tüketici paket sahipleri (beyan ve çalışma anı):**

- Modül manifesti ve anahtar beyanı: dört finans paketi için tek yazar MOD-0126 DL-1 (servis ortak yapı taşı; MOD-0119
  `:557`, MOD-0124 `:643`); MOD-0289 ve MOD-0217 için MDM manifest sağlayıcıları (MOD-0217 `ContractLifecycleManifestProvider.cs`).
- `[HasPermission]` zorlaması ve çalışma anı SoD kuralları (MOD-0126 R1–R3, R9; MOD-0119 R1–R3; MOD-0120 A1, A2, A3, A7;
  MOD-0124 R1–R3; MOD-0289 D23/2; MOD-0217 D47/5). Rol şablonu SoD değildir; bir kişiye birden çok rol verilebilir.
- Muafiyetin kullanım anı denetimi MOD-0126'dadır (AC-42, AC-43; 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`). Bu FU MOD-0126 paketini
  değiştirmez; DL-5 onun okuduğu alanları ve sorguyu sağlar.
- Aday sorgusunun sonucuna paketin kendi kurallarını uygulamak (MOD-0126 R1–R4, MOD-0119 R1–R3, MOD-0120 / MOD-0124 SoD
  dışlamaları, MOD-0217 hazırlayan hariç) ve kalanı MOD-0023'e `StartWorkflowInstanceRequest.CandidatePrincipalIds` olarak
  göndermek tüketicinindir (D66/1; şablonlar atamasız — Y-12); ayrı dizin istemcisi taşınmaz.

**Platform sahibi (Beste) — XMC-001:** DL-4'teki Platform dosyaları (Bölüm 5). Onay gelene kadar dokunulmaz (D63/2).
**MDM sahibi — XMC-001:** DL-4'teki MDM istemci dosyası.

**Kapsam dışı:**

- Manifest / anahtar beyanı, tüketici servis kodu, Gateway, ekran, gezinme.
- `platform.document-repository.*` hiçbir role verilmez (D56/2); belge deposu servis kimliği desteği MOD-0262-FU01 / Platform'un
  işidir (D56/2, Y-8).
- Bağımlı anahtarların (`mdm.legal-entities.read`, `platform.workflow.instances.start`, `platform.workflow.tasks.approve`,
  `.reject`, `.request-info`) kaydı — o modüllerin sahibindedir (D47 rol matrisi giriş paragrafı). Rollere nasıl ulaşacakları
  açık karar K-4.
- `procurement.*`, `crm.*`, MOD-0031 / MOD-0140 okuma izinleri (matrisin anahtar kapsamı dışı).
- Platform modül kataloğu / kiracı hakkı (entitlement) kayıtları, kiracı verisi, rol üyeliği ataması, token içeriği,
  Production / Staging işlemleri.
- `waive-sod` iznini token'da süreli yapmak (token düzenlemesi) — açık karar K-W2.

## 3. Owned Objects

| Nesne | Sahip | Kesin değişmez |
|---|---|---|
| 117 Treasury `Permission` tanımı | Auth genel kataloğu (beyan tüketici manifestinde) | Anahtar başına tek etkin genel tanım; `TenantId` yok; `Permission.Module` = Bölüm 8'deki modül kodu |
| 8 açık atama anahtarı | `ExplicitGrantOnlyPermissions` | Hiçbir otomatik yol (SuperAdmin tam katalog, modül hakkı eşitlemesi, varsayılan rol) vermez |
| Treasury hak profili | Auth `Application/Common/Services` | Saf eşleme; modül kodu başına tam anahtar kümesi; eksik / fazla / bilinmeyen anahtar → mutasyondan önce ret |
| 26 ayrılmış sistem rolü | Auth, kiracı başına sistem rolü | Bölüm 14'teki tam anahtar kümesi; kullanıcı ataması otomatik değil |
| Admin / Viewer Treasury varsayılanı | Auth hak profili | **Öneri: hiçbir Treasury anahtarı** (K-2) |
| Modül kaynaklı haklar | Auth | Kiracı sınırlı; `GrantSource = Module`; `SourceModuleCode` = anahtarın modül kodu |
| `PaymentSodWaiverAssignment` (DL-5) | Auth, kiracıya ait kayıt | Bölüm 4; hedef parti zorunlu; süre ≤ 24 saat; onaylayan ve gerekçe kodu zorunlu |
| `CreatePaymentSodWaiverAssignmentCommand`, `RevokePaymentSodWaiverAssignmentCommand` (DL-5) | Auth | K2 denetim (Bölüm 21) |
| `GetPaymentSodWaiverAssignmentsForBatchQuery` (DL-5) | Auth | Kiracı uçları ve iç (servis) ucu aynı sorgu; yalnız geçerli atamaları döner (Bölüm 8) |
| `GetPermissionCandidatesQuery` (DL-6) | Auth | Yalnız iç (servis) uç; salt okuma; Bölüm 8.6 kuralları; yanıt yalnız kullanıcı kimlikleri |

DL-1…DL-4 ve DL-6 yeni kalıcı varlık açmaz; mevcut `Permission`, `Role`, `RolePermission`, `RoleAssignmentVersion`, `User`,
`UserRole`, manifest ve kiracı hakkı modelleri kullanılır (DL-6 yalnız bir okuma dizini ekler, Bölüm 5.4).

## 4. Entity Fields

**Mevcut nesneler (DL-1…DL-3):**

| Nesne / alan | Değer ya da kural |
|---|---|
| `Permission.Key` | Bölüm 14'teki 117 anahtardan biri; küçük harf, noktalı, ≥ 3 parça (PKS-001 §1); takma ad ya da joker yok |
| `Permission.Module` | Anahtarın tüketici modül kodu (Bölüm 8, K-1b) |
| `Permission.Resource` / `.Action` | `PermissionKeyParser` (ilk parça modül, son parça eylem, aradakiler kaynak) — ör. `mdm.contracts.value.read` → kaynak `contracts.value`, eylem `read` |
| `Permission.Scope` | `Tenant` (kiracıya atanabilir); katalog satırı geneldir |
| `Role.Name` | 26 ayrılmış ad (Bölüm 14); kiracı sistem rolü; aynı adlı sistem dışı rol → çakışma, hak yok |
| `RolePermission.TenantId` | Sunucu tarafından türetilir; boş / uyuşmaz → kapalı-başarısız |
| `RolePermission.GrantSource` / `.SourceModuleCode` | `Module` / anahtarın modül kodu |

`read` eylemli 18 anahtar (varsayılan Viewer tabanının seçtiği eylem): `finance-treasury.payment-batches.read`,
`finance-treasury.payment-trace.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`,
`finance-treasury.payment-sod-settings.read`, `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read`,
`finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read`, `finance-treasury.cash-positions.read`,
`finance-treasury.cash-position-threshold-policies.read`, `mdm.banks.read`, `mdm.bank-accounts.read`,
`mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.read`, `mdm.contracts.read`, `mdm.contracts.value.read`,
`mdm.contracts.documents.read`.

**Yeni kayıt (DL-5): `PaymentSodWaiverAssignment` (`EntityBase`, kiracıya ait, koleksiyon `paymentSodWaiverAssignments`)**

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | Evet | JWT'den; istekte yok |
| `GranteeUserId` | Guid | Evet | Aynı kiracının kullanıcısı; çağıran olamaz |
| `TargetPaymentBatchId` | Guid | Evet | Boş Guid yasak; Auth partiyi doğrulamaz (opak), MOD-0126 rotadaki partiyle eşler (D54) |
| `StartsAtUtc` | DateTimeOffset | Evet | Sunucu saatinden en çok 5 dk önce (saat kayması), en çok 24 saat sonra |
| `ExpiresAtUtc` | DateTimeOffset | Evet | `StartsAtUtc < ExpiresAtUtc ≤ StartsAtUtc + 24 saat` |
| `ApprovedByUserId` | Guid | Evet | Sunucu türetir = çağıran; istekte yok; `GranteeUserId`'den farklı |
| `ReasonCode` | string | Evet | Kapalı liste `ACIL-ODEME`, `TEK-YETKILI`, `DENETIM-ISTEGI` (D49/4) |
| `RevokedAtUtc` / `RevokedByUserId` | DateTimeOffset? / Guid? | Hayır | Yalnız geri alma komutu yazar; geri alınan atama geçerli değildir |
| `IsDeleted` / `DeletedAt` | — | — | Soft delete (EntityBase); fiziksel silme yok; v1'de silme komutu yok |
| `Version` | — | — | Teknik eşzamanlılık alanı (EntityBase) |

Dizinler: `(TenantId, TargetPaymentBatchId, ExpiresAtUtc)` ve `(TenantId, GranteeUserId, TargetPaymentBatchId)`; ikisi de
kısmi `IsDeleted = false`. Benzersizlik kısıtı yok (aynı kişiye aynı partide ardışık atama mümkün; sorgu yalnız geçerli olanı döner).

## 5. Repo Scope

Bu planlama görevi yalnız bu paketi yazar. Sonraki, ayrı onaylı çalışma anı teslimi aşağıdaki **joker içermeyen** listeyle
sınırlıdır ("yeni" yazılmayan dosya mevcuttur; satır numaraları `chore/ct/treasury-intake-ct-05` @ `5cfd67813` ölçümüdür).

### 5.1 Auth — DL-1, DL-2, DL-3 (MOD-0018 sahibi alanı)

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — `Keys` kümesi
  `:89-94` (bugün 10 anahtar → 18); her yeni anahtar için gerekçeli sabit (dosyanın deseni `:10-87`).
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` —
  `EntitlementOnlyViewerPermissions` `:120-168` (Viewer `read` kuralı `:207-213`).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/TreasuryEntitlementGrantProfile.cs` — **yeni**;
  saf, modül kodu başına tam küme + 26 rol şablonu (desen `ProductAbbreviationEntitlementGrantProfile.cs:9-100`).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs` — rol adı
  listeleri `:94`, `:96-110`; `GrantModuleWithKeysAsync` profil seçimi `:151-200`; `GrantPermissionsToRolesAsync` `:282-346`
  (açık atama süzgeci `:294` aynen kalır); `RevokeModuleAsync` `:373-390`; `SelectForRole` `:572-580`;
  `ReconcileSpecialProfilesAsync` `:582-625`.

**Auth testleri:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs` — yalnız gerileme
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TreasuryEntitlementGrantProfileTests.cs` — **yeni**
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TreasuryPermissionOnboardingMongoTests.cs` —
  **yeni**; gerçek, ayrı sahipli Mongo; sabit veritabanı `diten_auth_treasury_itest`; açık `MONGO_TEST_URI`; uygulama portu
  27017 yasak (FU20 2026-09-10 eki deseni, `MongoDbIndexConfigurations.cs:129` `EnsurePermissionOnboardingIndexesAsync`).
- Mevcut `ProductAbbreviationEntitlementGrantProfileTests`, FU22 / BL-592 testleri — salt okunur gerileme.

### 5.2 Platform ve MDM — DL-4 (SEC-002 eki, D56/1) — **"Dokunulacak dosyalar" (XMC-001 isteği, D63/2)**

**Platform (sahibi Beste — XMC-001 onayı Beste'den; MOD-0018 sahibi bu onayı vermez):**

| Dosya | Satır (ölçüm) | Bugün | Değişiklik |
|---|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` | `:15` | `MaxAllowedPairs = 38` | tavan, eklenen çift sayısı kadar artar (K-5 listesine bağlı) |
| aynı dosya | `:35-56` | `FirstConsumerPermissions` — yalnız ürün anahtarları (38) | K-5'teki `mdm.bank-accounts.*` / `mdm.contracts.*` anahtarları eklenir |
| aynı dosya | `:97-101` (`:99`) | `AllowsPair` modül kodunu `"product-item-sku-master"`'a sabitler | izinli modül kodu kümesi (ürün + K-1b'deki MOD-0289 / MOD-0217 kodları); çift → modül kodu eşlemesi kesin |
| aynı dosya | `:103-115` (`:111`) | `IsValidConfiguration` her çiftin modül kodunu sabit koda zorlar | aynı küme; bir çift yanlış modül koduyla gelirse yapılandırma geçersiz (bugünkü kapalı-başarısız davranış korunur) |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs` | `:146`, `:253` (tavan 38 sabitlemesi); `:19-32`, `:69-133` (modül kodu ve izin listesi testleri) | — | yeni tavan; yeni çiftler için gerçek doğrulayıcı testi; ürün çiftleri değişmez; yanlış modül kodlu çift reddi |
| Ortam yapılandırması `TrustedLegalEntityScopeCredential:Mdm:AllowedPairs` | depoda yok (her ortam verir) | 38 çift | yeni çiftler (sıra Bölüm 8) |
| `docs/guides/operations/post-deploy-steps.md` | yeni adım (desen `:297-320`, BL-592 §7) | — | sıra, geri alma ve kontrol |

**Koşullu (K-6 "evet" ise — finans tüketicisi Platform kapsam ucunu kullanacaksa):**

| Dosya | Satır | Bugün |
|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedLegalEntityScopeCredentialOptions.cs` | `:7` | tek bağlama `Mdm` |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` | `:11`, `:27`, `:76-81` | tüketici `DITENMDMSERVICE` sabit; yalnız `options.Value.Mdm` okunur |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeRequestExecutor.cs` | `:68` | `AllowsPair` çağrısı (tüketiciye göre seçim gerekir) |
| `services/Diten.Platform/src/Diten.Platform.API/Program.cs` | `:192-193`, `:213-215` | seçenek ve doğrulayıcı kaydı |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalLegalEntityScopeResolutionController.cs` | `:15` (sınıf; ilgili satırlar ölçülmedi) | MDM kimlik bilgisiyle çağrı |

**MDM (sahibi MDM sahibi — XMC-001 onayı ondan):**

| Dosya | Satır | Bugün | Değişiklik |
|---|---|---|---|
| `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs` | `:18` | `private const string ModuleCode = "product-item-sku-master"` | izinli modül kodu kümesi |
| aynı dosya | `:162-171` (`:170`) | `IsValidRequest` başka modül kodunu 400 `LEGAL_ENTITY_SCOPE_REQUEST_INVALID` ile reddeder | MOD-0289 / MOD-0217 kodları kabul; bilinmeyen kod yine 400 |
| aynı dosya | `:260`, `:282`, `:362`, `:377`, `:412` | yanıttaki `moduleCode` istenenle birebir karşılaştırılır | değişmez (gerileme) |
| `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs` | — | — | yeni kodlar + bilinmeyen kod reddi |
| `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs` | — | — | gerileme |

### 5.3 Auth — DL-5 (waive-sod parti bağı, D64)

Golden Reference arka uç klasör düzeni (Bölüm 10):

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/PaymentSodWaiverAssignment.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/PaymentSodWaiverAssignments/Commands/CreatePaymentSodWaiverAssignmentCommand.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Commands/RevokePaymentSodWaiverAssignmentCommand.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Queries/GetPaymentSodWaiverAssignmentsForBatchQuery.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/CommandHandlers/CreatePaymentSodWaiverAssignmentHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/CommandHandlers/RevokePaymentSodWaiverAssignmentHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/QueryHandlers/GetPaymentSodWaiverAssignmentsForBatchHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Validators/CreatePaymentSodWaiverAssignmentValidator.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/PaymentSodWaiverAssignmentsModels.cs` — **yeni** (tek dosyada DTO'lar)
- `…/Features/PaymentSodWaiverAssignments/PaymentSodWaiverAssignmentErrorCodes.cs` — **yeni** (desen `Features/Roles/RoleErrorCodes.cs`)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IPaymentSodWaiverAssignmentRepository.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PaymentSodWaiverAssignmentRepository.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs` — iki dizin (`:26` `EnsureIndexesAsync` içinden)
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs` — depo kaydı (`:101-103` deseni)
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PaymentSodWaiverAssignmentsController.cs` — **yeni**; kiracı uçları
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPaymentSodWaiverAssignmentsController.cs` — **yeni**; servis ucu (kimlik doğrulama K-W3)
- Testler (**yeni**): `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SodWaivers/CreatePaymentSodWaiverAssignmentHandlerTests.cs`,
  `…/SodWaivers/GetPaymentSodWaiverAssignmentsForBatchHandlerTests.cs`, `…/SodWaivers/PaymentSodWaiverAssignmentAuditTests.cs`,
  `…/SodWaivers/PaymentSodWaiverAssignmentMongoTests.cs` (sabit `diten_auth_sodwaiver_itest`, `MONGO_TEST_URI`, 27017 yasak),
  `…/SodWaivers/PaymentSodWaiverAssignmentContractTests.cs` (MOD-0126 tüketici sözleşmesi).
- K-W1 "evet" ise yeni Auth anahtarları Auth'un kendi tohum / katalog yolunda (dosya, karar sonrası ölçülür).

**DL-5'te Platform dosyası yok (ölçüm):** atama kaydı, uçlar ve denetim Auth içindedir; denetim önce-yaz kalıbıyla Auth
günlüğüne gider (`IAuthAuditService.WriteOnceAsync`, `IAuthAuditService.cs:18`). Gateway: K-W4 — mevcut
`/api/roles/{everything}` (`gateway/Diten.ApiGateway/ocelot.json:2232`) altında kalırsa Gateway değişmez; yeni kök yol
seçilirse `integration-agent` görevi.

### 5.4 Auth — DL-6 (onaylayıcı aday sorgusu, D66/1)

Golden Reference arka uç klasör düzeni (Bölüm 10); satır numaraları `chore/ct/treasury-intake-ct-05` @ `b9396b0e6` ölçümü:

- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/PermissionCandidates/Queries/GetPermissionCandidatesQuery.cs` — **yeni**
- `…/Features/PermissionCandidates/Handlers/QueryHandlers/GetPermissionCandidatesHandler.cs` — **yeni**
- `…/Features/PermissionCandidates/Validators/GetPermissionCandidatesValidator.cs` — **yeni**
- `…/Features/PermissionCandidates/PermissionCandidatesModels.cs` — **yeni** (tek dosyada DTO'lar)
- `…/Features/PermissionCandidates/PermissionCandidateErrorCodes.cs` — **yeni** (desen `Features/Roles/RoleErrorCodes.cs`)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IRolePermissionRepository.cs` — izin → rol
  ters araması (kiracı sınırlı, `GrantSource` ile); bugün yalnız rol → izin yönü var (`:7-8`, `:17`)
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs` — uygulaması
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs` — `rolePermissions`
  için `(TenantId, PermissionId)` dizini (bugün yalnız `(RoleId, PermissionId, TenantId)` tekil ve `(RoleId, TenantId)`, `:162-168`)
- Salt okuma tüketilen (değişmez): `IUserRoleRepository.GetUserIdsByRoleAsync` (`IUserRoleRepository.cs:14`),
  `IUserRepository`, `TenantEffectivePermissionResolver` (`Application/Common/Authorization/TenantEffectivePermissionResolver.cs`;
  giriş / yenileme işleyicilerinin kullandığı — `LoginCommandHandler.cs:28`, `RefreshTokenCommandHandler.cs:26`)
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionCandidatesController.cs` — **yeni**; iç uç
  (kimlik doğrulama K-A1; DL-5 iç ucuyla aynı mekanizma)
- Testler (**yeni**): `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/PermissionCandidates/GetPermissionCandidatesHandlerTests.cs`,
  `…/PermissionCandidates/PermissionCandidatesMongoTests.cs` (sabit `diten_auth_candidates_itest`, `MONGO_TEST_URI`, 27017 yasak),
  `…/PermissionCandidates/PermissionCandidatesContractTests.cs` (tüketici sözleşmesi, K-A1 dosyasına karşı).
- K-A2 (a) seçilirse Auth'tan Platform kapsam çözümüne bağlantı (dosyalar karar sonrası ölçülür; Platform tarafı XMC-001 Beste).

**DL-6'da Platform dosyası yok (K-A2 (b) önerisiyle):** sorgu Auth içindedir; Gateway değişmez (iç uç).

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint çalışma kitapları, DCP'ler, backlog ve ilgisiz yönetişim dosyaları.
- `execution/registries/module-id-registry.md` ve `execution/domains/platform-shared-services/domain-config.md` bu görevde (öneri raporda).
- Altı tüketici paket ve MOD-0018-FU9…FU22 dahil diğer bütün paketler; özellikle MOD-0126 (D64: değiştirilmez).
- Bölüm 5 dışındaki bütün Auth dosyaları: `DataSeeder.cs`, token üretimi / yenileme, kullanıcı-rol atama işleyicileri,
  `FullCatalogPermissionGrantService.cs` (K-3 kararı olmadan), FU20 / FU22 / BL-592 profil üretim kodu, `appsettings*`, gizli bilgiler.
- `services/Diten.Platform/**` ve `services/Diten.MdmService/**` — Bölüm 5.2 dışında; Bölüm 5.2 de XMC-001 onayına kadar.
- `services/Diten.FinanceTreasuryService/**` (henüz yok; MOD-0126 DL-1'in), `gateway/**` (`ocelot.json` yalnız `integration-agent`), `frontend/**`.
- Katalog / hak eşitlemesi çalıştırma, rol üyeliği, kiracı verisi, Production / Staging.

## 7. Dependencies

| Bağımlılık | Durum (ölçüm 2026-10-28) | Etki |
|---|---|---|
| Master parent `MOD-0018` RBAC / ABAC; FU13 token / önbellek sınırlı bayatlık | var | haklar yalnız token yenileme / yeni girişle görünür |
| Modül manifestleri (anahtar beyanı): MOD-0126 DL-1 (dört finans paketi için tek yazar), MOD-0289 ve MOD-0217 MDM manifestleri | yok (servis yok / paket taslak) | DL-1/2/3 kodu manifestten **önce** dağıtılır (Bölüm 8 sırası); aksi hâlde Viewer tabanı `read` anahtarlarını her kiracıya kalıcı verir (BL-592 §7 (c), `post-deploy-steps.md:305`) |
| Modül kodu değerleri (K-1b) | seçilmedi (MOD-0289 `:146` "SEC-002 modül kodu değeri seçilmedi (ek paketinde)") | profil, kiracı hakkı ve DL-4 çiftleri bu değere bağlı |
| Platform modül kataloğu / kiracı hakkı kaydı (Treasury modül kodları) | yok | hak olmadan profil hiçbir hak vermez (beklenen) |
| MOD-0217 onayı | `draft` | DL-3 kodu onaydan sonra (Y-6, Y-10/1) |
| XMC-001 — Beste (Platform), MDM sahibi | istenmedi (D63/2) | DL-4 dosyalarına onaya kadar dokunulmaz |
| D64 main'de | değil (ct-05 dalında yerel commit `4d8a4050a`) | DL-5 paket kapısı (D62/1) |
| Auth K2 denetim yolu: önce-yaz `IAuthAuditService.WriteOnceAsync` | var (`IAuthAuditService.cs:18`; kullanım `IssueServiceIdentityTokenHandler.cs:123,151`) | DL-5 için uyulabilir yol |
| Auth denetim defteri: `auth-gunlugu` izi | **`aday`** — okuyan uç yok (`tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`) | **DL-5 ENGELİ:** yeni K2 komutu kabul edilmiş ize yazılmalı; CT defter kararı ya da okuyan uç gerekir (K-W5) |
| FinanceTreasuryService → Auth servis kimliği (DL-5 iç ucu) | servis yok; Auth'ta servis kimliği token ucu var (`Api/Controllers/Internal/InternalServiceIdentityTokensController.cs`) | K-W3 |
| MOD-0126 tüketici sözleşmesi | paket metninde: port `IPaymentSodWaiverAssignmentSource`, okunan alanlar (bitiş, başlangıç, onaylayan, gerekçe kodu, hedef parti), aday sorgusu (MOD-0126 `:1253-1259`, AC-42, AC-43) | DL-5 yanıt biçimi bununla birebir uyumlu (Bölüm 8.5) |
| D66 main'de | değil (karar dosyasında, yalnız `chore/ct/treasury-intake-ct-05` dalında; 2026-10-28) | DL-6 paket kapısı (D62/1) |
| Aday sorgusu tüketicileri (D66/1): MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3 | paket metinlerinde: aday tabanı bu paketin DL-6'sı, tüketici kendi kurallarını uygular, `CandidatePrincipalIds` (Y-12); izin anahtarları Bölüm 8.6 tablosu; MOD-0217 anahtarı `mdm.contracts.versions.approve` (KARAR D69; MOD-0217 O-3 (b) kapandı) | DL-6 kodu tüketici dilimlerinin kabul kapısı (Teslim dilimleri) |
| FinanceTreasuryService ve MdmService → Auth servis kimliği (DL-6 iç ucu) | FinanceTreasury servisi yok; MdmService var; Auth'ta servis kimliği token ucu var (`Api/Controllers/Internal/InternalServiceIdentityTokensController.cs`) | K-A1 (K-W3 ile aynı karar) |
| Kullanıcı → şirket kapsamı | Auth'ta yok (ölçüm: `services/Diten.AuthService/src`'de `LegalEntity` yalnız `EntitlementPermissionSyncService.cs` ve `ProductLegalEntityScopeEntitlementGrantProfile.cs`'te, ürün profili); Platform `IDataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, featureCode, …)` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IDataScopeResolver.cs:5-10`) | K-A2 |

## 8. Runtime Constraints

### 8.1 Modül kodu ve profil seçimi (DL-1…DL-3)

- Her Treasury anahtarı tam bir modül koduna aittir. **Öneri (karar değil, K-1b):** paket başına bir kod — kiracı AP'yi AR'siz
  alabilir. Çapraz modüllü roller (Hazine yöneticisi: MOD-0126 + MOD-0124; Hazine konsolide görüntüleyici: dört finans modülü)
  her kaynak modülden ayrı `RolePermission` satırı alır; bir modülün hakkı düşünce yalnız o modülün satırları silinir.
- Profil bir modül kodunun **bütün** beyan kümesini sahiplenir: o modül kodu için genel kalan (generic residual) boştur. Beyan
  kümesi profildeki tam kümeye eşit değilse (eksik, fazla, bilinmeyen eylem, takma ad, joker) mutasyondan **önce** hata —
  eksik ve fazla anahtarlar adlarıyla yazılır (BL-592 FIX1 deseni).
- Mutasyon sırası (FU22 §8 deseni): normalleştir → profil alt kümelerini tespit et → beyan kümesini ve katalogu tam doğrula →
  26 rol adının hepsini çakışma için önceden denetle (görünür bir sistem dışı aynı adlı rol → hiçbir rol / hak yazılmaz) →
  rolleri idempotent oluştur → hakları ver / gereksizleri geri al → yalnız eşleşen kiracı + kaynak modül satırlarına dokun;
  `Manual` / `System` / başka modül / başka kiracı satırları korunur.
- Hak eksik / kapalı / süresi dolmuş → eşleşen modül kaynaklı Treasury hakları bütün rollerden kalkar. Ulaşılamaz / bozuk /
  zaman aşımı / belirsiz hak durumu → ne verir ne geri alır; yeniden denenir. İptal belirteci yayılır.
- Hak yalnız rol hakkı yaratır; **kullanıcıya rol üyeliği otomatik değildir**.

### 8.2 Varsayılan roller

- Açık atama listesindeki 8 anahtar hiçbir otomatik yoldan (SuperAdmin tam katalog `FullCatalogPermissionGrantService`,
  modül eşitlemesi `:294`, varsayılan rol `DefaultRolePermissionTemplate.cs:181-183`) verilmez.
- 117 anahtarın **hiçbiri** varsayılan Viewer tabanına girmez: 18 `read` eylemli anahtar `EntitlementOnlyViewerPermissions`'a
  eklenir (diğer 99 anahtar — D69'un `mdm.contracts.versions.approve`'si dahil, eylemi `approve` — `read` eylemli olmadığından o kurala zaten girmez).
- Modül hakkıyla Admin ve Viewer: **öneri — hiçbir Treasury anahtarı** (K-2). Matris 117 anahtarın hepsini bir role ya da
  açık atamaya yerleştirmiştir; Admin'e fazladan verilen her anahtar matris dışıdır.
- SuperAdmin (yalnız varsayılan kiracı, `FullCatalogPermissionGrantService.cs`) açık atama dışındaki anahtarları tam katalog
  yoluyla alır — FU20 / FU22'de de böyledir; değiştirilip değiştirilmeyeceği K-3.

### 8.3 Dağıtım sırası (BL-592 §7 deseni)

1. Auth DL-1/2/3 kodu (açık atama + Viewer dışlaması + profil). 2. Tüketici manifesti (anahtarları ilk beyan eden adım).
3. (DL-4) Platform kodu, sonra Platform yapılandırması (`AllowedPairs`), sonra MDM istemcisi. Ters sıra: (2) Auth'tan önce
gelirse eski Auth `read` anahtarlarını her kiracının Viewer'ına verir ve geri almaz; (3)'te yapılandırma koddan önce gelirse
Platform MDM kimlik bilgisini tamamen reddeder (her güvenilir kapsam sorusu 403). Geri alma ters sırada.

### 8.4 SEC-002 eki (DL-4)

- Yalnız MDM tüketicisi (D56/1): MOD-0289 ve MOD-0217 anahtarlarıyla Platform güvenilir kapsam sorusu çalışır. Çift listesi
  K-5 (hangi anahtarlar şirket kapsamı sorar). `read-tenant-wide` anahtarları kapsam sormaz (kapsamı atlar), öneri: listede yok.
- Kapalı-başarısız davranış değişmez: bilinmeyen çift → 403; yanlış modül kodlu yapılandırma → kimlik bilgisi reddi;
  hata / boş liste / belirsizlik → erişim yok (Y-4/6).
- Finans servisinin kapsam yolu (`IFinanceTreasuryDataScopeSource`, Y-4/6) bugün Platform'da tüketici olarak yok; Platform
  ucu yalnız MDM kimlik bilgisini kabul eder (`TrustedLegalEntityScopeCredentialAuthenticator.cs:11`,
  `TrustedLegalEntityScopeCredentialOptions.cs:7`). Bu ekin kapsamına girip girmediği K-6.

### 8.5 `waive-sod` parti bağlı atama (DL-5, D54 + D64)

- Atama kaydı **kullanım yetkisidir, izin değildir**: MOD-0126 muafiyet ucu `[HasPermission("finance-treasury.payment-batches.waive-sod")]`
  ister (izin açık atamayla role verilir) **ve** Auth'tan o kullanıcı + o parti için geçerli atama okur. Öneri bu ikili yapıdır
  (token üretimine dokunmaz); iznin token'da süreli olması K-W2.
- Geçerlilik sorgu anında hesaplanır: `StartsAtUtc ≤ şimdi < ExpiresAtUtc`, geri alınmamış, silinmemiş, `TargetPaymentBatchId`
  istenen partiye eşit ve boş değil. Bitişte arka plan işi yoktur; atama kendiliğinden geçersizleşir.
- Yazma: onaylayan (çağıran) atamayı başka bir kullanıcı için yaratır; `ApprovedByUserId` sunucudan; kendine atama yasak.
  Atamayı onaylama yetkisi bir Auth anahtarıdır (MOD-0126 `:1246`: "bu onay izni MOD-0018'de tanımlanır") — K-W1.
- Onaylayanın partiyi oluşturan / gönderen / son güncelleyen olmaması Auth'ta denetlenemez (parti verisi Auth'ta yok); MOD-0126
  kullanım anında denetler (AC-43 (d)). Auth `ApprovedByUserId`'yi her yanıtta taşır.
- Sorgu yanıtı (MOD-0126 portu `IPaymentSodWaiverAssignmentSource` için; alan adları sözleşme dosyasında dondurulur, K-W6):
  her öğe `assignmentId`, `granteeUserId`, `targetPaymentBatchId`, `startsAtUtc`, `expiresAtUtc`, `approvedByUserId`,
  `reasonCode`. `granteeUserId` süzgeci kullanım anı içindir (MOD-0126 AC-43). **Muafiyet aday listesi (D56/3) burada ayrıca
  tanımlanmaz (D66/1 — tek aday sorgusu):** DL-6 aday sorgusunun `waive-sod` + parti biçimidir (Bölüm 8.6) ve aynı geçerlilik
  kuralını bu kayıttan okur; süzgeçsiz biçim yalnız kiracı uçlarındaki K1 kullanım incelemesi içindir (Bölüm 14.3).
- Tüketici kuralı (MOD-0126'nın, burada yalnız sözleşme olarak): yanıt yok / okunamıyor / alan eksik / hedef parti uyuşmuyor →
  403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; Auth'un sorgusu bunu destekler: başka partiye bağlı atama **hiç dönmez**, hedef partisiz
  satır **hiç dönmez**.
- Kiracı: `TenantId` JWT'den (kiracı uçları) ya da güvenilir servis bağlamından (iç uç); istek gövdesinde kiracı yok; başka
  kiracının atama kimliği → 404; başka kiracının partisi için sorgu boş döner.

### 8.6 Onaylayıcı aday sorgusu (DL-6, KARAR D66/1)

- **Amaç.** MOD-0023 şablonları atamasızdır; onaylayıcıları başlatma isteğinin `CandidatePrincipalIds` alanı belirler (Y-12).
  Aday **tabanı** bu sorgudur: "bu izne sahip kullanıcılar". Tüketici sonuca kendi paketinin kurallarını uygular ve kalanı
  `CandidatePrincipalIds` olarak gönderir (D66/1). Auth SoD kuralı, eşik ya da kayıt durumu bilmez; yalnız tabanı verir.
- **Uç (öneri, K-A1):** `GET /internal/permission-candidates?permissionKey={key}[&paymentBatchId={id}]` — iç uç, Gateway'den
  geçmez; kimlik doğrulama DL-5 iç ucuyla aynı mekanizma (K-W3 önerisi: servis kimliği token'ı, hedef kitle yalnız Auth iç
  uçları); izinli çağıranlar FinanceTreasuryService ve MdmService (MOD-0217). Kiracı kullanıcı token'ı kabul edilmez.
- **Kiracı.** `TenantId` güvenilir servis bağlamından (istekte kiracı alanı yok; DL-5 iç ucu kuralı). Başka kiracının
  kullanıcısı, rolü ya da hakkı hiç dönmez.
- **İzin sahipliği = token'a giren küme.** Kullanıcının kiracıdaki rolleri → rol hakları → kiracının modül hakkıyla etkin
  izin kümesi; hesap giriş / yenilemedekiyle aynıdır (`TenantEffectivePermissionResolver`). Anahtar katalogda yoksa ya da
  kiracının o modül hakkı yoksa kimse aday değildir.
- **Açık atama anahtarı** (`ExplicitGrantOnlyPermissions`, Bölüm 14.1): yalnız `GrantSource = Manual` rol hakkından gelen
  sahiplik sayılır (`Domain/Entities/GrantSource.cs:14-23`); `System` ya da `Module` kaynaklı bir satır (otomatik yoldan
  sızmış) sahiplik saymaz.
- **Yalnız etkin kullanıcı:** `IsActive = true` (`User.cs:31`), silinmemiş, davet beklemiyor (`IsInvitationPending() = false`,
  `:118`), platform hesabı değil (`IsPlatformAccount = false`, `:50`). Hizmet hesabı (`AccountKind`, `:56`) ve geçici kilit
  (`LockoutEnd`) — **K-A3**.
- **Şirket kapsamı.** Aday yalnız kaydın şirketinde (ödeyen şirket / fatura / pozisyon / sözleşme şirketi) kapsamı olan
  kullanıcıdır. Auth'ta kullanıcı → şirket verisi yoktur; kapsam Platform `IDataScopeResolver`'dadır (Bölüm 7). Kapsamın hangi
  tarafta uygulanacağı **K-A2** (CT önerisi (b): tüketici kendi kapsam portuyla süzer — FinanceTreasury
  `IFinanceTreasuryDataScopeSource`, MDM `IContractDataScopeSource`; Auth'a Platform bağımlılığı eklenmez). K-A2 ne olursa
  olsun şirket kapsamı dışındaki kullanıcı `CandidatePrincipalIds`'e girmez.
- **Muafiyet adayları (D56/3 ile birleşik).** `permissionKey = finance-treasury.payment-batches.waive-sod` için `paymentBatchId`
  zorunludur; yanıt yalnız `waive-sod`'u açık atamayla tutan, etkin ve bu partiye geçerli DL-5 ataması (Bölüm 8.5 geçerlilik
  kuralı) olan kullanıcılardır. Bu biçim DL-5 kaydını okur: DL-5 kodu main'de değilken `waive-sod` sorgusu kapalı-başarısız
  yanıt verir (kod sözleşme dosyasında, K-A1) ve tüketici muafiyet adayı eklemez (MOD-0126 AC-42; muafiyet yolu kapalı — D54,
  D66/3). Başka anahtarda `paymentBatchId` → 400.
- **Tüketicilerin anahtarları** (Bölüm 14.2 ile ölçüldü): MOD-0126 `finance-treasury.payment-batches.approve` (tier-2 partide
  `approve` ∩ `finance-treasury.payment-batches.tier2-approve`, R4; muafiyette `waive-sod` + parti); MOD-0119
  `finance-treasury.ap-invoices.approve`; MOD-0120 `finance-treasury.receivable-adjustments.approve` (eşik üstü
  `approve` ∩ `finance-treasury.receivable-adjustments.tier2-approve`); MOD-0124 `finance-treasury.cash-positions.approve`; MOD-0217
  `mdm.contracts.versions.approve` (**KARAR D69**; rol "Sözleşme onaylayıcı", açık atama değil; MOD-0217 hazırlayanı dışlar — MOD-0217 AC-33). Sorgu tek anahtar alır; kesişimi (∩) tüketici servis iki ayrı çağrıyla alır (sahip denetimi 61466e504, P2) — tier-2
  yetkisi tek başına aday yapmaz, çünkü karar ucu temel `approve`'u da ister. Bağımlı `platform.workflow.tasks.approve`'un ayrıca aday şartı olup olmadığı **K-A4** (K-4'e bağlı).
- **Yanıt:** `permissionKey`, `principalIds` (Guid listesi, tekil, sıralı); ad, e-posta, rol adı ya da başka kişi verisi yok.
  Boş liste hata değildir (200). Alan adları ve hata kodları sürümlü sözleşme dosyasında dondurulur (K-A1; K-W6 deseni).
- **Tüketici kuralı (sözleşme olarak):** her hata yanıtı ve okunamayan gövde "okunamaz"dır → gönderme kapalı-başarısız; Auth'un
  hiçbir yanıtı "aday yok → kural atla" ya da "herkes aday" anlamına gelmez.
- **Okuma denetimi:** sorgu okumadır, ayrıca denetlenmez (Bölüm 21).

## 9. Layout & Shell Contract

`shell: none`. Bu FU Razor görünümü, ön yüz rotası, DataTable, yerelleştirme kaynağı ya da gezinme öğesi sahiplenmez.
Hiçbir `.cshtml` dosyası oluşturulmaz ya da değiştirilmez; bu yüzden `Layout = "_LayoutPlatformAdmin"` /
`Layout = "_LayoutTenantShell"` yazılacak dosya yoktur. Atama ekranı ve rol ekranlarındaki gösterim ayrı paket kararıdır
(Bölüm 11).

## 10. Backend File Convention

- DL-1…DL-3 CRUD değildir; CQRS klasörü açmaz. Profil `Application/Common/Services` altında tek, saf, statik eşlemedir (FU20 /
  FU22 / BL-592 deseni); kalıcılık yalnız mevcut `EntitlementPermissionSyncService` üzerinden.
- DL-5 Golden Reference arka uç düzenini birebir izler: `Features/PaymentSodWaiverAssignments/{Commands, Queries,
  Handlers/CommandHandlers, Handlers/QueryHandlers, Validators}` + tek `PaymentSodWaiverAssignmentsModels.cs`. Command / Query
  `sealed record`; işleyici `{Verb}{Module}Handler`, doğrulayıcı `{Verb}{Module}Validator` (Command / Query soneki yok).
  Yanıt `Response<T>`. Bir dosyada tek `public` tür.
- DL-6 aynı düzeni izler: `Features/PermissionCandidates/{Queries, Handlers/QueryHandlers, Validators}` + tek
  `PermissionCandidatesModels.cs`; `GetPermissionCandidatesQuery` `sealed record`, `GetPermissionCandidatesHandler`,
  `GetPermissionCandidatesValidator`. Komut yok.
- İptal belirteci depo sınırına kadar taşınır.

## 11. Frontend File Contract

Ön yüz dosyası kapsamda değildir. Atama ekranı yok (v1'de atama API ile; K-W4). Rol ekranları rol adlarını mevcut Rol
ekranıyla gösterir; Türkçe görünen ad K-7.

**Kullanıcıya görünen metin.** Platform modülü — 2 dil (en, tr). Görünen metin yalnız 26 rol şablonunun görünen adı ve
açıklamasıdır (Bölüm 14: en ve tr birlikte). Hata yanıtları sabit kod taşır (`errorCodes`), metni ekran eşler
(`RoleErrorCodes.cs` deseni); bu FU yeni ekran metni yazmaz.

## 12. Validation Rules

| Girdi / karar | Zorunlu | Kural | Hata |
|---|---|---|---|
| Modül beyan kümesi | Evet | Bölüm 14'teki modül kodu kümesine birebir eşit | Mutasyondan önce ret; eksik / fazla adlarıyla |
| Açık atama anahtarı | Evet | 8 anahtar hiçbir rol şablonunda ve otomatik yolda yok | Herhangi bir otomatik hak → test kırmızı |
| Viewer tabanı | Evet | 117 anahtarın hiçbiri | Herhangi biri → test kırmızı |
| Admin / Viewer modül hakkıyla | Evet | K-2 kararı (öneri: hiçbiri) | Fazla hak → test kırmızı |
| Rol adları | Evet | 26 ad; aynı adlı sistem dışı rol → bütün profil mutasyonu durur | Rol ve hak yazılmaz |
| `platform.document-repository.*` | Evet | Hiçbir rolde yok (D56/2) | Test kırmızı |
| DL-5 `TargetPaymentBatchId` | Evet | Boş Guid değil | 400 `SOD_WAIVER_TARGET_BATCH_REQUIRED` |
| DL-5 `GranteeUserId` | Evet | Aynı kiracıda var; çağıran değil | yok / başka kiracı → 404 `SOD_WAIVER_GRANTEE_NOT_FOUND`; kendine → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` |
| DL-5 `StartsAtUtc` / `ExpiresAtUtc` | Evet | Bölüm 4; süre 0 < s ≤ 24 saat | 400 `SOD_WAIVER_DURATION_INVALID` |
| DL-5 `ReasonCode` | Evet | Kapalı liste (D49/4) | 400 `SOD_WAIVER_REASON_CODE_INVALID` |
| DL-5 onaylayan | Evet | Sunucu türetir; çağıranın K-W1 izni var | izin yok → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`; istekte `approvedByUserId` alanı → 400 (bilinmeyen alan) |
| DL-5 sorgu partisi | Evet | Boş Guid değil | 400 (MOD-0126 bunu 403'e çevirir) |
| DL-6 `permissionKey` | Evet | PKS-001 biçimi ve katalogda tanımlı | 400 (kod sözleşme dosyasında, K-A1) |
| DL-6 `paymentBatchId` | `waive-sod`'da evet, diğerlerinde yasak | Boş Guid değil | 400 |
| DL-6 çağıran | Evet | K-A1 servis kimliği (FinanceTreasuryService, MdmService) | yok / yanlış hedef kitle → 401 / 403; kullanıcı token'ı → 403 |

## 13. Failure Path to Verify

- **Beyan kümesi eksik / fazla:** profil hatası, sıfır rol / hak mutasyonu; eksik ve fazla anahtar adlarıyla.
- **Katalogda bir anahtar eksik / silinmiş / farklı:** rol oluşturma ve hak yok.
- **Rol adı çakışması** (26 addan ilki, ortadaki, sonuncusu): sıfır yeni rol, sıfır hak; kiracının önceki durumu aynen.
- **Etkin hak:** Bölüm 14'teki tam matris; tekrar oynatma kopya üretmez.
- **Hak düştü / kapandı / süresi doldu:** eşleşen Treasury hakları bütün 26 rolden ve Admin / Viewer'dan kalkar; başka modül
  ve elle verilen haklar kalır.
- **Hak belirsiz:** ne verir ne geri alır, başarı bildirilmez.
- **Kiracılar arası:** A kiracısının eşitlemesi B'nin rol / hak durumunu yaratmaz, silmez, göstermez.
- **Manifest Auth'tan önce** (sıra ihlali sınaması, birim testte): yeni Auth koduyla `read` anahtarı hak olmadan hiçbir Viewer'a gitmez.
- **DL-4 bilinmeyen çift / yanlış modül kodu:** 403 / yapılandırma geçersiz; ürün çiftleri etkilenmez.
- **DL-5 hedef partisiz istek** → 400, kayıt yok, denetim yok; **hedef partisiz bozuk satır** (veritabanında elle) → sorgu
  döndürmez → MOD-0126 403.
- **DL-5 başka partiye bağlı atama** → B partisi sorgusunda dönmez → MOD-0126 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`.
- **DL-5 süre 24 saat + 1 sn** → 400; **bitiş geçmiş** → sorgu döndürmez.
- **DL-5 onaylayan = alıcı** → 403; **K-W1 izni olmayan çağıran** → 403; **başka kiracının kullanıcısı / ataması** → 404.
- **DL-5 denetim yazılamıyor** → komut başarısız, atama yazılmaz (K2, önce-yaz).
- **DL-5 eşzamanlı geri alma** → `Version` çakışması 409; sessiz üzerine yazma yok.
- **DL-6 pasif / silinmiş / davet bekleyen / platform hesabı** izin sahibi → aday değil.
- **DL-6 açık atama anahtarı `Module` / `System` kaynaklı satırdan** → aday değil; yalnız `Manual` satır sayılır.
- **DL-6 kiracının modül hakkı yok** (rol hakkı duruyor) → aday değil (token'a girmeyen izin adaylık vermez).
- **DL-6 başka kiracının kullanıcısı** → yanıtta hiç yok.
- **DL-6 `waive-sod` başka partiye bağlı / süresi geçmiş / geri alınmış atama** → yanıtta yok; DL-5 kodu yokken → kapalı-başarısız.
- **DL-6 Auth ulaşılamıyor / hata** → tüketici gönderimi kapalı-başarısız (MOD-0023 çağrısı yok).
- **Yetkisiz aktör:** `platform_admin` geçişi kabul kanıtı değildir; testler doğrudan `tenant_user` ile koşar.

## 14. Authorization Convention

- Politika: tüketici uçlarında `[Authorize]` + her eylemde `[HasPermission("…")]` (tüketici paketlerin işi). Aktör `tenant_user`.
- Biçim PKS-001: küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. Ad alanları `finance-treasury.*` (D11/4) ve `mdm.*`.
  Not: PKS-001 §4 ad alanı tablosunda `finance-treasury.*` satırı yok (öneri raporda; `.antigravity/**` korumalı).

### 14.1 Yalnız açık atama (`ExplicitGrantOnlyPermissions`) — 8 anahtar

| Anahtar | Dayanak | Not |
|---|---|---|
| `finance-treasury.payment-batches.waive-sod` | D47/1, D49/4, D54 | kullanım ayrıca DL-5 atamasını ister (Bölüm 8.5) |
| `finance-treasury.payment-batches.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.ap-invoices.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.receivable-invoices.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.cash-positions.read-tenant-wide` | D14/1 | yalnız okuma |
| `mdm.bank-accounts.read-tenant-wide` | D23/1 | yalnız okuma, maskeli |
| `mdm.bank-accounts.read-unmasked` | D23/4 | gerekçe kodu + değerden önce denetim (MOD-0289) |
| `mdm.contracts.read-tenant-wide` | D41/1 | yalnız okuma; DL-3 ile (MOD-0217 onayından sonra) |

### 14.2 Rol şablonları — 26 (D47 rol matrisi, D49 ve D69 ile güncel)

`RoleName` sistem rol adıdır (CT önerisi, sahip teyidi K-7); `tr` adı matristeki addır. "(+X)" X'in bütün anahtarlarını
miras alır (D49/1). Sayı = rolün tam anahtar sayısı (mekanik genişletme, Bölüm 19).

**Finans — 19 rol (DL-1):**

| # | RoleName | en | tr | Anahtarlar | Sayı |
|---|---|---|---|---|---|
| 1 | `TreasuryPaymentViewer` | Payment Viewer | Ödeme görüntüleyici | `finance-treasury.payment-batches.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`, `finance-treasury.payment-trace.read` | 4 |
| 2 | `TreasuryApViewer` | AP Viewer | AP görüntüleyici | `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read` | 2 |
| 3 | `TreasuryArViewer` | AR Viewer | AR görüntüleyici | `finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read` | 2 |
| 4 | `TreasuryCashPositionViewer` | Cash Position Viewer | Nakit pozisyonu görüntüleyici | `finance-treasury.cash-positions.read`, `finance-treasury.cash-position-threshold-policies.read` | 2 |
| 5 | `TreasuryConsolidatedViewer` | Treasury Consolidated Viewer | Hazine konsolide görüntüleyici | 1–4'ün birleşimi (10 anahtar); hiçbir rol miras almaz; yalnız gerekçeyle atanır | 10 |
| 6 | `TreasuryPaymentPreparer` | Payment Preparer | Ödeme hazırlayıcı | (+1) + `finance-treasury.payment-batches.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.payment-evidence.link`, `finance-treasury.payment-attestations.create` | 12 |
| 7 | `TreasuryPaymentApprover` | Payment Approver | Ödeme onaylayıcı | (+1) + `finance-treasury.payment-batches.approve`, `.reject`, `.request-evidence`, `.reconcile` | 8 |
| 8 | `TreasuryPaymentTier2Approver` | Payment Approver (Tier 2) | Ödeme onaylayıcı (2. kademe) | (+7) + `finance-treasury.payment-batches.tier2-approve` | 9 |
| 9 | `TreasuryPaymentReleaser` | Payment Releaser | Ödeme serbest bırakıcı | (+1) + `finance-treasury.payment-batches.release`, `.reconcile` | 6 |
| 10 | `TreasuryManager` | Treasury Manager | Hazine yöneticisi | (+1) (+4) + `finance-treasury.payment-batches.bulk-delete`, `finance-treasury.payment-batches.reconcile`, `finance-treasury.cash-position-threshold-policies.create`, `.update`, `.delete`, `.bulk-delete`, `finance-treasury.cash-positions.bulk-delete`, `finance-treasury.cash-positions.workflow-reconcile`, `finance-treasury.cash-positions.export-reconcile`, `finance-treasury.payment-sod-settings.read`, `finance-treasury.payment-sod-settings.update` (D57/4). **Yok:** approve, reject, request-evidence, release, tier2-approve, waive-sod, publish, AP / AR okuması (D47/1) | 17 |
| 11 | `TreasuryApOperator` | AP Operator | Borç (AP) operatörü | (+2) + `finance-treasury.ap-invoices.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.ap-invoice-exceptions.create`, `finance-treasury.ap-invoice-evidence.link` | 10 |
| 12 | `TreasuryApApprover` | AP Approver | Borç (AP) onaylayıcı | (+2) + `finance-treasury.ap-invoices.approve`, `.reject`, `.request-info`, `.reconcile` (D50/1) | 6 |
| 13 | `TreasuryApOperationsManager` | AP Operations Manager | AP operasyon yöneticisi | (+11) + `finance-treasury.ap-invoices.hold`, `.release-hold`, `.bulk-delete`, `finance-treasury.ap-invoice-exceptions.resolve`, `.reopen`. **Yok:** approve, reject, request-info (D49/3) | 15 |
| 14 | `TreasuryArOperator` | AR Operator | Alacak (AR) operatörü | (+3) + `finance-treasury.receivable-invoices.create`, `.update`, `.delete`, `.issue`, `.collection-manage`, `finance-treasury.receivable-payments.record`, `finance-treasury.receivable-adjustments.request`, `.reconcile` | 10 |
| 15 | `TreasuryArApprover` | AR Approver | Alacak (AR) onaylayıcı | (+3) + `finance-treasury.receivable-adjustments.approve`, `.reject`, `.reconcile` (D50/1) | 5 |
| 16 | `TreasuryArOperationsManager` | AR Operations Manager | AR operasyon yöneticisi | (+14) + `finance-treasury.receivable-invoices.bulk-delete`. **Yok:** adjustments approve / reject / tier2-approve | 11 |
| 17 | `TreasuryArTier2AdjustmentApprover` | AR Tier-2 Adjustment Approver | AR 2. kademe düzeltme onaylayıcı | (+15) + `finance-treasury.receivable-adjustments.tier2-approve` (D50/2; tek başına atanır) | 6 |
| 18 | `TreasuryCashPositionPreparer` | Cash Position Preparer | Nakit pozisyonu hazırlayıcı | (+4) + `finance-treasury.cash-positions.create`, `.update`, `.delete`, `.reconcile`, `.attach-evidence`, `.submit`, `.workflow-reconcile`, `.export-evidence`, `.export-reconcile` | 11 |
| 19 | `TreasuryCashPositionApprover` | Cash Position Approver | Nakit pozisyonu onaylayıcı | (+4) + `finance-treasury.cash-positions.approve`, `.reject`, `.publish`, `.workflow-reconcile` | 6 |

**Ana veri — 7 rol (DL-2: 20–22, DL-3: 23–26):**

| # | RoleName | en | tr | Anahtarlar | Sayı |
|---|---|---|---|---|---|
| 20 | `BankDataViewer` | Bank Data Viewer | Banka verisi görüntüleyici | `mdm.banks.read`, `mdm.bank-accounts.read`, `mdm.bank-account-purposes.read` | 3 |
| 21 | `BankDataManager` | Bank Data Manager | Banka verisi yöneticisi | (+20) + `mdm.banks.create`, `.update`, `.activate`, `.deactivate`, `.delete`, `.bulk-delete`, `mdm.bank-accounts.create`, `.update`, `.delete`, `.bulk-delete`, `mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.create`, `.update`, `.delete`, `.activate`, `.deactivate`. **Yok:** `mdm.bank-accounts.activate` / `.deactivate` (D47/2) | 19 |
| 22 | `BankAccountActivator` | Bank Account Activator | Banka hesabı etkinleştirici | (+20) + `mdm.bank-accounts.activate`, `mdm.bank-accounts.deactivate` | 5 |
| 23 | `ContractViewer` | Contract Viewer | Sözleşme görüntüleyici | `mdm.contracts.read`, `mdm.contracts.documents.read` (belge deposu izni yok — D56/2) | 2 |
| 24 | `ContractManager` | Contract Manager | Sözleşme yöneticisi | (+23) + `mdm.contracts.create`, `.update`, `.delete`, `.bulk-delete`, `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.submit-approval`, `mdm.contracts.terminate`, `mdm.contracts.archive`, `mdm.contracts.value.read`, `mdm.contracts.value.write` (D49/2), `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`. **Yok:** `mdm.contracts.record-signature` (D47/5) | 15 |
| 25 | `ContractSignatureRecorder` | Contract Signature Recorder | Sözleşme imza kaydedici | `mdm.contracts.record-signature`, `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create` | 4 |
| 26 | `ContractVersionApprover` | Contract Version Approver | Sözleşme onaylayıcı | `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve` (**D69**; belge deposu izni yok — D56/2; açık atama değil). **Yok:** `mdm.contracts.submit-approval` ve yazma anahtarları | 3 |

**Bağımlı anahtarlar (başka modülün; kayıt o sahipte — K-4):** `mdm.legal-entities.read` (MOD-0220): 6–19 arasındaki
hazırlayan / operatör / karar veren / serbest bırakan rolleri, AP / AR operasyon yöneticileri, AR 2. kademe ve Hazine
yöneticisi (görüntüleyicilerde yok); `platform.workflow.instances.start` (MOD-0023): 6, 11, 13, 14, 16, 18, 24;
`platform.workflow.tasks.approve` / `.reject`: 7, 8, 12, 15, 17, 19 — 26 (Sözleşme onaylayıcı) için D69 yazmadı, açık (K-4 / K-A4);
`platform.workflow.tasks.request-info`: 7, 8, 12.

### 14.3 DL-5 Auth uçları ve izinleri

| Uç (öneri; kök yol K-W4) | Politika | İzin |
|---|---|---|
| `POST /api/roles/sod-waiver-assignments` | `[Authorize]` | K-W1 (öneri `auth.payment-sod-waivers.assign`, yalnız açık atama) |
| `POST /api/roles/sod-waiver-assignments/{id}/revoke` | `[Authorize]` | K-W1 ile aynı |
| `GET /api/roles/sod-waiver-assignments?paymentBatchId=` | `[Authorize]` | K-W1 (öneri `auth.payment-sod-waivers.read`) — K1 kullanım incelemesi |
| `GET /internal/sod-waiver-assignments/payment-batches/{batchId}[?granteeUserId=]` | servis kimliği (K-W3) | yalnız FinanceTreasuryService |

### 14.4 DL-6 iç uç (D66/1)

| Uç (öneri; ad K-A1) | Politika | İzin |
|---|---|---|
| `GET /internal/permission-candidates?permissionKey={key}[&paymentBatchId={id}]` | servis kimliği (K-A1; K-W3 ile aynı mekanizma) | yalnız FinanceTreasuryService ve MdmService; kiracı kullanıcı izni yok (iç uç) |

## 15. Gateway / API Routing Decision

- DL-1…DL-4: Gateway değişikliği **gereksiz**; uç ya da yöntem eklenmez.
- DL-5: kiracı uçları `/api/roles/{everything}` catch-all altında kalırsa (`gateway/Diten.ApiGateway/ocelot.json:2232`,
  aşağı akış Auth 5056) Gateway **değişmez**; ayrı kök yol seçilirse `integration-agent` görevi (OPTIONS dahil) — K-W4. İç uç
  Gateway'den geçmez (servisten servise).
- DL-6: iç uç, Gateway'den geçmez; Gateway **değişmez**.
- Ön yüz bu uçları çağırmaz (ekran yok).

## 16. Acceptance Criteria

**DL-1 — finans onboarding'i (76 anahtar, 19 rol, 5 açık atama, iki SoD ayar anahtarı):**

- [ ] AC-1 (zorunlu) — `ExplicitGrantOnlyPermissions.Keys` beş finans anahtarını içerir (`waive-sod` ve dört `read-tenant-wide`);
  test: SuperAdmin tam katalog yolu, modül hakkı eşitlemesi ve varsayılan rol seçimi bu beş anahtardan hiçbirini vermez.
  Sabotaj: anahtar listeden çıkarılınca test kırmızıya döner (üç yolun her biri ayrı).
- [ ] AC-2 (zorunlu) — Modül hakkı olmayan kiracıda varsayılan Viewer ve Admin hiçbir `finance-treasury.*` anahtarı tutmaz
  (`DefaultRolePermissionTemplate.SelectFor` 76 anahtarlı katalogla). Sabotaj: 11 `read` eylemli finans anahtarından biri
  `EntitlementOnlyViewerPermissions`'tan çıkarılınca test kırmızı.
- [ ] AC-3 (zorunlu) — Etkin hakla 19 finans rolü Bölüm 14.2'deki kümeyi birebir alır (rol başına küme eşitliği, sayılar
  4/2/2/2/10/12/8/9/6/17/10/6/15/10/5/11/6/11/6); kullanıcı-rol satırı sıfır. Sabotaj: profilde bir role bir anahtar eklenince
  ya da çıkarılınca o rolün testi kırmızı.
- [ ] AC-4 (zorunlu) — Görev ayrılığı yapı testi: hiçbir rol aynı nesne türünde hazırlama / oluşturma / gönderme eylemini onay /
  ret eylemiyle birlikte taşımaz (ödeme partisi, AP faturası, AR düzeltmesi, nakit pozisyonu); `TreasuryManager` approve,
  reject, request-evidence, release, tier2-approve, waive-sod, publish ve AP / AR okuması taşımaz. Sabotaj: `TreasuryManager`'a
  `payment-batches.approve` eklenince kırmızı.
- [ ] AC-5 (zorunlu) — Modül hakkıyla Admin ve Viewer K-2 kararının kümesini alır (öneri: boş). Sabotaj: genel kalan Treasury
  modül kodu için yeniden açılınca (Admin'e tüm anahtarlar) kırmızı.
- [ ] AC-6 (zorunlu) — Beyan kümesi tam değilse (bir anahtar eksik, bir yabancı anahtar, bilinmeyen eylem, büyük harf) hiçbir
  rol / hak yazılmaz ve hata eksik / fazla anahtarları adıyla verir (dört ayrı test).
- [ ] AC-7 (zorunlu) — 26 ayrılmış rol adından ilki, ortadaki ve sonuncusu (`ContractVersionApprover`, D69) için sistem dışı çakışma: sıfır rol, sıfır hak
  (parametreli test; FU22 yedi rol testinin deseni).
- [ ] AC-8 (zorunlu) — Hak düştü / kapandı / süresi doldu: eşleşen Treasury modül hakları bütün rollerden kalkar; `Manual`,
  `System`, başka modül ve başka kiracı satırları kalır; çapraz modüllü rollerde (`TreasuryManager`,
  `TreasuryConsolidatedViewer`) yalnız düşen modülün satırları kalkar.
- [ ] AC-9 (zorunlu) — Belirsiz hak durumu ne verir ne geri alır; tekrar oynatma idempotenttir; A / B kiracı yalıtımı.
- [ ] AC-10 (zorunlu) — `finance-treasury.payment-sod-settings.read` / `.update` yalnız `TreasuryManager`'da; açık atama
  listesinde değil (D57/4).
- [ ] AC-11 (zorunlu) — Gerçek Mongo testi (`diten_auth_treasury_itest`, `MONGO_TEST_URI`, 27017 değil): rol / hak kardinalitesi,
  tekrar oynatma, geri al / geri yükle, kaynak koruma, kiracı yalıtımı, sıfır otomatik `userRoles`; sahte / bellek içi yok, atlanmaz.
- [ ] AC-12 (zorunlu) — FU20 ABB (`1/1/3/5/3/2`), FU22, BL-592 ve ürün Admin / Viewer matrisleri değişmez (gerileme koşusu, sayılar raporda).

**DL-2 — banka onboarding'i (MOD-0289: 23 anahtar, 3 rol, 2 açık atama):**

- [ ] AC-13 (zorunlu) — `mdm.bank-accounts.read-tenant-wide` ve `mdm.bank-accounts.read-unmasked` açık atama listesinde; AC-1'in
  üç yol testi bu iki anahtar için. Sabotaj AC-1 gibi.
- [ ] AC-14 (zorunlu) — Roller 20–22 Bölüm 14.2'deki kümeyi birebir alır (3/19/5); `BankDataManager`'da
  `mdm.bank-accounts.activate` / `.deactivate` yok (D47/2). Sabotaj: `BankDataManager`'a `activate` eklenince kırmızı.
- [ ] AC-15 (zorunlu) — Hiçbir finans rolü (1–19) `mdm.banks.read` ya da `mdm.bank-accounts.read` taşımaz (D47/4).
- [ ] AC-16 (zorunlu) — Dört `read` eylemli MOD-0289 anahtarı (`mdm.banks.read`, `mdm.bank-accounts.read`,
  `mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.read`) hak olmadan hiçbir Viewer'a gitmez. Sabotaj AC-2 gibi.

**DL-3 — sözleşme onboarding'i (MOD-0217: 18 anahtar, 4 rol, 1 açık atama — D69 dahil) — kodu MOD-0217 onayından sonra:**

- [ ] AC-17 (zorunlu) — `mdm.contracts.read-tenant-wide` açık atama listesinde; AC-1'in üç yol testi.
- [ ] AC-18 (zorunlu) — Roller 23–26 Bölüm 14.2'deki kümeyi birebir alır (2/15/4/3); `ContractManager`'da
  `mdm.contracts.record-signature` ve `mdm.contracts.versions.approve` yok (D47/5, D69). `mdm.contracts.versions.approve` yalnız `ContractVersionApprover`'dadır,
  açık atama listesinde değildir (D69: "açık atama değil"); `ContractVersionApprover` `mdm.contracts.submit-approval` ve yazma anahtarı
  taşımaz (aynı nesne türünde gönderme + onay yok — AC-4'ün yapı testi deseni, sözleşme sürümü için bu dilimde). Sabotaj: `ContractManager`'a
  `record-signature` ya da `mdm.contracts.versions.approve` eklenince, ya da `mdm.contracts.versions.approve` açık atama listesine konunca kırmızı.
- [ ] AC-19 (zorunlu) — 26 rolün hiçbiri (D69'un `ContractVersionApprover`'ı dahil) `platform.document-repository.read` ya da `.manage` taşımaz (D56/2); profil kümesinde
  `platform.document-repository.*` öneki yoktur. Sabotaj: `ContractViewer`'a `platform.document-repository.read` eklenince kırmızı.
- [ ] AC-20 (zorunlu) — Üç `read` eylemli MOD-0217 anahtarı (`mdm.contracts.read`, `.value.read`, `.documents.read`) hak olmadan
  hiçbir Viewer'a gitmez.
- [ ] AC-21 (zorunlu) — DL-3 kodu yalnız MOD-0217 `approved` / `ready-for-dev` ve onay satırı main'de iken birleşir (PR kontrolü).

**DL-4 — SEC-002 eki (D56/1):**

- [ ] AC-22 (zorunlu) — Platform doğrulayıcısı K-5 listesindeki (modül kodu, anahtar) çiftlerini kabul eder; tavan yeni sayıya
  eşittir; ürün çiftleri (38) aynen geçerlidir; listede olmayan anahtar ve yanlış modül kodlu çift reddedilir (gerçek
  doğrulayıcıyla test). Sabotaj: `AllowsPair`'deki modül kodu denetimi kaldırılınca yanlış modül kodlu çift testi kırmızı.
- [ ] AC-23 (zorunlu) — MDM istemcisi K-1b'deki MOD-0289 / MOD-0217 modül kodlarıyla istek gönderir; bilinmeyen modül kodu 400
  `LEGAL_ENTITY_SCOPE_REQUEST_INVALID`; yanıttaki `moduleCode` istenenden farklıysa kapalı-başarısız (mevcut davranış).
- [ ] AC-24 (zorunlu) — Uçtan uca (dev ortamı, iki şirket iki kullanıcı): MOD-0289 anahtarıyla kapsam sorusu yalnız kullanıcının
  şirketlerini döner; hata / boş liste → erişim yok (MOD-0289 AC-17'nin sağlayıcı yarısı).
- [ ] AC-25 (zorunlu) — `post-deploy-steps.md` adımı sırayı (Platform kodu → yapılandırma → MDM istemcisi), geri almayı ve kontrolü
  yazar; XMC-001 onay kaydı (Beste, MDM sahibi) PR açıklamasında.

**DL-5 — `waive-sod` parti bağlı atama (D54 + D64; MOD-0018 sahibinin kabul şartları):**

- [ ] AC-26 (zorunlu) — **Kiracı yalıtımı:** her atama `TenantId` taşır (JWT ya da güvenilir servis bağlamından; istekte yok);
  B kiracısının kullanıcısı için yaratma, B'nin atamasını geri alma ya da okuma → 404; B'nin partisi için sorgu boş. Sabotaj:
  depo sorgusundan `TenantId` süzgeci kaldırılınca A / B testi kırmızı.
- [ ] AC-27 (zorunlu) — **Hedef parti zorunlu:** hedef partisiz istek 400 `SOD_WAIVER_TARGET_BATCH_REQUIRED`, kayıt ve denetim
  yok; hedef partisiz bozuk satır (testte doğrudan yazılmış) sorguda dönmez; A partisine bağlı atama B partisi sorgusunda dönmez.
  Sözleşme testi (MOD-0126 portunun sahte tüketicisiyle): yanıt yok / bozuk / alan eksik / hedef uyuşmuyor / Auth ulaşılamıyor
  durumlarının her biri tüketicide 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` kararına gider (beş ayrı test). Sabotaj: sorgudan parti
  süzgeci kaldırılınca A→B testi kırmızı.
- [ ] AC-28 (zorunlu) — **En çok 24 saat:** süre = 24 saat kabul; 24 saat + 1 sn → 400 `SOD_WAIVER_DURATION_INVALID`; bitiş ≤
  başlangıç → 400; başlangıç 5 dk'dan eski → 400; bitişi geçmiş atama sorguda dönmez (sahte saat, `TimeProvider`). Sabotaj:
  doğrulayıcıdaki üst sınır kaldırılınca 24 saat + 1 sn testi kırmızı; sorgudaki `ExpiresAtUtc > şimdi` koşulu kaldırılınca
  süresi geçmiş testi kırmızı.
- [ ] AC-29 (zorunlu) — **Onaylayan ve gerekçe kodu zorunlu:** `ApprovedByUserId` = çağıran (sunucu türetir); istekte
  `approvedByUserId` → 400; çağıran = alıcı → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN`; K-W1 izni yok → 403; gerekçe kodu yok
  ya da listede değil → 400 `SOD_WAIVER_REASON_CODE_INVALID`. Her yanıt öğesi `approvedByUserId` ve `reasonCode` taşır. Sabotaj:
  kendine atama denetimi kaldırılınca kırmızı.
- [ ] AC-30 (zorunlu) — **Denetim kaydı (K2):** yaratma ve geri alma `auth.sod-waiver-assignment.created` / `.revoked` olayını
  iş yazımından **önce** `WriteOnceAsync` ile yazar; olay alıcı, hedef parti, başlangıç, bitiş, onaylayan ve gerekçe kodunu taşır;
  denetim yazımı başarısızsa atama yazılmaz ve komut başarısız döner. Sabotaj: denetim yazıcısı hata verecek şekilde
  ayarlandığında atama koleksiyonunda satır kalırsa test kırmızı.
- [ ] AC-31 (zorunlu) — Geri alınan atama sorguda dönmez; eşzamanlı iki geri alma → biri 409; fiziksel silme yok.
- [ ] AC-32 (zorunlu) — İç sorgu ucu yalnız K-W3'teki servis kimliğini kabul eder; kimliksiz / yanlış hedef kitleli çağrı 401 / 403;
  kiracı kullanıcı token'ı iç uca erişemez.
- [ ] AC-33 (zorunlu) — Gerçek Mongo testi (`diten_auth_sodwaiver_itest`, `MONGO_TEST_URI`, 27017 değil): iki dizin, A / B kiracı,
  A→B parti, süre dolumu, geri alma.
- [ ] AC-34 (isteğe bağlı) — Yanıt biçimi `docs/analysis/contracts/` altında sürümlü sözleşme dosyasıyla dondurulur ve MOD-0126
  sahte istemcisi o dosyaya karşı yazılır (K-W6).

**DL-6 — onaylayıcı aday sorgusu (KARAR D66/1; MOD-0018 sahibinin teyidi):**

- [ ] AC-35 (zorunlu) — **Kiracı ve iç uç:** A kiracısı bağlamında sorgu yalnız A'nın kullanıcılarını döner; B kiracısında aynı
  rol / izinle tanımlı kullanıcı A yanıtında yoktur. Uç yalnız K-A1'deki servis kimliğini kabul eder: kimliksiz / yanlış hedef
  kitleli çağrı 401 / 403, kiracı kullanıcı token'ı 403. Sabotaj: depo sorgusundan `TenantId` süzgeci kaldırılınca A / B testi
  kırmızıya döner.
- [ ] AC-36 (zorunlu) — **İzin sahipliği token kümesiyle aynı:** rolü üzerinden `finance-treasury.payment-batches.approve` tutan
  kullanıcı aday; rolden hak geri alınınca aday değil; kiracının o modül hakkı yokken (rol hakkı duruyor) aday değil;
  katalogda olmayan anahtar 400. Sabotaj: işleyici `TenantEffectivePermissionResolver`'ı atlayıp ham rol hakkını sayınca
  "modül hakkı yok" testi kırmızıya döner.
- [ ] AC-37 (zorunlu) — **Açık atama anahtarında yalnız açık atama:** `waive-sod` `GrantSource = Manual` satırından → aday;
  aynı anahtar `Module` ya da `System` kaynaklı satırdan (testte doğrudan yazılmış) → aday değil. Sabotaj: `GrantSource`
  süzgeci kaldırılınca `Module` satırı testi kırmızıya döner.
- [ ] AC-38 (zorunlu) — **Yalnız etkin kullanıcı:** aynı izinle pasif (`IsActive = false`), silinmiş, daveti bekleyen ve
  platform hesabı kullanıcılar aday değildir (dört ayrı test); K-A3 kararı (hizmet hesabı, kilit) kendi testiyle eklenir.
  Sabotaj: etkinlik süzgeci kaldırılınca pasif kullanıcı testi kırmızıya döner.
- [ ] AC-39 (zorunlu) — **Muafiyet biçimi (D56/3 ile birleşik):** `waive-sod` + A partisi sorgusu yalnız A'ya geçerli DL-5
  ataması olan kullanıcıları döner; B partisine bağlı, süresi geçmiş ya da geri alınmış atama dönmez; `paymentBatchId`'siz
  `waive-sod` sorgusu ve başka anahtarla `paymentBatchId` 400; DL-5 kaydı yokken (DL-5 kodu öncesi) sorgu kapalı-başarısız
  yanıt verir, boş liste vermez. Sabotaj: parti süzgeci kaldırılınca A→B testi kırmızıya döner. (DL-5 kodu gelmeden yalnız
  kapalı-başarısız dalı kanıtlanır; atama dalları DL-5 ile birlikte kapanır.)
- [ ] AC-40 (zorunlu) — **Şirket kapsamı (K-A2):** (a) seçilirse Auth şirket dışı kullanıcıyı döndürmez (sabotaj: kapsam
  süzgeci kaldırılınca kırmızı); (b) seçilirse sözleşme dosyası yanıtın şirket süzgeci taşımadığını yazar ve beş tüketici
  paketin aday AC'si şirket dışı izin sahibini dışlar (MOD-0126 AC-11, MOD-0119 AC-10, MOD-0120 AC-9, MOD-0124 AC-10, MOD-0217
  AC-33). Karar verilmeden DL-6 kabul edilmez.
- [ ] AC-41 (zorunlu) — Yanıt yalnız `permissionKey` ve `principalIds` taşır (ad, e-posta, rol adı yok — sözleşme testi K-A1
  dosyasına karşı); gerçek Mongo testi (`diten_auth_candidates_itest`, `MONGO_TEST_URI`, 27017 değil): `(TenantId,
  PermissionId)` dizini, A / B kiracı, rol hakkı geri al / geri yükle; sahte / bellek içi yok, atlanmaz.

## 17. Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Profil birim | 117 anahtar, 26 rol, rol başına küme eşitliği, modül kodu başına tam küme, eksik / fazla / bilinmeyen eylem reddi |
| Taban | Hak olmadan Admin / Viewer'da sıfır Treasury anahtarı; 18 `read` anahtarı dışlama listesinde |
| Açık atama | 8 anahtar × 3 otomatik yol (SuperAdmin, modül eşitlemesi, varsayılan rol) |
| SoD yapı | Aynı nesne türünde hazırlama + karar birlikte yok; `TreasuryManager` yasak listesi |
| Bileşik eşitleme | Çapraz modüllü rollerde kaynak modül ayrımı; ürün modülü profilleri (FU20 / FU22 / BL-592) etkilenmez |
| Ön denetim atomikliği | 26 rol adında parametreli çakışma → sıfır mutasyon |
| Hak döngüsü | Etkin / eksik / kapalı / süresi dolmuş / belirsiz; geri yükleme kesinliği |
| Yalıtım | A / B kiracı, `Manual` / `System` / başka modül koruma |
| DL-4 | Gerçek Platform doğrulayıcısı ve MDM istemcisi testleri; dev ortamı iki şirket iki kullanıcı |
| DL-5 | İşleyici, doğrulayıcı, denetim (hata enjeksiyonu), sözleşme, gerçek Mongo, iç uç kimlik doğrulama |
| DL-6 | İşleyici (kiracı, etkin izin hesabı, açık atama kaynağı, etkinlik, muafiyet biçimi), doğrulayıcı, sözleşme, gerçek Mongo, iç uç kimlik doğrulama; tüketici tarafı her paketin aday AC'sinde sahte istemciyle |
| Gerçek Mongo | Ayrı sahipli örnek, sabit veritabanı, `MONGO_TEST_URI`; uygulama 27017 yasak; sahte / bellek içi kanıt kalıcılık iddiasını kapatmaz |
| Derleme / takımlar | Odaklı Auth testleri, tam AuthService takımı, Auth API Release derlemesi; DL-4'te Platform ve MDM odaklı + tam takım; gerçek sayılar ve önceden bilinen kırmızılar (FU20 kaydındaki `UserLookupValidationContractTests` gibi) taban karşılaştırmasıyla |

Kural üretim koduyla ölçülür: testler `DefaultRolePermissionTemplate`, `ExplicitGrantOnlyPermissions`,
`EntitlementPermissionSyncService` ve yeni profilin kendisini çağırır; anahtar listesinin test içinde kopyası "beklenen" olarak
yalnız matristen (bu paketten) gelir ve üretim kümesiyle eşitlenir. Her sabotaj ayrı commit'te değil, test içinde değiştirilmiş
üretim girdisiyle ya da teslim kaydında "kaldır → kırmızı → geri koy → yeşil" kanıtıyla gösterilir.

## 18. Ready-for-dev Checklist

- [x] Golden Reference kararı yazıldı (`none`; DL-5 arka uç düzeni Golden Reference'a birebir).
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`).
- [x] 117 anahtar, 26 rol ve 8 açık atama matrisle mekanik uzlaştırıldı (Bölüm 19; D69 sonrası yeniden koşuldu).
- [ ] D69 main'de (D62/1) — DL-3 paket kapısı ({K} ve Sözleşme onaylayıcı).
- [ ] FU numarası verildi; dosya adı, `id`, `branch` ve kayıt satırı yazıldı; `verify_module_id.py --check-id` geçti (K-1).
- [ ] MOD-0018 sahibi incelemesi ve teyidi (Y-15/3, Y-16; D69'un anahtarı ve rolü dahil — D69).
- [ ] K-1b modül kodları (tüketici paket sahipleriyle) — DL-1/2/3 ve DL-4 için.
- [ ] K-2 Admin / Viewer varsayılanı, K-4 bağımlı anahtarlar, K-7 rol adları — DL-1/2/3 için.
- [ ] Bağımsız inceleme (CT-APR-02) ve CT-APR-01 onay satırı main'de.
- [ ] XMC-001 onayı: Beste (Platform dosyaları) ve MDM sahibi (MDM istemcisi); K-5 çift listesi — DL-4 için.
- [ ] MOD-0217 `approved` / `ready-for-dev` — DL-3 kodu için.
- [ ] D64 main'de (D62/1) — DL-5 paket kapısı.
- [ ] `auth-gunlugu` izinin K2 için kabulü ya da başka kabul edilmiş iz (CT defter kararı, K-W5) — DL-5 için.
- [ ] K-W1 (atama onay izni), K-W2 (token), K-W3 (servis kimliği), K-W4 (yol), K-W6 (sözleşme dosyası) — DL-5 için.
- [ ] D66 main'de (D62/1) — DL-6 paket kapısı.
- [ ] MOD-0018 sahibinin DL-6 teyidi (D66/1: "MOD-0018 sahibinin teyidi gerekir") — DL-6 için.
- [ ] K-A1 (uç adı, servis kimliği, sözleşme dosyası), K-A2 (şirket kapsamı), K-A3 (etkin kullanıcı tanımının kalanı), K-A4
  (bağımlı iş akışı anahtarı) — DL-6 için.
- [ ] Ekran yok, tur yok, sayfa yardımı yok (UXH-001: sayfa sahiplenilmiyor).

## 19. Implementation Notes

**Matris uzlaştırması (2026-10-28, mekanik; D69 sonrası yeniden koşuldu).** Karar dosyasının "(4) Kapsama tablosu" satırları
ayrıştırıldı: 121 satır = 117 modül anahtarı + 4 bağımlı satır (`platform.workflow.tasks.approve` / `.reject` tek satır olduğundan
bağımlı anahtar sayısı 5); yinelenen yok. Modül dağılımı MOD-0126 22, MOD-0119 19, MOD-0120 15, MOD-0124 20, MOD-0289 23, MOD-0217 18
— karar dosyasının sayım paragrafıyla eşit. 26 rol (finans 19, ana veri 7), "(2) / (3)" tablolarından miras kuralıyla genişletildi:
rollerin birleşimi 109 anahtar, açık atama 8, kesişim boş, birleşim 117 = tablo; eksik ve fazla yok; rol başına sayılar Bölüm 14.2
ile aynı (yeni rol 26: 3). D69 öncesi aynı betik 120 satır / 116 / 108 / 25 rol verdi. Altı paketin metninde tam yazılmış `finance-treasury.*` / `mdm.*`
anahtarları tablonun dışında anahtar içermiyor (fazla görünenler olay adlarıdır: `…payment-batch.created` vb.; MOD-0217'deki
`mdm.contracts.documents` bir önek anması). MOD-0289'un üç amaç anahtarı (`mdm.bank-account-purposes.update` / `.delete` /
`.deactivate`) pakette yalnız kısaltılmış (`.update`) biçimde geçiyor — fark değil. Y-7'deki "113 anahtar" D47/D48 dönemine
aittir; D49/2 (+1 `mdm.contracts.value.write`), D57/4 (+2 SoD ayar anahtarı) ve D69 (+1 `mdm.contracts.versions.approve`) ile 117.

**Kod gerçeği (ölçüm).** `ExplicitGrantOnlyPermissions` bugün 10 anahtar taşır (`:89-94`), hiçbiri Treasury'nin değil. Viewer
`read` kuralı ve dışlama listesi `DefaultRolePermissionTemplate.cs:120-168, 207-213`; genel modül seçimi
`EntitlementPermissionSyncService.cs:572-580`; mevcut özel profiller ABB, FU22 ve ürün kimliği (`:96-110`, `:582-625`). Platform
güvenilir kapsam doğrulayıcısı modül kodunu ve izin listesini sabitler (`TrustedLegalEntityScopeCredentialAuthenticator.cs:35-56,
97-115`); MDM istemcisi aynı kodu sabitler (`PlatformTrustedLegalEntityScopeProviderClient.cs:18, 170`). Auth'ta süreli ya da
nesneye bağlı atama yapısı yoktur (`UserRole`, `RolePermission` süresiz); DL-5 yeni kayıt türüdür. Auth denetiminde önce-yaz
kalıbı vardır (`IAuthAuditService.WriteOnceAsync`), ama `auth-gunlugu` izi defterde `aday`dır.

**Neden ayrı FU.** FU16–FU22 ürün modülüne aittir; Treasury altı yeni modül kodu, 26 rol, çapraz modüllü rol ve süreli parti
bağlı atama getirir. Önceki FU'lara katmak tamamlanmış kapsamları genişletir.

**İlgili kayıtlı FU.** MOD-0018-FU11 "Temporary Access Pipeline Binding" (`planned`) süreli erişimin doğal evidir; K-W2 kararı
onunla ilişkilendirilir.

## 20. Follow-up Items

- `BANK-ACCOUNT-LOOKUP` 1.0.0 bir finans akışında okunmaya başlayınca okuyan rol yeniden ölçülür (D47/4 bölüm 5) — bu pakete revizyon.
- `PartiallyApproved` / çok adımlı onay (D62/2) gelince ek onay rolleri ayrı revizyon.
- Muafiyet atama ekranı (Rol ekranı ya da Hazine ekranı) ve 5 iş günlük K1 inceleme raporu ayrı paket.
- PKS-001 §4 ad alanı tablosuna `finance-treasury.*` satırı (korumalı yol, ayrı onay).
- Local Development / Production hak eşitlemesi, rol üyelikleri, token yenileme ve izin / ret duman testi ayrı operasyon kapısı.

## 21. Audited Events

Servisin denetim altyapısı bugün: `auth-kullanici-iletimi` (yol b, en iyi çaba) kabul edilmiş; `auth-gunlugu` ve
`auth-rbac-gunlugu` **`aday`** (`tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`). DL-1…DL-4 ve DL-6 yeni yazma komutu
eklemez (DL-6 yalnız okuma sorgusudur) (mevcut hak eşitlemesi yolunu kullanır; durumu defterde olduğu gibi kalır, yeni K2 borcu yazılmaz). DL-5'in iki komutu
K2 sınıfıdır (yetki değiştirir).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreatePaymentSodWaiverAssignmentCommand | auth.sod-waiver-assignment.created | PaymentSodWaiverAssignment | c — `auth-gunlugu` önce-yaz (`IAuthAuditService.WriteOnceAsync`); iz bugün `aday` → **engel, K-W5** | AfterState: GranteeUserId, TargetPaymentBatchId, StartsAtUtc, ExpiresAtUtc, ApprovedByUserId, ReasonCode | — |
| RevokePaymentSodWaiverAssignmentCommand | auth.sod-waiver-assignment.revoked | PaymentSodWaiverAssignment | c — aynı (engel, K-W5) | Before/After: RevokedAtUtc, RevokedByUserId (+ atamanın kimliği ve hedef partisi) | — |

- **K2 (kapalı-başarısız):** iki komut da denetim kaydını iş yazımından önce yazar; yazılamazsa komut başarısız olur, atama
  yazılmaz (AC-30). "En iyi çaba" yoktur. İz kabul edilmeden komut yazılamaz (audit-trail-standard §4.4: kabul edilmiş K2 yolu
  yoksa Control Tower'a yazılır) — bu yüzden DL-5'in kapısında.
- **Kayda yazılmayan:** token, istek başlıkları, kullanıcının kişi verisi (yalnız kimlikler).
- **Okuma denetimi:** iç sorgu, kiracı listesi ve DL-6 aday sorgusu okumadır; ayrıca denetlenmez (muafiyetin **kullanımı** MOD-0126'nın K2
  olayıdır, `…approved-with-sod-waiver`).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | finans onboarding'i — 76 `finance-treasury.*` anahtarı, 19 rol, 5 açık atama, iki SoD ayar anahtarı (a, b, c, f); AC-1…AC-12; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 1–19 | Module Summary, Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria |
| DL-2 | banka onboarding'i — MOD-0289: 23 anahtar, 3 rol, 2 açık atama; AC-13…AC-16; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 20–22 | Repo Scope, Runtime Constraints, Authorization Convention, Acceptance Criteria |
| DL-3 | sözleşme onboarding'i — MOD-0217: 18 anahtar, 4 rol, 1 açık atama (D69 dahil); **koşullu: MOD-0217 onayından sonra** (Y-6, Y-10/1); AC-17…AC-21; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 23–26 | Repo Scope, Runtime Constraints, Authorization Convention, Acceptance Criteria |
| DL-4 | SEC-002 eki — Platform izin listesi + MDM istemcisinin modül kodu (e; D56/1); XMC-001 Beste + MDM sahibi; AC-22…AC-25; ayrıntı: §5.2, §8.3, §8.4 | Repo Scope, Runtime Constraints, Acceptance Criteria |
| DL-5 | `waive-sod` parti bağlı atama — kayıt, yazma ucu, "bu parti için geçerli atamalar" sorgusu, K2 denetim (d; D54 + D64); AC-26…AC-34; ayrıntı: §4 yeni kayıt, §5.3, §8.5, §14.3 | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Audited Events |
| DL-6 | onaylayıcı aday sorgusu — "bu izne sahip kullanıcılar" iç ucu (g; D66/1); D56/3 muafiyet aday listesi bunun `waive-sod` + parti biçimi; AC-35…AC-41; ayrıntı: §5.4, §8.6, §14.4 | Owned Objects, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria |

Tüketici kapıları (D53/2): MOD-0126 / 0119 / 0120 / 0124 DL-1 kabulü → bu paketin DL-1 kodu main'de; MOD-0289 DL-1 → DL-2
(+ DL-4, D56/1); MOD-0217 DL-1 → DL-3 (+ DL-4). MOD-0126 DL-2: DL-5 paket metni (muafiyet alanları, D56/3) dilim önkoşulu;
DL-5 kodu DL-2 kabulünü **beklemez** — muafiyet yolu 403 ile kapalıyken DL-2 kabul edilir, muafiyet DL-5 main'e girince ayrı
kabul edilir (**KARAR D66/3**, D56/3'teki "DL-5 kodu main'de = DL-2 kabul kapısı" kuralının yerine). Aday sorgusu (D66/1;
D53 / D56/3 deseni): DL-6 paket metni main'de = tüketicinin onay diliminin önkoşulu, DL-6 kodu main'de = o dilimin kabul
kapısı — MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3. DL-6, DL-1'i beklemez; DL-5'e yalnız
`waive-sod` biçimi için dayanır (o biçim DL-5'e kadar kapalı-başarısız).

## Açık kararlar

| # | Konu | Seçenekler | CT önerisi | Sahip | Son tarih |
|---|---|---|---|---|---|
| K-1 | FU numarası | FU27 (doğrulayıcı `OK`) · Auth kulesinin vereceği numara · arşivdeki FU23–FU26'nın önce kayda alınması | FU27; arşiv FU23–FU26 kayıt durumu ayrıca netleşsin | MOD-0018 sahibi (Ali) | TBD |
| K-1b | Treasury modül kodları (manifest `ModuleCode`) | paket başına bir kod · dört finans paketine tek kod + MDM'de paket başına | paket başına bir kod (kiracı modül bazında alır); değerler tüketici paketlerin manifest adımında | MOD-0126 DL-1 yazarı (finans), MDM sahibi (MOD-0289 / MOD-0217), MOD-0018 sahibi teyidi | DL-1 başlamadan |
| K-2 | Modül hakkıyla Admin / Viewer Treasury varsayılanı | hiçbiri · yalnız görüntüleyici kümeleri · FU20 gibi `read` | hiçbiri (matris 117 anahtarın hepsini yerleştirdi) | MOD-0018 sahibi + K1 | DL-1 başlamadan |
| K-3 | Varsayılan kiracı SuperAdmin tam katalog yolu Treasury anahtarlarını alsın mı | bugünkü davranış (açık atama dışı hepsi) · Treasury'yi dışla | bugünkü davranış (FU20 / FU22 deseni); varsayılan kiracı iş kiracısı değil | MOD-0018 sahibi | TBD |
| K-4 | Bağımlı 5 anahtar rollere nasıl ulaşır (D69'un Sözleşme onaylayıcısının `platform.workflow.tasks.approve` / `.reject` alıp almayacağı dahil — D69 yazmadı) | (a) ilgili modülün kendi yolu, rolde yok (b) Treasury manifestinde çapraz modül bağımlılığı olarak beyan, profil verir (ürün kimliği `DeclaredCrossModuleDependencyKeys` deseni) (c) elle atama | (b) — ama `platform.workflow.*` tohum sahipli anahtarlardır (`SeedOwnedTenantScopeKeys`, FU24/25); kapsam / sınıf çatışması ölçülmeli | MOD-0018 sahibi + MOD-0023 / MOD-0220 sahipleri | DL-1 başlamadan |
| K-5 | DL-4'te Platform izin listesine girecek (modül kodu, anahtar) çiftleri | MOD-0289 + MOD-0217'nin şirket kapsamı soran anahtarları (`read-tenant-wide` hariç) | hesap ve sözleşme anahtarları; banka / amaç (referans veri) kapsam sorar mı MOD-0289 sahibi söyler | MOD-0289 / MOD-0217 sahibi; Beste (Platform) | DL-4 başlamadan |
| K-6 | SEC-002 eki finans tüketicisini (FinanceTreasuryService → Platform kapsam ucu) de kapsar mı | evet (ikinci kimlik bilgisi bağlaması, Bölüm 5.2 koşullu dosyalar) · hayır (dört finans paketinin O-19 / O-22 / O-14 / O-23 işi) | hayır bu ekte (D56/1 yalnız MDM'yi adlandırır); ayrı karar | K1 + Beste | TBD |
| K-7 | Rol adları ve Türkçe görünen ad | `RoleName` İngilizce PascalCase + `DisplayName` İngilizce (mevcut desen, tek dil) · tr ad için rol ekranında kod → kaynak eşlemesi | CT önerisi Bölüm 14.2; tr adın ekranda nasıl çıktığı ayrı karar — not: Rol ekranı kiracıya görünür ve çekirdek kural kiracı ekranı için 7 dil ister | MOD-0018 sahibi | DL-1 başlamadan |
| K-W1 | Atamayı onaylama izni | yeni Auth anahtarı (öneri `auth.payment-sod-waivers.assign`, `.read`; yalnız açık atama) · başka yol | yeni anahtar, yalnız açık atama, Hazine yöneticisi kişilerine / K1'e elle | MOD-0018 sahibi + K1 | DL-5 başlamadan |
| K-W2 | `waive-sod` izninin token'da süreli olması | (a) izin açık atamayla süresiz, kullanım ayrıca geçerli atama ister (b) atama izni token'a koyar ve bitişte düşürür (token üretimi değişir; FU11) | (a) — token üretimine dokunmaz; MOD-0126 her kullanımda atamayı okur | MOD-0018 sahibi | DL-5 başlamadan |
| K-W3 | İç sorgu ucunun kimlik doğrulaması | servis kimliği token'ı (MOD-0033-FU02, `InternalServiceIdentityTokensController`) · `X-Internal-Api-Key` (`InternalRolesController` deseni) | servis kimliği token'ı, hedef kitle yalnız bu uç | MOD-0018 sahibi | DL-5 başlamadan |
| K-W4 | Kiracı uçlarının kök yolu | `/api/roles/sod-waiver-assignments` (Gateway değişmez) · ayrı kök (`integration-agent`) | `/api/roles/…` | MOD-0018 sahibi | DL-5 başlamadan |
| K-W5 | K2 denetim izi | `auth-gunlugu` izinin kabulü (okuyan uç ya da CT defter kararı) · merkezi iletimin kapalı-başarısız yolu | `auth-gunlugu` + önce-yaz; CT defter kararı | Treasury CT + MOD-0018 sahibi (defter) | DL-5 başlamadan |
| K-W6 | Sorgu yanıtının sözleşme dosyası | `docs/analysis/contracts/` altında sürümlü dosya · yalnız pakette | sürümlü dosya (MOD-0126 sahte istemcisi ona karşı) | MOD-0018 sahibi + MOD-0126 yazarı | DL-5 başlamadan |
| K-A1 | DL-6 iç ucunun adı, kimliği ve sözleşme dosyası | uç `GET /internal/permission-candidates` · başka ad; kimlik K-W3 ile aynı (servis kimliği token'ı) · `X-Internal-Api-Key` (`InternalRolesController` deseni); sözleşme dosyası `docs/analysis/contracts/` altında sürümlü · yalnız pakette | `/internal/permission-candidates`; K-W3 ile aynı servis kimliği, izinli çağıranlar FinanceTreasuryService ve MdmService; sürümlü dosya (hata kodları dahil) | MOD-0018 sahibi + tüketici yazarları | DL-6 başlamadan |
| K-A2 | Şirket kapsamı nerede uygulanır | (a) Auth sorguya `legalEntityId` alır ve Platform kapsam çözümünü sorar (Auth → Platform yeni bağımlılık; Platform tarafı XMC-001 Beste) · (b) Auth kiracı tabanını verir, tüketici kendi kapsam portuyla süzer | (b) — Auth'ta kullanıcı → şirket verisi yok; tüketicilerin kapsam portu zaten DL-1 kabul kapısında (D51/2) | K1 + MOD-0018 sahibi (+ Beste (a) ise) | DL-6 başlamadan |
| K-A3 | "Etkin kullanıcı" tanımının kalanı | hizmet hesabı (`AccountKind = Service`) aday mı · geçici kilitli (`LockoutEnd` gelecekte) aday mı | hizmet hesabı aday değil; geçici kilit aday kalır (kilit geçicidir, onay görevi bekleyebilir) | MOD-0018 sahibi | DL-6 başlamadan |
| K-A4 | Bağımlı `platform.workflow.tasks.approve` aday şartı mı | (a) yalnız modül onay anahtarı · (b) modül anahtarı + bağımlı MOD-0023 anahtarı birlikte (sorgu iki anahtar alır) | K-4 kararına bağlı: K-4 (b) ise rollerde birlikte bulunur, (a) yeter; K-4 (c) ise (b) | MOD-0018 sahibi + MOD-0023 sahibi | DL-6 başlamadan |
