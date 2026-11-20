# DL-12 — feature durum olayları v1.1

UTC zamanları CT gözlem/kayıt zamanıdır; eski işçi olayları geçmişe yazılmaz. Parent koordinasyon kaydıdır, root ürün geliştirmez. Normal dağıtım kanıtları aynı dizinde; v1.0 negatifler ayrı korunur.

- 2026-11-20T20:42:01.200365+00:00 — WP-0230-DL12, WP-0230-DL12-BE, WP-0230-DL12-L10N: READY → DISPATCHED. İki actual normal0 kanıtı commitlendi; exact v1.1 blokları iki app işçisine byte-byte gönderildi, API isError=false makbuzları DISPATCH-RECEIPTS.json içinde; parent koordinasyondur, ürün sonucu değil. Önceki CT HEAD5fe5f148d8e839384f3a3e655e524a980d4fe84b; gerçek guard42/exit0/hash879dd2c57e9e0b608be0f4a49e8f6ff82609a101646a7360faaacdb5582f419d.

- 2026-11-20T20:44:04.097000+00:00 — WP-0230-DL12, WP-0230-DL12-BE, WP-0230-DL12-L10N: DISPATCHED → IN_PROGRESS. İki app snapshot active/latestTurn inProgress gözlendi: BE turn01a1fab0-e1f7-7f83-ac52-55344eb1a5a9 kapıyı doğrulayıp eski ürün RED testi hazırlığına geçti; L10N turn01a1fab0-f436-7523-80c1-e0ca99af3741 exact v1.1 kaynak/XML başlangıç kontrolüne geçti. Ürün/test sonucu henüz yok. Önceki CT HEADa346f7bdc3cebdcd5fc1e10edd86160b1739f427; gerçek guard42/exit0/hashf01b6c90a358105b8ca87e8127a79368abbba91d5e3ab73a93ce229b2181591a.
