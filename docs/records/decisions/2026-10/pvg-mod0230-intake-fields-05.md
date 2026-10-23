---
decision_id: PVG-MOD0230-INTAKE-FIELDS-05
status: approved — sahip Control Tower'ın A seçeneğini kabul etti (2026-10-22, sohbette "karar-a"); QPPV'ye bilgi olarak gider, kural bugünden uygulanır
prepared_at_local: 2026-10-22
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
supplements: docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md (AL-6) ve pvg-mod0230-intake-fields-04.md (KR-4); o kayıtlar değiştirilmez — docs-organization K4
trigger: bağımsız inceleme 06 F-1 (ORTA; `docs/records/reviews/MOD-0230/2026-10-22-31f02845.md`) ve Control Tower karşılaştırma notu 06 §3
scope: yalnız MOD-0230 tek seferlik CRM aramasının tetikleyicisi; operasyonel runtime kapısı kapalı kalır
---

# MOD-0230 — tek seferlik CRM aramasının tetikleyicisi (KR-5)

> AL-6 açıklaması elle girilen bildirenin "kayıt tamamlanınca" CRM'de bir kez aranacağını söyler, ama "tamamlanmış kayıt"ı
> tanımlamaz. İnceleme 06, bildiren bilgisi olmadan açılan bir vakanın (EK-3) aramayı ilk kaydetmede harcayabileceğini ve
> bildiren sonradan girildiğinde otomatik eşleşmenin olmayacağını buldu. Bu kayıt tetikleyiciyi bağlar.

## Karar

| # | Konu | Karar (sahip, 2026-10-22) | Ayrıca gereken onay |
|---|---|---|---|
| KR-5 | "Kayıt tamamlanınca" ne demek | Tek seferlik CRM araması, bildireni elle girilmiş (`ReporterSource = Manual`) ve henüz aranmamış (`ReporterCrmSearchState = Pending`) bir kayıtta, kaydetmeden sonra `ReporterNameOrInitials` alanının **dolu** olduğu (kırpıldıktan sonra boş değil) ve aramanın **yapılabildiği** (MOD-0150 okuma sözleşmesi var) **ilk** kaydetmede çalışır (oluşturma ya da güncelleme). O ana kadar arama durumu `Pending` kalır ve CRM'e arama çağrısı yapılmaz. O kaydetmede tek arama yapılır ve durum `Searched` olur; sonraki kaydetmelerde arama yoktur. Sonuç: MOD-0150 sözleşmesinden önce adı girilmiş kayıtlar, sözleşmeden sonraki ilk kaydetmelerinde bir kez aranır (netleştirme: sahip, 2026-10-22, sohbette "netleştir"; ilk taslaktaki "adın ilk kez dolduğu kaydetme" ifadesinin yerine). KR-4 aynen geçerlidir: bağ kaldırma işaretli kaydetmede arama yoktur ve bağı kaldırılmış kayıt bir daha aranmaz. Gerekçe: CRM'de aranabilecek en küçük bilgi ad ya da baş harftir; bilgisi olmadan açılan vaka (EK-3) aramayı harcamaz. | QPPV — bilgi |

Seçilmeyen seçenek (kayıt için): B — aramanın triyaj işleminde yapılması. Daha dolu veriyle arardı, ama aramayı oluşturma /
güncelleme dışındaki bir işleme taşır ve AL-6'nın bugünkü okumasını değiştirirdi.

## Pakete etkisi

Paket revizyonu 07: "One CRM search for a typed reporter" bölümü ve `ReporterCrmSearchState` kuralı KR-5'i yazar; "when the
record is completed" ifadesi ölçülebilir koşulla değiştirilir; BG-30 şu kabul testlerini alır (MOD-0150 sözleşmesinden
sonra): koşul öncesi `Pending` ve sıfır arama çağrısı; koşulun sağlandığı kaydetmede tek arama; sonrasında yeniden arama
yok. Bu hizalamayı `module-pack-author` yapar; Control Tower denetler.

## Yerini alan / yerine geçen

- Yok. Bu kayıt intake-fields-01 ve -04'ü değiştirmez; yalnız AL-6'daki "kayıt tamamlanınca" ifadesini ölçülebilir kılar.
