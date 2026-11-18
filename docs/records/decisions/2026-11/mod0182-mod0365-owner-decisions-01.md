# MOD-0182 Barkod / RFID ve MOD-0365 Cihaz / Uç — sahip kararları 01 (2026-11-12)

Kaynak: mockup özeti `execution/domains/supply-chain-execution/work-packs/mockups/barcode-scanning/BRIEF-MOD-0182-MOD-0365-barcode-devices.md`
(açık sorular S-1…S-6). 2026-11-06 kararları (iş takibinde kayıtlı; v1 sağlayıcıdan bağımsız, RFID tek uç, v1 yalnız çevrimiçi, kamera ile okutma kapsamda, paket
kuralları) geçerlidir; S-1 (çevrimdışı) o tarihte "v1 yalnız çevrimiçi, çevrimdışı kuyruk FU" olarak kapandı. Paket yok; kararlar paketin
girdisidir. Sahip: Beste.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| S-2 | RFID ve etiket basımı v1'de | **Evet:** RFID (tek uç, 06-11 kararı) ve etiket basımı v1'de. Etiket türleri (SSCC koli / palet etiketi, raf etiketi vb.) ve yazıcı bağlantısı pakette tanımlanır | Beste | 2026-11-12 |
| S-3 | Telefon kameraları | **Cihaz olarak kaydedilir** (cihaz sağlığı ve uyum izinde görünür) | Beste | 2026-11-12 |
| S-5 | MOD-0365 sahibi | **Merkez** (platform-shared-services; Beste / MVP-1 CT) | Beste | 2026-11-12 |
| S-6 | Türkiye İTS bildirimi | **Ayrı modül.** Blueprint 8.8'de "Serialization / Track & Trace" grubu var: MOD-0245 Serialization Repository Integration (W-3), MOD-0246 Aggregation / Verification (W-4), MOD-0247 Compliance Interfaces (EU FMD / DSCSA) (W-4), dış sistemler MOD-0260 (TraceLink / Arvato) ve MOD-0273 (EU Hub / DSCSA ağları). İTS adı Blueprint'te geçmiyor; ülke düzenleyici arayüzü olarak MOD-0247'nin kapsamına girmesi **çıkarımdır** — ayrı bir modül talebiyle araştırılır (registry'de bu kimlikler henüz yok). MOD-0182 yalnız okutulan serileri tutar | Beste | 2026-11-12 |
