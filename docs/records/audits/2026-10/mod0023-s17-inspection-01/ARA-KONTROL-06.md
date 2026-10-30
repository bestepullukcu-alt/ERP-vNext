# MOD-0023 §17 rev 3 — ara kontrol 06 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `bf7c6601…` (commit 0e2dedebe). Girdi: `.ct-teslim/MOD-0023-review-rev3-r7/` (129 dosya, ZIP sha256
  `3c083444…`). Taze bağlamlı Explore alt ajanı, model sonnet, Codex tur 2 metni; yalnız klasörü okudu. reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — engel yok; önceki resmî turun üç bulgusu kapandı; kişi alanı sınırı, tüketici listeleri, O-S17-3 bağlı ölçüt
  listesi, dilim tablosu temiz bulundu.

## Bulgular — kabul; CT çözüm yönü

- F-1 ORTA: expectedVersion'sız geç RequestInfo ikizi seçenekten bağımsız bir yazma yolu açığı (yazıcı yinelenen anahtarı
  yakalamıyor, WorkflowTerminalTransitionWriter.cs:333-340; benzersiz indeks Schema :220). (ii) "üretici değişikliği yok" derken bu
  vaka ölçülemez ya da ulaşılamaz kalıyor. **CT yönü:** RequestInfo yinelenen anahtar eşlemesini ((i-b)) seçenekten çıkar, DL-2'nin
  temel kapsamına al (yazıcı her iki seçenekte izinli yol); O-S17-3 yalnız Approve/Reject (b) için yeniden okuma ↔ tüketici kuralı
  kalsın.
- F-2 DÜŞÜK: AC-W14 "aynı aktör, farklı gövde → ayrı 409 kodu" eşzamanlı yolda mümkün değil (sürüm koruması gövdeyi ayırmaz;
  D2.cs:274-275, D2Fix1.cs:376-377 TRANSITION_CONFLICT bekliyor). **CT yönü:** ayrı kod yalnız sıralı yolda; eşzamanlı yolda
  TRANSITION_CONFLICT (tüketici için Unknown) kalır ve mevcut iki test değişmez.
