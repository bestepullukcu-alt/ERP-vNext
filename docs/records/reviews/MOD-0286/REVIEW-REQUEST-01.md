# MOD-0286 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `33dc5e146bbe6941bec17c9119b17024b03fc04c4e76e7e86d3dd2a7c25dea09`
- Kaynak commit: 79f87c1bf (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH (`scripts/pack_approval.py card`)
- Önceki inceleme: yok (paketin ilk sürümü). `pack_approval.py check` → çıkış 1 "onay yok" (beklenen).
- Bu sürümün nedeni: sahip 2026-10-22'de MOD-0286'yı başlatma kararı verdi ("ok başlayalım"); kapsam P1 = Tasarım Sistemi
  Şartnamesi (DL-1) + UI Uyum Kontrol Listesi (DL-2); Blueprint'in beş konsol sayfası P2 (DL-3, ertelendi). MOD-0286 kayıt
  satırı `main`'de yoktu; 2026-10-19 sahip kararındaki satır (`b06a86696`, yalnız `origin/test/crm-content-visit-e2e`) aynı
  dala `41901c477` ile taşındı.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor; paketi Claude (`module-pack-author`) yazdı, inceleyenin başka araç olması
  tercih edilir. Sahip Claude seçerse CT ayrı bir bağlı oturum açar; yazan oturum incelemez.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp
  `reviews.tsv` satırına yazılır.
- ZIP: `MOD-0286-review-01-79f87c1bf.zip` · sha256 `c96731c7b427c208825fd76560abc4d52cd149aa5438f378b0986d16302d76cb` ·
  601 250 bayt · 82 dosya + `MANIFEST.sha256` (83)
  - Yeri (S-29): CT worktree'sinde git'in izlemediği `.ct-teslim/MOD-0286-review-01/`; depoya girmez, sahip iletir.
  - 71 kaynak dosya `git show 79f87c1bf:<yol>` baytlarıyla alındı (paket, kayıt, PSS domain-config, onay politikaları ve
    tsv'ler, AGENTS.md ve çekirdek, 20 kural dosyası, DEV-0000/0001 ve MOD-0014 / MOD-0009-FU03 / MOD-0220 paketleri,
    işletim kartı, platform README, Platform denetim defteri, 7 betik, Golden Slim görünüm/JS/resx seti, paylaşılan
    modal/toast/liste kabuğu/DataTable dosyaları, `backbone-custom.css`).
  - `excerpts/` (11 dosya): Blueprint 8.8 satırları (MOD-0286 + andığı kimlikler, bütün sayfalar), kayıt satırları,
    onay kartı ve `check` çıktısı, Golden Slim doğrulayıcı çıktısı (kırmızı, 1 açık), nav anahtar kontrolü (kırmızı,
    MOD-0176/0177 kaynaklı), kimlik ön kontrolü (OK), keşif raporu (çıkış 3), git log + kayıt satırı diff'i, backlog
    alıntıları (BL-100/112/236/337/440/655), CT ölçümleri (sayılar ve satır çapaları).
- Beyanlar (inceleyen bilsin):
  - Paketi `module-pack-author` alt ajanı yazdı (model: oturumun modeli, Fable 5.1; effort high, ajan tanımından ölçüldü);
    CT brief verdi, sonucu denetledi: andığı her yol, satır çapası ve sayı yeniden ölçüldü ve tutuyor; CT'nin tek
    itirazı (PROD-005 "layout opsiyoneldir" ifadesinin olmadığı) yanlıştı, `frontend-standards.md:208` bunu söylüyor.
  - Paketin kendi bulduğu ve düzeltmediği (XMC-001) üç gerçek sorun pakette bulgu olarak duruyor: Golden Slim doğrulayıcı
    kırmızı (`personalization-client.js`, FU-4), MOD-0297 kayıt çakışması + MOD-0295 satırı yok (D-6 / FU-5), ikinci
    `_ListShell.cshtml` (`Views/PPM/Shared/`, FU-3). Bunları "paket düzeltmedi" diye ENGEL yazmak yerine, paketin
    bunları doğru sınıflandırıp sınıflandırmadığını değerlendirin.
  - Sayım tabanı: paket 169 paket / 177 DtDefaults yazdı; CT ölçümü 170 (MOD-0286'nın kendisi dahil) / 176 (`.min.js`
    hariç). Komutlar `excerpts/measurements.txt`'te; fark ölçüm tabanındandır.
  - `entity_base: GlobalEntity` P1'de varlık yokken yazıldı (şema enum'unda "none" yok; Runtime Constraints'te P2
    gerekçesi var). Kabul edilebilir mi, inceleyen söyler.
  - Paket gövdesi İngilizce, standart terimleri Türkçe (MOD-0230 rev 05 emsali).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-33dc5e14.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict …
  --reviewer codex|claude --model … --effort … --reviewer-session … --author K3 --author-session 05ce4e4b --requested-by GMG-ERP`
  → `reviews.tsv` satırı → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` → `check`
  → tek PR en sonda, satırı bestepullukcu-alt merge eder. Her adım sahibin açık onayıyla.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 01 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-01-79f87c1bf.zip · sha256 c96731c7b427c208825fd76560abc4d52cd149aa5438f378b0986d16302d76cb · kaynak commit 79f87c1bf (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 33dc5e146bbe6941bec17c9119b17024b03fc04c4e76e7e86d3dd2a7c25dea09 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Paket nedir: MOD-0286 "UI Design System & Page Pattern Governance" (Blueprint 8.8, Platform & Shared Services, W-1, Not Started). Bu sürüm P1'dir: iki belge (tasarım sistemi şartnamesi DL-1, UI uyum kontrol listesi DL-2, UIC-1…25) ve bir kimlik uzayı; çalışma zamanı sayfası, varlık, endpoint, izin, Ocelot rotası yok. Blueprint'in beş konsol sayfası P2 (DL-3), ertelendi ve WP'ye verilemez. status: draft, approval_status: none, iki kapı kapalı.

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/measurements.txt, excerpts/registry.txt, excerpts/blueprint-8.8-rows.txt, excerpts/pack-approval-card.txt, excerpts/datatable-verifier-golden-slim.txt, excerpts/git-log.txt, excerpts/product-backlog-excerpts.txt, excerpts/discovery-report.txt; .antigravity/rules/module-pack-standard.md (§2, §6, §7, §10.1, §16); .antigravity/rules/docs-organization.md; on bir UI kural dosyası (.antigravity/rules/frontend-standards.md, frontend-datatable-template.md, frontend-details-template.md, frontend-form-template.md, frontend-js-standard.md, premium-modal-standard.md, page-help-and-keyboard.md, views-organization.md, unauthorized-surface-standard.md, localization-standard.md, dynamic-localization-standard.md); audit-trail-standard.md; record-coding-standard.md; platform-global-search-registry.md; routes.md; cross-module-change-consent.md; execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md ve DEV-0001-golden-reference-compact.md; execution/domains/platform-shared-services/domain-config.md; execution/registries/module-id-registry.md (satır 70, 145, 164, 189 ve MOD-0295'in yokluğu); AGENTS.md §1 ve §6.1; .claude/rules/agents-core.md; execution/approvals/POLICY-REVIEW-START.md; docs/guides/operations/control-tower-operating-card.md §7 (onay sınırları); frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/* ve wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/* (UIC komutlarının hedefi); Views/Shared/_GlobalConfirmation.cshtml, _GlobalNotification.cshtml, Views/Shared/Components/DataTable/_ListShell.cshtml, Views/PPM/Shared/_ListShell.cshtml, wwwroot/assets/js/diten-datatable.js, dt-defaults.js, personalization-client.js, wwwroot/assets/css/backbone-custom.css (paketin andığı canlı kod).
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki dosyalara karşı statik olarak kendin ölç (satır sayısı, satır çapası, grep).

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Kimlik: frontmatter, kayıt satırı 164 ve Blueprint 8.8 satırı birbirini tutuyor mu; MOD-0046/BL-655 (D-3) ve MOD-0297/MOD-0295 (D-6) pakette "karar verilmedi" olarak mı duruyor, karar gibi yazılmış bir yer var mı?
- Kapsam ve yetki: P1'in hiçbir cümlesi kod, `.antigravity` değişikliği ya da başka paket düzeltmesi yetkisi veriyor mu; D-2, D-5, FU-1 sahip commit'i olarak mı işaretli; FU-3/4/5/6 XMC-001'e uygun "sorulacak" mı; P2 (DL-3) her yerde ertelenmiş ve WP'ye verilemez mi?
- 22 zorunlu bölüm (module-pack-standard §6): hepsi var mı; "P1'de yok" cevapları gerekçeli ve P2 taslağı "bağlayıcı değil" mi; `entity_base: GlobalEntity` P1'de varlık yokken kabul edilebilir mi, yoksa frontmatter yanıltıyor mu?
- Kabul ölçütleri (§7): AC-1…AC-14 biçimi (`AC-<n> (zorunlu|isteğe bağlı) — …`, tek başlık); her madde ölçülebilir mi (komut ya da adı belli kanıt); AC-5 ve AC-13'ün `run_record.py` bağlaması çalışır mı; belirsiz ifade var mı?
- UIC listesi (DL-2, 25 madde): kimlikler tekil mi; "auto" maddelerin komutları ZIP'teki Golden Slim dosyalarına karşı gerçekten koşulabilir mi (yol, bayrak, beklenen çıkış); "elle" maddeler kanıt biçimini adlandırıyor mu; on bir kural dosyasında olup listede olmayan zorunlu bir kural var mı; listede olup hiçbir kurala dayanmayan madde var mı; kaynak sütunundaki satır çapaları doğru mu?
- İddia ve kanıt: Module Summary'deki sayılar (3187 satır, 169/9/15 paket, 192/177/37/81 liste, 629/270 sayfa) excerpts/measurements.txt ile tutuyor mu; satır çapaları (frontend-standards.md:208, premium-modal-standard.md 27–52, AGENTS.md:139, _GlobalConfirmation.cshtml:206, diten-datatable.js:251/:961, frontend-datatable-template.md:153/:628, MOD-0220 paketi satır 562) doğru mu?
- Çatışma kaydı: üç çatışma gerçek mi (premium-modal inline style vs FG-003; frontend-standards.md:208 "opsiyoneldir" vs module-pack-standard §2 açık layout; MOD-0013 etiketi); "kural kazanır" çözümü yetki sırasına uygun mu?
- Blueprint uyumu: beş yumuşak sayfa P2'ye eksiksiz eşlenmiş mi; UI-GOV-BUNDLE anılmış mı; Dependencies tablosundaki depo durumları registry.txt ile tutuyor mu; tüketiciler doğru mu?
- Belge yeri (D-1): öneri docs-organization §1 beş soru ve §3.1 satırıyla tutuyor mu; `docs/` köküne dosya koyma riski var mı (AC-10)?
- Teslim dilimleri: her AC tek bir dilimde mi; DL-1/DL-2 kapsamı bölümlerle tutuyor mu; DL-3 "teslim edilmez" açık mı; §20.4-6 kuralına (bütün dilimler tek WP'de olamaz) uygun mu?
- Denetim ve kayıt kodu: Audited Events'te P1 için "yazma komutu yok" doğru okuma mı (AUD-001 §10.1); P2 satırları yol a ile tutarlı mı; RCS-001 cevabı ("yok"; P2 OTOMATİK önerisi, sahip kararı) kurala uygun mu?
- Dil: platform 2 / kiracı 7 dil ifadeleri doğru yerde mi; P1 belgelerinin dili konusunda bir kural ihlali var mı?
- Genel: hiçbir şey onaylı/kapalı gösterilmiyor mu; `owner: TBD` ve `target: TBD` ready-for-dev öncesi açık mı; Follow-up Items'ın regresyon riski işaretleri (🔴/🟡/🟢) gerekçeli mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 33dc5e146bbe6941bec17c9119b17024b03fc04c4e76e7e86d3dd2a7c25dea09
değerlendirilen olumsuz incelemeler: yok
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya: `.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-01/MOD-0286-review-01-79f87c1bf.zip` · sha256 `c96731c7b427c208825fd76560abc4d52cd149aa5438f378b0986d16302d76cb`
