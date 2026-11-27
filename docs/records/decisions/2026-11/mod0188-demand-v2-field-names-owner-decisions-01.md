# MOD-0188 Demand v2 / MOD-0189 bağlı koşu — alan adları sahip kararları 01 (2026-11-24)

**Konu:** MOD-0188 (talep, üretici) ile MOD-0189 (MRP, tüketici) arasındaki bağlı koşu (bound-run) sözleşmesinin alan adları,
tipleri, durumları, izinleri, olay adları, sayfalama ve tekrar (idempotency) kuralları.
**Sahip:** Beste (merkez). **Kayıt:** MVP-1 / WMS CT oturumu `ca7382cf`.

## Kaynak

- Öneri: CT alan adı öneri listesi "Demand v2 / MOD-0189 bağlı koşu — alan adı öneri listesi (CT, 2026-11-20)", K-1…K-12 (§7).
  Dosya oturum karalama alanındadır (repo dışı, kalıcı değil):
  `C:\Users\Ali\AppData\Local\Temp\claude\0884561ec2a2c211\ca7382cf-4f88-49e8-bdb1-901cd42e3083\scratchpad\demand-v2\alan-onerisi.md`.
  Bu kayıt önerinin karar içeriğini aşağıya taşır; karalama dosyası silinse de karar buradan okunur.
- Önerinin okuduğu kaynaklar: `execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md` (DRAFT),
  `WP-MOD0188-V2-READ-API.md`, `WP-MOD0188-CONTRACT-NEEDS.md`,
  `services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml` (`2.0.0-draft.1`), `demand-v2.events.schema.json`,
  MOD-0188 ve MOD-0189 paketleri, PlanningService MRP / DemandPlanning kodu ve `permission-key-standard`.
- Sahip cevapları (sohbet):
  - **2026-11-20:** K-3 = `warehouseId` **GUID** (metin değil; MRP kodu metne geçmek zorunda kalmasın). K-7 = kabul (bağ okuma
    yalnız servis içinde; yeni API ucu ya da Gateway rotası yok).
  - **2026-11-24:** K-1, K-2, K-4, K-5, K-6, K-8, K-9, K-10, K-11, K-12 = **önerildiği gibi**.

## Kararlar ve sonuçları

| # | Karar | Sonuç |
|---|---|---|
| K-1 | Bağ kaydında `demandChecksum` yerine `demandIntegrity { definitionId, digest }` (üreticinin `IntegrityProof` şekli). | Bağ, hangi kanonik tanımla (`MOD0188-CANONICAL-1/SHA-256` ya da `-2`) doğrulanacağını taşır. `MrpPublishedDemand.Checksum` (yalnız metin) koddaki karşılığıdır; tanım kimliği eklenmesi MOD-0189 kod dilimindedir. |
| K-2 | Bağ kaydına `demandContentVersion` ve `demandExpectedRowCount` eklenir (ikisi de zorunlu). | Özetin hangi içerik sürümüne ait olduğu ve tarihsel okumada tam anlık görüntü kontrolü bağ kaydından yapılır. |
| K-3 | `warehouseId` sözleşmede **UUID (GUID)**. | **Öneriden sapma** (aşağıda). MRP kodu (`MrpLiveSources.cs`, `MrpLiveCalculation.cs`) `Guid` olarak kalır; sınırda üreticinin metin değeri UUID olarak çözülür, çözülemezse kapalı hata. |
| K-4 | Zaman alanları `Utc` eki olmadan: `boundAt`, `startedAt`; değer her zaman UTC. | Üretici adlandırmasıyla (`publishedAt`, `occurredAt`) aynı. |
| K-5 | `boundBy` = bağı kuran kullanıcının kimliği (UUID); işlem izi `bindingAuditId`. | Bağ kaydında ad ya da e-posta yok. |
| K-6 | `runState` = `Running`, `Suspended`, `Completed`, `Stopped`, `ResultUntrusted`; geçişler önerideki gibi; `stopReason` = `DemandInvalidated`, `ScopeRevoked`, `IntegrityFailed`, `Cancelled`. | Uç durumlar `Stopped`, `ResultUntrusted`. Her geçiş `runVersion`'ı artırır ve denetim kaydı yazar; hiçbir geçiş bağ alanlarını değiştirmez. Denetim eylem adları geçişlerden çıkar: `RunCreated`, `RunSuspended`, `RunResumed`, `RunCompleted`, `RunStopped`, `RunResultUntrusted`. |
| K-7 | Bağ okuma arayüzü PlanningService içinde (sunucu içi); HTTP ucu ve Gateway rotası açılmaz. | `ocelot.json` ve Gateway testleri değişmez; integration-agent işi doğmaz. |
| K-8 | Yeni izinler yalnız `mrp.runs.create` ve `mrp.runs.cancel`; tarihsel okuma `demand.plans.consume` + kalıcı bağ ile; `mrp.runs.read` açıklaması düzeltilir. | `Suspended` / `Stopped` / `ResultUntrusted` sistem geçişleri izin istemez. `mrp.runs.read` açıklaması bugün "Calculate scoped MRP plan" (`MrpManifestProvider.cs:24`); "View MRP runs" anlamına düzeltilmesi MOD-0189 kod dilimindedir. |
| K-9 | Olaydaki `reasonCategory` adları API'deki `impactCode` adlarıyla aynı yapılır; `businessImpact` olaydan çıkarılır (`evidenceReference` yeterli). | Tek sözlük: `IdentityOrScope`, `CalendarTimeUnitOrQuantity`, `SourceMethodOrPolicy`, `ContentIntegrity`. `demand-v2.events.schema.json` taslağının ve `DemandRevisionOutboxPayload.cs` çevirisinin uyumlanması MOD-0188 üretici işidir; Demand v2 üretici dondurmasından önce yapılır. |
| K-10 | Tarihsel okuma sayfası üretici sınırlarıyla aynı: satır sayfası en çok 500 / varsayılan 200, denetim sayfası en çok 200 / varsayılan 100, imzalı cursor ömrü 30 dk; cursor ayrıca `runId`'ye bağlı. | Başka run'ın cursor'u kullanılamaz. |
| K-11 | Bağ kaydında `idempotencyKey` + `requestHash`. | Aynı anahtar + aynı içerik aynı run'ı döndürür; aynı anahtar + farklı içerik çatışmadır. |
| K-12 | Uzlaştırma sıklığı, durum kesintisinde azami bekleme ve güvenilmez sonucun görünür kalma süresi alan adlarını engellemez; sayılar teknik tasarımda ölçülerek gelir. | Bu üç değer dondurulan sözleşmede açık nokta olarak kalır; sayı varsayılmaz. |

### K-3 sapmasının açıklaması

Öneri `warehouseId`'yi **metin** önermişti: Demand v2 üretici taslağı ve kodu depo kimliğini metin taşıyor
(`demand-v2.openapi.yaml` :602, :853; `InternalSnapshotReadContracts.cs` :47 `string WarehouseId`), DRAFT sözleşme de "string"
diyordu. MRP kodu ise `Guid` kullanıyor (`MrpLiveSources.cs`, `MrpLiveCalculation.cs`). Sahip, MRP kodu metne geçmek zorunda
kalmasın diye bağ sözleşmesinde **GUID**'i seçti. Sonuçları:

- Bağ kaydı, denetim kaydı ve tarihsel okuma kapsamı `warehouseId`'yi UUID olarak tutar.
- Sınırda üreticinin metin değeri UUID olarak çözülür; çözülemeyen değer kapalı hatadır (bağ kurulmaz, sayfa verilmez).
  Eşleştirme metin karşılaştırmasıyla değil UUID değeriyle yapılır.
- Bütünlük özeti (`demandIntegrity.digest`) üreticinin kanonik tanımına göre üreticinin yazdığı metin baytlarıyla hesaplanır;
  tüketici özet doğrularken depo kimliğini yeniden biçimlendirmez.
- Üretici taslağının (`demand-v2.openapi.yaml`, olay şeması) `warehouseId`'yi `format: uuid` yapıp yapmayacağı bu kararla
  belirlenmedi; açık noktadır (sahip: Beste / MOD-0188).

## Hangi belgede ne değişir

| Belge | Değişiklik | Durum |
|---|---|---|
| `execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md` | K-1…K-12 uygulanır; DRAFT → **FROZEN 1.0.0** (2026-11-24), bu kayda atıf, değişiklik günlüğü. | Bu turda yapıldı. |
| `docs/analysis/contracts/README.md` | Sözleşme tablosuna DEMAND-V2 BOUND-RUN satırı. | Bu turda yapıldı. |
| `services/Diten.PlanningService/contracts/drafts/demand-v2.events.schema.json` (+ outbox çevirisi) | K-9: `reasonCategory` sözlüğü `impactCode` ile aynı; `businessImpact` çıkar. | MOD-0188 üretici işi; yapılmadı. |
| MOD-0188, MOD-0189, MOD-0190 paketleri | Dondurulan sözleşmeye atıf ve aşağıdaki maddeler (sözleşme belgesi §11). | Paket revizyonları ayrıca yazılır ve incelenir (CT-APR-01/02). |

## Sınır

- Bu kayıt **kod izni değildir**. Kod, commit, push, PR ya da dağıtım izni vermez. Yeni izin anahtarlarının runtime kaydı, kalıcı run
  yazımı ve `Superseded` içerik okuma yolu MOD-0189 / MOD-0188 kod dilimlerinde, pack onayından sonra açılır.
- Demand v2 üretici sözleşmesi (`demand-v2.openapi.yaml`, `2.0.0-draft.1`) bu kararla dondurulmaz; DRAFT kalır.
- **MOD-0188, MOD-0189 ve MOD-0190 paket revizyonları bunu izler**; o revizyonlar onaylanmadan bu sözleşme hiçbir kabul ölçütünü kapatmaz.
- `.antigravity/**` ve `ocelot.json` değişmedi.
