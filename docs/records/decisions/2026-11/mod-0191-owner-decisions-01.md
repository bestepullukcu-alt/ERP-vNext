# MOD-0191 — sahip kararları 01 (etkinleştirme ve gerekçeli değişiklik, K1 / K2)

Kaynak: CT · MVP-1 / MVP-5 (merkez). Sahip = MVP-4 sahibi (Beste); MVP-4 sahipliği 2026-11-17'de devredildi
(`1832d9df1`). Sorular, #446 ile main'e giren kapsam önerisinin (`execution/domains/supply-chain-execution/work-packs/WP-MOD0191-ACTIVATION-OVERRIDE-UI-SCOPE.md`,
`f38bc3784`, merge `bd2fe7bd9`) "Ali onayına sunulan öneriler" 1–2 ve "Uygulama öncesi karar ve sahiplik kapıları" ilk iki
satırıdır. Öneri metinde Ali'ye sorulacak diye yazılmıştı; sahip, MVP-4 sahibi olarak kararı kendisi verdi ("bunu aliye sormamıza
gerek yok"). Sözler, sahibin CT sohbetinde doğrudan yazdıklarıdır.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| K1 | **Ayrı etkinleştirme:** manuel miktara `Approved → Active` geçişi eklenir; MRP etkin miktarı `Active` kayıttan okur. Tarih aralıkları çakışınca seçenek **A — reddet** (bugünkü davranış, `ManualQuantityMongoStore.cs:112`); B (eski Active kaydın ileri aralığını sürümlü kapatma) seçilmedi. Mevcut `Approved` kayıtlar geçişte `Active` sayılır; miktar hiçbir koşulda sessizce sıfıra düşmez | "bunu aliye sormamıza gerek yok önerin gibi olsun" | CT sohbeti, doğrudan | 2026-11-19 |
| K2 | **Gerekçeli değişiklik (override):** onaylı miktar yerinde değişmez; kaynak miktar kimliği, eski / yeni miktar (temel birim) ve zorunlu gerekçe ile yeni bir taslak açılır, kaynak kayıt değişmez. Yeni izin yalnız `activate` ve `override` eylemleri için açılır; inceleme mevcut `safety-stock.policies.submit` / `.review` ile yapılır. Etkinleştiren kişi onaylayandan farklı olur | "bu da öneri gibi" | CT sohbeti, doğrudan | 2026-11-19 |

## Bu kaydın açmadığı noktalar (paket revizyonunda kapanır)

- **İzin anahtarlarının adı:** `safety-stock.policy.activate` / `safety-stock.override.create` (pack §14 adayları) ile
  `safety-stock.quantities.activate` / `.override` arasında seçim; PKS-001 Tier-3 `override` eylem kaydı ve §4
  `SafetyStockOverride / ApprovalDecision` ad alanı uzlaştırması.
- **Görev ayrılığının tam kapsamı:** K2 yalnız "etkinleştiren ≠ onaylayan" der; hazırlayan ve önemli editörün etkinleştiremeyeceği
  öneride vardı, paket revizyonunda açıkça yazılmalı.
- **Geri alma kuralı** ve geçmişe dönük MRP okuması (haftalık `asOf`) — MOD-0189 tüketici sözleşmesiyle birlikte.
- Gateway 5000 → SafetyStockService 5069 rotası (integration-agent) ve §3 port kaydı; tenant ekranı (7 dil, UAS-001).
- BL-501 audit bağlantısı kanıtı.

## Sonuç ve sonraki adım

- MOD-0191 **paket revizyonu gerekir** (CT-APR-01/02: ayrı revizyon, bağımsız inceleme, onay satırı). Bu kayıt kod izni ya da
  `ready-for-dev` yetkisi vermez; mevcut pack sürümünü değiştirmez.
- Karar MRP'yi (MOD-0189) de bağlar: MOD-0189 revizyonu `Active` okuma semantiğini bu kayda göre yazar.

Bu kayıt commit'lendikten sonra değiştirilmez; değişiklik yeni numaralı kayıtla yapılır.
