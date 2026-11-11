# MOD-0119 / MOD-0120 — tenant ve legal-entity izolasyonu: hedef taraf kanıtı

> **Amaç.** Kaynak projedeki MOD-0119 (Accounts Payable) güvenlik düzeltmesini ve
> MOD-0120 entegrasyonunu yazan geliştiriciye, Recovery'de **fiilen ölçülmüş**
> davranışı vermek. Bu dosya tasarım önerisi değil; her satırı dosya/satır kanıtına
> bağlı bir ölçüm tutanağıdır. Öneri olan kalemler §11'de ayrıca işaretlidir.
>
> **Ölçüm anı.** 2026-10-12
> **Repository HEAD.** `2acce233bc1910e47c3c8f01d5cab017e4316447`
> **Dal.** `feature/mvp6-logistics` — ⚠ `main` değil. `main`'de aynı olduğu ölçülmedi.
> **Yöntem.** Salt okunur. Bu ölçüm sırasında hiçbir dosya değiştirilmedi, staged
> veya commit edilmedi.

---

## 1 · Üç sorunun cevabı, tek tabloda

| soru | cevap | statü |
|---|---|---|
| Onaylı LE kapsamı sözleşmesi (uç/arayüz + sürüm) | **yok** | — |
| Çağrı kimliği + gerekli izin | **karar bekliyor** | Q470 açık |
| Eksik bağlam / kapsam dışı / bağımlılık hatası davranışı | **onaylı sözleşme yok** — uygulanmış referans var | mutabakat |
| Boş kapsam yorumu | **sıfır satır, asla filtresiz** | kural seviyesinde onaylı |
| Tenant çapında erişim için açık yetki | **var**, modüle özel | onaylı (sahip kararı) |
| Banka hesabı erişim kapsamı | **bulunamadı** — varlık yok | — |
| Supplier seviyesinde kullanıcı kapsamı | **bulunamadı** — yalnız tenant+LE izolasyonu var | — |

---

## 2 · Üç katmanı karıştırmayın

Kaynak taraftaki tartışmanın tamamı bu ayrımın üstünde döndü. Recovery'de üçünün de
ayrı yeri var:

| katman | ne cevaplar | Recovery'deki yer |
|---|---|---|
| **1 · kullanıcının LE yetkisi** | kullanıcı hangi LE'leri görebilir | `Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs:238` |
| **2 · LE'nin geçerliliği** | bu LE tenant'ın mı, Active mi, referenceable mı | `Diten.SupplyChainService.Infrastructure/Common/MdmLegalEntityScopeValidator.cs` |
| **3 · veri izolasyonu** | sorguya hangi filtre girer | `Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityRepository.cs:16` |

**MDM'in `Valid` cevabı, kullanıcının o LE'ye yetkili olduğunu göstermez.** İstekteki LE
kimliği de yetki kanıtı değildir. Katman 2 geçerliliği, katman 1 yetkiyi söyler.

⚠ **Ölçülmüş açık:** Recovery'de SupplyChain'de katman 2 ve 3 var, **katman 1 yok**.
`Scoped<T>()`'in kullandığı `LegalEntityId`, middleware'in header'dan alıp MDM'e
doğrulattığı değerdir; kullanıcının yetkili kümesiyle hiçbir yerde kesiştirilmez.
`IDataScopeResolver`'ın tüm üretim tüketicileri `Diten.Platform` assembly'lerinin
içindedir.

---

## 3 · Boş kapsam nasıl yorumlanır

**Sıfır satır. Asla filtresiz.** Kodda yazılı:

```csharp
// Diten.Platform.Application/Features/Tasks/Services/WorkReportScope.cs:55-62
public bool MatchesNothing => !TenantWide
    && OrganizationUnitIds.Count == 0
    && PositionIds.Count == 0
    && UserIds.Count == 0;

/// <summary>The fail-closed answer: no scopes resolved, so no rows.</summary>
public static WorkReportScope Empty { get; } = new(false, ...);
```

Gerekçe aynı dosyanın başlığında (`:22-24`):

> *"The dangerous default is not 'refuse'; it is 'no filter', which in a report reads
> as success."*

**Testler:**
```
Diten.Platform.Application.Tests/Tasks/WorkReportScopeTests.cs
  No_scopes_at_all_means_NOTHING_rather_than_everything
  An_empty_scope_matches_no_row_even_when_rows_exist
  An_EXCLUDE_scope_collapses_the_answer_instead_of_being_ignored
```

**Statü ayrımı.** Kural seviyesinde onaylı: `.antigravity/rules/data-scope-enforcement.md`
§3.3 bunu genel kural olarak koyar, §4 sert kapı tanımlar (kapsam cümlesi + kapsamı
soran kod + kanıtlayan test olmadan `ready-for-dev` yok). **Kod örneği yalnız Work
Report'ta**; AP / banka hesabı / supplier için örnek yok, çünkü o varlıklar Recovery'de
yok (§5).

---

## 4 · Tenant çapında erişim yetkisi

**Var.** Ve şekli, kopyalanması gereken kısım.

| | |
|---|---|
| permission adı | `platform.tasks.work-report.read-tenant-wide` |
| tanım | `Diten.Platform.Application/Features/Tasks/TaskModels.cs:117` |
| kontrol yolu | `Diten.Platform.Application/Features/Tasks/Services/WorkReportScopeSource.cs:121` |
| tek üretim yeri | `WorkReportScope.TenantWideScope()` — `WorkReportScope.cs:72` |
| otomatik-grant yasağı | `Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs:42` |

```csharp
// WorkReportScopeSource.cs:121-126
if (_permissions.IsPlatformActor || _permissions.Has(TaskPermissions.WorkReportReadTenantWide))
{
    return WorkReportScope.TenantWideScope();
}
return await ResolveOwnScopeAsync(ct);
```

### Devredilebilir üç karar

**(a) Otomatik yoldan asla verilmez.** `ExplicitGrantOnlyPermissions` sınıf başlığı:

> *"Permissions that must never be granted through an automatic pathway —
> full-catalog/SuperAdmin, module-entitlement sync, or default role provisioning. Only
> an authorized person's explicit role-permission assignment may grant one."*
> (sahip kararı 2026-09-11; Work Report anahtarı için 2026-09-14)

Kayıtlı gerekçe: anahtar bu listede olmasa, modül yetkilendirildiği anda
module-entitlement sync onu her tenant Admin'ine, full-catalog yolu da ilk sync'te
SuperAdmin'e verirdi — ve kapsamın daraltacak bir şeyi kalmazdı.

**(b) Boolean değil, ayrı fabrika.** `TenantWideScope()` ayrı bir metottur, çünkü
(`WorkReportScope.cs:68-71`):

> *"A separate factory rather than a boolean argument, so a call site cannot widen the
> scope by passing `true` from a variable that once meant something else."*

**(c) İzin yoksa 403 değil.** Eksik izin, çağıranın kendi kapsamına düşüşle
cevaplanır; `WorkReportScopeSource.cs:117-119` bunu yetki yükseltme denemesi değil
*görüntüleme tercihi* sayar.

### MOD-0119 için sonuç

⚠ Yetki **modüle özeldir** (`platform.tasks.work-report.*`). Yeniden kullanılabilir
**genel bir tenant-wide izni yok**. MOD-0119 kendi anahtarını tanımlamak ve onu (a)
maddesine kaydetmek zorundadır; kaydetmezse yukarıdaki sessiz yayılma gerçekleşir.

---

## 5 · Banka hesabı ve supplier kapsamı

**Banka hesabı: bulunamadı.** Üretim kodunda sıfır dosya:

```
BankAccount: 0 · Iban: 0 · IBAN: 0 · RemittanceAccount: 0 · PaymentInstrument: 0 · bank_account: 0
```

Recovery'de banka hesabı varlığı yoktur; dolayısıyla banka hesabı erişim kapsamı için
hedef sözleşme de yoktur.

**Supplier: var, ama kullanıcı bazlı kapsam değil.** `Diten.ProcurementService`'te
supplier tenant + LE ile kapsanır:

```
Diten.ProcurementService.Persistence/SupplierIndexConfiguration.cs:28        TenantId → LegalEntityId → SupplierId
Diten.ProcurementService.Persistence/Repositories/InvoiceMatchRepository.cs:36, :73
Diten.ProcurementService.Persistence/Repositories/ContractingRepository.cs:37
```

Bu tenant/LE izolasyonudur. **"Bu kullanıcı hangi supplier'ları görür"** sorusunun
hedefte karşılığı bulunamadı.

---

## 6 · Evidence parent doğrulaması

**Onaylı sözleşme yok. Uygulanmış referans var — ve o referans üç vakayı katlıyor.**

Evidence'a özgü parent doğrulaması bulunamadı: `Diten.PvgService/src` içinde `parent`
için sıfır eşleşme. Mevcut evidence reason code'ları parent doğrulaması ile ilgili
değil, evidence'ın *istenmesi/bağlanması* ile ilgilidir:

```
Diten.Platform.Application/Features/DocumentManagementExternalDocuments/...Models.cs:33   EVIDENCE_REFERENCE_REQUIRED
Diten.PvgService.Domain/RegPvBase/PvgSafeReasonCodes.cs:7                                  PVG_EVIDENCE_LINK_UNAVAILABLE
```

En yakın uygulanmış referans:

```csharp
// Diten.PpmService.Application/Features/Projects/Services/ProjectService.cs:76-82
private async Task<bool> ParentExists(ProjectParentType type, Guid id, CancellationToken ct)
    => type switch
    {
        ProjectParentType.Initiative => await initiatives.GetByIdAsync(tenant.TenantId, id, ct) is not null,
        ProjectParentType.Program    => await programs.GetByIdAsync(tenant.TenantId, id, ct) is not null,
        _ => false
    };

// :19 ve :35
if (!await ParentExists(r.ParentType, r.ParentId, token))
    return Response<ProjectDto>.Fail("Project parent was not found.", 404);
```

**Davranış: üç vaka da tek 404'e düşer, ayırt edici reason code yoktur.**
Desteklenmeyen tip `_ => false` ile, bulunamayan `is not null` ile, kapsam dışı ise
`GetByIdAsync(tenant.TenantId, …)` tenant parametresiyle — yani kapsam dışı burada
*tenant dışı* demektir ve aynı 404'e katlanır.

> ⚠ **Bu referans kopyalanmamalıdır.** Desteklenmeyen parent tipi bir istemci hatasıdır;
> kapsam dışı parent bir izolasyon cevabıdır. Tek 404'te birleştirmek ikisini ayırt
> edilemez kılar. §7'deki üç yollu sözleşme uygulanmalıdır.

---

## 7 · Hata sözleşmesi — üç yollu, katlanmaz

Codex'in MOD-0119'da uygulayacağı davranış:

| durum | cevap | gerekçe |
|---|---|---|
| bağlam eksik (LE claim/header yok) | **400** | eksik bağlam bir istemci hatasıdır, veri cevabı değil |
| kapsam dışı / referenceable değil | **404 + modülün kendi NOT_FOUND kodu** | yabancı ile inactive ayırt edilemez; cevap hiçbir şey açığa vurmaz |
| bağımlılık erişilemez **veya 403** | **ayrı hata — 404 DEĞİL** | aşağıdaki gerekçe |

Son satır belirleyicidir ve Recovery'de yorumla birlikte yazılıdır:

```csharp
// Diten.SupplyChainService.Infrastructure/Common/MdmLegalEntityScopeValidator.cs
// 403 means the caller lacks mdm.legal-entities.read. That is the caller's authorization problem and
// not a statement about the legal entity, so it is refused — but as unavailable, because treating it
// as NotReferenceable would report a permission gap as a missing record (Q420's distinction).
HttpStatusCode.Forbidden => LegalEntityScopeOutcome.Unavailable,
```

**404'ü alıp bu ayrımı almazsanız, eksik izinler aylarca "kayıt yok" olarak görünür.**

Fail-closed sözleşmenin tamamı (`ILegalEntityScopeValidator.cs`):

```csharp
/// Fail-closed by construction: the only non-rejecting outcome is Valid, and an unreachable MDM
/// maps to Unavailable rather than passing the request through.
public enum LegalEntityScopeOutcome { Valid, NotReferenceable, Unavailable }
```

Kapsam dışı cevabın uygulanmış hâli:

```
Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:69      tenant != headerTenant     → 404 LOAD_NOT_FOUND
Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:77      NotReferenceable           → 404 LOAD_NOT_FOUND
```
Aynı desen `Carrier` / `Return` / `Claim` / `Shipment` middleware'lerinde.

Platform tarafındaki karşılığı: `NOT_FOUND_NON_LEAKAGE` reason code + 404
(`WorkflowTaskTransitionSupport.cs:64-67, 83-86`;
`Contracts/DocumentRepository/DocumentRepositoryReasonCodes.cs:14`).

### Devredilebilir test seti

```
Diten.SupplyChainService.Tests/Common/LegalEntityScopeValidatorTests.cs
  Ok_IsValid_AndTheRequestIsOneGetCarryingTenantAndCallerToken
  NotFound_IsNotReferenceable_ForForeignAndForInactiveAlike
  EveryOtherStatus_IsUnavailable_NeverValid
  TransportFailure_IsUnavailable
  Timeout_IsUnavailable
  MissingOrNonHttpBaseUrl_IsUnavailable_AndNoCallIsMade
  EmptyIdentifiersOrBlankToken_AreRejectedBeforeAnyCall

Diten.SupplyChainService.Tests/Common/LegalEntityMiddlewareWiringTests.cs
  EveryRemainingModule_Answers...                                    ← her modülün aynı cevabı verdiğini ölçer
  LegalEntityScopeValidator_IsTheRealMdmOne_InTheProductionContainer
  MissingLegalEntityHeader_Is...
```

---

## 8 · LE kapsamı kontratı — dışa açılmış arayüz

**Yok. HEAD `2acce233b` itibarıyla değişiklik yok.**

- **Tek DI kaydı:** `Diten.Platform.Application/DependencyInjection.cs:60` →
  `services.AddScoped<IDataScopeResolver, OrgDataScopeResolver>();`
- **Platform dışı üretim tüketicisi:** sıfır.
- **Yayımlanmış arayüz:** yok. Kapsam resolver'ına dokunan tek HTTP yüzeyi
  `AccessExplainController` (`GET /api/platform/access/explain/me`) ve sözleşmesi ham id
  vermez — `ScopeKinds` + `ScopeCounts`, *"never raw scope ids"*
  (`Diten.Platform.API/Authorization/Explain/SelfAccessExplainResponse.cs`). Ayrıca
  self-only ve `/api/platform` yolu olduğu için bir tenant servisi kullanamaz.
- **Çağrı kimliği kararı:** karar bekliyor (Q470).
- **Onay kaydı:** yok.
- **Platform dışında:** `NoOpDataScopeResolver` boş liste döner → opt-in modül için
  sıfır satır (`Diten.Platform.Common/Authorization/NoOpDataScopeResolver.cs`).

**Önemli nüans — işin boyutunu değiştirir.** Aranan imza süreç içinde **zaten vardır**:
`IDataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, featureCode, ct)` tam
olarak tenant + kullanıcı + modül bazında çözüm yapar ve
`EntitlementDataScopeKind.LegalEntity` üretir. Eksik olan kontratın *tasarımı* değil,
*dışa açılması*. Yani platform işi sıfırdan kontrat icat etmek değil, mevcut olanı
yayınlamaktır.

Kullanıcının LE kümesinin türetilmesi (katman 1'in gövdesi):

```csharp
// Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs:228-237
// LegalEntity scope = the read-only MOD-0220 reference owned by the user's own Organization Unit(s).
// Fail-closed: the stale `LegalEntityId` stored on the Organization Unit is NOT sufficient on its own.
// Each candidate id is re-confirmed against the verified MOD-0220 read-only lookup-validation contract.
// The scope is emitted ONLY when the live result is currently referenceable.
```

**Testler:**
```
Diten.Platform.Application.Tests/Authorization/OrgDataScopeResolverTests.cs
  Valid_assignment_hydrates_org_position_managerchain_and_legalentity_scopes
  Referenceable_legal_entity_emits_legalentity_scope
  Non_referenceable_legal_entity_is_not_emitted_but_other_scopes_remain
  Validator_failure_does_not_emit_legalentity_and_preserves_other_scopes
  Cross_tenant_assignment_is_isolated
  Expired_ / Future_ / Soft_deleted_assignment_produces_no_scope
  Archived_position_produces_no_scope · Archived_org_unit_is_excluded_from_subtree

Diten.Platform.Application.Tests/Authorization/DataScopeResolverRegistrationTests.cs
  AddApplication_registers_real_data_scope_resolver_as_scoped
  AddApplication_does_not_register_noop_resolver_as_production_default

Diten.Platform.Application.Tests/Authorization/NoOpDataScopeResolverTests.cs
  ResolveAsync_returns_empty_for_arbitrary_inputs
```

---

## 9 · Kaynak taraftaki üretici boşluğu — `ar_legal_entity_scope`

Kaynak tarafın tespiti: AR bu claim'den bir yetkili LE kümesi **okuyor**, ama taranan
Auth / Platform / Treasury üretim kodunda bu claim'i **üreten** taraf bulunamadı.

**Recovery bu claim'i hiç tanımıyor.** Tarama temiz; çıkan eşleşmeler WorkingCalendar'ın
alakasız `legal_entity_scope_requires_legal_entity` reason code'udur. Yani bu, kaynağa
özel bir girdidir ve MOD-0120 buraya taşındığında okuduğu claim hiçbir zaman dolmaz —
transfer paketinde **"hedefte karşılığı olmayan girdi"** olarak beyan edilmelidir.

### Mühendislik sonucu: dar düzeltme olduğu gibi yazılırsa akış karanlıkta kalır

"Mevcut kümeden filtre uygula" + "boş küme erişim vermez" birleşince, üretici yoksa
küme üretimde **her zaman** boştur ve filtre her isteği sıfır satıra indirir. Güvenli,
ama çalışmıyor. Asıl risk bunun fark edilmemesidir: test fixture'ları claim'i doldurursa
testler yeşil kalır, üretimde akış sessizce boşalır. Ters senaryo daha kötüdür — bir
middleware boş kümeyi "kısıtlama yok" diye yorumlarsa filtre tamamen açılır.

### Boşluğu kapatan iki gözlem (tarama turu değil)

Kod taraması "bulamadım" der, "yok" demez. Claim üretimi koda bakmakla görünmeyen
yerlerde olur: gateway/BFF token zenginleştirme, IdP claim mapping,
`ClaimsTransformation` middleware, login sırasında DB'den claim ekleme, ya da başka bir
servisin ihraç ettiği eski token.

- **Gözlem A.** Şu anda AR verisi okuyabilen gerçek bir kullanıcının staging/üretim
  token'ında `ar_legal_entity_scope` **var mı**, varsa **kaç girdi** taşıyor? *(Token veya
  değerler gerekmez, sayı yeterlidir.)*
- **Gözlem B.** Tüketicinin boş-küme dalı bugün ne yapıyor: hiç satır mı döndürüyor,
  yoksa filtreyi hiç uygulamıyor mu? *(Tek dosya okuması.)*

| A | B | sonuç |
|---|---|---|
| claim yok | boş = filtre yok | **bugün açık.** Dar düzeltme gerçek bir sızıntıyı kapatır ve akışı karanlığa alır. İkisi birlikte kaydedilmeli. |
| claim yok | boş = sıfır satır | akış **bugün de** karanlık. Dar düzeltme bir açık kapatmıyor, mevcut durumu pekiştiriyor. |
| claim dolu | boş = filtre yok | **en kötüsü.** Üretici taranan yerlerin dışında; düzeltme doğru ama üretici bulunup sahiplenilmeden güvenilemez. |
| claim dolu | boş = sıfır satır | mekanizma uçtan uca çalışıyor; eksik olan yalnız üreticinin kim olduğunun yazılı olması. |

---

## 10 · Dar düzeltme onayı

**Dar düzeltme: onaylandı.** Kaynakta repository seviyesinde tenant + yetkili LE
filtresi uygulanır, mevcut kümeden okunur, boş küme erişim vermez. Yeni Platform
bağımlılığı eklenmez.

**Kayıt sınırı — bağlayıcı.** Düzeltme **"okuma izolasyonu kuruldu"** olarak kaydedilir,
**"LE yetkilendirmesi kuruldu"** olarak kaydedilmez. İkinci ifade, §9'daki üretici
boşluğunu kapanmış gösterir.

**`VerifiedLegalEntity` kanıt olmadan otomatik doldurulmaz.** Bu karar Recovery'nin
kendi şekliyle örtüşür: `AddLegalEntityScopesAsync` da Org Unit üzerinde *duran*
`LegalEntityId`'yi tek başına yeterli saymaz, her çözümlemede canlı olarak yeniden
doğrular.

**Devredilen koruma — kontrat değil, test.** Boş kümenin iki başarısızlık biçimini
ayırmak için üç testin karşılığı kaynağa konur. Platform bağımlılığı eklemez,
"boş küme = filtre yok" sapmasını yapısal olarak yasaklar:

```
AddApplication_does_not_register_noop_resolver_as_production_default
No_scopes_at_all_means_NOTHING_rather_than_everything
An_empty_scope_matches_no_row_even_when_rows_exist
```

**Canlı kabul kapısı.** Güvenilir LE kapsamı kontratı çözülmeden ilgili akışların canlı
kabulü yapılmaz. Bu, MOD-0120'nin hedef taraftaki kabul kriterine yazılı şart olarak
girer.

---

## 11 · Statü tablosu — neyi hangi ağırlıkta okumalı

| kalem | statü |
|---|---|
| Boş kapsam = sıfır satır | **onaylı kural** (`.antigravity/rules/data-scope-enforcement.md` §3.3, §4 sert kapı) |
| Tenant-wide'ın açık yetki olması + otomatik-grant yasağı | **onaylı sahip kararı** (2026-09-11, 2026-09-14) |
| Üç yollu hata sözleşmesi (§7) | **mutabakat + uygulanmış referans** — TP-001 v1.5 terfi etmediği için standart değil |
| `VerifiedLegalEntity`'nin kanıtsız doldurulmaması | **mutabakat** |
| Dar düzeltme ve kayıt sınırı (§10) | **sahip onayı** |
| Internal LE kapsamı uç noktası | **ÖNERİ** — sahip onayı yok, koda girmedi, kimse yazmıyor |
| MOD-0119'un izin anahtarları ve sınırlı operasyon rolü | **ÖNERİ** — kaynaktan 2026-10-12'de ulaştı, §13'te ölçümle karşılaştırıldı |

---

## 12 · Bekleyen sahip kararları

| # | karar | bunu çözmek neyi değiştirir |
|---|---|---|
| 1 | Katman 1 kontratının sahipliği — mevcut `IDataScopeResolver`'ın dışa açılması | §8 "yok" → "var" |
| 2 | Q470 — çağrı kimliği (servis kimliği / kullanıcı token'ı) + `mdm.legal-entities.read` izin çözümü | §1 "karar bekliyor" → karar |
| 3 | Üç yollu hata sözleşmesinin standarda alınması (TP-001 v1.5 terfisi) | §7 "mutabakat" → onaylı sözleşme |
| 4 | Kaynağın yeni izin anahtarları + sınırlı operasyon rolü önerisi | §13 — ölçümle karşılaştırıldı, onay sahipte |

Q470'in kayıtlı hâli (`MdmLegalEntityScopeValidator.cs` sınıf başlığı): SupplyChain MDM
servis kimliği tutmadığı için çağıranın Authorization'ı forward edilir; kullanıcı
`mdm.legal-entities.read` iznine ihtiyaç duyar; bu izin `DataSeeder`'da seed edilir ama
SupplyChain varsayılan rol şablonunda yoktur.

---

*Bu dosya bir ölçüm tutanağıdır. §11'de "ÖNERİ" işaretli kalemler dışında her satır
HEAD `2acce233b` üzerinde doğrulanmıştır. Yeni sözleşme tasarlanmamıştır; öneriler
mevcut davranış gibi sunulmamıştır. Token, sır veya kullanıcı verisi içermez.*

---

## 13 · Kaynağın izin önerisi — ölçümle karşılaştırma

*Ek: 2026-10-12. Kaynak taraf, belgeyi inceleyip SHA-256'yı doğruladıktan sonra dört
kalem iletti ve kendi inceleme kaydını kaynak depoda oluşturdu
(`docs/records/audits/2026-10/treasury-mod0119-target-tenant-le-evidence-review-20261012/REPORT.md`).
Recovery o taraftan değiştirilmedi. Aşağıdaki dört kalem **öneridir, onaylanmamıştır**;
karşılarındaki ölçüm bu depoda HEAD `2acce233b` üzerinde doğrulanmıştır.*

| kalem | öneri (onaylanmadı) |
|---|---|
| Normal LE kapsamlı okuma | `ap.import-failures.read` |
| LE'siz/belirsiz kayıtların okunması | `ap.import-failures.operational.read` |
| Sınırlı tenant rolü | `ap-import-operations` |
| Rolün izni | yalnız operasyonel okuma; otomatik kullanıcı ataması yok |

### 13.1 · Koruma TEK yolda değil, ÜÇ yolda gerekiyor

Kaynak tarafın bulgusu — "SuperAdmin'e tüm katalog izinlerini atayan seeder yolu
mevcut" — doğru, ama Recovery'de ölçülen koruma bundan geniştir. `ExplicitGrantOnlyPermissions`
listesi **üç ayrı otomatik dağıtım yolunda** tüketilir ve her biri kendi noktasında
durdurur:

```
Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:109
  var available = catalog.Where(p => !p.IsDeleted && !ExplicitGrantOnlyPermissions.Keys.Contains(p.Key));
      → varsayılan rol provisioning

Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs:137
  .Where(permission => !ExplicitGrantOnlyPermissions.Keys.Contains(permission.Key))
      → modül entitlement sync

Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs:37
  if (ExplicitGrantOnlyPermissions.Keys.Contains(permissionKey)) ...
      → tam katalog / SuperAdmin
```

> ⚠ **Yalnız seeder'ı kapatmak yetmez.** Kaynakta korumayı tek noktaya koyarsanız,
> `ap.import-failures.operational.read` diğer iki yoldan sızar: varsayılan rol şablonu
> onu yeni rollere, entitlement sync ise modül yetkilendirildiği anda her tenant
> Admin'ine verir. Recovery'de üç nokta ayrı ayrı sorar; kaynakta da üçü birlikte
> kapatılmalıdır. Hangi yolların kaynakta mevcut olduğu burada ölçülmedi — kaynak
> tarafın kendi envanteri gerekir.

### 13.2 · Kapı, zaten verilmiş izni geri almaz

`ExplicitGrantOnlyPermissions` kendi doküman yorumunda bunu açıkça söylüyor:

> *"Enrolling it here stops NEW automatic grants; it removes no existing role-permission
> row — today's holders are listed and re-granted explicitly during migration."*

Kaynakta SuperAdmin'e tüm katalog hâlihazırda atanmışsa, kapıyı eklemek o satırları
**geri almaz**. Kapıya ek olarak bir geçiş adımı gerekir: mevcut sahipler listelenir,
`ap.import-failures.operational.read` satırları geri alınır, gerekenlere açık atama ile
yeniden verilir. Bu adım yazılmazsa koruma yeni tenant'larda çalışır, mevcutlarda
çalışmaz — ve bu fark testlerde görünmez.

### 13.3 · Anahtar adlandırması: ölçülen konvansiyona bir sapma var

Recovery'deki mevcut anahtarlar `<alan>.<kaynak>.<eylem>` biçimindedir ve **genişletici
varyant eylem ekine** yazılır, ara segmente değil:

```
platform.tasks.read-all
platform.tasks.work-report.read-tenant-wide
crm.planned-visit.read-all
crm.visit-plan.read-all
auth.users.account-kind.manage
```

- `ap.import-failures.read` → konvansiyona **uyuyor**.
- `ap.import-failures.operational.read` → niteleyici **eylemden önce** geliyor; ölçülen
  desende genişletici nitelik eylemin kendisine eklenir (`read-all`, `read-tenant-wide`).
  Konvansiyonla tutarlı biçim `ap.import-failures.read-operational` veya
  `...read-unscoped` olurdu.

Bu bir adlandırma gözlemidir, tasarım önerisi değildir; kararı sahiptedir. Ancak
`HasPermission` eşleşmesi tam anahtar karşılaştırmasıyla çalıştığı için, sapma
fonksiyonel bir risk değil, yalnız katalog tutarlılığı sorunudur.

### 13.4 · "İzin yoksa kendi kapsamına düş" davranışı AP'ye taşınmamalı — doğru karar

Kaynak tarafın bu davranışı operasyonel AP erişimine uygulamama kararı, Work Report'un
**kendi yorumuyla** desteklenir. `WorkReportScopeSource.cs:117-119` fallback'i bir
yetki kararı olarak değil görüntüleme tercihi olarak gerekçelendirir:

> *"…for `Tenant` without the permission is not attempting an escalation to be rejected;
> they are stating a DISPLAY preference for a permission they do not hold, and the honest
> answer is what they were always going to get anyway: their own scope."*

Bu gerekçe iki koşula dayanır: **(a)** düşülen küme daha *dar*dır (kendi kapsamı) ve
**(b)** kaynak bir rapordur — daha az satır görmek dürüst bir cevaptır.

Operasyonel AP okumasında ikisi de geçerli değildir: LE'siz/belirsiz kayıtların daha dar
bir karşılığı yoktur (kapsamlı okuma onları hiç içermez), ve operatör bu kayıtlar
üzerinde **işlem yapmak** için bakar. Sessizce boş liste dönmek, iznin eksik olduğunu
gizler ve operatöre "düzeltilecek kayıt yok" der. Buradaki dürüst cevap reddir.

Kayda geçsin: bu, Recovery'nin Work Report davranışının AP'de **geçersiz** olduğu
anlamına gelmez; o davranışın kendi gerekçesi kendi kapsamını sınırlar. İki yüzey farklı
olduğu için farklı cevap verirler.

### 13.5 · Açık kalan kapı

Kaynak tarafın tespiti doğrudur: **bu belge güvenilir LE kapsamı üreticisini
sağlamamaktadır.** §8 bunu "yok" olarak ölçmüştür ve §12 karar #1 olarak sahibin önünde
durmaktadır. `ap.import-failures.read` (normal LE kapsamlı okuma) bu üretici gelmeden
güvenilir biçimde uygulanamaz; `ap.import-failures.operational.read` ise tanımı gereği
LE kapsamı dışındaki kayıtları hedeflediği için üreticiden bağımsızdır — yani iki anahtar
aynı kapıya bağlı değildir ve ikincisi birincisi beklemeden ilerleyebilir.
