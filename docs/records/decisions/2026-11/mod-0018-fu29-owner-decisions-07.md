# MOD-0018-FU29 — sahip kararları 07

Bu bir ek kayıttır. Önceki kayıtlar (01…06) değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Söz, sahibin bu sohbette doğrudan yazdığı sözdür.
- Kişi adı yazılmaz; sahip = MOD-0018 sahibi.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 16 | Paket onaylandı. Sürüm: `bdcef6f1b`, sha256 `df0565f45c4d1f0f6ccb63cea24fad602df66bb84e815bcdf0b7ac645f2970e0`. İnceleme: yedinci tur, KOŞULLU, engel yok (`reviews.tsv` satırı `f4f0402f2`). Karar kaydı 06'daki 15 bağlayıcı şart geçerlidir. | "onayla" | CT · Auth sohbeti, doğrudan | 2026-11-06 14:22 |

CT-APR-01 onay satırı (`--deferred`) şu sırayla yazılır:

1. Paketin başlık ön koşulu gereği, karar kayıtları ve kimlik kayıt satırı önce main'e girer.
2. Ardından onay satırı yazılır.

Bu kayıt commit'ten sonra değiştirilmez.
