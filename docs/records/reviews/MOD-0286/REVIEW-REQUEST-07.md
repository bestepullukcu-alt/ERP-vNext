# MOD-0286 — bağımsız inceleme isteği 07 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db` (revizyon 07)
- Kaynak commit: ecbed8701 (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi; `origin/main` c429c0338 = PR #210 merge, dal main + 2 commit)
- Onaylı taban: rev 06 `7f6868d9…` — main'de onaylı (PR #210, merge eden bestepullukcu-alt; `pack_approval.py check --ref origin/main` çıkış 0, HIGH). Rev 07 için `check`: "paket onaydan sonra değişti (33 satır) — yeniden onay gerekli" (`excerpts/pack-approval-check.txt`).
- Önceki incelemeler (`execution/approvals/reviews.tsv`, ZIP'te): 01 `33dc5e14` UYGUN DEĞİL (`fb15f188`), 02 `d15b6c8a` UYGUN DEĞİL (`7242768b`), 03 `9d075026` KOŞULLU, 04 `75bad532` KOŞULLU, 05 `d66d59cf` UYGUN, 06 `7f6868d9` UYGUN (sürüm bağlama).
- Bu sürümün nedeni: kabul için PSS-UI-P1-03 dağıtım prompt'u `scripts/prompt_gate.py` kapısında iki görev sınıfı ENGEL'i aldı. Kapı paketi künyeden okur (`scripts/ct_queue.py:1297-1312` `pack_approval_limit`): `status` `ready-for-dev`/`approved` olmalı, `approval_status` ya yok ya da `approved`. Rev 06 `status: draft`, `approval_status: none` ile onaylanmıştı. Künye değişikliği `review_exempt` listesinde değil → yeni inceleme ve yeni onay satırı gerekir. Aynı revizyonda bilinen atıf hataları da düzeltildi.
- Rev 07 = rev 06 + (1) künye `status: ready-for-dev`, `approval_status` satırı kaldırıldı (paket standardı alanı tanımlamıyor), başlık bandı "revision 07"; (2) atıf düzeltmeleri: D-8b karşı tarafı (`frontend-datatable-template.md:87, :100, :114` + BL-440 `:6199`, `:6264`; `frontend-js-standard.md:981-991` iki adı da taşımıyor), D-8a ve UIC-13 premium modal `style=` satırı 8 (4 değil), D-5'e iki ek geçiş (`.antigravity/ARCHITECTURE.md:234`, `.antigravity/workflows/debug.md:59`), FU-2 "5 dosya / 8 satır", erişilebilirlik `:630-633` (başlık `:628`), UIC-11 odak halkası `:633`, UIC-4 kaynak hücresi ayrıldı, UIC-15 kaçışsız dikey çizgi kaçışlandı, çatışma kaydı satırları 2 ve 4, satır 527, Implementation Notes'a tek "Rev 07" satırı. 556 satır (rev 06: 555), 17 satır eklendi / 16 çıktı; AC (420-437), UIC (145-178), RC (200-225) satırları yerinde. Fark: `excerpts/MOD-0286-pack-diff-f6575de30-to-ecbed8701.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-f6575de30.md`.
- Ayrıca ZIP'te yeni: inceleme 06 kayıtları; karar kayıtları `pss-mod0286-open-decisions-02.md` (öneri, karar yok) ve `pss-mod0286-kabul-tek-01.md` (DL-1 + DL-2 tek iş paketi PSS-UI-P1-03); main'deki DL-1 `spec.md`, DL-2 `compliance-checklist.md`, `manual.md`, `README.md`; kuyruk `CT-QUEUE.tsv` ve kayıt satırı; `scripts/ct_queue.py`, `scripts/prompt_gate.py`, SOP.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-23
- İnceleyen: sahip seçer; önceki altısı Codex.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high; beyan verilemiyorsa "verilemiyor" (sahip kararı `beyan-yok`).
- ZIP: `MOD-0286-review-07-ecbed8701.zip` · sha256 `58bfcedf5e8e499b40287ade5e149c4386c0e1b4050542a0251131dbb1143d4f` · 1 256 668 bayt · 150 dosya + `MANIFEST.sha256` (151)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-07/MOD-0286-review-07-ecbed8701.zip`; depoya girmez, sahip iletir.
- Beyanlar (inceleyen bilsin):
  - Bu tur küçük bir içerik incelemesidir: künye ve atıf satırları. Rev 06'nın içerik kapanışları (F-1…F-17) değişmedi.
  - Bu incelemeden sonra pakete **hiç dokunulmaz**; yeni onay satırı bu sha (`ffeaae57…`) için açılır (S-41: PR'da paket başına tek satır, onaylayan merge eder).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-ffeaae57.md`
- Kayıt adımları: bulgu dosyası commit → `review-row --ref <commit> …` → UYGUN ise `pack_approval.py row <paket> --author GMG-ERP --approver bestepullukcu-alt --risk <hesaplanan> --reason …` (tek satır) → `check` → push ve PR sahibin sözüyle, bestepullukcu-alt merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 07 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-07-ecbed8701.zip · sha256 58bfcedf5e8e499b40287ade5e149c4386c0e1b4050542a0251131dbb1143d4f · kaynak commit ecbed8701 (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 07'dir. Onaylı taban rev 06 (7f6868d9…, inceleme 06 UYGUN, main'de onaylı). Neden: dağıtım prompt kapısı paketin künyesini okur (scripts/ct_queue.py:1297-1312 pack_approval_limit; scripts/prompt_gate.py görev sınıfı denetimi) ve rev 06 künyesi status: draft / approval_status: none idi. Rev 07: künye status: ready-for-dev, approval_status satırı kaldırıldı, başlık bandı; ve atıf düzeltmeleri (D-8b karşı tarafı, D-8a/UIC-13 sekiz style= satırı, D-5 iki ek geçiş, FU-2 sayısı, :630-633 ve :633, UIC-4 kaynak hücresi, UIC-15 kaçış, çatışma kaydı satırları 2 ve 4, satır 527, tek "Rev 07" notu). Fark: excerpts/MOD-0286-pack-diff-f6575de30-to-ecbed8701.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-f6575de30.md.

Önce oku: hedef paket (künye, başlık bandı, Open Decisions D-5/D-8a/D-8b, DL-2 tablosu UIC-4/11/13/15, Implementation Notes son satırlar, Follow-up FU-2); pack diff ve önceki sürüm; inceleme 06 (docs/records/reviews/MOD-0286/2026-10-23-7f6868d9.md); scripts/ct_queue.py:1297-1312; scripts/prompt_gate.py görev sınıfı bölümü; scripts/pack_approval.py review_exempt; .antigravity/rules/module-pack-standard.md (künye alanları, status değerleri); atıfların hedefleri: .antigravity/rules/frontend-datatable-template.md, frontend-js-standard.md, premium-modal-standard.md, docs/roadmap/backlog/product-backlog.md (excerpts), .antigravity/ARCHITECTURE.md ve .antigravity/workflows/debug.md.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH — onaylı tabana göre değişiklik):
- Fark yalnız beyan edilen satırlar mı (diff'i kendin say; başka bir bölüm değişmiş mi; AC, UIC, RC satırları yerinde mi)?
- Künye: status: ready-for-dev paket standardının izin verdiği bir değer mi; build_gate / operational_runtime_gate closed kalırken ready-for-dev çelişki yaratıyor mu (P1 yalnız belge ve kanıt); approval_status satırının kaldırılması kapının kuralıyla (ct_queue.py:1307: yok ya da approved) ve standartla tutarlı mı; başlık bandı onayın approvals.tsv satırında durduğunu doğru söylüyor mu?
- Her atıf düzeltmesi: hedef dosyada satırında doğru mu (ZIP'teki dosyadan ölç); düzeltme başka bir yerde eski atfı bırakmış mı (paket içinde ara)?
- UIC tablosu: 32 satırın hepsi aynı hücre sayısında mı (kaçışlanmış dikey çizgiyi ayırıcı sayma); UIC kimlikleri tekil mi; RC 24 satır mı?
- Onay yolu: rev 07'ye açılacak tek onay satırının önünde başka engel görüyor musun?
- İsteğe bağlı: main'deki DL-1 spec.md §9 ve compliance-checklist.md bu düzeltmelerle tutarsız kalıyor mu (ör. kontrol listesindeki UIC-15 hücresi paketten farklılaştı mı)? Bu, paketin değil P1-03 işinin konusudur; bulgu değil not olarak yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ ffeaae570571960678256544d1a6c3856123466ae19ad2895b61ecef76cab3db
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-18 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-19 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 18'den başlar; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-07/MOD-0286-review-07-ecbed8701.zip
sha256 58bfcedf5e8e499b40287ade5e149c4386c0e1b4050542a0251131dbb1143d4f
```
