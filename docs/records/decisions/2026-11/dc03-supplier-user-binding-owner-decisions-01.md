# DC-03 — tedarikçi kullanıcısı bağlama (supplier_user): sahip kararları 01

- Konu: MOD-0148 Supplier Portal v1 kod kapısı DC-03 (karar 2026-10-21, MVP-6 plan v10.0 :90 C4; D9 tetiği
  `docs/records/decisions/2026-10/mvp6-mod0148-start-owner-decision-2026-10-22.md`). Sahip: merkez (Beste).
- Girdi: CT salt okunur araştırması (2026-11-13; alt ajan acd17c1538960f9e0) — kodda yapı yok; Blueprint SoR haritası "external identity
  bindings" sahipliğini MOD-0360'a verir; D1–D8 taslağı main dışı dallarda (`origin/docs/sce/dc03-supplier-actor-binding-draft-01`
  `eaa7bdd06`, `origin/feature/sce/mod-0148-dc03-decision-prep-01` `67a30c7f0`), #172 birleştirilmeden kapandı.

## Kararlar

- **2026-11-13 — M2 kabul (Beste, "evet"; kaynak: sahip sohbet cevabı):** Natig'in isteği
  (`docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC03-BINDING-CANONICAL-CODE-REQUEST.md`) kabul edildi: bağlama kaydı, bağlama anında
  MOD-0140'ın döndürdüğü kanonik `SUP-` kodunu saklar; kanonik kod değişirse bağlamayı DC-03 sahibi (merkez) düzeltir.

- **2026-11-13 — Kimlik (Beste, "tamam bu olabilir"):** DC-03 bağlama işi **MOD-0360-FU01** olarak açılır (Blueprint SoR: MOD-0360
  Tenant User & Identity Management — "external identity bindings"). Pack açılmadan önce `verify_module_id.py --check-id MOD-0360-FU01
  --name "Supplier User Binding" --parent MOD-0360` koşulur (fail-closed).
- **2026-11-13 — Token süresi (Beste, "Öneriyi kabul ediyorum"):** D4 düzeltildi: `supplier_user` erişim anahtarı **15 dk**, arka planda
  sessizce yenilenir; oturum (yenileme anahtarı) en çok **8 saat**, sonra yeniden giriş + e-posta kodu.
- **2026-11-13 — Taban:** paket, main dışı D1–D8 taslağını (D4 yukarıdaki gibi) esas alır; D1–D8 paketin S0 diliminde sahip onayına
  yeniden sunulur (bağlayıcı kayıt bu dosyaya yazılır).
- **Kiracı modeli (CT açıklaması, sahip sorusu üzerine):** tedarikçi kendi kiracısını açmaz; hizmet verdiği firmanın kiracısında (ör. GMG)
  `supplier_user` hesabıdır; başka bir kiracıda ayrı hesap (DC-03 C4 "kiracı başına e-posta başına tek hesap").
- **2026-11-13 — r1 incelemesi sahip kararları (Beste, "hepsi öneri gibi"; inceleme `docs/records/reviews/MOD-0360-FU01/2026-11-13-ce91314f.md`):**
  F-2 — erişim anahtarının bitişi oturum bitişini **asla geçmez**: exp = min(nbf + 15 dk, oturum sonu); oturum tam 8 saatte biter.
  F-5 — bağlamada `eligibleForTransactions` yanında tedarikçinin **onay durumu (onboardingStatus) ve satın almaya açıklığı
  (purchasingEnabled)** da denetlenir; reddedilmiş ya da satın almaya kapalı tedarikçiye portal açılmaz.
- **2026-11-13 — Auth soruları SQ-1…SQ-6, SQ-12, SQ-13, SQ-15 (Beste, merkez — Platform / Auth işini merkez yürütür: "diğerleri öneri
  gibi olsun"; Ali'ye bilgi verilir; paket MOD-0360-FU01 r2 8086bf9a 23):**
  SQ-1 hesap düzeyinde ayırt edici: **`AccountKind.Supplier = 3`** (taslağın "hesap Human kalır" metni değişir).
  SQ-2 v1 iki adımlı doğrulama **yalnız e-posta kodu**; challenge kaydına **kapı alanı** eklenir (kiracı kapısı tedarikçi challenge'ını kabul etmez).
  SQ-3 S2S `binding_rev` ucu: ayrı audience **`TRUSTED_SUPPLIER_BINDING_CONSUMER`** (MOD-0140 DL-7 audience'ından ayrı; MOD-0012-FU01
  `TRUSTED_SECRET_VAULT_CONSUMER` deseni), kabul edilen tek `service_name` **`Diten.SupplyChain`** (MOD-0148 portalını taşıyan servis);
  başka servis adı 403.
  SQ-4 D7 MOD-0140 S-4'e hizalanır (servis token'ı `POST /validate` ve tekil `GET /{supplierId}/eligibility`).
  SQ-5 Bölüm 5'teki Auth dosya listesine XMC-001 izni (merkez).
  SQ-6 ayrı **`auth.supplier-bindings.read`** anahtarı; bağa görünen kayıt kodu verilmez (RCS-001 "kod yok"); kod düzeltmesini kiracıda
  `auth.supplier-bindings.manage` yetkili hesap yürütür.
  SQ-12 (a) yalnız `[Authorize]` taşıyan uçların envanteri çıkarılır, her biri AC-15 kapsamına (tedarikçi token'ı reddi) eklenir — en az Auth uçları.
  SQ-13 (1) tedarikçi hesabı kiracı kullanıcı kotasına **sayılmaz** (ayrı sayım); (2) davet bağlantısı bugünkü Platform bildirim akışıyla;
  (3) v1 parola belirleme bugünkü anonim `/api/users/set-password` + kiracı Web sayfasıyla.
  SQ-15 son aktif bağ kaldırılınca hesap **pasif**; kişisel veri saklama kiracının kullanıcı saklama kuralını izler.
  Açık kalanlar: SQ-7, SQ-8, SQ-11, SQ-14 (Natig), SQ-9, SQ-10 (Cihan).

## 2026-11-13 akşam — Natig'i beklemeden ilerleme (Beste: "Natig'i beklemeden ilerleyelim, o bizim yaptığımıza göre yapsın")

- **SQ-7 = öneri:** `supplier_user` token'ına `supplychain.supplier-portal.read` / `.submit` sabit küme olarak girer (kiracı rolü yok, D2).
- **SQ-8:** iki adımlı doğrulamayı Auth zorlar (token yalnız OTP adımından sonra verilir, `amr` taşır); MOD-0148'in `amr`'yi yeniden
  denetlemesi isteğe bağlı savunmadır, bu paketin kapısı değildir.
- **SQ-11:** Auth `binding_rev` ucunu (AC-19) bu paket her durumda sunar. MOD-0148 yazmadan önce çağırırsa bağ kaldırma anında
  etkili olur (uyuşmazlık 403, Auth yok 503 — D8); çağırmazsa erişim token'ı ömrü içinde (en çok 15 dk). D3 bu iki durumla yazılır;
  MOD-0148'in seçimi kendi paketinde (Natig) kayda geçer, bu paketi bekletmez.
- **SQ-14:** portal izin anahtarlarının kataloğa tek yazarı MOD-0148 manifestidir (Natig); DL-2 başlama kapısı olarak kalır, DL-1'i bekletmez.
- **S0 kapanışı:** SQ-1, SQ-3, SQ-4, SQ-12, SQ-13 daha önce karara bağlandı; SQ-7 ve SQ-11 bu satırla kapandı. S0 kapalı.
- **MOD-0140 DL-3 (Cihan):** DL-1 başlama kapısı olmaktan çıkar, **kabul kapısı** olur. DL-1 uygunluk okumasını donmuş
  `supplier.openapi.yaml` 1.3.0'a karşı sözleşme ikiziyle kodlar; DL-1 ACCEPTED için MOD-0140 DL-3 main'de ve Gateway üzerinden canlı
  kanıt (gerçek 200, 404 `UNKNOWN_SUPPLIER`, token reddi) gerekir. Hata kodu yeri (sözleşme `error.code`; bugün kodda `Errors[]`): okuyucu
  sözleşmeye göre yazılır, MOD-0140 sözleşmeye uyar (Cihan'a iletildi).
- Natig'e bilgi metni gönderilir (MVP-6, kararlar ve MOD-0148'den beklenenler).

## Ek — DL-1 kapsam soruları S-1…S-4 (Beste, 2026-11-14)

Geliştirici oturumu (WP-0360-FU01-DL1) başlangıç ölçümünde dört XMC-001 / kapsam sorusu sordu; sahip (Beste) önerilenleri seçti:

- **S-1:** `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`'e tek `Perm.Entity.SUPPLIER_BINDINGS` satırı (yalnız
  ekleme; fiiller `manage` / `read` köprüde var, yeni fiil yok). Katalog tohumu `DataSeeder.BuildCanonicalPermissions()`'da yazılır. Platform
  `AccessGovernanceManifestProvider.cs` bir testle zorlanırsa geliştirici ayrıca yazar (o dosya için ayrı soru).
- **S-2:** `Supplier` hesap türünü yalnız bağ komutu açar; şu dosyalarda yalnız `Supplier`'ı reddeden satırlar onaylı:
  `Features/Users/Services/AccountKindWriter.cs` (`TryParse` tanımaz + `Apply` tedarikçi hesabında türü değiştirmez),
  `SetAccountKindCommandValidator` (`IsDefinedKindName`), `CreateUserCommandHandler.ResolveInitialKind` (ya da `CreateUserCommandValidator`).
  Ters yön (tedarikçi → `Human`) de kapalı. AC + sabotaj.
- **S-3:** `Domain/Entities/AuthAuditLog.cs`'e yalnız ekleme `EntityType` (string?) + `EntityId` (Guid?) ve indeks
  `(TenantId, EntityType, EntityId, OccurredAt)` (`MongoDbIndexConfigurations.cs`) — FU27 (D98 öneri 2) biçimiyle aynı.
- **S-4:** `Application/Common/Interfaces/IPlatformAuditForwarder.cs` + `Infrastructure/Services/PlatformAuditForwarder.cs`'e yalnız ekleme
  `Task<bool> TryForwardAsync(...)` (mevcut `ForwardAsync` onu çağırıp sonucu yok sayar; davranış aynen) ve `PlatformAuditEvent.IsMetaAudit`
  (varsayılan `false`) — FU27 D99/1, D106/1 emsali. Teslim edilmezse geçmiş ucu 503.
- Not: Auth MOD-0018 sahibi Ali; bu satırlar FU27 tasarımıyla aynı biçimde olduğundan çakışmaz, Ali'ye toplu bilgilendirmede bildirilir.

### S-3 uygulama notu ve S-5 (Beste, 2026-11-14)

- **S-3 uygulaması:** geliştirici `AuthAuditLog.cs`'e dokunmadan kendi satır tipiyle `authAuditLogs`'a aynı alanlar + `EntityType`, `EntityId`,
  `BindingId`, `AttemptId`, `Phase` yazıyor (`IgnoreExtraElementsConvention(true)` ölçüldü; satır `AuthAuditLog` olarak da okunuyor — testli).
  Onaylanan seçenekten daha dar; CT kabul. İndeks `MongoDbIndexConfigurations.cs`'te.
- **S-5 onaylı:** `tests/.../Users/AdminResetPathGuardTests.cs` `NonResetLinks` listesine tek satır
  `(…/CreateSupplierActorBindingHandler.cs, "NewSupplierAccountAsync")` (yalnız ekleme; `CreateUser` / `CreateByInvitationAsync` emsali).

### S-6…S-9 ve dev Mongo olayı (Beste, 2026-11-14)

- **S-6 onaylı:** `AccessGovernanceManifestSeedParityTests.ApiOnlyKeys`'e `auth.supplier-bindings.manage` ve `auth.supplier-bindings.read`
  (emsal `auth.users.lookup`; v1 ekransız; DL-4 ekranıyla manifeste taşınır). Platform `AccessGovernanceManifestProvider.cs`'e dokunulmaz.
- **S-7 onaylı:** `permission-scope-baseline.csv`'ye iki satır, kapsam 0 (Tenant).
- **S-8 onaylı:** `ProductIdentityEntitlementReconciliationCommandContractTests` `AddPersistence` parmak izi sabiti yeni değere (yalnız
  `Persistence/DependencyInjection.cs`'in dört kayıt satırı yüzünden).
- **S-9 onaylı:** aynı dosyada `ClosedWriterInventory` envanterine yeni yazar çağrıları elle, satır satır gerekçeli (yeniden üretim yok);
  tam liste raporda.
- **Dev Mongo olayı:** geliştiricinin geçici A-2 testi paylaşılan dev Auth veritabanında (`localhost:27017/diten_auth_v3`) boş
  `supplierActorBindings` koleksiyonu + 3 indeks oluşturdu (veri yazılmadı; salt-okunur ölçüm). Sahip kararı: **koleksiyon silinir**
  (önce belge sayısının 0 olduğu doğrulanır). Ders: `WithWebHostBuilder` kopyası izole Mongo ayarını taşımıyor — testler paylaşılan dev
  Mongo'ya hiç açılmamalı.

## Ek — MOD-0360-FU01 kabulü MOD-0140 DL-3 ile (Beste, 2026-11-17)

Kaynak: Cihan'ın MVP-2 devir notu §5 (PR #444, `docs/records/audits/2026-11/mvp2-mvp3-ct-handoff-01/MVP-2-DEVIR.md`). Başarılı bağ
`onboardingStatus = Approved` ister (F-5); Procurement bu değeri ancak MOD-0140 DL-4'te yazar (S-3'e bağlı, tarihsiz). DL-3 ile yalnız
"uygun değil → 422" ölçülebilir.

- **Karar A — koşullu kabul:** MOD-0140 DL-3 main'e girince MOD-0360-FU01 kabulü "uygun değil → 422" yolu ölçülerek koşullu verilir;
  başarılı bağ ölçütü DL-4'e kadar açık kalır ve DL-4 main'e girince ölçülüp kapanır. Test kiracısında Procurement verisine dışarıdan
  `Approved` yazılmaz (B reddedildi); F-5 değiştirilmez (C reddedildi).
- **Zarf ↔ sözleşme:** okuyucu hem MOD-0140 yanıt zarfını hem 1.3.0 sözleşme şeklini okur (devir notu önerisi); MOD-0140 §20 kararı
  gelince sadeleşir.

## Ek — MOD-0360-FU01 r3: MOD-0148 F-2 / F-8 (Ali, 2026-11-24)

Kaynak: Ali'nin Beste'ye cevabı (2026-11-24), r3 taslağına (2026-11-19) ve
`docs/records/reviews/MOD-0148/2026-11-15-75b33fcb.md` F-2 / F-8'e. Ali MOD-0018 / MOD-0360 sahibidir; XMC-001 izni Ali'den.

- **F-2 — evet:** `supplychain.supplier-portal.read` ve `.submit` otomatik verilmeyen izinler listesine
  (`ExplicitGrantOnlyPermissions`) girer; varsayılan rol şablonu, tam katalog ve modül eşitlemesi bu izinleri vermez. Bu değişiklik
  MOD-0360-FU01 DL-2'nin **ilk commit'i** (DL-2.0) olur ve MOD-0148 P4'ten **önce** main'e girer (SQ-17).
- **F-8 — 403:** listede olmayan servis adı ya da eksik / geçersiz `X-Tenant-Id` için 403; paketteki 401'ler buna göre düzeltilir.
  Başka kiracıya ait bağ için 404 ve `SUPPLIER_BINDING_NOT_FOUND` kodu (SQ-16).
- **Ali'nin koşulu:** hiç var olmayan bağ için de tıpatıp aynı cevap (aynı kod, aynı gövde) döner ve bu bir testle gösterilir
  (pakette AC-29, sabotaj 3; istek başına değişen izleme alanları karşılaştırma dışı).
- **Sonraki adım:** r3 pakete işlendi; bağımsız inceleme (CT-APR-02) sonrası onay satırı (CT-APR-01) Ali'ye getirilir.
- **Açık:** SQ-18 — DL-2.0, Bölüm 0.4 S0 kapanışını bekler mi? Öneri: yalnız r3 onayı + XMC-001. Sahip: Beste / Ali.
- **SQ-18 — KAPANDI (Ali, 2026-11-24; Beste iletti):** DL-2.0, Bölüm 0.4 S0 kapanışını **beklemez**; yalnız r3 onayını
  (CT-APR-02 + CT-APR-01) ve XMC-001 iznini bekler. Pakette SQ-18 "açık" görünür; bu kayıt onu kapatır, metin bir sonraki
  revizyonda hizalanır. Ali r3'ü (paket sha256 `068646c1…`) "onaylıyorum" ile onayladı; onay satırı bağımsız inceleme
  kaydından sonra yazılır.
