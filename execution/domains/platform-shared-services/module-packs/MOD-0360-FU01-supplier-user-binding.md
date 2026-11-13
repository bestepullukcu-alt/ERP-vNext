---
id: MOD-0360-FU01
name: Supplier User Binding
domain: platform-shared-services
service: Diten.AuthService
shell: tenant
golden_reference: slim
data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
owner: platform-shared-services / merkez (Beste) — DC-03 sahibi
approver: Ali (GMG-ERP) — Auth sahibi (MOD-0017 / MOD-0018); CT-APR-01 yüksek risk, paketi yazmamış onaylayıcı
risk: HIGH
branch: feature/pss/mod-0360-fu01-supplier-user-binding (henüz açılmadı)
started: 2026-11-13
target: TBD
form_field_count: 5
parent: MOD-0360
consumer_module: MOD-0148
decision_gate: DC-03 (MVP-6 plan v10.0 C4 / C7)
---

# MOD-0360-FU01 — Supplier User Binding (DC-03)

> **Taslak — kod başlatma izni değildir.** Bu paket kod, şema, izin kataloğu, rol, token, gateway rotası ya da dağıtım
> yetkisi vermez. Kod yalnız paket `approved` / `ready-for-dev` olduktan, CT-APR-02 bağımsız incelemesi ve CT-APR-01 onay
> satırı yazıldıktan ve Bölüm 0'daki S0 kapısı kapandıktan sonra, dilim sırasıyla (DL-1 → DL-4) yazılır. Auth ve Gateway
> dosyalarına dokunmak ayrıca XMC-001 izni ister (Bölüm 0.3).
>
> **Kimlik kapısı (DCP-002, 2026-11-13):** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0360-FU01
> --name "Supplier User Binding" --parent MOD-0360` → `OK  MOD-0360-FU01: proven against Blueprint/registry.` (çıkış 0).
> Blueprint SoR haritası "external identity bindings" sahipliğini MOD-0360 Tenant User & Identity Management'a verir
> (`execution/registries/module-id-registry.md` MOD-0360 satırı). Registry'ye `MOD-0360-FU01` satırının yazılması S0 işidir;
> bu paket registry'yi değiştirmez.
>
> **Sahip kararları (bağlayıcı):** `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` —
> M2 (kanonik `SUP-` kodu saklanır, değişince merkez düzeltir), kimlik MOD-0360-FU01, token süresi (erişim 15 dk sessiz
> yenileme, oturum en çok 8 saat sonra yeniden giriş + e-posta kodu), taban D1–D8 taslağı, kiracı modeli.

## 0. S0 — karar ve izin kapısı (kod dilimi değildir)

S0 kod üretmez; DL-1'in başlaması için kapanması gereken kararları ve izinleri toplar. Bağlayıcı kayıt
`docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` dosyasına eklenir.

### 0.1 Taban: D1–D8 (main dışı taslak) ve bu paketteki karşılığı

Kaynak: `git show origin/feature/sce/mod-0148-dc03-decision-prep-01:docs/records/decisions/2026-10/mvp6-supplier-dc03-owner-decision-01.md`
(`67a30c7f0`) ve aynı dalda `docs/roadmap/plans/mvp6-supplier-dc03-decision-prep-01/DECISION-DRAFT.md` /
`ACTOR-TYPE-SCAN.md`; ilk taslak `origin/docs/sce/dc03-supplier-actor-binding-draft-01` (`eaa7bdd06`). #172 birleştirilmeden
kapandığı için bu metinler main'de yoktur; S0'da sahibe yeniden sunulur.

| # | Taslak metni (özet) | Bu paketteki biçim | S0'da sahibe sorulan |
|---|---|---|---|
| D1 | Bağ Auth'ta: `SupplierActorBinding(UserId, TenantId, LegalEntityId, SupplierId, Status, Revision)` | Aynı; `SupplierId` yerine **kanonik `SUP-` kodu** (`SupplierCode`) — M2 (2026-11-13) | yeniden onay |
| D2 | Yeni `actor_type = supplier_user`; hesap `Human` kalır; token yalnız aktif bağ varken | `actor_type = supplier_user` aynı; **hesap türü ayırt edicisi SQ-1'e bağlı** (öneri: hesap düzeyinde ayırt edici, `Human` kalmaz) | SQ-1 |
| D3 | Token `supplier_id`, `legal_entity_id`, `binding_rev` taşır; her yazma Auth'tan güncel `binding_rev` sorar; Auth yoksa 503 | Aynı; S2S `binding_rev` ucu DL-2'de (AC-19); ucun audience'ı SQ-3 | yeniden onay + SQ-3 |
| D4 | Yazmada anında, okumada token ömrü içinde iptal; erişim token'ı 15 dk | 15 dk erişim + sessiz yenileme + **en çok 8 saat oturum**, sonra yeniden giriş + e-posta kodu (sahip, 2026-11-13) | yeniden onay (8 saat eki) |
| D5 | Kiracı + LE başına kişi başına en çok bir aktif bağ; çoklu bağda girişte seçim; token tek bağ | Aynı (tekil indeks + seçim bileti, AC-3, AC-17) | yeniden onay |
| D6 | Bağı kiracı Admin'i kurar/kaldırır (`auth.supplier-bindings.manage`); bağlama anında tedarikçi Procurement'ta `Active`, Admin'in token'ıyla okunur | Aynı; okuma MOD-0140 `GET /api/suppliers/{supplierId}/eligibility`, ölçüt `eligibleForTransactions = true` (MOD-0140 S-1); kod düzeltme komutu eklendi (M2) | yeniden onay |
| D7 | MOD-0148 Procurement'ı SupplyChain servis kimliğiyle çağırır; S2S kapsamı yalnız `validate` | MOD-0148'in işi, bu paketin kodu değil; **MOD-0140 S-4 ile çelişir** (S-4: `POST /validate` **ve** tekil `GET /{supplierId}/eligibility`) | SQ-4 |
| D8 | Hata önceliği 401 → 403 (aktör/bağ/rev) → 403 (tedarikçi Active değil) → 400 → 503; her ret sıfır yazma | Tedarikçi kapısı ve S2S ucu için aynı sıra (Bölüm 13); portal tarafı MOD-0148'de | yeniden onay |

D9 (inşa sahibi ve zamanı) taslakta boş bırakılmıştı; MVP-6 plan v10.0 C4 satırına göre Beste (merkez), MOD-0148
başladığında (`docs/roadmap/plans/mvp6-development-plan-v10.0.md:90`). MOD-0148 2026-10-22'de başladı (MOD-0148 §0.0).

### 0.2 DC-03 C4 onaylı beş madde (plan v10.0 :90, 2026-10-21) → bu paket

| C4 maddesi | Bu paketteki yeri |
|---|---|
| Ayrı portal alt alanı (yalnız host'a bağlı çerezler oturumları ayırır) | v1'de UI yok (MOD-0148 §0.0); tedarikçi kapısı ayrı API önekidir (`/api/supplier-auth/*`). Alt alan + çerez ayrımı portal ekranıyla gelir — Follow-up FU-B |
| Tedarikçi hesaplarında zorunlu iki adımlı doğrulama | DL-2: kiracı ayarından bağımsız zorunlu e-posta kodu (AC-11) |
| Bir kiracıda aynı e-postayla şirket + tedarikçi hesabı yok | Mevcut tekil indeks `{Email, TenantId}` (canlı kayıt) + kur komutunun ret dalı (AC-6) |
| Gateway izin listesi: `supplier_user` → `/api/supplier-performance/portal/*` + kendi oturum uçları | DL-3 (AC-21) |
| İnternete açılmadan önce `TrustedProxies`, gateway rate limit (MOD-0032), paylaşılan limiter deposu | **Kapsam dışı** — ayrı MOD-0032 FU (numarası ve sahibi TBD). v1 iç ağdadır (Bölüm 8) |

C7 (`docs/roadmap/plans/mvp6-development-plan-v10.0.md:93`): "MOD-0148 için aynı yol + bağ, `supplier_user` token'ı ve
gateway kuralı için ayrı Auth/Platform paketi" — bu paket o ayrı pakettir.

### 0.3 XMC-001 izinleri (kod öncesi)

| Yol | Sahip | Ne istenir |
|---|---|---|
| `services/Diten.AuthService/**` (yeni özellik klasörü + mevcut kapı dosyaları, Bölüm 5) | Ali — MOD-0017 / MOD-0018 sahibi | DL-1 ve DL-2 dosya listesine izin; `TenantDoorPlatformAccountRefusal` emsalinin tedarikçi hesabına genişletilmesi; `AssignRoleCommand` reddi; AccountKind oluşturma yolu bekçisi (`AccountKindCreationPathsGuardTests`) değişikliği |
| `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` | MOD-0032 (Gateway) sahibi / `integration-agent` | DL-3 izin listesi + F4 sertleştirmesi |
| `gateway/Diten.ApiGateway/ocelot.json` | yalnız `integration-agent` (AGENTS.md §4) | iki yeni rota (Bölüm 15) — ayrı görev |
| `tests/architecture/audit-ledger/Diten.AuthService.md` + `AuditTrailStandardTests.cs` | Control Tower (defter kararı) | yeni iz satırı (Bölüm 21) |
| `frontend/Diten.Web/**` (yalnız DL-4) | Web / Governance ekran sahibi | yeni ekran klasörü |

### 0.4 S0 kapanış ölçütü

S0, (1) D1–D8'in bu paketteki biçimiyle yeniden onayı karar dosyasına yazıldığında, (2) SQ-1, SQ-3, SQ-4, SQ-7 cevaplandığında,
(3) 0.3 tablosundaki Auth ve Gateway izinleri yazılı alındığında ve (4) Bölüm 21'deki denetim izi için Control Tower defter
kararı verildiğinde kapanır. SQ-2, SQ-8, SQ-9, SQ-10 DL-1'i bekletmez; ilgili dilimin başında kapanır (Bölüm 22).

## 1. Module Summary

MOD-0148 Supplier Portal'ın kullanıcısı bir tedarikçi kişisidir. Bu paket o kişinin kimliğini kurar:

- **Bağ (DL-1):** kiracı Admin'i, bir kişiyi kendi kiracısının bir tüzel kişiliğinde (LE) tek bir tedarikçiye bağlar.
  Bağ, bağlama anında MOD-0140'ın döndürdüğü **kanonik `SUP-` kodunu** saklar (M2). Kod değişirse merkez düzeltir.
- **Tedarikçi kapısı (DL-2):** ayrı giriş / yenileme / çıkış / `me`; her girişte zorunlu e-posta kodu; `supplier_user`
  erişim token'ı 15 dk, oturum en çok 8 saat; token yalnız `supplychain.supplier-portal.read` ve `.submit` taşır.
- **Gateway (DL-3):** `supplier_user` yalnız portal uçlarına ve kendi oturum uçlarına geçer; `actor_type`'ı olmayan token
  kiracı uçlarında 403 alır (F4 açığı kapanır).
- **Bağlama ekranı (DL-4, isteğe bağlı):** kiracı kabuğunda liste + kur / kaldır / kod düzelt, 7 dil.

**Kiracı modeli (sahip açıklaması, 2026-11-13):** tedarikçi kendi kiracısını açmaz; hizmet verdiği firmanın kiracısında
(ör. GMG) bir `supplier_user` hesabıdır. Başka bir kiracıda ayrı hesaptır — kiracı başına e-posta başına tek hesap.

**Kapsam v1:** yalnız iç ağ. İnternete açılma kapıları (TrustedProxies, MOD-0032 rate limit, paylaşılan limiter) bu
paketin dışındadır.

## 2. Ownership and Boundaries

**Kapsam içi**
- `SupplierActorBinding` kaydı (Auth, tenant-owned), üç yazma komutu, liste / tekil / geçmiş / sapma sorguları.
- Tedarikçi hesabının ayırt edicisi (SQ-1) ve bu hesabın kiracı kapısından, kiracı hesabının tedarikçi kapısından reddi.
- Tedarikçi kapısı uçları ve `supplier_user` token'ı; S2S `binding_rev` ucu (D3).
- Gateway `supplier_user` izin listesi ve F4 sertleştirmesi (DL-3; kod `integration-agent` / MOD-0032 sahibi iznine bağlı).
- Bağlama ekranı (DL-4, isteğe bağlı).

**Kapsam dışı**
- MOD-0148 portal uçları, `supplychain.supplier-portal.*` anahtarlarının tanımı, portalın `actor_type` / `binding_rev`
  denetimi, replay receipt (MOD-0148 §0.1) — MOD-0148'in işi.
- MOD-0140 uygunluk ucu ve servis token'ı (MOD-0140 DL-3, DL-7) — Cihan / Procurement.
- İnternete açılma: `TrustedProxies`, gateway rate limit, paylaşılan limiter deposu — ayrı MOD-0032 FU.
- Portal alt alanı, tedarikçi giriş ekranı, çerez oturumu — portal UI ile (Follow-up FU-B).
- Tedarikçinin self-servis "parolamı unuttum" akışı — Follow-up FU-C (v1'de Admin yeniden davet eder).
- Onboarding akışından otomatik bağ (D6 "sonraki sürüm") — Follow-up FU-D.
- Tedarikçi hesaplarının kiracı Kullanıcılar ekranındaki görünümü — MOD-0360 ekran işi, XMC-001 (Follow-up FU-E).

## 3. Owned Objects

- **Varlık:** `SupplierActorBinding` (koleksiyon `supplierActorBindings`).
- **Repository:** `ISupplierActorBindingRepository` + Mongo uygulaması (oturumlu yazma: iş yazımı + denetim sonuç satırı
  aynı işlemde — Bölüm 21).
- **Komutlar (DL-1):** `CreateSupplierActorBindingCommand` (kur; gerekirse tedarikçi hesabını açar ve davet gönderir),
  `RevokeSupplierActorBindingCommand` (kaldır), `CorrectSupplierActorBindingCodeCommand` (kod düzelt, M2).
- **Komutlar (DL-2):** `SupplierLoginCommand`, `VerifySupplierMfaCommand`, `ResendSupplierMfaCommand`,
  `SelectSupplierBindingCommand`, `RefreshSupplierTokenCommand`, `LogoutSupplierCommand`.
- **Sorgular:** `GetSupplierActorBindingListQuery`, `GetSupplierActorBindingByIdQuery`,
  `GetSupplierActorBindingHistoryQuery`, `GetSupplierActorBindingDriftQuery` (isteğe bağlı, AC-10),
  `GetSupplierSessionMeQuery`, `GetSupplierBindingRevisionQuery` (S2S, D3).
- **Dış okuma istemcisi:** `ISupplierEligibilityClient` (Auth → Gateway → Procurement `GET /api/suppliers/{supplierId}/eligibility`
  ve isteğe bağlı `POST /api/suppliers/validate`; çağıran Admin'in bearer token'ı ve `X-Legal-Entity-Id` = bağın LE'si iletilir — D6).
- **Denetim:** `ISupplierAccessAuditWriter` (önce-yaz, deneme + sonuç satırı) + `ISupplierAccessAuditReader` (yalnız okuma).
- **API uçları (Auth, 5056; Gateway 5000 üzerinden):**
  - `GET /api/users/supplier-bindings` · `GET /api/users/supplier-bindings/{id}` · `GET /api/users/supplier-bindings/{id}/history`
  - `POST /api/users/supplier-bindings` · `POST /api/users/supplier-bindings/{id}/revoke` ·
    `POST /api/users/supplier-bindings/{id}/correct-code` · `GET /api/users/supplier-bindings/drift` (isteğe bağlı)
  - `POST /api/supplier-auth/login` · `POST /api/supplier-auth/mfa/verify` · `POST /api/supplier-auth/mfa/resend` ·
    `POST /api/supplier-auth/select` · `POST /api/supplier-auth/refresh-token` · `POST /api/supplier-auth/logout` ·
    `GET /api/supplier-auth/me`
  - `GET /api/internal/supplier-bindings/{bindingId}/revision` (S2S; audience SQ-3)
- **İzinler (PKS-001):** `auth.supplier-bindings.manage` (üç yazma komutu; D6), `auth.supplier-bindings.read` (liste /
  tekil / geçmiş / sapma — **öneri**, SQ-6). `supplier_user` token'ına konan `supplychain.supplier-portal.read` ve `.submit`
  bu paketin anahtarı değildir (MOD-0148 §14); verilme yolu SQ-7.
- **Frontend rotası (DL-4):** `/Governance/SupplierBindings`.

## 4. Entity Fields

`SupplierActorBinding : EntityBase` (Auth; tenant-owned; `TenantId` sunucuda JWT'den çözülür, DTO'da yoktur).

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | Evet | JWT `tenant_id`; istemciden alınmaz |
| `LegalEntityId` | Guid | Evet | İstekte verilir; uygunluk cevabı 200 olmadan kayıt yazılmaz (Bölüm 12) |
| `UserId` | Guid | Evet | Tedarikçi hesabının `User.Id`'si (aynı kiracı) |
| `SupplierCode` | string (≤ 32) | Evet | **Uygunluk cevabının `supplierId` alanı** (kanonik `SUP-xxxxxxxx`); istekteki kod değil (M2) |
| `SupplierMasterId` | Guid? | Hayır | Uygunluk cevabının `supplierMasterId` alanı; bilgi amaçlı, karar alanı değil |
| `Status` | enum `Active` / `Revoked` | Evet | Kurulunca `Active`; kaldırılınca `Revoked` |
| `Revision` | int | Evet | 1'den başlar; kaldırma ve kod düzeltmede +1; token'daki `binding_rev`. Adı `Version` değildir (entity-base-template: `Version` eşzamanlılık alanıdır) |
| `BoundAt` / `BoundBy` | DateTime / Guid | Evet | Kuran Admin |
| `RevokedAt` / `RevokedBy` / `RevokeReason` | DateTime? / Guid? / string? (≤ 500) | Kaldırmada | Gerekçe zorunlu |
| `IsDeleted` / `DeletedAt` | bool / DateTime? | Evet | Kaldırma = soft delete (`IsDeleted = true`, `DeletedAt`) + `Status = Revoked` |

**İndeksler (mongo-indexing):**
- `ux_supplier_binding_tenant_le_user_active` — tekil `{TenantId, LegalEntityId, UserId}`, kısmi `{Status: Active, IsDeleted: false}` (D5).
- `ix_supplier_binding_tenant_user` — `{TenantId, UserId, Status}` (giriş ve yenilemede bağ okuması).
- `ix_supplier_binding_tenant_code` — `{TenantId, SupplierCode}` (sapma sorgusu ve kod düzeltme).

**Tedarikçi hesabı (mevcut `User` kaydı):** e-posta tekilliği mevcut indeksle sağlanır — `ux_users_email_tenant_active`,
`{Email, TenantId}` kısmi `IsDeleted = false` (`services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs:11-15`).
Ayırt edici alanın biçimi SQ-1'e bağlıdır; öneri `IsPlatformAccount` emsalidir (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/User.cs:50`).

**RCS-001:** bağ kullanıcıya görünen bir iş belgesi değildir (kişi ↔ tedarikçi eşlemesi); ekranda e-posta, LE ve `SUP-` kodu
görünür, ayrı kayıt kodu üretilmez. **Öneri** — SQ-6 ile sahibe sorulur.

## 5. Repo Scope

Yollar DL başında yeniden ölçülür; burada adı geçmeyen dosyaya dokunulmaz (XMC-001).

**DL-1 (Auth)**
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/SupplierActorBinding.cs`,
  `.../Domain/Enums/SupplierActorBindingStatus.cs`
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierActorBindings/` (Bölüm 10 yapısı)
- Yeni: `.../Application/Common/Interfaces/ISupplierActorBindingRepository.cs`, `ISupplierEligibilityClient.cs`,
  `ISupplierAccessAuditWriter.cs`, `ISupplierAccessAuditReader.cs`
- Yeni: `.../Persistence/Repositories/SupplierActorBindingRepository.cs`, `SupplierAccessAuditWriter.cs`, `SupplierAccessAuditReader.cs`
- Yeni: `.../Infrastructure/Services/SupplierEligibilityClient.cs`
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/SupplierActorBindingsController.cs` (`[Route("api/users/supplier-bindings")]`)
- Değişir: `.../Persistence/Configurations/MongoDbIndexConfigurations.cs` (üç indeks), `.../Persistence/DependencyInjection.cs`,
  `.../Infrastructure/DependencyInjection.cs`, `.../Domain/Entities/User.cs` + `.../Domain/Enums/AccountKind.cs` (SQ-1 cevabına göre)
- Değişir (XMC-001, Ali): `AssignRole` işleyicisi — tedarikçi hesabına rol reddi (AC-26)
- İzin kaydı: `auth.supplier-bindings.manage` / `.read` — Auth izin kataloğu tohumu (yer DL-1 başında ölçülür)
- Testler: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierBindings/` (yeni)
- Defter: `tests/architecture/audit-ledger/Diten.AuthService.md` (`## İzler` yeni satır),
  `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` (`AcceptedTrails` yeni üçlü),
  `tests/architecture/audit-ledger/CT-DECISIONS.md` (Control Tower satırı)

**DL-2 (Auth)**
- Yeni: `.../Application/Features/SupplierAuth/` (komutlar, sorgular, işleyiciler, doğrulayıcılar)
- Yeni: `.../Api/Controllers/SupplierAuthController.cs` (`[Route("api/supplier-auth")]`),
  `.../Api/Controllers/SupplierBindingRevisionController.cs` (`[Route("api/internal/supplier-bindings")]`)
- Değişir: `.../Infrastructure/Services/TokenService.cs` (`supplier_user` token üreticisi), `.../Infrastructure/Settings/JwtSettings.cs`
  (tedarikçi ömürleri), `.../Api/appsettings.json` (`JwtSettings` altına iki yeni anahtar; mevcut iki anahtar değişmez)
- Değişir (XMC-001, Ali): `LoginCommandHandler.cs`, `VerifyMfaCommandHandler.cs`, `RefreshTokenCommandHandler.cs`,
  `ForcedChangeTenantPasswordCommandHandler.cs`, `TenantDoorPlatformAccountRefusal.cs` — kiracı kapısının tedarikçi hesabını reddi;
  `AuthController.cs` (`me`, `refresh-token`, `logout` uçlarında `supplier_user` reddi); `MfaChallengeService.cs` (challenge'ın kapısı — SQ-2)

**DL-3 (Gateway — integration-agent / MOD-0032 sahibi)**
- Değişir: `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` (izin listesi + F4)
- Değişir (yalnız `integration-agent`): `gateway/Diten.ApiGateway/ocelot.json` (iki rota)
- Testler: gateway test projesi (yer DL-3 başında ölçülür)

**DL-4 (Web, isteğe bağlı)**
- Yeni: `frontend/Diten.Web/Views/Governance/SupplierBindings/` (Bölüm 11), `wwwroot/assets/js/Governance/SupplierBindings/`,
  `Resources/Views/Governance/SupplierBindings/SupplierBindingsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, MVC vekil denetleyici
  (`Controllers/SupplierBindingsController.cs`), sayfa yardımı kaydı.

## 6. Protected Paths

- `.antigravity/**` — kullanıcı onayı olmadan değişmez.
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız `integration-agent`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `Controllers/Archive/**`, `Views/Archive/**` — FROZEN.
- `services/Diten.SupplyChainService/**` (MOD-0148), `services/Diten.ProcurementService/**` (MOD-0140),
  `services/Diten.MdmService/**`, `services/Diten.Platform/**` — bu paket yalnız uçlarını çağırır, dosyalarına dokunmaz.
- `docs/analysis/contracts/supplier.openapi.yaml` — MOD-0140 sözleşmesi; bu paket yalnız okur.
- Auth'un bu paketin listesinde olmayan her dosyası (XMC-001).
- `services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json` içindeki `AccessTokenExpirationMinutes` /
  `RefreshTokenExpirationDays` değerleri — kiracı kullanıcılarının ömrü değişmez (D4).

## 7. Dependencies

| Bağımlılık | Durum (2026-11-13, HEAD `e8128c224`) | Etkilediği dilim |
|---|---|---|
| MOD-0140 DL-3 uygunluk ucu `GET /api/suppliers/{supplierId}/eligibility` main'de + Gateway üzerinden canlı kanıt (gerçek 200, gerçek 404 `UNKNOWN_SUPPLIER`, kabul edilmeyen token reddi) | main'de değil (SQ-9) | **DL-1 başlamaz** |
| Admin rolünde `procurement.suppliers.eligibility.read` (ya da `.read`) ve MOD-0140 S-2 ara çözümü `mdm.legal-entities.read` | rol yapılandırması; DL-1 başında ölçülür | DL-1 |
| Auth K2 denetim yolu: önce-yaz `IAuthAuditService.WriteOnceAsync` | var (`services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IAuthAuditService.cs:18`) | DL-1, DL-2 |
| Kabul edilmiş Auth denetim izi | `auth-gunlugu` `aday` (`tests/architecture/audit-ledger/Diten.AuthService.md`, `## İzler`); yeni iz için Control Tower defter kararı gerekir (Bölüm 21) | **DL-1 başlamaz** |
| Auth → Platform merkezi iletici (meta-denetim) | `IPlatformAuditForwarder` var; anahtar boşsa sessiz düşer — geçmiş ucu bunu 503 ile karşılar (MOD-0018-FU27 K-W5 emsali) | DL-1 |
| MOD-0148 izin anahtarları `supplychain.supplier-portal.read` / `.submit` Auth kataloğunda | MOD-0148 manifestiyle girer; DL-2 başında ölçülür | DL-2 |
| S2S `binding_rev` ucu için audience ve SupplyChain `service_name` | SQ-3 (MOD-0140 S-5 ile aynı sahip) | DL-2 (AC-19) |
| MOD-0140 DL-7 servis token'ı (MOD-0148'in uygunluk okuması) | Cihan; SQ-10 | bu paketi bekletmez; MOD-0148'i bekletir |
| `integration-agent` görevi (ocelot iki rota) | açılmadı | DL-3 |
| MOD-0032 FU (internete açılma kapıları) | numarası ve sahibi TBD | portalın internete açılması (bu paket değil) |

## 8. Runtime Constraints

- Servis: Auth 5056; dış erişim yalnız Gateway 5000 üzerinden (AGENTS.md §3). Web 5001 servislere doğrudan gitmez.
- Auth → Procurement çağrısı Gateway 5000 üzerinden `/api/suppliers/{everything}` rotasıyla (mevcut) yapılır; adres
  yapılandırmadan okunur, port koda yazılmaz.
- Kiracı izolasyonu: her sorgu ve komut `TenantId` (JWT) ile sınırlıdır; başka kiracının bağı 404.
- Soft delete: kaldırma `IsDeleted` + `DeletedAt`; kayıt fiziksel silinmez.
- Eşzamanlılık: kaldırma ve kod düzeltme `expectedRevision` taşır; uyuşmazlık 409, sessiz üzerine yazma yok.
- Token ömürleri: `supplier_user` erişim 15 dk, oturum (yenileme anahtarı) mutlak 8 saat — yeni `JwtSettings` anahtarları
  `SupplierAccessTokenExpirationMinutes = 15`, `SupplierSessionMaxHours = 8`. Kiracı ömürleri (`appsettings.json:49-50`,
  120 dk / 7 gün) değişmez.
- Oturum mutlaktır: kiracı yenilemesi her dönüşte ömrü kiracı ayarından yeniden başlatır
  (`services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs:246-249`);
  tedarikçi yenilemesi ilk oturumun bitişini taşır, uzatmaz.
- **Yalnız iç ağ (v1).** `TrustedProxies`, MOD-0032 rate limit ve paylaşılan limiter deposu olmadan tedarikçi kapısı ve
  portal internete açılmaz (C4). Bu kapıların inşası bu paketin dışındadır.
- Kişisel veri: tedarikçi kişisinin e-postası, adı ve oturum kayıtları kiracı dışından gelen kişisel veridir; denetim
  satırına parola, kod, token ve bilet yazılmaz (Bölüm 21).

## 9. Layout & Shell Contract

- `shell: tenant` (yalnız DL-4 ekranı; DL-1…DL-3 ekransızdır).
- `frontend/Diten.Web/Views/Governance/SupplierBindings/*.cshtml` dosyalarında **`Layout = "_LayoutTenantShell";` AÇIKÇA**
  yazılır; `_ViewStart.cshtml` varsayılanına güvenilmez. Emsal: `frontend/Diten.Web/Views/Governance/Users/Index.cshtml:7`.
- View klasörü: `Views/Governance/SupplierBindings/`; rota `/Governance/SupplierBindings`.
- Tedarikçi kişisinin göreceği ekran (portal) bu paketin değildir.

## 10. Backend File Convention

Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`):

```text
services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierActorBindings/
├── Commands/
│   ├── CreateSupplierActorBindingCommand.cs
│   ├── RevokeSupplierActorBindingCommand.cs
│   └── CorrectSupplierActorBindingCodeCommand.cs
├── Queries/
│   ├── GetSupplierActorBindingListQuery.cs
│   ├── GetSupplierActorBindingByIdQuery.cs
│   ├── GetSupplierActorBindingHistoryQuery.cs
│   └── GetSupplierActorBindingDriftQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateSupplierActorBindingHandler.cs
│   │   ├── RevokeSupplierActorBindingHandler.cs
│   │   └── CorrectSupplierActorBindingCodeHandler.cs
│   └── QueryHandlers/
│       ├── GetSupplierActorBindingListHandler.cs
│       ├── GetSupplierActorBindingByIdHandler.cs
│       ├── GetSupplierActorBindingHistoryHandler.cs
│       └── GetSupplierActorBindingDriftHandler.cs
├── Validators/
│   ├── CreateSupplierActorBindingValidator.cs
│   ├── RevokeSupplierActorBindingValidator.cs
│   └── CorrectSupplierActorBindingCodeValidator.cs
└── SupplierActorBindingsModels.cs

services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierAuth/
├── Commands/   SupplierLoginCommand, VerifySupplierMfaCommand, ResendSupplierMfaCommand,
│               SelectSupplierBindingCommand, RefreshSupplierTokenCommand, LogoutSupplierCommand
├── Queries/    GetSupplierSessionMeQuery, GetSupplierBindingRevisionQuery
├── Handlers/CommandHandlers/  {Verb}Supplier...Handler (6)
├── Handlers/QueryHandlers/    GetSupplierSessionMeHandler, GetSupplierBindingRevisionHandler
├── Validators/ SupplierLoginValidator, VerifySupplierMfaValidator, SelectSupplierBindingValidator
└── SupplierAuthModels.cs
```

- Komut / sorgu `sealed record`, işleyici `sealed class`; işleyici ve doğrulayıcı adında `Command` / `Query` eki YOK.
- `BulkDelete` yok: toplu kaldırma kapsam dışıdır (her kaldırma gerekçe ve revizyon ister).
- Denetleyiciler `CustomBaseController` + `Response<T>` (response-envelope).

## 11. Frontend File Contract

DL-4 (isteğe bağlı) — Slim seti, `golden_reference: slim`, `data_mode: server` (bir kiracının tedarikçi kişisi sayısı
tasarımda sınırlı değildir):

```text
frontend/Diten.Web/Views/Governance/SupplierBindings/
├── Index.cshtml                    (Layout = "_LayoutTenantShell" AÇIKÇA)
├── _Filter.cshtml                  (LE, durum, tedarikçi kodu, e-posta)
├── _DataTable.cshtml               (data-dt-standard="v2", data-dt-data-mode="server", skeleton)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml     (kur formu; ayrı "kod düzelt" ve "kaldır" panelleri aynı dosyada iki form)
├── _DetailsQuickView.cshtml        (bağ + geçmiş satırları)
└── SupplierBindingsIndex.cs

frontend/Diten.Web/wwwroot/assets/js/Governance/SupplierBindings/
├── index.js
└── index.l10n.js
```

- Kur formu kullanıcı alanları (form_field_count = 5): e-posta, ad, soyad, tüzel kişilik (LE), tedarikçi kodu.
- Lookup kararı: LE seçimi mevcut MDM LE okumasından (Web vekili + Gateway); tedarikçi seçimi MOD-0140 `GET /api/suppliers/eligible`
  (Admin'in kullanıcı izniyle, seçilen LE için). Sabit yedek liste YOK; tarayıcı JS servis portlarını çağırmaz.
- Tek mantıksal ekran; liste sayfası dokunma protokolü (`verify_datatable_page.py --area Governance --module SupplierBindings`) uygulanır.

## 12. Validation Rules

| Alan / koşul | Zorunlu | Kural | DB-level | Ön kontrol |
|---|---|---|---|---|
| `Email` (kur) | Evet | trim, küçük harf, e-posta biçimi, ≤ 256 | `ux_users_email_tenant_active` | aynı kiracıda hesap var mı; varsa tedarikçi hesabı mı (AC-6) |
| `FirstName` / `LastName` (kur, yeni hesapta) | Evet (yeni hesap) | trim, ≤ 100 | — | — |
| `LegalEntityId` | Evet | boş olmayan Guid | — | uygunluk cevabı 200 (Procurement LE'yi kiracıda çözer) |
| `SupplierCode` (istek) | Evet | trim, ≤ 64; kanonik ya da eski kod ya da uuid (MOD-0140 §3.1 kod çözümü) | — | uygunluk okuması |
| Saklanan `SupplierCode` | — | **cevabın `supplierId`'si**, `^SUP-` ile başlar; değilse 502/503 sınıfı ret, yazma yok | — | — |
| Uygunluk | Evet | `relationshipExists = true` ve `eligibleForTransactions = true` (MOD-0140 S-1) | — | — |
| Tekil aktif bağ | Evet | kiracı + LE + kullanıcı başına bir `Active` | `ux_supplier_binding_tenant_le_user_active` | var mı |
| `Reason` (kaldır, kod düzelt) | Evet | trim, 3–500 | — | — |
| `expectedRevision` (kaldır, kod düzelt) | Evet | pozitif tam sayı, kayıttakine eşit | koşullu güncelleme | — |
| Yeni kanonik kod (düzelt) | Evet | cevabın `supplierId`'si kayıttakinden farklı | — | — |
| Tedarikçi girişi | Evet | e-posta + parola; isteğe bağlı `legalEntityId` | — | aktif bağ ve tedarikçi hesabı (yoksa genel 401) |
| OTP | Evet | 6 hane, challenge tek kullanımlık, süresi dolmamış, **tedarikçi kapısına ait** | `ux_mfa_challenge_hash` | — |
| Seçim bileti | Evet (çoklu bağda) | tek kullanımlık, 5 dk, karması saklanır; seçilen LE kişinin aktif bağı | — | — |
| İstemcinin `supplierId` / LE taşıması (portal) | — | MOD-0148'in kuralı (D8: 400); bu paket token'a yalnız bağdan koyar | — | — |

## 13. Failure Path to Verify

Bağ yönetimi (DL-1):
- **Yinelenen aktif bağ** (aynı kiracı + LE + kullanıcı): 409 `SUPPLIER_BINDING_EXISTS`; eşzamanlı iki kurulumda biri kazanır,
  diğeri 409 (tekil indeks); sıfır ek yazma.
- **E-posta kiracı hesabına ait** (aynı kiracı, tedarikçi olmayan hesap): 409 `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT`; yazma yok.
- **Tedarikçi uygun değil** (`eligibleForTransactions = false`): 422 `SUPPLIER_NOT_ELIGIBLE`; yazma yok.
- **Tedarikçi bilinmiyor / LE'de ilişki yok** (Procurement 404): 422 `SUPPLIER_BINDING_SUPPLIER_UNKNOWN`; yazma yok.
- **Admin'in uygunluk okuma izni yok** (Procurement 403): 403 `SUPPLIER_BINDING_ELIGIBILITY_FORBIDDEN`; yazma yok.
- **Procurement erişilemez / 5xx / zaman aşımı / beklenmeyen gövde**: 503 `SUPPLIER_BINDING_ELIGIBILITY_UNAVAILABLE`; yazma yok.
- **Eksik alan**: 400 + doğrulayıcı mesajı; yazma yok.
- **Eşzamanlılık** (`expectedRevision` eski): 409 `SUPPLIER_BINDING_REVISION_CONFLICT`; sessiz üzerine yazma yok.
- **Kod değişmedi** (düzeltmede yeni kanonik = eski): 422 `SUPPLIER_BINDING_CODE_UNCHANGED`.
- **Yetkisiz aktör** (`auth.supplier-bindings.manage` yok): 403; yazma yok. Başka kiracının bağı: 404.
- **Denetim deneme satırı yazılamıyor**: 503 `SUPPLIER_BINDING_AUDIT_UNAVAILABLE`; iş verisi yazılmaz (K2).
- **İş yazımı ya da sonuç satırı düşer**: işlem geri alınır, komut başarısız; geçmişte deneme "sonuçlanmadı" görünür.

Tedarikçi kapısı (DL-2), D8 sırası:
- Token yok / geçersiz → 401.
- Tedarikçi kapısında: bilinmeyen e-posta, kiracı hesabı, platform hesabı, aktif bağı olmayan tedarikçi hesabı, yanlış parola,
  kilitli hesap → **aynı genel 401** (kod, gövde ve süre aynı; `SignInTiming` emsali, LoginCommandHandler.cs:103-114).
- Kiracı kapısında tedarikçi hesabı (giriş, MFA doğrulama, yenileme, zorunlu parola değişimi) → kiracı kapısının genel 401'i.
- `supplier_user` token'ıyla `/api/auth/me`, `/api/auth/refresh-token`, `/api/auth/logout`, `/api/tenant-auth/*` → 401/403, token
  verilmez.
- Yenilemede bağ kaldırılmış / silinmiş → 401 + oturum ailesi iptal.
- Oturum 8 saati aşmış → 401 `SUPPLIER_SESSION_EXPIRED`; yeniden giriş + e-posta kodu.
- Başka kapının challenge'ı ile doğrulama → 401.
- S2S `binding_rev` ucunda: servis token'ı yok / yanlış audience / listede olmayan `service_name` → 401; başka kiracının bağı → 404.
- Denetim satırı yazılamıyor → 503, token / oturum verilmez.

## 14. Authorization Convention

- **Bağ yönetimi:** `[Authorize]` + `[HasPermission("auth.supplier-bindings.manage")]` (kur, kaldır, kod düzelt);
  `[HasPermission("auth.supplier-bindings.read")]` (liste, tekil, geçmiş, sapma — öneri, SQ-6). Aktör `tenant_user`;
  `supplier_user` bu uçlarda 403. Uçlar `actor_type = tenant_user` dışını ayrıca reddeder (emsal
  `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/AuthController.cs:112`, sabit `:177`).
- **PKS-001:** `auth.supplier-bindings.manage` ve `.read` — 3 segment, küçük harf, çok kelime kebab-case; `auth` ad alanı
  Auth'undur. `manage` Tier 3 eylemdir ve bu pakette kaydedilir (tek anahtar üç komutu kapsar — D6 metni).
- **Tedarikçi kapısı:** `login`, `mfa/verify`, `mfa/resend`, `select`, `refresh-token` anonim (kendi kanıtıyla — parola,
  challenge, bilet, yenileme anahtarı); `logout` ve `me` yalnız `actor_type = supplier_user` token'ı. Emsal kapı ayrımı:
  `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs:617` (`IsPlatformActorToken`) ve
  `:717-721` (`NormalizeActorType`).
- **`supplier_user` token içeriği:** `sub`, `tenant_id`, `actor_type = supplier_user`, `supplier_id` (kanonik `SUP-`),
  `legal_entity_id`, `binding_id`, `binding_rev`, `amr = ["pwd","otp"]`, `jti`, `iat`, `nbf`, `exp` (`exp − nbf = 15 dk`);
  `permission` claim'leri **tam olarak** `supplychain.supplier-portal.read` ve `supplychain.supplier-portal.submit`; rol claim'i
  yok. Verilme yolu (sabit küme mi, kiracı rolü mü) SQ-7; öneri sabit küme (D2: kiracı rolü taşımaz).
- **S2S `binding_rev` ucu:** yalnız güvenilir servis token'ı (Auth'un kendi `ServiceClientIdentity` kaydı; audience ve
  `service_name` listesi SQ-3); kullanıcı token'ı bu uçta 401.
- **Kiracı kapısı:** tedarikçi hesabını hesap düzeyinde reddeder (`IsPlatformAccount` emsali,
  `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs:108-114`).
- **Rol ataması:** tedarikçi hesabına kiracı rolü atanamaz → 422 `SUPPLIER_ACCOUNT_ROLE_FORBIDDEN` (AC-26, XMC-001).

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gerekli** (DL-3); `ocelot.json` kısmı `integration-agent` görevidir, bu paket yazmaz.

- Bağ yönetimi `/api/users/supplier-bindings/**` altında kalır → mevcut `/api/users/{everything}` rotası kapsar
  (`gateway/Diten.ApiGateway/ocelot.json:2243-2251`, Auth 5056) → **rota değişikliği gerekmez**.
- Tedarikçi kapısı `/api/supplier-auth/{everything}` → Auth 5056: **yeni rota** (emsal `platform-auth` ve `tenant-auth`
  rotaları, `ocelot.json:2167-2194`), OPTIONS dahil.
- Portal `/api/supplier-performance/portal/{everything}` → SupplyChain 5066: **yeni rota**; bugün `ocelot.json`'da
  `supplier-performance` geçmez (2026-11-13 ölçümü).
- S2S `/api/internal/supplier-bindings/**` gateway'den **geçmez** (servisler arası iç çağrı); Gateway'e rota eklenmez.
- `TenantResolutionMiddleware`:
  - `supplier_user` için izin listesi: yalnız `/api/supplier-performance/portal/*` ve `/api/supplier-auth/*`; başka her yolda
    403 "Forbidden Actor". Bugün `tenant_user` dışı her aktör kiracı uçlarında 403 alır — portal dahil
    (`gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:229-235`), bu yüzden izin listesi zorunludur.
  - `/api/supplier-auth/login`, `/mfa/verify`, `/mfa/resend`, `/select`, `/refresh-token` kamu uçları listesine eklenir
    (emsal `IsPublicEndpoint`, `TenantResolutionMiddleware.cs:438-449`); kiracı çözümü kiracı kapısıyla aynı (host ya da `X-Tenant-Id`).
  - **F4 sertleştirmesi:** üç dal da `!string.IsNullOrWhiteSpace(actorType) &&` ile başlıyor — `actor_type` claim'i olmayan imzalı
    token kiracı kullanıcısı gibi geçer: kişiselleştirme `:146-150`, servis kararlı başlık çatışması `:185-190`, genel kiracı ucu
    `:229-235`. DL-3'te claim yoksa 403. **Bütün kiracı uçlarını etkiler** — regresyon testi zorunlu (AC-22).
  - `tenant_user` token'ı portal yoluna gateway'den geçer; ret MOD-0148'in bildirdiği kodlarla serviste verilir (MOD-0148 §0.0).

## 16. Acceptance Criteria

Her madde üretim koduyla ölçülür; testin kopyası değil, gerçek işleyici / uç / middleware sınanır. Her maddenin adlı
sabotajı vardır: üretim kodunda yazılan değişiklik testi kırmızıya çevirmelidir; çevirmiyorsa test bekçi sayılmaz.

**DL-1 — bağ kaydı**

- [ ] AC-1 (zorunlu) — Kur: Admin istekte eski kod ya da uuid verdiğinde kayıt, uygunluk cevabının `supplierId`'sini
  (kanonik `SUP-`) saklar; `Status = Active`, `Revision = 1`, `TenantId` JWT'den. Sabotaj: işleyicide saklanan değeri istekteki
  koda çevirmek → test kırmızı.
- [ ] AC-2 (zorunlu) — Uygunluk kapalı-başarısız: `eligibleForTransactions = false` → 422 `SUPPLIER_NOT_ELIGIBLE`; Procurement 404 →
  422 `SUPPLIER_BINDING_SUPPLIER_UNKNOWN`; 403 → 403 `SUPPLIER_BINDING_ELIGIBILITY_FORBIDDEN`; zaman aşımı / 5xx / bağlantı hatası /
  `supplierId` `SUP-` ile başlamayan gövde → 503; her dalda `supplierActorBindings` ve `users` koleksiyonunda sıfır yazma. Sabotaj:
  istemcide zaman aşımını "uygun" sonuca eşlemek ya da uygunluk çağrısını atlamak → test kırmızı.
- [ ] AC-3 (zorunlu) — Tekil aktif bağ: aynı kiracı + LE + kullanıcıya ikinci kur → 409 `SUPPLIER_BINDING_EXISTS`; eşzamanlı iki
  kurulumdan biri 409 alır (indeks). Aynı kişinin aynı kiracıda başka LE'de bağı kurulabilir (D5). Sabotaj: indeksi
  `MongoDbIndexConfigurations.cs`'ten çıkarmak ve ön kontrolü kaldırmak → eşzamanlılık testi kırmızı.
- [ ] AC-4 (zorunlu) — Kiracı izolasyonu: kiracı B token'ıyla kiracı A'nın bağı için tekil / geçmiş / kaldır / düzelt → 404; liste
  yalnız kendi kiracısını döner. Sabotaj: repository okumasından `TenantId` filtresini çıkarmak → test kırmızı.
- [ ] AC-5 (zorunlu) — Yetki: `auth.supplier-bindings.manage` olmadan üç komut 403, sıfır yazma; `.read` olmadan sorgular 403;
  `supplier_user` token'ı bu uçlarda 403. Sabotaj: denetleyicide `[HasPermission]` özniteliğini kaldırmak → test kırmızı.
- [ ] AC-6 (zorunlu) — Hesap: yeni e-posta → aynı kiracıda tedarikçi hesabı açılır (SQ-1 ayırt edicisiyle) ve davet bağlantısı
  gönderilir; e-posta aynı kiracıda tedarikçi olmayan bir hesaba aitse 409 `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT`, yazma yok;
  aynı e-posta başka kiracıda ayrı hesaptır. Sabotaj: tedarikçi olmayan hesabı bağlamaya izin vermek → test kırmızı.
- [ ] AC-7 (zorunlu) — Kaldır = soft delete: `IsDeleted = true`, `DeletedAt`, `Status = Revoked`, `Revision + 1`, gerekçe zorunlu;
  kayıt listeden düşer, geçmişi okunur; fiziksel silme yok. Sabotaj: repository'de `DeleteOneAsync` kullanmak → test kırmızı.
- [ ] AC-8 (zorunlu) — Kod düzelt (M2): yeni uygunluk okuması yapılır, cevabın kanonik kodu saklanır, `Revision + 1`, gerekçe
  zorunlu; `expectedRevision` eski → 409; kanonik kod aynı → 422 `SUPPLIER_BINDING_CODE_UNCHANGED`. Sabotaj: düzeltmede
  `Revision` artışını kaldırmak → test kırmızı.
- [ ] AC-9 (zorunlu) — Denetim K2: üç komutun her biri iş yazımından önce deneme satırı yazar; deneme yazılamazsa 503, iş verisi
  yok; iş yazımı ve sonuç satırı aynı Mongo işlemindedir; geçmiş ucu bağın satırlarını sırayla döner ve sonuçsuz denemeyi
  "sonuçlanmadı" gösterir; defterde yeni iz satırı, `PinnedCounts` değişmez. Sabotaj: yazıcının hatasını işleyicide yutmak ya da
  sonuç satırını işlem dışına almak → test kırmızı.
- [ ] AC-10 (isteğe bağlı) — Sapma sorgusu: aktif bağlardan, saklanan kodu MOD-0140'ın bugünkü kanonik cevabından farklı olanları
  listeler; Procurement erişilemezse 503 (boş liste değil). Sabotaj: hata durumunda boş liste dönmek → test kırmızı.
- [ ] AC-26 (zorunlu) — Tedarikçi hesabına kiracı rolü atanamaz: `AssignRole` → 422 `SUPPLIER_ACCOUNT_ROLE_FORBIDDEN`, yazma ve
  sürüm artışı yok. Sabotaj: işleyicideki tedarikçi hesabı denetimini kaldırmak → test kırmızı.

**DL-2 — tedarikçi kapısı**

- [ ] AC-11 (zorunlu) — Tedarikçi girişi kiracının `MfaRequired` ayarından bağımsız olarak her zaman e-posta kodu ister; doğru
  parola tek başına token vermez. Sabotaj: tedarikçi giriş işleyicisinde kiracı ayarına (`LoginCommandHandler.cs:151` deseni)
  bağlanmak → `MfaRequired = false` testi kırmızı.
- [ ] AC-12 (zorunlu) — Token: `actor_type = supplier_user`; `supplier_id`, `legal_entity_id`, `binding_id`, `binding_rev`,
  `tenant_id`, `amr` var; `exp − nbf = 900 sn`; `permission` claim kümesi tam olarak iki portal anahtarı; rol claim'i yok. Sabotaj:
  ömrü `AccessTokenExpirationMinutes`'ten (120) okumak ya da kiracı izinlerini eklemek → test kırmızı.
- [ ] AC-13 (zorunlu) — Oturum mutlak 8 saat: art arda yenilemeler ilk girişin bitişini aşmaz; 8 saatten sonra yenileme 401
  `SUPPLIER_SESSION_EXPIRED`. Sabotaj: yeni yenileme anahtarının bitişini `UtcNow + 8 sa` yapmak (kayan pencere) → test kırmızı.
- [ ] AC-14 (zorunlu) — Yenileme bağı yeniden okur: kaldırılmış bağ → 401 + oturum ailesi iptal; kod düzeltilmiş bağ → yeni token
  güncel `binding_rev` ve `supplier_id` taşır. Sabotaj: yenilemede bağ okumasını atlayıp eski claim'leri kopyalamak → test kırmızı.
- [ ] AC-15 (zorunlu) — Kapı ayrımı: tedarikçi hesabı kiracı kapısında (giriş, MFA doğrulama, yenileme, zorunlu parola değişimi)
  kiracı kapısının genel 401'ini alır; kiracı hesabı ve platform hesabı tedarikçi kapısında genel 401; `supplier_user` token'ı
  `/api/auth/me`, `/api/auth/refresh-token`, `/api/auth/logout` ve `/api/tenant-auth/*` uçlarında token almaz. Sabotaj: kiracı
  kapısındaki tedarikçi hesabı reddini kaldırmak → test kırmızı.
- [ ] AC-16 (zorunlu) — Aktif bağı olmayan tedarikçi hesabı, bilinmeyen e-posta ve yanlış parola aynı 401'i (kod, gövde, ölçülen
  süre bandı) alır. Sabotaj: bağ yokken ayrı bir hata kodu döndürmek → test kırmızı.
- [ ] AC-17 (zorunlu) — Çoklu bağ: kişinin kiracıda birden çok aktif bağı varsa kod doğrulaması token yerine seçim bileti ve
  yalnız kendi bağlarının listesini (LE, `SUP-` kodu) döner; `select` seçilen tek bağla token verir; bağlı olmayan LE ile seçim
  401; bilet ikinci kez 401. Sabotaj: bileti tek kullanımlık işaretlememek → ikinci kullanım testi kırmızı.
- [ ] AC-18 (zorunlu) — `logout` oturum ailesini iptal eder (sonraki yenileme 401); `me` bağ bilgisini ve oturum bitişini döner;
  `tenant_user` token'ıyla `me` 403. Sabotaj: `logout`'ta yalnız sunulan anahtarı iptal etmek → aile testi kırmızı.
- [ ] AC-19 (zorunlu) — S2S `binding_rev` ucu: geçerli servis token'ıyla `{active, revision, supplierCode, legalEntityId}`
  döner; kaldırılmış bağ `active = false`; kullanıcı token'ı / yanlış audience / listede olmayan `service_name` 401; başka kiracının
  bağı 404. Sabotaj: ucu `[Authorize]` kullanıcı şemasına açmak → kullanıcı token'ı testi kırmızı.
- [ ] AC-20 (zorunlu) — Oturum olayları (giriş challenge'ı, oturum başlangıcı, yenileme, çıkış) token verilmeden önce denetim
  satırı yazar; yazılamazsa 503 ve token yok; satırlarda parola, kod, token ve bilet yoktur. Sabotaj: yazımı token üretiminden
  sonraya almak → arıza enjeksiyonu testi kırmızı.

**DL-3 — Gateway**

- [ ] AC-21 (zorunlu) — `supplier_user` token'ı `/api/supplier-performance/portal/*` ve `/api/supplier-auth/*` dışında her yolda
  (ör. `/api/suppliers`, `/api/users`, `/api/inventory/...`) gateway'de 403 "Forbidden Actor"; izinli iki önekte servise iletilir.
  Sabotaj: izin listesi denetimini kaldırmak → portal dışı yol testi kırmızı.
- [ ] AC-22 (zorunlu) — F4: `actor_type` claim'i olmayan imzalı token kişiselleştirme, servis kararlı ve genel kiracı uçlarında
  403; regresyon: `tenant_user` token'ıyla örnek kiracı rotaları (en az Auth, Procurement, SupplyChain, Inventory, MDM birer uç) ve
  platform token'ıyla platform uçları önceki davranışla aynı. Sabotaj: `TenantResolutionMiddleware.cs`'te
  `!string.IsNullOrWhiteSpace(actorType) &&` kısa devresini geri koymak → claim'siz token testi kırmızı.
- [ ] AC-23 (zorunlu) — `ocelot.json`'da `/api/supplier-auth/{everything}` → 5056 ve `/api/supplier-performance/portal/{everything}`
  → 5066 rotaları OPTIONS dahil var; gateway rota testi her iki öneki doğru servise iletir. Değişikliği `integration-agent` yapar.
  Sabotaj: portal rotasını kaldırmak → rota testi kırmızı.

**DL-4 — bağlama ekranı (isteğe bağlı)**

- [ ] AC-24 (isteğe bağlı) — `Views/Governance/SupplierBindings/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` açıkça
  yazılı; `verify_datatable_page.py --area Governance --module SupplierBindings` PASS; `data-dt-data-mode="server"`.
  Sabotaj: `Layout` satırını silmek → doğrulayıcı kırmızı.
- [ ] AC-25 (isteğe bağlı) — 7 dil `.resx` paritesi (en, tr, fr, es, zh, ar, ru); `auth.supplier-bindings.read` olmayan kullanıcıya
  iskelet çizilmez, tek açıklama gösterilir (UAS-001); sabit yedek lookup listesi yok; sayfa yardımı anahtarları var (UXH-001).
  Sabotaj: bir dilden bir anahtarı silmek → parite testi kırmızı.

## 17. Test Expectations

| AC | Test (öneri adı) | Tür | Sabotaj (üretim kodu → test kırmızı) |
|---|---|---|---|
| AC-1 | `SupplierBindings/CreateStoresCanonicalCodeTests` | işleyici + sahte uygunluk istemcisi | sabotaj: saklanan değer istekteki kod olur |
| AC-2 | `SupplierBindings/CreateEligibilityFailClosedTests` (her dal ayrı) | işleyici + HTTP istemci (sahte sunucu) | sabotaj: zaman aşımı "uygun"a eşlenir / çağrı atlanır |
| AC-3 | `SupplierBindings/SingleActiveBindingConcurrencyTests` | gerçek Mongo (replika seti) | sabotaj: tekil indeks + ön kontrol kaldırılır |
| AC-4 | `SupplierBindings/SupplierBindingTenantIsolationHttpTests` | HTTP (WebApplicationFactory) | sabotaj: repository'den `TenantId` filtresi çıkar |
| AC-5 | `SupplierBindings/SupplierBindingPermissionHttpTests` | HTTP | sabotaj: `[HasPermission]` kaldırılır |
| AC-6 | `SupplierBindings/SupplierAccountEmailRulesTests` | işleyici + gerçek indeks | sabotaj: tedarikçi olmayan hesap bağlanabilir |
| AC-7 | `SupplierBindings/RevokeIsSoftDeleteTests` | repository + gerçek Mongo | sabotaj: `DeleteOneAsync` ile fiziksel silme |
| AC-8 | `SupplierBindings/CorrectCodeTests` | işleyici | sabotaj: `Revision` artışı kaldırılır |
| AC-9 | `SupplierBindings/SupplierAccessAuditWriterTests`, `SupplierBindingHistoryHttpTests`, `AuditTrailStandardTests` | yazıcı + arıza enjeksiyonu + mimari test | sabotaj: yazıcı hatası yutulur / sonuç satırı işlem dışı |
| AC-10 | `SupplierBindings/DriftQueryTests` | işleyici | sabotaj: hata durumunda boş liste |
| AC-26 | `SupplierBindings/AssignRoleRefusesSupplierAccountTests` | işleyici (XMC-001) | sabotaj: tedarikçi hesabı denetimi kaldırılır |
| AC-11 | `SupplierAuth/SupplierLoginAlwaysRequiresOtpTests` | işleyici, `MfaRequired` her iki değer | sabotaj: kiracı `MfaRequired` ayarına bağlanır |
| AC-12 | `SupplierAuth/SupplierTokenClaimsTests` | TokenService + JWT çözümü | sabotaj: ömür 120 dk anahtarından / kiracı izinleri eklenir |
| AC-13 | `SupplierAuth/SupplierSessionAbsoluteLifetimeTests` | işleyici + sahte saat | sabotaj: kayan pencere (`UtcNow + 8 sa`) |
| AC-14 | `SupplierAuth/SupplierRefreshRereadsBindingTests` | işleyici | sabotaj: yenilemede bağ okuması atlanır |
| AC-15 | `SupplierAuth/DoorSeparationHttpTests` (kiracı kapısının dört işleyicisi + `/api/auth/*`) | HTTP | sabotaj: kiracı kapısındaki tedarikçi reddi kaldırılır |
| AC-16 | `SupplierAuth/SupplierLoginUniformRefusalTests` | HTTP + süre bandı | sabotaj: bağ yokken ayrı hata kodu |
| AC-17 | `SupplierAuth/SupplierBindingSelectionTests` | HTTP | sabotaj: bilet tek kullanımlık işaretlenmez |
| AC-18 | `SupplierAuth/SupplierLogoutAndMeTests` | HTTP | sabotaj: yalnız sunulan anahtar iptal edilir |
| AC-19 | `SupplierAuth/BindingRevisionServiceEndpointTests` | HTTP + Platform biçimli servis token'ı | sabotaj: uç kullanıcı şemasına açılır |
| AC-20 | `SupplierAuth/SupplierSessionAuditFailClosedTests` | arıza enjeksiyonu | sabotaj: denetim yazımı token üretiminden sonraya alınır |
| AC-21 | Gateway `SupplierUserAllowListTests` | gateway middleware testi | sabotaj: izin listesi denetimi kaldırılır |
| AC-22 | Gateway `MissingActorTypeRefusedTests` + `TenantRouteRegressionTests` | gateway middleware testi | sabotaj: `IsNullOrWhiteSpace` kısa devresi geri konur |
| AC-23 | Gateway `SupplierRoutesOcelotTests` | rota testi | sabotaj: portal rotası kaldırılır |
| AC-24 | `verify_datatable_page.py`, `quality-gate-datatable` | doğrulayıcı | sabotaj: `Layout` satırı silinir |
| AC-25 | RESX parite testi, vitest (UAS-001), sayfa yardımı testi | frontend | sabotaj: bir dilden bir anahtar silinir |

- Her dilimde: ilgili servisin **tam** test takımı (filtre yok), `dotnet build` (Auth, Gateway; DL-4'te Web),
  `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`.
- Sabotaj kanıtı: her AC'nin sabotajı üretim kodunda uygulanır, kırmızı çıktı kaydedilir, geri alınır, yeşil çıktı kaydedilir
  (REP-001 koşu kaydı).
- DL-1 kabulü canlı kanıt ister: Gateway üzerinden gerçek MOD-0140 ucuna karşı bir başarılı kur ve bir 422 (E4).
- DL-3 kabulü canlı kanıt ister: `supplier_user` token'ıyla portal dışı bir yolda 403 ve portal yolunda servise ulaşma (E5).

## 18. Ready-for-dev Checklist

- [ ] S0 kapandı (Bölüm 0.4): D1–D8 yeniden onayı karar dosyasında; SQ-1, SQ-3, SQ-4, SQ-7 cevaplı
- [ ] XMC-001: Auth (Ali) ve Gateway (MOD-0032 sahibi / `integration-agent`) izinleri yazılı
- [ ] Control Tower defter kararı: yeni Auth denetim izi (Bölüm 21) — DL-1 kodundan önce
- [ ] MOD-0140 DL-3 main'de + Gateway üzerinden canlı kanıt (SQ-9) — DL-1 kodundan önce
- [ ] Registry'de `MOD-0360-FU01` satırı (S0)
- [ ] Golden Reference Slim okundu; Bölüm 10 yapısı birebir
- [ ] Frontmatter zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `data_mode`, `entity_base`)
- [ ] Layout & Shell Contract: `Layout = "_LayoutTenantShell"` açıkça (DL-4)
- [ ] Validation Rules her alan için yazılı; Failure Path en az dört senaryo (yinelenen, eksik, yetkisiz, eşzamanlılık) — var
- [ ] Authorization Convention: izin listesi + policy + aktör türü — var
- [ ] Gateway routing kararı açık + `integration-agent` görevi — DL-3 başında açılır
- [ ] Lookup kararı (DL-4): LE ve tedarikçi seçimi, sabit yedek yok — var
- [ ] Audited Events her yazma komutunu tam bir kez içeriyor; K2 sınıfı kapalı-başarısız ve testli — var
- [ ] Sayfa yardımı (DL-4): `Help.SUPPLIERBINDINGS.*` kiracı 7 dil; tur yok (beş akıştan biri değil)
- [ ] CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı (yüksek risk: yeni aktör türü, kimlik doğrulama yolu, gateway, kiracı)

## 19. Implementation Notes

- **Plan hizalaması:** plan kaynağı `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C4 (`:90`) ve C7 (`:93`). Planda bu iş için
  `1.x.y` yetenek kimliği yok → `plan kimliği yok`; plan MVP'si MVP-6 (C4/C7 satırları).
- **Neden hesap düzeyinde ayırt edici (SQ-1 önerisi):** kiracı kapısı platform hesabını şifreye bakmadan hesap alanıyla reddeder
  (`LoginCommandHandler.cs:108-114`, `User.cs:50`). Bağdan türetilen ayırt edici, bağ kaldırılınca hesabı kiracı kullanıcısına
  çevirir ve tedarikçi kişisi kiracı kapısından girebilir. Bu yüzden D2'deki "hesap `Human` kalır" metni öneriyle çelişir; S0'da
  D2 yeniden yazılır. `AccountKind` bugün `Unknown / Human / Service` (`services/Diten.AuthService/src/Diten.AuthService.Domain/Enums/AccountKind.cs:20-26`)
  ve otomatik oluşturma yolu `Unknown` bırakmak zorundadır (aynı dosya, özet yorumu; `AccountKindCreationPathsGuardTests`) — kur
  komutu yeni bir oluşturma yoludur, bekçi değişikliği XMC-001'dir.
- **MFA:** bugün yalnız e-posta kanalı var (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/MfaChallengeService.cs:43`)
  ve zorunluluk kiracı genelidir (`LoginCommandHandler.cs:151`). Tedarikçi kapısı ayarı okumaz, her girişte challenge açar.
  Challenge kaydının hangi kapıya ait olduğu tutulmalıdır (SQ-2).
- **Token ömrü:** `appsettings.json:49-50` bugün 120 dk / 7 gün; `JwtSettings.cs` varsayılanı 15 dk. Tedarikçi ömrü ayrı anahtardan
  okunur; kiracı değerine dokunulmaz.
- **Aktör türü yazımı:** Auth bugün iki yerde `actor_type` yazar (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:39`,
  `:113`); `supplier_user` üçüncü üreticidir.
- **Yenileme ve aktör:** yenileme, yenileme anahtarındaki aktörü erişim token'ınınkiyle karşılaştırır (`RefreshTokenCommandHandler.cs:110`);
  tedarikçi yenilemesi ayrı uçtadır ama aynı karşılaştırmayı korur.
- **E-posta tekilliği ve 409:** yarış durumunda indeks `DuplicateUserEmailException` üretir
  (`services/Diten.AuthService/src/Diten.AuthService.Application/Common/Exceptions/DuplicateUserEmailException.cs:8`); kur komutu bunu
  `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT` ya da `SUPPLIER_BINDING_EXISTS`'e (hesap tedarikçiyse) eşler.
- **Denetim deseni:** MOD-0018-FU27 K-W5 deseni (ayrı yazıcı tipi, deneme + sonuç satırı, iş yazımı ile sonuç satırı aynı işlem,
  okuma arayüzü, geçmiş ucu, meta-denetim, defter satırı) emsal alınır:
  `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md:485-545`, `:744`.
  FU27 DL-5 bugün kodda yok (`ISupplierAccessAuditWriter`'ın emsal tipi `IPaymentSodWaiverAuditWriter` yok); bu paket kendi yazıcısını kurar.
- **Tüketici:** MOD-0148 §0.0 (`execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md:31-61`) bu
  paketten bağ, kanonik kod, `supplier_user` token'ı, izin listesi ve iki adımlı doğrulamayı bekler; §0.1 replay receipt'in
  `validatedIssuer` / `subject` alanları bu paketin token'ından gelir (`:210-221`); §14 ve §15 portal izinleri ve rota sahipliği.
- **MOD-0140:** bağlama okuması DL-3 ucudur; S2S yolu (DL-7, S-4) MOD-0148'indir
  (`execution/domains/procurement/module-packs/MOD-0140-supplier-onboarding.md:457`, S-5 `:714`).
- **Sahip kararı kaynağı:** `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md`; Natig'in isteği
  `docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC03-BINDING-CANONICAL-CODE-REQUEST.md` (M2 ile kabul).
- Ölçümler HEAD `e8128c224` üzerinde kod okumasıdır; çalışma testi yapılmadı.

## 20. Follow-up Items

- **FU-A — MOD-0032 FU: internete açılma kapıları** (`TrustedProxies`, gateway rate limit, paylaşılan limiter deposu). Numara ve
  sahip TBD; portal internete açılmadan önce zorunlu (C4).
- **FU-B — portal alt alanı ve tedarikçi giriş ekranı** (host'a bağlı çerezler, 7 dil) — MOD-0148 portal UI ile.
- **FU-C — tedarikçinin self-servis parola sıfırlaması** — v1'de Admin yeniden davet eder.
- **FU-D — onboarding'den otomatik bağ** (D6 "sonraki sürüm").
- **FU-E — kiracı Kullanıcılar ekranında tedarikçi hesaplarının ayrımı** (MOD-0360 ekranı, XMC-001).
- **FU-F — MOD-0140 kanonik kod değişikliği bildirimi** (olay) — bugün tespit AC-10 sapma sorgusuyla elle yapılır.
- **FU-G — ikinci MFA kanalı** (TOTP vb.) — SQ-2 cevabına göre.

## 21. Audited Events

Servisin bugünkü durumu (`tests/architecture/audit-ledger/Diten.AuthService.md`, `## İzler`): kabul edilmiş tek iz
`auth-kullanici-iletimi` (yol b, en iyi çaba — K2 borcu); `auth-gunlugu` ve `auth-rbac-gunlugu` `aday`. `PinnedCounts` Auth için
`Debt 18 / Exceptions 1 / K2Debt 11` (`tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs:61`).
Bu paketin bütün yazma komutları **K2 sınıfıdır** (kimlik / yetki): kayıt yazılamazsa komut durur.

**Yol:** `c` — yeni iz `auth-tedarikci-erisim-gecmisi`, yazıcı `ISupplierAccessAuditWriter` (önce-yaz, `WriteOnceAsync` üzerine;
`authAuditLogs`'a `EntityType` + `EntityId` ile), okuma `ISupplierAccessAuditReader` + geçmiş ucu, meta-denetim
`IPlatformAuditForwarder` ile; kuralın §5.c beş koşulu DL-1'de tek tek kanıtlanır. **Engel:** iz için Control Tower defter kararı
(`CT-DECISIONS.md` satırı) DL-1 kodundan önce; karar gelmezse komutlar yazılmaz (AUD-001 §4.4). `PinnedCounts` değişmez — yeni
borç yok.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateSupplierActorBindingCommand | `auth.supplier-binding.created` (+ yeni hesapta `auth.supplier-account.created`) | SupplierActorBinding (+ User) | c | Sonra: LE, UserId, SupplierCode, SupplierMasterId, Status, Revision; istekteki kod; hesap açıldıysa e-posta | — |
| RevokeSupplierActorBindingCommand | `auth.supplier-binding.revoked` | SupplierActorBinding | c | Önce/sonra: Status, Revision, IsDeleted; gerekçe | — |
| CorrectSupplierActorBindingCodeCommand | `auth.supplier-binding.code-corrected` | SupplierActorBinding | c | Önce/sonra: SupplierCode, Revision; gerekçe; istekteki kod | — |
| SupplierLoginCommand | `auth.supplier-login.challenge-created` | MfaChallenge | c | kanal, maskeli hedef, LE seçimi (varsa) | — |
| VerifySupplierMfaCommand | `auth.supplier-session.started` ya da `auth.supplier-login.selection-issued` | RefreshToken (oturum) | c | SessionId, bindingId, binding_rev, oturum bitişi | — |
| ResendSupplierMfaCommand | `auth.supplier-login.challenge-resent` | MfaChallenge | c | kanal, sonuç | — |
| SelectSupplierBindingCommand | `auth.supplier-session.started` | RefreshToken (oturum) | c | SessionId, seçilen bindingId, binding_rev | — |
| RefreshSupplierTokenCommand | `auth.supplier-session.refreshed` / `auth.supplier-session.refused` | RefreshToken (oturum) | c | SessionId, binding_rev önce/sonra, ret nedeni | — |
| LogoutSupplierCommand | `auth.supplier-session.ended` | RefreshToken (oturum) | c | SessionId, iptal edilen anahtar sayısı | — |

- **Kayda yazılmayan alanlar:** parola, OTP kodu, erişim / yenileme token'ı ve karması, seçim bileti ve karması, davet bağlantısı.
- **Yazma hatası davranışı (K2):** deneme satırı iş yazımından önce; yazılamazsa 503 ve iş verisi / token yok; iş yazımı + sonuç
  satırı aynı Mongo işleminde (AC-9, AC-20). Belirsiz commit'te ikinci mutasyon yapılmaz, denetim kimliğiyle okunur (FU27 emsali).
- Sorgular (liste, tekil, geçmiş, sapma, `me`, `binding_rev`) yazma komutu değildir; geçmiş okuması meta-denetim satırı yazar.

## 22. Bağımlılık kapıları (dilim başına)

| Dilim | Başlamadan önce |
|---|---|
| DL-1 | S0 kapalı (0.4); XMC-001 Auth izni; Control Tower defter kararı (Bölüm 21); **MOD-0140 DL-3 main'de + Gateway canlı kanıtı** (SQ-9); CT-APR-02 + CT-APR-01 |
| DL-2 | DL-1 kabul; SQ-2 (challenge kapısı), SQ-3 (audience), SQ-7 (izin verme yolu); MOD-0148 anahtarları Auth kataloğunda |
| DL-3 | DL-2 kabul; Gateway izni (MOD-0032 sahibi); `integration-agent` görevi; regresyon rota listesi onaylı |
| DL-4 | DL-1 kabul; Web ekran sahibi izni; isteğe bağlı — sahip isterse |

## 23. Açık sorular

**Ali'ye (Auth sahibi, GMG-ERP)**
- **SQ-1 — hesap türü ayırt edicisi:** (a) `AccountKind.Supplier = 3`; (b) `IsPlatformAccount` gibi ayrı bir hesap alanı;
  (c) bağdan türetme. Öneri (a) ya da (b) — hesap düzeyi; (c) bağ kaldırılınca hesabı kiracı kullanıcısına çevirir. Taslağın
  "hesap `Human` kalır" metni bu öneriyle değişir.
- **SQ-2 — MFA kanalı:** v1 yalnız e-posta kodu mu (bugünkü tek kanal)? Challenge kaydına kapı alanı eklensin mi (kiracı kapısının
  `mfa/verify`'ı tedarikçi challenge'ını kabul etmesin)?
- **SQ-3 — S-5 audience / `service_name`:** S2S `binding_rev` ucu için audience adı ve kabul edilecek `service_name` listesi
  (MVP-6 SupplyChain); MOD-0140 DL-7 audience'ı ile aynı mı, ayrı mı?
- **SQ-4 — D7 ile MOD-0140 S-4 çelişkisi:** D7 S2S kapsamını "yalnız `validate`" yazıyor; S-4 servis token'ını `POST /validate` ve
  tekil `GET /{supplierId}/eligibility` için açıyor; MOD-0148 §0.0 tekil uygunluk okumasını kullanıyor. Öneri: D7 S-4'e hizalanır.
- **SQ-5 — XMC-001 kapsamı:** Bölüm 5'teki Auth dosya listesi (kiracı kapısının dört işleyicisi, `AuthController`, `AssignRole`,
  `AccountKind` bekçisi) için izin.
- **SQ-6 — `auth.supplier-bindings.read` ayrı anahtar mı** (öneri: evet, yalnız okuma rolleri için), RCS-001 "görünen kod yok" önerisi
  ve merkezin kod düzeltmesini kiracıda hangi hesapla yürüteceği.

**Natig'e (MOD-0148 sahibi)**
- **SQ-7 — izin verme yolu:** `supplier_user` token'ına `.read` / `.submit` sabit küme olarak mı (öneri) yoksa kiracıda tohumlanan bir
  rolle mi girsin?
- **SQ-8 — iki adımlı doğrulama claim'i:** portal `amr` claim'ini yeniden denetler mi, yoksa Auth'a mı güvenir (MOD-0148 §0.0 bu
  pakete bırakıyor)?

**Cihan'a (MOD-0140 / Procurement)**
- **SQ-9 — DL-3 main + canlı kanıt:** uygunluk ucu ne zaman main'de; Gateway üzerinden gerçek 200, gerçek 404 `UNKNOWN_SUPPLIER` ve
  kabul edilmeyen token reddi kanıtı ne zaman? DL-1 bunu bekler. Admin'in kullanıcı token'ıyla (D6) okuma için gereken izinler
  (`procurement.suppliers.eligibility.read`, S-2 `mdm.legal-entities.read`) onaylı mı?
- **SQ-10 — DL-7 zamanı:** MOD-0148'in servis token'ıyla okuması (bu paketi değil MOD-0148'i bekletir).

**Merkez / Control Tower (Beste)**
- Bölüm 21 yeni iz için defter kararı (yol `c`, beş koşul) — DL-1'den önce.
- MOD-0032 FU (internete açılma) numarası ve sahibi — TBD.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | S1 çekirdek: SupplierActorBinding kaydı, kur / kaldır / kod düzelt, uygunluk okuması, tedarikçi hesabı, K2 denetim izi | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-2 | S2 tedarikçi kapısı: login / OTP / seçim / refresh / logout / me, supplier_user token, S2S binding_rev ucu, kiracı kapısı reddi | Owned Objects, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-3 | S3 Gateway: supplier_user izin listesi, F4 sertleştirmesi, iki ocelot rotası (integration-agent) | Gateway / API Routing Decision, Repo Scope, Acceptance Criteria, Test Expectations | E5 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-4 | S4 bağlama ekranı (isteğe bağlı), 7 dil | Layout & Shell Contract, Frontend File Contract, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
