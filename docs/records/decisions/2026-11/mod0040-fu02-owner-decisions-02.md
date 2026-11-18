# MOD-0040-FU02 — sahip kararları 02 (2026-11-10)

Paket: `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md` r11 (sha256 `62f352de…cc2d`).
Sahip: Beste (K2). Karar paket metnini değiştirmez; DL-2'nin başlama önkoşulunu (A-8) kapatır. Paketin DL-2 revizyonu kararı işler.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| A-8 | `ORG` (Organizasyon Birimi) ve `POS` (Pozisyon) kodlarının izinli kapsamı — LegalEntity · Group · ikisi | **LegalEntity (şirket bazında).** ORG ve POS kodları şirket öneki alır (`{ŞİRKET}-ORG-000001`, `{ŞİRKET}-POS-000001`). Gerekçe (paketin önerisi): `OrganizationUnit.LegalEntityId` zorunludur (`OrganizationUnit.cs:9`); her birim bir şirkete bağlıdır | Beste | 2026-11-10 |

Sonraki adım: DL-2 paket revizyonu (kabul ölçütleri bu dilimin revizyonunda yazılır; kod izni yok — paket "DL-2" satırı). Revizyon
bağımsız inceleme ve sahip onayından geçmeden DL-2 kodu yazılmaz.
