---
id: MOD-0220-FU01
name: Legal Entity Short Code Permanent Lock
parent: MOD-0220
parent_name: Corporate Secretarial / Entity Management
domain: master-data-management
service: Diten.MdmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: client
data_mode_max_rows: 200
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "Yalniz DL-1 ve DL-2, dilim basina; onay satiri ve prompt kapisi olmadan dagitilmaz."
owner: mdm-domain-team
branch: feature/mdm/mod-0220-fu01-legal-entity-short-code-lock
started: 2026-11-10
target: TBD (AK-5)
form_field_count: 31
data_classification: "Kiracı içinde ana veri (şirket kısa kodu); kişisel veri yok."
audit_class: "Ana veri — yol b (merkezi günlük, `mdm-merkezi-iletim`); ret kodu kod biçiminde kayda girer."
dependencies:
  - MOD-0220 (ebeveyn; tüzel kişi kaydı, `legal-entity` manifesti, lookup-validation 1.0.0)
  - MOD-0040-FU02 (Record Code Service, Platform; yalnız tüketim — kod değişikliği yok)
  - MOD-0021 (merkezi denetim günlüğü, yol b)
---

# MOD-0220-FU01 — Legal Entity Short Code Permanent Lock

> RCS-001 §5.4 kalıcı kısa kod kilidinin MDM tarafı (RECORD-CODE-ALLOCATE 1.0.0 §12 N-12). Onay satırı ve prompt kapısı olmadan kod yazılmaz.
>
> **Künye notları:** `target` ve onaylayan TBD (AK-5). `golden_reference: compact` ev sahibi formun alan sayısındandır (`form_field_count: 31` = `frontend/Diten.Web/Views/MasterData/LegalEntities/Wizard.cshtml` içindeki 31 `id="le…"` girdisi, 2026-11-10 sayıldı); bu paket forma alan eklemez. `data_mode: client` mevcut listenin modudur (`frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/index.js:558` `serverSide: false`); liste bu pakette değişmez. `branch` kod dalı önerisidir; paket belgesi `chore/ct/mdm-ct-02` dalında.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-10, HEAD `3d80be94e`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0220-FU01 --name "Legal Entity Short Code Permanent Lock" --parent MOD-0220`
> → `OK  MOD-0220-FU01: proven against Blueprint/registry.` (çıkış 0). Kayıt satırı `execution/registries/module-id-registry.md:186` (reserved). `git log --all -S 'MOD-0220-FU01'`: yalnız kayıt satırı commit'i `3d80be94e`.
>
> **Kaynak okuma notu:** `origin/main:` önekli yollar bu dalda yok, `origin/main` (`084971b1e`) üzerinde okundu (`git show origin/main:<yol>`). MDM tüzel kişi kodu bu dalda `origin/main` ile aynı (`git diff --stat HEAD origin/main` ilgili dosyalarda boş, 2026-11-10). Bütün kod iddiaları kod okumasıdır; derlenmedi, çalıştırılmadı.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

## 0. Kararlar

### 0.1 Sahip kararı (değiştirilmedi)

| # | Tarih | Karar |
|---|---|---|
| S-1 | 2026-11-10 | "A, kalıcı kilit": bir şirkete ilk kayıt kodu verildikten sonra kısa kodu (MDM `code`) MDM'de değiştirilemez ve başka bir şirkete verilemez. Bu iş canlıya çıkana kadar Platform'daki ara dönem kilidi (409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`) geçerli kalır; bu paket o kilidi kaldırmaz |

### 0.2 Kaynaklar

| Kaynak | Ne diyor | Bu pakete etkisi |
|---|---|---|
| RCS-001 `.antigravity/rules/record-coding-standard.md:101-104` (§5.4) | Kısa kod 2–5 büyük harf, kiracıda benzersiz; "bir kez herhangi bir kaydın önekinde kullanıldıktan sonra **değiştirilemez**" | Kuralın kendisi |
| RCS-001 `.antigravity/rules/record-coding-standard.md:53` | Tüzel Kişilik kodu bugün "elle, gerekçesiz değişir — **uyumsuz**" | Kapatılan açık |
| `docs/analysis/contracts/record-code-allocate.md:193-195` (§7) | Ara dönem kilidi: kod verildikten sonra kısa kod değişirse ya da serbest kalan kısa kod başka şirkete verilirse yeni ayırma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; "MDM tarafındaki kalıcı kilit §12 N-12" | Bu paket N-12'dir |
| `docs/analysis/contracts/record-code-allocate.md:372` (§12 N-12) | Sahip "MDM sahibi (Ali) + Beste"; dondurmayı bekletmez | Sözleşme metni değişmez |
| `docs/analysis/contracts/record-code-allocate.md:185-192` (§7) | Hedef kural "aktif VE onaylı"; lookup-validation 1.0.0'da `approvalStatus` yok, v1.1 yayınlanana kadar servis yalnız aktifliği denetler, alan görünürse `APPROVED` dışı 422 | §0.4 Ö-1: kilit anı |
| `docs/analysis/contracts/record-code-allocate.md:121`, `:125`, `:141-143`, `:156` | Hata kodları `LEGAL_ENTITY_SHORT_CODE_CHANGED` (409), `LEGAL_ENTITY_NOT_ELIGIBLE` (422); değerlendirme sırası; önek = MDM `LegalEntity.Code` | Ad tutarlılığı (§12) |
| `origin/main:docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md` satır 195, 349, 372 | Donmuş metnin taslak kopyası; aynı cümleler | Ek bilgi yok |
| MOD-0220 paketi §21.5, §22 (`execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md:807-830`, `:883`, `:943-955`) | A1b (tüzel kişi yazıcılarının çite alınması) BLOCKED, yalnız plan; §22 lookup-validation'a `approvalStatus` ekler (onay bekliyor) | §7, §21 N-4, AK-8 |

### 0.3 Bugünkü kod (kod okuması, 2026-11-10)

**MDM (bu dal):**

- Kısa kod `LegalEntity.Code` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11`); doğrulama yalnız boş değil + en çok 64 karakter, kalıp yok (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityWriteRequestValidator.cs:12`). `^[A-Z]{2,5}$` kalıbı yalnız Platform'da (`origin/main:services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeFormat.cs:16,20`).
- Güncelleme kodu serbestçe değiştirir: `UpdateLegalEntityHandler` yalnız tekillik denetler (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs:36-41`), `ApplyEditableFields` kodu yazar (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:160-162`), depo CAS ile `Code`'u set eder (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs:40-47`).
- Tekillik dizini `ux_mdm_legal_entities_tenant_code` (TenantId, Code), **kısmi değil** (`LegalEntityRepository.cs:139-152`): yumuşak silinmiş satır kodunu dizinde tutar. Ön denetim `ExistsByCodeAsync` silinmişleri görmez (`LegalEntityRepository.cs:19-31`; `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:69-71`). Oluşturmada silinmiş bir şirketin koduyla gelen istek dizine çarpar; `CreateLegalEntityHandler` (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/CreateLegalEntityHandler.cs:19-23`, `:39`) ve API'de `DuplicateKey` eşlemesi yok (`git grep DuplicateKey -- services/Diten.MdmService/src/Diten.MdmService.Api`: 0) → büyük olasılıkla 500 (çalıştırılmadı).
- Yumuşak silme kodu ve satırı tutar (`RepositoryBase.cs:54-67`); kalıcı silme yolu yok.
- Yaşam döngüsü: oluşturma hep `Draft` (`CreateLegalEntityHandler.cs:35-37`). `Active`'e tek yol `ActivateLegalEntityHandler`, `Active` dışı her durumdan (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ActivateLegalEntityHandler.cs:25-38`); `Suspended` yalnız `Active`'ten (`SuspendLegalEntityHandler.cs:25-30`), `Archived` yalnız `Active` / `Suspended`'dan (`ArchiveLegalEntityHandler.cs:25-30`). Eski veri göçü eski `Archived`'i `Archived`'e taşır (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/LegalEntityOperationalStatusMigration.cs:12-17`).
- Etkinleştirme, askıya alma, arşiv bütün belgeyi sürüm denetimsiz değiştirir (`RepositoryBase.cs:40-52`, `ReplaceOneAsync`): eşzamanlı bir güncellemeyi sessizce ezebilir.
- `ApprovalStatus` yalnız oluşturmada yazılır (`LegalEntityModels.cs:150`), güncellemede yazılmaz (`:158-160`); "operator-set metadata only" (`services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LegalEntityApprovalStatus.cs:4`). Onay akışı yok.
- lookup-validation: `Active` değilse 404 (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-25`); yanıtta `approvalStatus` yok (`LegalEntityModels.cs:100-106`, `:238-245`).
- `LegalEntity` sınıf haritası **bilerek katı**: bilinmeyen alan taşıyan belge okunamaz (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs:67-68`, `:73-76`). Tüzel kişi belgesine yeni alan eklemek eski sürüme dönüşte okumaları kırar.
- Denetim: tüzel kişi komutları `IAuditableCommand` + `IAuditMetadataProvider` (ör. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/UpdateLegalEntityCommand.cs`); `AuditForwardingBehavior` reddi, komut `IAuditDetailsProvider`'a katılmışsa kod biçiminde kaydeder (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs:37-66`).
- İşlem ve dizin deseni: `MasterDataAtomicWrite` (snapshot / majority, geçici hatada yeniden koşu, sonucu bilinmeyen kesinleştirmede `Ambiguous`, işlemsiz sunucuda Development dışında `Unavailable`) ve `IndexOnce` (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs:31-99`, `:288`); seçenek `BrandProductStoreOptions` (`services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:467`).
- İzinler: güncelleme ve etkinleştirme `mdm.legal-entities.update` (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:64-74`), okuma `mdm.legal-entities.read` (`:24-50`).

**Platform (`origin/main`, bu paket değiştirmez):**

- Ayırma, şirket kısa kodunu MDM lookup-validation'dan okur (`origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/MdmLegalEntityShortCodeReader.cs:13,52`); `referenceable: true` ve `lifecycleState: "ACTIVE"` değilse kod vermez (`:115-121`); `approvalStatus` yalnız yanıtta varsa değerlendirilir, `APPROVED` dışı 422 (`:125-136`); kod kalıba uymuyorsa 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` (`:142`).
- Ara dönem kilidi: şirketin son öneki şimdikinden farklıysa ya da önek başka şirketin satırlarında varsa 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` (`origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs:176-186`; sorgular `origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RecordCodeRepositories.cs:164-192`; kod sabitleri `origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeReasonCodes.cs:20-22`). Testleri `origin/main:services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/AllocateRecordCodeHandlerTests.cs:199-217` ve `origin/main:services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeAllocationMongoTests.cs:482-507`.
- Ayırma bir **entegrasyon olayı yayımlamaz**: yalnız denetim kutusuna `platform.record-code.allocated` niyeti yazar (`origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeAuditIntent.cs:21-22`, `:30-40`); Platform'un olay yayımlayıcısında kayıt kodu kullanımı yok (`git grep RecordCode origin/main -- services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing`: 0).
- Platform'un iç kayıt kodu ucu yalnız ayırmadır (`origin/main:services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalRecordCodesController.cs:18,30`); "şirketin kodu kullanıldı mı" sorusunu yanıtlayan uç yok.

### 0.4 Tasarım sorusu 1 — MDM "ilk kod verildi"yi nereden bilir? (ölçüm ve öneri)

| Seçenek | Ölçüm | Değerlendirme |
|---|---|---|
| (a) Platform olayı | Ayırma entegrasyon olayı yayımlamıyor; yalnız denetim niyeti (§0.3) | Yeni olay = Platform değişikliği (XMC-001) + MDM tüketicisi; olay gecikmesi süresince kod değişebilir (pencere). **Elendi** |
| (b) MDM'nin Platform'a sorması | Böyle bir iç uç yok (§0.3) | Yeni Platform ucu (XMC-001) + her kod değişikliğinde MDM → Platform eşzamanlı bağımlılık (Platform kapalıyken kod düzeltilemez). Sahibin sözüne birebir uyar. **Yedek** (AK-1 reddedilirse) |
| (c-onay) İlk `APPROVED` | `ApprovalStatus` yalnız oluşturmada yazılan operatör verisi, akış yok; lookup-validation 1.0.0'da alan yok, Platform bugün onayı denetlemiyor (§0.3; sözleşme `:185-192`) | `LEGAL_ENTITY_NOT_ELIGIBLE` bugün hiç tetiklenmez: onaysız ama `Active` şirkete kod verilebilir. Onaya bağlı kilit pencere bırakır. **Elendi** |
| (c-verilebilirlik) **İlk "kod verilebilir" an** | Platform yalnız `Active` + `referenceable` + kalıba uyan koda kod verir (§0.3) | **Öneri (Ö-1)** |

**Ö-1 (öneri).** MDM kilidi, şirkete **kayıt kodu verilebilir olduğu ilk anda** koyar. Kod verilebilirlik koşulu P(e): `OperationalStatus` ∈ {`Active`, `Suspended`, `Archived`} **ve** `Code` `^[A-Z]{2,5}\z`'e (ASCII, sıralı karşılaştırma; Platform `RecordCodeFormat.cs:16` ile aynı) uyar **ve** — yalnız MOD-0220 §22 koda girmiş ve lookup-validation `approvalStatus` taşıyorsa — `ApprovalStatus = Approved`. `Suspended` / `Archived` P'dedir çünkü bu durumlara yalnız `Active`'ten geçilir (§0.3); kilit askıya alma ya da arşivle kalkmaz. P'yi sağlatan her yazım (etkinleştirme; P'yi sağlayan şirkette kodu kalıba uyduran güncelleme) kodu **aynı Mongo işleminde** kalıcı rezervasyona yazar.

**Gerekçe:** P, Platform'un kod verebilmesinin zorunlu koşuludur; kilit ilk koddan **önce ya da aynı anda** doğar, pencere kalmaz; başka modüle değişiklik gerekmez; MDM kendi verisinden karar verir.

**Sahip kararından sapma (açıkça):** sahip "ilk kayıt kodu verildikten sonra" dedi. Ö-1 daha erken kilitler: etkinleştirilmiş, kodu kalıba uyan ama **hiç kayıt kodu almamış** şirketin kısa kodu da değişmez. Ayrıca askıdaki şirkette kalıba uymayan kodu kalıba uyduran güncelleme, şirket yeniden etkinleşmeden kilitlenir. Kalıba uymayan kod (ör. `Acme-1`) kilitlenmez; Platform ona zaten kod vermez (409 `LEGAL_ENTITY_SHORT_CODE_MISSING`), düzeltilebilir kalır. Bu sapma **AK-1** ile sahibe sorulur; reddedilirse (b) yedektir ve Platform işi gerektirir.

### 0.5 Diğer öneriler

| # | Öneri | Pakete etkisi |
|---|---|---|
| Ö-2 | **Rezervasyon tek gerçek kaynaktır:** yeni koleksiyon `mdm_legal_entity_short_code_reservations`, kiracı başına kod başına tek satır, hiç silinmez. Tüzel kişi belgesine alan **eklenmez** (sınıf haritası katı; eski sürüme dönüşü ve bütün belge değiştiren askı / arşiv yazımlarının alanı ezmesini önler, §0.3) | §3, §4, §23 |
| Ö-3 | **Kilitli sayılma:** şirket için rezervasyon varsa **ya da** P(e) doğruysa kod kilitlidir. İkinci yarı, geri doldurma bitmeden açılan pencereyi kapatır | §12 |
| Ö-4 | **Geri doldurma:** açılışta, dizin hazır olunca, kiracı başına; P'yi sağlayan ve rezervasyonu olmayan her şirket (yumuşak silinmişler dahil) için sistem aktörlü `LockLegalEntityShortCodeCommand` MediatR'dan gönderilir; tekrarlanabilir | §3, §13, §15 |
| Ö-5 | **Tek anahtar:** `LegalEntityShortCodeLock:Enabled` (temel yapılandırmada `false`). Kapalıyken hiçbir davranış değişmez; açılması §23'teki uzlaştırma kapısına bağlı (AK-3). Kapatmak yeniden dağıtımsız geri dönüştür; rezervasyonlar kalır | §8, §23 |
| Ö-6 | **Etkinleştirme sürüm denetimli yazılır:** okunan sürüm ve kod filtrede; kaybeden 409 (bugünkü sessiz ezme kalkar). İstek biçimi değişmez (gövdesiz PATCH; MOD-0220 §21.5 madde 1'e dokunulmaz) | §12, §13 |
| Ö-7 | Hata adları Platform sözleşmesinin dilinde: 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`, 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`, 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, 503 `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN` | §12 (AK-2) |

## 1. Module Summary

Bir şirketin (MDM tüzel kişi) kısa kodunu, şirkete kayıt kodu verilebilir olduğu ilk anda kalıcı olarak kilitler ve kiracıda o şirkete rezerve eder: kilitli kod değiştirilemez, şirket silinse bile başka şirkete verilemez. Ekran kilitli kodu salt-okunur ve rozetle gösterir. Kullanıcı: MDM tüzel kişi sorumlusu; dolaylı tüketici: Platform kayıt kodu servisi ve onu kullanan modüller.

## 2. Ownership and Boundaries

**Kapsamda:** kod verilebilirlik koşulu (Ö-1), rezervasyon deposu, etkinleştirme ve güncellemede kilit ve rezervasyon yazımı, oluşturma / güncellemede rezerve kodun reddi, geri doldurma, anahtar, denetim, ayrıntı yanıtına kilit alanları, sihirbaz ve ayrıntı ekranında kilit gösterimi, 7 dil.

**Kapsam dışı:** Platform ara dönem kilidinin kaldırılması (Platform işi, MOD-0040-FU02, sahip Beste) · Platform'a yeni uç ya da olay (gerekmez; AK-1 reddedilirse ayrı iş) · MDM'de `^[A-Z]{2,5}$` kalıbının zorunlu kılınması (AK-4) · yanlış kilitlenen kodun açılması (yok; AK-6) · silinmiş kilitsiz taslağın kodunu serbest bırakmak (AK-7) · tüzel kişi yazıcılarının kapsam çitine alınması (MOD-0220 A1b) · lookup-validation sözleşmesine alan eklemek · liste ekranı.

## 3. Owned Objects

| Tür | Ad | |
|---|---|---|
| Entity | `LegalEntityShortCodeReservation` (`mdm_legal_entity_short_code_reservations`) | yeni |
| Kural | `LegalEntityShortCodeRule` (kalıp + P(e), tek yer) | yeni |
| Komut | `LockLegalEntityShortCodeCommand` (sistem aktörü, geri doldurma) | yeni |
| Komut | `ActivateLegalEntityCommand` | değişir: sürüm denetimli yazım + P doğrulanınca aynı işlemde rezervasyon |
| Komut | `UpdateLegalEntityCommand` | değişir: kilit ve rezervasyon denetimi; kodu P'ye sokan yazımda aynı işlemde rezervasyon; `IAuditDetailsProvider` |
| Komut | `CreateLegalEntityCommand` | değişir: rezerve kod 409; tekil dizin çarpması 500 değil 409 |
| Sorgu | `GetLegalEntityByIdQuery` | değişir: yanıta `shortCodeLocked`, `shortCodeLockedAt` |
| Koşucu | `LegalEntityShortCodeLockBackfillRunner` (açılış, kiracı başına) | yeni |
| Seçenek | `LegalEntityShortCodeLockOptions` (`Enabled`) | yeni |
| İzin | — | yok; mevcut `mdm.legal-entities.*` |

RCS-001: bu paket kayıt kodu üretmez; kayıt kodu öneklerinin kaynağını korur. Sınıf: İKİSİ DE değil.

## 4. Entity Fields

**`LegalEntityShortCodeReservation`** (`EntityBase`; `IgnoreExtraElements` kapsamında, katı köklere girmez)

| Alan | Tür | Kural |
|---|---|---|
| `Code` | string | Rezerve kısa kod, şirketten olduğu gibi kopyalanır (kalıba uyar) |
| `LegalEntityId` | Guid | Kodun sahibi |
| `ReservedAt` | DateTimeOffset | Kilit anı |
| `Source` | enum | `Activation` · `Update` · `Backfill` |
| `ReservedBy` | string | Kullanıcı kimliği ya da `system:mdm-short-code-backfill` |

Dizinler: tekil `ux_mdm_le_short_code_reservations_tenant_code` (TenantId, Code); tekil `ux_mdm_le_short_code_reservations_tenant_le` (TenantId, LegalEntityId) — şirket başına tek kilitli kod. Kısmi değil; `IsDeleted` hep `false`; serbest bırakma yöntemi yazılmaz.

Tüzel kişi belgesi (`LegalEntity`) değişmez (Ö-2).

## 5. Repo Scope

Tam liste §21 N-7. Özet: MDM Domain / Persistence / Application / Api yeni dosyaları; değişenler tüzel kişi komut işleyicileri, `LegalEntityModels.cs`, `LegalEntityRepository.cs`, `ILegalEntityRepository.cs`, `MdmDocumentClassMaps.cs`, `Program.cs`, `appsettings*.json`; testler (§18); DL-2 ön yüzü (§11).

## 6. Protected Paths

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `gateway/**` (`ocelot.json` yalnız integration-agent) · `execution/registries/**` · `docs/analysis/contracts/record-code-allocate.md` (donmuş 1.0.0) · `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` · MOD-0040-FU02 paketi. Birine dokunmak gerekirse XMC-001. Bu pakette Platform'a hiçbir değişiklik gerekmez (§7).

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| MOD-0220 tüzel kişi kodu | Kayıt, yaşam döngüsü, lookup-validation | kodda var (§0.3) |
| MOD-0220 §22 (TRM-14, `approvalStatus`) | P'nin onay terimi yalnız §22 koda girmişse | onay bekliyor; DL-1 prompt'u o günkü `ToLookupDto`'yu ölçer (§21 N-3) |
| MOD-0220 A1b (yazıcı çiti) | Aynı işleyicilere dokunur | BLOCKED, yalnız plan; eşgüdüm AK-8 |
| Platform ara dönem kilidi | Bu paket canlıya çıkana ve uzlaştırma bitene kadar tek koruma | kodda var, değişmez |
| Platform defteri uzlaştırma raporu | Anahtar açılmadan önce, salt-okunur sorgu Platform sahibince | **AK-3**; kod değişikliği değil |
| Denetim | `mdm-merkezi-iletim`, yol b (`tests/architecture/audit-ledger/Diten.MdmService.md`) | var |
| Gateway | `/api/legal-entities` mevcut | değişiklik yok |

**Başka modül değişikliği:** zorunlu olan yok. AK-1 reddedilirse seçenek (b) için Platform'da "şirketin LegalEntity kapsamlı kodu var mı" iç ucu ayrı iştir (MOD-0040-FU02 sahibi, XMC-001, sahip izni gerekir); bu paketin Repo Scope'una Platform dosyası girmez.

## 8. Runtime Constraints

MDM 5059, Gateway 5000, Web 5001. Her sorgu ve uç `TenantId` ile; başka kiracının şirketi 404; rezervasyon tekilliği yalnız kiracı içinde (başka kiracı aynı kodu kullanabilir, denetlenmez, sızdırılmaz). Tüzel kişi kiracı geneli kayıttır; tüzel kişi kapsam çiti bu kayda uygulanmaz (MOD-0220 A1b kapsamı). Silme yalnız yumuşak; rezervasyon silinmez. **İşlem:** rezervasyon yazan her yol (etkinleştirme, kodu P'ye sokan güncelleme, geri doldurma) `MasterDataAtomicWrite` ile tek işlem; işlemsiz sunucuda Development dışında 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`; Development'ın sıralı yazımında önce rezervasyon, sonra şirket (yarıda kalırsa fazladan kilit kalır, eksik kilit kalmaz). Sonucu bilinmeyen kesinleştirme 503 `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN`. **Dizin hazırlığı:** iki tekil dizin `IndexOnce` ile; hazır değilse rezervasyon gerektiren yazım 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, geri doldurma o açılışta atlar ve sonraki açılışta dener. Rezervasyon gerektirmeyen yazımlar (kilitli şirkette kod aynı kalan güncelleme, kalıba uymayan kodla etkinleştirme) bugünkü yoldan gider. **Anahtar:** `LegalEntityShortCodeLock:Enabled = false` iken bu paketin hiçbir denetimi ve yazımı çalışmaz, ayrıntı yanıtı `shortCodeLocked: false` döner.

## 9. Layout & Shell Contract

`shell: tenant`. Değişen görünümler (`Wizard.cshtml`, `Details.cshtml`) `@{ Layout = "_LayoutTenantShell"; }` satırını **açıkça** taşır (bugün `frontend/Diten.Web/Views/MasterData/LegalEntities/Wizard.cshtml:9`); satır korunur. Kabul: AC-13.

## 10. Backend File Convention

Golden Reference compact düzeni MOD-0220'nin mevcut özellik klasöründe sürer (`Features/LegalEntity/`: `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, `LegalEntityModels.cs`). Yeni dosyalar:

```text
Domain/Entities/LegalEntityShortCodeReservation.cs
Domain/ValueObjects/LegalEntityShortCodeRule.cs
Application/Features/LegalEntity/Commands/LockLegalEntityShortCodeCommand.cs        (sealed record; IAuditableCommand,
                                                                                     IAuditMetadataProvider, IAuditDetailsProvider,
                                                                                     IAuditTenantProvider, IAuditSystemActor)
Application/Features/LegalEntity/Handlers/CommandHandlers/LockLegalEntityShortCodeHandler.cs
Application/Features/LegalEntity/LegalEntityShortCodeLockOptions.cs
Persistence/Repositories/LegalEntityShortCodeReservations.cs                         (internal; ExternalReferenceReservations deseni)
Api/Services/LegalEntities/LegalEntityShortCodeLockBackfillRunner.cs
```

Handler / Validator adında `Command` / `Query` eki yok; `Response<T>` zarfı; hata metni kod biçiminde (`Response<…>.Fail("LEGAL_ENTITY_SHORT_CODE_LOCKED", 409)`). İşlem ve rezervasyon depoda (`LegalEntityRepository`), işleyicide değil (`handler-design.md`). `BulkDelete` yok: ana veri (mevcut MOD-0220 davranışı).

## 11. Frontend File Contract

Yalnız DL-2. Liste dosyalarına (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js`) dokunulmaz. MOD-0220 ekranı standart Compact setinde değil (sihirbaz: `Wizard.cshtml`, `Details.cshtml`); bu paket ekranı yeniden düzenlemez, bilinen sapma olarak kalır (§22 F-4).

| Dosya | Değişiklik |
|---|---|
| `frontend/Diten.Web/Views/MasterData/LegalEntities/Wizard.cshtml` | Kod alanının (`leCode`, `:139-140`) altında açıklama yeri; kilitliyken kilit simgesi |
| `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/wizard.js` | Düzenlemede `shortCodeLocked` doğruysa `leCode` `readonly` + `ShortCodeLockedHint`; değilse `ShortCodeLockNotice`; 409 / 503 kodlarını yerel metne çevirir (bugün ham metin gösterir, `:334-336`) |
| `frontend/Diten.Web/Views/MasterData/LegalEntities/Details.cshtml` + `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/details.js` | Kod yanında `ShortCodeLockedBadge` rozeti; etkinleştirme onayında kod kalıba uyuyor ve kilitsizse `ActivateConfirmShortCodeLock` (`patchLifecycle`, `:111-121`); 503 kodları yerel metin |
| `frontend/Diten.Web/Views/MasterData/LegalEntities/_WizardL10n.cshtml`, `frontend/Diten.Web/Views/MasterData/LegalEntities/_DetailsL10n.cshtml` | Yeni anahtarlar JS yüküne |
| `frontend/Diten.Web/Resources/Views/MasterData/LegalEntities/LegalEntitiesIndex.{en,tr,fr,es,zh,ar,ru}.resx` | 7 anahtar × 7 dil (aşağıda) |

| Anahtar | en | tr |
|---|---|---|
| `ShortCodeLockedBadge` | Locked | Kilitli |
| `ShortCodeLockedHint` | This short code is permanently locked: record codes may already carry it. | Bu kısa kod kalıcı olarak kilitli: kayıt kodları bu kodu taşıyor olabilir. |
| `ShortCodeLockNotice` | A short code of 2–5 capital letters is locked permanently once the company is active. | 2–5 büyük harfli kısa kod, şirket etkin olduğunda kalıcı olarak kilitlenir. |
| `ActivateConfirmShortCodeLock` | The company will be activated and its short code {0} will be locked permanently. Continue? | Şirket etkinleştirilecek ve kısa kodu {0} kalıcı olarak kilitlenecek. Devam edilsin mi? |
| `ErrorShortCodeLocked` | The short code is locked and cannot be changed. | Kısa kod kilitli; değiştirilemez. |
| `ErrorShortCodeReserved` | This short code belonged to another company and cannot be reused. | Bu kısa kod başka bir şirkete ait; yeniden kullanılamaz. |
| `ErrorShortCodeStoreUnavailable` | The change could not be saved right now. Try again. | Değişiklik şu anda kaydedilemedi. Yeniden deneyin. |

fr, es, zh, ar, ru metinleri DL-2'de yazılır (ar sağdan sola). Satır içi CSS yok (FG-003); sınıflar mevcut tema dosyasında. Yetkisiz kullanıcıya iskelet yok (UAS-001, mevcut davranış).

## 12. Validation Rules

| Kural | Ne zaman | Hata |
|---|---|---|
| Kalıp `^[A-Z]{2,5}\z`, ASCII, sıralı | P(e) hesabında; MDM girişini **zorlamaz** (AK-4) | — |
| Kilitli şirkette kod değişikliği | Güncelleme: kilitli (Ö-3) **ve** `Trim` sonrası yeni kod ≠ mevcut kod (sıralı; `abc` ≠ `ABC`) | 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED` |
| Rezerve kod | Oluşturma / güncelleme: yeni kod başka şirketin rezervasyonunda (sahibi silinmiş olsa da) | 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` |
| Kilitli şirkette diğer alanlar | Kod aynıysa güncelleme bugünkü gibi | — |
| Kodu P'ye sokan güncelleme | P güncellemeden önce yanlış, sonra doğru (ör. askıdaki ya da etkin şirkette kalıba uymayan kod kalıba uyduruldu) | Yazım + rezervasyon tek işlem; rezervasyon çakışırsa 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`, hiçbir şey yazılmaz |
| Etkinleştirme | Okunan sürüm + kodla koşullu yazım; P doğruysa aynı işlemde rezervasyon (`Source = Activation`) | Kaybeden 409 (mevcut metin "The Legal Entity has changed since it was loaded."); rezervasyon çakışırsa 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` |
| Oluşturmada tekil dizin çarpması (silinmiş kilitsiz şirketin kodu) | `DuplicateKey` | 409, mevcut metin "A Legal Entity with this code already exists." (bugün 500) |
| Dizin hazır değil / işlem yok | Rezervasyon gerektiren yazım | 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE` |
| Sonucu bilinmeyen kesinleştirme | Aynı | 503 `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN` |

**Güncelleme değerlendirme sırası:** 400 `ExpectedVersion` → 404 → 409 eski sürüm → 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED` → 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` → 409 kod var (mevcut metin) → ebeveyn denetimleri → yazım. Kilit denetimi tekillikten önce gelir: kilitli şirketin kullanıcısı başka şirketin varlığını öğrenmez.

## 13. Failure Path to Verify

| Durum | Beklenen |
|---|---|
| Etkinleştirilmiş, kodu kalıba uyan şirkette kod değişikliği | 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`, belge değişmez |
| Askıdaki / arşivdeki kilitli şirkette kod değişikliği | Aynı 409 |
| Taslak şirkette ya da kalıba uymayan kodlu etkin şirkette kod değişikliği | Bugünkü gibi 204 |
| Kilitli şirket silindi, başka şirket aynı kodla oluşturuluyor / güncelleniyor | 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` |
| Başka kiracıda aynı kod | Engellemez; başka kiracının şirketi 404 |
| Eşzamanlı etkinleştirme + kod değiştiren güncelleme | Tam biri başarılı; kilitlenen kod, belgede kalan kodla aynı; öbürü 409 |
| Aynı kodu iki şirkete eşzamanlı rezervasyon | Tekil dizin; tam biri başarılı |
| Rezervasyon yazımı işlem içinde başarısız | Şirket de değişmez (iki koleksiyon) |
| Dizin hazır değil | 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, iki koleksiyon değişmez; dizin hazır olunca aynı istek başarılı |
| Geri doldurma ikinci koşu | Değişiklik yok; açılış durmaz |
| Geri doldurma öncesi pencerede (anahtar açık) kilitli sayılması gereken şirket | Ö-3 ikinci yarısıyla kilitli; değişiklik 409 |
| Anahtar kapalı | Hiçbir yeni davranış yok (bugünkü testler yeşil) |
| İzinsiz kullanıcı | 403; ekran iskelet çizmez (mevcut) |
| Denetim iletimi başarısız | İş durmaz, uyarı (yol b) |

## 14. Authorization Convention

```text
Policy:     [Authorize] + [HasPermission(...)] her eylemde (değişmez)
Permission: mdm.legal-entities.update (güncelleme, etkinleştirme) · mdm.legal-entities.create · mdm.legal-entities.read
Actor type: tenant_user; geri doldurmada sistem aktörü system:mdm-short-code-backfill (IAuditSystemActor)
Scope:      Tenant (tüzel kişi kiracı geneli kayıttır)
```

Yeni anahtar, manifest değişikliği, Auth değişikliği yok. Kilit açma eylemi yok (AK-6).

## 15. Audited Events (AUD-001)

Tek yol: b, iz `mdm-merkezi-iletim`. İstisna sınıfı kullanılmaz. Olay adı `AuditDetails` meta verisine `EventName` olarak yazılır.

| Komut | Olay | Nesne | Yol | Önce / sonra | İstisna |
|---|---|---|---|---|---|
| `ActivateLegalEntityCommand` (değişir) | mevcut etkinleştirme; kilit doğduysa ek `legal-entity.short-code.locked` meta verisi (kod, kaynak `Activation`) | LegalEntity | b | Sonra: durum, kilitlenen kod | — |
| `UpdateLegalEntityCommand` (değişir, `IAuditDetailsProvider`'a katılır) | mevcut güncelleme; ret `LEGAL_ENTITY_SHORT_CODE_LOCKED` / `_RESERVED` kod biçiminde; kilit doğduysa `legal-entity.short-code.locked` (kaynak `Update`) | LegalEntity | b | Önce / sonra: kod (ret kaydında istenen kod meta veride) | — |
| `CreateLegalEntityCommand` (değişir) | mevcut oluşturma; ret `LEGAL_ENTITY_SHORT_CODE_RESERVED` kod biçiminde | LegalEntity | b | Sonra: kod | — |
| `LockLegalEntityShortCodeCommand` (yeni) | `legal-entity.short-code.locked`, şirket başına | LegalEntity | b, MediatR hattından, **sistem aktörü** | Sonra: kod, kaynak `Backfill` | — |

Denetim defteri dosyası (`tests/architecture/audit-ledger/Diten.MdmService.md`) değişmez: yeni komut mevcut işaretle kapsanır; `AuditTrailStandardTests` onu öbür komutlar gibi görür.

## 16. Gateway / API Routing Decision

Yeni uç yok. Mevcut `/api/legal-entities` rotaları ve MVC vekili (`frontend/Diten.Web/Controllers/LegalEntitiesController.cs:62-99`) yeterli; Gateway satırı gerekmez. Ayrıntı yanıtına iki alan eklenir (`shortCodeLocked`, `shortCodeLockedAt`; ekleme). lookup-validation yanıtı **değişmez** (Platform okuyucusu etkilenmez).

## 17. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-1: etkinleştirilmiş ve kodu `^[A-Z]{2,5}\z`'e uyan şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`; askıya alınmış ve arşivlenmiş durumda da 409; aynı kodla diğer alanların güncellemesi 204.
- [ ] AC-2 (zorunlu) — DL-1: taslak şirkette ve kodu kalıba uymayan etkin şirkette kod değişikliği bugünkü gibi 204; kalıba uymayan kodu kalıba uyduran güncelleme 204 döner ve aynı işlemde `Source = Update` rezervasyonu yazar.
- [ ] AC-3 (zorunlu) — DL-1: etkinleştirme, kod kalıba uyuyorsa aynı Mongo işleminde `Source = Activation` rezervasyonu yazar; işlem içi hata kancasıyla iki koleksiyondan hiçbiri değişmez.
- [ ] AC-4 (zorunlu) — DL-1: kilitli şirket yumuşak silindikten sonra aynı kodla oluşturma ve başka şirketin aynı koda güncellenmesi 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`; rezervasyon satırı silinmez.
- [ ] AC-5 (zorunlu) — DL-1: başka kiracının aynı kodu engellemez; başka kiracının şirketi için güncelleme ve etkinleştirme 404.
- [ ] AC-6 (zorunlu) — DL-1: eşzamanlı etkinleştirme + kod değiştiren güncellemede tam biri başarılı ve rezervasyondaki kod belgedeki kodla aynı; aynı koda eşzamanlı iki rezervasyonda tam biri başarılı.
- [ ] AC-7 (zorunlu) — DL-1: geri doldurma P'yi sağlayan her şirkete (yumuşak silinmiş dahil) tek `Source = Backfill` rezervasyonu yazar, P'yi sağlamayanlara yazmaz; ikinci koşu değişiklik üretmez; açılış durmaz; komut MediatR'dan sistem aktörüyle gider.
- [ ] AC-8 (zorunlu) — DL-1: geri doldurma koşmadan, anahtar açıkken P'yi sağlayan şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED` (Ö-3).
- [ ] AC-9 (zorunlu) — DL-1: rezervasyon dizinleri hazır değilken rezervasyon gerektiren etkinleştirme ve güncelleme 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE` döner, iki koleksiyon değişmez; dizin hazır olunca aynı istek başarılı.
- [ ] AC-10 (zorunlu) — DL-1: anahtar kapalıyken mevcut tüzel kişi testlerinin hepsi değişmeden yeşil ve hiçbir rezervasyon yazılmaz.
- [ ] AC-11 (zorunlu) — DL-1: P'nin her birleşimi (6 işletme durumu × kod kalıba uyar / uymaz × yumuşak silinmiş / değil; §22 koda girmişse × 4 onay durumu) için "kilitlenir" sonucu, MDM'nin gerçek `ValidateLegalEntityReferenceHandler` yanıtının sözleşme §7 koşulunu (200, `referenceable: true`, `lifecycleState: ACTIVE`, `approvalStatus` yok ya da `APPROVED`, kod kalıba uyar) sağlamasıyla `Active` satırlarında aynı; `Suspended` / `Archived` satırlarında aynı kod (ve varsa onay) koşuluyla kilitlenir; `Draft`, `InReview`, `Approved` işletme durumlarında kilitlenmez.
- [ ] AC-12 (zorunlu) — DL-1: §15'teki her yazma ve ret tam bir yol b kaydı üretir; ret kaydı kod biçiminde (`LEGAL_ENTITY_SHORT_CODE_LOCKED` / `_RESERVED`); geri doldurma kaydı sistem aktörlü; oluşturmada silinmiş kilitsiz şirketin koduyla gelen istek 500 değil 409.
- [ ] AC-13 (zorunlu) — DL-2: düzenleme sihirbazında kilitli şirketin kod alanı salt-okunur ve `ShortCodeLockedHint` görünür; ayrıntıda `ShortCodeLockedBadge`; kodu kalıba uyan kilitsiz şirketin etkinleştirme onayı `ActivateConfirmShortCodeLock`; 409 / 503 kodları yerel metinle; `Layout = "_LayoutTenantShell"` korunur.
- [ ] AC-14 (zorunlu) — DL-2: 7 yeni anahtar 7 dilde; `LegalEntityL10nContractTests` anahtar eşitliği yeşil; satır içi CSS yok.
- [ ] AC-15 (zorunlu) — DL-1 ve DL-2: §18 sabotajlarının her biri adlı testte kırmızı, geri alınca yeşil; `git status --porcelain` boş.
- [ ] AC-16 (isteğe bağlı) — DL-2: sayfa yardımında kilit kuralı (§19).

## 18. Test Expectations

Üretim koduyla, gerçek Mongo (replica set, `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs`) ile; test çifti yalnız denetim iletimi için. Bellek içi depo (`InMemoryLegalEntityRepository`) yalnız mevcut komut testlerinin derlenmesi için genişler; kilit kabulü onunla ölçülmez.

| Test | Ölçer |
|---|---|
| `LegalEntityShortCodeRuleTests` (yeni) | Kalıp (ASCII, `\z`, büyük-küçük harf), P birleşimleri; AC-11'in tablo yarısı |
| `LegalEntityShortCodeLockMongoTests` (yeni) | AC-1…AC-4, AC-6, AC-8, AC-9, AC-10 gerçek depo ve işlemle |
| `LegalEntityShortCodeLockHttpTests` (yeni, Api.Tests) | AC-1, AC-4, AC-5, AC-12 uçtan (kod, durum, kiracı 404) |
| `LegalEntityShortCodeBackfillTests` (yeni) | AC-7 |
| `LegalEntityShortCodeEligibilityContractTests` (yeni) | AC-11: gerçek `ValidateLegalEntityReferenceHandler` yanıtı ↔ kilit sonucu |
| `LegalEntityShortCodeAuditTests` (yeni) | AC-12 |
| `LegalEntityCommandTests`, `LegalEntityEditableCasMongoTests`, `LegalEntityUpdateContractTests`, `LegalEntityReferenceValidationTests` (mevcut) | AC-10: anahtar kapalıyken değişmeden yeşil |
| `MdmDocumentClassMapsTests` (mevcut) | Yeni tür kapsanan kümede, `LegalEntity` katı kalır |
| `LegalEntityL10nContractTests` (mevcut, DL-2) | AC-14 |

**Sabotajlar (her biri adlı testte kırmızı):** (1) güncelleme işleyicisinden kilit denetimini kaldır → AC-1; (2) P'den `Suspended` / `Archived`'i çıkar → AC-1 askı / arşiv satırı; (3) etkinleştirmede rezervasyonu işlem dışına, ayrı yazıma taşı → AC-3 hata kancası; (4) rezervasyon ön denetiminde sahibi silinmiş satırları yok say → AC-4; (5) rezervasyon dizininden `TenantId`'yi çıkar → AC-5; (6) etkinleştirme filtresinden okunan sürümü çıkar → AC-6; (7) geri doldurma filtresinden yumuşak silinmişleri çıkar → AC-7; (8) Ö-3'ün ikinci yarısını (P doğruysa kilitli) kaldır → AC-8; (9) depoda dizin hazırlık denetimini kaldır → AC-9; (10) anahtar denetimini kaldır → AC-10; (11) kalıbı `^[A-Za-z]{2,5}` yap → AC-11 / kural testi; (12) güncelleme komutunu `IAuditDetailsProvider`'dan çıkar → AC-12 ret kaydı; (13) geri doldurmada rezervasyonu MediatR'sız doğrudan yaz → AC-12 (`AuditTrailStandardTests` / `LegalEntityShortCodeAuditTests`); (14) DL-2: düzenlemede `readonly` atamasını kaldır → AC-13 ekran testi.

## 19. Page Help (UXH-001)

DL-2, isteğe bağlı (AC-16): 7 dilde — kısa kodun kayıt kodlarının öneki olduğu, ne zaman kilitlendiği, kilitli kodun neden değişmediği ve silinen şirketin kodunun neden yeniden kullanılmadığı.

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (2026-11-10, `3d80be94e`)
- [x] Künye; `golden_reference: compact` (ev sahibi form 31 alan), `data_mode: client`, `data_mode_max_rows: 200`
- [x] Layout `_LayoutTenantShell` (§9)
- [x] Validation, Failure Path, Authorization, Gateway, Audited Events
- [x] Teslim dilimleri, kanıt seviyesi, plan sütunları
- [x] Tasarım sorusu 1 kodla ölçüldü, tek öneri (§0.4)
- [ ] **AK-1** sahip: kilit anı sapması (Ö-1) — DL-1 prompt'undan önce zorunlu
- [ ] AK-2 hata adları (kule)
- [ ] AK-3 Platform defteri uzlaştırma raporu — anahtar açılmadan önce zorunlu (DL-1 kodunu bekletmez)
- [ ] AK-5 `target`, onaylayan, ikinci kişi — TBD
- [ ] AK-8 MOD-0220 A1b eşgüdümü
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01)

## 21. Implementation Notes

- **N-1 İki dilim.** DL-1 arka uç tek başına kilidi kurar; ekran DL-2'ye kadar ret kodunu ham gösterir (`wizard.js:334-336`). Bu yüzden anahtar DL-2 dağıtılmadan pilotta açılmaz (§23).
- **N-2 Kilit yazımı depoda.** `LegalEntityRepository` iki yeni yöntem alır: sürüm + kod koşullu etkinleştirme ve rezervasyon (tek işlem); kodu P'ye sokan güncellemede `UpdateEditableFieldsAsync`'in rezervasyonlu kardeşi. Rezervasyon sınıfı `ExternalReferenceReservations` desenindedir (`MasterDataAtomicWrite.cs:161-274`) ama **serbest bırakma yöntemi yoktur**. Çakışma `MasterDataAtomicWrite.IsDuplicateKey` (`:148-154`) ile tanınır; hangi dizine çarpıldığını yeni kod hata iletisindeki dizin adından ayırır: kod dizini → `RESERVED`, şirket dizini → aynı şirkete eşzamanlı ikinci kilit (zaten kilitli; sonuç başarılı sayılır).
- **N-3 Onay terimi (§22).** DL-1 prompt'u o günkü `LegalEntityModels.ToLookupDto`'yu ölçer: `ApprovalStatus` alanı varsa P onay terimini içerir, yoksa içermez. §22 DL-1'den sonra koda girerse onay terimi `LegalEntityShortCodeRule`'a eklenir ve AC-11 tablosu genişler; bu, §22'nin kendi işine değil, bu paketin takip satırına (F-2) yazılır. Onay sonradan değişmediği için (yalnız oluşturmada yazılıyor, §0.3) yeni bir kilit tetikleyicisi doğmaz; ileride onay geçişi eklenirse o geçiş de P'yi denetlemelidir (F-3).
- **N-4 MOD-0220 A1b ile ilişki.** Bu paket tüzel kişi belgesine yeni alan, yeni yazıcı ya da açılışta tüzel kişi belgesine yazan göç eklemez (geri doldurma yalnız rezervasyon koleksiyonuna yazar); A1b §21.5 madde 3'teki "çit dışı otomatik yazıcı" sorununu büyütmez. Etkinleştirme yazımını değiştirir (Ö-6) ama istek biçimini değiştirmez (§21.5 madde 1). A1b sonradan açılırsa bu paketin testleri yeşil kalmalı (AK-8).
- **N-5 Geri doldurma.** Kiracı listesi `mdm_legal_entities`'teki ayrık `TenantId` değerleri (yumuşak silinmişler dahil). Koşucu kiracı başına `TenantContext.SetTenant` ile kapsam açar ve şirket başına `LockLegalEntityShortCodeCommand` gönderir (sistem aktörü + `IAuditTenantProvider`; emsal `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ItemMaster/Commands/ApplyItemActivationDecisionCommand.cs`). Komut özelliklerinde `LegalEntityId` ilk `Guid` özelliğidir (denetimin varlık kimliği, `AuditForwardingBehavior.cs:83-103`). Koşu `Enabled = false` iken hiçbir şey yapmaz. "Rezervasyon yok" filtresi eksik alanı da yakalamalı (Mongo yeni alan dersi: `Eq(false)` alanı olmayan eski belgeyi kaçırır; burada ayrı koleksiyon, `$lookup` yerine kiracı başına iki okuma).
- **N-6 Kod yolu sırası.** Kilit denetimi, gelen kodun `Trim`'den sonraki hâliyle mevcut `entity.Code`'u sıralı karşılaştırır (`LegalEntityModels.cs:162` aynı `Trim`'i yapar).
- **N-7 Dosyalar.** Yeni (her satırda bir yol):
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntityShortCodeReservation.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/LegalEntityShortCodeRule.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/LockLegalEntityShortCodeCommand.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/LockLegalEntityShortCodeHandler.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityShortCodeLockOptions.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityShortCodeReservations.cs` (yeni)
  - `services/Diten.MdmService/src/Diten.MdmService.Api/Services/LegalEntities/LegalEntityShortCodeLockBackfillRunner.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityShortCodeRuleTests.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityShortCodeLockMongoTests.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityShortCodeBackfillTests.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityShortCodeEligibilityContractTests.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityShortCodeAuditTests.cs` (yeni)
  - `services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/LegalEntityShortCodeLockHttpTests.cs` (yeni)

  Değişen:
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/UpdateLegalEntityCommand.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/ActivateLegalEntityCommand.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/CreateLegalEntityCommand.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ActivateLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/CreateLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/GetLegalEntityByIdHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/appsettings.json`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/appsettings.Development.json`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/InMemoryLegalEntityRepository.cs`
  - `frontend/Diten.Web/Views/MasterData/LegalEntities/Wizard.cshtml` (DL-2)
  - `frontend/Diten.Web/Views/MasterData/LegalEntities/Details.cshtml` (DL-2)
  - `frontend/Diten.Web/Views/MasterData/LegalEntities/_WizardL10n.cshtml` (DL-2)
  - `frontend/Diten.Web/Views/MasterData/LegalEntities/_DetailsL10n.cshtml` (DL-2)
  - `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/wizard.js` (DL-2)
  - `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/details.js` (DL-2)
  - `frontend/Diten.Web/Resources/Views/MasterData/LegalEntities/` — 7 dil dosyası `LegalEntitiesIndex.{en,tr,fr,es,zh,ar,ru}.resx` (DL-2)

  Dayanılan (değişmez, incelemede okunur):
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LegalEntityApprovalStatus.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LegalEntityOperationalStatus.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityWriteRequestValidator.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/LegalEntityOperationalStatusMigration.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityEditableCasMongoTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityUpdateContractTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityReferenceValidationTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityL10nContractTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmDocumentClassMapsTests.cs`
  - `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`
  - `tests/architecture/audit-ledger/Diten.MdmService.md`
  - `frontend/Diten.Web/Controllers/LegalEntitiesController.cs`
  - `docs/analysis/contracts/record-code-allocate.md`
  - `.antigravity/rules/record-coding-standard.md`
  - `execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md`
  - Platform (yalnız `origin/main`, okuma): `origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/MdmLegalEntityShortCodeReader.cs`, `origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs`, `origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RecordCodeRepositories.cs`
- **N-8 SAP / Oracle.** SAP'de şirket kodu (BUKRS) anahtar alandır, yeniden adlandırılmaz (yalnız kopyala + taşı aracıyla, özel proje). Oracle Fusion'da tüzel kişi tanımlayıcısı oluşturulduktan sonra değişmez. Ö-1 ikisinden yumuşaktır: taslakta düzeltme serbest, kilit yalnız kod verilebilir olunca.
- **N-9 Plan.** Plan satırı verilmedi; dilim sütunlarında `plan kimliği yok` / `plan satırı yok`.
- Dilim tablosu `## 24. Teslim dilimleri` altında (`scripts/pack_approval.py` `SLICE_HEADING`).
- Kurallar: `module-pack-standard.md`, `handler-design.md`, `erp-architecture.md`, `response-envelope.md`, `multi-tenancy`, `localization-standard`, `audit-trail-standard.md`, RCS-001.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | Platform ara dönem kilidinin kaldırılıp kaldırılmayacağı (öneri: kalsın, ucuz ikinci koruma). Önkoşul: bu paket ilgili ortamda anahtar açık, geri doldurma bitti, AK-3 raporu sıfır | Platform (Beste) | 🟢 kaldırılmazsa yalnız çift denetim |
| F-2 | §22 bu paketten sonra koda girerse P'ye onay terimi + AC-11 genişlemesi | MDM | 🟡 eklenmezse onaysız etkin şirket kilitlenir (daha katı, güvenli yön) |
| F-3 | İleride onay geçişi (akış) eklenirse P'yi denetlemeli | MDM | 🟡 |
| F-4 | MOD-0220 ekranının Compact setine (`Create` / `Edit` / `Details` / `_Form`) dönüşü | MDM | 🟢 bilinen sapma |
| F-5 | RCS-001 `:53` satırının "uyumlu" olarak güncellenmesi (`.antigravity`, sahip onayı) | Sahip | 🟢 |
| F-6 | Sözleşme N-12'nin kapandı notu (yeni sürümde, ekleme) | Platform (Beste) | 🟢 |
| F-7 | `module-implementation-status.md` satırı, kod gelince | Kule | 🟢 |

## 23. Canlı sıra ve geri dönüş

**Kanıt.** DL-1 ve DL-2 kabulü E4: otomatik test + gerçek Mongo kalıcılığı + kiracı izolasyonu + denetim + hata yolları (yerel). Canlı kabul pilot kiracıda anahtar açıldıktan sonra.

**Sıra:**
1. MDM DL-1 + DL-2 dağıtımı, anahtar **kapalı** (davranış değişmez).
2. **Uzlaştırma kapısı (AK-3):** Platform sahibi, ortamın `record_code_allocations` koleksiyonunda `PrefixScope = LegalEntity` satırlarından (TenantId, LegalEntityId) başına son öneki ve önek kümesini salt-okunur çıkarır; MDM tarafı aynı şirketlerin şimdiki `Code`, `OperationalStatus`, `IsDeleted` değerlerini verir. Uyuşmazlık türleri: (M1) şirketin son öneki ≠ şimdiki kodu — bu şirket bugün ara dönem kilidiyle kod alamıyor; anahtar açılırsa yanlış kod **kalıcı** kilitlenir; önce kod önekine geri alınır. (M2) A'nın öneki B'nin şimdiki kodu — B önce yeniden adlandırılır (sahip kararı). Rapor sıfır uyuşmazlık göstermeden anahtar açılmaz. Rapor kuleye verilir ve kayda girer.
3. Anahtar pilotta açılır; açılış geri doldurmayı koşar. **Kademeli dağıtım:** anahtar açıkken eski kopyalar kilit denetimsizdir; bütün eski kopyalar durduktan sonra yeni kopyalardan biri yeniden başlatılır ve geri doldurma yeniden koşar (tekrarlanabilir).
4. Pilot denetimi: kodu kalıba uyan etkin bir şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`; silinmiş kilitli şirketin koduyla oluşturma 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`; Platform ayırması aynı şirkete kod vermeye devam eder.
5. Canlı ortamda aynı sıra (2–4).

**Geri dönüş.** Önce anahtar kapatılır (yeniden dağıtım yok; kilit denetimi durur, rezervasyonlar kalır; Platform ara dönem kilidi koruma olarak sürer). Eski MDM sürümüne dönüş güvenlidir: tüzel kişi belgesi değişmediği için katı sınıf haritası okumaları kırılmaz (Ö-2); eski sürüm rezervasyon koleksiyonunu görmez. Yeniden ileri dönüşte, anahtar kapalı geçen sürede kodu değişen şirketler için 2. adım yeniden koşulur.

**Platform bağlantıları (DL-2):** menü · Ctrl+K · 7 dil · yetkisiz yüz · sayfa yardımı · denetim mevcut ekranda; bu paket yalnız sihirbaz ve ayrıntı sayfasını değiştirir, satır başına durum DL-2 prompt'unda ölçülür.

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | kısa kod kilidi ve kalıcı rezervasyon: kod verilebilirlik kuralı, etkinleştirme ve güncellemede tek işlem, oluşturma / güncelleme reddi, geri doldurma, anahtar, denetim (arka uç) | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events (AUD-001), Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Implementation Notes, Canlı sıra ve geri dönüş | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | sihirbaz ve ayrıntı ekranında kilit gösterimi, etkinleştirme onayı, hata metinleri ve 7 dil | Repo Scope, Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Acceptance Criteria, Test Expectations, Canlı sıra ve geri dönüş | E4 | plan kimliği yok | plan satırı yok |

DL-1 çekirdektir; DL-2 DL-1'e dayanır. Anahtar pilotta DL-2 dağıtılmadan açılmaz (§21 N-1).

## 25. Açık kararlar

| # | Karar | Kim | Durum |
|---|---|---|---|
| AK-1 | Kilit anı: Ö-1 (ilk "kod verilebilir" an) sahibin "ilk kayıt kodu verildikten sonra" sözünden **daha erken** kilitler (§0.4 sapma paragrafı). Kabul mü, yoksa seçenek (b) (Platform iç ucu, ayrı XMC-001 işi, Beste + sahip izni) mi? | Sahip | **Açık — DL-1 prompt'undan önce zorunlu** |
| AK-2 | Hata adları `LEGAL_ENTITY_SHORT_CODE_LOCKED`, `LEGAL_ENTITY_SHORT_CODE_RESERVED`, `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN` (Ö-7); Platform sözleşmesi değişmez, Beste'ye bilgi | Kule | Açık |
| AK-3 | Uzlaştırma raporu (§23 adım 2): Platform defterinde salt-okunur sorgu Platform sahibince; başka modülün verisi olduğu için sahip izni ve Beste'nin kabulü gerekir | Sahip + Beste | **Açık — anahtar açılmadan önce zorunlu**; DL-1 kodunu bekletmez |
| AK-4 | MDM'de `^[A-Z]{2,5}$` kalıbını giriş kuralı yapmak (bugün 64 karaktere kadar serbest; mevcut kodları kırabilir) | Sahip | Açık; bu paketin dışında, öneri: ayrı takip |
| AK-5 | `target` tarihi, onaylayan, ikinci kişi | Kule | **Açık: TBD** |
| AK-6 | Yanlış kilitlenen (hiç kayıt kodu almamış) şirket için denetimli kilit açma yolu — v1'de yok (sahip kararı "kalıcı"); istenirse seçenek (b)'deki Platform ucunu gerektirir | Sahip | Açık; v1'de yok |
| AK-7 | Silinmiş, hiç kilitlenmemiş taslağın kodu bugün tekil dizinde kalıcı tutuluyor (kısmi olmayan dizin); bu paket yalnız 500'ü 409 yapar, serbest bırakmaz | Kule | Açık; davranış bugünkü gibi |
| AK-8 | MOD-0220 A1b (yazıcı çiti) ile eşgüdüm: aynı işleyiciler; hangisi önce girerse öbürünün testleri yeşil kalmalı | Kule | Açık |
