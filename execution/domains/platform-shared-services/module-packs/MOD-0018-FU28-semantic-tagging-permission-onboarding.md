---
id: MOD-0018-FU28
name: Semantic Tagging Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
owner: auth-owner (MOD-0018 sahibi inceler ve teyit eder) / yazar Treasury Intake CT
branch: feature/pss/mod-0018-fu28-semantic-tagging-permission-onboarding (henüz açılmadı)
started: 2026-10-30
target: TBD
form_field_count: 0
parent: MOD-0018
consumer_module: MOD-0057
---

# MOD-0018-FU28 — Semantic Tagging Permission Onboarding

> **Taslak — kod başlatma izni değildir.** Bu paket izin kataloğu, rol, hak (grant), rol üyeliği, hak eşitlemesi (entitlement),
> token, manifest, gezinme, yapılandırma, gizli bilgi ya da kiracı verisi oluşturmaz. Kod yalnız paket `approved` /
> `ready-for-dev` olduktan, MOD-0057 onaylandıktan ve CT-APR-01 onay satırı main'e girdikten sonra, Bölüm 5'teki tam yol
> listesiyle yazılır. MOD-0018 sahibinin bu metne teyidi **yoktur**.
>
> **Yetki dayanağı.** MOD-0057'nin Auth kaydı FU27'ye eklenmez, ayrı MOD-0018 ekinde yapılır (**KARAR D97/3**, kaynak Y-26, MOD-0018
> sahibi Ali). Ek metnini Treasury CT yazar, MOD-0018 sahibi inceler ve teyit eder (D9, D53; öneri dosyasındaki "metni biz yazarız, sen
> teyit edersin" düzeni). K1 payı **KARAR D90/3**: O-5 (a) `crm.*` ad alanı + üç rol şablonu. Kaynak metin: MOD-0057 "Authorization
> Convention" (`execution/domains/commercial-suite/module-packs/MOD-0057-semantic-tagging-taxonomy-management.md:584-603`, HEAD
> `a7d6a44a9`). Anahtar bu pakette uydurulmaz; MOD-0057 değişirse bu paket onunla birlikte değişir.
>
> **Kaynakların main durumu (ölçüm 2026-10-30, `origin/main` @ `bf3b0cd57`, son fetch 13:03, uzak durum doğrulanmadı):** D90 main'de;
> D97, D100 ve Y-26 main'de **değil** (dal `chore/ct/treasury-intake-ct-05`). D62/1 gereği paket kapısı bunların main'e girmesini
> bekler (Bölüm 18).
>
> **DCP-002 kimlik durumu.** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU28 --name "Semantic Tagging
> Permission Onboarding" --parent MOD-0018` → `OK  MOD-0018-FU28: proven against Blueprint/registry.` (çıkış 0, 2026-10-30, paket
> yazılmadan önce). `--discover "Semantic Tagging Permission Onboarding"` → çıkış 3 (inceleme gerekli; Blueprint 1, registry 7, paket
> 11, gateway 1, görev 1; rapor-sha256 `49229ccc3ae802fded421b29950ef6d689ff3587b029556e535a4d744b7fcb11`): adaylar MOD-0057'nin
> kendisi, MOD-0018 onboarding kardeşleri ve TRM-18; rapor metninde `FU28` geçmiyor, mükerrer yok. D100/2 taraması aynı sonucu
> verir. Numara **adaydır** (D97/3); MOD-0018 sahibinin paket teyidine dahildir.
>
> **Golden Reference kararı.** Arka uç yetki / katalog / onboarding işi; Razor ya da DataTable yüzeyi yok: `shell: none`,
> `golden_reference: none`, `form_field_count: 0` bilinçlidir. Yeni kalıcı varlık yoktur; `entity_base: EntityBase` Auth için
> standardın somut değeridir (FU20 deseni).

## 1. Module Summary

Bu FU, MOD-0057 Semantic Tagging & Taxonomy Management izinlerini Auth'a katar. FU27'den çok dardır: tek tüketici, 17 anahtar, 3 rol.

| | Kapsam | Dilim |
|---|---|---|
| a | MOD-0057'nin 17 `crm.tag-*` anahtarı için özel, saf hak profili (tam küme doğrulaması) | DL-1 |
| b | 1 yalnız açık atama anahtarı: `crm.tag-assignments.read-tenant-wide`; `ExplicitGrantOnlyPermissions` son hâli **21** (20 + 1, D97/3; sıra K-6) | DL-1 |
| c | 3 rol şablonu (Bölüm 14.2); adlar K1 seçimi, **MOD-0018 sahibinin teyidi açık** (K-4) | DL-1 |
| d | İki `read` eylemli anahtarın varsayılan Viewer tabanından dışlanması | DL-1 |
| e | 3 rolün 7 dilli görünen adı (kiracıya görünür, D79/9 deseni) | DL-2 |

Neden özel profil gerekir (kod gerçeği, ölçüldü): genel eşitleme modül hakkıyla Admin'e modülün bütün anahtarlarını, Viewer'a `read`
anahtarlarını verir (`EntitlementPermissionSyncService.cs:90-94`, seçim `:572`); varsayılan Viewer tabanı, modül hakkı olmayan
kiracıda dahil, `read` eylemli kiracı anahtarını verir (`DefaultRolePermissionTemplate.cs:207-213`). Profil olmazsa Admin, MOD-0057'nin
ayırdığı iki yetkiyi (taksonomi yazma, bağ yazma) birlikte alır ve iki `read` anahtarı her kiracıya gider.

## 2. Ownership and Boundaries

**Bu FU / Auth sahibi (MOD-0018; yazar Treasury CT, inceleme ve teyit sahibin):** açık atama üyeliği, Viewer tabanı dışlaması, hak
profili ve 3 ayrılmış sistem rolü, kiracı + kaynak modül sınırlı idempotent hak verme / geri alma, rol görünen adı kaynakları.

**MOD-0057 yazarı (commercial-suite sahibi atanmadı — O-13, kararı K1 verir, D65/1):** `[HasPermission]` zorlaması, veri kapsamı
(SEC-002), `read-tenant-wide` anlamı, varsa manifest beyanı (K-1).

**Kapsam dışı:**

- `services/Diten.CrmService/**`, MOD-0057 ekranları, menü, Gateway.
- Platform / MDM değişikliği ve SEC-002 eki: bağlar örgütsel kapsam alanı taşımaz (MOD-0057 Runtime Constraints "Veri kapsamı"); FU27
  DL-4 benzeri çift listesi **yok**.
- Muafiyet atama kaydı, iç uç, aday sorgusu (FU27 DL-5 / DL-6 benzeri iş yok); çapraz modül bağımlı anahtar yok (MOD-0057 v1 MOD-0048,
  MOD-0023, MOD-0262 tüketmez).
- MOD-0162 `crm.knowledge.*` ve diğer `crm.*` anahtarları; FU27 kapsamı.
- Rol üyeliği ataması, `read-tenant-wide` iznini bir role bağlama, token içeriği, katalog / hak eşitlemesi çalıştırma, kiracı verisi,
  Production / Staging.

## 3. Owned Objects

| Nesne | Sahip | Kesin değişmez |
|---|---|---|
| 17 `crm.tag-*` `Permission` tanımı | Auth genel kataloğu; beyan yolu K-1 | Anahtar başına tek etkin genel tanım; `TenantId` yok; `Permission.Module` = K-1'deki modül kodu |
| `crm.tag-assignments.read-tenant-wide` | `ExplicitGrantOnlyPermissions` | Hiçbir otomatik yol vermez; hiçbir rol şablonunda yok |
| Semantic Tagging hak profili | Auth `Application/Common/Services` | Saf eşleme; tam 17 anahtar; eksik / fazla / bilinmeyen → mutasyondan önce ret |
| 3 ayrılmış sistem rolü | Auth, kiracı başına | Bölüm 14.2 tam küme; kullanıcı ataması otomatik değil |
| Admin / Viewer varsayılanı | Auth hak profili | K-2 (öneri: hiçbir `crm.tag-*`) |
| Modül kaynaklı haklar | Auth | Kiracı sınırlı; `GrantSource = Module`; `SourceModuleCode` = K-1 kodu |

Yeni kalıcı varlık, komut, sorgu, uç ya da DTO yoktur; mevcut `Permission`, `Role`, `RolePermission`, `RoleAssignmentVersion` kullanılır.

## 4. Entity Fields

Yeni alan yok.

| Mevcut nesne / alan | Değer ya da kural |
|---|---|
| `Permission.Key` | Bölüm 14.2'deki 17 anahtardan biri; takma ad ve joker yok |
| `Permission.Module` | K-1 kodu; açık atıf şart — `crm` hizmet ad alanı değildir, atıf yoksa modül `crm` türer (`PermissionModuleAttribution.cs:47-48, 60-70`) |
| `Permission.Scope` | `Tenant` (`DefaultRolePermissionTemplate.cs:92`: modül `PlatformAdminModules` dışında) |
| `Role.Name` | Bölüm 14.2'deki 3 ad; kiracı sistem rolü; aynı adlı sistem dışı rol → çakışma, hak yok |
| `RolePermission.TenantId` | Sunucu türetir; boş / uyuşmaz → kapalı-başarısız |
| `RolePermission.GrantSource` / `.SourceModuleCode` | `Module` / K-1 kodu |

## 5. Repo Scope

Bu planlama görevi yalnız bu paketi ve kimlik kaydı satırını yazar. Sonraki, ayrı onaylı teslim aşağıdaki listeyle sınırlıdır ("yeni"
yazılmayan dosya mevcuttur; satır numaraları `chore/ct/treasury-intake-ct-05` @ `a7d6a44a9` ölçümüdür).

### 5.1 Auth — DL-1

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — `Keys` `:89-94`; gerekçeli sabit.
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` — `EntitlementOnlyViewerPermissions` `:120`.
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/SemanticTaggingEntitlementGrantProfile.cs` — **yeni**;
  saf, tam küme + 3 rol (desen `ProductLegalEntityScopeEntitlementGrantProfile.cs`).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs` — rol listesi
  `:96-110`, `GrantModuleWithKeysAsync` `:151`, `GrantPermissionsToRolesAsync` `:282`, `RevokeModuleAsync` `:373`, `SelectForRole` `:572`,
  `ReconcileSpecialProfilesAsync` `:582`.
- **Yalnız K-1 (b) seçilirse:** `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` (CRM satırları `:515-564` deseni).

**Testler** (`services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/`): `DefaultRolePermissionTemplateTests.cs`,
`EntitlementPermissionSyncServiceTests.cs`, `EntitlementSyncConsumerTests.cs` (yalnız gerileme), `SemanticTaggingEntitlementGrantProfileTests.cs`
— **yeni**, `SemanticTaggingPermissionOnboardingMongoTests.cs` — **yeni** (sabit veritabanı `diten_auth_tagging_itest`, açık
`MONGO_TEST_URI`, uygulama portu 27017 yasak; FU27 §5.1 deseni).

### 5.2 Web — DL-2 (rol görünen adı satırları)

- `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` — yedi dosyanın her birine 3 `RoleName_{RoleName}` satırı
  (desen `SharedResource.en.resx:567-570`).
- `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`RoleNames` `:152`),
  `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_IndexL10n.cshtml` (`RoleNames` `:32`),
  `frontend/Diten.Web/Views/Governance/Roles/_IndexL10n.cshtml` — 3 giriş. **Ölçüm:** Roller köprüsü bugün yok; FU27 §5.5 ekler (K-6).
- `frontend/Diten.Web.Tests/Governance/SemanticTaggingRoleNameL10nGuardTests.cs` — **yeni** (desen `Navigation/NavManifestL10nGuardTests.cs`).

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint çalışma kitapları, DCP'ler, backlog, ilgisiz yönetişim dosyaları.
- MOD-0057 paketi, MOD-0018-FU9…FU27 paketleri ve diğer bütün paketler; `execution/domains/**/domain-config.md`.
- Bölüm 5 dışındaki Auth dosyaları: `DataSeeder.cs` (K-1 (b) dışında), token üretimi, kullanıcı-rol atama işleyicileri,
  `FullCatalogPermissionGrantService.cs`, FU20 / FU22 / ürün kimliği profil üretim kodu, `appsettings*`, gizli bilgiler.
- `services/Diten.CrmService/**`, `services/Diten.Platform/**`, `services/Diten.MdmService/**`, `gateway/**`, Bölüm 5.2 dışındaki `frontend/**`.
- Katalog / hak eşitlemesi çalıştırma, rol üyeliği, kiracı verisi, Production / Staging.

## 7. Dependencies

| Bağımlılık | Durum (ölçüm 2026-10-30) | Etki |
|---|---|---|
| MOD-0057 onayı | `status: draft`, `approval_status: not-approved` | Kod yalnız onaydan sonra; bu paketin kodu main'de = MOD-0057 DL-1 kabul kapısı (D53/2 deseni, MOD-0057 Ready-for-dev) |
| Bu paketin metni + sahip teyidi | yok | MOD-0057 paket kapısı (MOD-0057 "Auth paket metni main'de" maddesi); DL-1 ve DL-2 kodu için de |
| Anahtar beyanı | **CrmService'te `ModuleRegistration` klasörü ve manifest sağlayıcısı yok**; MOD-0057 Repo Scope'unda da yok; CRM anahtarları bugün `DataSeeder.cs` `moduleOverride` ile kaydolur (`:515-564`) | K-1; profil beyan kümesine bağlıdır |
| Platform modül kataloğu / kiracı hakkı | `services/Diten.Platform` altında `crm-account` / `crm-contact` / `crm-territory` / `crm-knowledge` geçmiyor (arama 0) | K-1 kodu için hak yolu ölçülmedi; hak olmadan profil hak vermez (beklenen) |
| `ExplicitGrantOnlyPermissions` | bugün 10 anahtar (`:89-94`); FU27 10 ekler (`draft`, kod yok) | 21 yalnız FU27'nin tamamından sonra doğrudur (K-6) |
| Roller ekranı `RoleNames` köprüsü | yok; FU27 §5.5 ekler | DL-2 (K-6) |
| `crm.tag-*` kodda | `services`, `frontend`, `gateway` altında 0 eşleşme; üç rol adı da 0 | Depoda çakışma yok; kiracı verisi ölçülmedi (Bölüm 19) |
| FU13 token / önbellek sınırlı bayatlık | var | Hak yalnız token yenileme / yeni girişle görünür |
| D97, D100, Y-26 main'de | değil | Paket kapısı (D62/1) |

## 8. Runtime Constraints

### 8.1 Profil ve mutasyon sırası (FU22 §8, FU27 §8.1 deseni)

Normalleştir → profil alt kümesini tespit et → beyan kümesini ve katalogu tam doğrula → 3 rol adının hepsini çakışma için önceden
denetle → rolleri idempotent oluştur → hakları ver / gereksizleri geri al → yalnız eşleşen kiracı + kaynak modül satırlarına dokun.
`Manual` / `System` / başka modül / başka kiracı satırları korunur. Profil modül kodunun bütün beyan kümesini sahiplenir; genel kalan
boştur. Ön denetimden sonraki eşzamanlı ad yarışı kapalı-başarısız kalır, ama hak almamış rol iskeleti bırakabilir (FU22 §8 madde 5 sınırı).

Hak eksik / kapalı / süresi dolmuş → eşleşen modül kaynaklı haklar bütün rollerden kalkar. Ulaşılamaz / bozuk / belirsiz hak durumu → ne
verir ne geri alır; yeniden denenir. İptal belirteci yayılır. Hak yalnız rol hakkı yaratır; **kullanıcıya rol üyeliği otomatik değildir**.

### 8.2 Varsayılan roller

- `crm.tag-assignments.read-tenant-wide` hiçbir otomatik yoldan verilmez (`DefaultRolePermissionTemplate.cs:182`, süzgeç
  `GrantPermissionsToRolesAsync`); role yalnız yetkili kişinin açık (`Manual`) atamasıyla bağlanır.
- `crm.tag-taxonomies.read` ve `crm.tag-assignments.read` `EntitlementOnlyViewerPermissions`'a girer (`:120`, kural `:211`); aksi hâlde
  hakkı olmayan kiracının Viewer'ı da alır. Bu dışlama K-2 sonucundan bağımsızdır.
- Modül hakkıyla Admin / Viewer: K-2. Varsayılan kiracı SuperAdmin tam katalog: K-3.

### 8.3 Dağıtım sırası (BL-592 §7, FU27 §8.3 deseni)

K-1 (a) ise: 1. FU28 DL-1 Auth kodu → 2. MOD-0057 manifest beyanı. Ters sırada eski Auth `read` anahtarlarını her kiracının Viewer'ına
verir. Geri alma ters sırada. K-1 (b) ise beyan ve kod aynı Auth dağıtımındadır, sıra riski yoktur.

## 9. Layout & Shell Contract

`shell: none`. Razor görünümü, rota, DataTable ya da gezinme öğesi sahiplenilmez; yeni `.cshtml` yoktur, `Layout = "..."` yazılacak dosya
yoktur. DL-2 yalnız mevcut üç `_IndexL10n.cshtml` köprüsüne rol adı girişi ekler; sayfa, yerleşim ve kabuk değişmez.

## 10. Backend File Convention

CRUD değildir; CQRS klasörü açılmaz (Golden Reference klasör düzeni uygulanmaz). Profil `Application/Common/Services` altında tek, saf,
statik eşlemedir; kalıcılık yalnız mevcut `EntitlementPermissionSyncService` üzerinden. Anahtar kümeleri birleştirilmez: profil FU20 /
FU22 / ürün kimliği profillerinden bağımsız doğrulanır. İptal belirteci depo sınırına kadar taşınır.

## 11. Frontend File Contract

Yeni ekran yok. **Kullanıcıya görünen metin — 7 dil** (rol ekranları kiracıya görünür; Çekirdek kural 4, D79/9 deseni): 3 rolün görünen
adı (Bölüm 14.2). Kod `Role.Name`, kaynak anahtarı `RoleName_{RoleName}` (`SharedResource`), köprü `RoleNames` (`window.L10n.RoleNames`),
ekran yalnız `isSystem` rolde sözlüğe bakar. İngilizce dışı değer en ile aynı olamaz. Saklı `Role.DisplayName` tablonun `en` sütunudur.

## 12. Validation Rules

| Girdi / karar | Zorunlu | Kural | Hata |
|---|---|---|---|
| Beyan kümesi | Evet | Bölüm 14.2'deki 17 anahtara birebir eşit; büyük harf, joker, takma ad yok | Mutasyondan önce ret; eksik / fazla adlarıyla |
| Katalog tanımı | Evet | 17 anahtar etkin ve K-1 modül koduyla | Rol ve hak yazılmaz |
| Açık atama anahtarı | Evet | Otomatik yolda yok, rol şablonunda yok | Test kırmızı |
| Viewer tabanı | Evet | Hakkı olmayan kiracıda hiçbir `crm.tag-*` | Test kırmızı |
| Rol adları | Evet | Aynı adlı sistem dışı rol → bütün profil mutasyonu durur | Rol ve hak yazılmaz |
| Rol görünen adı | Evet | 7 dosyada boş olmayan değer; en dışı dil en ile aynı değil; üç köprüde giriş | Test kırmızı |

## 13. Failure Path to Verify

- **Mükerrer / çakışma:** üç rol adından her biri sistem dışı rol olarak var → sıfır yeni rol, sıfır hak; kiracının önceki durumu aynen.
- **Eksik:** beyan kümesinde anahtar eksik ya da katalogda eksik / silinmiş → sıfır rol / hak mutasyonu; anahtar adlarıyla hata.
- **Yetkisiz:** hakkı olmayan kiracıda Viewer / Admin hiçbir `crm.tag-*` almaz; `read-tenant-wide` otomatik yolla kimseye gitmez.
  Testler `platform_admin` geçişiyle değil, doğrudan `tenant_user` rolleriyle koşar.
- **Eşzamanlılık:** tekrar oynatma kopya üretmez; ön denetim sonrası ad yarışı hak vermez (Bölüm 8.1).
- **Hak düştü:** modül kaynaklı `crm.tag-*` hakları üç rolden kalkar; elle verilen `read-tenant-wide` ve başka modül hakları kalır.
- **Hak belirsiz:** ne verir ne geri alır, başarı bildirilmez.
- **Kiracılar arası:** A kiracısının eşitlemesi B'nin rol / hak durumunu yaratmaz, silmez, göstermez.
- **Görünen ad eksik:** bir dilde yok / boş / en ile aynı / köprüde yok → guard kırmızı.

## 14. Authorization Convention

Politika tüketici uçlarında `[Authorize]` + her eylemde `[HasPermission("…")]` (MOD-0057'nin işi); aktör `tenant_user`. Biçim PKS-001;
ad alanı `crm.*` (D90/3, O-5 (a)). Joker yok; kaynaktaki `taxonomy.*` ve `TAXONOMY_*` takma adları taşınmaz (MOD-0057).

### 14.1 Yalnız açık atama — 1 anahtar

| Anahtar | Dayanak | Not |
|---|---|---|
| `crm.tag-assignments.read-tenant-wide` | MOD-0057 Authorization Convention (O-7, D90/1), D97/3 | Yalnız okuma; kiracı geneli bağ listesi; yazma vermez; hiçbir rol şablonunda varsayılan değil |

Koddaki liste bugün 10; FU27 ile 20; bu ekle **21** (D97/3; sıra K-6).

### 14.2 Rol şablonları — 3 (**adlar K1 seçimi, Ali teyidi açık — K-4; onaylı değil**)

`RoleName` İngilizce PascalCase sabit koddur. Anahtar sayıları: `TagReader` 2, `TagAssigner` 5, `TagTaxonomyManager` 13; rollerin birleşimi
16; 1 açık atama ile toplam 17.

| Anahtar | Katman | `TagReader` | `TagAssigner` | `TagTaxonomyManager` |
|---|---|---|---|---|
| `crm.tag-taxonomies.read` | T1 | ✓ | ✓ | ✓ |
| `crm.tag-taxonomies.create` | T1 | | | ✓ |
| `crm.tag-taxonomies.update` | T1 | | | ✓ |
| `crm.tag-taxonomies.delete` | T1 | | | ✓ |
| `crm.tag-taxonomies.bulk-delete` | T2 | | | ✓ |
| `crm.tag-taxonomies.activate` | T2 | | | ✓ |
| `crm.tag-taxonomies.retire` | T3 | | | ✓ |
| `crm.tag-taxonomies.terms.create` | T1 | | | ✓ |
| `crm.tag-taxonomies.terms.update` | T1 | | | ✓ |
| `crm.tag-taxonomies.terms.delete` | T1 | | | ✓ |
| `crm.tag-taxonomies.terms.bulk-delete` | T2 | | | ✓ |
| `crm.tag-taxonomies.terms.deprecate` | T3 | | | ✓ |
| `crm.tag-assignments.read` | T1 | ✓ | ✓ | ✓ |
| `crm.tag-assignments.create` | T1 | | ✓ | |
| `crm.tag-assignments.delete` | T1 | | ✓ | |
| `crm.tag-assignments.bulk-delete` | T2 | | ✓ | |
| `crm.tag-assignments.read-tenant-wide` | T3 | | | |

Taksonomi yazma izni bağ yazma izni vermez; bağ izni taksonomi değiştirmez (MOD-0057).

**Görünen adlar** — Treasury yazar, MOD-0018 sahibi teyit eder (D79/9 deseni); **teyit yok (K-5).** **ar, ru ve zh için anadil kontrolü
yapılmadı (düşük güven).**

| Kod (`RoleName`) | en | tr | fr | es | zh | ar | ru |
|---|---|---|---|---|---|---|---|
| `TagReader` | Tag Reader | Etiket okuyucu | Lecteur d'étiquettes | Lector de etiquetas | 标签查看者 | قارئ الوسوم | Читатель меток |
| `TagAssigner` | Tag Assigner | Etiketleyici | Étiqueteur | Etiquetador | 标签标注者 | مُعيِّن الوسوم | Ответственный за назначение меток |
| `TagTaxonomyManager` | Tag Taxonomy Manager | Etiket taksonomisi yöneticisi | Gestionnaire des taxonomies d'étiquettes | Gestor de taxonomías de etiquetas | 标签分类法经理 | مدير تصنيفات الوسوم | Менеджер таксономий меток |

"Manager" D103 tek terimini izler (tr yöneticisi, fr Gestionnaire, es Gestor, zh 经理, ar مدير, ru Менеджер). "Etiket" terimi depodaki
`SharedResource.{lang}.resx` `TagsPlaceholder` değerleriyle aynıdır (fr étiquette, es etiqueta, zh 标签, ar وسم, ru метка). 2026-10-29 öneri
dosyasındaki `Tagger` / `TaxonomyManager` satırlarından farklar: yeni adlara uyum, zh 经理, ru "тегов" → "меток".

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. Bu FU uç ya da rota eklemez; `/api/crm/tag-*` rotaları MOD-0057'nin integration-agent görevidir.
Rol görünen adı işi mevcut `GET /api/roles` yanıtındaki `name` kodunu kullanır.

## 16. Acceptance Criteria

Sabotajlar üretim kodunda uygulanır; her biri ayrı testte kırmızıya döner. AC'ler K-2 ve K-3 için CT önerisine göre yazıldı; karar farklı
çıkarsa `ready-for-dev` öncesi yeniden yazılır. Rol adları K-4'e bağlıdır.

**DL-1 — Auth profili:**

- [ ] AC-1 (zorunlu) — `ExplicitGrantOnlyPermissions.Keys` `crm.tag-assignments.read-tenant-wide` içerir; test: SuperAdmin tam katalog yolu, modül hakkı eşitlemesi ve varsayılan rol seçimi bu anahtarı vermez. Sabotaj: anahtar listeden çıkarılınca üç yolun testi ayrı ayrı kırmızıya döner.
- [ ] AC-2 (zorunlu) — Modül hakkı olmayan kiracıda varsayılan Viewer ve Admin hiçbir `crm.tag-*` anahtarı tutmaz (`SelectFor`, 17 anahtarlı katalog). Sabotaj: `crm.tag-taxonomies.read` ya da `crm.tag-assignments.read` `EntitlementOnlyViewerPermissions`'tan çıkarılınca o anahtarın testi kırmızıya döner (iki ayrı test).
- [ ] AC-3 (zorunlu) — Etkin hakla üç rol Bölüm 14.2'deki kümeyi birebir alır (rol başına küme eşitliği, 2 / 5 / 13); kullanıcı-rol satırı sıfır; hiçbir rolde `read-tenant-wide` yok; hiçbir rol taksonomi veya terim yazma anahtarını bağ yazma anahtarıyla birlikte taşımaz. Sabotaj: `TagTaxonomyManager`'a `crm.tag-assignments.create`, `TagAssigner`'a `crm.tag-taxonomies.update`, herhangi bir role `read-tenant-wide` eklenince ya da bir anahtar çıkarılınca o rolün testi kırmızıya döner.
- [ ] AC-4 (zorunlu) — Modül hakkıyla Admin ve Viewer hiçbir `crm.tag-*` almaz (K-2 önerisi). Sabotaj: genel kalan bu modül kodu için yeniden açılınca test kırmızıya döner.
- [ ] AC-5 (zorunlu) — Beyan kümesi tam değilse (bir anahtar eksik, yabancı anahtar, bilinmeyen eylem, büyük harf) hiçbir rol / hak yazılmaz ve hata eksik / fazla anahtarları adıyla verir (dört ayrı test). Sabotaj: tam küme doğrulaması kaldırılınca dört test de kırmızıya döner.
- [ ] AC-6 (zorunlu) — Üç rol adının her biri için sistem dışı çakışma: sıfır rol, sıfır hak (parametreli test). Sabotaj: ad ön denetimi kaldırılınca üç vaka kırmızıya döner.
- [ ] AC-7 (zorunlu) — Hak düştü / kapandı / süresi doldu: modül kaynaklı `crm.tag-*` hakları bütün rollerden kalkar; `Manual`, `System`, başka modül ve başka kiracı satırları kalır. Sabotaj: geri almadaki kaynak modül süzgeci kaldırılınca elle verilen `read-tenant-wide` satırını koruyan test kırmızıya döner.
- [ ] AC-8 (zorunlu) — Belirsiz hak durumu ne verir ne geri alır; tekrar oynatma idempotenttir; A / B kiracı yalıtımı. Sabotaj: kiracı süzgeci kaldırılınca yalıtım testi kırmızıya döner.
- [ ] AC-9 (zorunlu) — Gerçek Mongo testi (`diten_auth_tagging_itest`, `MONGO_TEST_URI`, 27017 değil): rol / hak kardinalitesi, tekrar oynatma, geri al / geri yükle, kaynak koruma, kiracı yalıtımı, sıfır otomatik `userRoles`; sahte / bellek içi yok, atlanmaz. Sabotaj: profil eşitleme yolundan çıkarılınca kardinalite testi kırmızıya döner.
- [ ] AC-10 (zorunlu) — Profil yalnız bu 17 anahtara uygulanır: `crm.territory.*`, `crm.knowledge.*`, FU20, FU22, ürün kimliği ve main'deyse FU27 matrisleri değişmez (gerileme koşusu, sayılar raporda). Sabotaj: profilin uygulanma koşulu `crm.` önekine genişletilince komşu CRM anahtarı testi kırmızıya döner.

**DL-2 — rol görünen adları:**

- [ ] AC-11 (zorunlu) — `SemanticTaggingRoleNameL10nGuardTests` beklenen rol adlarını üretim profilinden türetir (elle yazılmış liste yok) ve her rol için denetler: (i) `RoleName_{RoleName}` yedi `SharedResource.{lang}.resx` dosyasında (en, tr, fr, es, zh, ar, ru) var ve boş değil; (ii) İngilizce dışı altı dilde değer en ile aynı değil; (iii) üç `RoleNames` köprüsünde giriş var; (iv) değerler Bölüm 14.2 tablosuna eşit. Sabotaj: `SharedResource.ar.resx`'ten bir satır silinince, değer en ile aynı yapılınca, bir köprüden giriş kaldırılınca ya da profile kaynaksız rol eklenince test kırmızıya döner.
- [ ] AC-12 (zorunlu) — Kullanıcı rolleri ekranında `tr` ve `ar` dilleriyle üç rol tablodaki görünen adla çıkar; ham kod ya da saklı İngilizce ad görünmez; özel rolün adı değişmez (tarayıcı kanıtı).

## 17. Test Expectations

- **Birim:** profil tam küme ve rol matrisi, açık atama, Viewer tabanı (AC-1…AC-8, AC-10).
- **Entegrasyon:** gerçek Mongo (AC-9); suite kendi `MONGO_TEST_URI` değeriyle, ayrı sahipli örnekte.
- **Guard:** AC-11; rol adı kümesi üretim profilinden gelir.
- **Build:** `Diten.AuthService` ve `Diten.Web` 0 hata; yerel kapıda `scripts/` Python testleri.
- **Bilinen kırmızılar:** koşu TRX ile alınır, `scripts/known_failures.py check` ile okunur.
- Yetki sınaması `tenant_user` ile yapılır; `platform_admin` geçişi kabul kanıtı değildir.

## 18. Ready-for-dev Checklist

- [x] Golden Reference kararı yazıldı (`none`; form ve DataTable yok).
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`).
- [x] 17 anahtar MOD-0057 tablosuyla mekanik uzlaştırıldı (Bölüm 19).
- [x] `verify_module_id.py --check-id` geçti; kayıt satırı yazıldı (numara sahip teyidine bağlı).
- [x] Layout yok (`shell: none`), Gateway kararı gereksiz, Audited Events yazıldı, yeni sayfa yok, tur yok (UXH-001: sayfa sahiplenilmiyor).
- [ ] D97, D100 ve Y-26 main'de (D62/1).
- [ ] K-1 kayıt yolu ve modül kodu değeri kapandı; (a) ise MOD-0057 Repo Scope'una manifest satırı eklendi.
- [ ] K-2, K-3, K-6, K-7 kapandı.
- [ ] K-4 rol adları MOD-0018 sahibince teyit edildi.
- [ ] MOD-0018 sahibi paket metnini (FU28 numarası dahil) teyit etti; teyit karar dosyasına kaynak olarak yazıldı (D53/1 deseni; D70/3 sıkılaştırmasının buraya uygulanması CT çıkarımıdır).
- [ ] CT-APR-02 bağımsız inceleme bu sürüm için kaydedildi, açık ENGEL yok.
- [ ] CT-APR-01 sürüme bağlı onay satırı; paketi yazmamış onaylayıcı (ilk onay, izin yolu: yüksek risk).
- [ ] MOD-0057 `approved` / `ready-for-dev` — DL-1 kodu için.
- [ ] K-5 üç rolün yedi dilli görünen adı teyit edildi — DL-2 için.

## 19. Implementation Notes

**Anahtar uzlaştırması (2026-10-30, mekanik).** MOD-0057 `:584-598` tablosundaki kısaltmalar açıldı, Bölüm 14.2 ile karşılaştırıldı:
17 = 17, eksik ve fazla yok; roller `:600-603` metniyle aynı (okuyucu, okuyucu + 3 bağ anahtarı, bütün `crm.tag-taxonomies.*` + okuyucu).

**Ölçülemeyenler.** (1) Kiracı verisinde `TagReader`, `TagAssigner`, `TagTaxonomyManager` adlı sistem dışı rol olup olmadığı: canlı veri
okunmadı; Ali'nin önek önerisinin gerekçesi "çakışma riski"dir, koruma AC-6'dır. (2) K-1 kodu için Platform kiracı hakkı yolu. (3) ar, ru, zh
çevirileri. (4) `origin/main` ölçümü son fetch'e dayanır.

**Neden ayrı FU.** D97/3 ve Y-26: FU27 Treasury'ye özgü; MOD-0057 commercial-suite'tir.

**Neden iki dilim.** DL-1 MOD-0057 DL-1 kabul kapısıdır; 7 dil teyidini ve FU27'nin Roller köprüsünü beklemez.

## 20. Follow-up Items

- MOD-0057 O-5 satırı ve D97/3 "kayıt satırı açık" ifadesi bu paket main'e girince güncellenir (bu görevin dışında).
- Rol açıklaması yazılırsa aynı 7 dil kuralına girer (K-8).
- Local Development / Production hak eşitlemesi, rol üyelikleri, token yenileme ve izin / ret duman testi ayrı operasyon kapısı.

## 21. Audited Events

Bu FU yeni yazma komutu eklemez; tablo satırı yoktur. Rol ve hak mutasyonu mevcut hak eşitlemesi yolundan geçer, durumu defterde olduğu
gibi kalır, yeni K2 borcu yazılmaz. Servisin altyapısı bugün: `auth-kullanici-iletimi` (yol b, en iyi çaba) kabul edilmiş; `auth-gunlugu` ve
`auth-rbac-gunlugu` **`aday`** (`tests/architecture/audit-ledger/Diten.AuthService.md`). `read-tenant-wide` iznini role elle bağlama mevcut
rol-izin atama yolundandır (K-7).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| — (yeni yazma komutu yok) | — | — | — | — | — |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | Auth profili — 17 anahtar, 3 rol, 1 açık atama, Viewer dışlaması; AC-1…AC-10; ayrıntı §5.1, §8, §14 | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events |
| DL-2 | rol görünen adları — 3 rol × 7 dil; AC-11, AC-12; ayrıntı §5.2, §14.2 | Repo Scope, Layout & Shell Contract, Frontend File Contract, Validation Rules, Acceptance Criteria, Test Expectations |

DL-2 önkoşulu: DL-1 (guard rol adlarını profilden türetir), K-5, FU27 Roller köprüsü (K-6).

## Açık kararlar

| # | Konu | Seçenekler | CT önerisi | Sahip | Son tarih |
|---|---|---|---|---|---|
| K-1 | Anahtar kayıt yolu ve modül kodu değeri | (a) CrmService'e manifest sağlayıcısı (bugün yok; desen `Diten.ProcurementService.Api/ModuleRegistration/SupplierOnboardingManifestProvider.cs`) + Platform katalog / kiracı hakkı; MOD-0057 Repo Scope revizyonu gerekir · (b) `DataSeeder.cs` `moduleOverride` (CRM'in bugünkü deseni); Bölüm 5.1 koşullu satırı açılır, hak yolu ölçülmeli | **(a)** — hak (entitlement) temelli profil ve Viewer dışlaması ancak kiracı hakkıyla anlam taşır; Y-16/4 yolu. Kod değeri kaynaklarda yok, burada seçilmedi | MOD-0018 sahibi (Ali) + K1 (MOD-0057 adına, D65/1) | DL-1 başlamadan |
| K-2 | Modül hakkıyla Admin / Viewer varsayılanı | hiçbiri · Viewer'a iki `read` · Admin'e tümü | **hiçbiri** (FU27 K-2 deseni; öneri dosyası) | MOD-0018 sahibi | DL-1 başlamadan |
| K-3 | Varsayılan kiracı SuperAdmin tam katalog | bugünkü davranış (açık atama dışı 16 anahtar) · dışla | **bugünkü davranış**; `FullCatalogPermissionGrantService.cs` değişmez | MOD-0018 sahibi | DL-1 başlamadan |
| K-4 | Rol adları | `TagReader`, `TagAssigner`, `TagTaxonomyManager` | **KAPANDI — Y-32 / KARAR D106/4:** MOD-0018 sahibi uygun buldu; kodda ve main paketlerinde çakışma yok (sahip ölçümü) |
| K-5 | 7 dilli görünen ad tablosu | Bölüm 14.2 · sahip düzeltmesi | tablo; tr üçüncü satır MOD-0057'deki "Taksonomi yöneticisi" yerine ekran başlığına uyar ("Etiket taksonomileri"); **ar, ru, zh anadil kontrolsüz** | MOD-0018 sahibi | DL-2 başlamadan |
| K-6 | FU27 ile sıra | (a) FU28 FU27'den bağımsız girer · (b) FU27 sonrası | **(a)** — testler anahtar üyeliğini ölçer, toplam sayıyı değil; FU28 önce girerse kod sayısı 11, son hâl 21. DL-2'nin Roller köprüsü yine FU27'yi bekler ya da DL-2 onu ekler (karar gerekir) | MOD-0018 sahibi + K1 | DL-1 başlamadan |
| K-7 | `read-tenant-wide` elle atamasının denetim izi | D80/2 (b) borcuna bağlanır, FU28'i bekletmez · FU28 için de ön şart | **bağlanır, bekletmez**; kaynaklarda FU28 için karar yok | MOD-0018 sahibi | DL-1 başlamadan |
| K-8 | Rol açıklaması (`Role.Description`) | yazılmaz · 7 dil yazılır | yazılmaz (FU27 K-7 notuyla aynı açıklık) | MOD-0018 sahibi | DL-2 başlamadan |
