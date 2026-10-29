# Tedarikçi ana kaydı kiracıda tektir; onay ve satın alma ilişkisi şirket (LE) bazındadır (MOD-0140)

- karar veren: Natig (yönetim), 2026-10-21 — sahip tarafından 2026-10-23'te iletildi
- durum: KABUL EDİLDİ
- kaynak (repoda): `docs/roadmap/plans/mvp6-development-plan-v10.0.md`, C1 satırı ("Answered 2026-10-21"), main, PR #177
- iş: WP-MVP2-SUPPLIER-TENANT-01 · modül MOD-0140 Supplier Onboarding · servis `Diten.ProcurementService` (5065)
- öneri ve ölçüm: `docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md`,
  `docs/records/audits/2026-10/mvp2-supplier-tenant-01/MEASUREMENT.md`

## Karar metni (aynen)

> "Karar — 2026-10-21, Natig (yönetim): Tedarikçinin ana kaydı kiracı içinde tektir; kiracının bütün tüzel kişilikleri
> (LE) aynı kaydı paylaşır. Onay durumu, ödeme koşulları ve satın alma ilişkisi her LE için ayrı tutulur. Aynı firma iki
> kez kaydedilmez; tedarikçi onayı şirket/tesis bazında kalır."

## Bağlam

Bugün tedarikçi kiracı + LE kapsamlıdır: her şirket aynı firmayı ayrı kayıt olarak açar
(`SupplierRepository.cs:142-148`, `SupplierIndexConfiguration.cs:27-43`). `supplier.openapi.yaml` 1.2.0 (FROZEN)
":12 Tenant + Legal-Entity scoped" der. MVP-6 DC-02, kiracı + LE için olumlu bir tedarikçi uygunluğu ister.

## Sonuç

- `Supplier` kiracıda tek kayıttır; ortak alanlar (ad, ülke, vergi no, iletişim, belgeler, KYC/yaptırım tarama sonucu)
  bütün şirketlerde aynıdır.
- `SupplierCompanyRelationship` (kiracı + LE + tedarikçi) şirket bazındaki bilgileri tutar: durum, onboarding ve onay,
  şirketin tarama istisnası, ödeme koşulu kodu, satın alma ilişkisi, harici eşleme ve eski kodlar.
- "Aynı firma iki kez kaydedilmez": tekillik kiracı + ülke + normalize vergi no'dur. Başka şirkette aynı vergi no ile
  oluşturma 409 döner, mevcut kodu ve "bu şirkete bağla" yolunu verir.
- Uygunluk (DC-02 dahil): "(kiracı, şirket, kod) için aktif ilişki var mı?"
- Sözleşme `supplier.openapi` 1.2.0 → 1.3.0, yalnız eklemeli (K16).

## Sahip uygulama kararları (2026-10-21 / 2026-10-23)

K-1..K-10: öneri belgesi §5a ve MOD-0140 §19. Bu kayıttan sonra gelenler (2026-10-23):
- K-10 onaylayıcı Beste (`bestepullukcu-alt`); CT-APR-02 incelemesi MVP-1 Control Tower.
- S4 kapsamı (MOD-0141/0143/0144/0145, XMC-001 onayı): yalnız "bloke tedarikçiye yeni işlem yok" kontrolü; başka
  davranış değişmez.
- Blocked / OnHold tedarikçi şirkette görünür ve yönetilebilir kalır; yeni işleme yalnız Active ilişki girer.
- `Suppliers:Model` geri alma anahtarı S1'de kalır.

## Kapsam dışı

- Canlıda tedarikçi verisi yoktur; göç yalnız geliştirme/test verisi için çalışır.
- Inventory karantinasındaki tedarikçi referansı Inventory sahibinindir (FU-0175-SUPPLIER-REF, XMC-001).
- MVP-6 uygunluk okuyucusu 1.3.0 sözleşmesiyle MVP-6 tarafında yazılır.
