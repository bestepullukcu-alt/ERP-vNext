# Golden Reference Transition Control

Bu kontrol listesi, `GoldenReferenceItem` referans modulunun MDM business gorunumunden `developer-enablement` ownership'ine kontrollu gecisini yonetmek icindir.

## Stage 1 — Governance
- [x] `developer-enablement` domain iskeleti olusturuldu.
- [x] `DEV-0000-golden-reference-item` module pack olusturuldu.
- [x] Yeni domain amaci ve kapsami yazildi.
- [ ] Eski `MOD-0000-golden-reference-item` icin emeklilik/redirect karari dosyaya islendi.

## Stage 2 — Inventory confirmation
- [x] Frontend izleri listelendi.
- [x] Backend izleri listelendi.
- [x] Gateway izleri listelendi.
- [x] Reference/script izleri listelendi.
- [x] Her iz icin "gecici host" veya "tasinacak" etiketi verildi.

## Stage 3 — Documentation alignment
- [x] Eski `reference/golden-module-kit` kaynagi kaldirildi.
- [x] Eski scaffold dokumanlari kaldirildi.
- [ ] Reference asset'lerin hangi senaryo icin oldugu netlestirildi.

## Stage 4 — Runtime strategy
- [x] Golden Reference Item'in hedef host servisi icin karar verildi: `DitenDevEnablementService`
- [x] Fiziksel tasima gerekecekse etaplari yazildi.
- [ ] Gateway route stratejisi netlestirildi.
- [x] Frontend area naming stratejisi netlestirildi: `DevEnablement`

## Stage 5 — Legacy handling
- [ ] MDM altindaki `MOD-0000` dosyasi `superseded` veya benzeri duruma cekildi.
- [ ] Eski ownership ile yeni ownership arasina acik bir not eklendi.
- [ ] Duplicate source of truth kalmayacak sekilde tek aktif module pack belirlendi.

## Exit criteria
- `GoldenReferenceItem`, business module degil reference asset olarak herkes tarafindan okunabilir sekilde tanimlanmis olmali.
- Yeni referanslar ayni domain altinda acilabilecek net bir naming ve ownership modeli olmali.
- `.antigravity`'ye tasinacak pattern'ler ile runtime host gercegi birbirine karistirilmamali.
- MDM altindaki canli GoldenReferenceItem izi temizlenmis olmali.
