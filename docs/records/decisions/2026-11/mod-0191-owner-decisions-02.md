# MOD-0191 — sahip kararları 02: S-1…S-14 cevapları (Beste, 2026-11-24)

Önceki kayıt: `mod-0191-owner-decisions-01.md` (K1 / K2, commit c26108068; o kayıt commit'ten sonra değiştirilmez, bu yüzden cevaplar yeni numaralı kayıttadır).

## S-1…S-14 cevapları (Beste, 2026-11-24)

Sorular MOD-0191 paketinin r2 sürümündeki §24 "Açık sorular (Beste)" tablosudur (r2: `execution/domains/supply-chain-execution/module-packs/MOD-0191-safety-stock-optimization.md`,
taban HEAD `6e84a168c`). Sahip = MVP-4 sahibi (Beste); cevaplar CT oturumunda verildi ve paket r3'e işlendi. Bu bölüm
yukarıdaki K1 / K2 satırlarını ve "Bu kaydın açmadığı noktalar" metnini değiştirmez; yalnız ekler.

> **Not (kayıt kuralı):** Kaydın yukarıdaki son cümlesi değişikliğin yeni numaralı kayıtla yapılmasını ister. Bu bölüm o
> kurala karşın aynı kayda **ek** olarak yazıldı; mevcut satırlar değişmedi. Ayrı kayıt gerekirse bu bölüm aynen
> `mod-0191-owner-decisions-02.md`'ye taşınır.

| No | Konu | Cevap | Pakete etkisi (r3) |
|---|---|---|---|
| S-1 | Onaylı override nasıl etkinleşir | **A** — override etkinleşirken aynı işlemde kaynak `Superseded` olur (miktarı, aralığı değişmez); yalnız açık override bağıyla (`SourceQuantityId`), başka hiçbir kayıt için değil. | Çakışma denetimi yalnız bağlı kaynağı dışarıda bırakır; başka örtüşen `Active` kayıt yine 409. |
| S-2 | Görev ayrılığının kapsamı | **Hayır** — etkinleştiren kişi hazırlayan ya da gönderen de olabilir; yalnız K2 geçerlidir: **etkinleştiren ≠ onaylayan**. (Paket r2'nin "en az üç kişi" önerisinden farklıdır.) | Üç ayrı kişi varsayan bütün kural, AC ve sabotajlar yeniden yazılır; en az iki kişi (hazırlayan / gönderen / etkinleştiren bir kişi olabilir, onaylayan ayrı). |
| S-3 | Onay anı çakışma denetimi | **Evet** — r1'in onay anındaki çakışma denetimi kaldırılır; çakışma yalnız etkinleştirmede ve yalnız `Active` kayıtlara karşı denetlenir. | r1 davranış değişikliği (DL-2). |
| S-4 | Manuel miktar / override taslağına gerekçeli ret | **Evet** — mevcut `safety-stock.policies.review` ile. Anahtarın anlamı politika retinden manuel miktar ve override taslağı retine genişler; bu genişleme PKS-001 §2 gereği açıkça kaydedilir. | `InReview → Rejected` geçişi; yeni anahtar yok. |
| S-5 | Geri alma kuralı | **A** — geri alma / pasifleştirme komutu yok; yanlış `Active` miktar gerekçeli değişiklikle (override) düzeltilir. | Yeni dilim ve yeni anahtar yok. |
| S-6 | İzin adları | **İlk seçenek** — `safety-stock.quantities.activate` + `safety-stock.quantities.override`. | Rol köprüsüne `override` fiili (X-4) gerekir. |
| S-7 | Politikaya yeni revision taslağı açılınca `Active` miktar | **A** — `Active` miktar kendi onaylı politika revision'ına bağlı kalır; yeni taslak okumayı etkilemez. | Okumadaki `CurrentRevisionId` eşitliği kalkar. |
| S-8 | Golden Reference sapmaları G-0191-01 / G-0191-02 | **Karar verilmedi — açık.** Sahip önce açıklama istedi. | Paket metni (G-0191-01 / -02) değişmez; S-8 açık kalır. |
| S-9 | Ekrandaki görünen adların kaynağı | Adlar **sahip servislerden** gelir: ürün (SKU) adı ürün ana verisi servisinden (MDM), depo adı depo / stok servisinden, kullanıcı adı Auth kullanıcı lookup'ından. Bugün var olan uçlar ölçülüp pakete yazılır; eksik uç varsa açık madde olarak yazılır, uydurulmaz. | Ölçülen uçlar pakette §11 ve §7'de. |
| S-10 | Mockup brief ve turu | **Evet** — mockup brief DL-6'dan önce. | DL-6 başlama kapısı. |
| S-11 | Politika ekranı | **Önerildiği gibi** — bu revizyonda, ayrı dilim (DL-7). | — |
| S-12 | BL-501 bağlantısı ve kategori | Bağlantıyı **merkezi ekip** yapar (MVP-4 geliştiricisi merkezi ekibin kendisidir); kategori **`MasterData`**. Zamanlama merkezi erteleme kararını izler: BL-501 bağlantısı merkezi ekipçe **MVP'lerden sonra** yapılır; daha erkene planlanmaz. | DL-8 merkezi ekibin, MVP'ler sonrası dilimidir. |
| S-13 | Aralığı geçmişte kalan kaydın etkinleştirilmesi | **Önerildiği gibi** — bitiş tarihi bugünden (UTC) önceyse etkinleştirme 409; şirket saat dilimi (BL-628) gelince yeniden bakılır. | — |
| S-14 | Künyedeki `status` | **Önerildiği gibi** — `ready-for-dev` kalır (r1 kapsamı); `approval_status: pending`. | — |

**Sonuç:** S-1…S-7 ve S-9…S-14 kapandı; **S-8 açık**. Bu cevaplar kod izni ya da r3 onayı değildir; r3 için bağımsız
inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01) ayrıca gerekir. r2 sürümü onaylanmadı ve r3 ile yer değiştirir.
