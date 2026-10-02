# Denetim kaydı envanteri — 2026-10-02

| | |
|---|---|
| İş paketi | WP-AUDIT-STANDARD-01 · P-2026-10-02-02 |
| İlgili kayıt | BL-456 (kullanıcı yaşam döngüsü denetlenmiyordu) — aynı kökün genel hali |
| Ölçülen | dal `chore/audit-trail-standard`, taban `36b42bb71` |
| Kural | `.antigravity/rules/audit-trail-standard.md` (AUD-001) |
| Yer | `docs/records/` — soru 2: belirli bir tarihte olanı kaydeder, düzeltilmez (K4). Durum değişince yenisi yazılır. |

Bu belge bir **ölçümdür**, düzeltme değildir. Hiçbir servisin üretim kodu değiştirilmedi.

---

## 1. Soru

Bir kullanıcı ya da servis kiracı / platform verisini değiştirdiğinde, **kim, neyi, ne zaman** değiştirdi kaydı
bir yere düşüyor mu?

## 2. Tanım ve yöntem

**Yazma komutu:** `services/<servis>/src` altında MediatR `IRequest` uygulayan (doğrudan ya da serviste tanımlı,
`IRequest`'ten türeyen bir arayüzle), soyut olmayan, sorgu olmayan her tür. Sorgu = adı `Query` ile biten ya da
ad alanı `.Queries` içeren (Platform `AuditBehavior.IsQueryRequest` ile aynı tanım).

**Tek sayım.** Aşağıdaki tablo elle sayılmadı; mimari testinin ölçümünün çıktısıdır. Aynı tanım, aynı kod:

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests \
      --filter "FullyQualifiedName~Inventory_PrintsTheTable" --logger "console;verbosity=detailed"

**Bir komut ne zaman "denetleniyor":** üretim kaynağında (yorumlar ve dizgi sabitleri çıkarıldıktan sonra)
- komut türü, servisin defterinde kayıtlı bir **işareti** taşıyorsa (ör. `IAuditableCommand` + `IAuditMetadataProvider`), ya da
- komutun handler'ı, defterde kayıtlı bir **yazıcıyı** anıyorsa (ör. `IUserAuditRecorder`), ya da
- defterde `## Dolaylı` olarak bildirilmiş ve iki ucu kanıtlanmışsa

**ve** o izin yolu kabul edilmişse (`a` / `b` / `c`). Yolu `aday` olan iz — mekanizma var ama kayıt gerçek bir
depoya düşmüyor, okunamıyor ya da teslim edilmiyor — denetim sayılmaz.

**Sınırı:** bu bir metin ölçümüdür. İşaretin / yazıcının *bağlı* olduğunu görür, kaydın *yazıldığını* görmez.
Hiçbir komut çalıştırılmadı, hiçbir veritabanı okunmadı.

## 3. Sonuç

Testin ürettiği tablo (2026-10-02, 27/27 yeşil koşudan):

| servis | yazma komutu | a | b | c | istisna | borç | borcun içinde aday izi olan | handler bulunamayan | MediatR dışı *Command |
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
| Diten.PvgService | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 13 |
| Diten.TalentEcosystemService | 106 | 0 | 0 | 0 | 0 | 106 | 0 | 0 | 0 |
| **TOPLAM** | 1017 | 280 | 14 | 23 | 5 | 695 | 178 | 12 | 23 |

Sütunlar: **a** Platform içi merkezi günlük · **b** merkezi günlüğe iletim · **c** eşdeğer iz · **istisna**
gerekçeli bildirim · **borç** hiçbir kabul edilmiş ize gitmeyen · **aday izi olan** borcun içinde bir
mekanizmaya dokunan ama o mekanizma kabul koşullarını sağlamayan · **handler bulunamayan** tarayıcının
`IRequestHandler<Komut…>` bulamadığı (genel/jenerik handler) · **MediatR dışı** adı `Command` ile biten ama
MediatR isteği olmayan tür (ölçüm dışı; yalnız sayısı sabitlendi).

**Okuma:** 1017 yazma komutunun **317'si (%31)** denetleniyor, 5'i gerekçeli istisna, **695'i (%68)** hiçbir
kabul edilmiş ize gitmiyor. Denetlenenlerin 295'i Platform'da. Platform dışındaki 553 komutun **22'si**
denetleniyor (Auth 8, MDM 14).

## 4. Servis başına denetim altyapısı

"Altyapı" = işaret + pipeline davranışı, iletici, ya da kendi günlük koleksiyonu. Kaynak: kod okuması (alt ajan
ölçümü; ⓥ işaretli satırlar ayrıca elle doğrulandı).

| servis | altyapı | kayıt nereye düşüyor | kabul | not |
|---|---|---|---|---|
| Platform | `IAuditableCommand` + `AuditBehavior` → `IAuditService` → `audit_outbox` → `audit_events` ⓥ | merkezi günlük | **a** | Yazma hatasında komut devam eder; `CriticalCategories` boş ⓥ |
| Platform | `ITransactionOwnedAuditCommand` (25 komut) ⓥ | `audit_outbox` (iş yazmasıyla aynı işlem) | **aday** | Bulgu F1 |
| Platform | `TaskTransition` (görev etkinlik akışı) | `task_transitions` | **c** | Görev güncellemesinden *sonra*, işlemsiz yazılır |
| Platform | `WorkflowTransitionLog` | `workflow_transition_logs` | **c** | `…/instances/{id}/history` |
| Platform | `IInterfaceRegistryAuditSink` | hiçbir yere — kayıtlı uygulama `Null…Sink` | **aday** | 5 komut |
| Platform | `MeetingMinutesVersion` | `meeting_minutes_versions` | iz değil | Depo `UpdateAsync` sunuyor; sürümlü belge, olay günlüğü değil |
| Auth | `IUserAuditRecorder` → `authAuditLogs` + `IPlatformAuditForwarder` | yerel + merkezi günlük | **b** | En iyi çaba; temel ayarda S2S anahtarı boş → iletim atlanır |
| Auth | `IAuthAuditService`, `IRbacAuditRecorder` → `authAuditLogs` | yerel koleksiyon | **aday** | Yazılıyor, güncelleme/silme yok — **okuyan uç yok** (Bulgu F4) |
| MDM | `IAuditableCommand` + `AuditForwardingBehavior` → `POST /api/internal/audit/append` ⓥ | merkezi günlük | **b** | En iyi çaba; handler istisna atarsa kayıt yok; ad/önce-sonra taşımıyor |
| MDM | `IProductAbbreviationHistoryRepository` | `mdm_product_abbreviation_history` | **c** | 8 komut `ProductAbbreviationWorkflow` üzerinden |
| MDM | `LocalAuditIntent` (ürün kümelerine gömülü) | kümenin içinde outbox | ölçüm dışı | Teslim eden işçi yok |
| CRM | `I{Contact,Account,ContentComposition,KnowledgeConcept}AuditPublisher` ⓥ | **uygulama günlüğü** (varsayılan) | **aday** | `Crm:Audit:Mode=http` hiçbir ayarda yok; açılsa Platform 400 döner (Bulgu F3) |
| CRM | `ITerritoryLifecycleAuditPublisher` | uygulama günlüğü | **aday** | HTTP uygulaması yok |
| HCM | `IDraftAuditService` | uygulama günlüğü ("non-authoritative fallback") | **aday** | `IHcmAuditAppendClient` kayıtlı ama çağıranı yok |
| ESBP | `IEnterpriseStrategyAuditSink` | kendi Mongo `AuditEvent` | **aday** | Kayıtta **kiracı kimliği yok**; okuma ucu yalnız Project |
| PPM | `IAuditIntentRepository` | `ppm_audit_intents` (aynı işlem) | **aday** | Üretici ayarda kapalı; Platform'da tüketici yok; okuma ucu yok |
| MG | `DwsAuditIntent…`, `IDwsAuditSimulator` | yerel-test outbox / bellek | yok | Servis yalnız `--local-test` ile açılıyor |
| PVG | `PvgAuditIntent` | hiçbir yere — bellek içi kayıt | yok | MediatR kullanmıyor |
| DevEnablement, HumanCapital, Procurement, TalentEcosystem | **yok** | — | — | İşaret, iletici, günlük: hiçbiri |

Merkezi günlüğe gerçekten yazan servis sayısı: **üç** (Platform, Auth'un kullanıcı olayları, MDM'in tüzel kişi
komutları). `POST /api/internal/audit/append` ucunun iki istemcisi var: Auth ve MDM.

## 5. Platform — borcun dağılımı

Defterden türetilmiş kırılım (toplam 164 = §3 tablosu). 55 özellik klasörünün 27'sinde borç yok.

| özellik klasörü | komut | denetlenen | istisna | borç |
|---|---:|---:|---:|---:|
| Tasks | 50 | 15 | 0 | 35 |
| Meetings | 25 | 0 | 0 | 25 |
| Tenants | 28 | 9 | 0 | 19 |
| DocumentManagementGovernanceSweep | 9 | 0 | 0 | 9 |
| ModulePages | 8 | 0 | 0 | 8 |
| InterfaceRegistry | 6 | 0 | 0 | 6 |
| WorkingCalendar | 6 | 0 | 0 | 6 |
| ModuleCatalog | 6 | 0 | 0 | 6 |
| DocumentManagementInstantiation | 5 | 0 | 0 | 5 |
| SubscriptionPlans | 5 | 0 | 0 | 5 |
| WorkingCalendarImport | 5 | 0 | 0 | 5 |
| Notifications | 15 | 9 | 2 | 4 |
| PlatformAdministrators | 8 | 5 | 0 | 3 |
| Workflow | 10 | 7 | 0 | 3 |
| ModuleServices | 3 | 0 | 0 | 3 |
| ModuleDomains | 3 | 0 | 0 | 3 |
| diğer 12 klasör (belge yönetimi alt modülleri, BRD, kanıt bağlama, gezinme, modül kaydı) | 68 | 49 | 0 | 19 |

Adıyla anılması gereken borç komutları (kimlik / kiracı durumu — kural §6'ya göre istisna olamaz):
`SuspendTenantCommand`, `InviteTenantAdminUserCommand`, `ActivateTenantAdminUserCommand`,
`UpdateTenantSettingsCommand`, `SuspendPlatformAdministratorCommand`, `InvitePlatformAdministratorCommand`,
`ResendPlatformAdministratorInviteCommand`, `ReplaceTenantNavPreferencesCommand` (kiracı geneli menü ayarı).

## 6. Bulgular

Her biri kod okumasıyla; **canlıda ölçülmedi**. Düzeltmeleri bu paketin işi değil.

**F1 · Platform "işlem içi" denetim yolu büyük olasılıkla kayıt üretmiyor (25 komut).** `AuditBehavior`,
`ITransactionOwnedAuditCommand` taşıyan komutu atlar; kaydı handler işlem içinde `audit_outbox`'a yazar. Üç
çağıranın yükü yalnız `Outcome` ve bir alan taşıyor (`PhysicalEntitlementAuditIntent.cs`,
`TenantSubscriptionTransactionWriter.cs`, `GlobalApplicabilityTransactionCoordinator.cs`); eşleyici ise yükün
içinde `TenantId`, `CorrelationId`, `ActorType`, `Category`, `SourceService` ister
(`AuditOutboxPayloadMapper.cs`). Eşleme istisnası → `DeadLetter` (`AuditOutboxProcessor.cs`). Yükte aktör de
yok. Etkilenen: abonelik planları, kiracı aboneliği, modül yetkilendirmesi, modül kataloğu, manifest kaydı.
Doğrulamak için salt-okunur sorgu:

    db.audit_outbox.aggregate([{ $group: { _id: { s: "$Status", t: "$RequestType" }, n: { $sum: 1 } } }])

**F2 · 8 kiracı aboneliği komutu `AuditBehavior` izin listesinde yok.** `ITransactionOwnedAuditCommand` taşıyan
25 türün 17'si `IsAuthorizedTransactionOwnedAuditCommand` içinde; `AssignPlanToTenant`, `Create/Activate/
Suspend/Reactivate/Cancel/Expire/RenewTenantSubscription` yok. Davranış bu durumda
`InvalidOperationException` atıyor. Bu komutlar pipeline'dan geçiyorsa her çağrıda patlar; geçmiyorsa
neden işaret taşıdıkları belirsiz. Ölçülmedi.

**F3 · CRM'in 89 komutu "denetim yayıncısı" çağırıyor ve kayıt yalnız uygulama günlüğüne düşüyor.** Varsayılan
kayıt `Logging…AuditPublisher`. HTTP modu açılsa bile gönderilen `Category = "Crm"` ve `Operation =
"contact.create"` biçimli değerler Platform'un `AuditCategory` / `AuditOperation` kümelerinde yok → 400. Ayrıca
HTTP gövdesinde `ActorId` yok.

**F4 · Auth rol/izin ve parola olayları yazılıyor, kimse okuyamıyor.** `authAuditLogs` için okuma ucu yok.
10 komut (rol oluştur/güncelle/sil, izin ata/geri al, parola değiştir, giriş…) bu yüzden `aday`.

**F5 · Kiracı yöneticisi merkezi günlüğü okuyamıyor.** Tek okuma denetleyicisi `PlatformAuditController`,
`[Authorize(Policy = "PlatformAdminOnly")]`. Kiracı, kendi verisinde kimin ne yaptığını göremez.

**F6 · Hiçbir yolda yazma hatası komutu durdurmuyor** (dışa aktarım hariç). `CriticalCategories` boş,
`RejectedCritical` üreten kod yok. Düşmeyen kayıt yalnız bir uyarı logudur.

**F7 · Saklama politikası uygulanmıyor.** Politikalar saklanıyor ve düzenlenebiliyor; onları `audit_events`
üzerinde uygulayan iş, TTL ya da silme bulunamadı. (Kayıtlar silinmiyor — bu yönüyle güvenli; ama KVKK saklama
süresi de işlemiyor.)

**F8 · ESBP denetim kaydında kiracı kimliği yok.** `AuditEvent` kaydı: aktör, zaman, nesne, eylem, korelasyon,
önce/sonra özeti — `TenantId` yok.

**F9 · MDM iletimi başarısız komutu kaydetmiyor.** Davranış `await next()`'ten sonra çalışır; handler istisna
atarsa iletim hiç yapılmaz. Ayrıca aktör adı ve önce/sonra taşımıyor.

## 7. Ölçümün görmediği yazma yolları

Aşağıdakiler **farklı bir ölçümdür** (HTTP fiili sezgisi: `[HttpPost/Put/Patch/Delete]` taşıyan eylemin gövdesinde
MediatR gönderimi var mı). §3 tablosuyla toplanmaz; `POST` ile yapılan okumaları (çözümleme, arama) da sayar.

| servis | yazma eylemi | MediatR'a giden | **gitmeyen** | örnek |
|---|---:|---:|---:|---|
| Auth | 38 | 27 | 11 | `PlatformAuthController.ProvisionPlatformAdmin / ResetPassword`, `InternalPermissionsController.Sync / Delete` |
| CRM | 209 | 203 | 6 | `ImportExportController.ApplyContactWorkbook`, `ClaimsController.Approve` |
| DevEnablement | 9 | 8 | 1 | `ReferenceWorkItemProviderController.DispatchAction` |
| EnterpriseStrategy | 64 | 45 | 19 | `EnterpriseStrategyKpisController.Create / Update / Archive` |
| HCM | 8 | 6 | 2 | `EmployeeDraftsController.Submit` |
| ManagementGovernance | 22 | 0 | 22 | `ProcessModelingLocalTestController.*` |
| Platform | 479 | 474 | 5 | iç uçlar (`InternalAuditController.Append` vb.) |
| PPM | 40 | 28 | 12 | `InvestmentCaseGateIReferencesController.*` |
| PVG | 0 denetleyici · 4 minimal-API yazma ucu | — | 4 | `PvgCaseIntakeTriageEndpoints` |
| HumanCapital, MDM, Procurement, TalentEcosystem | 72 · 25 · 36 · 106 | hepsi | 0 | — |

Ayrıca hiç sayılmayanlar: arka plan işleri (Hangfire), olay tüketicileri, başlangıç tohumlamaları.

**MediatR kullanmayan komutlar (sayıları test tarafından sabitlendi):**
- **PvgService — 13 `*Command`**, MediatR yok; uygulama servisleri ve minimal-API uçları üzerinden çalışıyor.
  `PvgAuditIntent` bellek içi bir kayıt; hiçbir yere yazılmıyor. ⚠ Farmakovijilans — hasta güvenliği verisi.
- **ManagementGovernanceService — 10 `*Command`** (yapı / düğüm / revizyon komutları) MediatR isteği değil.

## 8. Bilinen boşluklar

1. Metin ölçümü: handler yazıcıyı enjekte edip çağırmasa "denetleniyor" sayılır.
2. `## Dolaylı` bildirimleri (ESBP 20, PPM 25, MDM 8) yalnız iki ucu kanıtlar; ESBP ve PPM'dekiler zaten `aday`
   olduğu için kredi vermez. MDM'deki 8 komut `c` kredisi alır — `ProductAbbreviationWorkflow` dar bir tür olduğu
   için kabul edildi, tek tek komut izlenmedi.
3. Görev etkinlik akışı kredisi `TaskTransitionKind`'ı anan 13 handler'a verildi. Depo üzerinden dolaylı geçiş
   yazan diğer görev komutları (35) borçta; bir kısmı gerçekte iz bırakıyor olabilir. Yanlış yönü güvenli olan
   seçildi: fazla borç, eksik borç değil.
4. Kaydın §3 alanlarını taşıdığı hiçbir komut için çalıştırılarak doğrulanmadı.
5. §4 tablosundaki alan / hata / görünürlük bilgileri alt ajan ölçümüdür; ⓥ dışındakiler ikinci kez okunmadı.
6. `Diten.DataKnowledgeService` ve `Diten.SupplyChainService` `AGENTS.md`'de geçiyor ama bu dalda `services/`
   altında yok; ölçülmedi.

---

## Ek — Öneri: diğer servisler için ortak iletici (karar CT ve sahibin)

**Sorun.** Dört servisin hiçbir denetim altyapısı yok (219 komut); dördü kendi yarım çözümünü yazmış (CRM, HCM,
ESBP, PPM — 258 komut) ve dördü de merkezi günlüğe ulaşmıyor. Her ekip MDM'in davranışını kopyalarsa yedi ayrı
`AuditForwardingBehavior` olur ve F9 gibi kusurlar yedi kez tekrarlanır.

| | seçenek | artı | eksi |
|---|---|---|---|
| **A** | **Ortak paket** — `Diten.Building.Blocks` altında `IAuditableCommand` + `IAuditMetadataProvider` + iletim davranışı + S2S istemci. Servis bir satırla kaydeder. | Tek uygulama, tek düzeltme yeri. Mimari testi tek işareti tanır. MDM deseni kanıtlı. SAP/Oracle/Veeva yönüne en yakın: denetim çerçevenin işi. | Senkron HTTP, en iyi çaba: Platform kapalıyken kayıt düşer. Building.Blocks'a MediatR bağımlılığı. MDM ve Auth'un göçü gerekir. |
| **B** | **Olay yolu** — servis kendi outbox'ına denetim niyeti yazar (iş yazmasıyla aynı işlem), RabbitMQ ile Platform tüketir. PPM'in yarım kalan tasarımı. | Kayıt iş verisiyle atomik; Platform kapalıyken kaybolmaz. GxP için "en iyi çaba yetmez" kararına (K2) tek uyan seçenek. | Her serviste outbox + işçi + Platform'da tüketici ve şema. Bugün iki denemesi de teslim etmiyor (PPM kapalı, MDM işçisiz). En pahalı. |
| **C** | **Mevcut "yönetilen ekleme" ucu** — `POST /api/v1/platform/audit/events` (kullanıcı belirteciyle, Gateway üzerinden). CRM ve HCM istemcileri yazılmış. | Uç ve yetki hazır; aktör belirteçten zorlanıyor (sahtesi yazılamaz). | Sistem aktörlü işler (arka plan) kullanamaz. İstemci tarafı her serviste ayrı yazılmış ve ikisi de çalışmıyor (F3). Yine en iyi çaba. |
| **D** | **Hiçbir şey ortaklaştırma** — her servis kendi eşdeğer izini (yol `c`) kurar. | Ekipler bağımsız. | Platform yöneticisinin "bu kiracıda kim ne yaptı" sorusu yedi ayrı ekrana dağılır. Beş kabul koşulu yedi kez ölçülür. Bugünkü durumun adı. |

**Öneri: A, iki aşamalı; GxP sınıfı için B'ye açık kapı.**
1. A'yı kur, önce altyapısı olmayan dört serviste aç. Borç listeleri test eşliğinde erir.
2. Aynı işaretin arkasındaki taşıyıcıyı değiştirilebilir yaz (HTTP bugün, outbox yarın). K2 "GxP kaydı yazılamazsa
   komut durur" diye karar verilirse yalnız o sınıf B'ye geçer; işaret ve test değişmez.

SAP ve Oracle'da karşılığı: değişiklik belgesi / denetim politikası çerçevenin işidir, iş nesnesi yalnız
"denetle" der. A aynı ayrımı kurar. C'nin karşılığı yoktur — hiçbiri denetim kaydını istemcinin kendi
belirteciyle yazdırmaz.

Önce cevaplanması gereken: **K1/F1** (Platform'un kendi işlem içi yolu çalışıyor mu) — çünkü B'nin Platform
tarafı aynı outbox eşleyicisinden geçer.
