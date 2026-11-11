# MOD-0189 — bağlı MRP çalışma kanıtı sözleşme taslağı

**Durum:** DRAFT; MOD-0189 üretici, MOD-0188 tarihsel okuma tüketicisi ve Control Tower incelemesi bekliyor. Bu belge Demand v2'yi dondurmaz ve `Superseded` içerik okumasını açmaz.

## Ölçülen başlangıç

`MrpLiveCalculation` bugün `Published` talep revizyonunu, checksum'ı ve durum sürümünü iki kez okuyan, anlık hesap döndüren bir akıştır. `MrpPublishedDemand` aynı Tenant, LegalEntity, revision, durum sürümü ve SKU × depo bilgisini taşır. Kalıcı `runId` kaydı, run–revision bağı veya bu bağı sunucuda doğrulayan okuma ucu mevcut değildir. Bu yüzden mevcut hesap yanıtı, HTTP parametresi veya bir `Published` olayı tarihsel okuma yetkisi veremez.

## Önerilen değişmez bağ kaydı

Alan adları ve kesin şema MOD-0189 üretici kabulüyle sabitlenecek; aşağıdaki alanlar tüketici ihtiyacıdır.

| Alan | Tip | Doğrulama amacı |
|---|---|---|
| `runId` | UUID | Tek, değişmez MRP çalışma kimliği. |
| `tenantId`, `legalEntityId` | UUID | Sunucu kaynaklı kapsam; her sorguda ikisi de zorunlu. |
| `planningCycleId`, `planningPeriodKey` | UUID, string | Bağlandığı çevrim ve dönem; istemcinin seçimi kanıt sayılmaz. |
| `demandRevisionId` | UUID | Tam olarak bir Demand revision'ına değişmez bağ. |
| `demandStateVersion`, `demandChecksum` | integer, SHA-256 | Bağ anında doğrulanan Published durum ve tam snapshot bütünlüğü. |
| `skuId`, `warehouseId` | UUID, string | Run'ın doğrulanmış seri kapsamı; daha geniş tarihsel okuma vermez. |
| `boundAtUtc`, `startedAtUtc` | UTC timestamp | Bağın ve çalışma başlangıcının audit zamanı. |
| `boundBy`, `bindingAuditId` | UUID | Bağı kuran yetkili işlem ve değişmez audit izi. |
| `runState`, `runVersion` | enum, integer | Etkin, askıda, tamamlanmış veya geçersiz run'ın sürümlü durumu. |

Kayıt, yalnız doğrulanmış `Published` snapshot ve sunucuda çözülen Tenant × LegalEntity × seri kapsamından sonra oluşturulmalı. Bağ, ilk run yazısı ve audit aynı Mongo işleminde kalıcılaşmalı; audit yazılamazsa run oluşmamalı. Tekrar istek aynı anahtar/içerikte aynı run'ı vermeli, farklı içerikte çatışmalı. Bağ ve kaynak revision sonradan değiştirilmemeli. Kalıcı run oluşturma ve yeni yazma komutunun audit defteri/izinleri ayrı MOD-0189 uygulama incelemesidir; bu taslak bunları açmaz.

## MOD-0188 tarihsel okuma sınırı

1. Talep içeriğini isteyen aktör için `demand.plans.consume` ve güncel Tenant × LegalEntity × seri kapsamı, Mongo'da revision varlığına bakılmadan önce doğrulanır. Kapsam dışı sonuç boş 404; yetki kaynağı arızası kapalı hatadır.
2. PlanningService, `runId` için MOD-0189'un kalıcı bağını **sunucu tarafında** okur. İstemcinin `revisionId`, cursor, olay veya imzalanmamış alındı beyanı bağı kanıtlamaz. `runId` ile bağlı `demandRevisionId`, çevrim, şirket ve istenen seri eşleşmelidir.
3. Bağ anında kaynak Published olmalı ve checksum/bütünlük doğrulanmalıdır. Yeni run `Superseded` veya `Invalidated` revision'a bağlanamaz. Sonraki `Superseded` yalnız önceden bağlanmış run'ın aynı revision içeriğini tarihsel olarak okumasına adaydır; `Invalidated` içerik okuması ayrı audit yoluyla sınırlı kalır.
4. Her tarihsel sayfa öncesi run bağının hâlâ yetkili olduğu, revision'ın yetkili durumu, sayfa kapsamı ve checksum yeniden doğrulanır. Bağ yoksa, run/revision uyuşmuyorsa veya yetkili durum servisi kesilmişse içerik verilmez. Cursor runId, revisionId, Tenant, LegalEntity, seri ve checksum'a bağlanır; başka run'da kullanılamaz.

MOD-0189 dört kontrol noktasında aynı revision'ın yetkili durumunu okumalı: run kabulü/bağlama, tam snapshot sonrası hesap başlangıcı, sonucu güvenilir yayımlama, öneriyi onaylama veya MOD-0141'e devretme. Durum okunamazsa işlem durur. Kayıp/geç/yinelenmiş olaylarda kalıcı run ve öneriler düzenli yetkili durum uzlaştırmasından geçer; olay tek başına otorite değildir.

## Açık kararlar ve kapı

- MOD-0189 sahibi bağ kaydının alan adlarını, `runState` geçişlerini, sunucu içi okuma arayüzünü, izinleri ve audit kaydını onaylamalı; MOD-0188 sahibi veri minimizasyonu, sayfalama ve yetki eşlemesini kabul etmeli. Pack kabul ölçütü etkisi varsa CT-APR-01/02 uyarınca ayrı pack revizyonu gerekir.
- Uzlaştırma sıklığı, durum kesintisindeki azami bekleme ve geçersiz kılma sonrası güvenilir sonucun görünür kalabileceği süre henüz kararlaştırılmadı; sayı varsayılmayacak.
- Mongo yarış/rollback, yeniden başlatma, kapsam iptali, checksum bozulması, olay kaybı ve runId–revisionId karışması gerçek geçici Mongo ile test edilmeden `Superseded` içerik API'si açılmayacak.
- Bu taslakta kod, API, yeni izin veya Demand v2 freeze yoktur; mevcut MOD-0188 okuma kapalı davranışı korunur.
