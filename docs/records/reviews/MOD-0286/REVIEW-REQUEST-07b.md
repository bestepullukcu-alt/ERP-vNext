# MOD-0286 — tamamlayıcı inceleme isteği 07b (aynı sürüm; inceleme 07 F-18 kapanışı)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db` (revizyon 07; **paket değişmedi**)
- Kaynak commit: b3f06a55f (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi). İnceleme 07 kaydı ve satırı: `2026-10-26-ffeaae57.md` @ 007fec9e2, `reviews.tsv` satırı @ b3f06a55f (KOŞULLU, F-18 ORTA açık).
- Neden: F-18 bir kanıt eksiğiydi — paket `:58`, `:517`, `:530` BL-440 için `product-backlog.md:6264` atfını taşıyor, inceleme 07 ZIP'indeki kesit yalnız `:6199-6223` idi. Bu ZIP tam `product-backlog.md` dosyasını ve `:6195-6275` numaralı kesiti taşıyor. Sahip kararı `07b` (2026-10-26).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-07b-b3f06a55f.zip` · sha256 `ce5366dd3a12826f69cdc81a12f2b1ffe28e54434eb2ee5d320f28b22982957a` · 412 801 bayt · 7 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-07b/MOD-0286-review-07b-b3f06a55f.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-ffeaae57-07b.md` — aynı sürüm için ikinci kayıt; F-18 bu kayıtta `kapandı` ya da `açık` olarak yeniden yazılır (ORTA kapanışları: açık / kapandı / kabul — onaylayan; `@` başvurusu yalnız ENGEL kabulünde).
- Sonra: bulgu dosyası commit → `review-row` 07b → `check` → tek onay satırı (`--author GMG-ERP --approver bestepullukcu-alt`) → push ve PR sahibin sözüyle, bestepullukcu-alt merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack tamamlayıcı inceleme 07b (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-07b-b3f06a55f.zip · sha256 ce5366dd3a12826f69cdc81a12f2b1ffe28e54434eb2ee5d320f28b22982957a · kaynak commit b3f06a55f (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db — inceleme 07 ile AYNI sürüm; tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bağlam: inceleme 07 (docs/records/reviews/MOD-0286/2026-10-26-ffeaae57.md, ZIP'te) KOŞULLU verdi; tek bulgu F-18 (ORTA): paket :58, :517 ve :530 BL-440 için product-backlog.md:6264 createList fabrika notunu gösteriyor, ama o ZIP'teki kesit yalnız 6199-6223 idi. Bu ZIP tam docs/roadmap/backlog/product-backlog.md dosyasını, numaralı kesiti excerpts/product-backlog-BL-440-6195-6275.txt'yi ve paketteki :6264 geçen satırları excerpts/pack-lines-citing-6264.txt'yi taşır. Paket değişmedi.

Görev (yalnız bu):
- product-backlog.md:6264 satırı BL-440'a mı ait (başlık :6199 ile :6264 arasında başka bir ### BL- başlığı var mı) ve DitenDataTable.createList fabrika notunu mu taşıyor? Tam dosyadan ölç; kesite güvenme.
- Paket :58, :517, :530'daki BL-440 atıfları (:6199 başlık, :6264 createList notu) bu ölçümle doğru mu?
- Bunlar doğruysa F-18'i kapat; değilse açık bırak ve nedenini yaz. Başka bir şey inceleme; yeni bulgu ancak bu üç satırla ilgiliyse yazılır.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-18 | ORTA | <bulgunun kısa metni ve kapanış gerekçesi> | <kapandı ya da açık> | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; F-18 satırını mutlaka yaz; yeni bulgu gerekirse numara 19'dan başlar; ORTA kapanışı yalnız açık / kapandı / kabul — onaylayan; açık kalan ORTA için sahip ve son tarih yazılır; F-18 kapandıysa ve başka bulgu yoksa "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-07b/MOD-0286-review-07b-b3f06a55f.zip
sha256 ce5366dd3a12826f69cdc81a12f2b1ffe28e54434eb2ee5d320f28b22982957a
```
