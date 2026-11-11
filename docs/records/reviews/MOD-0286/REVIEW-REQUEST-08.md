# MOD-0286 — bağımsız inceleme isteği 08 (SOP §20.4 / CT-APR-02; sürüm bağlama)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `ebaf59e0187a61c83049947c651b3f832a36c76da4132e90a8dbc7837d2ff94a` (revizyon 08)
- Kaynak commit: 6d8cc6ace (dal `chore/ct/pss-ui-ct-01`, main `ded2a4de0`'dan ileri sarıldı + 1 commit; yerel, push edilmedi)
- Önceki sürüm: rev 07 `ffeaae57…` — inceleme 07 KOŞULLU (F-18 ORTA) + 07b UYGUN (F-18 kapandı); PR #222 ile main'e girdi. Onaylı taban (geçerli): rev 06 `7f6868d9…`.
- Neden: rev 07'nin inceleme ve onay satırları dalda main-ilk ebeveyn birleştirmesinin (1139bc0d1) ikinci ebeveyn tarafında kaldı. Main'deki `scripts/pack_approval.py` `main_merge` (S-42 düzeltmesi, `cc597fec0`; ZIP'te `:735-760`) bir satırı yalnız, onu getiren ana çizgi merge'ünün birinci olmayan ebeveyninin kendi ilk-ebeveyn çizgisindeyse sayar → "iç içe ya da ters merge" ENGEL (`excerpts/pack-approval-check-main-ded2a4de0-rev07.txt`). `review_problems` bir sürümün bütün satırlarını saydığı ve onay dosyaları yalnız ekleme aldığı için rev 07 bir daha onaylanamaz. CT'nin hatası: önerdiği kuru koşu yalnız `append_only_problems`'ı ölçtü, insan adımını ölçmedi.
- Rev 08 = rev 07 + Implementation Notes'ta tek "Rev 08" satırı (satır 531); başka hiçbir satır değişmedi. 557 satır. Fark: `excerpts/MOD-0286-pack-diff-ecbed8701-to-6d8cc6ace.diff` (+1 / -0); önceki sürüm `excerpts/MOD-0286-pack-previous-version-ecbed8701.md`. Başlık bandı bilerek rev 07 metnini korur (içerik aynı; değişiklik tek satırla sınırlı kalsın diye).
- Satır yazma düzeni: bu dal main'den ileri sarıldı; inceleme 08 satırı ve onay satırı bu dalın kendi çizgisinde, hiçbir birleştirmeden önce yazılacak; main ilerlerse yalnız normal `git merge origin/main` (dal birinci ebeveyn; S-42) ve satırlar ondan sonra.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-08-6d8cc6ace.zip` · sha256 `99278af6cbe9abb1474b76069636e237d59ec04b6131216728bae23294013c42` · 160 097 bayt · 16 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-08/MOD-0286-review-08-6d8cc6ace.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-ebaf59e0.md`
- Sonra: bulgu dosyası commit → `review-row` 08 → `check` kuru koşu (insan adımı dahil) → tek onay satırı (`--author GMG-ERP --approver bestepullukcu-alt`) → push ve PR sahibin sözüyle, bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 08 (SOP §20.4 / CT-APR-02; sürüm bağlama). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-08-6d8cc6ace.zip · sha256 99278af6cbe9abb1474b76069636e237d59ec04b6131216728bae23294013c42 · kaynak commit 6d8cc6ace (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 ebaf59e0187a61c83049947c651b3f832a36c76da4132e90a8dbc7837d2ff94a — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 08'dir. Rev 07 (ffeaae57…) senin inceleme 07 (KOŞULLU) ve 07b (UYGUN) incelemelerinden geçti ve PR #222 ile main'e girdi; ancak main'deki scripts/pack_approval.py main_merge (ZIP'te :735-760, S-42 düzeltmesi) rev 07'nin inceleme ve onay satırlarını "iç içe ya da ters merge" diye reddediyor (excerpts/pack-approval-check-main-ded2a4de0-rev07.txt; dal geçmişi excerpts/git-topology.txt). review_problems bir sürümün bütün satırlarını saydığı ve onay dosyaları yalnız ekleme aldığı için rev 07 artık onaylanamaz. Rev 08 = rev 07 + Implementation Notes'ta tek "Rev 08" satırı (satır 531). Fark: excerpts/MOD-0286-pack-diff-ecbed8701-to-6d8cc6ace.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-ecbed8701.md.

Önce oku: pack diff; hedef paketin satır 525-535 bölgesi; inceleme 07 ve 07b kayıtları (docs/records/reviews/MOD-0286/2026-10-26-ffeaae57.md, -07b.md); excerpts/pack-approval-check-main-ded2a4de0-rev07.txt; scripts/pack_approval.py main_merge (:735-760) ve review_problems; execution/approvals/reviews.tsv ve approvals.tsv (rev 07 satırları ve rev 06 satırları); excerpts/git-topology.txt.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (sürüm bağlama; risk HIGH):
- Fark yalnız beyan edilen tek satır mı (diff'i kendin say; başka satır değişmiş mi; künye, AC, UIC, RC, dilim tablosu aynı mı)?
- Rev 08 notu, rev 07'nin onaylanamama nedenini main_merge ve review_problems koduyla doğru anlatıyor mu (ZIP'teki koddan ölç)?
- Not satırı paketin içeriğini ya da yetkisini değiştiren bir ifade taşıyor mu (ör. kod yetkisi, kapsam, karar)? Başlık bandının rev 07 metnini koruması bir tutarsızlık mı, yoksa kabul edilebilir mi? Tek cümle görüş.
- Onay yolu: rev 08 için yazılacak inceleme ve onay satırlarının main_merge kuralından geçmesi için beyan edilen yazma düzeni (dalın kendi çizgisi, birleştirmeden önce; gerekirse yalnız dal-ilk merge) yeterli mi? Başka bir engel görüyor musun?
- Rev 07 içeriği için inceleme 07 ve 07b'nin bulguları rev 08'de de geçerli mi (F-18 kapanışı dahil)?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ ebaf59e0187a61c83049947c651b3f832a36c76da4132e90a8dbc7837d2ff94a
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-19 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-20 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 19'dan başlar; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-08/MOD-0286-review-08-6d8cc6ace.zip
sha256 99278af6cbe9abb1474b76069636e237d59ec04b6131216728bae23294013c42
```
