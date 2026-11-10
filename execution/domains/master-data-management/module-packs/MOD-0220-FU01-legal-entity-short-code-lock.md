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
form_field_count: 33
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
> **Künye notları:** `target` ve onaylayan TBD (AK-5). `golden_reference: compact` ev sahibi formun alan sayısındandır (`form_field_count: 33` = `frontend/Diten.Web/Views/MasterData/LegalEntities/Wizard.cshtml` içindeki 33 `input` / `select` / `textarea` öğesi, iki adres `textarea`'sı dahil; 2026-11-10 sayıldı); bu paket forma alan eklemez. `data_mode: client` mevcut listenin modudur (`frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/index.js:558` `serverSide: false`); liste bu pakette değişmez. `branch` kod dalı önerisidir; paket belgesi `chore/ct/mdm-ct-02` dalında.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-10, HEAD `3d80be94e`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0220-FU01 --name "Legal Entity Short Code Permanent Lock" --parent MOD-0220`
> → `OK  MOD-0220-FU01: proven against Blueprint/registry.` (çıkış 0). Kayıt satırı `execution/registries/module-id-registry.md:186` (reserved). `git log --all -S 'MOD-0220-FU01'`: yalnız kayıt satırı commit'i `3d80be94e`.
>
> **Kaynak okuma notu:** `origin/main:` önekli yollar bu dalda yok, `origin/main` (`084971b1e`) üzerinde okundu (`git show origin/main:<yol>`). MDM tüzel kişi kodu bu dalda `origin/main` ile aynı (`git diff --stat HEAD origin/main` ilgili dosyalarda boş, 2026-11-10). Bütün kod iddiaları kod okumasıdır; derlenmedi, çalıştırılmadı.
>
> **Değişen bölümler (inceleme turu 1, DÜZELT; önceki sha256 `8afcb483…`):** AK-1 kapandı (sahip kararı, §0.4, §25); F-1 askı / arşiv yazımları ve tüzel kişi belgesine yazan yolların listesi (§0.3, §3, §5, §12, §13, §17 AC-17, §18, §21 N-7, N-10); F-2 kilit karşılaştırması rezervasyon koduna, tutarlılık ölçümü ve kapısı, geri doldurmada çakışma (Ö-3, Ö-4, §12, §13, §17 AC-7 / AC-18, §23); F-3 M3 ve yeniden uzlaştırma, tek kod yapısının gerekçesi, yanlış kilit çaresi (Ö-2, §4, §23, §25 AK-6); F-4 P'den onay terimi çıktı (Ö-1, AC-11, N-3, F-2); F-5 sabotajlar (2), (4), (5), (6); F-6 AC-2, AC-11; F-7 etkinleştirme ve oluşturma `IAuditDetailsProvider` (§3, §15, sabotaj 12); F-8 depo kurucusu, `DependencyInjection.cs` (§21 N-2, N-7); F-9 işlem içi çarpma ve tek `IndexOnce` geri çağrısı (§21 N-2); F-10 alan sayısı 33, Platform onay kodları, olay iddiası (künye, §0.3); F-11 anahtar yeniden başlatma ister ve ara dönem kilidi kalkınca kapatılamaz (Ö-5, §23).
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
- Etkinleştirme, askıya alma, arşiv bütün belgeyi sürüm denetimsiz değiştirir (`RepositoryBase.cs:40-52`, `ReplaceOneAsync`; işleyiciler `ActivateLegalEntityHandler.cs:19-38`, `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs:19-34`, `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs:19-34`): okuma ile yazım arasında kodu değiştiren bir güncellemeyi sessizce eski koda geri yazabilir — kilitli kodu ezebilir.
- **Tüzel kişi belgesine yazan bütün yollar (ölçüm, 2026-11-10):** `git grep mdm_legal_entities -- services` → yalnız `LegalEntityRepository` (`:14`) ve `LegalEntityOperationalStatusMigration.cs:21`; `ILegalEntityRepository` yazma yöntemlerini (`CreateAsync` / `UpdateAsync` / `DeleteAsync` / `UpdateEditableFieldsAsync`) çağıran yalnız `Features/LegalEntity/` işleyicileri (ürün kapsamı işleyicileri yalnız okur: `CreateProductLegalEntityScopePolicyHandler.cs`, `ReplaceProductLegalEntityScopePolicyHandler.cs`, `GetProductLegalEntityScopeCreateOptionsHandler.cs`, `ProductLegalEntityScopeCandidateFacade.cs`). Tüzel kişi tohumu yok. Başka servisler yalnız HTTP ile okur (`services/Diten.CrmService/src/Diten.CrmService.Infrastructure/CyclePeriod/MdmCyclePeriodLegalEntityValidator.cs`, `services/Diten.HcmService/src/Diten.HcmService.Infrastructure/ReferenceValidation/GatewayReferenceValidationClient.cs`, `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Infrastructure/LegalEntities/MdmLegalEntityHierarchyClient.cs`, Platform okuyucusu). Kural ve dışlamalar §21 N-10.
- MDM olay yayımlamaz ve kayıt kodu servisini çağırmaz (`git grep -E "IPublishEndpoint|IEventPublisher|PublishAsync\(" -- services/Diten.MdmService/src`: 0; `git grep -n "RecordCode\|record-code" -- services/Diten.MdmService/src`: yalnız ürün dış başvuru rezervasyonunun `RecordCode` alanı). MDM'de `IOptionsMonitor` kullanımı yok (0 eşleşme); seçenekler açılışta bağlanır.
- `ApprovalStatus` yalnız oluşturmada yazılır (`LegalEntityModels.cs:150`), güncellemede yazılmaz (`:158-160`); "operator-set metadata only" (`services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LegalEntityApprovalStatus.cs:4`). Onay akışı yok.
- lookup-validation: `Active` değilse 404 (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-25`); yanıtta `approvalStatus` yok (`LegalEntityModels.cs:100-106`, `:238-245`).
- `LegalEntity` sınıf haritası **bilerek katı**: bilinmeyen alan taşıyan belge okunamaz (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs:67-68`, `:73-76`). Tüzel kişi belgesine yeni alan eklemek eski sürüme dönüşte okumaları kırar.
- Denetim: tüzel kişi komutları `IAuditableCommand` + `IAuditMetadataProvider` (ör. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/UpdateLegalEntityCommand.cs`); `AuditForwardingBehavior` reddi, komut `IAuditDetailsProvider`'a katılmışsa kod biçiminde kaydeder (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs:37-66`).
- İşlem ve dizin deseni: `MasterDataAtomicWrite` (snapshot / majority, geçici hatada yeniden koşu, sonucu bilinmeyen kesinleştirmede `Ambiguous`, işlemsiz sunucuda Development dışında `Unavailable`) ve `IndexOnce` (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs:31-99`, `:288`); seçenek `BrandProductStoreOptions` (`services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:467`).
- İzinler: güncelleme ve etkinleştirme `mdm.legal-entities.update` (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:64-74`), okuma `mdm.legal-entities.read` (`:24-50`).

**Platform (`origin/main`, bu paket değiştirmez):**

- Ayırma, şirket kısa kodunu MDM lookup-validation'dan okur (`origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/MdmLegalEntityShortCodeReader.cs:13,52`); `referenceable: true` ve `lifecycleState: "ACTIVE"` değilse kod vermez (`:115-121`); `approvalStatus` yalnız yanıtta varsa değerlendirilir: `DRAFT` / `SUBMITTED` / `REJECTED` → 422 `LEGAL_ENTITY_NOT_ELIGIBLE`, boş ya da tanımsız değer → 503, `APPROVED` → devam (`:23`, `:125-136`); kod kalıba uymuyorsa 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` (`:142`).
- Ara dönem kilidi: şirketin son öneki şimdikinden farklıysa ya da önek başka şirketin satırlarında varsa 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` (`origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs:176-186`; sorgular `origin/main:services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RecordCodeRepositories.cs:164-192`; kod sabitleri `origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeReasonCodes.cs:20-22`). Testleri `origin/main:services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/AllocateRecordCodeHandlerTests.cs:199-217` ve `origin/main:services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeAllocationMongoTests.cs:482-507`.
- Ayırma işleyicisi başarılı ayırmada denetim kutusuna `platform.record-code.allocated` niyeti yazar (`origin/main:services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeAuditIntent.cs:21-22`, `:30-40`); işleyicide olay yayımlama satırı görülmedi (kod okuması). Platform'un genel olay yolunda kayıt kodu olayı olup olmadığı **ölçülmedi**; seçenek (a) bu yüzden değil, aşağıdaki pencere gerekçesiyle elendi.
- Platform'un iç kayıt kodu ucu yalnız ayırmadır (`origin/main:services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalRecordCodesController.cs:18,30`); "şirketin kodu kullanıldı mı" sorusunu yanıtlayan uç yok.

### 0.4 Tasarım sorusu 1 — MDM "ilk kod verildi"yi nereden bilir? (ölçüm ve öneri)

| Seçenek | Ölçüm | Değerlendirme |
|---|---|---|
| (a) Platform olayı | Ayırma işleyicisinde yalnız denetim niyeti görüldü; genel olay yolu ölçülmedi (§0.3); MDM olay tüketmiyor | Olay ister var ister yeni olsun, MDM tüketicisi gerekir ve olay gecikmesi süresince kod değişebilir (pencere); yeni olay Platform değişikliğidir (XMC-001). **Elendi** |
| (b) MDM'nin Platform'a sorması | Böyle bir iç uç yok (§0.3) | Yeni Platform ucu (XMC-001) + her kod değişikliğinde MDM → Platform eşzamanlı bağımlılık (Platform kapalıyken kod düzeltilemez). Sahibin sözüne birebir uyardı. **Kapandı** (AK-1, sahip Ö-1'i seçti) |
| (c-onay) İlk `APPROVED` | `ApprovalStatus` yalnız oluşturmada yazılan operatör verisi, akış yok; lookup-validation 1.0.0'da alan yok, Platform bugün onayı denetlemiyor (§0.3; sözleşme `:185-192`) | `LEGAL_ENTITY_NOT_ELIGIBLE` bugün hiç tetiklenmez: onaysız ama `Active` şirkete kod verilebilir. Onaya bağlı kilit pencere bırakır. **Elendi** |
| (c-verilebilirlik) **İlk "kod verilebilir" an** | Platform yalnız `Active` + `referenceable` + kalıba uyan koda kod verir (§0.3) | **Öneri (Ö-1)** |

**Ö-1 (sahip kararıyla kesinleşti, AK-1 kapandı).** MDM kilidi, şirkete **kayıt kodu verilebilir olduğu ilk anda** koyar. Kod verilebilirlik koşulu P(e): `OperationalStatus` ∈ {`Active`, `Suspended`, `Archived`} **ve** `Code` `^[A-Z]{2,5}\z`'e (ASCII, sıralı karşılaştırma; Platform `RecordCodeFormat.cs:16` ile aynı) uyar. **Onay terimi yoktur** (inceleme turu 1, F-4, kule kabulü): MOD-0220 §22 lookup-validation'a `approvalStatus` eklese de P değişmez; onaysız etkin şirket de kilitlenir (daha katı yön). `Suspended` / `Archived` P'dedir çünkü bu durumlara yalnız `Active`'ten geçilir (§0.3); kilit askıya alma ya da arşivle kalkmaz. P'yi sağlatan her yazım (etkinleştirme; P'yi sağlayan şirkette kodu kalıba uyduran güncelleme) kodu **aynı Mongo işleminde** kalıcı rezervasyona yazar.

**Gerekçe:** P, Platform'un kod verebilmesinin zorunlu koşuludur; kilit ilk koddan **önce ya da aynı anda** doğar, pencere kalmaz; başka modüle değişiklik gerekmez; MDM kendi verisinden karar verir.

**Sahip kararından sapma (açıkça):** sahip "ilk kayıt kodu verildikten sonra" dedi. Ö-1 daha erken kilitler: etkinleştirilmiş, kodu kalıba uyan ama **hiç kayıt kodu almamış** şirketin kısa kodu da değişmez. Ayrıca askıdaki şirkette kalıba uymayan kodu kalıba uyduran güncelleme, şirket yeniden etkinleşmeden kilitlenir. Kalıba uymayan kod (ör. `Acme-1`) kilitlenmez; Platform ona zaten kod vermez (409 `LEGAL_ENTITY_SHORT_CODE_MISSING`), düzeltilebilir kalır. **Sahip bu sapmayı kabul etti:** 2026-11-10, kendi tıklamasıyla "Kod alabilir olunca (Önerilen)" — kayıt `docs/records/decisions/2026-11/mdm-0220-fu01-lock-moment-owner-decision-01.md`. Platform değişikliği yok; seçenek (b) kapandı.

### 0.5 Diğer öneriler

| # | Öneri | Pakete etkisi |
|---|---|---|
| Ö-2 | **Rezervasyon tek gerçek kaynaktır:** yeni koleksiyon `mdm_legal_entity_short_code_reservations`, kiracı başına kod başına tek satır, şirket başına tek satır, hiç silinmez. Tüzel kişi belgesine alan **eklenmez** (sınıf haritası katı; eski sürüme dönüşte okumalar kırılmaz, §0.3). **Şirket başına tek kod gerekçesi (F-3):** ara dönem kilidi bir şirkete yalnız son önekiyle kod verir ve başka şirketin önekini reddeder (`AllocateRecordCodeHandler.cs:176-186`), bu yüzden Platform 1.0.0 işleyicisi altında bir şirketin defterde birden çok öneki ya da bir önekin birden çok şirketi olamaz (kod okuması). Olursa (M3, §23) anahtar açılmaz; çözüm sahip kararıyla elle veri düzeltmesi + kayıt, v1'de çok önekli rezervasyon yok | §3, §4, §23 |
| Ö-3 | **Kilitli sayılma ve karşılaştırma (F-2):** şirket için rezervasyon varsa kilitli koddur **rezervasyondaki kod**; güncellemedeki yeni kod onunla karşılaştırılır (belgedeki kodla değil). Rezervasyon yoksa ve P(e) doğruysa kilitli kod belgedeki koddur. İkinci yarı, geri doldurma bitmeden açılan pencereyi kapatır | §12 |
| Ö-4 | **Geri doldurma:** açılışta, dizin hazır olunca, kiracı başına; P'yi sağlayan ve rezervasyonu olmayan her şirket (yumuşak silinmişler dahil) için sistem aktörlü `LockLegalEntityShortCodeCommand` MediatR'dan gönderilir; tekrarlanabilir. **Kodu başka şirkette rezerve olan şirket atlanır** ve rapora yazılır (günlük satırı + sayaç: kiracı, şirket, kod, sahip şirket); otomatik çözme yok, sahip kararı ister. Her koşuda **tutarlılık ölçümü**: rezervasyonu olan her şirkette rezervasyon kodu = belge kodu mu; uyuşmazlık sayısı günlüğe ve sayaca yazılır (§23 kapısı) | §3, §13, §15, §23 |
| Ö-5 | **Tek anahtar:** `LegalEntityShortCodeLock:Enabled` (temel yapılandırmada `false`). Kapalıyken hiçbir davranış değişmez; açılması §23'teki uzlaştırma kapısına bağlı (AK-3). Seçenek açılışta bağlanır (MDM'de `IOptionsMonitor` yok, §0.3): **değiştirmek yeniden başlatma ister**. Kapatmak yeniden dağıtımsız (yalnız yeniden başlatma) geri dönüştür; rezervasyonlar kalır. **Platform ara dönem kilidi kaldırıldıktan sonra anahtarın kapatılması yasaktır** (tek koruma budur) | §8, §23 |
| Ö-6 | **Durum geçişleri sürüm ve kod koşullu yazılır (F-1):** etkinleştirme, askıya alma ve arşiv bütün belgeyi değiştirmez; yalnız `OperationalStatus`, `UpdatedAt` ve `Version + 1`'i `$set` / `$inc` ile, filtrede okunan sürüm ve okunan kodla yazar; kaybeden 409 (bugünkü sessiz ezme kalkar). İstek biçimi değişmez (gövdesiz PATCH; MOD-0220 §21.5 madde 1'e dokunulmaz) | §12, §13 |
| Ö-7 | Hata adları Platform sözleşmesinin dilinde: 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`, 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`, 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, 503 `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN` | §12 (AK-2) |

## 1. Module Summary

Bir şirketin (MDM tüzel kişi) kısa kodunu, şirkete kayıt kodu verilebilir olduğu ilk anda kalıcı olarak kilitler ve kiracıda o şirkete rezerve eder: kilitli kod değiştirilemez, şirket silinse bile başka şirkete verilemez. Ekran kilitli kodu salt-okunur ve rozetle gösterir. Kullanıcı: MDM tüzel kişi sorumlusu; dolaylı tüketici: Platform kayıt kodu servisi ve onu kullanan modüller.

## 2. Ownership and Boundaries

**Kapsamda:** kod verilebilirlik koşulu (Ö-1), rezervasyon deposu, etkinleştirme ve güncellemede kilit ve rezervasyon yazımı, oluşturma / güncellemede rezerve kodun reddi, geri doldurma, anahtar, denetim, ayrıntı yanıtına kilit alanları, sihirbaz ve ayrıntı ekranında kilit gösterimi, 7 dil.

**Kapsam dışı:** Platform ara dönem kilidinin kaldırılması (Platform işi, MOD-0040-FU02, sahip Beste) · Platform'a yeni uç ya da olay (gerekmez; AK-1 sahip kararıyla kapandı) · MDM'de `^[A-Z]{2,5}$` kalıbının zorunlu kılınması (AK-4) · yanlış kilitlenen kodun açılması (yazılım yolu yok; AK-6) · silinmiş kilitsiz taslağın kodunu serbest bırakmak (AK-7) · tüzel kişi yazıcılarının kapsam çitine alınması (MOD-0220 A1b) · lookup-validation sözleşmesine alan eklemek · liste ekranı.

## 3. Owned Objects

| Tür | Ad | |
|---|---|---|
| Entity | `LegalEntityShortCodeReservation` (`mdm_legal_entity_short_code_reservations`) | yeni |
| Kural | `LegalEntityShortCodeRule` (kalıp + P(e), tek yer) | yeni |
| Komut | `LockLegalEntityShortCodeCommand` (sistem aktörü, geri doldurma) | yeni |
| Komut | `ActivateLegalEntityCommand` | değişir: sürüm + kod koşullu durum yazımı + P doğrulanınca aynı işlemde rezervasyon; `IAuditDetailsProvider` |
| Komut | `SuspendLegalEntityCommand` · `ArchiveLegalEntityCommand` | değişir: bütün belge değiştirme yerine sürüm + kod koşullu durum yazımı (F-1); kaybeden 409 |
| Komut | `UpdateLegalEntityCommand` | değişir: kilit ve rezervasyon denetimi; kodu P'ye sokan yazımda aynı işlemde rezervasyon; `IAuditDetailsProvider` |
| Komut | `CreateLegalEntityCommand` | değişir: rezerve kod 409; tekil dizin çarpması 500 değil 409; `IAuditDetailsProvider` |
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

Dizinler: tekil `ux_mdm_le_short_code_reservations_tenant_code` (TenantId, Code); tekil `ux_mdm_le_short_code_reservations_tenant_le` (TenantId, LegalEntityId) — şirket başına tek kilitli kod (gerekçe Ö-2; M3 çıkarsa anahtar açılmaz). Kısmi değil; `IsDeleted` hep `false`; serbest bırakma yöntemi yazılmaz. İki dizin **tek** `IndexOnce.Ensure` geri çağrısında birlikte kurulur (`CreateMany`); biri kurulamazsa ikisi de hazır sayılmaz.

Tüzel kişi belgesi (`LegalEntity`) değişmez (Ö-2).

## 5. Repo Scope

Tam liste §21 N-7. Özet: MDM Domain / Persistence / Application / Api yeni dosyaları; değişenler tüzel kişi komut işleyicileri (askıya alma ve arşiv dahil), `LegalEntityModels.cs`, `LegalEntityRepository.cs`, `ILegalEntityRepository.cs`, `MdmDocumentClassMaps.cs`, `Persistence/DependencyInjection.cs`, `Program.cs`, `appsettings*.json`; testler (§18); DL-2 ön yüzü (§11).

## 6. Protected Paths

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `gateway/**` (`ocelot.json` yalnız integration-agent) · `execution/registries/**` · `docs/analysis/contracts/record-code-allocate.md` (donmuş 1.0.0) · `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` · MOD-0040-FU02 paketi. Birine dokunmak gerekirse XMC-001. Bu pakette Platform'a hiçbir değişiklik gerekmez (§7).

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| MOD-0220 tüzel kişi kodu | Kayıt, yaşam döngüsü, lookup-validation | kodda var (§0.3) |
| MOD-0220 §22 (TRM-14, `approvalStatus`) | Yok: P onay terimi içermez (F-4); §22 lookup yanıtına alan ekler, bu paket o yanıtı değiştirmez | onay bekliyor; bağımlılık değil (§21 N-3) |
| MOD-0220 A1b (yazıcı çiti) | Aynı işleyicilere dokunur | BLOCKED, yalnız plan; eşgüdüm AK-8 |
| Platform ara dönem kilidi | Bu paket canlıya çıkana ve uzlaştırma bitene kadar tek koruma | kodda var, değişmez |
| Platform defteri uzlaştırma raporu | Anahtar açılmadan önce ve açıldıktan + geri doldurmadan sonra, salt-okunur sorgu Platform sahibince | **AK-3**; kod değişikliği değil |
| Denetim | `mdm-merkezi-iletim`, yol b (`tests/architecture/audit-ledger/Diten.MdmService.md`) | var |
| Gateway | `/api/legal-entities` mevcut | değişiklik yok |

**Başka modül değişikliği:** yok. AK-1 sahip kararıyla kapandı (Ö-1); Platform'a uç ya da olay eklenmez, Repo Scope'a Platform dosyası girmez. Ara dönem kilidinin kaldırılması Platform'un kendi işidir (F-1).

## 8. Runtime Constraints

MDM 5059, Gateway 5000, Web 5001. Her sorgu ve uç `TenantId` ile; başka kiracının şirketi 404; rezervasyon tekilliği yalnız kiracı içinde (başka kiracı aynı kodu kullanabilir, denetlenmez, sızdırılmaz). Tüzel kişi kiracı geneli kayıttır; tüzel kişi kapsam çiti bu kayda uygulanmaz (MOD-0220 A1b kapsamı). Silme yalnız yumuşak; rezervasyon silinmez. **İşlem:** rezervasyon yazan her yol (etkinleştirme, kodu P'ye sokan güncelleme, geri doldurma) `MasterDataAtomicWrite` ile tek işlem; işlemsiz sunucuda Development dışında 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`; Development'ın sıralı yazımında önce rezervasyon, sonra şirket (yarıda kalırsa fazladan kilit kalır, eksik kilit kalmaz). Sonucu bilinmeyen kesinleştirme 503 `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN`. **Dizin hazırlığı:** iki tekil dizin `IndexOnce` ile; hazır değilse rezervasyon gerektiren yazım 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, geri doldurma o açılışta atlar ve sonraki açılışta dener. Rezervasyon gerektirmeyen yazımlar (kilitli şirkette kod aynı kalan güncelleme, kalıba uymayan kodla etkinleştirme) bugünkü yoldan gider. **Anahtar:** `LegalEntityShortCodeLock:Enabled = false` iken bu paketin hiçbir denetimi ve yazımı çalışmaz, ayrıntı yanıtı `shortCodeLocked: false` döner; durum geçişlerinin sürüm + kod koşullu yazımı (Ö-6) anahtardan bağımsızdır (yeni davranış değil, ezme hatasının düzeltilmesi). Anahtar açılışta okunur; değiştirmek yeniden başlatma ister. Platform ara dönem kilidi kaldırıldıktan sonra kapatılamaz (Ö-5).

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
| Kilitli şirkette kod değişikliği | Güncelleme: kilitli (Ö-3) **ve** `Trim` sonrası yeni kod ≠ kilitli kod — rezervasyon varsa **rezervasyondaki kod**, yoksa belgedeki kod (sıralı; `abc` ≠ `ABC`) | 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED` |
| Rezerve kod | Oluşturma / güncelleme: yeni kod başka şirketin rezervasyonunda (sahibi silinmiş olsa da) | 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` |
| Kilitli şirkette diğer alanlar | Kod aynıysa güncelleme bugünkü gibi | — |
| Kodu P'ye sokan güncelleme | Yalnız `Active` / `Suspended` / `Archived` şirkette: P güncellemeden önce yanlış, sonra doğru (kalıba uymayan kod kalıba uyduruldu). Taslakta rezervasyon **yazılmaz** | Yazım + rezervasyon tek işlem; rezervasyon çakışırsa 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`, hiçbir şey yazılmaz |
| Etkinleştirme | Okunan sürüm + kodla koşullu durum yazımı (Ö-6); P doğruysa aynı işlemde rezervasyon (`Source = Activation`) | Kaybeden 409 (mevcut metin "The Legal Entity has changed since it was loaded."); rezervasyon çakışırsa 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` |
| Askıya alma, arşiv | Okunan sürüm + kodla koşullu durum yazımı (Ö-6); rezervasyon yazmaz (kilit zaten etkinleştirmede doğdu) | Kaybeden 409 (aynı metin); kod hiçbir durumda geri yazılmaz |
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
| Etkinleştirme okuduktan sonra kodu **değiştirmeyen** bir güncelleme kesinleşti | Etkinleştirme 409 (sürüm koşulu); belge durumu değişmez |
| Askıya alma / arşiv okuduktan sonra kod değiştiren güncelleme kesinleşti | Askıya alma / arşiv 409; yeni kod korunur (bugün eski koda geri yazılırdı) |
| Aynı kodu iki şirkete eşzamanlı rezervasyon | Tekil dizin; tam biri başarılı |
| Rezervasyon yazımı işlem içinde başarısız | Şirket de değişmez (iki koleksiyon) |
| Dizin hazır değil | 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, iki koleksiyon değişmez; dizin hazır olunca aynı istek başarılı |
| Geri doldurma ikinci koşu | Değişiklik yok; açılış durmaz |
| Geri doldurmada şirketin kodu başka şirketin rezervasyonunda | Şirket atlanır, raporda (kiracı, şirket, kod, sahip şirket); otomatik çözme yok |
| Rezervasyon kodu ≠ belge kodu (veri bozulması ya da geri dönüş penceresi) | Geri doldurma ölçer ve sayar; kilit karşılaştırması rezervasyon koduna göre (Ö-3); §23 kapısı sıfır ister |
| İşlem içinde rezervasyon tekil dizine çarptı | İşlem iptal edilir, işlem dışında sahip okunur: aynı şirketse başarı (eşzamanlı ikinci kilit), başka şirketse 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED` (§21 N-2) |
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

Tek yol: b, iz `mdm-merkezi-iletim`. İstisna sınıfı kullanılmaz. Olay adı `AuditDetails` meta verisine `EventName` olarak yazılır. Ret kodu yalnız `IAuditDetailsProvider`'a katılan komutta kayda girer (`AuditForwardingBehavior.cs:50-52`); bu yüzden etkinleştirme, oluşturma ve güncelleme katılır (F-7).

| Komut | Olay | Nesne | Yol | Önce / sonra | İstisna |
|---|---|---|---|---|---|
| `ActivateLegalEntityCommand` (değişir, `IAuditDetailsProvider`'a katılır) | mevcut etkinleştirme; kilit doğduysa ek `legal-entity.short-code.locked` meta verisi (kod, kaynak `Activation`); ret `LEGAL_ENTITY_SHORT_CODE_RESERVED` / `_STORE_UNAVAILABLE` kod biçiminde | LegalEntity | b | Önce / sonra: durum; kilitlenen kod | — |
| `SuspendLegalEntityCommand`, `ArchiveLegalEntityCommand` (değişir: yalnız yazım yolu) | mevcut kayıtlar, değişmez | LegalEntity | b | mevcut | — |
| `UpdateLegalEntityCommand` (değişir, `IAuditDetailsProvider`'a katılır) | mevcut güncelleme; ret `LEGAL_ENTITY_SHORT_CODE_LOCKED` / `_RESERVED` kod biçiminde; kilit doğduysa `legal-entity.short-code.locked` (kaynak `Update`) | LegalEntity | b | Önce / sonra: kod (ret kaydında istenen kod meta veride) | — |
| `CreateLegalEntityCommand` (değişir, `IAuditDetailsProvider`'a katılır) | mevcut oluşturma; ret `LEGAL_ENTITY_SHORT_CODE_RESERVED` kod biçiminde | LegalEntity | b | Sonra: kod | — |
| `LockLegalEntityShortCodeCommand` (yeni) | `legal-entity.short-code.locked`, şirket başına | LegalEntity | b, MediatR hattından, **sistem aktörü** | Sonra: kod, kaynak `Backfill` | — |

Denetim defteri dosyası (`tests/architecture/audit-ledger/Diten.MdmService.md`) değişmez: yeni komut mevcut işaretle kapsanır; `AuditTrailStandardTests` onu öbür komutlar gibi görür.

## 16. Gateway / API Routing Decision

Yeni uç yok. Mevcut `/api/legal-entities` rotaları ve MVC vekili (`frontend/Diten.Web/Controllers/LegalEntitiesController.cs:62-99`) yeterli; Gateway satırı gerekmez. Ayrıntı yanıtına iki alan eklenir (`shortCodeLocked`, `shortCodeLockedAt`; ekleme). lookup-validation yanıtı **değişmez** (Platform okuyucusu etkilenmez).

## 17. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-1: etkinleştirilmiş ve kodu `^[A-Z]{2,5}\z`'e uyan şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`; askıya alınmış ve arşivlenmiş durumda da 409 — hem rezervasyonlu hem **rezervasyonsuz** (yalnız P ile kilitli) satırlarda; aynı kodla diğer alanların güncellemesi 204.
- [ ] AC-2 (zorunlu) — DL-1: taslak şirkette ve kodu kalıba uymayan etkin şirkette kod değişikliği bugünkü gibi 204; taslakta kodu kalıba uyduran güncelleme rezervasyon **yazmaz**; `Active` / `Suspended` / `Archived` şirkette kalıba uymayan kodu kalıba uyduran güncelleme 204 döner ve aynı işlemde `Source = Update` rezervasyonu yazar.
- [ ] AC-3 (zorunlu) — DL-1: etkinleştirme, kod kalıba uyuyorsa aynı Mongo işleminde `Source = Activation` rezervasyonu yazar; işlem içi hata kancasıyla iki koleksiyondan hiçbiri değişmez.
- [ ] AC-4 (zorunlu) — DL-1: kilitli şirket yumuşak silindikten sonra aynı kodla oluşturma ve başka şirketin aynı koda güncellenmesi 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`; rezervasyon satırı silinmez.
- [ ] AC-5 (zorunlu) — DL-1: ikinci kiracıda aynı kodlu şirketin etkinleştirilmesi başarılı ve kendi kiracısında rezervasyon yazar; başka kiracının şirketi için güncelleme ve etkinleştirme 404.
- [ ] AC-6 (zorunlu) — DL-1: eşzamanlı etkinleştirme + kod değiştiren güncellemede tam biri başarılı ve rezervasyondaki kod belgedeki kodla aynı; etkinleştirme okuduktan sonra kodu değiştirmeyen bir güncelleme kesinleşirse etkinleştirme 409 ve belge durumu değişmez; aynı koda eşzamanlı iki rezervasyonda tam biri başarılı.
- [ ] AC-7 (zorunlu) — DL-1: geri doldurma P'yi sağlayan her şirkete (yumuşak silinmiş dahil) tek `Source = Backfill` rezervasyonu yazar, P'yi sağlamayanlara yazmaz; kodu başka şirkette rezerve olan şirketi atlar ve raporlar, öbür şirketlere devam eder; her koşuda rezervasyon kodu ≠ belge kodu sayısını günlüğe ve sayaca yazar; ikinci koşu değişiklik üretmez; açılış durmaz; komut MediatR'dan sistem aktörüyle gider.
- [ ] AC-8 (zorunlu) — DL-1: geri doldurma koşmadan, anahtar açıkken P'yi sağlayan şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED` (Ö-3).
- [ ] AC-9 (zorunlu) — DL-1: rezervasyon dizinleri hazır değilken rezervasyon gerektiren etkinleştirme ve güncelleme 503 `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE` döner, iki koleksiyon değişmez; dizin hazır olunca aynı istek başarılı.
- [ ] AC-10 (zorunlu) — DL-1: anahtar kapalıyken mevcut tüzel kişi testlerinin hepsi değişmeden yeşil ve hiçbir rezervasyon yazılmaz.
- [ ] AC-11 (zorunlu) — DL-1: yumuşak silinmemiş her birleşim (6 işletme durumu × kod kalıba uyar / uymaz × 4 onay durumu) için: `Active` satırlarında "kilitlenir" sonucu, MDM'nin gerçek `ValidateLegalEntityReferenceHandler` yanıtının 200 + `referenceable: true` + `lifecycleState: ACTIVE` + kod kalıba uyar koşuluyla aynıdır ve onay durumundan bağımsızdır; `Suspended` / `Archived` satırlarında kod kalıba uyuyorsa kilitlenir; `Draft`, `InReview`, `Approved` işletme durumlarında kilitlenmez. Yumuşak silinmiş şirketler bu karşılaştırmanın dışındadır (lookup 404); onların davranışı AC-4 ve AC-7'dedir.
- [ ] AC-12 (zorunlu) — DL-1: §15'teki her yazma ve ret tam bir yol b kaydı üretir; etkinleştirme, oluşturma ve güncelleme retleri kod biçiminde (`LEGAL_ENTITY_SHORT_CODE_LOCKED` / `_RESERVED` / `_STORE_UNAVAILABLE`); geri doldurma kaydı sistem aktörlü; oluşturmada silinmiş kilitsiz şirketin koduyla gelen istek 500 değil 409.
- [ ] AC-13 (zorunlu) — DL-2: düzenleme sihirbazında kilitli şirketin kod alanı salt-okunur ve `ShortCodeLockedHint` görünür; ayrıntıda `ShortCodeLockedBadge`; kodu kalıba uyan kilitsiz şirketin etkinleştirme onayı `ActivateConfirmShortCodeLock`; 409 / 503 kodları yerel metinle; `Layout = "_LayoutTenantShell"` korunur.
- [ ] AC-14 (zorunlu) — DL-2: 7 yeni anahtar 7 dilde; `LegalEntityL10nContractTests` anahtar eşitliği yeşil; satır içi CSS yok.
- [ ] AC-15 (zorunlu) — DL-1 ve DL-2: §18 sabotajlarının her biri adlı testte kırmızı, geri alınca yeşil; `git status --porcelain` boş.
- [ ] AC-16 (isteğe bağlı) — DL-2: sayfa yardımında kilit kuralı (§19).
- [ ] AC-17 (zorunlu) — DL-1: askıya alma ve arşiv yalnız durumu sürüm + kod koşullu yazar; okuduktan sonra kodu değiştiren bir güncelleme kesinleşirse askıya alma / arşiv 409 döner ve yeni kod korunur; tüzel kişi durum geçişlerinde bütün belge değiştirme (`RepositoryBase.UpdateAsync`) kullanılmaz.
- [ ] AC-18 (zorunlu) — DL-1: rezervasyon kodu belge kodundan farklıysa (test verisiyle kurulur) kilit karşılaştırması rezervasyon koduna göredir: belgedeki kodla güncelleme 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`, rezervasyon koduna dönüş 204.
- [ ] AC-19 (zorunlu) — DL-1: işlem içinde rezervasyon dizinine çarpma işlemi iptal eder ve hiçbir koleksiyonu değiştirmez; aynı şirkete eşzamanlı iki kilit (etkinleştirme + geri doldurma) tek satır ve iki başarı; başka şirketin koduna çarpma 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`.

## 18. Test Expectations

Üretim koduyla, gerçek Mongo (replica set, `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs`) ile; test çifti yalnız denetim iletimi için. Bellek içi depo (`InMemoryLegalEntityRepository`) yalnız mevcut komut testlerinin derlenmesi için genişler; kilit kabulü onunla ölçülmez.

| Test | Ölçer |
|---|---|
| `LegalEntityShortCodeRuleTests` (yeni) | Kalıp (ASCII, `\z`, büyük-küçük harf), P birleşimleri; AC-11'in tablo yarısı |
| `LegalEntityShortCodeLockMongoTests` (yeni) | AC-1…AC-4, AC-6, AC-8, AC-9, AC-10, AC-17, AC-18, AC-19 gerçek depo ve işlemle |
| `LegalEntityShortCodeLockHttpTests` (yeni, Api.Tests) | AC-1, AC-4, AC-5, AC-12 uçtan (kod, durum, kiracı 404) |
| `LegalEntityShortCodeBackfillTests` (yeni) | AC-7 |
| `LegalEntityShortCodeEligibilityContractTests` (yeni) | AC-11: gerçek `ValidateLegalEntityReferenceHandler` yanıtı ↔ kilit sonucu |
| `LegalEntityShortCodeAuditTests` (yeni) | AC-12 |
| `LegalEntityCommandTests`, `LegalEntityEditableCasMongoTests`, `LegalEntityUpdateContractTests`, `LegalEntityReferenceValidationTests` (mevcut) | AC-10: anahtar kapalıyken değişmeden yeşil |
| `MdmDocumentClassMapsTests` (mevcut) | Yeni tür kapsanan kümede, `LegalEntity` katı kalır |
| `LegalEntityL10nContractTests` (mevcut, DL-2) | AC-14 |

**Sabotajlar (her biri adlı testte kırmızı):** (1) güncelleme işleyicisinden kilit denetimini kaldır → AC-1; (2) P'den `Suspended` / `Archived`'i çıkar → AC-1 **rezervasyonsuz** askı / arşiv satırı; (3) etkinleştirmede rezervasyonu işlem dışına, ayrı yazıma taşı → AC-3 hata kancası; (4) `CreateLegalEntityHandler`'dan rezervasyon ön denetimini kaldır → AC-4 (beklenen `LEGAL_ENTITY_SHORT_CODE_RESERVED` yerine dizin çarpmasının mevcut metni döner); (5) rezervasyon kod dizininden `TenantId`'yi çıkar → AC-5 ikinci kiracıda etkinleştirme; (6) etkinleştirme filtresinden okunan sürümü çıkar → AC-6 kodu değiştirmeyen güncelleme sırası; (7) geri doldurma filtresinden yumuşak silinmişleri çıkar → AC-7; (8) Ö-3'ün ikinci yarısını (P doğruysa kilitli) kaldır → AC-8; (9) depoda dizin hazırlık denetimini kaldır → AC-9; (10) anahtar denetimini kaldır → AC-10; (11) kalıbı `^[A-Za-z]{2,5}` yap → AC-11 / kural testi; (12) etkinleştirme, oluşturma ya da güncelleme komutunu `IAuditDetailsProvider`'dan çıkar → AC-12'nin o komuta ait ret kaydı (üç ayrı sabotaj); (13) geri doldurmada rezervasyonu MediatR'sız doğrudan yaz → AC-12 (`AuditTrailStandardTests` / `LegalEntityShortCodeAuditTests`); (14) DL-2: düzenlemede `readonly` atamasını kaldır → AC-13 ekran testi; (15) askıya alma ya da arşivi bütün belge değiştirmeye (`RepositoryBase.UpdateAsync`) geri döndür → AC-17; (16) kilit karşılaştırmasını belge koduna çevir → AC-18; (17) işlem içi çarpmada işlem dışı sahip okumasını kaldır (çarpmayı hep `RESERVED` say) → AC-19 aynı şirket satırı; (18) geri doldurmada çakışan şirkette atlamak yerine hata fırlat → AC-7 (öbür şirketler rezerve edilmez).

## 19. Page Help (UXH-001)

DL-2, isteğe bağlı (AC-16): 7 dilde — kısa kodun kayıt kodlarının öneki olduğu, ne zaman kilitlendiği, kilitli kodun neden değişmediği ve silinen şirketin kodunun neden yeniden kullanılmadığı.

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (2026-11-10, `3d80be94e`)
- [x] Künye; `golden_reference: compact` (ev sahibi form 33 alan), `data_mode: client`, `data_mode_max_rows: 200`
- [x] Layout `_LayoutTenantShell` (§9)
- [x] Validation, Failure Path, Authorization, Gateway, Audited Events
- [x] Teslim dilimleri, kanıt seviyesi, plan sütunları
- [x] Tasarım sorusu 1 kodla ölçüldü, tek öneri (§0.4)
- [x] AK-1 sahip kararı: kilit anı = kod alabilir olunca (2026-11-10, `docs/records/decisions/2026-11/mdm-0220-fu01-lock-moment-owner-decision-01.md`)
- [x] İnceleme turu 1 (DÜZELT) bulguları F-1…F-11 işlendi
- [ ] AK-2 hata adları (kule)
- [ ] AK-3 Platform defteri uzlaştırma raporu — anahtar açılmadan önce ve açıldıktan sonra zorunlu (DL-1 kodunu bekletmez)
- [ ] AK-5 `target`, onaylayan, ikinci kişi — TBD
- [ ] AK-8 MOD-0220 A1b eşgüdümü
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01)

## 21. Implementation Notes

- **N-1 İki dilim.** DL-1 arka uç tek başına kilidi kurar; ekran DL-2'ye kadar ret kodunu ham gösterir (`wizard.js:334-336`). Bu yüzden anahtar DL-2 dağıtılmadan pilotta açılmaz (§23).
- **N-2 Kilit yazımı depoda.** `LegalEntityRepository` yeni yöntemler alır: sürüm + kod koşullu durum geçişi (etkinleştirme, askıya alma, arşiv; yalnız `OperationalStatus`, `UpdatedAt`, `Version + 1`), isteğe bağlı aynı işlemde rezervasyonla; kodu P'ye sokan güncellemede `UpdateEditableFieldsAsync`'in rezervasyonlu kardeşi. Rezervasyon sınıfı `ExternalReferenceReservations` desenindedir (`MasterDataAtomicWrite.cs:161-274`) ama **serbest bırakma yöntemi yoktur**. **İşlem içi çarpma (F-9):** rezervasyon eklemeden önce işlem içinde (TenantId, Code) ve (TenantId, LegalEntityId) ön okunur; buna rağmen ekleme `MasterDataAtomicWrite.IsDuplicateKey` (`:148-154`) ile çarparsa yazım `Written` dışı bir sonuç döner ve `RunAsync` işlemi iptal eder (`:71-75`; emsal `TryReserveAsync` `:219-222`). İptalden sonra sahip işlem **dışında** okunur (emsal `HeldBySelfAsync` `:230-254`): aynı şirket ve aynı kod → başarı (eşzamanlı ikinci kilit; durum yazımı yeniden denenir), başka şirket → 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`. İki tekil dizin tek `IndexOnce.Ensure` geri çağrısında `CreateMany` ile kurulur (`:288`). **Kurucu (F-8):** mevcut iki parametreli kurucu `LegalEntityRepository(IMongoDatabase, ITenantContext)` korunur (`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs:1099` değişmez; istemci `database.Client`, seçenek varsayılan `BrandProductStoreOptions`); DI için `IMongoClient` + `BrandProductStoreOptions` alan ikinci kurucu eklenir, kayıt `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs:65`'te güncellenir.
- **N-3 Onay terimi yok (§22, F-4).** P onay durumuna bakmaz. MOD-0220 §22 lookup yanıtına `approvalStatus` eklediğinde Platform onaysız şirkete 422 verir; MDM yine kilitler (daha katı, güvenli yön; kule kabulü). §22 bu paketin bağımlılığı değildir ve bu paket lookup yanıtını değiştirmez.
- **N-4 MOD-0220 A1b ile ilişki.** Bu paket tüzel kişi belgesine yeni alan, yeni yazıcı ya da açılışta tüzel kişi belgesine yazan göç eklemez (geri doldurma yalnız rezervasyon koleksiyonuna yazar); A1b §21.5 madde 3'teki "çit dışı otomatik yazıcı" sorununu büyütmez. Etkinleştirme, askıya alma ve arşiv yazımını değiştirir (Ö-6) ama istek biçimini değiştirmez (§21.5 madde 1). A1b sonradan açılırsa bu paketin testleri yeşil kalmalı (AK-8).
- **N-5 Geri doldurma.** Kiracı listesi `mdm_legal_entities`'teki ayrık `TenantId` değerleri (yumuşak silinmişler dahil). Koşucu kiracı başına `TenantContext.SetTenant` ile kapsam açar ve şirket başına `LockLegalEntityShortCodeCommand` gönderir (sistem aktörü + `IAuditTenantProvider`; emsal `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ItemMaster/Commands/ApplyItemActivationDecisionCommand.cs`). Komut özelliklerinde `LegalEntityId` ilk `Guid` özelliğidir (denetimin varlık kimliği, `AuditForwardingBehavior.cs:83-103`). Koşu `Enabled = false` iken hiçbir şey yapmaz. "Rezervasyon yok" filtresi eksik alanı da yakalamalı (Mongo yeni alan dersi: `Eq(false)` alanı olmayan eski belgeyi kaçırır; burada ayrı koleksiyon, `$lookup` yerine kiracı başına iki okuma).
- **N-6 Karşılaştırma.** Kilit denetimi, gelen kodun `Trim`'den sonraki hâlini **kilitli kodla** sıralı karşılaştırır: rezervasyon varsa rezervasyon kodu, yoksa `entity.Code` (Ö-3; `LegalEntityModels.cs:162` aynı `Trim`'i yapar). Rezervasyon kodu belge kodundan farklıysa (bozulma ya da geri dönüş penceresi) şirket rezervasyon koduna dönmeden düzenlenemez; geri doldurma uyuşmazlığı sayar (Ö-4).
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
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/GetLegalEntityByIdHandler.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
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
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityWriteRequestValidator.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/LegalEntityOperationalStatusMigration.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs` (iki parametreli depo kurucusu, `:1099`)
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
- **N-10 Tüzel kişi belgesine yazan yollar ve kural (F-1).** Ölçülen yollar §0.3. Kural: belgeye yazan her yol ya kodu hiç yazmaz ya da kilit denetiminden geçer, ve hiçbiri bütün belgeyi değiştirmez. (i) Oluşturma: ekleme, rezervasyon ön denetimi (§12). (ii) Güncelleme: sürüm CAS (`LegalEntityRepository.cs:40-47`) + kilit denetimi. (iii) Etkinleştirme, askıya alma, arşiv: sürüm + kod koşullu durum yazımı (Ö-6). (iv) Yumuşak silme (`RepositoryBase.cs:54-67`): yalnız `IsDeleted`, `DeletedAt`, `UpdatedAt`'i `$set` eder, kodu yazmaz; rezervasyon kalır — **gerekçeli dışlama**, değişmez. (v) `LegalEntityOperationalStatusMigration` (`LegalEntityOperationalStatusMigration.cs:19-39`): kiracısız açılış göçü, yalnız eski `LifecycleStatus` alanını taşıyan belgelerde `OperationalStatus`'u yazar, kodu yazmaz, tiplenmiş kullanımdan ve geri doldurmadan önce eşzamanlı koşar (`DependencyInjection.cs:52-60`); taşıdığı `Active` / `Archived` satırlar geri doldurmada P'ye girer — **gerekçeli dışlama**, değişmez (MOD-0220 §21.5 madde 3'ün konusu). (vi) Başka modül ve tohum: yazan yok (§0.3).
- Dilim tablosu `## 24. Teslim dilimleri` altında (`scripts/pack_approval.py` `SLICE_HEADING`).
- Kurallar: `module-pack-standard.md`, `handler-design.md`, `erp-architecture.md`, `response-envelope.md`, `multi-tenancy`, `localization-standard`, `audit-trail-standard.md`, RCS-001.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | Platform ara dönem kilidinin kaldırılıp kaldırılmayacağı (öneri: kalsın, ucuz ikinci koruma). Önkoşul: bu paket ilgili ortamda anahtar açık, geri doldurma bitti, AK-3 raporu sıfır | Platform (Beste) | 🟢 kaldırılmazsa yalnız çift denetim |
| F-2 | ~~§22 sonrası onay terimi~~ — kapandı (F-4: P onay terimi içermez) | — | — |
| F-3 | İleride onay geçişi (akış) eklenirse P'yi denetlemeli | MDM | 🟡 |
| F-4 | MOD-0220 ekranının Compact setine (`Create` / `Edit` / `Details` / `_Form`) dönüşü | MDM | 🟢 bilinen sapma |
| F-5 | RCS-001 `:53` satırının "uyumlu" olarak güncellenmesi (`.antigravity`, sahip onayı) | Sahip | 🟢 |
| F-6 | Sözleşme N-12'nin kapandı notu (yeni sürümde, ekleme) | Platform (Beste) | 🟢 |
| F-7 | `module-implementation-status.md` satırı, kod gelince | Kule | 🟢 |

## 23. Canlı sıra ve geri dönüş

**Kanıt.** DL-1 ve DL-2 kabulü E4: otomatik test + gerçek Mongo kalıcılığı + kiracı izolasyonu + denetim + hata yolları (yerel). Canlı kabul pilot kiracıda anahtar açıldıktan sonra.

**Sıra:**
1. MDM DL-1 + DL-2 dağıtımı, anahtar **kapalı** (davranış değişmez).
2. **Uzlaştırma kapısı (AK-3):** Platform sahibi, ortamın `record_code_allocations` koleksiyonunda `PrefixScope = LegalEntity` satırlarından (TenantId, LegalEntityId) başına son öneki ve önek kümesini, ayrıca (TenantId, önek) başına şirket kümesini salt-okunur çıkarır; MDM tarafı aynı şirketlerin şimdiki `Code`, `OperationalStatus`, `IsDeleted` değerlerini verir. Uyuşmazlık türleri: (M1) şirketin son öneki ≠ şimdiki kodu — bu şirket bugün ara dönem kilidiyle kod alamıyor; anahtar açılırsa yanlış kod **kalıcı** kilitlenir; önce kod önekine geri alınır. (M2) A'nın öneki B'nin şimdiki kodu — B önce yeniden adlandırılır (sahip kararı). (M3) bir şirkete birden çok önek **ya da** bir önek birden çok şirkette — Platform 1.0.0 işleyicisi altında olmamalı (Ö-2); v1 rezervasyonu şirket başına tek kod tuttuğu için eski önekleri rezerve edemez; çıkarsa anahtar açılmaz, sahip kararıyla elle veri düzeltmesi yapılır ve kayda geçer. Rapor üç türde de sıfır göstermeden anahtar açılmaz. Rapor kuleye verilir ve kayda girer.
3. Anahtar pilotta açılır (yapılandırma + yeniden başlatma, Ö-5); açılış geri doldurmayı koşar. **Kademeli dağıtım:** anahtar açıkken eski kopyalar kilit denetimsizdir; bütün eski kopyalar durduktan sonra yeni kopyalardan biri yeniden başlatılır ve geri doldurma yeniden koşar (tekrarlanabilir).
3a. **Tutarlılık kapısı (F-2):** geri doldurmanın günlüğünde ve sayacında rezervasyon kodu ≠ belge kodu sayısı **sıfır** ve atlanan (başka şirkette rezerve) şirket sayısı sıfır; değilse pilot denetimine geçilmez, sahip kararı alınır. Bu ölçüm her dağıtım, anahtar değişikliği ve geri dönüş adımından sonra yeniden okunur.
3b. **Yeniden uzlaştırma (F-3):** anahtar açıldıktan ve geri doldurma bittikten sonra 2. adımın raporu yeniden alınır; ek olarak her rezervasyonun kodu, şirketin defterdeki önekiyle (varsa) aynı olmalı. Sıfır uyuşmazlık kayda girer.
4. Pilot denetimi: kodu kalıba uyan etkin bir şirkette kod değişikliği 409 `LEGAL_ENTITY_SHORT_CODE_LOCKED`; silinmiş kilitli şirketin koduyla oluşturma 409 `LEGAL_ENTITY_SHORT_CODE_RESERVED`; Platform ayırması aynı şirkete kod vermeye devam eder.
5. Canlı ortamda aynı sıra (2–4).

**Geri dönüş.** Önce anahtar kapatılır (yeniden dağıtım yok, yeniden başlatma gerekir; kilit denetimi durur, rezervasyonlar kalır; Platform ara dönem kilidi koruma olarak sürer). **Platform ara dönem kilidi kaldırıldıktan sonra anahtar kapatılamaz** (Ö-5); o noktadan sonra geri dönüş yalnız ileri düzeltmedir. Eski MDM sürümüne dönüş güvenlidir: tüzel kişi belgesi değişmediği için katı sınıf haritası okumaları kırılmaz (Ö-2); eski sürüm rezervasyon koleksiyonunu görmez ve askı / arşivde bütün belge değiştirmeye döner. Yeniden ileri dönüşte 2., 3a. ve 3b. adımlar yeniden koşulur (anahtar kapalı geçen sürede kodu değişen şirket rezervasyon koduyla uyuşmazlık olarak görünür). **Yanlış kilit çaresi (AK-6):** v1'de yazılım yolu yoktur; tek çare sahip kararıyla elle veri düzeltmesi (rezervasyon satırının ve gerekirse belge kodunun düzeltilmesi, Platform defteriyle karşılaştırılarak) ve bunun karar kaydıdır.

**Platform bağlantıları (DL-2):** menü · Ctrl+K · 7 dil · yetkisiz yüz · sayfa yardımı · denetim mevcut ekranda; bu paket yalnız sihirbaz ve ayrıntı sayfasını değiştirir, satır başına durum DL-2 prompt'unda ölçülür.

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | kısa kod kilidi ve kalıcı rezervasyon: kod verilebilirlik kuralı, durum geçişlerinin sürüm ve kod koşullu yazımı, etkinleştirme ve güncellemede tek işlem, oluşturma / güncelleme reddi, geri doldurma ve tutarlılık ölçümü, anahtar, denetim (arka uç) | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events (AUD-001), Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Implementation Notes, Canlı sıra ve geri dönüş | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | sihirbaz ve ayrıntı ekranında kilit gösterimi, etkinleştirme onayı, hata metinleri ve 7 dil | Repo Scope, Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Acceptance Criteria, Test Expectations, Canlı sıra ve geri dönüş | E4 | plan kimliği yok | plan satırı yok |

DL-1 çekirdektir; DL-2 DL-1'e dayanır. Anahtar pilotta DL-2 dağıtılmadan açılmaz (§21 N-1).

## 25. Açık kararlar

| # | Karar | Kim | Durum |
|---|---|---|---|
| AK-1 | Kilit anı: Ö-1 (ilk "kod verilebilir" an), sahibin "ilk kayıt kodu verildikten sonra" sözünden daha erken kilitler | Sahip | **Kapandı — sahip 2026-11-10 kendi tıklamasıyla "Kod alabilir olunca (Önerilen)"; Platform değişikliği yok.** Kayıt: `docs/records/decisions/2026-11/mdm-0220-fu01-lock-moment-owner-decision-01.md` |
| AK-2 | Hata adları `LEGAL_ENTITY_SHORT_CODE_LOCKED`, `LEGAL_ENTITY_SHORT_CODE_RESERVED`, `LEGAL_ENTITY_SHORT_CODE_STORE_UNAVAILABLE`, `LEGAL_ENTITY_WRITE_OUTCOME_UNKNOWN` (Ö-7); Platform sözleşmesi değişmez, Beste'ye bilgi | Kule | Açık |
| AK-3 | Uzlaştırma raporu (§23 adım 2, M1–M3; anahtar açıldıktan ve geri doldurmadan sonra adım 3b'de yeniden): Platform defterinde salt-okunur sorgu Platform sahibince; başka modülün verisi olduğu için sahip izni ve Beste'nin kabulü gerekir | Sahip + Beste | **Açık — anahtar açılmadan önce zorunlu**; DL-1 kodunu bekletmez |
| AK-4 | MDM'de `^[A-Z]{2,5}$` kalıbını giriş kuralı yapmak (bugün 64 karaktere kadar serbest; mevcut kodları kırabilir) | Sahip | Açık; bu paketin dışında, öneri: ayrı takip |
| AK-5 | `target` tarihi, onaylayan, ikinci kişi | Kule | **Açık: TBD** |
| AK-6 | Yanlış kilitlenen (hiç kayıt kodu almamış) şirket için kilit açma — v1'de yazılım yolu yok (sahip kararı "kalıcı"); tek çare sahip kararıyla elle veri düzeltmesi + karar kaydı (§23). Denetimli bir açma yolu istenirse Platform'dan "kod verildi mi" bilgisi gerekir (ayrı iş, XMC-001) | Sahip | Açık; v1'de yazılım yolu yok |
| AK-7 | Silinmiş, hiç kilitlenmemiş taslağın kodu bugün tekil dizinde kalıcı tutuluyor (kısmi olmayan dizin); bu paket yalnız 500'ü 409 yapar, serbest bırakmaz | Kule | Açık; davranış bugünkü gibi |
| AK-8 | MOD-0220 A1b (yazıcı çiti) ile eşgüdüm: aynı işleyiciler; hangisi önce girerse öbürünün testleri yeşil kalmalı | Kule | Açık |
