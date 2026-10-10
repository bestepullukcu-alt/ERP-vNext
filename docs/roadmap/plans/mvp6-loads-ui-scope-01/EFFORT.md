# Teslimat bazlı O/M/P efor — saat

O iyimser / M en olası / P kötümser; kişi-saat, takvim taahhüdü veya actual spent değildir. 8 saat=1 kişi-gün. Referans son mevcut `mvp6-effort-carrier-e2e-update-05/EFFORT.tsv`. Bu paket merkezi effort dosyasını değiştirmez; aşağıdaki **replace** işlemi CT önerisidir, ayrıca ek bütçe değildir. Teslim edilmiş satırlara saat kredisi veya yüzde artışı yazılmaz.

| Teslimat | O | M | P | Eski satır disposition |
|---|---:|---:|---:|---|
| UI exact pack delta/karar/Phase1.5 dispatch kapanışı |4|8|16|0185-1-REMAINING 3.6/6/9.6 yerine |
| List+create Slim, repeatable editor,7 dil, module tests |24|40|64|0185-4-REMAINING 28.8/48/96 yerine |
| UI Gateway/nav/permission/L10n/personalization single-owner integration |8|16|28|0185-5-REMAINING'in yeni UI alt kalemi |
| Gerçek Auth/browser/persistence/restart bağımsız UI VER |12|20|36|0185-6-REMAINING'in yeni UI alt kalemi |
| **Bounded UI teslimatları toplamı** |**48**|**84**|**144**|**6 /10.5 /18 kişi-gün** |
| Kalan live-reference/root integration (UI dışında) |12|20|32|0185-5-REMAINING'in ayrı kalan alt kalemi; yukarıdaki8/16/28 ile birlikte eski19.2/32/51.2'yi REPLACE eder |
| Kalan cross-module/live acceptance ve dar defect closure |8|16|28|0185-6-REMAINING'in kalan alt kalemi; yukarıdaki12/20/36 ile birlikte eski14.4/24/38.4'ü REPLACE eder |
| Contract seam/root compatibility rezervi |4.8|8|12.8|0185-2-REMAINING aynen korunur, yeni tahmine eklenmez |
| Backend module-owned root/live dar rework rezervi |9.6|16|25.6|0185-3-REMAINING aynen korunur, yeni tahmine eklenmez |
| **Yeni MOD0185 remaining görünümü** |**82.4**|**144**|**242.4**|Eski80.4/134/233.6 yerine; net+2/+10/+8.8 |

0185-1/2/3/5/6-DELIVERED satırları unchanged. Eski frontend/integration/test/design kalan rezervlerini silmeden yukarıdakileri append etmek double-count olur. Yeni satır ID önerileri:0185-1-REMAINING,0185-4-UI,0185-5-UI,0185-5-LIVE,0185-6-UI,0185-6-LIVE;2/3-REMAINING korunur. Transition/detail/lookup UI burada tahminlenmedi; ayrı root kararı sonrası kapsam/tahmin gerekir, bu tahmin full lifecycle UI vaadi değildir.

Belirsizlikler: Slim nested editor ve7 dil M40; hazır shared altyapı O, route/provider/preimage çakışması P; gerçek Auth/LE ortamı ve browser evidence kapasitesi P36; live/root kararları ayrı rezervde. Root tasarım değişikliği UI ilk dilimini büyütürse rebaseline gerekir. Bekleme süresi ve başka modül Auth rework maliyeti bu saatlere otomatik eklenmez. Bu hazırlığın actual saatleri ölçülmedi; tamamlanan doküman için teslim edilmiş efor icat edilmedi.
