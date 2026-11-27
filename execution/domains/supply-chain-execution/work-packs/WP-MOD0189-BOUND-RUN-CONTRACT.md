# MOD-0189 — bağlı MRP çalışma kanıtı sözleşmesi (bound-run)

**Durum:** FROZEN · **Sürüm:** 1.0.0 · **Tarih:** 2026-11-24
**Sahip onayı:** Beste — `docs/records/decisions/2026-11/mod0188-demand-v2-field-names-owner-decisions-01.md` (K-1…K-12; K-3 ve K-7
2026-11-20, diğerleri 2026-11-24).
**Taraflar:** MOD-0189 (bağ kaydının ve run'ın sahibi) · MOD-0188 (talep revizyonunun sahibi, tarihsel okuma sağlayıcısı). Servis:
`Diten.PlanningService` (5068).

**Bu sözleşme neyi dondurmaz:** Demand v2 üretici API'si (`services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml`,
`2.0.0-draft.1`) ve olay şeması DRAFT kalır. Bu belge kod, API ucu, Gateway rotası, runtime izin kaydı ya da `Superseded` içerik okuma
yolu açmaz; bunlar paket revizyonları ve kod dilimleriyle gelir (§11). Dondurulduktan sonra kırıcı değişiklik yasaktır; yalnız ekleme
yeni küçük sürümle yapılır (K16).

## 1. Ölçülen başlangıç

`MrpLiveCalculation` bugün `Published` talep revizyonunu, checksum'ı ve durum sürümünü iki kez okuyan, anlık hesap döndüren bir akıştır. `MrpPublishedDemand` aynı Tenant, LegalEntity, revision, durum sürümü ve SKU × depo bilgisini taşır. Kalıcı `runId` kaydı, run–revision bağı veya bu bağı sunucuda doğrulayan okuma ucu mevcut değildir. Bu yüzden mevcut hesap yanıtı, HTTP parametresi veya bir `Published` olayı tarihsel okuma yetkisi veremez.

## 2. Değişmez bağ kaydı (sahibi MOD-0189)

Standart alanlar (`TenantId`, `LegalEntityId`, `IsDeleted`, `DeletedAt`, oluşturma/güncelleme izi, teknik sürüm) repo standardı gereği
ayrıca vardır. Aşağıdaki iş alanlarının adı ve tipi bu sürümle sabittir.

| Alan | Tip | Zorunlu | Anlam | Karar |
|---|---|---|---|---|
| `runId` | UUID | evet | Tek, değişmez MRP çalışma kimliği | — |
| `tenantId` | UUID | evet | Kiracı; sunucu belirler | — |
| `legalEntityId` | UUID | evet | Şirket; sunucu doğrular | — |
| `planningCycleId` | UUID | evet | Bağlanılan planlama çevrimi | — |
| `planningPeriodKey` | string, `format: date` (`yyyy-MM-dd`) | evet | Dönemin ilk hafta başlangıcı (üretici biçimiyle aynı) | — |
| `demandRevisionId` | UUID | evet | Tam olarak bir talep revizyonuna değişmez bağ (üreticide adı `revisionId`) | — |
| `demandStateVersion` | integer (64 bit) | evet | Bağ anında okunan yetkili durum sürümü (üreticide `stateVersion`) | — |
| `demandContentVersion` | integer | evet | Bağlanan revizyonun değişmez içerik sürümü | K-2 |
| `demandIntegrity` | nesne `{ definitionId, digest }` | evet | Bağ anında doğrulanan tam anlık görüntü özeti; `definitionId` ∈ `MOD0188-CANONICAL-1/SHA-256`, `MOD0188-CANONICAL-2/SHA-256`; `digest` 64 büyük harf onaltılık | K-1 |
| `demandExpectedRowCount` | integer | evet | Bağlanan revizyonun beklenen satır sayısı | K-2 |
| `skuId` | UUID | evet | Run'ın seri kapsamı: ürün | — |
| `warehouseId` | **UUID** | evet | Run'ın seri kapsamı: depo | K-3 |
| `baseUomId` | string | evet | Serinin temel birimi | — |
| `boundAt` | date-time (UTC) | evet | Bağın kurulduğu an | K-4 |
| `startedAt` | date-time (UTC) | evet | Hesabın başladığı an | K-4 |
| `boundBy` | UUID | evet | Bağı kuran kullanıcının kimliği; ad / e-posta yok | K-5 |
| `bindingAuditId` | UUID | evet | Bağın değişmez denetim kaydı | K-5 |
| `runState` | enum (§3) | evet | Run'ın durumu | K-6 |
| `runVersion` | integer | evet | Run durum sürümü; her geçişte +1. `demandStateVersion` ile karıştırılmaz | K-6 |
| `idempotencyKey` | string | evet | Aynı isteğin tekrarını tanıma | K-11 |
| `requestHash` | string (SHA-256, onaltılık) | evet | Aynı anahtar + farklı içerik çatışmasını ayırt etme | K-11 |
| `stopReason` | enum (§3) | `Stopped` durumunda | Run neden durdu | K-6 |

**`warehouseId` sınır kuralı (K-3):** Üretici depo kimliğini metin taşır. Bağ kurulurken ve her tarihsel sayfada üreticinin değeri UUID
olarak çözülür; çözülemeyen değer kapalı hatadır (bağ kurulmaz, sayfa verilmez). Eşleştirme UUID değeriyle yapılır, metin
karşılaştırmasıyla değil. `demandIntegrity.digest` doğrulanırken depo kimliği yeniden biçimlendirilmez; özet üreticinin kanonik tanımı ve
yazdığı baytlarla hesaplanır.

**Kurulma kuralı:** Kayıt yalnız doğrulanmış `Published` anlık görüntü ve sunucuda çözülen Tenant × LegalEntity × seri kapsamından
sonra oluşur. Bağ, ilk run yazısı ve denetim kaydı aynı Mongo işleminde kalıcılaşır; denetim yazılamazsa run oluşmaz. Bağ alanları ve
kaynak revizyon sonradan değiştirilmez.

Bağ kaydı dışında kalan run kanıt alanları (`inventoryAsOf`, `leadTimeRevisionId`, `leadTimeDays`, `safetyPolicyRevisionId`,
`bomVersionId`, `sourceVersion`, `evidenceChecksum`, `proposalId`) MOD-0189 run/öneri şemasınındır; bu sözleşmenin konusu değildir.

## 3. Run durumları (`runState`, K-6)

| Durum | Anlam |
|---|---|
| `Running` | Bağ kuruldu, hesap sürüyor |
| `Suspended` | Yetkili talep durumu okunamadı; bekliyor |
| `Completed` | Sonuç güvenilir olarak yayımlandı (kontrol noktası 3) |
| `Stopped` | Bitmeden durduruldu |
| `ResultUntrusted` | Tamamlanmış run'ın revizyonu sonradan `Invalidated` oldu; sonuç güvenilmez, uygulanmamış öneriler bloklu |

Geçişler:

| Geçiş | Tetik | İzin |
|---|---|---|
| `Running → Completed` | Kontrol noktası 3 geçti (§8) | sistem |
| `Running → Suspended` | Durum servisi / yetki kaynağı okunamadı | sistem |
| `Suspended → Running` | Durum yeniden okundu; aynı `demandRevisionId`, uygun `demandStateVersion`, aynı özet | sistem |
| `Running` / `Suspended → Stopped` | `stopReason` = `DemandInvalidated`, `ScopeRevoked`, `IntegrityFailed` | sistem |
| `Running` / `Suspended → Stopped` | `stopReason` = `Cancelled` (kullanıcı iptali, gerekçe zorunlu) | `mrp.runs.cancel` |
| `Completed → ResultUntrusted` | Sonraki uzlaştırmada revizyon `Invalidated` bulundu | sistem |

- Uç durumlar: `Stopped`, `ResultUntrusted`.
- Hiçbir geçiş `demandRevisionId`'yi veya bağ alanlarını değiştirmez. Her geçiş `runVersion`'ı bir artırır ve denetim kaydı yazar (§5).
- Revizyonun `Superseded` olması run'ı durdurmaz; bağlı run aynı revizyonu tarihsel olarak okuyabilir (§7).

## 4. İzinler (K-8)

| Anahtar | Kullanım | Durum |
|---|---|---|
| `mrp.runs.create` | Kalıcı run + bağ oluşturma | yeni (runtime kaydı MOD-0189 kod dilimindedir) |
| `mrp.runs.cancel` | Kullanıcının run'ı durdurması (`Cancelled`) | yeni (runtime kaydı MOD-0189 kod dilimindedir) |
| `mrp.runs.read` | Run ve bağ görüntüleme; açıklama "View MRP runs" anlamına düzeltilir | kayıtlı (bugünkü açıklama "Calculate scoped MRP plan") |
| `demand.plans.consume` | Talep içeriği okuma; `Superseded` tarihsel okuma yalnız bu izin + sunucunun okuduğu kalıcı bağ ile | kayıtlı |

Tarihsel okuma için yeni izin yoktur. Sistem geçişleri izin istemez. `mrp.proposals.read|approve` ve `mrp.evidence.read` bu sözleşmenin
konusu değildir. Her eylem `[HasPermission]` ister; yetkisiz kullanıcı 403, kapsam dışı kayıt boş 404.

## 5. Denetim kaydı

Bağ, ilk run yazısı ve denetim aynı Mongo işlemindedir; denetim yazılamazsa run yoktur. Bugün yerel `c` yolu kullanılır; merkezi denetim
BL-501 bekler, geldiğinde yerel iz kaldırılmaz.

Eylem adları (K-6 geçişlerinden): `RunCreated`, `RunSuspended`, `RunResumed`, `RunCompleted`, `RunStopped`, `RunResultUntrusted`.

| Alan | Tip | Not |
|---|---|---|
| `auditId` | UUID | Bağ kaydı için `= bindingAuditId` |
| `action` | enum | Yukarıdaki altı ad |
| `tenantId`, `legalEntityId` | UUID | |
| `actorId` | UUID | Kullanıcı ya da sistem geçişlerinde servis kimliği; ad yazılmaz |
| `occurredAt` | date-time (UTC) | |
| `runId`, `runVersionBefore`, `runVersionAfter` | UUID, integer, integer | |
| `demandRevisionId`, `demandStateVersion`, `demandIntegrity.digest` | UUID, integer, string | Bağın kanıtı |
| `skuId`, `warehouseId` | UUID, UUID | Kapsam |
| `stopReason` / `reason` | enum / string | Durdurma nedeni / kullanıcı iptal gerekçesi |
| `correlationId` | string | OBS-001 |

Talep satırları, miktarlar ve kişi adları denetim kaydına yazılmaz. (Alan tablosu öneri listesinin §4'ünden alındı; numaralı bir K
değildir — açık nokta A-5.)

## 6. Tekrar istek (idempotency, K-11)

- Aynı Tenant × LegalEntity içinde aynı `idempotencyKey` + aynı `requestHash` → ilk run aynen döner; ikinci run, ikinci bağ ya da ikinci
  denetim kaydı oluşmaz.
- Aynı `idempotencyKey` + farklı `requestHash` → çatışma; yazım yok.
- `requestHash`'e giren alan kümesi teknik tasarımda yazılır (açık nokta A-4); sözleşme adı, tipi ve davranışı sabitler.

## 7. MOD-0188 tarihsel okuma sınırı

1. Talep içeriğini isteyen aktör için `demand.plans.consume` ve güncel Tenant × LegalEntity × seri kapsamı, Mongo'da revision varlığına bakılmadan önce doğrulanır. Kapsam dışı sonuç boş 404; yetki kaynağı arızası kapalı hatadır.
2. PlanningService, `runId` için MOD-0189'un kalıcı bağını **sunucu tarafında** okur (K-7: servis içi arayüz; bunun için HTTP ucu ve Gateway rotası açılmaz). İstemcinin `revisionId`, cursor, olay veya imzalanmamış alındı beyanı bağı kanıtlamaz. `runId` ile bağlı `demandRevisionId`, çevrim, şirket ve istenen seri eşleşmelidir.
3. Bağ anında kaynak `Published` olmalı ve `demandIntegrity` doğrulanmalıdır. Yeni run `Superseded` veya `Invalidated` revision'a bağlanamaz. Sonraki `Superseded` yalnız önceden bağlanmış run'ın aynı revision içeriğini tarihsel olarak okumasına izin verir; `Invalidated` içerik okuması ayrı audit yoluyla sınırlı kalır.
4. Tarihsel okuma yalnız run'ın kendi serisini (`skuId` × `warehouseId`) verir; daha geniş okuma vermez. Durum okuması satır taşımaz.
5. **Sayfalama (K-10):** satır sayfası en çok 500, varsayılan 200; denetim sayfası en çok 200, varsayılan 100; imzalı cursor ömrü 30 dk. Cursor `runId`, `demandRevisionId`, Tenant, LegalEntity, seri, `demandIntegrity`, içerik/durum sürümü ve sayfa boyutuna bağlanır; başka run'da kullanılamaz.
6. Her tarihsel sayfa öncesi run bağının hâlâ yetkili olduğu, revision'ın yetkili durumu, sayfa kapsamı ve özet yeniden doğrulanır. Bağ yoksa, run/revision uyuşmuyorsa veya yetkili durum servisi kesilmişse içerik verilmez. Satır sayısı `demandExpectedRowCount` ile tutmazsa kısmi sayfa verilmez.
7. Kişi verisi yalnız kullanıcı kimliğidir (UUID); ad / e-posta yok.

## 8. Kontrol noktaları ve uzlaştırma

MOD-0189 dört kontrol noktasında aynı revision'ın yetkili durumunu okur: (1) run kabulü/bağlama, (2) tam anlık görüntü sonrası hesap
başlangıcı, (3) sonucu güvenilir yayımlama, (4) öneriyi onaylama veya MOD-0141'e devretme. Durum okunamazsa işlem durur (`Suspended`).
Kayıp/geç/yinelenmiş olaylarda kalıcı run ve öneriler düzenli yetkili durum uzlaştırmasından geçer; olay tek başına otorite değildir.

## 9. Olaylar

- Bu sözleşme MOD-0189 için yeni bir entegrasyon olayı tanımlamaz; run geçişleri §5 denetim kaydıyla izlenir.
- MOD-0188'in yayın / geçersiz kılma olayları yalnız kontrol işaretidir; karar her zaman yetkili durum sorgusundan alınır.
- **Neden sözlüğü (K-9):** geçersiz kılma nedeni API'de ve olayda tek sözlüktür — `IdentityOrScope`, `CalendarTimeUnitOrQuantity`,
  `SourceMethodOrPolicy`, `ContentIntegrity` (API'deki `impactCode` adları). Olaydaki `reasonCategory` bu adlara çekilir;
  `businessImpact` olaydan çıkarılır, `evidenceReference` yeterlidir. MOD-0189 bu alanlarla karar vermez; nedeni yalnız denetim ve
  gösterim için taşır.

## 10. Açık noktalar (dondurmayı engellemez)

| # | Konu | Neden açık | Sahip / yer |
|---|---|---|---|
| A-1 | Uzlaştırma sıklığı | K-12: sayı teknik tasarımda ölçülerek gelir; varsayılmaz | MOD-0189 teknik tasarım |
| A-2 | Durum kesintisinde azami bekleme (`Suspended` süresi) | K-12 | MOD-0189 teknik tasarım |
| A-3 | `ResultUntrusted` sonucun görünür kalma süresi | K-12 | MOD-0189 teknik tasarım |
| A-4 | `requestHash`'e giren alan kümesi ve kanonik biçimi | Öneri yalnız adı ve davranışı sabitledi | MOD-0189 teknik tasarım |
| A-5 | §5 denetim alan tablosunun sahip teyidi | Öneri §4'ten alındı; numaralı K değil | Beste / MOD-0189 paket revizyonu |
| A-6 | Üretici taslağında `warehouseId` için `format: uuid` | K-3 tüketici tarafını belirledi; üretici tarafı kararlaştırılmadı. Mevcut üretici verisinin tamamının geçerli UUID olduğu ölçülmedi | Beste / MOD-0188 |
| A-7 | K-9 olay şeması ve outbox çevirisi uyumu | `demand-v2.events.schema.json` ve `DemandRevisionOutboxPayload.cs` bugün eski adları taşıyor | MOD-0188 üretici işi; Demand v2 dondurmasından önce |
| A-8 | Gerçek geçici Mongo testleri | Mongo yarış/rollback, yeniden başlatma, kapsam iptali, özet bozulması, olay kaybı ve runId–revisionId karışması test edilmeden `Superseded` içerik okuması açılmaz | MOD-0188 / MOD-0189 kod dilimleri |

## 11. Paket revizyonları (bu sözleşmeye atıf yapacak)

- **MOD-0189:** §4 aday alan listesine bu sözleşmenin bağ kaydı (`warehouseId` UUID, `demandIntegrity`, `demandContentVersion`,
  `demandExpectedRowCount`, `boundAt`/`startedAt`, `boundBy`, `bindingAuditId`, `runState`/`runVersion`, `requestHash`, `stopReason`);
  §12 "Demand revision" satırı `revisionId`/checksum yerine bağ alanları; §13'teki açık "aralık ve askı durumu" A-1/A-2'ye bağlanır;
  §14 izinler (`mrp.runs.create`, `mrp.runs.cancel` yeni; "kayıtlı runtime izinleri değildir" cümlesi `mrp.runs.read` için düzeltilir ve
  açıklaması "View MRP runs"); §15 bağ okuma servis içi, Gateway rotası yok (K-7); §16/§18 Demand v2 tüketici sınırı maddesi bu sözleşmeye bağlanır; §20 seçenek tablosu; §21 denetim eylem adları.
- **MOD-0188:** §14 `demand.plans.consume` satırı ve §17 "MOD-0189 tüketici sözleşmesi" paragrafı bu sözleşmeye atıf yapar; §15
  olay paragrafına K-9 neden sözlüğü ve `businessImpact` çıkarımı; §15 sayfa satırına K-10 sınırları + cursor'un `runId`'ye bağlanması;
  K-7 servis içi bağ okuma (Gateway yok); A-6 kararı.
- **MOD-0190:** §22 uptake kapısının "DEMAND v1.1 plan ID, version, checksum" ölçütü ve "MOD-0188 reserved / planned, no pack"
  maddesi güncellenir; Demand v2 kimlik/özet adları (`revisionId`, `stateVersion`, `IntegrityProof`) için bu sözleşmeye atıf.
  Bağlı tarihsel okuma yalnız MOD-0189 run'larınındır; MOD-0190'a `Superseded` okuma vermez.

## Değişiklik günlüğü

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| DRAFT | — | Tüketici ihtiyacı taslağı (alan adları sabit değil). |
| 1.0.0 | 2026-11-24 | FROZEN. Sahip kararları K-1…K-12 uygulandı (`docs/records/decisions/2026-11/mod0188-demand-v2-field-names-owner-decisions-01.md`): `demandChecksum` → `demandIntegrity {definitionId, digest}`; `demandContentVersion`, `demandExpectedRowCount`, `baseUomId`, `idempotencyKey`, `requestHash`, `stopReason` eklendi; `warehouseId` UUID; `boundAtUtc`/`startedAtUtc` → `boundAt`/`startedAt`; `runState` değerleri ve geçişleri; izinler; denetim eylem adları; neden sözlüğü; sayfalama; açık noktalar A-1…A-8. |
