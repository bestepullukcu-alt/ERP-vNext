# MOD-0262-FU01 DL-2 ve MOD-0181 DL-4 — sahip kararları (2026-11-12)

Kaynak: CT kayıtları `docs/records/audits/2026-11/mod-0262-fu01-dl2-ct-acceptance-2026-11-12.md` (KOŞULLU) ve
`mod-0181-dl4-live-measurement-2026-11-12.md`. Sahip: Beste — "1 ve 2 önerdiğin gibi" (2026-11-12, MVP-5 WMS CT oturumu `ca7382cf`).

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| K-1 | MOD-0181 DL-4 FIX-02 — Pano ve Sayım Farkları'nda ad çözülemeyince kimliğe (GUID) düşen alanlar | **Evet:** FIX-01 kuralıyla düzeltilir (kimlik hiçbir yerde gösterilmez; lokalize "okunamadı"); WP-0181-DL4-FIX-02 DL-5 oturumuna gönderildi. DL-5 Kanıt Paketi'ndeki kimlikler FIX-02 dışı, DL-5 doğrulamasında | Beste | 2026-11-12 |
| K-2 | MOD-0262-FU01 AC-21 (dosyadan sonra gelen bozuk bölüm) | **Paket r9'da gerçeğe hizalanır:** AC-21'in iki vakası "sağlayıcı ≤ 1 çağrı, satır yok, yarım dosya yok, 400"; §8.1'de bu yolun kaydını `AuditBehavior` (`Failed` / `Create`, 400) yazar. Gerekçe: §12 "depolama çağrılmaz" ile AD-4 akışlı okuma kuralı çelişiyor; sonuç güvenli. FU testi: host'ta "4 alan + dosya + sonra alan" vakası | Beste | 2026-11-12 |
| K-3 | MOD-0262-FU01 AC-25 (yükleme kaydında `EntityId` / `AfterState` yok) | **KISMİ kabul + adlı FU** (DL-1 `StoreRepositoryObjectCommand` açığıyla tek FU): seçenekler — `ContentId`'yi yürütücü önceden üretir (korunan yol) ya da `AuditBehavior` yanıttan `EntityId` / `AfterState` alır (MOD-0021 sahibi kararı). FU sahibi ve seçeneği FU açılırken | Beste | 2026-11-12 |
| K-4 | MOD-0262-FU01 ORTA-1 (Mongo deposunda kiracı yüklemi testsiz) | Karardan bağımsız düzeltme talimatı: gerçek Mongo + iki kiracı vakası, CT sabotajı A3 kırmızı olmalı | CT | 2026-11-12 |
