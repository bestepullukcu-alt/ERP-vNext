---
id: MOD-0262-FU08
name: Document Binary Store Upload Audit Completion
parent: MOD-0262
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: ready-for-dev
approval_status: pending
revision: "r2 (2026-11-13) — r1 (sha256 5d3bc4c9…befbdc9f9, commit 045bd0b67) bağımsız incelemesi KOŞULLU (docs/records/reviews/MOD-0262-FU08/2026-11-13-5d3bc4c9.md; 3 orta, 3 düşük, engel yok) tek geçişte işlendi. F-1: K-7 (Beste, 2026-11-13 — MOD-0021 korunan yoluna dar ekleme izni) ve CT kararları (MVP-1 CT, 2026-11-13 — arayüz biçimi, sağlayıcı istisnası, tek dilim, kayıt satırı, dal tabanı MVP-1 hattı) pakete yazıldı; §5, §7, §18, §19 ve kaynak satırı hizalandı. F-2: tekrarsızlık anahtarı etkisi doğru tanımlandı (§8 madde 5, §13); yeni AC-16 (aynı korelasyonla 503 sonra 201 → 2 kayıt) ve sabotajı S-19. F-3: AC-9 TrustedDocumentRepositoryConsumerMongoTests.cs desenine çevrildi (§5, §17). F-4: yürütücü atfı :152, S-10'un kırmızısının kaynağı. F-5: DL-1 metadata beklentisi ayrı yazıldı (AC-3, §12). F-6: AC-4 istisna alt vakası yapısal notu ve sabotajı S-20; AC-10 izinsiz token iki izni de taşımaz. AC-1…AC-15 numaraları değişmedi, AC-16 eklendi. status ready-for-dev (S-49); r2 yeni bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) ister. — r1 (2026-11-13) — ilk taslak. MOD-0262-FU01 r9 §20'deki iki adlı takip işi (FU-0262-AUDIT-ENTITY ve FU-0262-TRAILING-PART) sahip kararı K-5 (Beste, 2026-11-13) ile tek FU olarak açıldı; yol (b) sahip kararı K-6 (Beste, 2026-11-13)."
status_note: "r2: ready-for-dev; approval_status pending — r2'nin CT-APR-02 bağımsız incelemesi ve CT-APR-01 onay satırı (risk HIGH) bekleniyor; onay satırı gelmeden kod yazılmaz. Korunan yol izni K-7 ile kayıtlı (§7)."
owner: platform-shared-services
branch: feature/pss/mod-0262-fu08-upload-audit-completion
started: 2026-11-13
target: TBD
form_field_count: 0
capability_pack: DCP-008
---

# MOD-0262-FU08 — Document Binary Store Upload Audit Completion

> **Durum (r2, 2026-11-13).** `status: ready-for-dev`, `approval_status: pending`. r1'in bağımsız incelemesi KOŞULLU
> (`docs/records/reviews/MOD-0262-FU08/2026-11-13-5d3bc4c9.md`); bulgular F-1…F-6 bu sürümde işlendi (§19.1). Geriye r2'nin
> CT-APR-02 bağımsız incelemesi ve CT-APR-01 onay satırı kaldı (§18). `status` tek başına yetki değildir; onay satırı gelmeden
> kod yazılmaz. MOD-0021 korunan yol izni K-7 ile kayıtlıdır (§7). Sahip: Beste (merkez / Platform) — K-5.
>
> **Kimlik (DCP-002, ölçüldü 2026-11-13).** `MOD-0262` (Internal Document Repository Service) altında FU. `MOD-0262-FU02`…`FU07`
> DCP-008 üye tablosunda başka içeriğe ayrılmıştır (`execution/portfolio/delivery-capability-packs/DCP-008-internal-document-repository-service.md`);
> `MOD-0262-FU08` depoda hiçbir pakette, kayıtta ya da HEAD / `origin/main` ağacında kullanılmıyordu (tek geçiş: FU01 §19.3'teki
> "depoda `MOD-0262-FU08` ve sonrası yok" cümlesi). Ön kontrol: `verify_module_id.py . --check-id MOD-0262-FU08 --name "Document Binary Store Upload Audit Completion" --parent MOD-0262`
> → `OK  MOD-0262-FU08: proven against Blueprint/registry.` (çıkış 0). Yeni `MOD-xxxx` açılmaz. Ölçüm sınırı §19.
>
> **Kaynak.** Ana paket `execution/domains/platform-shared-services/module-packs/MOD-0262-FU01-document-binary-store.md` r9
> (onaylı: `execution/approvals/approvals.tsv` satır 88; inceleme `docs/records/reviews/MOD-0262-FU01/2026-11-13-71d15dec.md`):
> §20 satırları `FU-0262-AUDIT-ENTITY` ve `FU-0262-TRAILING-PART`, §21 yükleme satırları, AC-21, AC-25. Sahip ve CT kararları
> `docs/records/decisions/2026-11/mod0262-fu01-mod0181-owner-decisions-2026-11-12.md`: K-2, K-3, K-5, K-6 (satır 23), CT kararları
> (MVP-1 CT, satır 28) ve K-7 (satır 33).

---

## 1. Module Summary

FU01'in iki yükleme komutu bugün denetim kaydını yazıyor, ama kayıt yüklenen nesneyi adlandırmıyor: `EntityId` ve §21
`AfterState` boş. Nedeni sıradır: `AuditBehavior` metadata'yı handler'dan önce komutun `GetAuditMetadata()`'sından alır
(`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs:143`), `ContentId` ise sağlayıcı
çalışınca doğar. Bugün yükleme kaydını satıra bağlayan yalnız `CorrelationId`'dir (FU01 AC-25 KISMİ).

Bu FU iki işi tek dilimde kapatır (K-5: ikisi aynı yükleme yolunu ve aynı denetim kaydını ölçer):

- **(A) Yükleme denetim kaydının nesnesi.** DL-1 `StoreRepositoryObjectCommand` ve DL-2 `StoreTrustedRepositoryObjectCommand`
  başarı kayıtları `EntityId = ContentId` ve §21 `AfterState`'i taşır. Yol (b) — K-6: `AuditBehavior` başarılı yanıttan
  `EntityId` / `AfterState` alır. Alanları komut seçer (isteğe bağlı arayüz — CT kararı); `AuditBehavior` yalnız birleştirir.
  İçerik, `ObjectKey` ve dosya adı yazılmaz.
- **(B) Dosyadan sonraki bölüm, host'ta.** FU01 AC-21'in gerçek "dört alan + dosya + sonra alan" vakası host'ta ölçülür:
  400 `VALIDATION_FAILED`, sağlayıcı ≤ 1 çağrı, satır yok, yarım dosya yok, tek `AuditBehavior` kaydı. FU01 r9 incelemesinin
  F-1 isteği de burada karşılanır: kaydın yazarı, `Outcome` (`Failed`), `Operation` (`Create`) ve `ResponseStatusCode` (400)
  doğrulanır. Ayrıca `two-files` vakası da bu doğrulamaları alır.

**Hedef kullanıcı:** doğrudan yok; sayfa yok. Fayda denetim kaydını okuyanadır: yükleme kaydından satıra `EntityId` ile
gidilir.

---

## 2. Ownership and Boundaries

**Kapsamda**
- `AuditBehavior`'a başarılı yanıttan durum alma adımı (isteğe bağlı arayüz; arayüzü uygulamayan komut değişmez).
- Yeni sözleşme: `IAuditResponseStateProvider<TResponse>` + `AuditResponseState` (MOD-0021 sözleşme klasörü).
- İki yükleme komutunun bu arayüzü uygulaması ve §21 `AfterState` alanları.
- FU01 DL-2 host testlerine (B) vakaları ve `two-files` vakasına yazar / sonuç doğrulamaları.

**Kapsam dışı**
- `ContentId`'yi yürütücünün önceden üretmesi — K-3 seçenek (a); K-6 ile seçilmedi.
- Telafi ve içerik okuma kayıtları — `EntityId`'leri FU01 DL-2'de zaten yazılıyor ve ölçülüyor.
- Kullanıcı yolunun `MultipartReader`'a geçişi ve kullanıcı yolu içerik okuma denetimi — FU01 K-D'nin ayrı FU'su (bu paket değil).
- FU01 AC-29 (Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER`) — Auth'un işi, bu FU'nun kapısı değil (§7).
- Sayan akışın, yürütücünün, handler'ın ve sağlayıcının davranışı — (B) yalnız test ekler; bu dosyalara yalnız geçici sabotaj
  için dokunulur, geri alınır.
- Aynı korelasyonla gelen iki başarısız kaydın tek kayıtta kalması (bugünkü davranış, §13) — bu FU değiştirmez.

**Bu FU'nun sahip olduğu:** iki yükleme kaydının `EntityId` / `AfterState` sözleşmesi ve `AuditBehavior`'daki yanıt durumu adımı.
Başka bir şey değil.

---

## 3. Owned Objects

| Tür | Öğe | Not |
|---|---|---|
| Contract (yeni, MOD-0021 klasörü) | `IAuditResponseStateProvider<in TResponse>` — `AuditResponseState? GetAuditResponseState(TResponse response)` | Yalnız başarılı yanıtta çağrılır. Saf eşlemedir: depo okumaz, servis çağırmaz, yan etkisi yoktur. Biçim CT kararıdır (komut sağlar; yansıma yok). |
| Contract (yeni, MOD-0021 klasörü) | `AuditResponseState(Guid? EntityId, IReadOnlyDictionary<string, object?>? AfterState)` | Aynı dosyada. |
| Behavior (değişen) | `AuditBehavior<TRequest, TResponse>` | Başarılı sonuçtan sonra, istek arayüzü uyguluyorsa durumu alır; komutun kendi metadata değerleri önceliklidir (§8). |
| Command (değişen, DL-1) | `StoreRepositoryObjectCommand` | `IAuditResponseStateProvider<Response<RepositoryObjectModel>>` uygular. Route, izin, imza ve `GetAuditMetadata()` değişmez. |
| Command (değişen, DL-2) | `StoreTrustedRepositoryObjectCommand` | Aynı arayüz; `TrustedConsumerModule` komutun `Consumer.ConsumerModule`'ünden gelir. `GetAuditMetadata()` değişmez. |
| Model yardımcısı (yeni, iç) | `RepositoryObjectAuditState.For(RepositoryObjectModel model, string? trustedConsumerModule)` | `DocumentRepositoryModels.cs` içinde (DTO'lar tek dosyada). İki komut aynı alan listesini buradan alır. |
| API | **yok** | Yeni uç yok; var olan uçların yanıt gövdesi değişmez. |
| Permission | **yok** | Yeni izin yok. |
| Frontend route | **yok** | `shell: none`. |

Adlar bu paketin kararıdır. Geliştirici farklı bir ad ölçerse (ör. çakışma) raporlar; sessizce değiştirmez.

---

## 4. Entity Fields

Yeni varlık ya da alan yok. `RepositoryObject` (`TenantScopedEntity`) ve `ContentRef` değişmez; şema, indeks ve taşıma yok.

**Denetim kaydı alanları (yalnız iki yükleme komutunun başarı kaydı):**

| Alan | Değer | Biçim |
|---|---|---|
| `EntityId` | yanıtın `ContentId`'si | `Guid` (kayıt alanı) |
| `AfterState.ContentId` | `ContentId` | metin, `"D"` biçimi |
| `AfterState.Scope` | satırın `Scope`'u | metin (enum adı, ör. `Documents`) |
| `AfterState.OwningItemId` | `OwningItemId` | metin, `"D"` |
| `AfterState.OwningVersionId` | `OwningVersionId` | metin, `"D"` |
| `AfterState.ByteSize` | `ByteSize` | `Int64` |
| `AfterState.Checksum` | `Checksum` (SHA-256, depo hesaplar) | metin |
| `AfterState.TrustedConsumerModule` | yalnız DL-2: `Consumer.ConsumerModule` (v1 `MOD-0217`) | metin |

Guid değerleri `AfterState`'te metin yazılır: `Dictionary<string, object?>` içindeki değer nesne serileştiricisinden geçer ve FU01
metadata'sı da Guid'i `"D"` metni yazar. Bu karar çıkarımdır; AC-9 gerçek Mongo gidiş-dönüşüyle ölçer.

**Kayda hiç yazılmayan:** `ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, `CreatedAt`, dosya içeriği, token,
istemci sırrı. `AfterState` metadata değildir; iki komutun metadata'sı değişmez (DL-2: FU01 §21 yükleme kümesi; DL-1: bugün
metadata yok — §12).

---

## 5. Repo Scope

```text
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/IAuditResponseStateProvider.cs            ← NEW (arayüz + AuditResponseState; MOD-0021 korunan klasör — K-7 izni)
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs                       ← yanıt durumu adımı (FU01 §6'da korunan dosya — K-7 izni)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/StoreRepositoryObjectCommand.cs         ← arayüzü uygular
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/StoreTrustedRepositoryObjectCommand.cs  ← arayüzü uygular; sınıf yorumundaki "no entity id" cümlesi güncellenir
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/DocumentRepositoryModels.cs     ← RepositoryObjectAuditState (iç yardımcı)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditBehaviorResponseStateTests.cs          ← NEW (AC-4…AC-7, sahte komutlarla)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumerMongoTests.cs     ← AC-9, AC-16 (host + MongoIntegrationHarness + AuditOutboxRepository deseni)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumerUploadTests.cs   ← AC-1, AC-3, AC-4 (host), AC-8, AC-11, AC-12, AC-13
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/DocumentRepositoryUserUploadAuditTests.cs          ← NEW (AC-2, AC-10; host altyapısı TrustedDocumentRepositoryConsumerHostTests)
```

**Dal tabanı (CT kararı, karar dosyası satır 28): MVP-1 hattı.** FU01 DL-2 kodu (`dae798bc9`, FIX-01 `333e91559`) orada; `origin/main`'de
değil. Dal `feature/pss/mod-0262-fu08-upload-audit-completion` MVP-1 hattından açılır.

---

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız `integration-agent`; bu FU rota gerektirmez (§15).
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.CrmService/**`, `services/Diten.EnterpriseStrategyService/**` — başka modüllerin şeridi.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` — MOD-0021'in. K-7 izniyle bu FU yalnız yeni
  `IAuditResponseStateProvider.cs`'i ekler; başka dosyası değişmez (`AuditActorTypeResolver.cs`, `AuditRequestMetadata.cs`,
  `AuditAppendRequest.cs` dahil).
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs` — FU01 §6'da "değişmeyen denetim
  dosyası". K-7 izniyle bu FU yalnız §8'deki adımı ekler.
- Değişmeyen: `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditService.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditIdempotencyKeyBuilder.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ValidationBehavior.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ExceptionBehavior.cs`.
- Değişmeyen (yalnız geçici sabotaj, geri alınır): `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryUploadLimits.cs`,
  `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/CommandHandlers/StoreTrustedRepositoryObjectHandler.cs`,
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs`,
  `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs`.

---

## 7. Dependencies

| Bağımlılık | Tür | Durum |
|---|---|---|
| **MOD-0021 korunan yol izni** — `AuditBehavior.cs` değişikliği ve `Contracts/Audit/` altına yeni dosya | Kod kapısı | ✅ **K-7** (Beste, 2026-11-13; `docs/records/decisions/2026-11/mod0262-fu01-mod0181-owner-decisions-2026-11-12.md` satır 33): dar ekleme izni — yalnız iki yükleme komutu yanıt durumunu sağlar, yalnız başarıda eklenir, sağlayıcı hatası iş sonucunu bozmaz, diğer komutların kaydı değişmez (AC-6, S-8). MOD-0021 sahibi merkez (K-7 metni). Bu izin r2 onay satırının yerine geçmez. |
| MOD-0262-FU01 DL-2 kodu (`dae798bc9` + `333e91559`) | **HARD** — kod tabanı | ✅ MVP-1 hattında; `origin/main`'de değil. Dal tabanı MVP-1 hattı (CT kararı, satır 28). |
| MOD-0262-FU01 r9 onayı | **HARD** — sözleşme | ✅ `approvals.tsv` satır 88 (onaylayan Ali, 2026-11-13). |
| MOD-0021 Audit Trail (`AuditBehavior` → `IAuditService` → `audit_outbox`) | altyapı | ✅ `tests/architecture/audit-ledger/Diten.Platform.md` → `platform-merkezi` (yol `a`). |
| FU01 AC-29 — Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` | **Bu FU'nun kapısı değil** | ⛔ main'de yok (Ali, Auth). Testler test anahtarıyla imzalı token kullanır; bu FU'nun kabulü AC-29'u beklemez. |
| MOD-0217 DL-2 (tüketici) | ters bağımlılık | Etkilenmez: yanıt gövdesi, ret kodları ve durum kodları değişmez. Denetim tarafındaki tek görünür fark §8 madde 5'tir. |

---

## 8. Runtime Constraints

- Servis `Diten.Platform`, port **5057**; Gateway **5000**. Yeni uç yok.
- Kiracı: kayıt kiracısı bugünkü gibi ortam kiracısıdır. Kullanıcı yolunda kullanıcının token kiracısı; güvenilen yolda yürütücünün
  komut gövdesini çalıştırmadan önce `TenantScope.Begin` ile bağladığı token kiracısı
  (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs:152`; satır 139 aynı
  bağlamayı kimliği doğrulanmış ret dalı için yapar). Yanıt durumu adımı depo okumaz; kiracılar arası veri okuyamaz.
- Soft delete, şema, indeks: değişmez.

**`AuditBehavior` yanıt durumu kuralı (K-6, yol b; biçim CT kararı; izin K-7).** Bugün başarılı sonuçta kayıt
`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs:69` ile
`:81` arasında yazılır; `EntityId` ve `AfterState` yalnız metadata'dan gelir (`:175`, `:179`). Bu FU'dan sonra:

1. Yalnız `outcome == Succeeded` olduğunda ve istek `IAuditResponseStateProvider<TResponse>` uyguluyorsa
   `GetAuditResponseState(response)` çağrılır. `Failed` sonuçta, çalışma zamanı istisnasında, iptalde, dışlanan ve sorgu
   isteklerinde ve `ITransactionOwnedAuditCommand` yolunda çağrılmaz. İstisna dalında (`:63`–`:66`) yanıt nesnesi yoktur;
   sağlayıcının orada çağrılamaması yapısaldır (AC-4).
2. Birleştirme: `EntityId = metadata.EntityId ?? state.EntityId`; `AfterState = metadata.AfterState ?? state.AfterState`.
   Komutun kendi değeri önceliklidir. `Metadata`, `CorrelationId`, `Operation`, `Category`, `EntityType`, aktör ve kiracı değişmez.
3. Sağlayıcı istisna atarsa (CT kararı): yanıt aynen döner, kayıt durumsuz (bugünkü gibi) yazılır, uyarı logu yalnız istek
   türü adını taşır (istisna mesajı ve değer yok). `OperationCanceledException` bugünkü gibi yayılır. Gerekçe: baytlar ve satır
   yazıldıktan sonra zenginleştirme hatası iş sonucunu 500'e çevirmemeli.
4. Yansıma yok: `AuditBehavior` yanıtın `Data`'sını ya da bir `ContentId` / `Id` özelliğini genel olarak okumaz. Arayüzü
   uygulamayan komutun kaydı alan alan bugünküyle aynıdır.
5. **Tekrarsızlık anahtarı (r2, F-2).** Anahtar `CorrelationId | RequestType | EntityType | EntityId | Operation | Sequence`
   üzerinden kurulur (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditIdempotencyKeyBuilder.cs:10`;
   çağrı `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditService.cs:73`); `Outcome` anahtarda yoktur.
   Yazıcı aynı anahtarı ikinci kez almazsa kayıt `Duplicate` olarak düşer (`AuditService.cs:109`; üretimde `audit_outbox`
   `IdempotencyKey` indeksi). Güvenilen yolda korelasyon `X-Correlation-Id` başlığından gelir
   (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs:546`), yani
   tüketicinin aynı başlıkla yaptığı yeniden deneme aynı korelasyonu taşır.
   - **Bugün:** yükleme kaydında `EntityId` boş olduğundan aynı korelasyonla yapılan ikinci yüklemenin kaydı — sonuç ne olursa
     olsun (503 sonra 201, ya da iki kez 201) — ilkinin anahtarını taşır ve düşer. Başarılı ikinci yükleme kayıtsız kalır.
   - **FU08'den sonra:** başarı kaydı `ContentId`'yi taşıdığı için anahtarı ayrıdır. Aynı korelasyonla 503 sonra 201 → **2 kayıt**
     (`Failed` + `Succeeded`); iki kez 201 → iki farklı `ContentId`, **2 kayıt**. AC-16 ölçer.
   - **Değişmeyen:** aynı korelasyonla iki başarısız yükleme → ikisinin de `EntityId`'si boş, anahtar aynı → **1 kayıt**
     (ikincisi `Duplicate`). Bilinen sınır, bu FU değiştirmez (§13).
   - İptal ve ret kayıtlarının istek türü farklıdır
     (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryRefusalAudit.cs:30` ve `:31`); onlarla anahtar
     paylaşımı yoktur. FU01 D-2 sınırını (tek istek içinde iptal satırı + başarı) AC-14, FU01 AC-33 / AC-35 testleriyle ölçer.

---

## 9. Layout & Shell Contract

`shell: none` — yalnız arka uç. Razor görünümü, layout, `wwwroot` dosyası, RESX yok. DataTable yok; `data_mode` uygulanmaz.

---

## 10. Backend File Convention

`golden_reference: none`; Slim / Compact ön yüz sözleşmesi uygulanmaz. Arka uç FU01 §10 düzenindedir; bu FU yeni komut, sorgu,
handler ya da doğrulayıcı eklemez. Yeni dosyalar:

```text
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/
└── IAuditResponseStateProvider.cs        (interface + sealed record AuditResponseState)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/
└── DocumentRepositoryModels.cs           (RepositoryObjectAuditState — DTO'lar tek dosyada kalır)
```

---

## 11. Frontend File Contract

Yok (`shell: none`, `golden_reference: none`).

---

## 12. Validation Rules

Yeni kullanıcı girdisi yok; doğrulayıcı değişmez. Sözleşme kuralları:

| Kural | Nerede | Ölçüm |
|---|---|---|
| `AfterState` anahtar kümesi DL-1'de tam altı, DL-2'de tam yedi anahtar (§4); eksik ve fazla 0 | `RepositoryObjectAuditState.For` | AC-1, AC-2, AC-3 |
| Yasak alan yok (`ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, içerik, token) | aynı | AC-3 |
| `EntityId` = yanıttaki `ContentId`; `Guid.Empty` değil | sağlayıcı | AC-1, AC-2 |
| Başarısız yanıtta durum yok | `AuditBehavior` | AC-4 |
| DL-2 metadata anahtar kümesi FU01 §21 yükleme kümesiyle aynı (ortak altı anahtar) | `StoreTrustedRepositoryObjectCommand.GetAuditMetadata()` değişmez | AC-3 |
| DL-1 metadata bugünkü gibi: `StoreRepositoryObjectCommand.GetAuditMetadata()` metadata taşımaz; başarı kaydında `Metadata` yalnız `AuditService`'in eklediği anahtarları taşır (bugünkü küme) | `StoreRepositoryObjectCommand.GetAuditMetadata()` değişmez | AC-3 |

---

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| Dört alan + dosya + sonra gelen alan (güvenilen yol) | 400 `VALIDATION_FAILED`; sağlayıcı 1 çağrı; satır yok; yarım dosya yok; tek `AuditBehavior` kaydı (`Failed` / `Create`, `ResponseStatusCode = 400`, `EntityId` ve `AfterState` boş); ret yazıcısı kaydı 0 (AC-11). |
| İki dosya bölümü | aynı (AC-12). |
| Sağlayıcı 400, akış içi 413 | bugünkü sonuç; kayıtta `EntityId` / `AfterState` boş (AC-4). |
| Sağlayıcı (yanıt durumu) istisna atar | iş yanıtı aynen; kayıt durumsuz; uyarı logu yalnız tür adı (AC-7). |
| Yetkisiz kullanıcı DL-1 yüklemesi (ne `manage` ne `visit-evidence.write`) | 403; satır yok; yükleme kaydı yok (AC-10). |
| Kiracı B'nin yüklemesi | kayıt B'de, `EntityId` B'nin satırı (AC-8). |
| Aynı korelasyonla yeniden deneme (r2, F-2; §8 madde 5) | 503 sonra 201 → 2 kayıt (`Failed`, `Succeeded` + `EntityId`); iki kez 201 → 2 kayıt, iki farklı `ContentId` (bugün ikinci kayıt `Duplicate` ile düşer). AC-16. |
| Aynı korelasyonla iki başarısız yükleme | 1 kayıt (ikincisi `Duplicate`): `EntityId` boş olduğundan anahtar aynı. Bugünkü davranış; bu FU değiştirmez (bilinen sınır). |
| Eşzamanlılık | Durum yalnız o isteğin yanıtından okunur; ortak durum, önbellek ya da depo okuması yok (AC-6, AC-8). |
| Mükerrer kayıt (tek istek) | yükleme başarısı tam 1 kayıt (sahte yazıcı); FU01 D-2 sınırı değişmez (AC-14). |

---

## 14. Authorization Convention

- Yeni izin ve yeni uç yok. DL-1 yükleme ucu `[HasPermission(DocumentRepositoryPermissions.Manage, DocumentRepositoryPermissions.VisitEvidenceWrite)]`
  ile kalır (`services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs:60`); öznitelik "herhangi
  biri yeter" kuralıyla çalışır, yetkisiz vaka iki izni de taşımayan token'dır (AC-10). İzin anahtarları
  `platform.document-repository.*` (Platform servisi biçimi).
- Güvenilen yol: `[AllowAnonymous]` denetleyici + yürütücüdeki kapı (servis token'ı, kapalı tüketici listesi, kiracı bağlama) —
  FU01 §8.1, değişmez.
- Aktör türü: kullanıcı yolunda token'ın aktör türü, güvenilen yolda `Service` (FU01 K-B) — değişmez.

---

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. Yeni uç yok; `ocelot.json`'a dokunulmaz; `integration-agent` görevi yok.

---

## 16. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-2 güvenilen yükleme başarı kaydı (gerçek HTTP host, gerçek `AuditBehavior` + `AuditService`, sahte yazıcı): 201'den sonra tek `StoreTrustedRepositoryObjectCommand` kaydı vardır; `EntityId` yanıt gövdesindeki `contentId`'ye eşittir; `AfterState` anahtar kümesi birebir {`ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum`, `TrustedConsumerModule`} ve değerler satırla aynıdır (Guid'ler `"D"` metni, `ByteSize` Int64, `Scope = Documents`, `Checksum` satırın SHA-256'sı, `TrustedConsumerModule = MOD-0217`). FU01 AC-25 testi (`A_service_token_without_permissions_runs_upload_meta_content_compensate_and_each_success_is_one_service_record`) yükleme kaydı için `EntityId` doğrulamasını da alır. Test: `TrustedDocumentRepositoryConsumerUploadTests.A_trusted_upload_record_carries_the_content_id_and_the_after_state`. Sabotaj S-1: `AuditBehavior`'daki yanıt durumu birleştirmesi kaldırılınca `EntityId` boş kalır, test kırmızı. Sabotaj S-2: `StoreTrustedRepositoryObjectCommand` sağlayıcısı `TrustedConsumerModule`'ü atlayınca anahtar kümesi doğrulaması kırmızı.
- [ ] AC-2 (zorunlu) — DL-1 kullanıcı yolu: `platform.document-repository.manage` izinli kullanıcı token'ıyla `POST api/v1/document-repository/objects` 201 döner; tek `StoreRepositoryObjectCommand` kaydında `EntityId = ContentId`, `AfterState` anahtar kümesi birebir {`ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum`} (`TrustedConsumerModule` yok), kayıt kiracısı token kiracısıdır. Test: `DocumentRepositoryUserUploadAuditTests.A_user_upload_record_carries_the_content_id_and_the_after_state`. Sabotaj S-3: `StoreRepositoryObjectCommand`'dan arayüz uygulaması kaldırılınca test kırmızı.
- [ ] AC-3 (zorunlu) — Gizli ve gereksiz bilgi yok: iki yükleme kaydının `AfterState`'inde ve `Metadata`'sında `ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, dosya içeriği ve token yoktur. Metadata ayrı ölçülür (r2, F-5): DL-2 yükleme kaydının metadata anahtar kümesi FU01 §21'deki ortak altı anahtardır (FU01 AC-25 testindeki `Keys(store)` eşitliği yeşil kalır); DL-1 yükleme kaydının metadata'sı bu FU'dan önceki kümeyle aynıdır (komut metadata vermez; test önce bugünkü kümeyi ölçüp sabitler, sonra eşitliği doğrular). Test: AC-1 ve AC-2 testlerindeki birebir anahtar kümeleri + `Keys(store)` eşitliği + `DocumentRepositoryUserUploadAuditTests.A_user_upload_record_metadata_is_unchanged`. Sabotaj S-4: `RepositoryObjectAuditState.For`'a `FileName` eklenince AC-1 ve AC-2 testleri kırmızı. Sabotaj S-5: durum `AfterState` yerine `Metadata`'ya yazılınca FU01 AC-25 anahtar testi ve DL-1 metadata testi kırmızı.
- [ ] AC-4 (zorunlu) — Yalnız başarıda: `Failed` sonuçta ve çalışma zamanı istisnasında sağlayıcı çağrılmaz; kayıtta `EntityId` ve `AfterState` boştur. Birim testler (`AuditBehaviorResponseStateTests`, sabit durum döndüren ve çağrı sayan sahte sağlayıcılı sahte komut): `A_failed_response_never_asks_the_response_state_provider`, `A_runtime_exception_never_asks_the_response_state_provider`. Host: `A_provider_400_is_one_AuditBehavior_record` ve `One_byte_over_50_MiB_is_413_from_inside_the_command_with_one_AuditBehavior_record_and_no_partial_file` kayıtta `EntityId` / `AfterState` boşluğunu doğrular. Sabotaj S-6: başarı koşulu kaldırılınca sahte sağlayıcının sabit `EntityId`'si `Failed` kayda düşer, ilk birim test kırmızı. İstisna alt vakası yapısaldır (r2, F-6): istisna dalında (`AuditBehavior.cs` `:63`–`:66`) yanıt nesnesi yoktur, S-6 o yolu etkilemez; test gerileme bekçisidir. Sabotaj S-20: istisna dalına sağlayıcıyı `default(TResponse)` ile çağıran satır eklenince sahte sağlayıcının çağrı sayısı 1 olur, istisna alt vakası kırmızı.
- [ ] AC-5 (zorunlu) — Komutun kendi metadata'sı önceliklidir: `GetAuditMetadata()` dolu `EntityId` / `AfterState` verirse yanıt durumu onları ezmez. Test: `AuditBehaviorResponseStateTests.Command_metadata_entity_id_and_after_state_win_over_the_response_state`. Sabotaj S-7: birleştirme sırası ters çevrilince (`state ?? metadata`) test kırmızı.
- [ ] AC-6 (zorunlu) — İsteğe bağlı; başka komut değişmez: arayüzü uygulamayan sahte komutun `AuditAppendRequest`'i alan alan bugünküyle aynıdır (yanıtında `ContentId` / `Id` özelliği olsa bile); `AuditBehaviorTests`'in tüm vakaları yeşil kalır. Test: `AuditBehaviorResponseStateTests.A_command_without_the_provider_gets_the_same_append_request`. Sabotaj S-8: `AuditBehavior`'a yanıtın `Data.ContentId`'sini yansımayla okuyan genel dal eklenince test kırmızı.
- [ ] AC-7 (zorunlu) — Sağlayıcı hatası iş sonucunu değiştirmez (CT kararı): sağlayıcı istisna atarsa yanıt aynen döner, tek kayıt durumsuz yazılır, uyarı logu istek türü adını taşır ve istisna mesajını taşımaz. Test: `AuditBehaviorResponseStateTests.A_throwing_response_state_provider_keeps_the_answer_and_writes_the_record_without_state`. Sabotaj S-9: sağlayıcı çağrısını saran yakalama kaldırılınca istisna yayılır, test kırmızı.
- [ ] AC-8 (zorunlu) — Kiracı izolasyonu: iki kiracının (A, B) güvenilen token'larıyla iki yükleme; her kaydın kiracısı kendi token kiracısıdır, `EntityId`'si o kiracının satırının `ContentId`'sidir; B'nin kaydında A'nın `ContentId`'si yoktur. Test: `TrustedDocumentRepositoryConsumerUploadTests.Two_tenants_uploads_each_record_names_its_own_tenants_content_id`. Sabotaj S-10: yürütücünün komut gövdesi için yaptığı token kiracısı bağlaması (`TrustedDocumentRepositoryConsumerRequestExecutor.cs:152`, `TenantScope.Begin`) kaldırılınca test kırmızı. Kırmızının kaynağı boş ortam kiracısıdır (satır ve kayıt kiracısız kalır ya da reddedilir), kiracılar arası sızıntı değil (r2, F-4); kiracılar arası karışmanın olmaması yapısaldır — durum yalnız o isteğin yanıtından okunur (§8 madde 4).
- [ ] AC-9 (zorunlu) — Gerçek Mongo (r2, F-3): `TrustedDocumentRepositoryConsumerMongoTests.cs` deseniyle (gerçek HTTP host + `MongoIntegrationHarness` + üretim `AuditOutboxRepository`) güvenilen yükleme yapılır; `audit_outbox`'tan okunan `StoreTrustedRepositoryObjectCommand` kaydında `EntityId` ve `AfterState` değerleri yanıtla ve satırla aynıdır (Guid alanları metin, `ByteSize` Int64; serileştirme hatası yok). Test: `TrustedDocumentRepositoryConsumerMongoTests.An_upload_record_with_after_state_round_trips_on_the_production_outbox`. Sabotaj S-11: `AuditBehavior`'daki sağlayıcı çağrısı kaldırılınca okunan kayıtta `EntityId` ve `AfterState` boş, test kırmızı.
- [ ] AC-10 (zorunlu) — Yetki: bu FU yeni uç ve izin açmaz; DL-1 yükleme ucunun `[HasPermission]`'ı aynıdır. Yetkisiz token ne `platform.document-repository.manage` ne `platform.document-repository.visit-evidence.write` taşır (öznitelik "herhangi biri yeter"; vaka token'ı yalnız `platform.document-repository.read` taşır — r2, F-6). Bu token'la yükleme 403 döner, satır yoktur, `StoreRepositoryObjectCommand` kaydı yoktur. Test: `DocumentRepositoryUserUploadAuditTests.A_user_with_neither_manage_nor_visit_evidence_write_cannot_upload_and_no_upload_record_is_written`. Sabotaj S-12: `DocumentRepositoryController.Store`'dan `[HasPermission]` kaldırılınca 201 döner, test kırmızı.
- [ ] AC-11 (zorunlu) — FU01 AC-21'in host vakası (K-2): dört alan + dosya + sonra gelen alan, iki fikstür — `four-fields-file-then-unknown-field` (sonra `note=x`) ve `four-fields-file-then-repeated-scope` (sonra `scope=Documents`). Her biri: 400, ret kodu `VALIDATION_FAILED`; `host.Storage.StoreCalls == 1` (K-2'nin "≤ 1"i; bu vakada tam 1 — fikstürün dosyayı tamamen geçtiğinin kanıtı); satır yok; depolama kökünde dosya yok; toplam kayıt 1; kaydın `RequestType`'ı `StoreTrustedRepositoryObjectCommand`, `Outcome = Failed`, `Operation = Create`, `Metadata["ResponseStatusCode"] = 400`, `EntityId` ve `AfterState` boş; `document-repository.trusted-consumer.denied` kaydı 0. Test: `TrustedDocumentRepositoryConsumerUploadTests.A_field_after_a_complete_file_is_400_with_one_AuditBehavior_record_and_nothing_left` (Theory). Sabotaj S-13: sayan akışta yalnız `FormatViolated = true` ataması kaldırılınca (`IOException` kalır) yanıt 503 olur, test kırmızı. (Dal tümüyle kaldırılırsa kopya başarılı olur, 201 ve satır yazılır; test yine kırmızıdır ama başka bir şeyi ölçer — FU01 r9 incelemesi F-3.) Sabotaj S-14: handler'ın `FormatViolated` denetimi kaldırılınca 503, test kırmızı. Sabotaj S-15: sağlayıcının `IOException` dalındaki `TryRemovePartial` kaldırılınca "dosya yok" doğrulaması kırmızı. Sabotaj S-16 (fikstür): fikstür eski biçime (üç alan + dosya + dördüncü alan) çekilince `StoreCalls` 0 olur, test kırmızı.
- [ ] AC-12 (zorunlu) — FU01 r9 incelemesi F-1: `two-files` vakası AC-11'deki yazar, `Outcome`, `Operation`, `ResponseStatusCode`, boş `EntityId` / `AfterState` ve ret yazıcısı 0 doğrulamalarını alır (`A_form_outside_the_contract_is_400_and_leaves_no_row` içinde ya da ayrı test `Two_file_sections_is_400_with_one_AuditBehavior_record`). Sabotaj: S-13 ve S-14 bu vakayı da kırmızı yapar.
- [ ] AC-13 (isteğe bağlı) — Dürüst fikstür adı: mevcut `field-after-file` fikstürü ölçtüğü şeye göre `missing-field-before-file-then-field` diye adlandırılır; beklentisi değişmez (sağlayıcı 0, ret yazıcısı tek kayıt). Sabotaj S-17: yürütücünün dosyadan önceki eksik alan denetimi kaldırılınca bu vaka kırmızı.
- [ ] AC-14 (zorunlu) — Gerileme yok: Platform test projesi filtresiz tam koşuda yeşil, Platform build 0 hata; FU01 AC-21, AC-25, AC-26, AC-33 ve AC-35 testleri yeşil ve kayıt sayıları aynı (tek istekte yükleme başarısı 1 kayıt; iptal satırı FU01 D-2 kuralında); denetim defteri mimari testleri yeşil (yeni yazma komutu yok). Sabotaj: S-1 uygulanınca tam koşu kırmızıdır (en az AC-1); geliştirici kırmızı test adlarını raporlar.
- [ ] AC-15 (zorunlu) — Kapsam ve kapı: değişen dosyalar §5 listesinin alt kümesidir (`git diff --name-only` MVP-1 hattı tabanına karşı); `AuditBehavior.cs` ve `Contracts/Audit/IAuditResponseStateProvider.cs` değişikliği K-7'nin dar izni içindedir; geliştirme kaydı izin kaydının yolunu yazar (`docs/records/decisions/2026-11/mod0262-fu01-mod0181-owner-decisions-2026-11-12.md`, K-7, satır 33). Sabotaj S-18: §5 dışı bir dosya (ör. `ValidationBehavior.cs`) değiştirilince CT'nin dosya listesi karşılaştırması kırmızı.
- [ ] AC-16 (zorunlu) — Aynı korelasyonla yeniden deneme (r2, F-2; §8 madde 5), üretim yazıcısıyla: `TrustedDocumentRepositoryConsumerMongoTests.cs` deseninde (gerçek host + `MongoIntegrationHarness` + `AuditOutboxRepository`) aynı `X-Correlation-Id` ile önce 503 (ör. gövde iptalsiz kesilir — FU01 `A_body_cut_without_cancellation_is_503_with_one_AuditBehavior_record_and_nothing_left` deseni) sonra 201 alan iki yükleme `audit_outbox`'ta tam **2** `StoreTrustedRepositoryObjectCommand` kaydı bırakır: biri `Failed` (`EntityId` boş), biri `Succeeded` (`EntityId = ContentId`), anahtarları farklı. Test: `TrustedDocumentRepositoryConsumerMongoTests.A_retry_with_the_same_correlation_after_a_503_keeps_both_upload_records`. Sabotaj S-19: `AuditBehavior`'daki yanıt durumu birleştirmesi kaldırılınca (S-1 ile aynı değişiklik) başarı kaydının anahtarı başarısız kaydınkine eşit olur, ikinci kayıt `Duplicate` ile düşer, sayı 1, test kırmızı. Not: aynı korelasyonla iki başarısız yükleme 1 kayıt bırakır (bugünkü davranış, §13); bu AC onu değiştirmez ve ölçmesi zorunlu değildir.

---

## 17. Test Expectations

| Katman | Beklenti |
|---|---|
| Unit (`AuditBehaviorResponseStateTests`) | AC-4 (iki vaka), AC-5, AC-6, AC-7 — gerçek `AuditBehavior`, yakalayıcı `IAuditService`, sahte komutlar; kayıt alanları tek tek. |
| Host (gerçek HTTP, gerçek `AuditBehavior` + `AuditService`, sahte yazıcı) | AC-1, AC-2, AC-3, AC-4 (host kısmı), AC-8, AC-10, AC-11, AC-12, AC-13 — `TrustedDocumentRepositoryConsumerHostTests` altyapısı (`Host.UserToken`, `host.Records()`, `host.Storage.StoreCalls`). Sahte yazıcı (`TestAuditWriter`) tekrarsızlık uygulamaz; anahtar etkisi yalnız gerçek yazıcıyla ölçülür. |
| Gerçek Mongo (üretim yazıcısı) | AC-9, AC-16 — `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumerMongoTests.cs` deseni (host + `MongoIntegrationHarness` + `AuditOutboxRepository`). Mongo yoksa test "atlandı" değil, koşulmadı diye raporlanır. |
| Kiracı | AC-8 (güvenilen yol, iki kiracı); AC-2 kayıt kiracısı. |
| Sabotaj | S-1…S-20: her biri geçici uygulanır, kırmızı test adı ve çıkış raporlanır, geri alınır, yeşil yeniden ölçülür. Sabotaj kanıtı olmayan AC kabul edilmez. |
| Build / suite | Platform build 0 hata; Platform test projesi filtresiz tam koşu (AC-14). |
| Kapsam / kapı | AC-15 — CT, `git diff --name-only`'yi §5 listesiyle karşılaştırır ve K-7 izin kaydının yolunu geliştirme kaydında arar. |
| Soft delete | Değişmez; FU01 testleri kapsar. |
| Browser smoke, RESX, verifier | Uygulanmaz (sayfa yok, metin yok). |

---

## 18. Ready-for-dev Checklist

- [x] Golden Reference: uygulanmaz (`golden_reference: none`, sayfa yok) — FU01 §9 / §10 düzeni okundu.
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`).
- [x] Layout & Shell Contract: `shell: none`, layout yok.
- [x] Backend File Convention: yeni komut / handler yok; yeni dosyalar §10.
- [x] Frontend File Contract: yok.
- [x] Validation Rules: §12.
- [x] Failure Path: §13 (bozuk biçim, yetkisiz, kiracı, eşzamanlılık, aynı korelasyonla tekrar).
- [x] Authorization Convention: §14.
- [x] Gateway kararı: gereksiz (§15).
- [x] Lookup kararı: uygulanmaz (lookup yok).
- [x] AC'ler test edilebilir, her birinin adlı sabotajı ya da yapısal notu var (§16).
- [x] Audited Events: iki yükleme komutu (§21).
- [x] Sayfa yardımı / tur: sayfa yok, tur yok.
- [x] r1 bağımsız incelemesi (CT-APR-02): KOŞULLU, `docs/records/reviews/MOD-0262-FU08/2026-11-13-5d3bc4c9.md`; F-1…F-6 r2'de işlendi (§19.1).
- [x] MOD-0021 korunan yol izni: K-7 (Beste, 2026-11-13), karar dosyası satır 33 (§7).
- [x] Dal tabanı: MVP-1 hattı (CT kararı, karar dosyası satır 28; §5).
- [x] `status: ready-for-dev` (S-49).
- [ ] r2'nin CT-APR-02 bağımsız incelemesi (yazandan ayrı oturum) ve `reviews.tsv` satırı.
- [ ] r2'nin CT-APR-01 onay satırı (risk HIGH: denetim davranışı, korunan yol, ürün kodu yolu).
- [ ] `execution/registries/module-id-registry.md`'ye `MOD-0262-FU08` satırı ve FU01 §20 yönlendirmesi — CT, paket onayıyla birlikte (CT kararı, satır 28).

---

## 19. Implementation Notes

- **Kararlar.** Karar dosyası `docs/records/decisions/2026-11/mod0262-fu01-mod0181-owner-decisions-2026-11-12.md`:
  - K-5 (Beste, 2026-11-13): FU01 r9 §20'deki iki adlı FU tek FU olarak açılır; sahip Beste (merkez / Platform); r9 incelemesinin
    F-1'i bu FU'nun beklentisidir (AC-11, AC-12), F-2 (sahip biçimi) bu paketin künyesi ve durum kutusuyla karşılanır.
  - K-6 (Beste, 2026-11-13; satır 23): K-3'ün yolu (b) — `AuditBehavior` başarılı yanıttan `EntityId` / `AfterState` alır; kimlik
    `MOD-0262-FU08`.
  - CT kararları (MVP-1 CT, 2026-11-13; satır 28): arayüz biçimi — komut `IAuditResponseStateProvider<TResponse>` sağlar, yansıma
    yok (§3, §8); sağlayıcı istisnası yutulur, kayıt durumsuz yazılır, uyarı logu, iş sonucu bozulmaz (§8 madde 3, AC-7); tek
    dilim — XMC izni gecikseydi (B) ayrı dilime alınabilirdi, K-7 ile gerek kalmadı (§22); kayıt satırı ve FU01 §20 yönlendirmesi
    paket onayıyla birlikte CT yazar (§18); dal tabanı MVP-1 hattı (§5).
  - K-7 (Beste, 2026-11-13; satır 33): `AuditBehavior.cs` ve `Contracts/Audit/**` korunan yoluna dar ekleme izni (§7).
  - K-2 aynen: dosyadan sonraki bölüm 400, sağlayıcı ≤ 1, satır yok, yarım dosya yok, yazar `AuditBehavior`.
- **Neden isteğe bağlı arayüz.** (b)'nin iki biçimi vardı: `AuditBehavior` yanıtı yansımayla okur ya da komut neyin yazılacağını
  söyler. İkincisi seçildi (CT kararı): alanları komut seçer, yasak alan ihtimali komutun dosyasında ölçülür, öteki Platform
  komutlarının kaydı değişmez (AC-6).
- **Ölçüm (r1: 2026-11-13, HEAD `f827bf948`; r2 eklemeleri HEAD `44e6cda87`).** Okunan kod: `AuditBehavior.cs` (kayıt `:69`–`:81`,
  istisna dalı `:63`–`:66`, metadata `:143`, `EntityId` `:175`, `AfterState` `:179`), `AuditRequestMetadata.cs`, `AuditService.cs`
  (anahtar `:71`–`:79`, `Duplicate` `:109`, `BuildPayload` `AfterState`'i taşır), `AuditIdempotencyKeyBuilder.cs:10` (anahtarda
  `Outcome` yok), iki yükleme komutu (`StoreRepositoryObjectCommand.cs:15`–`:19` — metadata yok; `StoreTrustedRepositoryObjectCommand.cs:20`–`:26`
  — ikisinde de `EntityId` / `AfterState` yok), yürütücü (`:139` ret dalı, `:152` komut gövdesi kiracı bağlaması, `:546` korelasyon),
  `StoreTrustedRepositoryObjectHandler.cs:51`, `TrustedDocumentRepositoryUploadLimits.cs:119`, `LocalFileSystemContentStorageGateway.cs:118`–`:121`,
  `DocumentRepositoryController.cs:59`–`:84`, `PlatformSchemaManifest.AccessGovernance.cs:119` (`audit_outbox` `IdempotencyKey` indeksi),
  `TrustedDocumentRepositoryConsumerUploadTests.cs` (`:54`, `:91`, `:283`, `:291`, `:296`, `:315`, `:318`), `TrustedDocumentRepositoryConsumerHostTests.cs:381`
  ve `:478` (`TestAuditWriter` tekrarsızlık uygulamaz), `TrustedDocumentRepositoryConsumerMongoTests.cs:31`–`:61` (host + harness +
  `AuditOutboxRepository`). DL-1 `StoreRepositoryObjectCommand`'ın denetim kaydını doğrulayan test bugün yok (`grep` 0, test
  projesi). Test, build ve sabotaj koşulmadı; aynı korelasyonla yeniden denemedeki `Duplicate` düşmesi koddan çıkarıldı (AC-16 ölçer).
- **Kimlik ölçümü.** `--discover "document binary store audit entity"` → çıkış 3 ("inceleme gerekli"): blueprint 0, registry 1
  (`MOD-0262-FU01`), paket 1 (FU01 paketi), gateway 2 (`audit`, `document-management` → 5057), görev 1 (PVG-M1-15, MOD-0262
  bağımlılık takibi). Hiçbiri bu FU'nun mükerrer kimliği değil: FU01 ana pakettir, gateway kaynakları var olan Platform rotalarıdır,
  PVG satırı tüketici bağımlılığıdır. `--check-id` FU02, FU07 ve FU08 için çıkış 0 verdi: araç DCP-008'in FU02…FU07
  ayrılmasını görmüyor (yalnız Blueprint ve kayıt satırını okuyor); boş numara bu yüzden DCP-008 tablosu ve depo taramasıyla
  seçildi (FU08).
- **Plan hizalaması.** Plan satırı bu pakete verilmedi (plan dosyası depoda değil): dilim tablosunda `plan satırı yok`. Bu FU yeni plan
  MVP'si ya da yetenek kimliği getirmez; FU01'in AC-21 / AC-25 artığıdır.

### 19.1 r1 → r2 revizyon kapanış tablosu

Kaynak: `docs/records/reviews/MOD-0262-FU08/2026-11-13-5d3bc4c9.md` (KOŞULLU) ve karar dosyası (satır 23, 28, 33).

| Bulgu / r1 sorusu | Karar | Düzeltme | Bölümler |
|---|---|---|---|
| F-1 (ORTA) — paket K-7 ve CT kararlarının gerisinde | K-7, CT kararları | §7 izin satırı ✅ K-7 satır 33; §18 kutular; §19 sorular kararlarla kapandı; §5 dal tabanı MVP-1 hattı; künye, durum kutusu ve kaynak satırı | künye, durum kutusu, §5, §7, §18, §19, AC-15 |
| r1 S-1 (izin) | K-7 | dar ekleme izni | §6, §7 |
| r1 S-2 (arayüz biçimi) | CT | komut sağlar, yansıma yok | §3, §8 |
| r1 S-3 (sağlayıcı istisnası) | CT | yutulur, kayıt durumsuz, uyarı | §8 madde 3, AC-7 |
| r1 S-4 (dilim) | CT | tek dilim | §22 |
| r1 S-5 (kayıt satırı, FU01 §20) | CT | paket onayıyla CT yazar | §18 |
| r1 S-6 (dal tabanı) | CT | MVP-1 hattı | §5 |
| F-2 (ORTA) — tekrarsızlık anahtarı etkisi | — | doğru tanım; AC-16 + S-19; iki başarısız kayıt → 1 notu | §8 madde 5, §13, AC-16 |
| F-3 (ORTA) — AC-9 deseni | — | `TrustedDocumentRepositoryConsumerMongoTests.cs` deseni | AC-9, §5, §17 |
| F-4 (DÜŞÜK) — yürütücü atfı | — | `:152`; S-10 kırmızısının kaynağı | §8, AC-8 |
| F-5 (DÜŞÜK) — DL-1 metadata | — | DL-1 ayrı beklenti | §4, §12, AC-3 |
| F-6 (DÜŞÜK) — AC-4 istisna, AC-10 izinler | — | yapısal not + S-20; iki izin de dışarıda | AC-4, AC-10, §14 |

AC-1…AC-15 numaraları ve anlamları korunur (AC-3, AC-4, AC-8, AC-9, AC-10, AC-15 metni netleşti); yeni AC-16. Yeni sabotajlar S-19, S-20.

---

## 20. Follow-up Items

| Öğe | Hedef |
|---|---|
| Kullanıcı yolu `MultipartReader` geçişi ve kullanıcı yolu içerik okuma denetimi | FU01 K-D'nin ayrı FU'su — sahip Beste; tarihi FU01 §20'de; kimlik DCP-002 ile (bu paket değil) |
| FU01 AC-29 Auth audience | Auth (Ali) — FU01 kabul kapısı, bu FU'nun değil |
| Aynı korelasyonla iki başarısız yüklemenin tek kayıtta kalması | bilinen sınır (§13); anahtara `Outcome` eklemek MOD-0021'in işidir, bu FU'nun değil |
| Başka komutların (`Create…`) aynı arayüzü benimsemesi | her modülün kendi işi; bu FU yalnız iki yükleme komutunu bağlar (K-7 sınırı) |
| FU01 §20 satırlarının bu kimliğe yönlendirilmesi ve kayıt satırı | CT, paket onayıyla birlikte (§18) |

---

## 21. Audited Events

Servisin denetim altyapısı var: `tests/architecture/audit-ledger/Diten.Platform.md` → `## İzler` → `platform-merkezi` (yol `a`).
Bu FU yeni yazma komutu eklemez; sahip olduğu yalnız iki var olan yükleme komutunun kayıt içeriğidir. FU01'in öteki komutları
(telafi) ve okuma / ret / iptal satırları FU01 §21'de kalır, değişmez.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `StoreRepositoryObjectCommand` (FU01 DL-1) | `StoreRepositoryObjectCommand` | `RepositoryObject` | a | Başarıda `EntityId = ContentId`; AfterState: `ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum` (yanıttan, `IAuditResponseStateProvider`). Metadata değişmez (komut metadata vermez). Başarısızda değişmez (durum yok). | — |
| `StoreTrustedRepositoryObjectCommand` (FU01 DL-2) | `StoreTrustedRepositoryObjectCommand` | `RepositoryObject` | a (`ActorType = Service`, FU01 K-B) | Başarıda `EntityId = ContentId`; AfterState: yukarıdaki alanlar + `TrustedConsumerModule`. Metadata anahtar kümesi FU01 §21 ile aynı. Başarısızda (400 / 413 / dosyadan sonraki bölüm) durum yok, `ResponseStatusCode` `AuditBehavior`'dan. Aynı korelasyonla yeniden denemede başarı kaydı ayrı yazılır (§8 madde 5). | — |

Kayda yazılmayan: `ObjectKey`, dosya adı, medya türü, sağlayıcı adı, oluşturan, içerik, token, istemci sırrı. K2 sınıfı değil
(FU01 §21 gerekçesi aynen).

---

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | yükleme denetim kaydının nesnesi (`EntityId` / `AfterState`, yol b) ve dosyadan sonraki bölümün host testi — çekirdek ve tek dilim (CT kararı) | Module Summary, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria, Test Expectations, Audited Events | birim + gerçek HTTP host + gerçek Mongo (üretim yazıcısı) + sabotaj S-1…S-20 + filtresiz tam Platform suite; korunan yol izni K-7 (§7) | plan satırı yok | plan satırı yok |

**AC ↔ dilim:** DL-1 → AC-1…AC-16.
