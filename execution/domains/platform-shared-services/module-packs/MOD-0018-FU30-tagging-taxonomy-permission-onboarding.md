---
id: MOD-0018-FU30
name: Tagging Taxonomy Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
owner: auth-owner
branch: chore/ct/auth-ct-01
started: 2026-11-01
target: TBD
form_field_count: 0
parent: MOD-0018
consumer_module: MOD-0057
---

# MOD-0018-FU30 — Tagging Taxonomy Permission Onboarding

> **Taslak. Kod izni değildir.** Bu paket izin kataloğu, rol, hak, rol üyeliği, hak eşitlemesi, token, manifest, gezinme,
> yapılandırma ya da kiracı verisi değiştirmez. Onay satırı yok. Kod üç şart birlikte sağlanınca yazılır: (1) bu paket
> `approved` / `ready-for-dev` ve CT-APR-01 onay satırı main'de; (2) MOD-0057 paketi `approved` / `ready-for-dev` ve onay satırı
> main'de (KARAR D53/2: Auth kayıt kodu MOD-0057 onayından sonra, MOD-0057 DL-1 kabulünden önce); (3) Açık kararlar K-1, K-2,
> K-6, K-7 ve K-10 kapalı. `branch` alanı paket metninin dalıdır. Kod dalı dilimde ayrıca açılır (öneri
> `feature/pss/mod-0018-fu30-tagging-taxonomy-permission-onboarding`).
>
> **Sahip kararı ve kaynak.** MOD-0018 sahibi iki ek açmaya söz verdi: önce rol atama eki (FU29), sonra etiketleme (MOD-0057)
> eki. Söz `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-01.md` satır 11'de "iletildi" olarak yazılı. O dosya
> ölçüm anında commit'siz ve main'de değil. Bu ekin açılış sözü ("ek-ac", CT · Auth sohbeti, 2026-11-01) CT görev iletisinden
> alındı. Depoda kaydı yok (Açık kararlar K-16). MOD-0057 tarafı: KARAR D90/3 O-5 (a) ad alanını ve üç rol şablonunu K1 payı
> olarak verdi, MOD-0018 sahibinin kayıt teyidini açık bıraktı (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`
> "## D90", satır 996-1015; MOD-0057 paketi satır 901).
>
> **DCP-002 kimlik notu.** Ön kontrol bu paket yazılmadan hemen önce yeniden koşuldu (2026-11-01, HEAD `a34c18739`, origin/main
> `a6750f575`): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU30 --name "Tagging Taxonomy Permission
> Onboarding" --parent MOD-0018` → `OK  MOD-0018-FU30: proven against Blueprint/registry.` (çıkış 0). Sonuç CT'nin önceki
> koşusuyla aynı. `--discover "Tagging Taxonomy Permission Onboarding"` çıkış 3 ("inceleme gerekli") verdi. Bulgular: Blueprint 1
> (MOD-0057, bu paketin tüketicisi), registry 7 (MOD-0018-FU16…FU22 ve FU27 onboarding ekleri), paket 11 (aynı ekler, MOD-0057 ve
> arşiv dallarındaki FU23 / FU26 / FU-TBD), gateway 1 (`permissions` → 5056), görev 1 (TRM-18, MOD-0057'nin kendi kuyruk satırı).
> Rapor-sha256 `e0f04ccf0c9d1d90643df3f193c4ca3cabd44f193e91b27f3a7a64f8693ef87e`. Mükerrer paket yok. Kayıtta
> (`execution/registries/module-id-registry.md`) `MOD-0018-FU30` satırı **yok**. Doğrulayıcı ana MOD'un FU'sunu satırsız da kabul
> ediyor (FU29 paketinin U-6 ölçümüyle aynı). **Kimlik uyuşmazlığı:** Hazine kulesi aynı kaydı `MOD-0018-FU28` adayıyla anmıştı
> (KARAR D97/3 ve D100/2, commit `51aa2b319` ve `f60522198`, yalnız `origin/chore/ct/treasury-intake-ct-06` dalında, main'de değil).
> Bu paket CT görev iletisine göre FU30'u kullanır. FU28 kaydının geri çekilmesi ya da FU30'a eşlenmesi Açık kararlar K-10'dadır.
>
> **Golden Reference kararı.** Arka uç yetki ve onboarding işi. Yeni sayfa, DataTable ya da ön yüz rotası yok: `shell: none`,
> `golden_reference: none`, `form_field_count: 0` bilinçlidir. Yeni varlık da yok. Dokunulan kayıtlar mevcut `Role` ve
> `RolePermission`'dır. İkisi de kiracıya aittir ve `EntityBase` üzerinde durur (`Role.cs:3`, `RolePermission.cs:3`). Bu yüzden
> `entity_base: EntityBase` yazıldı (FU20 deseni). `Permission` katalog satırı geneldir (`Permission.cs:18`, `GlobalEntityBase`).
>
> **Ölçüm tabanı.** Aksi yazılmadıkça her `dosya:satır` origin/main @ `a6750f575` üzerinde alındı. Çalışma ağacı (HEAD `a34c18739`)
> `services/`, `frontend/` ve `execution/` altında bu commit'ten yalnız FU29 paketiyle ayrılır (`git diff --stat origin/main HEAD`;
> FU29 bu dalda commit'li, main'de değil). Satır numaraları numaralı okumadan ya da `grep -n` çıktısından alındı.

## Module Summary

Bu FU, MOD-0057 (Semantic Tagging & Taxonomy Management, domain `commercial-suite`, servis `Diten.CrmService`) izinlerinin
Auth tarafını yazar. Kaynak MOD-0057 paketinin "Authorization Convention" bölümüdür
(`execution/domains/commercial-suite/module-packs/MOD-0057-semantic-tagging-taxonomy-management.md` satır 569-603, numaralı
okuma). O bölüm "Auth'ta kayıt MOD-0018 sahibinin işidir (D9, D53)" der (satır 602).

| | Kapsam | Dilim |
|---|---|---|
| a | 17 izin anahtarının Auth kuralları: PKS-001 biçimi, joker yok, takma ad yok | DL-1 |
| b | 1 "yalnız açık atama" anahtarı: `crm.tag-assignments.read-tenant-wide` (`ExplicitGrantOnlyPermissions`) | DL-1 |
| c | 2 `read` eylemli anahtarın varsayılan Viewer tabanından dışlanması (`EntitlementOnlyViewerPermissions`) | DL-1 |
| d | Kiracı modül hakkıyla kurulan saf hak profili ve 3 ayrılmış sistem rolü: `TagReader`, `TagAssigner`, `TagTaxonomyManager` | DL-1 |
| e | 3 rolün görünen adı, 7 dilde (en, tr, fr, es, zh, ar, ru) | DL-1 |

**"Kayıt" ne demek.** Bu pakette kayıt, anahtarların Auth kurallarıdır (a–e). Anahtarın kataloğa ilk girişi (beyan) ayrı bir
adımdır ve yolu açıktır (Açık kararlar K-2). FU27 desenine göre beyan, tüketici modülün manifest adımıdır (FU27 Bölüm 1, Y-16/4).
CRM'in bugünkü deseni ise Auth tohum dosyasıdır (`DataSeeder.cs`, `moduleOverride: "crm-…"`; Implementation Notes madde 6).

**Neden profil gerekir (kod gerçeği).** Genel eşitleme bir modül hakkıyla Admin'e modülün bütün anahtarlarını, Viewer'a bütün
`read` eylemli anahtarlarını verir (`EntitlementPermissionSyncService.cs:572-580`). Varsayılan Viewer tabanı, modül hakkı olmayan
kiracıda bile, `read` eylemli her kiracı anahtarını verir (`DefaultRolePermissionTemplate.cs:207-213`; dışlama yalnız
`EntitlementOnlyViewerPermissions`, `:120-168`). Profil olmazsa Admin taksonomi yazma ve bağ yazma izinlerinin ikisini birden alır.
MOD-0057 bu ikisini ayrı rollere koyar (satır 603). Viewer da her kiracıda iki okuma anahtarını alır.

**Kullanıcı:** Kiracı yöneticisi, MOD-0057 modül hakkı açılınca üç hazır rolü rol ekranlarında kendi dilinde görür ve kişilere
elle verir. Kiracı geneli bağ okuması (`read-tenant-wide`) hiçbir rolle gelmez. Yetkili kişi onu bir role elle bağlar.

## Ownership and Boundaries

**Kapsam içi (MOD-0018 sahibi yazar ve kabul eder):**

- `crm.tag-assignments.read-tenant-wide`'in `ExplicitGrantOnlyPermissions` listesine girmesi.
- `crm.tag-taxonomies.read` ve `crm.tag-assignments.read`'in `EntitlementOnlyViewerPermissions` listesine girmesi.
- `crm.tag-` önekli anahtarlar için tam küme doğrulamalı, saf (kalıcılık yazmayan) hak profili ve 3 ayrılmış sistem rol şablonu.
- Profilin mevcut eşitleme yoluna bağlanması: kiracı + kaynak modül sınırlı, idempotent hak verme ve geri alma. Kullanıcıya otomatik
  rol üyeliği yok.
- 3 rolün `SharedResource` görünen adı (7 dil) ve mevcut `RoleNames` köprülerine girişleri.

**MOD-0057 sahibinin (K1, commercial-suite sahibi atanana kadar; KARAR D61, D65/1):**

- Anahtarların beyanı (Açık kararlar K-2) ve modül kodu değeri (K-1).
- `[HasPermission]` zorlaması, SEC-002 kapsam süzgeci (`read-tenant-wide` olmadan yalnız kendi bağları; MOD-0057 satır 413-419,
  AC-26) ve uç davranışları.

**Kapsam dışı:**

- CrmService kodu, Gateway, yeni ekran, gezinme, sayfa yardımı.
- Platform modül kataloğu ve kiracı hakkı kayıtları; `CrmManifestProvider.cs` (Platform sahibi). K-2 (a) seçilirse o dosya
  MOD-0057'nin beyan adımıdır, bu paketin değil.
- Rol üyeliği ataması, token içeriği, Production / Staging işlemleri, hak eşitlemesinin elle çalıştırılması.
- Kaynaktaki `taxonomy.read|manage|assign|approve|publish` iki parçalı anahtarları ve `TAXONOMY_*` takma adları (MOD-0057 satır
  576-578). Taşınmaz, eşlenmez.
- FU27 (Hazine) ve FU29 (rol atama denetimi) kapsamları. Bu paket onların dosyalarına yalnız aşağıdaki Repo Scope satırlarıyla
  dokunur. Sıra bağımlılığı Dependencies'tedir.

## Owned Objects

| Nesne | Sahip | Kesin değişmez |
|---|---|---|
| 17 `crm.tag-*` `Permission` tanımı | Auth genel kataloğu (beyan K-2'ye göre) | Anahtar başına tek etkin genel tanım; `TenantId` yok; `Permission.Module` = K-1 modül kodu; `Scope = Tenant` |
| 1 açık atama anahtarı `crm.tag-assignments.read-tenant-wide` | `ExplicitGrantOnlyPermissions` | Hiçbir otomatik yol (SuperAdmin tam katalog, modül hakkı eşitlemesi, varsayılan rol) vermez; yalnız elle (`Manual`) rol-izin ataması |
| `TagTaxonomyEntitlementGrantProfile` | Auth `Application/Common/Services` | Saf eşleme; `crm.tag-` önekli tam 17 anahtar; eksik / fazla / bilinmeyen eylem / tekil yazım / joker → mutasyondan önce ret |
| 3 ayrılmış sistem rolü (`TagReader`, `TagAssigner`, `TagTaxonomyManager`) | Auth, kiracı başına sistem rolü | Authorization Convention tablosundaki tam küme; kullanıcı ataması otomatik değil |
| Admin / Viewer Etiket varsayılanı | Auth hak profili | Hiçbir `crm.tag-*` anahtarı (öneri, K-3) |
| Modül kaynaklı haklar | Auth | Kiracı sınırlı; `GrantSource = Module`; `SourceModuleCode` = K-1 modül kodu |
| 3 `RoleName_{RoleName}` kaynak anahtarı × 7 dil | Web `SharedResource` | İngilizce dışı değer `en` ile aynı olamaz |

**Yazma komutu yok. Sorgu, uç, DTO, ön yüz rotası yok.** Yeni kalıcı varlık yok. Mevcut `Permission`, `Role`, `RolePermission`,
`UserRole` ve kiracı hakkı modelleri kullanılır.

## Entity Fields

Yeni varlık yok. Mevcut alanların bu paketteki değerleri:

| Nesne / alan | Değer ya da kural |
|---|---|
| `Permission.Key` | Authorization Convention tablosundaki 17 anahtardan biri; küçük harf, noktalı, ≥ 3 parça (PKS-001 §1) |
| `Permission.Module` | K-1 modül kodu; `DefaultRolePermissionTemplate.AdminModules` (`:66`) listesinde olmayan bir değer |
| `Permission.Resource` / `.Action` | `PermissionKeyParser`: ilk parça modül, son parça eylem, aradakiler kaynak. Örnek `crm.tag-taxonomies.terms.deprecate` → kaynak `tag-taxonomies.terms`, eylem `deprecate`; `crm.tag-assignments.read-tenant-wide` → eylem `read-tenant-wide` (`read` değil) |
| `Permission.Scope` | `Tenant` |
| `Role.Name` | 3 ayrılmış ad; kiracı sistem rolü (`IsSystem = true`); aynı adlı sistem dışı rol → çakışma, hiçbir rol / hak yazılmaz |
| `Role.DisplayName` / `.Description` | Saklı değer İngilizce (`en` sütunu; FU20 deseni `ProductAbbreviationEntitlementGrantProfile.cs:39-61`); ekran sistem rolünde kaynak eşlemesini gösterir |
| `RolePermission.TenantId` | Sunucu türetir; boş ya da uyuşmaz → kapalı-başarısız |
| `RolePermission.GrantSource` / `.SourceModuleCode` | `Module` / K-1 modül kodu (elle bağlanan `read-tenant-wide` satırı `Manual`) |

## Repo Scope

Bu planlama görevi yalnız bu paketi yazar. Sonraki, ayrı onaylı teslim aşağıdaki **joker içermeyen** listeyle sınırlıdır.
"yeni" yazılmayan dosya mevcuttur.

**Auth (MOD-0018 sahibi alanı):**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — gerekçeli yeni sabit
  (dosyanın deseni `:10-106`) ve `Keys` kümesine bir öğe (`:108-114`).
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` —
  `EntitlementOnlyViewerPermissions` (`:120-168`) iki satır.
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/TagTaxonomyEntitlementGrantProfile.cs` — **yeni**;
  saf, `crm.tag-` önekli 17 anahtar + 3 rol şablonu (desen `ProductAbbreviationEntitlementGrantProfile.cs:7-89`).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs` — rol listesi
  `ReconciliationRoleNames` (`:96-110`); profil tespiti `GrantModuleWithKeysAsync` (`:151-280`, ör. `:173-197`, `:207-210`);
  `GrantPermissionsToRolesAsync` profil dalı (`:303-336`; açık atama süzgeci `:294` aynen kalır); `RevokeModuleAsync` (`:373-`);
  `ReconcileSpecialProfilesAsync` genel kalan süzgeci (`:582-`, `:610-617`); `ResolveDedicatedRolesAsync` şablon listesi ve ad
  çakışma ön denetimi (`:796-867`, `:828-832`).
- K-2 (b) seçilirse ek: `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` — yalnız 17 katalog satırı
  (`moduleOverride` = K-1 değeri; CRM deseni `:604-606`). Kiracıya hak tohumu yazılmaz.

**Auth testleri:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TagTaxonomyEntitlementGrantProfileTests.cs` — **yeni**.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TagTaxonomyPermissionOnboardingMongoTests.cs` —
  **yeni**; sabit veritabanı `diten_auth_fu30_itest`; ayrı sahipli loopback Mongo; uygulama portu 27017 yasak (FU20 deseni
  `ProductAbbreviationPermissionOnboardingMongoTests.cs:22`, `:202-212`).
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- K-2 (b) seçilirse ek: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Authorization/TagTaxonomyPermissionSeedTests.cs`
  — **yeni** (desen `KnowledgePermissionSeedTests.cs`).
- Salt okunur gerileme: `ProductAbbreviationEntitlementGrantProfileTests.cs`, `ProductLegalEntityScopeEntitlementGrantProfileTests.cs`,
  `ProductIdentityLifecycleEntitlementGrantProfileTests.cs` ve üç `*PermissionOnboardingMongoTests.cs` (aynı klasör);
  `Users/AccountKindTests.cs` (`:62-71`, açık atama listesi).

**Web (rol görünen adları, D79/9 deseni):**

- `frontend/Diten.Web/Resources/SharedResource.en.resx`, `SharedResource.tr.resx`, `SharedResource.fr.resx`, `SharedResource.es.resx`,
  `SharedResource.zh.resx`, `SharedResource.ar.resx`, `SharedResource.ru.resx` — her birine 3 `RoleName_{RoleName}` satırı
  (bugünkü dört satırın yanına; en dosyasında `:567-570`).
- `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`RoleNames` sözlüğü `:152-158`) ve
  `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_IndexL10n.cshtml` (`:32-38`) — 3 giriş.
- `frontend/Diten.Web/Views/Governance/Roles/_IndexL10n.cshtml` ve `frontend/Diten.Web/wwwroot/assets/js/Governance/Roles/index.js`
  — **yalnız K-9'da "FU30 ekler" seçilirse.** Bugün Roller ekranında köprü yok (ölçüm: `grep -n RoleNames` iki dosyada 0).
- `frontend/Diten.Web.Tests/Governance/TagRoleNameL10nGuardTests.cs` — **yeni**; beklenen ad kümesini üretim profilinden türetir
  (desen `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs`).

## Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint çalışma kitapları, DCP'ler, backlog.
- `execution/registries/module-id-registry.md`, `execution/domains/platform-shared-services/domain-config.md`,
  `execution/domains/commercial-suite/domain-config.md` — bu görevde. Kayıt satırı ayrı değişiklikle (Ready-for-dev Checklist).
- Bütün öteki paketler; özellikle MOD-0057, MOD-0018-FU27 (iki dosya), MOD-0018-FU29 ve `docs/records/**`.
- Repo Scope dışındaki bütün Auth dosyaları: token üretimi / yenileme, kullanıcı-rol ve rol-izin işleyicileri (FU29 alanı),
  `FullCatalogPermissionGrantService.cs` (değişmez, K-4 önerisi), öteki profil dosyaları, `appsettings*`, gizli bilgiler.
  `DataSeeder.cs` yalnız K-2 (b) ile ve yalnız 17 katalog satırıyla.
- `services/Diten.CrmService/**`, `services/Diten.Platform/**` (`CrmManifestProvider.cs` dahil), `services/Diten.MdmService/**`,
  `gateway/**` (`ocelot.json` yalnız `integration-agent`).
- `frontend/**` — Repo Scope'taki Web dosyaları dışında; o dosyalarda yalnız rol görünen adı satırları. `_Layout.cshtml` donuk.
- Katalog / hak eşitlemesi çalıştırma, rol üyeliği, kiracı verisi, Production / Staging.

## Dependencies

| Bağımlılık | Durum (ölçüm 2026-11-01) | Etki |
|---|---|---|
| Ana MOD `MOD-0018`; FU13 token / önbellek sınırlı bayatlık | var | Haklar token yenileme ya da yeni girişle görünür |
| MOD-0057 paketi | `status: draft`, `approval_status: not-approved` (satır 19-20); main sürümü sha256 `08cbeb5d…306f`; bağımsız inceleme `UYGUN` (`execution/approvals/reviews.tsv:165`, 2026-10-29); `approvals.tsv`'de satır yok | **Kod kapısı (D53/2).** DL-1 kodu MOD-0057 onayından sonra yazılır; MOD-0057 DL-1 kabulünden önce main'de olmalı (MOD-0057 satır 778, AC-19 satır 684) |
| MOD-0057 paket kapısı (D53/1) | MOD-0057 satır 768: "Auth paket metni main'de — `crm.tag-*` anahtarları, açık atama anahtarı ve rol şablonları" | Bu paketin metni main'de + MOD-0018 sahibinin teyidi = MOD-0057'nin paket kapısı (FU27 D70/3 deseni; K-11) |
| Anahtar beyanı (katalog girişi) | yok; MOD-0057 paketi beyan yolunu adlandırmıyor (`grep -n -i manifest` yalnız kaynak devir dosyası ve menü satırları, 87-89, 337, 782, 788) | K-2. Beyan olmadan profil hiçbir hak vermez (beklenen) |
| CRM modül kataloğu / kiracı hakkı | `CrmManifestProvider.cs:64` `ModuleCode: "CRM"`, `IsBaseline: false` (`:73`) — Platform dosyası | K-1 (a) seçilirse mevcut CRM hakkı yeter; (b) seçilirse Platform'a yeni katalog öğesi gerekir (Platform sahibi, XMC-001) |
| `ExplicitGrantOnlyPermissions` | main'de **13** anahtar (`:108-114`; `grep -c "public const string"` → 13); son ek 3 `leave.*` anahtarı, `5bdf42fa4` (2026-10-31) | Bu ek +1. Sayı kod anında yeniden ölçülür (sıra aşağıda) |
| MOD-0018-FU27 (Hazine) | `draft`; metni main'de; kodu yok. Açık atamaya +10 ekler (FU27 §14.1); `ReconciliationRoleNames` ve `ResolveDedicatedRolesAsync`'e rol ekler; Roller ekranı köprüsünü §5.5'te ekler | Aynı dosyalara dokunur. Hangi paket önce birleşirse öteki yeniden tabanlanır. Açık atama sayısı: FU30 önce → 13 + 1 = 14; FU27 önce → 13 + 10 + 1 = 24 |
| MOD-0018-FU29 (rol atama denetimi) | `ready-for-dev` (bu dalda commit'li, `30351862e`; main'de değil); yeni izin anahtarı yok (FU29 Owned Objects "İzin" satırı, Açık kararlar (f) önerisi) | Dosya çakışması yok. FU29 (f)'de yeni anahtar seçilirse açık atama sayısı yeniden ölçülür |
| Hazine kaydı D97/3, D100/2 | yalnız `origin/chore/ct/treasury-intake-ct-06` (`51aa2b319`, `f60522198`); main'de değil | Kimlik FU28 adayı ve rol adları orada; K-10, K-7 |
| Auth denetim defteri | `auth-kullanici-iletimi` `b`; `auth-gunlugu` ve `auth-rbac-gunlugu` `aday` (`tests/architecture/audit-ledger/Diten.AuthService.md:10-14`) | Bu paket yazma komutu eklemez; defter değişmez (Audited Events) |
| Bağımlı (başka modülün) anahtarları | yok — MOD-0057 v1 MOD-0048, MOD-0023, MOD-0262'yi tüketmez (MOD-0057 satır 148) | Rol şablonlarına çapraz modül anahtarı girmez (K-12) |

## Runtime Constraints

**Modül kodu ve profil seçimi.**

- 17 anahtar tek bir modül koduna aittir (K-1). Profil, FU22'nin bileşik modül sırasını izler
  (`MOD-0018-FU22-product-legal-entity-scope-permission-onboarding.md` satır 169-185): (1) modül kodu ve anahtarlar normalleştirilir;
  (2) `crm.tag-` önekli alt küme tespit edilir; (3) alt küme varsa onaylı 17 anahtarlı kümeye birebir eşit olmalı, yoksa mutasyondan
  **önce** hata (eksik ve fazla anahtarlar adlarıyla); (4) katalog tanımları aynı denetimden geçer; (5) 3 rol adının hepsi çakışma
  için önceden denetlenir (görünür sistem dışı aynı adlı rol → hiçbir rol / hak yazılmaz); (6) genel kalan = modülün bütün kümesi
  eksi `crm.tag-` alt kümesi; (7) Admin-tam / Viewer-okuma davranışı yalnız genel kalana uygulanır; (8) profil 3 rolü idempotent
  oluşturur ve haklarını verir / gereksizleri geri alır; (9) yalnız eşleşen kiracı + kaynak modül satırlarına dokunulur; `Manual`,
  `System`, başka modül ve başka kiracı satırları korunur.
- Profil iki giriş yolundan da tetiklenir: `GrantModuleAsync` (katalogdan modüle göre, `:132-149`) ve `GrantModuleWithKeysAsync`
  (manifest anahtar kümesiyle, `:151-`). İkisi de `GrantPermissionsToRolesAsync`'e iner (`:148`, `:282-`); profil dalı oradadır
  (`:303-336`).
- K-1 (a) "CRM" seçilirse genel kalan CRM'in bugünkü bütün anahtarlarıdır ve davranışları değişmez (AC-13).
- Hak eksik / kapalı / süresi dolmuş → eşleşen modül kaynaklı Etiket hakları 3 rolden kalkar. Ulaşılamaz / bozuk / zaman aşımı /
  belirsiz hak durumu → ne verir ne geri alır; yeniden denenir. İptal belirteci yayılır.
- Hak yalnız rol hakkı yaratır. **Kullanıcıya rol üyeliği otomatik değildir.**

**Varsayılan roller.**

- `crm.tag-assignments.read-tenant-wide` hiçbir otomatik yoldan verilmez: SuperAdmin tam katalog (`FullCatalogPermissionGrantService.cs:39`),
  modül eşitlemesi (`EntitlementPermissionSyncService.cs:294`), varsayılan rol (`DefaultRolePermissionTemplate.cs:181-183`). Profil
  onu hiçbir role koymaz. Konsa bile `:294` süzgeci düşürür. Eylemi `read-tenant-wide`'dır, `read` değil. Viewer kuralına zaten girmez.
- `crm.tag-taxonomies.read` ve `crm.tag-assignments.read` `EntitlementOnlyViewerPermissions`'a girer. Öteki 14 anahtarın eylemi
  `read` değildir (`create`, `update`, `delete`, `bulk-delete`, `activate`, `retire`, `deprecate`, `read-tenant-wide`). Viewer kuralına
  zaten girmezler.
- Modül hakkıyla Admin ve Viewer: hiçbir `crm.tag-*` anahtarı (öneri K-3; FU27 KARAR D79/2 deseni).
- SuperAdmin (yalnız varsayılan kiracı) açık atama dışındaki 16 anahtarı tam katalog yoluyla alır: bugünkü davranış (öneri K-4; FU27
  KARAR D79/3 deseni). `FullCatalogPermissionGrantService.cs` değişmez.
- Modül kodu `AdminModules` (`DefaultRolePermissionTemplate.cs:66`: `access-governance`, `legal-entity`, `crm-account`,
  `crm-contact`, `esign`) listesinde olamaz. Olursa modül hakkı olmayan kiracının Admin'i 16 anahtarı alır.

**Dağıtım sırası (BL-592 §7 deseni).** (1) Auth DL-1 kodu (açık atama + Viewer dışlaması + profil). (2) Anahtar beyanı (K-2).
(3) MOD-0057 DL-1 kabul testleri gerçek izinle (MOD-0057 AC-19). Ters sıra: beyan Auth'tan önce gelirse eski Auth iki `read`
anahtarını her kiracının Viewer'ına verir. Admin modül hakkıyla bütün anahtarları alır. Geri alma ters sırada yapılır.

**Kiracı.** Profil kiracıyı yalnız eşitleme çağrısının `tenantId`'sinden alır. Sabit ya da varsayılan kiracı yok. Başka kiracının
rol / hak satırı okunmaz, yazılmaz, silinmez.

## Layout & Shell Contract

`shell: none`. Bu FU Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez. Hiçbir `.cshtml` dosyası
oluşturulmaz. Yalnız Repo Scope'taki mevcut `_IndexL10n.cshtml` köprülerine rol görünen adı girişleri eklenir. Sayfa, yerleşim
(layout) ve kabuk değişmez. Bu yüzden `Layout = "_LayoutPlatformAdmin"` ya da `Layout = "_LayoutTenantShell"` yazılacak dosya yoktur.

## Backend File Convention

- DL-1 CRUD değildir ve CQRS klasörü açmaz. Profil `Application/Common/Services` altında tek, saf, statik eşlemedir (FU20 / FU22 /
  BL-592 deseni). Kalıcılık yalnız mevcut `EntitlementPermissionSyncService` üzerinden yapılır.
- Profil biçimi `ProductAbbreviationEntitlementGrantProfile.cs:7-89` ile aynıdır: `ModuleCode`, `PermissionPrefix = "crm.tag-"`,
  anahtar sabitleri, `PermissionKeys` (17), `DedicatedRoles` (3 şablon: `RoleName`, `DisplayName`, `Description`, anahtar kümesi),
  `AppliesTo`, `ValidateExactPermissionSet`, önek denetimi. Bir dosyada tek `public static class` ve şablon kaydı mevcut desendeki gibi.
- İptal belirteci depo sınırına kadar taşınır.

## Frontend File Contract

Yeni ekran, sayfa ya da ön yüz rotası yok. Ön yüz işi yalnız 3 rolün görünen adıdır. Rol ekranları (Rol izinleri, Kullanıcı
rolleri; K-9'a göre Roller) sistem rolünün adını kod → kaynak eşlemesiyle gösterir.

**Kullanıcıya görünen metin — 7 dil.** MOD-0057 kiracı modülüdür. Rol ekranları kiracıya görünür. Çekirdek kural 4 gereği 7 dil
(en, tr, fr, es, zh, ar, ru) yazılır. Eşleme FU27 Bölüm 5.5 / 11 desenidir (KARAR D79/9):

- **Kod:** `Role.Name` = `RoleName` (İngilizce PascalCase, sabit, dilden bağımsız).
- **Kaynak anahtarı:** `RoleName_{RoleName}` (ör. `RoleName_TagReader`), `Resources/SharedResource.{lang}.resx`. Ekran kaynağına
  konmaz, çünkü aynı ad birden çok ekranda görünür (localization-standard §3 A).
- **Köprü:** her ekranın `_IndexL10n.cshtml`'indeki `RoleNames` sözlüğü (`window.L10n.RoleNames`). Ekran yalnız `isSystem` rolde
  sözlüğe bakar (`wwwroot/assets/js/Governance/RoleAssignments/index.js:37`, `…/UserRoleAssignments/index.js:99`). Özel rolde
  kullanıcının yazdığı `DisplayName` aynen kalır.
- **Kural:** İngilizce dışı değer `en` ile aynı olamaz ("Placeholder YASAK", localization-standard §1). Çeviri belirsizse teslim
  durur ve MOD-0018 sahibinden kurumsal metin istenir.
- Değerler Authorization Convention "Görünen adlar" tablosundadır. Rol açıklaması (`Role.Description`) kapsam dışıdır (K-15).

## Validation Rules

| Girdi / karar | Zorunlu | Kural | Hata |
|---|---|---|---|
| `crm.tag-` önekli beyan kümesi | Evet | Authorization Convention tablosundaki 17 anahtara birebir eşit | Mutasyondan önce ret; eksik / fazla adlarıyla |
| Önek altında yabancı anahtar (`crm.tag-taxonomies.publish`), tekil yazım (`crm.tag-taxonomy.read`), joker (`crm.tag-taxonomies.*`) | — | Kümede olamaz | Mutasyondan önce ret |
| Katalog tanımı | Evet | 17 anahtarın her biri silinmemiş; `Scope = Tenant`; `Module` = K-1 kodu; `Resource` / `Action` `PermissionKeyParser` ile; anahtar küçük harf (`Ordinal` karşılaştırma, ABB deseni `EntitlementPermissionSyncService.cs:869-891`) | Mutasyondan önce ret |
| Açık atama anahtarı | Evet | `crm.tag-assignments.read-tenant-wide` hiçbir otomatik yolda ve hiçbir rol şablonunda yok | Otomatik hak → test kırmızı |
| Viewer tabanı (hak yok) | Evet | 17 anahtarın hiçbiri | Herhangi biri → test kırmızı |
| Admin / Viewer modül hakkıyla | Evet | Hiçbir `crm.tag-*` anahtarı (K-3) | Fazla hak → test kırmızı |
| Rol şablonu kümeleri | Evet | `TagReader` 2, `TagAssigner` 5, `TagTaxonomyManager` 13 anahtar; taksonomi yazma ve bağ yazma aynı rolde yok | Fark → test kırmızı |
| Rol adları | Evet | 3 ad; aynı adlı sistem dışı rol → bütün profil mutasyonu durur (`EntitlementPermissionSyncService.cs:828-832` deseni) | Rol ve hak yazılmaz |
| Modül kodu | Evet | K-1 değeri; `AdminModules`'te değil | Admin taban testi kırmızı |
| Rol görünen adı | Evet | Her `RoleName` için `RoleName_{RoleName}` yedi `SharedResource` dosyasında boş olmayan değerle; İngilizce dışı değer `en` ile aynı değil; main'de var olan her `RoleNames` köprüsünde giriş var | Eksik dil / boş / en kopyası / köprüde eksik → test kırmızı (AC-14) |

## Failure Path to Verify

- **Beyan kümesi eksik / fazla / tekil yazım / joker / büyük harf:** profil hatası, sıfır rol / hak mutasyonu; eksik ve fazla anahtar
  adlarıyla.
- **Katalogda bir anahtar eksik / silinmiş / farklı (`Scope`, `Module`, eylem):** rol oluşturma ve hak yok.
- **Rol adı çakışması** (`TagReader` ilk, `TagAssigner` orta, `TagTaxonomyManager` son): sıfır yeni rol, sıfır hak; kiracının önceki
  durumu aynen.
- **Etkin hak:** Authorization Convention tablosundaki tam küme; tekrar oynatma kopya üretmez.
- **Hak düştü / kapandı / süresi doldu:** eşleşen Etiket hakları 3 rolden kalkar; başka modül ve elle verilen haklar kalır.
- **Hak belirsiz / ulaşılamaz / bozuk:** ne verir ne geri alır, başarı bildirilmez.
- **Kiracılar arası:** A kiracısının eşitlemesi B'nin rol / hak durumunu yaratmaz, silmez, değiştirmez, göstermez.
- **Beyan Auth'tan önce** (sıra ihlali, birim testte): yeni Auth koduyla iki `read` anahtarı hak olmadan hiçbir Viewer'a gitmez.
- **Eşzamanlılık:** iki eşitleme aynı kiracıya aynı anda gelir: rol ve hak satırları tekil kalır (mevcut idempotent upsert); ad ön
  denetiminden sonra araya giren aynı adlı sistem dışı rol, hak verilmeden önce kapalı-başarısız durur (FU22 satır 175-177 sınırı:
  atanmamış sistem rol iskeleti kalabilir; dağıtık tek yazıcı ayrı iştir).
- **Yetkisiz aktör:** bu paket uç açmaz. Elle `read-tenant-wide` bağlama mevcut rol-izin atama ucunun bugünkü izniyle korunur
  (`auth.roles.assign-permission`); `platform_admin` geçişi kabul kanıtı değildir, testler `tenant_user` ile koşar.
- **Rol görünen adı bir dilde yok / boş / en ile aynı / köprüde yok:** guard testi kırmızı (yakalanmasa ekran o dilde saklı İngilizce
  adı gösterirdi).

## Authorization Convention

- Politika: tüketici uçlarında `[Authorize]` + her eylemde `[HasPermission("…")]` (MOD-0057'nin işi, satır 571). Aktör `tenant_user`.
- Biçim PKS-001: küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. Ad alanı `crm.*`: PKS-001 §4 onu "gelecek iş alanı" olarak
  ayırır (`.antigravity/rules/permission-key-standard.md:103`); commercial-suite domain-config CRM'e bağlar
  (`execution/domains/commercial-suite/domain-config.md:73`). Somut sahip satırı PKS-001'de yok (K-5).
- Joker yok. Kaynaktaki iki parçalı `taxonomy.*` ve `TAXONOMY_*` takma adları taşınmaz (MOD-0057 satır 576-582). Recovery'de
  yayımlanmadıkları için takma ad gerekmez (PKS-001 §3).
- `git grep -c -F "crm.tag-" origin/main -- services frontend gateway scripts tests` → eşleşme yok (çıkış 1). Anahtarlar yeni.

**17 anahtar (MOD-0057 satır 584-598):**

| # | Anahtar | Katman (MOD-0057) | Not |
|---|---|---|---|
| 1 | `crm.tag-taxonomies.read` | T1 | Viewer dışlaması |
| 2 | `crm.tag-taxonomies.create` | T1 | |
| 3 | `crm.tag-taxonomies.update` | T1 | |
| 4 | `crm.tag-taxonomies.delete` | T1 | |
| 5 | `crm.tag-taxonomies.bulk-delete` | T2 | |
| 6 | `crm.tag-taxonomies.activate` | T2 | |
| 7 | `crm.tag-taxonomies.retire` | T3 (PKS-001 §5 örnek listesinde) | |
| 8 | `crm.tag-taxonomies.terms.create` | T1 (alt kaynak `terms`) | |
| 9 | `crm.tag-taxonomies.terms.update` | T1 | taşıma `update` içinde |
| 10 | `crm.tag-taxonomies.terms.delete` | T1 | |
| 11 | `crm.tag-taxonomies.terms.bulk-delete` | T2 | |
| 12 | `crm.tag-taxonomies.terms.deprecate` | T3 (PKS-001 §5 örnek listesinde) | |
| 13 | `crm.tag-assignments.read` | T1 | Viewer dışlaması |
| 14 | `crm.tag-assignments.create` | T1 | |
| 15 | `crm.tag-assignments.delete` | T1 | |
| 16 | `crm.tag-assignments.bulk-delete` | T2 | |
| 17 | `crm.tag-assignments.read-tenant-wide` | T3 (SEC-002 §3.5 kalıbı) | **yalnız açık atama, yalnız okuma**; ad K-6 |

**Rol şablonları — 3 (MOD-0057 satır 600-603; adlar K-7):**

| # | RoleName | Anahtarlar | Sayı |
|---|---|---|---|
| 1 | `TagReader` | `crm.tag-taxonomies.read`, `crm.tag-assignments.read` | 2 |
| 2 | `TagAssigner` | (+1) + `crm.tag-assignments.create`, `crm.tag-assignments.delete`, `crm.tag-assignments.bulk-delete` | 5 |
| 3 | `TagTaxonomyManager` | (+1) + `crm.tag-taxonomies.create`, `.update`, `.delete`, `.bulk-delete`, `.activate`, `.retire`, `crm.tag-taxonomies.terms.create`, `.update`, `.delete`, `.bulk-delete`, `.deprecate` (12 `crm.tag-taxonomies.*` anahtarının tamamı + `crm.tag-assignments.read`) | 13 |

"(+1)" `TagReader`'ın iki anahtarını miras alır. `crm.tag-assignments.read-tenant-wide` hiçbir şablonda yoktur. Taksonomi yazma izni
bağ yazma izni vermez; bağ izni taksonomi değiştirmez (`TagAssigner`'da `read` dışında `crm.tag-taxonomies.*` yok,
`TagTaxonomyManager`'da bağ yazma anahtarı yok). Şablonların birleşimi 16 anahtardır; 17. anahtar açık atamadır. Birleşim + açık
atama = 17, kesişim boş.

**Görünen adlar — 3 rol × 7 dil** (`SharedResource.{lang}.resx` içinde `RoleName_{RoleName}` değeri). Başlangıç çevirileri CT görev
iletisinden alındı. İki hücre ölçüme göre düzeltildi (aşağıdaki not). Sahip teyidinde düzeltilebilir. Düzeltme bu tabloya ve yedi
kaynak dosyaya birlikte yazılır (K-8).

| # | Kod (`RoleName`) | en | tr | fr | es | zh | ar | ru |
|---|---|---|---|---|---|---|---|---|
| 1 | `TagReader` | Tag Reader | Etiket okuyucu | Lecteur d'étiquettes | Lector de etiquetas | 标签查看者 | قارئ الوسوم | Читатель тегов |
| 2 | `TagAssigner` | Tag Assigner | Etiketleyici | Étiqueteur | Etiquetador | 标签标注者 | مُعيِّن الوسوم | Разметчик тегов |
| 3 | `TagTaxonomyManager` | Tag Taxonomy Manager | Taksonomi yöneticisi | Gestionnaire de taxonomies | Gestor de taxonomías | 分类法管理员 | مدير التصنيفات | Менеджер таксономий |

**İki düzeltmenin gerekçesi (ölçüm):** (1) `en` sütunu başlık harfiyle yazıldı ("Tag Reader"). Mevcut dört rol adı
(`SharedResource.en.resx:567-570`: "Super Administrator", "Read Only") ve FU27'nin 27 adı (FU27 §14.2) bu biçimdedir. CT
iletisindeki "Tag reader" biçimi tek başına farklı kalırdı. (2) `ru` "Manager": FU27'de nesne / veri yöneticisi rolleri tek sözcükle
çevrilmiş: `BankDataManager` → "Менеджер банковских данных", `ContractManager` → "Менеджер контрактов". Aynı rollerde fr
"Gestionnaire", es "Gestor", zh "管理员", ar "مدير" kullanılmış (FU27 §14.2 satır 21, 24). Bu tablo fr, es, zh, ar'da aynı sözcüğü
kullanıyor. CT iletisindeki ru "Руководитель" FU27'de kişi / birim başına (`TreasuryManager` "Руководитель казначейства") kullanılır.
Tutarlılık için "Менеджер таксономий" yazıldı. tr "yöneticisi" FU27'deki bütün Manager rolleriyle aynıdır.

## Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. Bu paket uç ya da yöntem eklemez. Rol görünen adı işi yeni uç ya da rota istemez; mevcut
`GET /api/roles` yanıtındaki `name` kodu kullanılır. MOD-0057'nin `/api/crm/tag-*` rotaları MOD-0057 DL-3'ün `integration-agent`
görevidir (MOD-0057 satır 372, 780). `ocelot.json`'a bu paket yazmaz.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) izlenir; bu paketteki her madde yalnız "tanımlı"dır. Her maddenin sabotajı teslim kaydında "kaldır, kırmızı, geri koy, yeşil" biçiminde gösterilir.

- [ ] AC-1 (zorunlu) — `ExplicitGrantOnlyPermissions.Keys` `crm.tag-assignments.read-tenant-wide`'i gerekçeli bir sabitle içerir ve üç otomatik yolun hiçbiri onu vermez: SuperAdmin tam katalog (`FullCatalogPermissionGrantService`), kiracı modül hakkı eşitlemesi (Admin, Viewer ve üç Etiket rolü) ve varsayılan rol seçimi (`DefaultRolePermissionTemplate.SelectFor`); listenin öğe sayısı kodun yazıldığı andaki main tabanının bir fazlasıdır (taban o anda ölçülür; 2026-11-01'de 13). Sabotaj: anahtar listeden çıkarılınca üç yolun testi ayrı ayrı kırmızıya döner.
- [ ] AC-2 (zorunlu) — Modül hakkı olmayan kiracıda varsayılan Admin ve Viewer 17 anahtarlı katalogla hiçbir `crm.tag-*` anahtarı almaz; `crm.tag-taxonomies.read` ve `crm.tag-assignments.read` `EntitlementOnlyViewerPermissions` listesindedir. Sabotaj: iki `read` anahtarından biri listeden çıkarılınca Viewer testi kırmızıya döner.
- [ ] AC-3 (zorunlu) — Etkin modül hakkıyla üç rol Authorization Convention tablosundaki kümeyi birebir alır (rol başına küme eşitliği; sayılar `TagReader` 2, `TagAssigner` 5, `TagTaxonomyManager` 13); hiçbir rolde `crm.tag-assignments.read-tenant-wide` yoktur; otomatik `UserRole` satırı sıfırdır. Sabotaj: profilde bir role bir anahtar eklenince ya da çıkarılınca o rolün testi kırmızıya döner.
- [ ] AC-4 (zorunlu) — Ayrım yapı testi: hiçbir rol taksonomi yazma anahtarını (`crm.tag-taxonomies.*` içinde `read` dışındaki 11 anahtar) bağ yazma anahtarıyla (`crm.tag-assignments.create`, `.delete`, `.bulk-delete`) birlikte taşımaz; `TagAssigner`'da `read` dışında `crm.tag-taxonomies.*` anahtarı, `TagTaxonomyManager`'da bağ yazma anahtarı yoktur. Sabotaj: `TagTaxonomyManager`'a `crm.tag-assignments.create` eklenince test kırmızıya döner.
- [ ] AC-5 (zorunlu) — Modül hakkıyla Admin ve Viewer hiçbir `crm.tag-*` anahtarı almaz (Açık kararlar K-3 önerisi; sahip başka seçerse bu madde onay satırından önce yeniden yazılır); aynı modül kodunun öteki anahtarlarında Admin / Viewer davranışı bugünkü gibidir. Sabotaj: genel kalandan `crm.tag-` alt kümesini dışlayan süzgeç kaldırılınca Admin 16, Viewer 2 Etiket anahtarı alır ve test kırmızıya döner.
- [ ] AC-6 (zorunlu) — `crm.tag-` önekli beyan kümesi onaylı 17 anahtara eşit değilse hiçbir rol / hak yazılmaz ve hata eksik ve fazla anahtarları adıyla verir; beş ayrı test: bir anahtar eksik, yabancı anahtar (`crm.tag-taxonomies.publish`), tekil yazım (`crm.tag-taxonomy.read`), joker (`crm.tag-taxonomies.*`), büyük harfli anahtar (`CRM.TAG-TAXONOMIES.READ`). Sabotaj: tam küme doğrulaması kaldırılınca beş testin her biri kırmızıya döner.
- [ ] AC-7 (zorunlu) — Rol adı çakışma ön denetimi: kiracıda `TagReader` (ilk), `TagAssigner` (orta) ya da `TagTaxonomyManager` (son) adlı sistem dışı bir rol varsa sıfır yeni rol ve sıfır hak yazılır, kiracının önceki rol / hak durumu aynen kalır (parametreli test; FU22 deseni). Sabotaj: ön denetim bir şablon için atlanınca o parametrenin testi kırmızıya döner.
- [ ] AC-8 (zorunlu) — Hak düştü / kapandı / süresi doldu: üç roldeki modül kaynaklı Etiket hakları kalkar; `Manual`, `System`, başka modül ve başka kiracı satırları kalır; elle (`Manual`) bir role bağlanmış `crm.tag-assignments.read-tenant-wide` satırı kalır. Sabotaj: geri alma süzgecinden `GrantSource = Module` koşulu kaldırılınca `Manual` satırı silinir ve test kırmızıya döner.
- [ ] AC-9 (zorunlu) — Belirsiz / ulaşılamaz / bozuk hak durumu ne verir ne geri alır; tekrar oynatma kopya üretmez; A kiracısının eşitlemesi B kiracısının rol / hak durumunu yaratmaz, silmez, değiştirmez. Sabotaj: geri almadaki kiracı süzgeci kaldırılınca B'nin satırı silinir ve yalıtım testi kırmızıya döner.
- [ ] AC-10 (zorunlu) — Profilin `ModuleCode` sabiti Açık kararlar K-1 değeridir; 17 anahtarın katalog tanımı bu modül kodunu, `Scope = Tenant`'ı ve `PermissionKeyParser` ayrımını taşır (`crm.tag-taxonomies.terms.deprecate` → kaynak `tag-taxonomies.terms`, eylem `deprecate`; `crm.tag-assignments.read-tenant-wide` → eylem `read-tenant-wide`); tanımı farklı, silinmiş ya da eksik anahtarda mutasyondan önce ret. Sabotaj: tanım denetimi kaldırılınca test kataloğunda `Scope = PlatformAdmin` yapılmış anahtarla yazılan hak testi kırmızıya döner.
- [ ] AC-11 (zorunlu) — Modül kodu `DefaultRolePermissionTemplate.AdminModules` listesinde değildir (2026-11-01'de `access-governance`, `legal-entity`, `crm-account`, `crm-contact`, `esign`); modül hakkı olmayan kiracının Admin'i bu yüzden Etiket anahtarı almaz. Sabotaj: modül kodu listeye eklenince AC-2'nin Admin testi kırmızıya döner.
- [ ] AC-12 (zorunlu) — Gerçek Mongo testi (`diten_auth_fu30_itest`; ayrı sahipli loopback Mongo, uygulama portu 27017 yasak, FU20 deseni): rol / hak sayıları, tekrar oynatma, geri al / geri yükle, kaynak koruma, kiracı yalıtımı, sıfır otomatik `userRoles`; sahte ya da bellek içi depo bu maddeyi kapatmaz, test atlanmaz. Sabotaj: üç Etiket rolü `ReconciliationRoleNames` listesinden çıkarılınca hak düşürüldükten sonra gerçek Mongo'da Etiket satırları kalır ve test kırmızıya döner.
- [ ] AC-13 (zorunlu) — Gerileme: FU20 ABB (`1/1/3/5/3/2`), FU22 ve BL-592 ürün profillerinin rol matrisleri ile CRM'in mevcut açık atama anahtarlarının (`crm.planned-visit.read-all`, `crm.visit-plan.read-all`, `crm.doctor-insights.read-all`) davranışı değişmez; gerçek test sayıları teslim kaydında. Sabotaj: Etiket önek denetimi `crm.` önekine genişletilince CRM'in öteki anahtarları genel kalandan düşer ve gerileme testi kırmızıya döner.
- [ ] AC-14 (zorunlu) — Görünen ad, 7 dil: `TagRoleNameL10nGuardTests` beklenen rol adı kümesini üretim profilinden türetir (elle yazılmış liste yok) ve her `RoleName` için şunları denetler: (i) `RoleName_{RoleName}` yedi `SharedResource.{lang}.resx` dosyasında (en, tr, fr, es, zh, ar, ru) boş olmayan değerle vardır; (ii) İngilizce dışı altı dilde değer `en` değerinden farklıdır; (iii) main'de var olan her `RoleNames` köprüsünde giriş vardır; (iv) değerler Authorization Convention "Görünen adlar" tablosuyla eşittir. Sabotaj: bir satır `SharedResource.ar.resx`'ten silinince, bir değer `en` ile aynı yapılınca, bir köprüden giriş kaldırılınca ya da profile kaynaksız yeni bir rol eklenince test kırmızıya döner.
- [ ] AC-15 (zorunlu) — Açık atama elle verilebilir: `crm.tag-assignments.read-tenant-wide` insan rolüne verilebilir (`DefaultRolePermissionTemplate.CanBeGrantedToHumanRole` doğru, `Scope = Tenant`), mevcut elle rol-izin atamasıyla (`GrantSource = Manual`) bir role bağlanır ve eşitlemede korunur. Sabotaj: anahtar insan dışı izin listesine (`NonHumanPermissionKeys`) eklenince elle atama testi kırmızıya döner.
- [ ] AC-16 (zorunlu) — Kod kapısı: DL-1 kodu MOD-0057 paketi `approved` ya da `ready-for-dev` olduktan ve iki paketin CT-APR-01 onay satırı main'e girdikten sonra yazılır; teslim kaydı iki paket için `python3 scripts/pack_approval.py check <paket>` çıktısını (çıkış 0) ve paket sha256'sını taşır. Sabotaj: paketin bir boşlukla değiştirilmiş kopyasında `check` çıkış 0 vermez; bu ret çıktısı da kayda yazılır ve o kayıt kabul edilmez.
- [ ] AC-17 (zorunlu) — Kapsam sınırı: DL-1 değişikliğinin dosya listesi Repo Scope listesinin alt kümesidir (`git diff --name-only <taban>..HEAD` çıktısı teslim kaydında); `gateway/**`, `services/Diten.CrmService/**`, `services/Diten.Platform/**`, `FullCatalogPermissionGrantService.cs` ve token üretimi değişmez; `DataSeeder.cs` yalnız Açık kararlar K-2 (b) seçildiyse ve yalnız 17 katalog satırıyla değişir. Sabotaj: listede olmayan bir dosya eklenmiş dalda karşılaştırma farkı gösterir ve teslim kabul edilmez.

## Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Profil birim | 17 anahtar, 3 rol, rol başına küme eşitliği, `crm.tag-` önekli tam küme, eksik / fazla / tekil yazım / joker / büyük harf reddi, katalog tanımı denetimi (AC-3, AC-4, AC-6, AC-10) |
| Taban | Hak olmadan Admin / Viewer'da sıfır Etiket anahtarı; iki `read` anahtarı dışlama listesinde; modül hakkıyla da Admin / Viewer'da sıfır; modül kodu `AdminModules`'te değil (AC-2, AC-5, AC-11) |
| Açık atama | 1 anahtar × 3 otomatik yol; liste sayısı = kod anındaki taban + 1; elle atama ve eşitlemede korunma (AC-1, AC-15) |
| Ön denetim atomikliği | 3 rol adında parametreli çakışma → sıfır mutasyon (AC-7) |
| Hak döngüsü | Etkin / eksik / kapalı / süresi dolmuş / belirsiz; geri yükleme; kaynak koruma; A / B kiracı (AC-8, AC-9) |
| Gerçek Mongo | Ayrı sahipli örnek, sabit veritabanı `diten_auth_fu30_itest`, uygulama 27017 yasak; sahte / bellek içi kanıt kalıcılık iddiasını kapatmaz (AC-12) |
| Gerileme | FU20 / FU22 / BL-592 profilleri ve CRM'in mevcut açık atama anahtarları (AC-13); `Users/AccountKindTests.cs` açık atama listesi |
| Rol görünen adı | `frontend/Diten.Web.Tests` guard testi: üretimden türetilen her `RoleName` × 7 dil, en kopyası yok, köprülerde giriş, tabloyla eşit (AC-14) |
| Süreç | İki paket için `pack_approval.py check` çıktısı; `git diff --name-only` kapsam karşılaştırması (AC-16, AC-17) |
| Derleme / takımlar | Odaklı Auth testleri, tam AuthService takımı, Auth API Release derlemesi, Web derlemesi ve odaklı `Diten.Web.Tests`; gerçek sayılar ve önceden bilinen kırmızılar taban karşılaştırmasıyla |

Kural üretim koduyla ölçülür: testler `DefaultRolePermissionTemplate`, `ExplicitGrantOnlyPermissions`,
`EntitlementPermissionSyncService` ve yeni profilin kendisini çağırır. Test içindeki "beklenen" anahtar listesi yalnız bu paketten
gelir ve üretim kümesiyle eşitlenir. Gösterilemeyen sabotajın koruması koruma sayılmaz.

## Page Help (UXH-001)

Yeni sayfa yok, sayfa yardımı yok. Bu paket mevcut rol ekranlarına yalnız görünen ad kaynağı ekler. Tur gerekmez: "kullanıcı + rol
kurulumu" akışında adım değişmez, listede üç yeni sistem rolü görünür.

## Ready-for-dev Checklist

- [x] Golden Reference kararı yazıldı (`none`; profil FU20 / FU22 deseni).
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`); `target` TBD (K-14).
- [x] Layout & Shell Contract: `.cshtml` oluşturulmuyor, yazılacak `Layout` satırı yok (gerekçe yazılı).
- [x] Validation Rules, Failure Path to Verify (en az 4 senaryo: mükerrer ad, eksik küme, yetkisiz, eşzamanlılık), Authorization
  Convention ve Gateway kararı yazılı.
- [x] Audited Events: yeni yazma komutu yok, gerekçe yazılı.
- [x] Acceptance Criteria kimlikli; her maddede sabotaj.
- [ ] **Kayıt satırı:** `execution/registries/module-id-registry.md`'ye `MOD-0018-FU30` satırı ayrı bir değişiklikle yazıldı ve
  `verify_module_id.py --check-id` yeniden koşuldu.
- [ ] **Kimlik uzlaştırması (K-10):** Hazine kaydındaki `MOD-0018-FU28` adayı (D97/3, D100/2; ct-06 dalı) geri çekildi ya da FU30'a
  eşlendi; MOD-0057 O-5 satırı aynı değişiklikte hizalandı.
- [ ] **Sahip kararı kaydı (K-16):** bu ekin açılışı ("ek-ac") depoda bir karar kaydına yazıldı ve onay satırından önce main'de.
- [ ] MOD-0018 sahibi incelemesi ve teyidi: 17 anahtar, 1 açık atama, 3 rol adı ve kümesi, 7 dilli adlar (K-7, K-8).
- [ ] K-1 modül kodu ve K-2 beyan yolu kapandı (MOD-0057 sahibi; K-1 (b) seçilirse Platform sahibinin XMC-001'i).
- [ ] K-6 `read-tenant-wide` adı kapandı (yayımdan sonra değiştirilemez, PKS-001 §2).
- [ ] K-3, K-4 teyit edildi (öneri: FU27 D79/2, D79/3 deseni).
- [ ] K-9 Roller ekranı köprüsünün sahibi seçildi (FU27 §5.5 ya da bu paket).
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum), `reviews.tsv` satırı, açık ENGEL yok.
- [ ] CT-APR-01 onay satırı (yüksek risk: yetki, ilk onay) — paketi yazmamış ve `high: evet` olan onaylayıcı.
- [ ] **Kod kapısı — DL-1 için:** MOD-0057 `approved` / `ready-for-dev` ve onay satırı main'de (D53/2; AC-16).
- [ ] Ekran yok, tur yok, sayfa yardımı yok (UXH-001: sayfa sahiplenilmiyor).

## Implementation Notes

**Okunan ve ölçülen (origin/main @ `a6750f575`):**

1. MOD-0057 paketi (main sürümü sha256 `08cbeb5d…306f`), numaralı okuma: künye 1-27; Owned Objects 154-231 (uç tablosu 184-208);
   Repo Scope / Protected Paths / Dependencies 319-373 (MOD-0018 satırı 367); Runtime Constraints 375-420 (SEC-002 kapsam cümlesi
   413-419); Authorization Convention 569-603; Acceptance Criteria 662-707 (AC-19 684, AC-26 691, AC-28 693); Ready-for-dev 741-782
   (Auth satırları 768, 778); Açık kararlar 893-909 (O-5 901); Teslim dilimleri 911-959.
2. `ExplicitGrantOnlyPermissions.cs` (`git show origin/main:…`): 13 sabit (`grep -c "public const string"` → 13), `Keys` `:108-114`.
   CRM'in üç `read-all` anahtarı `:73`, `:79`, `:87`. Platform'un iki anahtarı `platform.tasks.read-all` (`:29`) ve
   `platform.tasks.work-report.read-tenant-wide` (`:42`). Son değişiklik `5bdf42fa4` (2026-10-31, üç `leave.*` anahtarı).
3. `DefaultRolePermissionTemplate.cs`: `AdminModules` `:66`; `EntitlementOnlyViewerPermissions` `:120-168`; `SelectFor` `:175-217`
   (açık atama süzgeci `:181-183`, Viewer `read` kuralı `:207-213`).
4. `EntitlementPermissionSyncService.cs` (927 satır): rol listeleri `:93-110`; `GrantModuleAsync` `:132-149`; `GrantModuleWithKeysAsync`
   `:151-`; açık atama süzgeci `:294`; profil dalı `:303-336`; `SelectForRole` `:572-580`; `ReconcileSpecialProfilesAsync` `:582-`
   (genel kalan `:610-617`); `ResolveDedicatedRolesAsync` `:796-867` (ad çakışması `:828-832`); ABB tanım denetimi `:869-891`.
5. `ProductAbbreviationEntitlementGrantProfile.cs:1-89` (profil biçimi); `FullCatalogPermissionGrantService.cs:39` (açık atama
   süzgeci); `ProductAbbreviationPermissionOnboardingMongoTests.cs:22`, `:202-212` (sabit veritabanı, loopback ve 27017 reddi).
6. CRM anahtar beyanı bugün iki yoldan: (a) Platform `CrmManifestProvider.cs:64` (`ModuleCode: "CRM"`, sayfa eylemleri, ör. `:154`,
   `:164`, `:181`); (b) Auth `DataSeeder.cs` `moduleOverride: "crm-…"` (dokuz değer: `crm-account` 9, `crm-contact` 13,
   `crm-content-composition` 10, `crm-doctor-insights` 5, `crm-knowledge` 17, `crm-planned-visit` 4, `crm-territory` 5,
   `crm-visit-plan` 1, `crm-visit-report` 1 satır; ör. `:604-606`). Platform kanonik anahtar denetimi ≥ 3 parça kabul eder
   (`ModulePageDescriptorNormalizer.cs:66-74`; `ModulePageActionDescriptorRequestValidator.cs:55-64`): 4 parçalı
   `crm.tag-taxonomies.terms.*` anahtarları manifest yolundan geçer.
7. Web: `SharedResource.en.resx:567-570` ve `SharedResource.tr.resx:567-570` dört `RoleName_*` satırı; köprüler
   `RoleAssignments/_IndexL10n.cshtml:151-158`, `UserRoleAssignments/_IndexL10n.cshtml:31-38`; Roller ekranında köprü yok
   (`Roles/_IndexL10n.cshtml` ve `Roles/index.js`'te `grep -n RoleNames` 0 satır).
8. Karar kayıtları: D53 (`treasury-intake-owner-decisions-05.md:524-532`), D90 (`:996-1015`); D9 `treasury-intake-owner-decisions-02.md:14`
   (yalnız başlık okundu). D97/3 ve D100/2 yalnız `git show 51aa2b319` ve `git show f60522198` ile okundu (ct-06 dalı).
9. Öneri belgesi `docs/roadmap/plans/erp-finance-legal-module-intake-01/proposals/05-MOD-0057-auth-registration.md` (main, 62 satır):
   aynı 17 anahtar; rol adları `TagReader` / `Tagger` / `TaxonomyManager` (satır 41-43); 7 dil tablosu (satır 51-53).
10. FU27 ana sürümü (main): yapı örneği; §5.1 `:363-365`, §5.5 `:515-540`, §8.1-8.3 `:586-624`, §14.1 `:873-888`, §14.2 `:890-996`,
    Teslim dilimleri ve Açık kararlar `:1409-1456`. FU22 §8 `:167-185`. FU29: kabul ölçütü ve sabotaj yazım biçimi, Teslim dilimleri,
    Açık kararlar.

**Makineyle okunan desenler ve sonuçları (bu oturumda koşuldu):**

- `git grep -c -F "crm.tag-" origin/main -- services frontend gateway scripts tests` → eşleşme yok (çıkış 1).
  `git grep -l -F "crm.tag-" origin/main` → 3 dosya (karar dosyası -05, öneri 05, MOD-0057 paketi).
- `git grep -n -w -E "TagReader|TagAssigner|TagTaxonomyManager" origin/main` → yalnız öneri 05 satır 41, 42, 43, 51 (hepsi
  `TagReader` geçtiği için). `TagAssigner` ve `TagTaxonomyManager` main'de 0. `git log --all -S "TagAssigner"` → `51aa2b319`
  (D97/3), `528f0601e` (inceleme kaydı).
- `git log --all -S "MOD-0018-FU30"` → boş. `git log --all -S "MOD-0018-FU28"` → `0ef7fbe8b` (FU29 paketi), `f60522198`, `8321c7709`.
  `git log --all --name-only -- "…/MOD-0018-FU28*" "…/MOD-0018-FU30*"` → boş (hiçbir dalda paket dosyası yok).
- Teslim dilimleri tablosu ve Acceptance Criteria biçimi, yazımdan sonra depo okuyucularıyla ölçüldü
  (`scripts/pack_approval.py` `slices()`, `scripts/acceptance_record.py` `parse_pack()`); sonuç teslim raporundadır.

**Ölçüm uyuşmazlıkları (paket metniyle düzeltilmedi, kaydedildi):**

- U-1: `ExplicitGrantOnlyPermissions` main'de 13 anahtar taşıyor. FU27 §5.1 ve §14.1 "bugün 10 → 20" (`:363-365`, `:888`), D97/3
  "20 + 1 = 21" diyor. Fark 2026-10-31'de eklenen üç `leave.*` anahtarıdır. FU27 metni bu görevde değiştirilmedi.
- U-2: Kimlik. Hazine kaydı aynı eki `MOD-0018-FU28` adayıyla anıyor (ct-06, main'de değil). Bu paket FU30'dur (K-10).
- U-3: Rol adları. Main'deki öneri 05 `Tagger` / `TaxonomyManager` diyor. D97/3 (ct-06) K1 seçimini `TagAssigner` / `TagTaxonomyManager`
  diye yazıyor ve Ali teyidine bırakıyor. Bu paket CT iletisi ve D97/3'teki adları kullanır (K-7). Kodda ve paketlerde çakışma yok.
- U-4: Görünen adlarda CT iletisinden iki sapma: `en` başlık harfi ve `ru` "Менеджер таксономий" (Authorization Convention notu; K-8).
- U-5: MOD-0057 main sürümünde D97/3 notu (ayrı ek, aday FU28, rol adları) yok. Not yalnız ct-06 dalında (`51aa2b319`).
- U-6: MOD-0057 anahtar beyan yolunu adlandırmıyor. FU27 deseni manifest; CRM'in bugünkü deseni Auth tohum dosyası (madde 6; K-2).
- U-7: Roller ekranı `RoleNames` köprüsünü hâlâ kullanmıyor (FU27 §5.5 ölçümü main'de de doğru; K-9).
- U-8: `MOD-0018-FU30` kayıt satırı yok ama doğrulayıcı `OK` veriyor (DCP-002 notu).

**Plan hizalaması.** Teslim dilimleri tablosundaki `plan yetenek kimliği` ve `plan MVP` hücreleri **ölçülmedi**: plan çalışma kitabı
depoda değil (`docs/roadmap/plans/ct-plan-alignment-01/PROPOSAL.md:15-16` plan sayfalarını anar, dosyayı vermez). MOD-0057 paketi
de plan satırı anmıyor. Değer uydurulmadı. Plan okunduğunda kaynak (sürüm ve satır) buraya yazılır.

**Koşturulmayan:** derleme, test ve dev sunucusu (görev kuralı). Sayılar metin ölçümüdür, test çıktısı değildir.

**Neden ayrı FU.** FU27 Hazine'ye özgüdür (K1 / Ali kararı D97/3: "FU27'ye eklenmez"). FU16–FU22 ürün modülüne aittir. MOD-0057
başka domain'in (commercial-suite) yeni modül kodu, üç rol ve bir açık atama getirir.

## Follow-up Items

- PKS-001 §4 ad alanı tablosuna `crm.*` → `Diten.CrmService` somut satırı (korumalı yol, sahip onayı, ayrı değişiklik; K-5).
- PKS-001 §5 Tier 3 listesine kiracı geneli okuma eyleminin tek adı (`read-tenant-wide` ya da `read-all`; K-6) ve CRM'in mevcut
  `read-all` anahtarlarının hizalanması (PKS-001 §3 takma ad kuralıyla; ayrı iş).
- FU27 ve bu paketin rol görünen adı guard testlerinin tek ortak guard'da birleşmesi (iki profil, tek kaynak denetimi).
- Rol açıklamasının (`Role.Description`) 7 dilde gösterilmesi (K-15; FU27 K-7 notu ile ortak iş).
- Local Development / Production hak eşitlemesi, rol üyelikleri, token yenileme ve izin / ret duman testi: ayrı operasyon kapısı.
- MOD-0057 v2'de etiketleme kuralları ve onay iş akışı gelince (MOD-0057 Follow-up) yeni anahtarlar ve roller bu pakete revizyon ya
  da yeni FU olur.

## Audited Events

**Servisin denetim altyapısı bugün:** `auth-kullanici-iletimi` (yol `b`, en iyi çaba) kabul edilmiş; `auth-gunlugu` ve
`auth-rbac-gunlugu` `aday` (`tests/architecture/audit-ledger/Diten.AuthService.md:10-14`).

**Bu paket yeni yazma komutu eklemez ve mevcut bir yazma komutunu değiştirmez.** Rol ve hak satırları mevcut hak eşitlemesi
yoluyla yazılır (`EntitlementPermissionSyncService`). Bu yol bir işleyiciden geçmez ve kendi denetim durumu vardır (FU29 Ownership
and Boundaries, "Sistem yolları"). Durumu defterde olduğu gibi kalır. Yeni K2 borcu yazılmaz. FU27 Bölüm 21'deki DL-1…DL-4
gerekçesiyle aynıdır. `read-tenant-wide`'ın bir role elle bağlanması mevcut `AssignPermissionCommand` ile yapılır. O komutun denetimi
FU29'un kapsamıdır (bugün `## Bilinen borç`).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| (yeni yazma komutu yok) | — | — | — | — | — (sayılacak komut yok; Owned Objects) |

- **Kayda yazılmayan:** bu paket kayıt yazmaz.
- **Okuma denetimi:** yok; bu paket okuma ucu açmaz.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek dilim — 17 `crm.tag-*` anahtarının Auth kuralları: 1 açık atama, 2 Viewer dışlaması, kiracı modül hakkıyla 3 rol şablonu (TagReader, TagAssigner, TagTaxonomyManager) ve 3 rolün 7 dilli görünen adı; AC-1…AC-17; kod kapısı: MOD-0057 approved / ready-for-dev ve iki paketin CT-APR-01 onay satırı main'de (D53/2) | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria, Test Expectations, Audited Events | E4 | ölçülmedi | ölçülmedi |

Tek dilim. DL-1 bu paketin çekirdek dilimidir ve tamamıdır. Kanıt seviyesi E4'tür: kalıcılık (gerçek Mongo), RBAC / kiracı ve hata
yolları (`docs/guides/operations/control-tower-sop.md:1652`). **Kod kapısı:** MOD-0057 onayı (D53/2). **Tüketici kapıları:** bu
paketin metni main'de + MOD-0018 sahibinin teyidi = MOD-0057'nin paket kapısı (MOD-0057 satır 768; FU27 D70/3 deseni). Bu paketin
DL-1 kodu main'de = MOD-0057 DL-1'in kabul kapısı (MOD-0057 satır 778, AC-19). **Sıra:** Auth DL-1 kodu, sonra anahtar beyanı
(K-2), sonra MOD-0057 DL-1 kabulü. Plan hücreleri Implementation Notes "Plan hizalaması"ndadır.

## Açık kararlar

| # | Konu | Seçenekler | CT önerisi (bu paketin önerisi; karar değil) | Sahip | Bağımlılık / son tarih |
|---|---|---|---|---|---|
| K-1 | Modül kodu (`Permission.Module`, profilin `ModuleCode`, kiracı hakkının kodu) | (a) mevcut `CRM` kodu (`CrmManifestProvider.cs:64`, `IsBaseline: false`); profil `crm.tag-` alt kümesini sahiplenir, CRM'in öteki anahtarları genel kalanda (FU22 bileşik modül deseni) · (b) MOD-0057'ye ayrı kod (ör. `crm-tagging`; CRM tohum deseni `crm-knowledge` vb.); Platform'a yeni katalog öğesi ve ayrı kiracı hakkı gerekir | **(a).** CRM hakkı olan kiracı etiketlemeyi hazır alır, Platform'a yeni katalog öğesi gerekmez. (b) ayrı satış birimi isterse seçilir. Her iki seçenekte kod `AdminModules`'te olamaz (AC-11). Ölçülmedi: CRM manifest anahtarlarının Auth'taki `Permission.Module` değeri (DL-1 başında ölçülür) | MOD-0057 sahibi (K1, D61 / D65/1); (b)'de Platform sahibi (XMC-001); teyit MOD-0018 sahibi | DL-1 başlamadan |
| K-2 | Anahtar beyanı (kataloğa ilk giriş) | (a) Platform `CrmManifestProvider` sayfa eylemleri (FU27 Y-16/4 deseni: manifest → Platform kataloğu → Auth eşitlemesi; Platform dosyası, XMC-001) · (b) Auth `DataSeeder.cs` katalog satırları (`moduleOverride`; CRM'in bugünkü deseni, Auth'ta, bu paketin Repo Scope'una ek satır) | **(a)**, K-1 (a) ile. Hak eşitlemesi manifest anahtar kümesini doğrudan alır (`GrantModuleWithKeysAsync`) ve beyan tüketici modülün adımı olarak kalır. Beyan MOD-0057'nin bir dilimine (DL-4 menü / manifest maddesi, satır 337, 782) ya da ayrı Platform değişikliğine yazılır. Sıra: Auth DL-1 önce | MOD-0057 sahibi + Platform sahibi (Beste, XMC-001); teyit MOD-0018 sahibi | DL-1 başlamadan |
| K-3 | Modül hakkıyla Admin / Viewer Etiket varsayılanı | hiçbiri · yalnız `TagReader` kümesi · genel davranış (Admin hepsi, Viewer `read`) | **Hiçbiri** (FU27 KARAR D79/2 deseni). Üç rol şablonu 16 anahtarın hepsini yerleştirir; Admin'e fazladan verilen her anahtar ayrımı (MOD-0057 satır 603) bozar | MOD-0018 sahibi | DL-1 başlamadan |
| K-4 | Varsayılan kiracı SuperAdmin tam katalog yolu | bugünkü davranış (açık atama dışı 16 anahtar) · Etiket anahtarlarını dışla | **Bugünkü davranış** (FU27 KARAR D79/3 deseni; varsayılan kiracı iş kiracısı değil); `FullCatalogPermissionGrantService.cs` değişmez | MOD-0018 sahibi | DL-1 başlamadan |
| K-5 | `crm.*` ad alanının sahipliği | PKS-001 §4 bugün `crm.*`'ı "gelecek iş alanı" diye ayırır (`permission-key-standard.md:103`); commercial-suite domain-config CRM'e bağlar (`domain-config.md:73`); KARAR D90/3 O-5 (a) | Anahtarlar `crm.*` altında geçerli sayılır (domain-config + D90/3). PKS-001 §4'e somut satır (`crm.*` → `Diten.CrmService`) ayrı, sahip onaylı `.antigravity` değişikliğidir (Follow-up). DL-1'i bekletmez | commercial-suite sahibi (K1) + `.antigravity` sahibi | Paket onayı (bilgi); PKS-001 satırı ayrı |
| K-6 | Kiracı geneli okuma eyleminin adı | (a) `read-tenant-wide` (MOD-0057 satır 596, SEC-002 §3.5 kalıbı; FU27'de 8 anahtar; `platform.tasks.work-report.read-tenant-wide`) · (b) `read-all` (CRM'in açık atama anahtarları `crm.planned-visit.read-all`, `crm.visit-plan.read-all`, `crm.doctor-insights.read-all`; `platform.tasks.read-all`). PKS-001 §5 Tier 3 listesinde ikisi de yok | **(a) `read-tenant-wide`.** Anlamı ("kiracının tamamı") adında açık; tüketici paketin yazdığı ad ve FU27 ile aynı. CRM'deki tutarsızlık ayrı iştir (Follow-up). **Yayımdan sonra ad değiştirilemez** (PKS-001 §2): kod öncesi kapanır | MOD-0057 sahibi (ad alanı) + MOD-0018 sahibi | DL-1 başlamadan |
| K-7 | Rol adları | `TagReader` / `TagAssigner` / `TagTaxonomyManager` (D97/3 K1 seçimi, ct-06; CT iletisi) · `TagReader` / `Tagger` / `TaxonomyManager` (öneri 05, main) | **`TagReader` / `TagAssigner` / `TagTaxonomyManager`.** Üçü de `Tag` önekli; `TaxonomyManager` MOD-0162'nin "Konu taksonomisi"yle karışabilirdi (MOD-0057 satır 428-431). Kodda ve paketlerde çakışma yok (ölçüm, Implementation Notes). `RoleName` yayımdan sonra sabit koddur | MOD-0018 sahibi | DL-1 başlamadan |
| K-8 | 7 dilli görünen adlar | Authorization Convention tablosu (CT iletisi + iki ölçümlü düzeltme) · başka metin | **Tablo.** Kurumsal metin istenirse tablo ve yedi dosya birlikte düzeltilir | MOD-0018 sahibi | DL-1 başlamadan |
| K-9 | Roller ekranındaki `RoleNames` köprüsü | (a) FU27 §5.5 ekler, bu paket yalnız giriş ekler · (b) bu paket ekler · (c) önce birleşen ekler, öteki yalnız giriş ekler | **(c).** Tek kopya kalır; sıra kararı gerekmez. Köprü yokken bu paketin girişleri iki ekranda görünür, Roller ekranı saklı İngilizce adı gösterir (bugünkü dört rolde de böyle) | MOD-0018 sahibi + Hazine CT (FU27) | DL-1 başlamadan |
| K-10 | Kimlik uzlaştırması FU28 → FU30 | Hazine kaydı (D97/3, D100/2) ve MOD-0057 O-5 notu (ct-06) FU28'i aday yazıyor · bu paket FU30 | **FU30 kalır** (CT görev iletisi; `--check-id` OK; FU28 hiçbir dalda paket dosyası değil). Hazine CT, D97/3 notunu "FU30" diye düzeltir ve FU28 adayını geri çeker; MOD-0057 O-5 satırı aynı değişiklikte hizalanır. Kayıt satırı FU30 için yazılır | MOD-0018 sahibi + Hazine CT | Onay satırından önce |
| K-11 | MOD-0057 onay durumu ve iki kapı | MOD-0057 `draft` / `not-approved`; inceleme `UYGUN` @ `08cbeb5d`; onay satırı yok | Kapılar D53'ün iki parçası: bu paketin metni main'de + sahip teyidi = MOD-0057 paket kapısı; bu paketin DL-1 kodu main'de = MOD-0057 DL-1 kabul kapısı. Bu paketin kodu MOD-0057 onayından sonra (AC-16) | MOD-0057 sahibi (K1) + onaylayıcı; MOD-0018 sahibi (teyit) | DL-1 kodu öncesi |
| K-12 | Bağımlı (başka modülün) anahtarları | yok · MOD-0057 ileride tükettiği modülün anahtarını isterse çapraz modül bağımlılığı (FU27 K-4 (b) deseni) | **Yok** (MOD-0057 satır 148: MOD-0048, MOD-0023, MOD-0262 v1'de tüketilmez). v2'de tüketici gelirse revizyon | MOD-0018 sahibi (teyit) | DL-1 başlamadan |
| K-13 | Rollerin kiracı modül hakkıyla verilmesi | (a) profil, kiracı modül hakkı açılınca 3 sistem rolünü anahtarlarıyla oluşturur; üyelik elle (FU20 / FU22 deseni, `ResolveDedicatedRolesAsync` `:796-867`) · (b) roller tohumla her kiracıda · (c) roller elle kurulur | **(a).** Hak açılınca 3 sistem rolü anahtarlarıyla oluşur; hak düşünce modül kaynaklı anahtarlar kalkar; rolün kendisinin kalıp kalmadığı mevcut desenle aynıdır ve DL-1 başında ölçülür; kullanıcı üyeliği otomatik değil | MOD-0018 sahibi | DL-1 başlamadan |
| K-14 | Hedef tarih | — | TBD | MOD-0018 sahibi | Onay öncesi |
| K-15 | Rol açıklamasının dili (`Role.Description`) | saklı İngilizce (bugünkü desen) · 7 dil | **Saklı İngilizce, kapsam dışı** (FU27 K-7 notu ile aynı); 7 dil istenirse FU27 ile ortak iş (Follow-up) | MOD-0018 sahibi | Bilgi |
| K-16 | Ekin açılış kararının kaydı | "ek-ac" (CT · Auth sohbeti, 2026-11-01) depoda yok; FU29 karar taslağı satır 11 iki eki "iletildi" diye yazıyor, dosya commit'siz | Karar kaydı (FU29 (l) deseni) bu ekin açılışını doğrudan sahip sözüyle yazar ve onay satırından önce main'e girer. Bu paket kaydı yazmaz | MOD-0018 sahibi + CT · Auth | Onay satırından önce |
