# MOD-0018-FU31 — sahip kararları 07

Bu bir ek kayıttır. 01…06 numaralı kayıtlar değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Söz, sahibin bu sohbette doğrudan yazdığı sözdür.
- Kişi adı yazılmaz; sahip = MOD-0018 sahibi.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 15 | Paket onaylandı. Sürüm: `42c652e9d`, sha256 `07db37117210ccad6c4525341a5766163cfd5c7cda350b429562d0923e219c35`. İnceleme: beşinci tur, KOŞULLU, engel yok (`reviews.tsv` satırı `62a264c74`). Kayıt 05'teki beş bağlayıcı şart ve kayıt 06'daki Platform şartları geçerlidir. | "onayla" | CT · Auth sohbeti, doğrudan | 2026-11-16 11:25 |

Not: Sahibin bir önceki mesajı «onyala» idi. Kural yalnız tam «onayla» sözünü tanıdığı için o mesaj onay sayılmadı. Onay, bu tabloda yazılı «onayla» sözüdür.

## Kod için kalan kapılar

Paketin künyesine göre koda başlamadan önce şunlar gerekir:

1. Onay satırı main'de olmalı.
2. `MOD-0018-FU31` kimlik kayıt satırı main'de olmalı (K-10).
3. DL-1 başlamadan kayıt 05'teki iki şart kapanmalı:
   - F-1 tasarımı,
   - F-3.

Bu kayıt commit'ten sonra değiştirilmez.
