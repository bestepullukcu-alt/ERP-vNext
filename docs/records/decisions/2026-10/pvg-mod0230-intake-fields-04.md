---
decision_id: PVG-MOD0230-INTAKE-FIELDS-04
status: approved — sahip Control Tower'ın iki kuralını kabul etti (2026-10-22, sohbette "revizyon-06"); QPPV'ye bilgi olarak gider, kural bugünden uygulanır
prepared_at_local: 2026-10-22
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
supplements: docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md (AL-6) ve pvg-mod0230-intake-fields-03.md (KR-2); o kayıtlar değiştirilmez — docs-organization K4
trigger: bağımsız inceleme 05 F-1 (ORTA; `docs/records/reviews/MOD-0230/2026-10-22-94cbb2d5.md`) ve Control Tower karşılaştırma notu 05 §1 eki, §3
scope: yalnız MOD-0230 bildiren bağı; operasyonel runtime kapısı kapalı kalır
---

# MOD-0230 — bağ kaldırma ile tek seferlik CRM aramasının önceliği (KR-4)

> İnceleme 05, paketin iki kuralının sırasını söylemediğini buldu: KR-2'ye göre kullanıcı bağı açık bir işaretle
> kaldırır ve bildiren elle girilmiş sayılır; AL-6 açıklamasına göre elle girilmiş bildiren kayıt tamamlanınca CRM'de
> bir kez aranır ve eşleşirse bağlanır. Control Tower ayrıca sorunun yalnız aynı kaydetmede değil sonraki kaydetmelerde de
> çıkabildiğini ölçtü (karşılaştırma notu 05 §1 eki). Bu kayıt iki kuralın birlikte nasıl işleyeceğini bağlar.

## Kararlar

| # | Konu | Karar (sahip, 2026-10-22) | Ayrıca gereken onay |
|---|---|---|---|
| KR-4 (a) | Aynı kaydetmede öncelik | Bağ kaldırma işaretli kaydetmede (`RemoveReporterLink = true`) tek seferlik CRM araması çalışmaz. Sonuç `ReporterSource = Manual`, boş `ReporterSourceId` ve korunan anlık kopyadır; CRM kesin eşleşme döndürse bile değişmez. | yok |
| KR-4 (b) | Sonraki kaydetmelerde kalıcılık | Kullanıcının bağını kaldırdığı bir kayıtta tek seferlik CRM araması sonraki hiçbir kaydetmede çalışmaz. Bağ yalnız kullanıcının açık seçimiyle (seçim çifti) yeniden kurulur. Gerekçe: KR-2'nin "sessiz bağ değişikliği yok" ilkesi; açık bir kullanıcı eylemi sistem tarafından geri alınmaz. | QPPV — bildiren bağı MOD-0231 mükerrer kontrolünün dayanağıdır (AL-7); bilgi |

## Pakete etkisi

Paket revizyonu 06: tek seferlik CRM araması bölümü ve "Reporter selection contract" KR-4'ü yazar; kaydın bağının
kullanıcı tarafından kaldırıldığını sunucunun nasıl bildiği (saklanan bir durum) paket içinde tanımlanır; BG-29 / BG-30
bu iki durumu ölçen kabul testlerini alır (CRM araması MOD-0150 sözleşmesinden sonra çalıştığı için bu testler de o
sözleşmeden sonra). Bu hizalamayı `module-pack-author` yapar; Control Tower denetler.

## Yerini alan / yerine geçen

- Yok. Bu kayıt intake-fields-01 ve -03'ü değiştirmez; yalnız iki kuralın sırasını bağlar.
