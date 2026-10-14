# MVP-4 bilgi notu — BOM-version olayı (MOD-0193 → MOD-0189 MRP)

Tarih: 2026-10-13 · Kaynak: ürün sahibi kararı (Ali), "BOM-version olayı KAPSAMDA" · Üretici: `Diten.ManufacturingService` (5070)
· Sözleşme: `docs/analysis/contracts/bom.openapi.yaml` **v1.2.0** — `webhooks.bomVersionLifecycleEvent`, şema `BomVersionEvent`.

Bu not MVP-4'e (MOD-0189 MRP, pack henüz taslak) **bilgi** içindir; MVP-4 dosyalarına dokunulmadı.

## Olaylar

| olay adı (taşıma zarfı `eventName`) | `eventType` | ne zaman |
|---|---|---|
| `manufacturing.bomversion.effective.v1` | `BomVersionEffective` | bir taslak yürürlüğe alındığında (release) |
| `manufacturing.bomversion.superseded.v1` | `BomVersionSuperseded` | aynı item'ın o ana kadar yürürlükte olan sürümü için, aynı release'te |

Bir release en fazla iki olay üretir; ikisi aynı `correlationId`'yi taşır. Taslak oluşturma / düzenleme / silme olay üretmez.

## Gövde (`BomVersionEvent`, camelCase, hepsi zorunlu)

`eventId` · `eventType` · `occurredAt` · `correlationId` · `tenantId` · `legalEntityId` (şirket) · `itemId` · `bomVersionId`
· `version` (item başına revizyon no) · `status` (`Effective` | `Superseded`) · `effectiveFrom` · `contractVersion` (`v1`)

**Satır içeriği (bileşen, miktar, birim, rota) olayda YOKTUR.** Tüketici yapıyı `GET /api/bom/{itemId}/current` ya da
`POST /api/bom/explode` ile okur (aynı tenant + `X-Legal-Entity-Id`).

## Teslim

- Outbox: olay kaydı BOM değişikliğiyle **aynı Mongo işleminde** yazılır (`mfg_bom_outbox`); değişiklik yazılmazsa olay yok.
- Taşıma: paylaşılan `Diten.BuildingBlocks.Eventing.EventTransportMessage`, MassTransit + RabbitMQ; exchange
  `Diten.BuildingBlocks.Eventing:EventTransportMessage` (fanout). Gövde `canonicalPayloadUtf8` alanındadır.
- **En az bir kez** teslim; tüketici `eventId` ile tekilleştirmelidir. Sıra garantisi yok; `occurredAt` + `version` ile sırala.
- Broker taşıması ortam ayarıyla açılır (`EventTransport:Enabled`). Kapalıyken olaylar outbox'ta `Pending` bekler.

## MVP-4 için açık sorular (karar MVP-4'te)

1. Tüketici exchange'e kendi kuyruğunu bağlar; kuyruk adı ve dayanıklılık MVP-4'ün kararıdır.
2. Aynı `eventName`'leri başka modüller de aynı exchange'e yayınlar (paylaşılan mesaj tipi); tüketici `eventName` ile süzmelidir.
