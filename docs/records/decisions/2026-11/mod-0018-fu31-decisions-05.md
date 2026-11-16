# MOD-0018-FU31 — sahip kararları 05

Bu bir ek kayıttır. 01…04 numaralı kayıtlar değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Söz, sahibin bu sohbette doğrudan yazdığı sözdür.
- Kişi adı yazılmaz; sahip = MOD-0018 sahibi.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 12 | Paket onaya gider. Sürüm `42c652e9d`, sha256 `07db37117210ccad6c4525341a5766163cfd5c7cda350b429562d0923e219c35`. Beşinci incelemenin sonucu KOŞULLU, engel yok (`reviews.tsv` satırı `62a264c74`). Paket bir daha değiştirilmez. İncelemenin beş bulgusu, aşağıdaki tabloda yazılı kod adımları için bağlayıcı başlama şartıdır. Kod adımı, şartı kapanmadan başlamaz. Adımın kabulü, şartın kapandığını kodla ve testle gösterir. | "Evet, şartla onay" | CT · Auth sohbeti, doğrudan | 2026-11-11 12:29 |

Bağlayıcı şartların kaynağı: `docs/records/reviews/MOD-0018-FU31/2026-11-11-07db3711.md`.

| Bulgu | Önem | Bağlandığı adım | Şart |
|---|---|---|---|
| F-1 | ORTA | DL-1 başlamadan (tasarım), DL-1 kabulü (test) | Satır silinmeden önce, o satırın eksik "granted" kaydı `recordedLate` ile yazılır. Bu kural hak düşüşü ve geri alma yollarında da uygulanır. Silme kaydı yazılıp çökme olursa ve satır canlı kalırsa, bu durum kalıcı bir "revoked" kaydı bırakmaz; nasıl ele alınacağı tanımlanır. AC-31'e şu çökme sırası eklenir: "ekleme çöktü, sonraki olay geri alma". |
| F-2 | ORTA | DL-1 kabulü | AC-31'in (1) ve (2) sabotajları, ölçülen davranışın bulunduğu yere konur: ya fırlatan bir sahte forwarder kullanılır (`RoleAuditRecorderTests.cs:56-68` deseni), ya da sabotaj forwarder'ın içine konur. Her sabotaj kırmızı verir. |
| F-3 | DÜŞÜK | DL-1 başlamadan | `not_applied` kaydının olay adı ve yazım yolu tanımlanır. Seçenekler: koşucu deseniyle yalnız yerel yazılan `role_permission_retraction_not_applied`, ya da bu kaydın kaldırılması. Ad sözlüğe girecekse K-19 izninin kapsamı Platform sahibiyle yeniden teyit edilir. Yarışı kaybeden koşu "uygulanmadı" kaydı bırakmaz. |
| F-4 | DÜŞÜK | DL-1 | İlk koşudaki geriye dönük `recordedLate` kaydı ya kapsam dışı yazılır, ya da AC-28 (b) ve Validation Rules bu istisnayla tutarlı hale getirilir. İletilen kayıtta asıl değişiklik zamanı ile kayıt zamanı ayırt edilir. |
| F-5 | DÜŞÜK | — | Metin düzeltmesi: denetim yazımı her yerde `RecordOnceAsync` olarak anılır. AC-32 (4)(i) içindeki test sayısı ölçülen sayıya çekilir. Düzeltme kabul kaydında yapılır. |

## Açık kalanlar

- **K-20:** `_IndexL10n.cshtml` yük satırının Platform iznine dahil olduğu henüz teyit edilmedi. Bu teyit DL-1 kodunun kapısıdır; onayın kapısı değildir.
- **Paket onayı:** Bu kayıt onay sözü değildir. Paket onayı, MOD-0018 sahibinin «onayla» sözüyle verilir.

Bu kayıt commit'ten sonra değiştirilmez.
